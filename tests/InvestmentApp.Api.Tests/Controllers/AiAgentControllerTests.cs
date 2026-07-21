using System.Security.Claims;
using FluentAssertions;
using InvestmentApp.Api.Controllers;
using InvestmentApp.Application.TradePlans.Commands.CreateTradePlan;
using InvestmentApp.Application.TradePlans.Commands.UpdateTradePlanStatus;
using InvestmentApp.Application.Trades.Commands.CreateTrade;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace InvestmentApp.Api.Tests.Controllers;

public class AiAgentControllerTests
{
    private readonly Mock<IMediator> _mediator = new();

    private AiAgentController Sut(string userId = "user-1")
    {
        var controller = new AiAgentController(_mediator.Object);
        var identity = new ClaimsIdentity(new[] { new Claim("sub", userId) }, "ApiKey");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }

    [Fact]
    public async Task CreatePlan_NullsStatusAndTradeId_AndSetsUserId()
    {
        CreateTradePlanCommand? sent = null;
        _mediator.Setup(m => m.Send(It.IsAny<CreateTradePlanCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<string>, CancellationToken>((c, _) => sent = (CreateTradePlanCommand)c)
            .ReturnsAsync("plan-1");

        var cmd = new CreateTradePlanCommand { Symbol = "VNM", Status = "Executed", TradeId = "t-x" };
        await Sut().CreatePlan(cmd);

        sent!.Status.Should().BeNull();
        sent.TradeId.Should().BeNull();
        sent.UserId.Should().Be("user-1");
    }

    [Fact]
    public async Task UpdateStatus_Restore_ReturnsBadRequest_AndDoesNotDispatch()
    {
        var result = await Sut().UpdateStatus("plan-1",
            new UpdateTradePlanStatusCommand { Status = "restore" });

        result.Should().BeOfType<BadRequestObjectResult>();
        _mediator.Verify(m => m.Send(It.IsAny<UpdateTradePlanStatusCommand>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateStatus_Executed_Dispatches_WithUserId()
    {
        UpdateTradePlanStatusCommand? sent = null;
        _mediator.Setup(m => m.Send(It.IsAny<UpdateTradePlanStatusCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Unit>, CancellationToken>((c, _) => sent = (UpdateTradePlanStatusCommand)c)
            .ReturnsAsync(Unit.Value);

        var result = await Sut().UpdateStatus("plan-1",
            new UpdateTradePlanStatusCommand { Status = "executed", TradeId = "t-1" });

        result.Should().BeOfType<NoContentResult>();
        sent!.UserId.Should().Be("user-1");
        sent.Id.Should().Be("plan-1");
    }

    [Fact]
    public async Task CreateTrade_SetsOriginAiAgent_AndUserId()
    {
        CreateTradeCommand? sent = null;
        _mediator.Setup(m => m.Send(It.IsAny<CreateTradeCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<string>, CancellationToken>((c, _) => sent = (CreateTradeCommand)c)
            .ReturnsAsync("trade-1");

        await Sut().CreateTrade(new CreateTradeCommand
        {
            PortfolioId = "p1", Symbol = "VNM", TradeType = "BUY", Quantity = 100, Price = 50000
        });

        sent!.UserId.Should().Be("user-1");
        sent.Origin.Should().Be("AI_AGENT");
    }

    [Fact]
    public void GetDoc_Returns200_WithETagAndBody()
    {
        var sut = Sut();
        var result = sut.GetDoc() as ContentResult;

        result!.StatusCode.Should().Be(200);
        result.Content.Should().Contain("Mục lục");
        sut.Response.Headers.ETag.ToString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetDoc_MatchingIfNoneMatch_Returns304()
    {
        var sut = Sut();
        sut.ControllerContext.HttpContext.Request.Headers.IfNoneMatch = $"\"{AiAgentController.DocVersion}\"";

        var result = sut.GetDoc() as StatusCodeResult;

        result!.StatusCode.Should().Be(StatusCodes.Status304NotModified);
    }
}
