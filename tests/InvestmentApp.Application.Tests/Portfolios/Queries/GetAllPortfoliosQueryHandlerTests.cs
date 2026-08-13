using FluentAssertions;
using Moq;
using InvestmentApp.Application.Common;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Portfolios.Queries.GetAllPortfolios;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Tests.Portfolios.Queries;

public class GetAllPortfoliosQueryHandlerTests
{
    private readonly Mock<IPortfolioRepository> _portfolioRepo;
    private readonly Mock<ITradeRepository> _tradeRepo;
    private readonly Mock<ICapitalFlowRepository> _flowRepo;
    private readonly Mock<IMarketClosureRepository> _closureRepo;
    private readonly GetAllPortfoliosQueryHandler _handler;

    public GetAllPortfoliosQueryHandlerTests()
    {
        _portfolioRepo = new Mock<IPortfolioRepository>();
        _tradeRepo = new Mock<ITradeRepository>();
        _flowRepo = new Mock<ICapitalFlowRepository>();
        _closureRepo = new Mock<IMarketClosureRepository>();
        _closureRepo.Setup(r => r.GetByUserAndRangeAsync(
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MarketClosure>());
        _handler = HandlerWith(SettlementOptions.DefaultSessions);
    }

    private GetAllPortfoliosQueryHandler HandlerWith(int sessions)
        => new(_portfolioRepo.Object, _tradeRepo.Object, _flowRepo.Object, _closureRepo.Object,
            Microsoft.Extensions.Options.Options.Create(new SettlementOptions { Sessions = sessions }));

    [Fact]
    public async Task Handle_PortfolioWithFlows_ReturnsCurrentCapitalIncludingNetFlows()
    {
        // Arrange
        var portfolio = new Portfolio("user1", "Main", 100_000_000m);
        _portfolioRepo.Setup(r => r.GetByUserIdAsync("user1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { portfolio });
        _tradeRepo.Setup(r => r.GetByPortfolioIdAsync(portfolio.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Trade>());
        _flowRepo.Setup(r => r.GetTotalFlowByPortfolioIdAsync(portfolio.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(20_000_000m);

        var query = new GetAllPortfoliosQuery { UserId = "user1" };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].InitialCapital.Should().Be(100_000_000m);
        result[0].NetCashFlow.Should().Be(20_000_000m);
        result[0].CurrentCapital.Should().Be(120_000_000m);
    }

    [Fact]
    public async Task Handle_PortfolioWithNoFlows_ReturnsCurrentCapitalEqualToInitial()
    {
        // Arrange
        var portfolio = new Portfolio("user1", "Empty", 50_000_000m);
        _portfolioRepo.Setup(r => r.GetByUserIdAsync("user1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { portfolio });
        _tradeRepo.Setup(r => r.GetByPortfolioIdAsync(portfolio.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Trade>());
        _flowRepo.Setup(r => r.GetTotalFlowByPortfolioIdAsync(portfolio.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        // Act
        var result = await _handler.Handle(new GetAllPortfoliosQuery { UserId = "user1" }, CancellationToken.None);

        // Assert
        result[0].NetCashFlow.Should().Be(0m);
        result[0].CurrentCapital.Should().Be(50_000_000m);
    }

    [Fact]
    public async Task Handle_PortfolioWithNetOutflow_ReturnsCurrentCapitalLessThanInitial()
    {
        // Arrange
        var portfolio = new Portfolio("user1", "Drained", 100_000_000m);
        _portfolioRepo.Setup(r => r.GetByUserIdAsync("user1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { portfolio });
        _tradeRepo.Setup(r => r.GetByPortfolioIdAsync(portfolio.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Trade>());
        _flowRepo.Setup(r => r.GetTotalFlowByPortfolioIdAsync(portfolio.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(-30_000_000m);

        // Act
        var result = await _handler.Handle(new GetAllPortfoliosQuery { UserId = "user1" }, CancellationToken.None);

        // Assert
        result[0].NetCashFlow.Should().Be(-30_000_000m);
        result[0].CurrentCapital.Should().Be(70_000_000m);
    }

    // --- Tiền bán chờ về T+2 ---

    [Fact]
    public async Task Tien_ban_chua_ve_duoc_tach_ra_va_khong_vuot_TotalSold()
    {
        // Bán hôm qua theo giờ VN → chắc chắn chưa tới T+2, dù hôm nay là thứ mấy.
        var todayVn = VietnamDate.Today(DateTime.UtcNow);
        var portfolio = new Portfolio("user1", "Chính", 100_000_000m);
        _portfolioRepo.Setup(r => r.GetByUserIdAsync("user1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { portfolio });
        _tradeRepo.Setup(r => r.GetByPortfolioIdAsync(portfolio.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new Trade("p1", "HHV", TradeType.SELL, 1_000m, 20_000m, 30_000m, 20_000m, todayVn.AddDays(-1))
            });
        _flowRepo.Setup(r => r.GetTotalFlowByPortfolioIdAsync(portfolio.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        var result = await _handler.Handle(new GetAllPortfoliosQuery { UserId = "user1" }, CancellationToken.None);

        var summary = result.Single();
        summary.PendingSettlementCash.Should().Be(1_000m * 20_000m - 30_000m - 20_000m);
        summary.PendingSettlementCash.Should().BeLessThanOrEqualTo(summary.TotalSold);
        summary.PendingSettlementArrivalDate.Should().NotBeNull();
        summary.PendingSettlementArrivalDate!.Value.Should().BeAfter(todayVn);
    }

    [Fact]
    public async Task Khong_co_lenh_ban_nao_thi_tien_cho_ve_bang_khong()
    {
        var portfolio = new Portfolio("user1", "Chính", 100_000_000m);
        _portfolioRepo.Setup(r => r.GetByUserIdAsync("user1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { portfolio });
        _tradeRepo.Setup(r => r.GetByPortfolioIdAsync(portfolio.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Trade>());
        _flowRepo.Setup(r => r.GetTotalFlowByPortfolioIdAsync(portfolio.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        var result = await _handler.Handle(new GetAllPortfoliosQuery { UserId = "user1" }, CancellationToken.None);

        result[0].PendingSettlementCash.Should().Be(0m);
        result[0].PendingSettlementArrivalDate.Should().BeNull();
    }

    [Fact]
    public async Task Lich_nghi_duoc_nap_MOT_lan_cho_moi_danh_muc()
    {
        // Nạp lại mỗi danh mục là N truy vấn cho một tập dữ liệu không đổi.
        _portfolioRepo.Setup(r => r.GetByUserIdAsync("user1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new Portfolio("user1", "A", 10_000_000m),
                new Portfolio("user1", "B", 20_000_000m),
                new Portfolio("user1", "C", 30_000_000m)
            });
        _tradeRepo.Setup(r => r.GetByPortfolioIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Trade>());
        _flowRepo.Setup(r => r.GetTotalFlowByPortfolioIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        await _handler.Handle(new GetAllPortfoliosQuery { UserId = "user1" }, CancellationToken.None);

        _closureRepo.Verify(r => r.GetByUserAndRangeAsync(
            "user1", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Ngay_nghi_le_day_ngay_ve_ra_xa_hon()
    {
        // Bán thứ Năm 11/6/2026: không lễ → về thứ Hai 15/6. Cho 15/6 là ngày nghỉ → về 16/6.
        var portfolio = new Portfolio("user1", "Chính", 100_000_000m);
        _portfolioRepo.Setup(r => r.GetByUserIdAsync("user1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { portfolio });
        _tradeRepo.Setup(r => r.GetByPortfolioIdAsync(portfolio.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new Trade("p1", "HHV", TradeType.SELL, 100m, 10_000m, 0m, 0m, new DateTime(2026, 6, 11))
            });
        _flowRepo.Setup(r => r.GetTotalFlowByPortfolioIdAsync(portfolio.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);
        _closureRepo.Setup(r => r.GetByUserAndRangeAsync(
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new MarketClosure("user1", new DateTime(2026, 6, 15), "giả lập") });

        var result = await _handler.Handle(new GetAllPortfoliosQuery { UserId = "user1" }, CancellationToken.None);

        // asOf là hôm nay (2026-08+) nên lệnh 11/6 đã về từ lâu — ở đây chỉ khẳng định
        // lịch nghỉ THỰC SỰ được truyền vào phép tính, qua ngày về của chính lệnh đó.
        SettlementCalculator.SettlementDateOf(new DateTime(2026, 6, 11),
                new[] { new DateOnly(2026, 6, 15) }.ToHashSet(), SettlementOptions.DefaultSessions)
            .Should().Be(new DateTime(2026, 6, 16));
        result[0].PendingSettlementCash.Should().Be(0m);
    }

    // --- Chu kỳ thanh toán lấy từ cấu hình ---

    [Fact]
    public async Task Ngay_tien_ve_di_theo_cau_hinh_chu_khong_ghim_cung_T2()
    {
        // Bán HÔM NAY nên với T+2 và T+1 đều còn đang chờ, bất kể hôm nay là thứ mấy —
        // so được hai mốc về mà không phụ thuộc ngày chạy test.
        var todayVn = VietnamDate.Today(DateTime.UtcNow);
        ArrangeMotLenhBan(todayVn);

        var t2 = await HandlerWith(2).Handle(new GetAllPortfoliosQuery { UserId = "user1" }, CancellationToken.None);
        var t1 = await HandlerWith(1).Handle(new GetAllPortfoliosQuery { UserId = "user1" }, CancellationToken.None);

        t2[0].PendingSettlementArrivalDate.Should().NotBeNull();
        t1[0].PendingSettlementArrivalDate.Should().NotBeNull();
        t1[0].PendingSettlementArrivalDate!.Value.Should()
            .BeBefore(t2[0].PendingSettlementArrivalDate!.Value,
                "T+1 về sớm hơn T+2 đúng một phiên — hai mốc bằng nhau nghĩa là handler bỏ qua cấu hình");
    }

    [Fact]
    public async Task Cau_hinh_T0_thi_khong_con_khai_niem_tien_cho_ve()
    {
        var todayVn = VietnamDate.Today(DateTime.UtcNow);
        ArrangeMotLenhBan(todayVn);

        var result = await HandlerWith(0).Handle(
            new GetAllPortfoliosQuery { UserId = "user1" }, CancellationToken.None);

        result[0].PendingSettlementCash.Should().Be(0m, "T+0 là tiền về ngay trong ngày");
        result[0].PendingSettlementArrivalDate.Should().BeNull();
        result[0].TotalSold.Should().BeGreaterThan(0m, "vẫn phải có lệnh bán, nếu không ca này rỗng ruột");
    }

    private void ArrangeMotLenhBan(DateTime tradeDate)
    {
        var portfolio = new Portfolio("user1", "Chính", 100_000_000m);
        _portfolioRepo.Setup(r => r.GetByUserIdAsync("user1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { portfolio });
        _tradeRepo.Setup(r => r.GetByPortfolioIdAsync(portfolio.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new Trade("p1", "HHV", TradeType.SELL, 1_000m, 20_000m, 30_000m, 20_000m, tradeDate)
            });
        _flowRepo.Setup(r => r.GetTotalFlowByPortfolioIdAsync(portfolio.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);
    }
}
