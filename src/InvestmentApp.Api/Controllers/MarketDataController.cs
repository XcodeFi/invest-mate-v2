using InvestmentApp.Application.MarketData.Queries.GetStockPrice;
using InvestmentApp.Application.MarketData.Queries.GetStockPriceHistory;
using InvestmentApp.Application.MarketData.Queries.GetBatchPrices;
using InvestmentApp.Application.MarketData.Queries.GetMarketIndex;
using InvestmentApp.Application.MarketData.Queries.GetStockDetail;
using InvestmentApp.Application.MarketData.Queries.GetMarketOverview;
using InvestmentApp.Application.MarketData.Queries.SearchStocks;
using InvestmentApp.Application.MarketData.Queries.GetTopFluctuation;
using InvestmentApp.Application.MarketData.Queries.GetTradingHistorySummary;
using InvestmentApp.Application.MarketData.Queries.GetTechnicalAnalysis;
using InvestmentApp.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

[ApiController]
[Route("api/v1/market")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class MarketDataController : ControllerBase
{
    private readonly IMediator _mediator;

    public MarketDataController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Get current price for a stock symbol
    /// </summary>
    [HttpGet("price/{symbol}")]
    [ProducesResponseType(typeof(StockPriceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrentPrice(string symbol)
    {
        try
        {
            var query = new GetStockPriceQuery { Symbol = symbol };
            var result = await _mediator.Send(query);
            return Ok(result);
        }
        catch (ArgumentException)
        {
            return NotFound(new { message = $"No price data found for symbol {symbol}" });
        }
    }

    /// <summary>
    /// Get historical prices for a stock symbol
    /// </summary>
    [HttpGet("price/{symbol}/history")]
    [ProducesResponseType(typeof(List<StockPriceHistoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPriceHistory(
        string symbol,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var query = new GetStockPriceHistoryQuery
        {
            Symbol = symbol,
            From = from ?? DateTime.UtcNow.AddMonths(-3),
            To = to ?? DateTime.UtcNow
        };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Get current prices for multiple symbols
    /// </summary>
    [HttpGet("prices")]
    [ProducesResponseType(typeof(List<BatchPriceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBatchPrices([FromQuery] string symbols)
    {
        if (string.IsNullOrWhiteSpace(symbols))
            return BadRequest(new { message = "Symbols parameter is required" });

        var symbolList = symbols.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var query = new GetBatchPricesQuery { Symbols = symbolList };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Get market index data (VNINDEX, VN30, HNX)
    /// </summary>
    [HttpGet("index/{symbol}")]
    [ProducesResponseType(typeof(MarketIndexDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMarketIndex(string symbol)
    {
        try
        {
            var query = new GetMarketIndexQuery { IndexSymbol = symbol };
            var result = await _mediator.Send(query);
            return Ok(result);
        }
        catch (ArgumentException)
        {
            return NotFound(new { message = $"No data found for index {symbol}" });
        }
    }

    // =============================================
    // Extended Stock Info Endpoints (24hmoney.vn)
    // =============================================

    /// <summary>
    /// Get comprehensive stock detail including company info, order book, and foreign trading
    /// </summary>
    [HttpGet("stock/{symbol}/detail")]
    [ProducesResponseType(typeof(StockDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStockDetail(string symbol)
    {
        try
        {
            var query = new GetStockDetailQuery { Symbol = symbol };
            var result = await _mediator.Send(query);
            return Ok(result);
        }
        catch (ArgumentException)
        {
            return NotFound(new { message = $"Không tìm thấy thông tin cho mã {symbol}" });
        }
    }

    /// <summary>
    /// Get market overview - all major indices (VN-INDEX, HNX-INDEX, UPCOM-INDEX)
    /// </summary>
    [HttpGet("overview")]
    [ProducesResponseType(typeof(List<MarketOverviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMarketOverview()
    {
        var query = new GetMarketOverviewQuery();
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Search stocks by symbol or company name
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(List<StockSearchDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchStocks([FromQuery] string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return BadRequest(new { message = "Keyword parameter is required" });

        var query = new SearchStocksQuery { Keyword = keyword };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Get top fluctuating stocks (gainers/losers)
    /// </summary>
    /// <param name="floor">Floor code: "10" = HOSE, "02" = HNX, "03" = UPCOM</param>
    [HttpGet("top-fluctuation")]
    [ProducesResponseType(typeof(List<TopFluctuationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTopFluctuation([FromQuery] string floor = "10")
    {
        var query = new GetTopFluctuationQuery { Floor = floor };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Get trading history summary (change % by day, week, month, 3m, 6m)
    /// </summary>
    [HttpGet("stock/{symbol}/summary")]
    [ProducesResponseType(typeof(TradingHistorySummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTradingHistorySummary(string symbol)
    {
        try
        {
            var query = new GetTradingHistorySummaryQuery { Symbol = symbol };
            var result = await _mediator.Send(query);
            return Ok(result);
        }
        catch (ArgumentException)
        {
            return NotFound(new { message = $"Không tìm thấy thông tin giao dịch cho mã {symbol}" });
        }
    }

    /// <summary>
    /// Get technical analysis (EMA, RSI, MACD, Volume, Support/Resistance, Signal)
    /// </summary>
    [HttpGet("stock/{symbol}/analysis")]
    [ProducesResponseType(typeof(TechnicalAnalysisResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTechnicalAnalysis(string symbol)
    {
        var query = new GetTechnicalAnalysisQuery { Symbol = symbol };
        var result = await _mediator.Send(query);
        return Ok(result);
    }
}
