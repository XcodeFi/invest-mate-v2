using FluentAssertions;
using InvestmentApp.Api.Mcp;
using InvestmentApp.Application.CompanyDossiers.Gate;
using InvestmentApp.Application.TradePlans.Commands.CreateTradePlan;
using InvestmentApp.Application.TradePlans.Commands.UpdateTradePlan;
using InvestmentApp.Application.TradePlans.Commands.UpdateTradePlanStatus;
using InvestmentApp.Application.TradePlans.Queries.GetTradePlans;
using MediatR;
using ModelContextProtocol;
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

    // Cổng hồ sơ ném DossierGateException; qua MCP mọi exception thường bị che thành
    // "An error occurred invoking 'create_trade_plan'." nên agent mất cả reason lẫn missing[]
    // và không có đường tự chữa. McpException là loại duy nhất giữ được nguyên văn.
    [Fact]
    public async Task CreateTradePlan_WhenGateBlocksInsufficient_SurfacesReasonAndMissingList()
    {
        _mediator.Setup(m => m.Send(It.IsAny<CreateTradePlanCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DossierGateException("HAH",
                DossierGateResult.Fail("insufficient", "riskFactors: cần ≥ 3, đang có 1")));

        var act = async () => await TradePlanTools.CreateTradePlan(
            "HAH", entryPrice: 20000, stopLoss: 18000, target: 26000, quantity: 10000,
            _mediator.Object, McpTestContext.WithUser("u-7"), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpException>();
        ex.Which.Message.Should().Contain("HAH")
            .And.Contain("riskFactors: cần ≥ 3, đang có 1")
            .And.Contain("upsert_company_dossier")
            .And.Contain("get_dossier_gate_status");
    }

    [Fact]
    public async Task CreateTradePlan_WhenGateBlocksUnconfirmed_SaysAgentCannotSign()
    {
        _mediator.Setup(m => m.Send(It.IsAny<CreateTradePlanCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DossierGateException("HAH", DossierGateResult.Fail("unconfirmed")));

        var act = async () => await TradePlanTools.CreateTradePlan(
            "HAH", entryPrice: 20000, stopLoss: 18000, target: 26000, quantity: 10000,
            _mediator.Object, McpTestContext.WithUser("u-7"), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpException>();
        // Không được gợi ý agent tự ký — đúng chỗ để nói rõ chỉ con người ký được.
        ex.Which.Message.Should().Contain("/company-dossier/HAH").And.Contain("KHÔNG ký được");
    }

    [Fact]
    public async Task CreateTradePlan_WhenGateBlocksMissing_TellsAgentToDraftFirst()
    {
        _mediator.Setup(m => m.Send(It.IsAny<CreateTradePlanCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DossierGateException("FPT", DossierGateResult.Fail("missing")));

        var act = async () => await TradePlanTools.CreateTradePlan(
            "FPT", entryPrice: 100000, stopLoss: 90000, target: 130000, quantity: 1000,
            _mediator.Object, McpTestContext.WithUser("u-7"), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<McpException>();
        // reason=missing không kèm missing[] nào, nên thông báo phải tự đứng được một mình.
        ex.Which.Message.Should().Contain("Chưa có hồ sơ").And.Contain("upsert_company_dossier");
    }

    [Fact]
    public async Task UpdateTradePlan_WhenGateBlocks_SurfacesReasonToo()
    {
        _mediator.Setup(m => m.Send(It.IsAny<UpdateTradePlanCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DossierGateException("SSI", DossierGateResult.Fail("expired")));

        var act = async () => await TradePlanTools.UpdateTradePlan("plan-1",
            _mediator.Object, McpTestContext.WithUser("u-8"), CancellationToken.None, symbol: "SSI");

        var ex = await act.Should().ThrowAsync<McpException>();
        ex.Which.Message.Should().Contain("SSI").And.Contain("/company-dossier/SSI");
    }

    // Chỉ dịch riêng cổng hồ sơ. Bọc rộng hơn là che mất mọi lỗi khác dưới một câu đẹp đẽ.
    [Fact]
    public async Task CreateTradePlan_OtherExceptions_PassThroughUnchanged()
    {
        _mediator.Setup(m => m.Send(It.IsAny<CreateTradePlanCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Portfolio không thuộc người dùng"));

        var act = async () => await TradePlanTools.CreateTradePlan(
            "VNM", entryPrice: 80000, stopLoss: 75000, target: 95000, quantity: 100,
            _mediator.Object, McpTestContext.WithUser("u-7"), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Portfolio không thuộc*");
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
