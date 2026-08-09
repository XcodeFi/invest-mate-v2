using InvestmentApp.Application.CorporateActions.Commands.CreateCorporateAction;
using InvestmentApp.Application.CorporateActions.Commands.DeleteCorporateAction;
using InvestmentApp.Application.CorporateActions.Commands.SettleCorporateAction;
using InvestmentApp.Application.CorporateActions.Queries.GetCorporateActions;
using InvestmentApp.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

[ApiController]
[Route("api/v1/corporate-actions")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class CorporateActionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CorporateActionsController(IMediator mediator) => _mediator = mediator;

    private string GetUserId() =>
        User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException();

    public record CreateRequest(
        string PortfolioId, string Symbol, CorporateActionType Type,
        DateTime ExDate, DateTime? SettlementDate,
        decimal? PercentOfPar, decimal? TaxRatePercent,
        decimal? RatioOld, decimal? RatioNew, string? Note);

    public record SettleRequest(DateTime SettledAt, string? LinkExistingCapitalFlowId);

    [HttpGet("portfolio/{portfolioId}")]
    [ProducesResponseType(typeof(List<CorporateActionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByPortfolio(string portfolioId, [FromQuery] string? symbol)
    {
        var result = await _mediator.Send(new GetCorporateActionsQuery(GetUserId(), portfolioId, symbol));
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateRequest request)
    {
        var id = await _mediator.Send(new CreateCorporateActionCommand(
            GetUserId(), request.PortfolioId, request.Symbol, request.Type,
            request.ExDate, request.SettlementDate, request.PercentOfPar,
            request.TaxRatePercent, request.RatioOld, request.RatioNew, request.Note));

        return Created($"/api/v1/corporate-actions/{id}", new { Id = id });
    }

    [HttpPost("{id}/settle")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Settle(string id, [FromBody] SettleRequest request)
    {
        await _mediator.Send(new SettleCorporateActionCommand(
            GetUserId(), id, request.SettledAt, request.LinkExistingCapitalFlowId));
        return NoContent();
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(string id)
    {
        await _mediator.Send(new DeleteCorporateActionCommand(GetUserId(), id));
        return NoContent();
    }
}
