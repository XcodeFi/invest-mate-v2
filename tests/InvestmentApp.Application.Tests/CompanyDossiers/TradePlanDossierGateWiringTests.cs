using FluentAssertions;
using InvestmentApp.Application.CompanyDossiers.Gate;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.TradePlans.Commands.CreateTradePlan;
using InvestmentApp.Application.TradePlans.Commands.UpdateTradePlan;
using InvestmentApp.Application.TradePlans.Queries.GetTradePlans;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.CompanyDossiers;

public class TradePlanDossierGateWiringTests
{
    private readonly Mock<ICompanyDossierGate> _gate = new();

    [Fact]
    public async Task Create_WhenGateBlocks_ShouldThrowBeforePersisting()
    {
        _gate.Setup(g => g.EnsureAsync("user-1", "HPG", It.IsAny<decimal>(),
                It.IsAny<decimal?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DossierGateException("HPG",
                DossierGateResult.Fail("missing")));

        var handler = TestFactory.CreateTradePlanHandler(_gate.Object, out var repo);
        var command = TestFactory.CreateCommand(userId: "user-1", symbol: "HPG");

        var act = () => handler.Handle(command, default);

        await act.Should().ThrowAsync<DossierGateException>();
        repo.Verify(r => r.AddAsync(It.IsAny<TradePlan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_WithStatusExecuted_ShouldStillRunGateFirst()
    {
        _gate.Setup(g => g.EnsureAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<decimal>(), It.IsAny<decimal?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DossierGateException("HPG",
                DossierGateResult.Fail("missing")));

        var handler = TestFactory.CreateTradePlanHandler(_gate.Object, out var repo);
        var command = TestFactory.CreateCommand(userId: "user-1", symbol: "HPG");
        command.Status = "Executed";
        command.TradeId = "trade-1";

        var act = () => handler.Handle(command, default);

        await act.Should().ThrowAsync<DossierGateException>();
        // Assert "có throw" là không đủ: mock gate luôn throw nên test pass y như nhau
        // dù gate chạy trước hay sau khối auto-transition. Phải chứng minh plan chưa
        // bao giờ được ghi xuống, đó mới là bằng chứng gate chặn TRƯỚC.
        repo.Verify(r => r.AddAsync(It.IsAny<TradePlan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_WhenGatePasses_ShouldPassPlanSizeAndBalance()
    {
        var handler = TestFactory.CreateTradePlanHandler(_gate.Object, out _);
        var command = TestFactory.CreateCommand(userId: "user-1", symbol: "HPG");
        command.Quantity = 100;
        command.EntryPrice = 80_000m;
        command.AccountBalance = 100_000_000m;

        await handler.Handle(command, default);

        _gate.Verify(g => g.EnsureAsync("user-1", "HPG",
            8_000_000m, 100_000_000m, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_LotsSumExceedsThreshold_ShouldGateOnLotsDerivedSizeNotHeaderSize()
    {
        // Cửa hậu lots: header quantity=1 → planSize theo header chỉ 100đ (bậc nhỏ),
        // nhưng SetLots sẽ ghi đè Quantity thành tổng lots (100.000) trước khi lưu.
        // Cổng phải chấm theo số đó (100.000 × 100 = 10tr = 10% > 5%), không phải header.
        _gate.Setup(g => g.EnsureAsync("user-1", "HPG", 10_000_000m,
                100_000_000m, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DossierGateException("HPG", DossierGateResult.Fail("insufficient")));

        var handler = TestFactory.CreateTradePlanHandler(_gate.Object, out var repo);
        var command = TestFactory.CreateCommand(userId: "user-1", symbol: "HPG");
        command.Quantity = 1;
        command.EntryPrice = 100m;
        command.AccountBalance = 100_000_000m;
        command.EntryMode = "Single";
        command.Lots = new List<PlanLotDto>
        {
            new() { LotNumber = 1, PlannedPrice = 100m, PlannedQuantity = 60_000 },
            new() { LotNumber = 2, PlannedPrice = 100m, PlannedQuantity = 40_000 }
        };

        var act = () => handler.Handle(command, default);

        await act.Should().ThrowAsync<DossierGateException>();
        _gate.Verify(g => g.EnsureAsync("user-1", "HPG", 10_000_000m,
            100_000_000m, It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.AddAsync(It.IsAny<TradePlan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_WhenSizeCrossesThresholdUpward_ShouldRunGateWithNewSize()
    {
        var handler = TestFactory.UpdateTradePlanHandler(_gate.Object,
            existingQuantity: 20, existingEntryPrice: 100_000m, accountBalance: 100_000_000m);
        var command = TestFactory.UpdateCommand(quantity: 120, entryPrice: 100_000m);

        await handler.Handle(command, default);

        _gate.Verify(g => g.EnsureAsync("user-1", "HPG",
            12_000_000m, 100_000_000m, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_WhenSizeStaysBelowThreshold_ShouldNotRunGate()
    {
        var handler = TestFactory.UpdateTradePlanHandler(_gate.Object,
            existingQuantity: 20, existingEntryPrice: 100_000m, accountBalance: 100_000_000m);
        var command = TestFactory.UpdateCommand(quantity: 30, entryPrice: 100_000m);

        await handler.Handle(command, default);

        _gate.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Update_WhenPlanAlreadyAboveThreshold_ShouldNotReRunGate()
    {
        var handler = TestFactory.UpdateTradePlanHandler(_gate.Object,
            existingQuantity: 120, existingEntryPrice: 100_000m, accountBalance: 100_000_000m);
        var command = TestFactory.UpdateCommand(quantity: 130, entryPrice: 100_000m);

        await handler.Handle(command, default);

        _gate.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Update_WhenAccountBalanceNull_ShouldNotRunGateRegardlessOfSize()
    {
        var handler = TestFactory.UpdateTradePlanHandler(_gate.Object,
            existingQuantity: 20, existingEntryPrice: 100_000m, accountBalance: null);
        var command = TestFactory.UpdateCommand(quantity: 1000, entryPrice: 1_000_000m);

        await handler.Handle(command, default);

        _gate.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Update_OnlyQuantitySent_ShouldFallBackToExistingEntryPrice()
    {
        // Ghim fallback partial-update. Không có test này thì ai đó đổi lại thành
        // request.EntryPrice thẳng là size về 0, gate im lặng, cửa hậu mở lại.
        var handler = TestFactory.UpdateTradePlanHandler(_gate.Object,
            existingQuantity: 20, existingEntryPrice: 100_000m, accountBalance: 100_000_000m);

        var command = TestFactory.UpdateCommand(quantity: 120, entryPrice: null);

        await handler.Handle(command, default);

        // 120 × 100.000 = 12tr = 12% > 5%
        _gate.Verify(g => g.EnsureAsync(It.IsAny<string>(), It.IsAny<string>(),
            12_000_000m, 100_000_000m, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_QuantityAndEntryPriceOmittedNoLots_ShouldFallBackToPlanValues()
    {
        // Bổ sung cho Update_OnlyQuantitySent_ShouldFallBackToExistingEntryPrice (chỉ ghim
        // entryPrice fallback): ở đây CẢ quantity và entryPrice đều bỏ trống, không có lots —
        // ResolveEffectiveGateInputs phải lấy cả hai từ plan (40 × 100.000), không phải 0.
        // Kích hoạt cổng bằng hạ balance qua ngưỡng, độc lập với quantity/entryPrice.
        var handler = TestFactory.UpdateTradePlanHandler(_gate.Object,
            existingQuantity: 40, existingEntryPrice: 100_000m, accountBalance: 100_000_000m);

        var command = new UpdateTradePlanCommand
        {
            Id = "plan-1",
            UserId = "user-1",
            AccountBalance = 50_000_000m
        };

        await handler.Handle(command, default);

        _gate.Verify(g => g.EnsureAsync("user-1", "HPG",
            4_000_000m, 50_000_000m, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_AccountBalanceOmitted_ShouldFallBackToPlanAccountBalance()
    {
        // AccountBalance không gửi lên — effective balance dùng để chấm ngưỡng và truyền
        // cho gate phải lấy từ plan hiện tại (80tr), không phải null. Kích hoạt cổng bằng
        // đổi mã (luôn chấm bất kể ngưỡng), số còn lại giữ nguyên so với plan.
        var handler = TestFactory.UpdateTradePlanHandler(_gate.Object,
            existingQuantity: 10, existingEntryPrice: 50_000m, accountBalance: 80_000_000m);

        var command = TestFactory.UpdateCommandWithSymbolOnly("VNM");

        await handler.Handle(command, default);

        _gate.Verify(g => g.EnsureAsync("user-1", "VNM",
            500_000m, 80_000_000m, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_SymbolOmitted_ShouldFallBackToPlanSymbol()
    {
        // Symbol không gửi lên — effective symbol dùng để chấm cổng phải lấy từ mã hiện
        // tại của plan (đã chuẩn hoá hoa), không phải null/rỗng. Kích hoạt cổng bằng nâng
        // quantity qua ngưỡng, dùng bộ số riêng để không trùng test khác.
        var handler = TestFactory.UpdateTradePlanHandler(_gate.Object,
            existingQuantity: 8, existingEntryPrice: 70_000m, accountBalance: 100_000_000m);
        var command = TestFactory.UpdateCommand(quantity: 100, entryPrice: 70_000m);

        await handler.Handle(command, default);

        _gate.Verify(g => g.EnsureAsync("user-1", "HPG",
            7_000_000m, 100_000_000m, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_LoweringAccountBalanceAcrossThreshold_ShouldRunGate()
    {
        // Cửa hậu đường số dư: size mới 4tr vẫn dưới ngưỡng CŨ (5tr), nhưng cùng
        // request hạ số dư còn 50tr nên tỷ lệ thật thành 8%.
        var handler = TestFactory.UpdateTradePlanHandler(_gate.Object,
            existingQuantity: 20, existingEntryPrice: 100_000m, accountBalance: 100_000_000m);

        var command = TestFactory.UpdateCommand(quantity: 40, entryPrice: 100_000m);
        command.AccountBalance = 50_000_000m;

        await handler.Handle(command, default);

        _gate.Verify(g => g.EnsureAsync(It.IsAny<string>(), It.IsAny<string>(),
            4_000_000m, 50_000_000m, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_SymbolChangedAndCrossesThreshold_ShouldRunGateWithNewSymbol()
    {
        // Đổi mã A→B kèm nâng size lên ngưỡng lớn. Cửa hậu cũ chấm A (mã cũ) — A đỗ,
        // nhưng vị thế thật lại mở ở B, B không có hồ sơ nào. Phải chấm mã MỚI.
        var handler = TestFactory.UpdateTradePlanHandler(_gate.Object,
            existingQuantity: 20, existingEntryPrice: 100_000m, accountBalance: 100_000_000m);
        var command = TestFactory.UpdateCommand(quantity: 120, entryPrice: 100_000m, symbol: "VNM");

        await handler.Handle(command, default);

        _gate.Verify(g => g.EnsureAsync("user-1", "VNM",
            12_000_000m, 100_000_000m, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_SymbolChangedButStillBelowThreshold_ShouldRunGateWithNewSymbol()
    {
        // Đổi mã A→B, size không đổi và vẫn dưới ngưỡng. wasBelow&&isNowAtOrAbove cũ sẽ
        // là false nên cổng cũ không chạy — nhưng đổi mã là mở vị thế mới ở công ty khác,
        // đường tạo chặn cả lệnh nhỏ (bậc nhỏ đòi BusinessModel) nên đường sửa cũng phải vậy.
        var handler = TestFactory.UpdateTradePlanHandler(_gate.Object,
            existingQuantity: 20, existingEntryPrice: 100_000m, accountBalance: 100_000_000m);
        var command = TestFactory.UpdateCommand(quantity: 20, entryPrice: 100_000m, symbol: "VNM");

        await handler.Handle(command, default);

        _gate.Verify(g => g.EnsureAsync("user-1", "VNM",
            2_000_000m, 100_000_000m, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_SymbolChangedAndGateBlocks_ShouldNotPersist()
    {
        // Đường sửa hiện chưa có test nào ghim việc cổng chặn được phần ghi xuống DB —
        // đường tạo có (Create_WhenGateBlocks_ShouldThrowBeforePersisting), đường sửa không.
        _gate.Setup(g => g.EnsureAsync("user-1", "VNM", It.IsAny<decimal>(),
                It.IsAny<decimal?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DossierGateException("VNM", DossierGateResult.Fail("missing")));

        var handler = TestFactory.UpdateTradePlanHandler(_gate.Object,
            existingQuantity: 20, existingEntryPrice: 100_000m, accountBalance: 100_000_000m,
            out var repository);
        var command = TestFactory.UpdateCommand(quantity: 120, entryPrice: 100_000m, symbol: "VNM");

        var act = () => handler.Handle(command, default);

        await act.Should().ThrowAsync<DossierGateException>();
        repository.Verify(r => r.UpdateAsync(It.IsAny<TradePlan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_SymbolUnchangedAndBelowThreshold_ShouldNotRunGate()
    {
        // Giữ nguyên "plan có rồi thì thôi" — không đổi mã, không vượt ngưỡng.
        var handler = TestFactory.UpdateTradePlanHandler(_gate.Object,
            existingQuantity: 20, existingEntryPrice: 100_000m, accountBalance: 100_000_000m);
        var command = TestFactory.UpdateCommand(quantity: 25, entryPrice: 100_000m);

        await handler.Handle(command, default);

        _gate.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Update_LotsSumExceedsThresholdWithQuantityUnset_ShouldGateOnLotsDerivedSize()
    {
        // Cửa hậu lots trên đường sửa: quantity không gửi lên (fallback về plan.Quantity=20,
        // dưới ngưỡng), nhưng lots cộng lại 60 × entryPrice cũ 100.000 = 6tr = 6% > 5%.
        // SetLots sẽ ghi đè Quantity thành 60 trước khi lưu, cổng phải chấm theo số đó.
        _gate.Setup(g => g.EnsureAsync("user-1", "HPG", 6_000_000m,
                100_000_000m, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DossierGateException("HPG", DossierGateResult.Fail("insufficient")));

        var handler = TestFactory.UpdateTradePlanHandler(_gate.Object,
            existingQuantity: 20, existingEntryPrice: 100_000m, accountBalance: 100_000_000m,
            out var repo);
        var command = TestFactory.UpdateCommandWithLots(new List<PlanLotDto>
        {
            new() { LotNumber = 1, PlannedPrice = 100_000m, PlannedQuantity = 40 },
            new() { LotNumber = 2, PlannedPrice = 100_000m, PlannedQuantity = 20 }
        });

        var act = () => handler.Handle(command, default);

        await act.Should().ThrowAsync<DossierGateException>();
        _gate.Verify(g => g.EnsureAsync("user-1", "HPG", 6_000_000m,
            100_000_000m, It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.UpdateAsync(It.IsAny<TradePlan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static class TestFactory
    {
        public static CreateTradePlanCommandHandler CreateTradePlanHandler(
            ICompanyDossierGate gate, out Mock<ITradePlanRepository> repository)
        {
            repository = new Mock<ITradePlanRepository>();
            return new CreateTradePlanCommandHandler(
                repository.Object,
                Mock.Of<ITradeRepository>(),
                gate);
        }

        public static CreateTradePlanCommand CreateCommand(string userId, string symbol) => new()
        {
            UserId = userId,
            Symbol = symbol,
            Direction = "Buy",
            EntryPrice = 10_000m,
            StopLoss = 9_000m,
            Target = 12_000m,
            Quantity = 100
        };

        public static UpdateTradePlanCommandHandler UpdateTradePlanHandler(
            ICompanyDossierGate gate, int existingQuantity, decimal existingEntryPrice, decimal? accountBalance)
            => UpdateTradePlanHandler(gate, existingQuantity, existingEntryPrice, accountBalance, out _);

        public static UpdateTradePlanCommandHandler UpdateTradePlanHandler(
            ICompanyDossierGate gate, int existingQuantity, decimal existingEntryPrice, decimal? accountBalance,
            out Mock<ITradePlanRepository> repository)
        {
            repository = new Mock<ITradePlanRepository>();
            var plan = new TradePlan("user-1", "HPG", "Buy",
                existingEntryPrice, existingEntryPrice * 0.9m, existingEntryPrice * 1.2m, existingQuantity,
                accountBalance: accountBalance,
                thesis: "Thesis đủ dài để không vướng gate kỷ luật thesis khi test luồng sửa plan.");
            repository.Setup(r => r.GetByIdAsync("plan-1", It.IsAny<CancellationToken>())).ReturnsAsync(plan);
            return new UpdateTradePlanCommandHandler(repository.Object, gate);
        }

        public static UpdateTradePlanCommand UpdateCommand(int quantity, decimal? entryPrice, string? symbol = null) => new()
        {
            Id = "plan-1",
            UserId = "user-1",
            Symbol = symbol,
            Quantity = quantity,
            EntryPrice = entryPrice
        };

        public static UpdateTradePlanCommand UpdateCommandWithSymbolOnly(string symbol) => new()
        {
            Id = "plan-1",
            UserId = "user-1",
            Symbol = symbol
        };

        public static UpdateTradePlanCommand UpdateCommandWithLots(List<PlanLotDto> lots) => new()
        {
            Id = "plan-1",
            UserId = "user-1",
            EntryMode = "Single",
            Lots = lots
        };
    }
}
