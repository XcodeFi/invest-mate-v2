using InvestmentApp.Api.Auth;
using InvestmentApp.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

/// <summary>Tính phí/thuế cho agent (scheme ApiKey) để preview trước khi ghi trade. Mirror FeesController.CalculateFees.</summary>
[ApiController]
[Route("api/v1/ai/agent/fees")]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.Scheme)]
public class AiAgentFeesController : AiAgentControllerBase
{
    private readonly IFeeCalculationService _feeService;

    public AiAgentFeesController(IMediator mediator, IFeeCalculationService feeService) : base(mediator)
        => _feeService = feeService;

    [HttpPost("calculate")]
    [ProducesResponseType(typeof(FeeCalculationResponse), StatusCodes.Status200OK)]
    public IActionResult Calculate([FromBody] FeeCalculationRequest request)
    {
        if (request.Quantity * request.Price <= 0)
            return BadRequest("Transaction amount must be positive");

        return Ok(AgentTradeFeeCalculator.Calculate(
            _feeService, request.TradeType, request.Quantity, request.Price));
    }
}
