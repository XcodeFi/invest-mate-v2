using InvestmentApp.Api.Auth;
using InvestmentApp.Application.TradePlans.Queries.GetActivePositions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

/// <summary>Đọc holdings thật cho agent (scheme ApiKey). Mirror PositionsController.</summary>
[ApiController]
[Route("api/v1/ai/agent/positions")]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.Scheme)]
public class AiAgentPositionsController : AiAgentControllerBase
{
    public AiAgentPositionsController(IMediator mediator) : base(mediator) { }

    [HttpGet]
    [ProducesResponseType(typeof(List<ActivePositionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActivePositions([FromQuery] string? portfolioId = null)
    {
        var query = new GetActivePositionsQuery { UserId = GetUserId(), PortfolioId = portfolioId };
        return Ok(await _mediator.Send(query));
    }
}
