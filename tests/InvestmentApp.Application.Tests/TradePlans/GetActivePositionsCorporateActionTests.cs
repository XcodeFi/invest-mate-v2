using FluentAssertions;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Portfolios.Queries;
using InvestmentApp.Application.TradePlans.Queries.GetActivePositions;
using InvestmentApp.Domain.Entities;
using Moq;
using Xunit;

namespace InvestmentApp.Application.Tests.TradePlans;

public class GetActivePositionsCorporateActionTests
{
    private readonly Mock<IPortfolioRepository> _portfolios = new();
    private readonly Mock<IPnLService> _pnl = new();
    private readonly Mock<ITradePlanRepository> _plans = new();
    private readonly Mock<ITradeRepository> _trades = new();

    private readonly Portfolio _portfolio = new("u1", "Danh mục", 100_000_000m);

    private GetActivePositionsQueryHandler Sut() =>
        new(_portfolios.Object, _pnl.Object, _plans.Object, _trades.Object);

    private void SetupPortfolio() =>
        _portfolios.Setup(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { _portfolio });

    private void SetupPnL(PositionPnL position) =>
        _pnl.Setup(s => s.CalculatePortfolioPnLAsync(_portfolio.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PortfolioPnLSummary { Positions = new List<PositionPnL> { position } });

    private void SetupNoTradesOrPlans()
    {
        _trades.Setup(r => r.GetByPortfolioIdAndSymbolAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Trade>());
        _plans.Setup(r => r.GetActiveByPortfolioAndSymbolAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TradePlan?)null);
    }

    [Fact]
    public async Task CoTucCoPhieuChuaVe_TraVeSoLuongTongVaSoChoVe()
    {
        SetupPortfolio();
        SetupNoTradesOrPlans();
        SetupPnL(new PositionPnL
        {
            Symbol = "HPG",
            Quantity = 1300,
            SettledQuantity = 1000,
            PendingQuantity = 300,
            AverageCost = 19_230.77m,
            CurrentPrice = 23_076.92m
        });

        var result = await Sut().Handle(new GetActivePositionsQuery { UserId = "u1" }, CancellationToken.None);

        var dto = result.Single();
        dto.Quantity.Should().Be(1300);
        dto.SettledQuantity.Should().Be(1000);
        dto.PendingQuantity.Should().Be(300);
        dto.AverageCost.Should().Be(19_230.77m);
    }

    [Fact]
    public async Task CoTucTienMatDaVe_CongVaoTongLaiLo()
    {
        SetupPortfolio();
        SetupNoTradesOrPlans();
        var position = new PositionPnL
        {
            Symbol = "SAB",
            Quantity = 1000,
            SettledQuantity = 1000,
            AverageCost = 55_000m,
            CurrentPrice = 54_500m,
            DividendNet = 475_000m
        };
        SetupPnL(position);

        var result = await Sut().Handle(new GetActivePositionsQuery { UserId = "u1" }, CancellationToken.None);

        var dto = result.Single();
        dto.DividendNet.Should().Be(475_000m);
        dto.UnrealizedPnL.Should().Be(-500_000m);
        dto.TotalPnLWithDividend.Should().Be(-25_000m); // −500.000 + 475.000
    }
}
