using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.MarketData.Queries.GetTechnicalSeries;
using Moq;

namespace InvestmentApp.Application.Tests.MarketData;

public class GetTechnicalSeriesQueryHandlerTests
{
    [Fact]
    public async Task Handle_Normalizes_Symbol_And_Calls_GetSeriesAsync()
    {
        var expected = new TechnicalSeriesResult { Symbol = "VNM" };
        var service = new Mock<ITechnicalIndicatorService>();
        service.Setup(s => s.GetSeriesAsync("VNM", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var handler = new GetTechnicalSeriesQueryHandler(service.Object);

        var result = await handler.Handle(new GetTechnicalSeriesQuery { Symbol = " vnm " }, default);

        result.Should().BeSameAs(expected);
        service.Verify(s => s.GetSeriesAsync("VNM", It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DefaultsToTwelveMonths_SoExistingCallersKeepTheirBehaviour()
    {
        var service = new Mock<ITechnicalIndicatorService>();
        service.Setup(s => s.GetSeriesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(new TechnicalSeriesResult());
        var handler = new GetTechnicalSeriesQueryHandler(service.Object);

        await handler.Handle(new GetTechnicalSeriesQuery { Symbol = "VNM" }, default);

        service.Verify(s => s.GetSeriesAsync("VNM", 12, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(24, 24)]
    [InlineData(36, 36)]
    [InlineData(1, 3)]
    [InlineData(0, 3)]
    [InlineData(-5, 3)]
    [InlineData(100, 36)]
    public async Task Handle_PassesRequestedMonthsClampedToThreeThroughThirtySix(int requested, int expected)
    {
        var service = new Mock<ITechnicalIndicatorService>();
        service.Setup(s => s.GetSeriesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(new TechnicalSeriesResult());
        var handler = new GetTechnicalSeriesQueryHandler(service.Object);

        await handler.Handle(new GetTechnicalSeriesQuery { Symbol = "VNM", Months = requested }, default);

        service.Verify(s => s.GetSeriesAsync("VNM", expected, It.IsAny<CancellationToken>()), Times.Once);
    }
}
