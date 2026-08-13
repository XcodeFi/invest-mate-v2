using FluentAssertions;
using InvestmentApp.Api.Mcp;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using Moq;

namespace InvestmentApp.Api.Tests.Mcp;

/// <summary>
/// Discovery-level check: the SDK registers all tools with correct read/destructive
/// annotations. Resolves tools straight from DI (no app boot → no Mongo/secret dependency).
/// </summary>
public class McpToolDiscoveryTests
{
    private static readonly string[] ReadTools =
    {
        "list_trade_plans", "get_trade_plan", "list_portfolios", "list_positions",
        "calculate_fees", "get_symbol_timeline", "list_watchlists", "get_watchlist",
        "list_journals", "get_journal_by_trade", "list_trades_pending_review",
        "list_journal_entries_by_symbol",
        // P0 — Decision & Risk Intelligence
        "get_decision_queue", "get_discipline_score", "get_discipline_streak",
        "get_pending_thesis_reviews", "get_portfolio_risk", "get_stop_loss_targets",
        "get_trailing_stop_alerts", "get_scenario_advisories", "get_volatility_sizing",
        // Phase B — daily digest
        "get_daily_digest",
        // P1 — Performance & Wealth Analytics
        "get_performance", "get_equity_curve", "get_monthly_returns",
        "get_savings_comparison", "get_campaign_analytics", "get_net_worth_summary",
        "get_flow_history", "get_adjusted_return",
        // Hồ sơ công ty — agent đọc được và soạn được, KHÔNG ký được (ADR-0011 D2)
        "list_company_dossiers", "get_company_dossier", "get_dossier_gate_status",
        "get_company_fundamentals",
        // Lịch nghỉ giao dịch — nền tính T+2
        "list_market_closures"
    };

    private static readonly string[] WriteTools =
    {
        "create_trade_plan", "update_trade_plan", "set_trade_plan_status", "move_stop_loss", "create_trade",
        "create_watchlist", "update_watchlist", "delete_watchlist", "add_watchlist_item",
        "update_watchlist_item", "remove_watchlist_item", "import_vn30",
        "create_journal", "update_journal", "delete_journal",
        "create_journal_entry", "update_journal_entry", "delete_journal_entry",
        "upsert_company_dossier",
        "add_market_closures", "remove_market_closure"
    };

