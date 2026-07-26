using FluentAssertions;
using InvestmentApp.Api.Mcp;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.MarketData.Queries.GetBatchPrices;
using InvestmentApp.Application.MarketData.Queries.GetMarketOverview;
using InvestmentApp.Application.MarketData.Queries.GetStockDetail;
using InvestmentApp.Application.MarketData.Queries.GetStockPrice;
using InvestmentApp.Application.MarketData.Queries.GetStockPriceHistory;
using InvestmentApp.Application.MarketData.Queries.GetTechnicalAnalysis;
using InvestmentApp.Application.MarketData.Queries.GetTopFluctuation;
using InvestmentApp.Application.MarketData.Queries.SearchStocks;
using MediatR;
using Moq;

namespace InvestmentApp.Api.Tests.Mcp;

/// <summary>
/// Market data is public — these queries carry no UserId, so the tools inject no IHttpContextAccessor.
/// Symbols are normalized to uppercase in the wrapper (project-wide symbol convention).
/// </summary>
public class MarketToolsTests
{
    private readonly Mock<IMediator> _mediator = new();

    [Fact]
    public async Task GetStockDetail_NormalizesSymbol()
    {
        McpTestContext.Capture<StockDetailDto, GetStockDetailQuery>(
            _mediator, out var sent, new StockDetailDto());
        await MarketTools.GetStockDetail(" fpt ", _mediator.Object, CancellationToken.None);
        sent()!.Symbol.Should().Be("FPT");
    }

    [Fact]
    public async Task GetStockPrice_NormalizesSymbol()
    {
        McpTestContext.Capture<StockPriceDto, GetStockPriceQuery>(
            _mediator, out var sent, new StockPriceDto());
        await MarketTools.GetStockPrice("hpg", _mediator.Object, CancellationToken.None);
        sent()!.Symbol.Should().Be("HPG");
    }

    [Fact]
    public async Task GetStockPriceHistory_PassesExplicitRange()
    {
        var from = new DateTime(2026, 01, 01, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 06, 30, 0, 0, 0, DateTimeKind.Utc);
        McpTestContext.Capture<List<StockPriceHistoryDto>, GetStockPriceHistoryQuery>(
            _mediator, out var sent, new List<StockPriceHistoryDto>());
        await MarketTools.GetStockPriceHistory("vnm", _mediator.Object, CancellationToken.None, from, to);
        sent()!.Symbol.Should().Be("VNM");
        sent()!.From.Should().Be(from);
        sent()!.To.Should().Be(to);
    }

    [Fact]
    public async Task GetStockPriceHistory_DefaultsToLastThreeMonths()
    {
        McpTestContext.Capture<List<StockPriceHistoryDto>, GetStockPriceHistoryQuery>(
            _mediator, out var sent, new List<StockPriceHistoryDto>());
        var before = DateTime.UtcNow;
        await MarketTools.GetStockPriceHistory("SSI", _mediator.Object, CancellationToken.None);
        var after = DateTime.UtcNow;

        sent()!.To.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        sent()!.From.Should().BeOnOrAfter(before.AddMonths(-3)).And.BeOnOrBefore(after.AddMonths(-3));
    }

    [Fact]
    public async Task GetTechnicalAnalysis_NormalizesSymbol()
    {
        McpTestContext.Capture<TechnicalAnalysisResult, GetTechnicalAnalysisQuery>(
            _mediator, out var sent, new TechnicalAnalysisResult());
        await MarketTools.GetTechnicalAnalysis("mwg", _mediator.Object, CancellationToken.None);
        sent()!.Symbol.Should().Be("MWG");
    }

    [Fact]
    public async Task SearchStocks_PassesKeyword()
    {
        McpTestContext.Capture<List<StockSearchDto>, SearchStocksQuery>(
            _mediator, out var sent, new List<StockSearchDto>());
        await MarketTools.SearchStocks("ngân hàng", _mediator.Object, CancellationToken.None);
        sent()!.Keyword.Should().Be("ngân hàng");
    }

    [Fact]
    public async Task GetMarketOverview_DispatchesQuery()
    {
        McpTestContext.Capture<List<MarketOverviewDto>, GetMarketOverviewQuery>(
            _mediator, out var sent, new List<MarketOverviewDto>());
        await MarketTools.GetMarketOverview(_mediator.Object, CancellationToken.None);
        sent().Should().NotBeNull();
    }

    [Fact]
    public async Task GetTopFluctuation_DefaultsToHose()
    {
        McpTestContext.Capture<List<TopFluctuationDto>, GetTopFluctuationQuery>(
            _mediator, out var sent, new List<TopFluctuationDto>());
        await MarketTools.GetTopFluctuation(_mediator.Object, CancellationToken.None);
        sent()!.Floor.Should().Be("10");
    }

    [Fact]
    public async Task GetTopFluctuation_PassesExplicitFloor()
    {
        McpTestContext.Capture<List<TopFluctuationDto>, GetTopFluctuationQuery>(
            _mediator, out var sent, new List<TopFluctuationDto>());
        await MarketTools.GetTopFluctuation(_mediator.Object, CancellationToken.None, "02");
        sent()!.Floor.Should().Be("02");
    }

    [Fact]
    public async Task GetBatchPrices_NormalizesEachSymbol()
    {
        McpTestContext.Capture<List<BatchPriceDto>, GetBatchPricesQuery>(
            _mediator, out var sent, new List<BatchPriceDto>());
        await MarketTools.GetBatchPrices(new List<string> { "fpt", " hpg " }, _mediator.Object, CancellationToken.None);
        sent()!.Symbols.Should().BeEquivalentTo(new[] { "FPT", "HPG" });
    }

    [Fact]
    public async Task GetBatchPrices_DropsBlankEntries()
    {
        McpTestContext.Capture<List<BatchPriceDto>, GetBatchPricesQuery>(
            _mediator, out var sent, new List<BatchPriceDto>());
        await MarketTools.GetBatchPrices(new List<string> { "fpt", "", "   ", null! }, _mediator.Object, CancellationToken.None);
        sent()!.Symbols.Should().BeEquivalentTo(new[] { "FPT" });
    }

    [Fact]
    public async Task SearchStocks_RejectsBlankKeyword()
    {
        var act = () => MarketTools.SearchStocks("   ", _mediator.Object, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
