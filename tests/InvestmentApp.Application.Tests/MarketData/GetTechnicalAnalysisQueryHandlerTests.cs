using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.MarketData.Queries.GetTechnicalAnalysis;
using Moq;

namespace InvestmentApp.Application.Tests.MarketData;

public class GetTechnicalAnalysisQueryHandlerTests
{
    private static (GetTechnicalAnalysisQueryHandler Handler, Mock<ITechnicalIndicatorService> Service) Create()
    {
        var service = new Mock<ITechnicalIndicatorService>();
        service.Setup(s => s.AnalyzeAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(new TechnicalAnalysisResult());
        return (new GetTechnicalAnalysisQueryHandler(service.Object), service);
    }

    [Fact]
    public async Task Handle_Normalizes_Symbol_And_DefaultsToTwelveMonths()
    {
        var (handler, service) = Create();

        await handler.Handle(new GetTechnicalAnalysisQuery { Symbol = " vnm " }, default);

        service.Verify(s => s.AnalyzeAsync("VNM", 12, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(24, 24)]
    [InlineData(1, 3)]
    [InlineData(100, 36)]
    public async Task Handle_PassesRequestedMonthsClampedToThreeThroughThirtySix(int requested, int expected)
    {
        var (handler, service) = Create();

        await handler.Handle(new GetTechnicalAnalysisQuery { Symbol = "VNM", Months = requested }, default);

        service.Verify(s => s.AnalyzeAsync("VNM", expected, It.IsAny<CancellationToken>()), Times.Once);
    }
}
