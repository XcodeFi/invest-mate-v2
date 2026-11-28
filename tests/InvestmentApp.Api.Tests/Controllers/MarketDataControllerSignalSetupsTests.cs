using System.Reflection;
using FluentAssertions;
using InvestmentApp.Api.Controllers;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.MarketData.Queries.GetSignalSetups;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace InvestmentApp.Api.Tests.Controllers;

public class MarketDataControllerSignalSetupsTests
{
    [Fact]
    public async Task get_signal_setups_sends_the_query_and_returns_ok()
    {
        var mediator = new Mock<IMediator>();
        var dto = new SignalSetupsDto { Symbol = "FPT" };
        GetSignalSetupsQuery? sent = null;
        mediator.Setup(m => m.Send(It.IsAny<GetSignalSetupsQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<SignalSetupsDto>, CancellationToken>((q, _) => sent = (GetSignalSetupsQuery)q)
            .ReturnsAsync(dto);

        var result = await new MarketDataController(mediator.Object).GetSignalSetups("fpt");

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(dto);
        sent!.Symbol.Should().Be("fpt");
    }

    [Fact]
    public void the_route_is_get_market_symbol_signal_setups_behind_the_jwt_policy()
    {
        var method = typeof(MarketDataController).GetMethod(nameof(MarketDataController.GetSignalSetups))!;

        method.GetCustomAttribute<HttpGetAttribute>()!.Template.Should().Be("{symbol}/signal-setups");
        typeof(MarketDataController).GetCustomAttribute<RouteAttribute>()!.Template.Should().Be("api/v1/market");
        typeof(MarketDataController).GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull("cùng chính sách JWT với mọi endpoint thị trường");
    }
}
