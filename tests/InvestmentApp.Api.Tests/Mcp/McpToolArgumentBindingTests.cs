using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using InvestmentApp.Api.Mcp;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Decisions.Commands.ResolveDecision;
using InvestmentApp.Application.JournalEntries.Commands.CreateJournalEntry;
using InvestmentApp.Application.JournalEntries.Commands.UpdateJournalEntry;
using InvestmentApp.Application.Journals.Commands.CreateJournal;
using InvestmentApp.Application.Journals.Commands.UpdateJournal;
using InvestmentApp.Application.TradePlans.Commands.CreateTradePlan;
using InvestmentApp.Application.TradePlans.Commands.UpdateStopLoss;
using InvestmentApp.Application.TradePlans.Commands.UpdateTradePlan;
using InvestmentApp.Application.Watchlists.Commands.AddWatchlistItem;
using InvestmentApp.Application.Watchlists.Commands.CreateWatchlist;
using InvestmentApp.Application.Watchlists.Commands.ImportVn30;
using InvestmentApp.Application.Watchlists.Commands.UpdateWatchlist;
using InvestmentApp.Application.Watchlists.Commands.UpdateWatchlistItem;
using InvestmentApp.Application.Watchlists.Dtos;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Moq;

namespace InvestmentApp.Api.Tests.Mcp;

/// <summary>
/// Write tools must accept FLAT arguments. Taking a MediatR command as the tool parameter
/// makes the SDK generate a nested {"command":{...}} schema, so a caller sending flat args
/// gets "missing a value for the required parameter 'command'" on every attempt.
/// </summary>
public class McpToolArgumentBindingTests
{
    private const string TestUserId = "user-123";

