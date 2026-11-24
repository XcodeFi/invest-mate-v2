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
}
