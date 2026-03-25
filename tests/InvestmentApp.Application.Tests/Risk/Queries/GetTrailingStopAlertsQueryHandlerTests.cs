using FluentAssertions;
using Moq;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Risk.Queries.GetTrailingStopAlerts;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Tests.Risk.Queries;

public class GetTrailingStopAlertsQueryHandlerTests
{
    private readonly Mock<IRiskCalculationService> _riskService;
    private readonly Mock<IPortfolioRepository> _portfolioRepo;
    private readonly GetTrailingStopAlertsQueryHandler _handler;

    public GetTrailingStopAlertsQueryHandlerTests()
    {
        _riskService = new Mock<IRiskCalculationService>();
        _portfolioRepo = new Mock<IPortfolioRepository>();
        _handler = new GetTrailingStopAlertsQueryHandler(_riskService.Object, _portfolioRepo.Object);
    }

    [Fact]
    public async Task Handle_ValidPortfolio_ReturnsTrailingStopAlerts()
    {
        // Arrange
        var portfolio = new Portfolio("user1", "Test Portfolio", 100_000_000m);
        _portfolioRepo.Setup(r => r.GetByIdAsync("port1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(portfolio);

        var expected = new TrailingStopAlertsResult
        {
            PortfolioId = "port1",
            TotalActiveTrailingStops = 3,
            AlertCount = 1,
            Alerts = new List<TrailingStopAlert>
            {
                new()
                {
                    Symbol = "FPT",
                    TradeId = "trade1",
                    EntryPrice = 100_000m,
                    CurrentPrice = 96_000m,
                    TrailingStopPercent = 5m,
                    TrailingStopPrice = 95_000m,
                    DistancePercent = 1.04m,
                    Severity = "danger",
                    ShouldUpdatePrice = false,
                    NewTrailingStopPrice = null
                }
            }
        };

        _riskService.Setup(s => s.GetTrailingStopAlertsAsync("port1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var query = new GetTrailingStopAlertsQuery { PortfolioId = "port1", UserId = "user1" };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.PortfolioId.Should().Be("port1");
        result.TotalActiveTrailingStops.Should().Be(3);
        result.AlertCount.Should().Be(1);
        result.Alerts.Should().HaveCount(1);
        result.Alerts[0].Symbol.Should().Be("FPT");
        result.Alerts[0].Severity.Should().Be("danger");
        result.Alerts[0].DistancePercent.Should().BeApproximately(1.04m, 0.01m);
    }

    [Fact]
    public async Task Handle_PortfolioNotFound_ThrowsArgumentException()
    {
        // Arrange
        _portfolioRepo.Setup(r => r.GetByIdAsync("nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Portfolio?)null);

        var query = new GetTrailingStopAlertsQuery { PortfolioId = "nonexistent", UserId = "user1" };

        // Act & Assert
        await _handler.Invoking(h => h.Handle(query, CancellationToken.None))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Handle_WrongUser_ThrowsArgumentException()
    {
        // Arrange
        var portfolio = new Portfolio("other-user", "Other Portfolio", 50_000_000m);
        _portfolioRepo.Setup(r => r.GetByIdAsync("port1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(portfolio);

        var query = new GetTrailingStopAlertsQuery { PortfolioId = "port1", UserId = "user1" };

        // Act & Assert
        await _handler.Invoking(h => h.Handle(query, CancellationToken.None))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Handle_DelegatesCorrectlyToService()
    {
        // Arrange
        var portfolio = new Portfolio("user1", "My Portfolio", 100_000_000m);
        _portfolioRepo.Setup(r => r.GetByIdAsync("port1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(portfolio);

        _riskService.Setup(s => s.GetTrailingStopAlertsAsync("port1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TrailingStopAlertsResult { PortfolioId = "port1" });

        var query = new GetTrailingStopAlertsQuery { PortfolioId = "port1", UserId = "user1" };

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _riskService.Verify(s => s.GetTrailingStopAlertsAsync("port1", It.IsAny<CancellationToken>()), Times.Once);
    }
}
