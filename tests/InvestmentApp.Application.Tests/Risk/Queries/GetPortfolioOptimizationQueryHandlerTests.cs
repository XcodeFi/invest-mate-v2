using FluentAssertions;
using Moq;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Risk.Queries.GetPortfolioOptimization;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Tests.Risk.Queries;

public class GetPortfolioOptimizationQueryHandlerTests
{
    private readonly Mock<IRiskCalculationService> _riskService;
    private readonly Mock<IPortfolioRepository> _portfolioRepo;
    private readonly GetPortfolioOptimizationQueryHandler _handler;

    public GetPortfolioOptimizationQueryHandlerTests()
    {
        _riskService = new Mock<IRiskCalculationService>();
        _portfolioRepo = new Mock<IPortfolioRepository>();
        _handler = new GetPortfolioOptimizationQueryHandler(_riskService.Object, _portfolioRepo.Object);
    }

    [Fact]
    public async Task Handle_ValidPortfolio_ReturnsOptimizationResult()
    {
        // Arrange
        var portfolio = new Portfolio("user1", "Test Portfolio", 100_000_000m);
        _portfolioRepo.Setup(r => r.GetByIdAsync("port1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(portfolio);

        var expected = new PortfolioOptimizationResult
        {
            PortfolioId = "port1",
            TotalValue = 100_000_000m,
            DiversificationScore = 72m,
            ConcentrationAlerts = new List<ConcentrationAlert>
            {
                new() { Symbol = "VIC", PositionPercent = 35m, Limit = 20m, Severity = "danger" }
            },
            SectorExposures = new List<SectorExposure>
            {
                new() { Sector = "Bất động sản", Symbols = new() { "VIC", "VHM" }, TotalValue = 45_000_000m, ExposurePercent = 45m, Limit = 40m, IsOverweight = true }
            },
            CorrelationWarnings = new List<CorrelationWarning>
            {
                new() { Symbol1 = "VIC", Symbol2 = "VHM", Correlation = 0.85m, RiskLevel = "high" }
            },
            Recommendations = new() { "Giảm tỷ trọng VIC (35% > giới hạn 20%)" }
        };

        _riskService.Setup(s => s.GetPortfolioOptimizationAsync("port1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var query = new GetPortfolioOptimizationQuery { PortfolioId = "port1", UserId = "user1" };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.PortfolioId.Should().Be("port1");
        result.DiversificationScore.Should().Be(72m);
        result.ConcentrationAlerts.Should().HaveCount(1);
        result.ConcentrationAlerts[0].Symbol.Should().Be("VIC");
        result.ConcentrationAlerts[0].Severity.Should().Be("danger");
        result.SectorExposures.Should().HaveCount(1);
        result.SectorExposures[0].IsOverweight.Should().BeTrue();
        result.CorrelationWarnings.Should().HaveCount(1);
        result.CorrelationWarnings[0].RiskLevel.Should().Be("high");
        result.Recommendations.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_PortfolioNotFound_ThrowsArgumentException()
    {
        // Arrange
        _portfolioRepo.Setup(r => r.GetByIdAsync("nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Portfolio?)null);

        var query = new GetPortfolioOptimizationQuery { PortfolioId = "nonexistent", UserId = "user1" };

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

        var query = new GetPortfolioOptimizationQuery { PortfolioId = "port1", UserId = "user1" };

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

        _riskService.Setup(s => s.GetPortfolioOptimizationAsync("port1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PortfolioOptimizationResult { PortfolioId = "port1" });

        var query = new GetPortfolioOptimizationQuery { PortfolioId = "port1", UserId = "user1" };

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _riskService.Verify(s => s.GetPortfolioOptimizationAsync("port1", It.IsAny<CancellationToken>()), Times.Once);
    }
}
