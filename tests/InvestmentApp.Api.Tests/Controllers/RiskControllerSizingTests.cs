using System.Security.Claims;
using FluentAssertions;
using InvestmentApp.Api.Controllers;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Risk.Queries.CalculatePositionSizing;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace InvestmentApp.Api.Tests.Controllers;

/// <summary>Controller chỉ chuyển tiếp: tính toán nằm ở CalculatePositionSizingQuery để REST và MCP dùng chung một đường.</summary>
public class RiskControllerSizingTests
{
    [Fact]
    public async Task position_sizing_delegates_to_the_shared_query_with_the_caller_id_and_flags()
    {
        var mediator = new Mock<IMediator>();
        var expected = new PositionSizingResult { RecommendedModel = "kelly" };
        CalculatePositionSizingQuery? sent = null;
        mediator.Setup(m => m.Send(It.IsAny<CalculatePositionSizingQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<PositionSizingResult>, CancellationToken>((q, _) => sent = (CalculatePositionSizingQuery)q)
            .ReturnsAsync(expected);
        var controller = new RiskController(mediator.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "user-1") }, "test"))
                }
            }
        };
        var request = new PositionSizingRequest { AccountBalance = 1m, EntryPrice = 1m, StopLoss = 1m };

        var result = await controller.CalculatePositionSizing(request, useHistory: true, strategyId: "s1");

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(expected);
        sent!.UserId.Should().Be("user-1");
        sent.Request.Should().BeSameAs(request);
        sent.UseHistory.Should().BeTrue();
        sent.StrategyId.Should().Be("s1");
    }
}
