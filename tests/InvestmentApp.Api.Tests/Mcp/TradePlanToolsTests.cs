using FluentAssertions;
using InvestmentApp.Api.Mcp;
using InvestmentApp.Application.TradePlans.Commands.CreateTradePlan;
using InvestmentApp.Application.TradePlans.Commands.UpdateTradePlan;
using InvestmentApp.Application.TradePlans.Commands.UpdateTradePlanStatus;
using InvestmentApp.Application.TradePlans.Queries.GetTradePlans;
using MediatR;
using Moq;

namespace InvestmentApp.Api.Tests.Mcp;

public class TradePlanToolsTests
{
    private readonly Mock<IMediator> _mediator = new();

    [Fact]
    public async Task ListTradePlans_SetsUserId_AndActiveOnly()
    {
        McpTestContext.Capture<IEnumerable<TradePlanDto>, GetTradePlansQuery>(_mediator, out var sent, Array.Empty<TradePlanDto>());
        await TradePlanTools.ListTradePlans(true, _mediator.Object, McpTestContext.WithUser("u-1"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-1");
        sent()!.ActiveOnly.Should().BeTrue();
    }

    [Fact]
    public async Task GetTradePlan_SetsIdAndUserId()
    {
        McpTestContext.Capture<TradePlanDto?, GetTradePlanByIdQuery>(_mediator, out var sent, null);
        await TradePlanTools.GetTradePlan("plan-9", _mediator.Object, McpTestContext.WithUser("u-2"), CancellationToken.None);
        sent()!.Id.Should().Be("plan-9");
        sent()!.UserId.Should().Be("u-2");
    }

    [Fact]
    public async Task CreateTradePlan_ForcesDraft_AndSetsUserId()
    {
        McpTestContext.Capture<string, CreateTradePlanCommand>(_mediator, out var sent, "plan-new");
        var id = await TradePlanTools.CreateTradePlan(
            "VNM", entryPrice: 80000, stopLoss: 75000, target: 95000, quantity: 100,
            _mediator.Object, McpTestContext.WithUser("u-3"), CancellationToken.None);
        id.Should().Be("plan-new");
        // Status/TradeId không còn nằm trong tham số tool → luôn Draft (ADR-0004).
        sent()!.Status.Should().BeNull();
        sent()!.TradeId.Should().BeNull();
        sent()!.UserId.Should().Be("u-3");
        sent()!.Symbol.Should().Be("VNM");
        sent()!.Direction.Should().Be("Buy");
        sent()!.ConfidenceLevel.Should().Be(5);
    }

    [Fact]
    public async Task UpdateTradePlan_SetsIdAndUserId()
    {
        McpTestContext.Capture<Unit, UpdateTradePlanCommand>(_mediator, out var sent, Unit.Value);
        await TradePlanTools.UpdateTradePlan("plan-1",
            _mediator.Object, McpTestContext.WithUser("u-4"), CancellationToken.None, symbol: "SSI");
        sent()!.Id.Should().Be("plan-1");
        sent()!.UserId.Should().Be("u-4");
        sent()!.Symbol.Should().Be("SSI");
    }

    [Fact]
    public async Task SetTradePlanStatus_Restore_Throws()
    {
        var act = async () => await TradePlanTools.SetTradePlanStatus(
            "plan-1", "restore", null, _mediator.Object, McpTestContext.WithUser("u-5"), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SetTradePlanStatus_Executed_Dispatches()
    {
        McpTestContext.Capture<Unit, UpdateTradePlanStatusCommand>(_mediator, out var sent, Unit.Value);
        await TradePlanTools.SetTradePlanStatus("plan-1", "executed", "t-1",
            _mediator.Object, McpTestContext.WithUser("u-6"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-6");
        sent()!.Id.Should().Be("plan-1");
        sent()!.Status.Should().Be("executed");
    }
}
