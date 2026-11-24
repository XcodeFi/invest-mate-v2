using System.Security.Claims;
using FluentAssertions;
using InvestmentApp.Api.Controllers;
using InvestmentApp.Application.TradePlans.Queries.GetStrategyExpectancy;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace InvestmentApp.Api.Tests.Controllers;

public class TradePlansControllerExpectancyTests
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
    public async Task GetExpectancy_scopes_the_query_to_the_caller_and_returns_the_result()
    {
        GetStrategyExpectancyQuery? sent = null;
        var expected = new StrategyExpectancyDto();
        _mediator.Setup(m => m.Send(It.IsAny<GetStrategyExpectancyQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<StrategyExpectancyDto>, CancellationToken>((q, _) => sent = (GetStrategyExpectancyQuery)q)
            .ReturnsAsync(expected);

        var result = await CreateController("user-42").GetExpectancy(CancellationToken.None);

        sent!.UserId.Should().Be("user-42", "thống kê chỉ lấy dữ liệu của người gọi");
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(expected);
    }

    [Fact]
    public void Route_is_a_literal_segment_so_it_does_not_collide_with_the_plan_id_route()
    {
        var method = typeof(TradePlansController).GetMethod(nameof(TradePlansController.GetExpectancy))!;
        var template = method.GetCustomAttributes(typeof(HttpGetAttribute), false)
            .Cast<HttpGetAttribute>().Single().Template;

        template.Should().Be("expectancy");
    }
}
