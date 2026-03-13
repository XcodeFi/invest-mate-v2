using InvestmentApp.Domain.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace InvestmentApp.Api.Controllers;

[ApiController]
[Route("api/v1/templates")]
public class TemplatesController : ControllerBase
{
    private readonly IMongoDatabase _database;

    public TemplatesController(IMongoDatabase database)
    {
        _database = database;
    }

    private string GetUserId() =>
        User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException();

    /// <summary>
    /// Get all strategy templates (public, no auth required)
    /// </summary>
    [HttpGet("strategies")]
    [ProducesResponseType(typeof(IEnumerable<StrategyTemplate>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStrategyTemplates(
        [FromQuery] string? category = null,
        [FromQuery] string? difficulty = null,
        [FromQuery] string? timeFrame = null)
    {
        var collection = _database.GetCollection<StrategyTemplate>("strategy_templates");
        var filterBuilder = Builders<StrategyTemplate>.Filter;
        var filter = filterBuilder.Empty;

        if (!string.IsNullOrEmpty(category))
            filter &= filterBuilder.Eq(t => t.Category, category);
        if (!string.IsNullOrEmpty(difficulty))
            filter &= filterBuilder.Eq(t => t.DifficultyLevel, difficulty);
        if (!string.IsNullOrEmpty(timeFrame))
            filter &= filterBuilder.Eq(t => t.TimeFrame, timeFrame);

        var templates = await collection.Find(filter)
            .SortBy(t => t.SortOrder)
            .ToListAsync();

        return Ok(templates);
    }

    /// <summary>
    /// Get a single strategy template by ID
    /// </summary>
    [HttpGet("strategies/{id}")]
    [ProducesResponseType(typeof(StrategyTemplate), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStrategyTemplate(string id)
    {
        var collection = _database.GetCollection<StrategyTemplate>("strategy_templates");
        var template = await collection.Find(t => t.Id == id).FirstOrDefaultAsync();
        if (template == null) return NotFound(new { message = "Strategy template not found" });
        return Ok(template);
    }

    /// <summary>
    /// Get all risk profile templates (public, no auth required)
    /// </summary>
    [HttpGet("risk-profiles")]
    [ProducesResponseType(typeof(IEnumerable<RiskProfileTemplate>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRiskProfileTemplates()
    {
        var collection = _database.GetCollection<RiskProfileTemplate>("risk_profile_templates");
        var templates = await collection.Find(FilterDefinition<RiskProfileTemplate>.Empty)
            .SortBy(t => t.SortOrder)
            .ToListAsync();
        return Ok(templates);
    }

    /// <summary>
    /// Get a single risk profile template by ID
    /// </summary>
    [HttpGet("risk-profiles/{id}")]
    [ProducesResponseType(typeof(RiskProfileTemplate), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRiskProfileTemplate(string id)
    {
        var collection = _database.GetCollection<RiskProfileTemplate>("risk_profile_templates");
        var template = await collection.Find(t => t.Id == id).FirstOrDefaultAsync();
        if (template == null) return NotFound(new { message = "Risk profile template not found" });
        return Ok(template);
    }

    // ─── Trade Plan Templates (user-specific) ────────────────────────────────

    /// <summary>
    /// Get all trade plan templates for current user
    /// </summary>
    [HttpGet("trade-plans")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(typeof(IEnumerable<TradePlanTemplate>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTradePlanTemplates()
    {
        var userId = GetUserId();
        var col = _database.GetCollection<TradePlanTemplate>("trade_plan_templates");
        var templates = await col.Find(t => t.UserId == userId)
            .SortByDescending(t => t.UpdatedAt)
            .ToListAsync();
        return Ok(templates);
    }

    /// <summary>
    /// Save a new trade plan template
    /// </summary>
    [HttpPost("trade-plans")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(typeof(TradePlanTemplate), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateTradePlanTemplate([FromBody] TradePlanTemplateRequest request)
    {
        var userId = GetUserId();
        var col = _database.GetCollection<TradePlanTemplate>("trade_plan_templates");

        var template = new TradePlanTemplate
        {
            Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString(),
            UserId = userId,
            Name = request.Name,
            Symbol = request.Symbol,
            Direction = request.Direction ?? "Buy",
            EntryPrice = request.EntryPrice,
            StopLoss = request.StopLoss,
            Target = request.Target,
            StrategyId = request.StrategyId,
            MarketCondition = request.MarketCondition ?? "Trending",
            Reason = request.Reason,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await col.InsertOneAsync(template);
        return CreatedAtAction(nameof(GetTradePlanTemplates), new { id = template.Id }, template);
    }

    /// <summary>
    /// Delete a trade plan template
    /// </summary>
    [HttpDelete("trade-plans/{id}")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteTradePlanTemplate(string id)
    {
        var userId = GetUserId();
        var col = _database.GetCollection<TradePlanTemplate>("trade_plan_templates");
        var result = await col.DeleteOneAsync(t => t.Id == id && t.UserId == userId);
        if (result.DeletedCount == 0) return NotFound(new { message = "Template not found" });
        return NoContent();
    }
}

public record TradePlanTemplateRequest(
    string Name,
    string? Symbol,
    string? Direction,
    decimal? EntryPrice,
    decimal? StopLoss,
    decimal? Target,
    string? StrategyId,
    string? MarketCondition,
    string? Reason,
    string? Notes
);
