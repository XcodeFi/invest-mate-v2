using InvestmentApp.Application.Risk.Commands.SetRiskProfile;
using InvestmentApp.Application.Risk.Commands.SetStopLossTarget;
using InvestmentApp.Application.Risk.Queries.GetRiskProfile;
using InvestmentApp.Application.Risk.Queries.GetPortfolioRisk;
using InvestmentApp.Application.Risk.Queries.GetDrawdown;
using InvestmentApp.Application.Risk.Queries.GetStopLossTargets;
using InvestmentApp.Application.Risk.Queries.GetCorrelation;
using InvestmentApp.Application.Risk.Queries.GetPortfolioOptimization;
using InvestmentApp.Application.Risk.Queries.GetTrailingStopAlerts;
using InvestmentApp.Application.Risk.Queries.GetStressTest;
using InvestmentApp.Application.Risk.Queries.GetRiskBudget;
using InvestmentApp.Application.Risk.Queries.GetSectorExposureForPlan;
using InvestmentApp.Application.Risk.Queries.GetVolatilitySizingForPlan;
using InvestmentApp.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

[ApiController]
[Route("api/v1/risk")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class RiskController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPositionSizingService _positionSizing;

    public RiskController(IMediator mediator, IPositionSizingService positionSizing)
    {
        _mediator = mediator;
        _positionSizing = positionSizing;
    }

    private string GetUserId() =>
        User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException();

    /// <summary>
    /// Get risk profile for a portfolio
    /// </summary>
    [HttpGet("portfolio/{portfolioId}/profile")]
    [ProducesResponseType(typeof(RiskProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRiskProfile(string portfolioId)
    {
        var query = new GetRiskProfileQuery
        {
            PortfolioId = portfolioId,
            UserId = GetUserId()
        };
        var result = await _mediator.Send(query);
        if (result == null)
            return NotFound(new { message = "No risk profile found. Create one to set risk limits." });
        return Ok(result);
    }

    /// <summary>
    /// Set or update risk profile for a portfolio
    /// </summary>
    [HttpPost("portfolio/{portfolioId}/profile")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetRiskProfile(string portfolioId, [FromBody] SetRiskProfileCommand command)
    {
        command.PortfolioId = portfolioId;
        command.UserId = GetUserId();
        var id = await _mediator.Send(command);
        return Ok(new { id });
    }

    /// <summary>
    /// Get portfolio risk summary (positions, drawdown, VaR)
    /// </summary>
    [HttpGet("portfolio/{portfolioId}/summary")]
    [ProducesResponseType(typeof(PortfolioRiskSummary), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPortfolioRisk(string portfolioId)
    {
        var query = new GetPortfolioRiskQuery
        {
            PortfolioId = portfolioId,
            UserId = GetUserId()
        };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Get max drawdown analysis
    /// </summary>
    [HttpGet("portfolio/{portfolioId}/drawdown")]
    [ProducesResponseType(typeof(DrawdownResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDrawdown(string portfolioId)
    {
        var query = new GetDrawdownQuery
        {
            PortfolioId = portfolioId,
            UserId = GetUserId()
        };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Get correlation matrix for portfolio symbols
    /// </summary>
    [HttpGet("portfolio/{portfolioId}/correlation")]
    [ProducesResponseType(typeof(CorrelationMatrix), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCorrelation(string portfolioId)
    {
        var query = new GetCorrelationQuery
        {
            PortfolioId = portfolioId,
            UserId = GetUserId()
        };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Get stop-loss/target settings for a portfolio
    /// </summary>
    [HttpGet("portfolio/{portfolioId}/stop-loss")]
    [ProducesResponseType(typeof(StopLossTargetsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStopLossTargets(string portfolioId)
    {
        var query = new GetStopLossTargetsQuery
        {
            PortfolioId = portfolioId,
            UserId = GetUserId()
        };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Set stop-loss and target for a trade
    /// </summary>
    [HttpPost("stop-loss")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetStopLossTarget([FromBody] SetStopLossTargetCommand command)
    {
        command.UserId = GetUserId();
        var id = await _mediator.Send(command);
        return Ok(new { id });
    }

    /// <summary>
    /// Get portfolio optimization analysis (concentration, sector diversification, correlation warnings)
    /// </summary>
    [HttpGet("portfolio/{portfolioId}/optimization")]
    [ProducesResponseType(typeof(PortfolioOptimizationResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPortfolioOptimization(string portfolioId)
    {
        var query = new GetPortfolioOptimizationQuery
        {
            PortfolioId = portfolioId,
            UserId = GetUserId()
        };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Tỷ trọng ngành hiện tại và sau một lệnh dự kiến, cho bước kiểm-trước ở form lập kế hoạch.
    /// </summary>
    /// <remarks>
    /// <paramref name="symbol"/> và <paramref name="addValue"/> bắt buộc: thay giá trị thiếu bằng 0
    /// sẽ trả về một con số trông như thật (tỷ trọng "sau lệnh" bằng tỷ trọng hiện tại).
    /// </remarks>
    [HttpGet("portfolio/{portfolioId}/sector-exposure")]
    [ProducesResponseType(typeof(SectorExposureForPlan), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSectorExposureForPlan(
        string portfolioId, [FromQuery] string symbol, [FromQuery] decimal? addValue,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return BadRequest(new { code = "SYMBOL_REQUIRED", message = "Thiếu tham số symbol" });
        // decimal? chứ không phải decimal: kiểu không-nullable sẽ bind giá trị thiếu thành 0 trong
        // im lặng, và 0 làm "tỷ trọng sau lệnh" bằng đúng tỷ trọng hiện tại — một con số trông như
        // thật. Đây là chỗ tài liệu đã ghi tham số này bắt buộc, nên code phải bắt buộc thật.
        if (addValue is null)
            return BadRequest(new { code = "ADD_VALUE_REQUIRED", message = "Thiếu tham số addValue" });

        var query = new GetSectorExposureForPlanQuery
        {
            PortfolioId = portfolioId,
            UserId = GetUserId(),
            Symbol = symbol,
            AddValue = addValue.Value
        };
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Trần khối lượng theo ngân sách biến động cho một lệnh mua dự kiến (ADR-0014).
    /// </summary>
    /// <remarks>
    /// Cả ba tham số bắt buộc, và cả ba đều nullable ở chữ ký vì cùng lý do như
    /// <c>sector-exposure</c>: kiểu không-nullable bind giá trị thiếu thành 0 trong im lặng, mà
    /// <c>quantity = 0</c> cho "biến động sau lệnh" bằng đúng biến động hiện tại — một con số
    /// trông như thật.
    /// </remarks>
    [HttpGet("portfolio/{portfolioId}/volatility-sizing")]
    [ProducesResponseType(typeof(VolatilitySizingResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVolatilitySizingForPlan(
        string portfolioId, [FromQuery] string symbol, [FromQuery] decimal? entryPrice,
        [FromQuery] int? quantity, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return BadRequest(new { code = "SYMBOL_REQUIRED", message = "Thiếu tham số symbol" });
        if (entryPrice is null or <= 0)
            return BadRequest(new { code = "ENTRY_PRICE_REQUIRED", message = "Thiếu hoặc sai tham số entryPrice" });
        if (quantity is null or <= 0)
            return BadRequest(new { code = "QUANTITY_REQUIRED", message = "Thiếu hoặc sai tham số quantity" });

        var query = new GetVolatilitySizingForPlanQuery
        {
            PortfolioId = portfolioId,
            UserId = GetUserId(),
            Symbol = symbol,
            EntryPrice = entryPrice.Value,
            Quantity = quantity.Value
        };
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get trailing stop alerts with real-time price monitoring
    /// </summary>
    [HttpGet("portfolio/{portfolioId}/trailing-stop-alerts")]
    [ProducesResponseType(typeof(TrailingStopAlertsResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTrailingStopAlerts(string portfolioId)
    {
        var query = new GetTrailingStopAlertsQuery
        {
            PortfolioId = portfolioId,
            UserId = GetUserId()
        };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Run stress test with dynamic beta per position
    /// </summary>
    [HttpPost("portfolio/{portfolioId}/stress-test")]
    [ProducesResponseType(typeof(StressTestResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> RunStressTest(string portfolioId, [FromBody] StressTestRequest request)
    {
        var query = new GetStressTestQuery
        {
            PortfolioId = portfolioId,
            UserId = GetUserId(),
            MarketChangePercent = request.MarketChangePercent
        };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Get daily risk budget status (trade count and loss limits)
    /// </summary>
    [HttpGet("portfolio/{portfolioId}/budget")]
    [ProducesResponseType(typeof(RiskBudgetStatus), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRiskBudget(string portfolioId)
    {
        var query = new GetRiskBudgetQuery
        {
            PortfolioId = portfolioId,
            UserId = GetUserId()
        };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Calculate position sizing using multiple models for comparison.
    /// </summary>
    [HttpPost("position-sizing")]
    [ProducesResponseType(typeof(PositionSizingResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> CalculatePositionSizing([FromBody] PositionSizingRequest request)
    {
        var result = await _positionSizing.CalculateAsync(request);
        return Ok(result);
    }
}

public class StressTestRequest
{
    public decimal MarketChangePercent { get; set; }
}
