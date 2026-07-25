using FluentAssertions;
using InvestmentApp.Api.Mcp;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace InvestmentApp.Api.Tests.Mcp;

/// <summary>
/// Discovery-level check: the SDK registers all 29 slice-1 tools with correct read/destructive
/// annotations. Resolves tools straight from DI (no app boot → no Mongo/secret dependency).
/// </summary>
public class McpToolDiscoveryTests
{
    private static readonly string[] ReadTools =
    {
        "list_trade_plans", "get_trade_plan", "list_portfolios", "list_positions",
        "calculate_fees", "get_symbol_timeline", "list_watchlists", "get_watchlist",
        "list_journals", "get_journal_by_trade", "list_trades_pending_review",
        "list_journal_entries_by_symbol"
    };

    private static readonly string[] WriteTools =
    {
        "create_trade_plan", "update_trade_plan", "set_trade_plan_status", "create_trade",
        "create_watchlist", "update_watchlist", "delete_watchlist", "add_watchlist_item",
        "update_watchlist_item", "remove_watchlist_item", "import_vn30",
        "create_journal", "update_journal", "delete_journal",
        "create_journal_entry", "update_journal_entry", "delete_journal_entry"
    };

    private static IReadOnlyList<McpServerTool> Tools()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMcpServer().WithToolsFromAssembly(typeof(PortfolioTools).Assembly);
        return services.BuildServiceProvider().GetServices<McpServerTool>().ToList();
    }

    [Fact]
    public void Registers_All_29_Slice1_Tools()
    {
        var names = Tools().Select(t => t.ProtocolTool.Name).ToHashSet();
        foreach (var name in ReadTools.Concat(WriteTools))
            names.Should().Contain(name);
        (ReadTools.Length + WriteTools.Length).Should().Be(29);
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
}