    private static IReadOnlyList<McpServerTool> Tools()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        // Register the same services the tools inject — the SDK excludes DI-registered
        // service params from the generated tool schema. Missing a registration would
        // silently leak that param (+ its whole object graph) into the tool's input schema.
        services.AddSingleton(Mock.Of<IMediator>());
        services.AddSingleton(Mock.Of<IFeeCalculationService>());
        services.AddSingleton(Mock.Of<IAiAssistantService>());
        services.AddHttpContextAccessor();
        services.AddMcpServer().WithToolsFromAssembly(typeof(PortfolioTools).Assembly);
        return services.BuildServiceProvider().GetServices<McpServerTool>().ToList();
    }

    [Fact]
    public void Registers_All_56_Tools()
    {
        var names = Tools().Select(t => t.ProtocolTool.Name).ToHashSet();
        foreach (var name in ReadTools.Concat(WriteTools))
            names.Should().Contain(name);
        (ReadTools.Length + WriteTools.Length).Should().Be(56);
    }

    [Fact]
    public void No_Tool_Escapes_The_Guard_Lists()
    {
        // Mọi guard khác trong file này chỉ chạy trên tên nằm trong ReadTools/WriteTools. Thiếu
        // chiều ngược lại thì một tool mới được đăng ký nhưng không có trong danh sách sẽ đi qua
        // TOÀN BỘ guard mà không test nào đỏ: không kiểm ReadOnly, không kiểm schema phẳng, không
        // kiểm rò rỉ service tiêm vào. Chính là hình dạng "luật có nhưng không có đường bắn".
        var registered = Tools().Select(t => t.ProtocolTool.Name).ToHashSet();
        var listed = ReadTools.Concat(WriteTools).ToHashSet();

        registered.Except(listed).Should().BeEmpty(
            "tool mới phải được khai vào ReadTools hoặc WriteTools, nếu không nó thoát mọi guard");
    }

    [Fact]
    public void No_Mcp_Tool_Can_Sign_A_Company_Dossier()
    {
        // Điểm tựa của toàn bộ tính năng hồ sơ công ty: agent soạn được nội dung, chỉ CON NGƯỜI ký.
        // `ConfirmedAt` chỉ đặt được qua endpoint JWT. Một cổng mà agent tự thoả mãn được thì không
        // đo hiểu biết của người bỏ tiền, nó chỉ đo "agent đã điền gì đó" (ADR-0011 D2).
        // Test này tồn tại để lần sau có người thêm tool `confirm_company_dossier` cho tiện thì đỏ
        // ngay, thay vì âm thầm phá bỏ lý do tính năng ra đời.
        var tools = Tools();

        var signingNames = tools.Select(t => t.ProtocolTool.Name)
            .Where(n => n.Contains("confirm", StringComparison.OrdinalIgnoreCase)
                     || n.Contains("sign", StringComparison.OrdinalIgnoreCase))
            .ToList();
        signingNames.Should().BeEmpty("không tool MCP nào được ký/xác nhận hồ sơ công ty");

        var schemasExposingConfirmedAt = tools
            .Where(t => t.ProtocolTool.InputSchema.ToString()
                .Contains("confirmedAt", StringComparison.OrdinalIgnoreCase))
            .Select(t => t.ProtocolTool.Name)
            .ToList();
        schemasExposingConfirmedAt.Should().BeEmpty("không tool nào được nhận ConfirmedAt làm tham số");
    }

    [Fact]
    public void Read_Tools_Are_ReadOnly()
    {
        var byName = Tools().ToDictionary(t => t.ProtocolTool.Name, t => t.ProtocolTool.Annotations);
        foreach (var name in ReadTools)
            byName[name]!.ReadOnlyHint.Should().BeTrue($"{name} should be read-only");
    }

    [Fact]
    public void Write_Tools_Are_Destructive()
    {
        var byName = Tools().ToDictionary(t => t.ProtocolTool.Name, t => t.ProtocolTool.Annotations);
        foreach (var name in WriteTools)
            byName[name]!.DestructiveHint.Should().BeTrue($"{name} should be destructive");
    }

    [Fact]
    public void Tool_Schemas_Exclude_Injected_Services_And_Include_Real_Args()
    {
        var schema = Tools().ToDictionary(t => t.ProtocolTool.Name, t => t.ProtocolTool.InputSchema.GetRawText());

        // DI-only tool → no injected services surface as tool inputs.
        schema["list_portfolios"].Should().NotContain("mediator").And.NotContain("http");

        // Sync tool injecting IFeeCalculationService → real args present, service absent.
        schema["calculate_fees"].Should().Contain("tradeType").And.Contain("quantity");
        schema["calculate_fees"].Should().NotContain("feeService");

        // create_trade → real args present; mediator/feeService/http/ct excluded.
        schema["create_trade"].Should().Contain("symbol");
        schema["create_trade"].Should().NotContain("feeService").And.NotContain("mediator");

        // move_stop_loss → dời SL cho kế hoạch đang giữ vị thế; không đi qua update_trade_plan
        // (bản đó chặn cứng plan Executed), nên schema phải tự mang newStopLoss + reason.
        schema["move_stop_loss"].Should().Contain("newStopLoss").And.Contain("reason");
        schema["move_stop_loss"].Should().NotContain("mediator").And.NotContain("http");

        // P0 risk tools → portfolioId arg present; injected services absent.
        schema["get_portfolio_risk"].Should().Contain("portfolioId").And.NotContain("mediator");
        schema["get_stop_loss_targets"].Should().Contain("portfolioId").And.NotContain("mediator");
        schema["get_trailing_stop_alerts"].Should().Contain("portfolioId").And.NotContain("mediator");

        // get_daily_digest — no-args tool; injected services must not leak.
        schema["get_daily_digest"].Should().NotContain("aiAssistant").And.NotContain("http");

        // P1 analytics tools → real args present; injected services absent.
        schema["get_performance"].Should().Contain("portfolioId").And.NotContain("mediator");
        schema["get_savings_comparison"].Should().Contain("annualRate").And.NotContain("mediator");
        schema["get_flow_history"].Should().Contain("from").And.Contain("to").And.NotContain("mediator");
    }

    [Fact]
    public void No_Tool_Wraps_Its_Args_In_A_Command_Object()
    {
        // Taking a MediatR command as the tool parameter generates a nested {"command":{...}}
        // schema — callers sending flat args fail with "missing ... required parameter 'command'".
        // Every tool must expose its arguments flat, like create_trade does.
        foreach (var tool in Tools())
        {
            var schema = tool.ProtocolTool.InputSchema;
            if (!schema.TryGetProperty("properties", out var props)) continue;
            props.EnumerateObject().Select(p => p.Name).Should()
                .NotContain("command", $"{tool.ProtocolTool.Name} must take flat args, not a command wrapper");
        }
    }

    [Fact]
    public void Optional_Params_Are_Not_Required_In_Schema()
    {
        // Params with C# defaults must drop out of the schema's "required" array —
        // otherwise spec-compliant hosts can't omit them despite "bỏ trống = ..." descriptions.
        var schema = Tools().ToDictionary(t => t.ProtocolTool.Name, t => t.ProtocolTool.InputSchema);

        static string[] Required(System.Text.Json.JsonElement el) =>
            el.TryGetProperty("required", out var req)
                ? req.EnumerateArray().Select(e => e.GetString()!).ToArray()
                : Array.Empty<string>();

        // Write tools: only genuinely mandatory fields are required; the rest are omittable.
        Required(schema["create_journal_entry"]).Should()
            .BeEquivalentTo(new[] { "symbol", "entryType", "title", "content" });
        Required(schema["update_journal_entry"]).Should().BeEquivalentTo(new[] { "id" });
        Required(schema["create_journal"]).Should().BeEquivalentTo(new[] { "tradeId", "portfolioId" });
        Required(schema["create_trade_plan"]).Should()
            .BeEquivalentTo(new[] { "symbol", "entryPrice", "stopLoss", "target", "quantity" });
        Required(schema["create_watchlist"]).Should().BeEquivalentTo(new[] { "name" });
        Required(schema["import_vn30"]).Should().BeEmpty();
        Required(schema["update_journal"]).Should().BeEquivalentTo(new[] { "id" });
        Required(schema["update_trade_plan"]).Should().BeEquivalentTo(new[] { "id" });
        // reason là tuỳ chọn ở tầng schema — bắt buộc hay không do entity quyết theo chiều dời SL,
        // nên đánh dấu required ở đây sẽ chặn cả trường hợp siết SL vốn không cần lý do.
        Required(schema["move_stop_loss"]).Should().BeEquivalentTo(new[] { "id", "newStopLoss" });
        Required(schema["update_watchlist"]).Should().BeEquivalentTo(new[] { "id", "name" });
        Required(schema["add_watchlist_item"]).Should().BeEquivalentTo(new[] { "id", "symbol" });
        Required(schema["update_watchlist_item"]).Should().BeEquivalentTo(new[] { "id", "symbol" });

        Required(schema["get_savings_comparison"]).Should().BeEquivalentTo(new[] { "portfolioId" });
        Required(schema["get_flow_history"]).Should().BeEquivalentTo(new[] { "portfolioId" });
        Required(schema["get_campaign_analytics"]).Should().BeEmpty();
    }
}
