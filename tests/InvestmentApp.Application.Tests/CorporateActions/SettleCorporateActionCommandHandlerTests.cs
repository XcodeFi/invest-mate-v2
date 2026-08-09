using FluentAssertions;
using InvestmentApp.Application.CorporateActions.Commands.SettleCorporateAction;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using Moq;
using Xunit;

namespace InvestmentApp.Application.Tests.CorporateActions;

public class SettleCorporateActionCommandHandlerTests
{
    private readonly Mock<ICorporateActionRepository> _actions = new();
    private readonly Mock<ICapitalFlowRepository> _flows = new();
    private readonly Mock<ITradeRepository> _trades = new();
    private readonly SettleCorporateActionCommandHandler _handler;

    private static readonly DateTime Ex = new(2026, 6, 10);
    private static readonly DateTime SettledAt = new(2026, 7, 20);

    public SettleCorporateActionCommandHandlerTests()
        => _handler = new SettleCorporateActionCommandHandler(_actions.Object, _flows.Object, _trades.Object);

    [Fact]
    public async Task CoTucCoPhieu_ChiDanhDauDaVe_KhongSinhDongTien()
    {
        var action = CorporateAction.StockDividend("p1", "u1", "HPG", 100, 130, Ex, SettledAt);
        _actions.Setup(r => r.GetByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(action);

        await _handler.Handle(new SettleCorporateActionCommand("u1", "a1", SettledAt, null), CancellationToken.None);

        action.SettledAt.Should().Be(SettledAt);
        _flows.Verify(r => r.AddAsync(It.IsAny<CapitalFlow>(), It.IsAny<CancellationToken>()), Times.Never);
        _actions.Verify(r => r.UpdateAsync(action, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CoTucTienMat_SinhDongTienSauThue_TrenSoLuongTaiNgayGDKHQ()
    {
        var action = CorporateAction.CashDividend("p1", "u1", "SAB", 5m, Ex, SettledAt, 5m);
        _actions.Setup(r => r.GetByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(action);
        _actions.Setup(r => r.GetByPortfolioIdAndSymbolAsync("p1", "SAB", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { action });
        _trades.Setup(r => r.GetByPortfolioIdAndSymbolAsync("p1", "SAB", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new Trade("p1", "SAB", TradeType.BUY, 1000, 55_000, tradeDate: new DateTime(2026, 1, 5)) });

        CapitalFlow? created = null;
        _flows.Setup(r => r.AddAsync(It.IsAny<CapitalFlow>(), It.IsAny<CancellationToken>()))
            .Callback<CapitalFlow, CancellationToken>((f, _) => created = f)
            .Returns(Task.CompletedTask);

        await _handler.Handle(new SettleCorporateActionCommand("u1", "a1", SettledAt, null), CancellationToken.None);

        created.Should().NotBeNull();
        created!.Amount.Should().Be(475_000m);
        created.Type.Should().Be(CapitalFlowType.Dividend);
        created.Symbol.Should().Be("SAB");
        // Ngày dòng tiền phải là ngày xác nhận, không bị múi giờ đẩy lùi một ngày
        created.FlowDate.Should().Be(SettledAt);
        action.CapitalFlowId.Should().Be(created.Id);
    }

    [Fact]
    public async Task LienKetDongTienCu_ThiKhongTaoMoi()
    {
        var action = CorporateAction.CashDividend("p1", "u1", "SAB", 5m, Ex, SettledAt, 5m);
        _actions.Setup(r => r.GetByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(action);
        var existing = new CapitalFlow("p1", "u1", CapitalFlowType.Dividend, 475_000m);
        _flows.Setup(r => r.GetByIdAsync("f1", It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        await _handler.Handle(new SettleCorporateActionCommand("u1", "a1", SettledAt, "f1"), CancellationToken.None);

        _flows.Verify(r => r.AddAsync(It.IsAny<CapitalFlow>(), It.IsAny<CancellationToken>()), Times.Never);
        action.CapitalFlowId.Should().Be(existing.Id);
        existing.Symbol.Should().Be("SAB");
        existing.CorporateActionId.Should().Be(action.Id);
    }

    [Fact]
    public async Task LienKetDongTienCuaNguoiKhac_ThiNemUnauthorized_VaKhongGhiDe()
    {
        var action = CorporateAction.CashDividend("p1", "u1", "SAB", 5m, Ex, SettledAt, 5m);
        _actions.Setup(r => r.GetByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(action);

        var foreignFlow = new CapitalFlow("p9", "u2", CapitalFlowType.Dividend, 475_000m);
        _flows.Setup(r => r.GetByIdAsync("f9", It.IsAny<CancellationToken>())).ReturnsAsync(foreignFlow);

        var act = () => _handler.Handle(
            new SettleCorporateActionCommand("u1", "a1", SettledAt, "f9"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        foreignFlow.CorporateActionId.Should().BeNull();
        _flows.Verify(r => r.UpdateAsync(It.IsAny<CapitalFlow>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SuKienCuaNguoiKhac_ThiNemUnauthorized()
    {
        var action = CorporateAction.StockDividend("p1", "u2", "HPG", 100, 130, Ex, null);
        _actions.Setup(r => r.GetByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(action);

        var act = () => _handler.Handle(new SettleCorporateActionCommand("u1", "a1", SettledAt, null), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task DaXacNhanRoi_ThiNemInvalidOperation()
    {
        var action = CorporateAction.StockDividend("p1", "u1", "HPG", 100, 130, Ex, null);
        action.MarkSettled(SettledAt);
        _actions.Setup(r => r.GetByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(action);

        var act = () => _handler.Handle(new SettleCorporateActionCommand("u1", "a1", SettledAt, null), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task DaBanHetTruocNgayGDKHQ_ThiKhongSinhDongTien()
    {
        var action = CorporateAction.CashDividend("p1", "u1", "SAB", 5m, Ex, SettledAt, 5m);
        _actions.Setup(r => r.GetByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(action);
        _actions.Setup(r => r.GetByPortfolioIdAndSymbolAsync("p1", "SAB", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { action });
        _trades.Setup(r => r.GetByPortfolioIdAndSymbolAsync("p1", "SAB", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new Trade("p1", "SAB", TradeType.BUY, 1000, 55_000, tradeDate: new DateTime(2026, 1, 5)),
                new Trade("p1", "SAB", TradeType.SELL, 1000, 60_000, tradeDate: new DateTime(2026, 3, 1))
            });

        await _handler.Handle(new SettleCorporateActionCommand("u1", "a1", SettledAt, null), CancellationToken.None);

        _flows.Verify(r => r.AddAsync(It.IsAny<CapitalFlow>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
