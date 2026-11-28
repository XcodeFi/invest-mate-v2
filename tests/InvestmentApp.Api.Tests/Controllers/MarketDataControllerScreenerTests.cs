using System.Reflection;
using FluentAssertions;
using InvestmentApp.Api.Controllers;
using InvestmentApp.Application.MarketData.Queries.ScreenStocks;
using InvestmentApp.Application.MarketData.Screener;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace InvestmentApp.Api.Tests.Controllers;

public class MarketDataControllerScreenerTests
{
    [Fact]
    public async Task screener_maps_every_query_parameter_and_returns_ok()
    {
        var mediator = new Mock<IMediator>();
        var dto = new ScreenerResult();
        ScreenStocksQuery? sent = null;
        mediator.Setup(m => m.Send(It.IsAny<ScreenStocksQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<ScreenerResult>, CancellationToken>((q, _) => sent = (ScreenStocksQuery)q)
            .ReturnsAsync(dto);

        var result = await new MarketDataController(mediator.Object).Screener(
            floors: "HOSE,HNX", includeUpcom: true, minAdtv: 6m, minPrice: 7000m, minCap: 1500m, minRoe: 14m, maxPe: 30m,
            minRs3m: 55, skipQualityLayer: true, includeExcluded: true, excludedSkip: 10, excludedTake: 50, lookupSymbol: "VNM");

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(dto);
        (sent!.Floors, sent.IncludeUpcom, sent.MinAdtv, sent.MinPrice, sent.MinCap, sent.MinRoe, sent.MaxPe, sent.MinRs3m,
            sent.SkipQualityLayer, sent.IncludeExcluded, sent.ExcludedSkip, sent.ExcludedTake, sent.LookupSymbol)
            .Should().Be(("HOSE,HNX", true, 6m, 7000m, 1500m, 14m, 30m, 55, true, true, 10, 50, "VNM"));
    }

    [Fact]
    public void the_route_is_get_market_screener_behind_the_jwt_policy()
    {
        var method = typeof(MarketDataController).GetMethod(nameof(MarketDataController.Screener))!;

        method.GetCustomAttribute<HttpGetAttribute>()!.Template.Should().Be("screener");
        typeof(MarketDataController).GetCustomAttribute<RouteAttribute>()!.Template.Should().Be("api/v1/market");
        typeof(MarketDataController).GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
    }
}
