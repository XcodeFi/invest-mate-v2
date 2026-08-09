using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.ValueObjects;
using InvestmentApp.Infrastructure.Services;
using Moq;

namespace InvestmentApp.Infrastructure.Tests.Services;

public class PnLServiceCorporateActionTests
{
    private readonly Mock<ITradeRepository> _trades = new();
    private readonly Mock<IPortfolioRepository> _portfolios = new();
    private readonly Mock<IStockPriceService> _prices = new();
    private readonly Mock<ICorporateActionRepository> _actions = new();

    private const string PortfolioId = "p1";
    private static readonly DateTime Ex = new(2026, 6, 10);

    private PnLService Sut() => new(_trades.Object, _portfolios.Object, _prices.Object, _actions.Object);

    private void SetupPortfolio() =>
        _portfolios.Setup(r => r.GetByIdAsync(PortfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Portfolio("u1", "Danh mục", 100_000_000m));

    private void SetupTrades(params Trade[] trades) =>
        _trades.Setup(r => r.GetByPortfolioIdAsync(PortfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trades);

    private void SetupActions(params CorporateAction[] actions) =>
        _actions.Setup(r => r.GetByPortfolioIdAsync(PortfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(actions);

    private void SetupPrice(decimal price) =>
        _prices.Setup(s => s.GetCurrentPriceAsync(It.IsAny<StockSymbol>()))
            .ReturnsAsync(new Money(price, "VND"));

    [Fact]
    public async Task CoTucCoPhieuChuaVe_ThiKhongCoLoGia()
    {
        SetupPortfolio();
        SetupTrades(new Trade(PortfolioId, "HPG", TradeType.BUY, 1000, 25_000,
            tradeDate: new DateTime(2026, 1, 5)));
        SetupActions(CorporateAction.StockDividend(PortfolioId, "u1", "HPG", 100, 130, Ex,
            new DateTime(2026, 7, 20)));
        SetupPrice(23_076.92m); // 30.000 / 1,3

        var summary = await Sut().CalculatePortfolioPnLAsync(PortfolioId);

        var hpg = summary.Positions.Single();
        hpg.Quantity.Should().Be(1300);
        hpg.SettledQuantity.Should().Be(1000);
        hpg.PendingQuantity.Should().Be(300);
        hpg.AverageCost.Should().BeApproximately(19_230.77m, 0.01m);
        // 1300 × 23.076,92 ≈ 30tr so với vốn 25tr → vẫn lãi, không phải lỗ giả
        summary.TotalUnrealizedPnL.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task KhongDieuChinh_ThiSeHienThiLoGia_ChungMinhBugDangSua()
    {
        SetupPortfolio();
        SetupTrades(new Trade(PortfolioId, "HPG", TradeType.BUY, 1000, 25_000,
            tradeDate: new DateTime(2026, 1, 5)));
        SetupActions(); // không nhập sự kiện quyền
        SetupPrice(23_076.92m);

        var summary = await Sut().CalculatePortfolioPnLAsync(PortfolioId);

        // 1000 × 23.076,92 = 23,08tr so với vốn 25tr → lỗ giả, đúng như trước khi có tính năng
        summary.TotalUnrealizedPnL.Should().BeLessThan(0);
    }

    [Fact]
    public async Task CoTucTienMatChuaVe_ThiVaoCotChoVe_KhongDoiGiaVon()
    {
        SetupPortfolio();
        SetupTrades(new Trade(PortfolioId, "SAB", TradeType.BUY, 1000, 55_000,
            tradeDate: new DateTime(2026, 1, 5)));
        SetupActions(CorporateAction.CashDividend(PortfolioId, "u1", "SAB", 5m, Ex,
            new DateTime(2026, 7, 10), 5m));
        SetupPrice(54_500m);

        var summary = await Sut().CalculatePortfolioPnLAsync(PortfolioId);

        var sab = summary.Positions.Single();
        sab.AverageCost.Should().Be(55_000);
        sab.PendingDividend.Should().Be(475_000);
        sab.DividendNet.Should().Be(0);
        // Lãi giá âm 500đ/CP nhưng tổng gồm cổ tức thì gần hoà
        sab.UnrealizedPnL.Should().Be(-500_000m);
        sab.TotalPnLWithDividend.Should().Be(-25_000m); // −500.000 + 475.000
    }

    [Fact]
    public async Task CoTucTienMatDaVe_ThiVaoCotDaNhan()
    {
        SetupPortfolio();
        SetupTrades(new Trade(PortfolioId, "SAB", TradeType.BUY, 1000, 55_000,
            tradeDate: new DateTime(2026, 1, 5)));
        var action = CorporateAction.CashDividend(PortfolioId, "u1", "SAB", 5m, Ex,
            new DateTime(2026, 7, 10), 5m);
        action.MarkSettled(new DateTime(2026, 7, 10));
        SetupActions(action);
        SetupPrice(54_500m);

        var summary = await Sut().CalculatePortfolioPnLAsync(PortfolioId);

        var sab = summary.Positions.Single();
        sab.DividendNet.Should().Be(475_000);
        sab.PendingDividend.Should().Be(0);
    }

    [Fact]
    public async Task CalculatePositionPnL_ApDungSuKienQuyenTheoMa()
    {
        _trades.Setup(r => r.GetByPortfolioIdAndSymbolAsync(PortfolioId, "HPG", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new Trade(PortfolioId, "HPG", TradeType.BUY, 1000, 25_000,
                tradeDate: new DateTime(2026, 1, 5)) });
        _actions.Setup(r => r.GetByPortfolioIdAndSymbolAsync(PortfolioId, "HPG", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { CorporateAction.StockDividend(PortfolioId, "u1", "HPG", 100, 130, Ex, null) });
        SetupPrice(23_076.92m);

        var position = await Sut().CalculatePositionPnLAsync(PortfolioId, new StockSymbol("HPG"));

        position.Quantity.Should().Be(1300);
        position.PendingQuantity.Should().Be(300);
    }
}
