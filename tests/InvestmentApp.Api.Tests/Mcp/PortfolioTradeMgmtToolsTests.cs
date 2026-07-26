using FluentAssertions;
using InvestmentApp.Api.Mcp;
using InvestmentApp.Application.Portfolios.Commands.CreatePortfolio;
using InvestmentApp.Application.Portfolios.Commands.DeletePortfolio;
using InvestmentApp.Application.Portfolios.Commands.UpdatePortfolio;
using InvestmentApp.Application.Portfolios.Queries.GetPortfolio;
using InvestmentApp.Application.Trades.Commands.BulkCreateTrades;
using InvestmentApp.Application.Trades.Commands.DeleteTrade;
using InvestmentApp.Application.Trades.Commands.LinkTradeToPlan;
using InvestmentApp.Application.Trades.Queries.GetTradesByPortfolio;
using MediatR;
using Moq;

namespace InvestmentApp.Api.Tests.Mcp;

/// <summary>P3 — portfolio/trade management: 2 read + 6 write tools.</summary>
public class PortfolioTradeMgmtToolsTests
{
    private readonly Mock<IMediator> _mediator = new();

    [Fact]
    public async Task GetPortfolio_SetsUserId_AndId()
    {
        McpTestContext.Capture<PortfolioDto?, GetPortfolioQuery>(
            _mediator, out var sent, new PortfolioDto());
        await PortfolioTools.GetPortfolio("p1", _mediator.Object, McpTestContext.WithUser("u-1"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-1");
        sent()!.Id.Should().Be("p1");
    }

    [Fact]
    public async Task GetTradesByPortfolio_SetsUserId_AndDefaultsPaging()
    {
        McpTestContext.Capture<TradeListDto, GetTradesByPortfolioQuery>(
            _mediator, out var sent, new TradeListDto());
        await TradeTools.GetTradesByPortfolio("p2", _mediator.Object, McpTestContext.WithUser("u-2"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-2");
        sent()!.PortfolioId.Should().Be("p2");
        sent()!.Page.Should().Be(1);
        sent()!.PageSize.Should().Be(20);
        sent()!.Symbol.Should().BeNull();
        sent()!.TradeType.Should().BeNull();
    }

    [Fact]
    public async Task GetTradesByPortfolio_PassesFiltersAndPaging()
    {
        McpTestContext.Capture<TradeListDto, GetTradesByPortfolioQuery>(
            _mediator, out var sent, new TradeListDto());
        await TradeTools.GetTradesByPortfolio("p3", _mediator.Object, McpTestContext.WithUser("u-3"), CancellationToken.None,
            symbol: "fpt", tradeType: "BUY", page: 2, pageSize: 50);
        sent()!.Symbol.Should().Be("FPT");
        sent()!.TradeType.Should().Be("BUY");
        sent()!.Page.Should().Be(2);
        sent()!.PageSize.Should().Be(50);
    }

    [Fact]
    public async Task CreatePortfolio_SetsUserId_NameAndCapital()
    {
        McpTestContext.Capture<string, CreatePortfolioCommand>(_mediator, out var sent, "new-id");
        var id = await PortfolioTools.CreatePortfolio("Danh mục dài hạn", 100_000_000m,
            _mediator.Object, McpTestContext.WithUser("u-4"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-4");
        sent()!.Name.Should().Be("Danh mục dài hạn");
        sent()!.InitialCapital.Should().Be(100_000_000m);
        id.Should().Be("new-id");
    }

    [Fact]
    public async Task UpdatePortfolio_SetsUserId_IdAndName()
    {
        McpTestContext.Capture<bool, UpdatePortfolioCommand>(_mediator, out var sent, true);
        await PortfolioTools.UpdatePortfolio("p5", "Tên mới", _mediator.Object, McpTestContext.WithUser("u-5"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-5");
        sent()!.Id.Should().Be("p5");
        sent()!.Name.Should().Be("Tên mới");
    }

    [Fact]
    public async Task DeletePortfolio_SetsUserId_AndId()
    {
        McpTestContext.Capture<bool, DeletePortfolioCommand>(_mediator, out var sent, true);
        await PortfolioTools.DeletePortfolio("p6", _mediator.Object, McpTestContext.WithUser("u-6"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-6");
        sent()!.Id.Should().Be("p6");
    }

    [Fact]
    public async Task DeleteTrade_SetsUserId_AndTradeId()
    {
        McpTestContext.Capture<bool, DeleteTradeCommand>(_mediator, out var sent, true);
        await TradeTools.DeleteTrade("t1", _mediator.Object, McpTestContext.WithUser("u-7"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-7");
        sent()!.TradeId.Should().Be("t1");
    }

    [Fact]
    public async Task LinkTradeToPlan_SetsUserId_TradeIdAndPlanId()
    {
        McpTestContext.Capture<bool, LinkTradeToPlanCommand>(_mediator, out var sent, true);
        await TradeTools.LinkTradeToPlan("t2", "plan-1", _mediator.Object, McpTestContext.WithUser("u-8"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-8");
        sent()!.TradeId.Should().Be("t2");
        sent()!.PlanId.Should().Be("plan-1");
    }

    [Fact]
    public async Task BulkCreateTrades_SetsUserId_PortfolioIdAndItems()
    {
        McpTestContext.Capture<BulkCreateTradesResult, BulkCreateTradesCommand>(
            _mediator, out var sent, new BulkCreateTradesResult());
        var items = new List<BulkTradeItem>
        {
            new() { Symbol = "fpt", TradeType = "BUY", Quantity = 100, Price = 120_000m },
            new() { Symbol = "HPG", TradeType = "SELL", Quantity = 200, Price = 27_000m }
        };
        await TradeTools.BulkCreateTrades("p7", items, _mediator.Object, McpTestContext.WithUser("u-9"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-9");
        sent()!.PortfolioId.Should().Be("p7");
        sent()!.Trades.Should().HaveCount(2);
        sent()!.Trades[0].Symbol.Should().Be("FPT", "symbol chuẩn hóa uppercase tại wrapper");
    }

    [Fact]
    public async Task BulkCreateTrades_RejectsRowWithoutSymbol()
    {
        var items = new List<BulkTradeItem>
        {
            new() { Symbol = "FPT", TradeType = "BUY", Quantity = 100, Price = 120_000m },
            new() { Symbol = "  ", TradeType = "BUY", Quantity = 50, Price = 30_000m }
        };
        var act = () => TradeTools.BulkCreateTrades("p9", items,
            _mediator.Object, McpTestContext.WithUser("u-11"), CancellationToken.None);
        (await act.Should().ThrowAsync<ArgumentException>()).WithMessage("*thứ 2*");
    }

    [Fact]
    public async Task BulkCreateTrades_RejectsEmptyList()
    {
        var act = () => TradeTools.BulkCreateTrades("p8", new List<BulkTradeItem>(),
            _mediator.Object, McpTestContext.WithUser("u-10"), CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
