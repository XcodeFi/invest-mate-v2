using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Risk.Queries.GetSectorExposureForPlan;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.Risk.Queries;

public class GetSectorExposureForPlanQueryHandlerTests
{
    private readonly Mock<IRiskCalculationService> _riskService = new();
    private readonly Mock<IPortfolioRepository> _portfolioRepo = new();
    private readonly GetSectorExposureForPlanQueryHandler _handler;

    public GetSectorExposureForPlanQueryHandlerTests()
    {
        _handler = new GetSectorExposureForPlanQueryHandler(_riskService.Object, _portfolioRepo.Object);
    }

    [Fact]
    public async Task Handle_WhenPortfolioBelongsToAnotherUser_ShouldThrowAndNotCallService()
    {
        _portfolioRepo.Setup(r => r.GetByIdAsync("port1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Portfolio("chu-so-huu-khac", "Portfolio người khác", 100_000_000m));
        var query = new GetSectorExposureForPlanQuery
        {
            PortfolioId = "port1", UserId = "user1", Symbol = "HPG", AddValue = 9_000_000m
        };

        var act = () => _handler.Handle(query, default);

        await act.Should().ThrowAsync<ArgumentException>();
        // Assert "có throw" là không đủ: phải chứng minh service chưa bao giờ được gọi, đó mới là
        // bằng chứng kiểm quyền chạy TRƯỚC khi đọc dữ liệu danh mục của người khác.
        _riskService.Verify(s => s.GetSectorExposureForPlanAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenPortfolioMissing_ShouldThrowAndNotCallService()
    {
        _portfolioRepo.Setup(r => r.GetByIdAsync("port1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Portfolio?)null);
        var query = new GetSectorExposureForPlanQuery
        {
            PortfolioId = "port1", UserId = "user1", Symbol = "HPG", AddValue = 9_000_000m
        };

        var act = () => _handler.Handle(query, default);

        await act.Should().ThrowAsync<ArgumentException>();
        _riskService.Verify(s => s.GetSectorExposureForPlanAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenOwnerMatches_ShouldPassSymbolAndAddValueThrough()
    {
        _portfolioRepo.Setup(r => r.GetByIdAsync("port1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Portfolio("user1", "Portfolio của tôi", 100_000_000m));
        var expected = new SectorExposureForPlan
        {
            Symbol = "HPG",
            Sector = "Tài nguyên cơ bản",
            CurrentPercent = 32m,
            ProjectedPercent = 41m,
            LimitPercent = 40m,
            SameSectorSymbols = new List<string> { "HSG", "NKG" }
        };
        _riskService.Setup(s => s.GetSectorExposureForPlanAsync(
                "port1", "HPG", 9_000_000m, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _handler.Handle(new GetSectorExposureForPlanQuery
        {
            PortfolioId = "port1", UserId = "user1", Symbol = "HPG", AddValue = 9_000_000m
        }, default);

        result.Should().BeSameAs(expected);
    }
}
