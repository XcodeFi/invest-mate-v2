using InvestmentApp.Api.Auth;
using InvestmentApp.Application.Portfolios.Queries.GetAllPortfolios;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

/// <summary>Liệt kê danh mục cho agent (scheme ApiKey) — để lấy portfolioId khi ghi trade. Mirror GetAllPortfoliosQuery.</summary>
[ApiController]
[Route("api/v1/ai/agent/portfolios")]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.Scheme)]
public class AiAgentPortfoliosController : AiAgentControllerBase
{
    public AiAgentPortfoliosController(IMediator mediator) : base(mediator) { }

    [HttpGet]
    [ProducesResponseType(typeof(List<PortfolioSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPortfolios()
        => Ok(await _mediator.Send(new GetAllPortfoliosQuery { UserId = GetUserId() }));
}
