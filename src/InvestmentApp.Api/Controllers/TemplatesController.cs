using InvestmentApp.Domain.Entities;
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
}
