using InvestmentApp.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

[ApiController]
[Route("api/v1/pnl")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class PnLController : ControllerBase
{
    private readonly IPnLService _pnlService;
    private readonly IPortfolioRepository _portfolioRepository;

    public PnLController(IPnLService pnlService, IPortfolioRepository portfolioRepository)
    {
        _pnlService = pnlService;
        _portfolioRepository = portfolioRepository;
    }

    private string GetUserId() =>
        User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException();

    /// <summary>
    /// Get P&L summary for a portfolio
    /// </summary>
    [HttpGet("portfolio/{portfolioId}")]
    [ProducesResponseType(typeof(Application.Portfolios.Queries.PortfolioPnLSummary), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPortfolioPnL(string portfolioId)
    {
        var userId = GetUserId();

        // Verify user owns the portfolio
        var portfolio = await _portfolioRepository.GetByIdAsync(portfolioId);
        if (portfolio == null || portfolio.UserId != userId)
            return NotFound();

        var pnlSummary = await _pnlService.CalculatePortfolioPnLAsync(portfolioId);
        return Ok(pnlSummary);
    }

    /// <summary>
    /// Get P&L for a specific position in a portfolio
    /// </summary>
    [HttpGet("portfolio/{portfolioId}/position/{symbol}")]
    [ProducesResponseType(typeof(Application.Portfolios.Queries.PositionPnL), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPositionPnL(string portfolioId, string symbol)
    {
        var userId = GetUserId();

        var portfolio = await _portfolioRepository.GetByIdAsync(portfolioId);
        if (portfolio == null || portfolio.UserId != userId)
            return NotFound();

        var stockSymbol = new Domain.ValueObjects.StockSymbol(symbol);
        var positionPnL = await _pnlService.CalculatePositionPnLAsync(portfolioId, stockSymbol);
        return Ok(positionPnL);
    }

    /// <summary>
    /// Get P&L summary across all user portfolios
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOverallPnL()
    {
        var userId = GetUserId();
        var portfolios = await _portfolioRepository.GetByUserIdAsync(userId);
        var portfolioList = portfolios.ToList();

        decimal totalInvested = 0;
        decimal totalUnrealizedPnL = 0;
        decimal totalRealizedPnL = 0;
        decimal totalPortfolioValue = 0;
        var portfolioPnLs = new List<object>();

        foreach (var portfolio in portfolioList)
        {
            try
            {
                var pnl = await _pnlService.CalculatePortfolioPnLAsync(portfolio.Id);
                totalInvested += pnl.TotalInvested;
                totalUnrealizedPnL += pnl.TotalUnrealizedPnL;
                totalRealizedPnL += pnl.TotalRealizedPnL;
                totalPortfolioValue += pnl.TotalPortfolioValue;

                portfolioPnLs.Add(new
                {
                    PortfolioId = portfolio.Id,
                    PortfolioName = portfolio.Name,
                    pnl.TotalInvested,
                    pnl.TotalUnrealizedPnL,
                    pnl.TotalRealizedPnL,
                    pnl.TotalPortfolioValue,
                    pnl.TotalPnL,
                    pnl.TotalReturnPercentage
                });
            }
            catch
            {
                // Skip portfolios with no trades
            }
        }

        return Ok(new
        {
            TotalPortfolios = portfolioList.Count,
            TotalInvested = totalInvested,
            TotalUnrealizedPnL = totalUnrealizedPnL,
            TotalRealizedPnL = totalRealizedPnL,
            TotalPortfolioValue = totalPortfolioValue,
            TotalPnL = totalRealizedPnL + totalUnrealizedPnL,
            TotalReturnPercentage = totalInvested > 0 ? ((totalRealizedPnL + totalUnrealizedPnL) / totalInvested) * 100 : 0,
            Portfolios = portfolioPnLs
        });
    }
}