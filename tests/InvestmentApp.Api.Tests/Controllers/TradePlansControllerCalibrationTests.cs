using System.Security.Claims;
using FluentAssertions;
using InvestmentApp.Api.Controllers;
using InvestmentApp.Application.TradePlans.Queries.GetEstimateCalibration;
using InvestmentApp.Domain.Services;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace InvestmentApp.Api.Tests.Controllers;

public class TradePlansControllerCalibrationTests
{
    private readonly Mock<IMediator> _mediator = new();

    private TradePlansController CreateController(string userId = "user-1")
    {
        var controller = new TradePlansController(_mediator.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", userId) }, "test"))
            }
        };
        return controller;
    }

    [Fact]
    public async Task GetEstimateCalibration_scopes_the_query_to_the_caller_and_passes_the_strategy()
    {
        GetEstimateCalibrationQuery? sent = null;
        var expected = Calibration.Compute(Array.Empty<(decimal, decimal)>());
        _mediator.Setup(m => m.Send(It.IsAny<GetEstimateCalibrationQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<CalibrationResult>, CancellationToken>((q, _) => sent = (GetEstimateCalibrationQuery)q)
            .ReturnsAsync(expected);

        var result = await CreateController("user-42").GetEstimateCalibration("s1");

        sent!.UserId.Should().Be("user-42");
        sent.StrategyId.Should().Be("s1");
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(expected);
    }

    [Fact]
    public void Route_is_a_literal_segment_so_it_does_not_collide_with_the_plan_id_route()
    {
        var method = typeof(TradePlansController).GetMethod(nameof(TradePlansController.GetEstimateCalibration))!;
        method.GetCustomAttributes(typeof(HttpGetAttribute), false).Cast<HttpGetAttribute>().Single().Template
            .Should().Be("calibration");
    }
}
