using InvestmentApp.Application.CompanyDossiers.Commands.ConfirmCompanyDossier;
using InvestmentApp.Application.CompanyDossiers.Commands.UpsertCompanyDossier;
using InvestmentApp.Application.CompanyDossiers.DTOs;
using InvestmentApp.Application.CompanyDossiers.Queries.GetCompanyDossier;
using InvestmentApp.Application.CompanyDossiers.Queries.GetDossierGateStatus;
using InvestmentApp.Application.CompanyDossiers.Queries.ListCompanyDossiers;
using InvestmentApp.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

[ApiController]
[Route("api/v1/company-dossiers")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class CompanyDossiersController : ControllerBase
{
    private readonly IMediator _mediator;

    public CompanyDossiersController(IMediator mediator) => _mediator = mediator;

    private string GetUserId() =>
        User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException();

    [HttpGet]
    [ProducesResponseType(typeof(List<CompanyDossierDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await _mediator.Send(new ListCompanyDossiersQuery { UserId = GetUserId() }, ct));

    [HttpGet("{symbol}")]
    [ProducesResponseType(typeof(CompanyDossierDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string symbol, CancellationToken ct)
    {
        var dto = await _mediator.Send(
            new GetCompanyDossierQuery { UserId = GetUserId(), Symbol = symbol }, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpGet("{symbol}/gate-status")]
    [ProducesResponseType(typeof(DossierGateStatusDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GateStatus(string symbol,
        [FromQuery] int? quantity, [FromQuery] decimal? entryPrice,
        [FromQuery] decimal? accountBalance, CancellationToken ct)
        => Ok(await _mediator.Send(new GetDossierGateStatusQuery
        {
            UserId = GetUserId(), Symbol = symbol,
            Quantity = quantity, EntryPrice = entryPrice, AccountBalance = accountBalance
        }, ct));

    [HttpPut("{symbol}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Upsert(string symbol,
        [FromBody] UpsertCompanyDossierRequest? request, CancellationToken ct)
    {
        // ConfigureApiBehaviorOptions.SuppressModelStateInvalidFilter=true nghĩa là body lỗi
        // deserialize sẽ vào đây là null thay vì tự 400 — chặn trước khi dereference → NRE → 500.
        if (request is null)
            return BadRequest(new { error = "Body request không hợp lệ — kiểm tra BusinessModel/Moats/RiskFactors." });

        var id = await _mediator.Send(new UpsertCompanyDossierCommand
        {
            UserId = GetUserId(),
            Symbol = symbol,
            BusinessModel = request.BusinessModel,
            Moats = request.Moats,
            RiskFactors = request.RiskFactors,
            Notes = request.Notes,
            ByAgent = false   // cửa JWT là người dùng, không bao giờ là agent
        }, ct);

        return Ok(new { id });
    }

    [HttpPost("{symbol}/confirm")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Confirm(string symbol, CancellationToken ct)
    {
        await _mediator.Send(
            new ConfirmCompanyDossierCommand { UserId = GetUserId(), Symbol = symbol }, ct);
        return Ok();
    }
}

public class UpsertCompanyDossierRequest
{
    public string BusinessModel { get; set; } = string.Empty;
    public List<MoatItem> Moats { get; set; } = new();
    public List<RiskFactor> RiskFactors { get; set; } = new();
    public string? Notes { get; set; }
}
