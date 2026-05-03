using InvestmentApp.Application.Analytics.Queries.GetPerformance;
using InvestmentApp.Application.Analytics.Queries.GetEquityCurve;
using InvestmentApp.Application.Analytics.Queries.GetMonthlyReturns;
using InvestmentApp.Application.Analytics.Queries.GetSavingsComparison;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

[ApiController]
[Route("api/v1/analytics")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class AdvancedAnalyticsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IBankRateProvider _bankRateProvider;
    private readonly ICashFlowAdjustedReturnService _cashFlowAdjustedReturnService;

    public AdvancedAnalyticsController(
        IMediator mediator,
        IBankRateProvider bankRateProvider,
        ICashFlowAdjustedReturnService cashFlowAdjustedReturnService)
    {
        _mediator = mediator;
        _bankRateProvider = bankRateProvider;
        _cashFlowAdjustedReturnService = cashFlowAdjustedReturnService;
    }

    private string GetUserId() =>
        User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException();

    /// <summary>
    /// Get full performance metrics (CAGR, Sharpe, Sortino, WinRate, ProfitFactor, Expectancy)
    /// </summary>
    [HttpGet("portfolio/{portfolioId}/performance")]
    [ProducesResponseType(typeof(PerformanceSummary), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPerformance(string portfolioId)
    {
        var query = new GetPerformanceQuery
        {
            PortfolioId = portfolioId,
            UserId = GetUserId()
        };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Get equity curve data for charting
    /// </summary>
    [HttpGet("portfolio/{portfolioId}/equity-curve")]
    [ProducesResponseType(typeof(EquityCurveData), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEquityCurve(string portfolioId)
    {
        var query = new GetEquityCurveQuery
        {
            PortfolioId = portfolioId,
            UserId = GetUserId()
        };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Get monthly returns heatmap data
    /// </summary>
    [HttpGet("portfolio/{portfolioId}/monthly-returns")]
    [ProducesResponseType(typeof(MonthlyReturnsData), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMonthlyReturns(string portfolioId)
    {
        var query = new GetMonthlyReturnsQuery
        {
            PortfolioId = portfolioId,
            UserId = GetUserId()
        };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// So sánh hiệu suất danh mục với sổ tiết kiệm (opportunity cost).
    /// </summary>
    /// <param name="savingsRate">Lãi suất so sánh (decimal 0.05 = 5%/năm). Null → weighted avg của Savings accounts / fallback 5%.</param>
    /// <param name="asOf">Thời điểm chốt so sánh. Null → UtcNow.</param>
    [HttpGet("portfolio/{portfolioId}/vs-savings")]
    [ProducesResponseType(typeof(SavingsComparisonDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVsSavings(
        string portfolioId,
        [FromQuery] decimal? savingsRate = null,
        [FromQuery] DateTime? asOf = null,
        CancellationToken ct = default)
    {
        var query = new GetSavingsComparisonQuery
        {
            UserId = GetUserId(),
            PortfolioId = portfolioId,
            AnnualRate = savingsRate,
            AsOf = asOf,
        };
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// Household-level performance: TWR + CAGR aggregated across all portfolios of the caller.
    /// CAGR is annualized from a household snapshot series; <c>isStable=true</c> when the
    /// snapshot window spans at least 1 year — otherwise the CAGR is an extrapolation.
    /// </summary>
    [HttpGet("household/performance")]
    [ProducesResponseType(typeof(HouseholdReturnSummary), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHouseholdPerformance(CancellationToken ct)
    {
        var summary = await _cashFlowAdjustedReturnService.GetHouseholdReturnSummaryAsync(GetUserId(), ct);
        return Ok(summary);
    }

    /// <summary>
    /// Top bank rate per term (1/3/6/9/12 tháng) từ 24hmoney (kênh online). Cache 6h.
    /// </summary>
    [HttpGet("bank-rates")]
    [ProducesResponseType(typeof(BankRateSnapshot), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBankRates(CancellationToken ct)
    {
        var snapshot = await _bankRateProvider.GetTopRatesAsync(ct);
        return Ok(snapshot);
    }
}