    /// <summary>Invokes a tool with flat JSON args and returns the command MediatR received.</summary>
    private static async Task<TCommand> CallAsync<TCommand, TResponse>(
        string toolName, string flatArgsJson, TResponse response)
        where TCommand : class
    {
        TCommand? captured = null;
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<IRequest<TResponse>>(), It.IsAny<CancellationToken>()))
                // Callback must be typed as the declared param (IRequest<T>), not the concrete command.
                .Callback<IRequest<TResponse>, CancellationToken>((req, _) => captured = req as TCommand)
                .ReturnsAsync(response);

        var http = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", TestUserId) }))
            }
        };

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(mediator.Object);
        services.AddSingleton(Mock.Of<IFeeCalculationService>());
        services.AddSingleton(Mock.Of<IAiAssistantService>());
        services.AddSingleton<IHttpContextAccessor>(http);
        services.AddMcpServer().WithToolsFromAssembly(typeof(PortfolioTools).Assembly);
        var sp = services.BuildServiceProvider();

        var tool = sp.GetServices<McpServerTool>().First(t => t.ProtocolTool.Name == toolName);
        var ctx = new RequestContext<CallToolRequestParams>(
            Mock.Of<McpServer>(), new JsonRpcRequest { Method = "tools/call" })
        {
            Services = sp,
            Params = new CallToolRequestParams
            {
                Name = toolName,
                Arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(flatArgsJson)
            }
        };

        var result = await tool.InvokeAsync(ctx, CancellationToken.None);
        result.IsError.Should().NotBe(true, $"{toolName} should accept flat arguments");
        captured.Should().NotBeNull($"{toolName} should forward a {typeof(TCommand).Name} to MediatR");
        return captured!;
    }

    [Fact]
    public async Task Hold_Decision_Accepts_Flat_Args_And_Always_Holds()
    {
        var cmd = await CallAsync<ResolveDecisionCommand, ResolveDecisionResult>("hold_decision",
            """
            {"decisionId":"StopLossHit:pf-1:HHV","note":"Nền hỗ trợ 10.000 vẫn giữ, chưa vỡ khối lượng",
             "tradePlanId":"plan-1","symbol":"HHV","portfolioId":"pf-1"}
            """,
            new ResolveDecisionResult { ResultId = "j1", Message = "ok", ResultType = "JournalEntry" });

        cmd.UserId.Should().Be(TestUserId);
        cmd.DecisionId.Should().Be("StopLossHit:pf-1:HHV");
        cmd.Note.Should().Be("Nền hỗ trợ 10.000 vẫn giữ, chưa vỡ khối lượng");
        cmd.TradePlanId.Should().Be("plan-1");
        cmd.Symbol.Should().Be("HHV");
        // portfolioId phải tới được command: thiếu nó thì cờ dập cảnh báo rơi về phạm vi
        // (mã, loại) và giấu luôn cảnh báo cùng mã ở danh mục khác.
        cmd.PortfolioId.Should().Be("pf-1");
        // Điểm cốt tử: action LUÔN là HoldWithJournal. Không có tham số nào đặt được ExecuteSell —
        // đường tạo lệnh bán với khối lượng cứng theo kế hoạch cố tình không mở ra MCP.
        cmd.Action.Should().Be(DecisionAction.HoldWithJournal);
    }

    [Fact]
    public async Task Hold_Decision_Accepts_Only_Required_Args()
    {
        var cmd = await CallAsync<ResolveDecisionCommand, ResolveDecisionResult>("hold_decision",
            """{"decisionId":"BuyOpportunity:VCB","note":"Chờ xác nhận khối lượng trước khi vào"}""",
            new ResolveDecisionResult { ResultId = "j1", Message = "ok", ResultType = "JournalEntry" });

        cmd.TradePlanId.Should().BeNull();
        cmd.Symbol.Should().BeNull();
        cmd.PortfolioId.Should().BeNull();
        cmd.Action.Should().Be(DecisionAction.HoldWithJournal);
    }

    [Fact]
    public async Task Move_Stop_Loss_Accepts_Flat_Args()
    {
        var cmd = await CallAsync<UpdateStopLossCommand, Unit>("move_stop_loss",
            """{"id":"plan-1","newStopLoss":71000,"reason":"Pyramid xong, dời cả cụm lên"}""",
            Unit.Value);

        cmd.PlanId.Should().Be("plan-1");
        cmd.NewStopLoss.Should().Be(71_000m);
        cmd.Reason.Should().Be("Pyramid xong, dời cả cụm lên");
        cmd.UserId.Should().Be(TestUserId);
    }

    [Fact]
    public async Task Move_Stop_Loss_Accepts_Omitted_Reason()
    {
        // Siết SL không cần lý do; entity mới là chỗ quyết bắt buộc hay không theo chiều dời.
        var cmd = await CallAsync<UpdateStopLossCommand, Unit>("move_stop_loss",
            """{"id":"plan-1","newStopLoss":71000}""", Unit.Value);

        cmd.Reason.Should().BeNull();
    }

    [Fact]
    public async Task Create_Journal_Entry_Accepts_Flat_Args()
    {
        var cmd = await CallAsync<CreateJournalEntryCommand, string>("create_journal_entry",
            """
            {"symbol":"HHV","entryType":"Observation","title":"Khối ngoại xả",
             "content":"Khối ngoại bán ròng phiên thứ 3 liên tiếp","marketContext":"VNIndex điều chỉnh",
             "tags":["khoi-ngoai","quan-sat"],"confidenceLevel":4}
            """, "entry-id");

        cmd.UserId.Should().Be(TestUserId);
        cmd.Symbol.Should().Be("HHV");
        cmd.EntryType.Should().Be("Observation");
        cmd.Title.Should().Be("Khối ngoại xả");
        cmd.Content.Should().Be("Khối ngoại bán ròng phiên thứ 3 liên tiếp");
        cmd.MarketContext.Should().Be("VNIndex điều chỉnh");
        cmd.Tags.Should().BeEquivalentTo("khoi-ngoai", "quan-sat");
        cmd.ConfidenceLevel.Should().Be(4);
    }

    [Fact]
    public async Task Create_Journal_Entry_Accepts_Only_Required_Args()
    {
        var cmd = await CallAsync<CreateJournalEntryCommand, string>("create_journal_entry",
            """{"symbol":"VCB","entryType":"Review","title":"T","content":"C"}""", "entry-id");

        cmd.Symbol.Should().Be("VCB");
        cmd.Tags.Should().BeNull();
        cmd.PortfolioId.Should().BeNull();
    }

    [Fact]
    public async Task Create_Journal_Accepts_Flat_Args()
    {
        var cmd = await CallAsync<CreateJournalCommand, string>("create_journal",
            """
            {"tradeId":"trade-1","portfolioId":"pf-1","entryReason":"Break kháng cự",
             "marketContext":"Khối ngoại xả","technicalSetup":"Cup&Handle","confidenceLevel":7}
            """, "journal-id");

        cmd.UserId.Should().Be(TestUserId);
        cmd.TradeId.Should().Be("trade-1");
        cmd.MarketContext.Should().Be("Khối ngoại xả");
        cmd.ConfidenceLevel.Should().Be(7);
    }

    [Fact]
    public async Task Update_Journal_Entry_Accepts_Flat_Args()
    {
        var cmd = await CallAsync<UpdateJournalEntryCommand, bool>("update_journal_entry",
            """{"id":"entry-1","title":"Tiêu đề mới","rating":4,"tags":["bai-hoc"]}""", true);

        cmd.Id.Should().Be("entry-1");
        cmd.UserId.Should().Be(TestUserId);
        cmd.Title.Should().Be("Tiêu đề mới");
        cmd.Rating.Should().Be(4);
        cmd.Tags.Should().BeEquivalentTo("bai-hoc");
    }

    [Fact]
    public async Task Create_Trade_Plan_Accepts_Flat_Args()
    {
        var cmd = await CallAsync<CreateTradePlanCommand, string>("create_trade_plan",
            """
            {"symbol":"FPT","direction":"Buy","entryPrice":120000,"stopLoss":112000,
             "target":140000,"quantity":100,"thesis":"Tăng trưởng CNTT","confidenceLevel":6}
            """, "plan-id");

        cmd.UserId.Should().Be(TestUserId);
        cmd.Symbol.Should().Be("FPT");
        cmd.EntryPrice.Should().Be(120000);
        cmd.StopLoss.Should().Be(112000);
        cmd.Target.Should().Be(140000);
        cmd.Quantity.Should().Be(100);
        cmd.ConfidenceLevel.Should().Be(6);
    }

    [Fact]
    public async Task Create_Trade_Plan_Binds_Complex_List_Args()
    {
        // Structured params stay typed lists — they must bind as flat top-level arrays of objects.
        var cmd = await CallAsync<CreateTradePlanCommand, string>("create_trade_plan",
            """
            {"symbol":"SSI","entryPrice":30000,"stopLoss":28000,"target":36000,"quantity":500,
             "checklist":[{"label":"Xu hướng tăng","category":"Kỹ thuật","checked":true,"critical":true,"hint":"EMA50"}],
             "invalidationCriteria":[{"trigger":"TrendBreak","detail":"Mất mốc 28"}],
             "exitTargets":[{"level":1,"actionType":"PartialExit","price":34000,"percentOfPosition":50,"label":"Chốt 1"}],
             "timeHorizon":"MediumTerm","expectedReviewDate":"2026-08-15T00:00:00Z"}
            """, "plan-id");

        cmd.Checklist.Should().HaveCount(1);
        cmd.Checklist![0].Label.Should().Be("Xu hướng tăng");
        cmd.Checklist[0].Critical.Should().BeTrue();
        cmd.InvalidationCriteria.Should().HaveCount(1);
        cmd.InvalidationCriteria![0].Detail.Should().Be("Mất mốc 28");
        cmd.ExitTargets.Should().HaveCount(1);
        cmd.ExitTargets![0].Price.Should().Be(34000);
        cmd.TimeHorizon.Should().Be("MediumTerm");
        cmd.ExpectedReviewDate.Should().Be(new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Update_Trade_Plan_Accepts_Flat_Args()
    {
        var cmd = await CallAsync<UpdateTradePlanCommand, Unit>("update_trade_plan",
            """{"id":"plan-1","stopLoss":76000,"thesis":"Nâng cắt lỗ","timeHorizon":"LongTerm"}""", Unit.Value);

        cmd.Id.Should().Be("plan-1");
        cmd.UserId.Should().Be(TestUserId);
        cmd.StopLoss.Should().Be(76000);
        cmd.TimeHorizon.Should().Be("LongTerm");
        cmd.Symbol.Should().BeNull("trường không truyền phải là null để handler giữ nguyên");
    }

    [Fact]
    public async Task Update_Journal_Accepts_Flat_Args()
    {
        var cmd = await CallAsync<UpdateJournalCommand, Unit>("update_journal",
            """{"id":"j-1","lessonsLearned":"Vào sớm quá","rating":3}""", Unit.Value);

        cmd.Id.Should().Be("j-1");
        cmd.UserId.Should().Be(TestUserId);
        cmd.LessonsLearned.Should().Be("Vào sớm quá");
        cmd.Rating.Should().Be(3);
        cmd.MarketContext.Should().BeNull();
    }

    [Fact]
    public async Task Create_Watchlist_Accepts_Flat_Args()
    {
        var cmd = await CallAsync<CreateWatchlistCommand, WatchlistDto>("create_watchlist",
            """{"name":"Ngân hàng","emoji":"🏦","sortOrder":2}""", new WatchlistDto());

        cmd.UserId.Should().Be(TestUserId);
        cmd.Name.Should().Be("Ngân hàng");
        cmd.Emoji.Should().Be("🏦");
        cmd.SortOrder.Should().Be(2);
    }

    [Fact]
    public async Task Update_Watchlist_Accepts_Flat_Args()
    {
        var cmd = await CallAsync<UpdateWatchlistCommand, Unit>("update_watchlist",
            """{"id":"w-1","name":"Tên mới","emoji":"🔥"}""", Unit.Value);

        cmd.Id.Should().Be("w-1");
        cmd.UserId.Should().Be(TestUserId);
        cmd.Name.Should().Be("Tên mới");
        cmd.Emoji.Should().Be("🔥");
    }

    [Fact]
    public async Task Add_Watchlist_Item_Accepts_Flat_Args()
    {
        var cmd = await CallAsync<AddWatchlistItemCommand, WatchlistDetailDto>("add_watchlist_item",
            """{"id":"w-1","symbol":"MBB","note":"Chờ nhịp điều chỉnh","targetBuyPrice":22000}""",
            new WatchlistDetailDto());

        cmd.WatchlistId.Should().Be("w-1");
        cmd.UserId.Should().Be(TestUserId);
        cmd.Symbol.Should().Be("MBB");
        cmd.Note.Should().Be("Chờ nhịp điều chỉnh");
        cmd.TargetBuyPrice.Should().Be(22000);
    }

    [Fact]
    public async Task Update_Watchlist_Item_Accepts_Flat_Args()
    {
        var cmd = await CallAsync<UpdateWatchlistItemCommand, WatchlistDetailDto>("update_watchlist_item",
            """{"id":"w-1","symbol":"MBB","targetSellPrice":28000}""", new WatchlistDetailDto());

        cmd.WatchlistId.Should().Be("w-1");
        cmd.UserId.Should().Be(TestUserId);
        cmd.Symbol.Should().Be("MBB");
        cmd.TargetSellPrice.Should().Be(28000);
    }

    [Fact]
    public async Task Import_Vn30_Accepts_No_Args()
    {
        var cmd = await CallAsync<ImportVn30Command, WatchlistDetailDto>("import_vn30",
            "{}", new WatchlistDetailDto());

        cmd.UserId.Should().Be(TestUserId);
        cmd.WatchlistId.Should().BeNull();
    }
}
