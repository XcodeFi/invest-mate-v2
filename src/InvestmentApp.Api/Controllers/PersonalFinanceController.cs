using InvestmentApp.Application.PersonalFinance.Commands.RemoveFinancialAccount;
using InvestmentApp.Application.PersonalFinance.Commands.UpsertFinancialAccount;
using InvestmentApp.Application.PersonalFinance.Commands.UpsertFinancialProfile;
using InvestmentApp.Application.PersonalFinance.Dtos;
using InvestmentApp.Application.PersonalFinance.Queries.GetFinancialProfile;
using InvestmentApp.Application.PersonalFinance.Queries.GetGoldPrices;
using InvestmentApp.Application.PersonalFinance.Queries.GetNetWorthSummary;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

[ApiController]
[Route("api/v1/personal-finance")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class PersonalFinanceController : ControllerBase
{
    private readonly IMediator _mediator;

    public PersonalFinanceController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string GetUserId() =>
        User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException();

    /// <summary>Lấy profile hiện tại của user. Trả 404 nếu chưa tạo.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(FinancialProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile()
    {
        var result = await _mediator.Send(new GetFinancialProfileQuery { UserId = GetUserId() });
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Net worth summary với auto-sync securitiesValue từ portfolios + health score 0-100 + rule checks.
    /// Trả empty summary nếu user chưa có profile.
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(NetWorthSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary()
    {
        var result = await _mediator.Send(new GetNetWorthSummaryQuery { UserId = GetUserId() });
        return Ok(result);
    }

    /// <summary>
    /// Bảng giá vàng hiện tại từ 24hmoney (Miếng + Nhẫn). Public reference data — dùng cho FE render
    /// dropdown + live price trong form Gold account. Cache 5 phút.
    /// </summary>
    [HttpGet("gold-prices")]
    [ProducesResponseType(typeof(IReadOnlyList<GoldPriceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGoldPrices()
    {
        var result = await _mediator.Send(new GetGoldPricesQuery());
        return Ok(result);
    }

    /// <summary>
    /// Upsert profile: get-or-create với soft-delete restore. MonthlyExpense bắt buộc khi tạo mới;
    /// optional khi update. Rule fields partial (null = không đổi).
    /// </summary>
    [HttpPut]
    [ProducesResponseType(typeof(FinancialProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpsertProfile([FromBody] UpsertFinancialProfileCommand command)
    {
        command.UserId = GetUserId();
        try
        {
            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Upsert account: thêm mới nếu AccountId null, update nếu set. Type=Gold + 3 Gold fields → auto-calc Balance
    /// qua IGoldPriceProvider; else Balance bắt buộc (trừ Securities).
    /// </summary>
    [HttpPut("accounts")]
    [ProducesResponseType(typeof(FinancialAccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpsertAccount([FromBody] UpsertFinancialAccountCommand command)
    {
        command.UserId = GetUserId();
        try
        {
            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Xóa account. Throw 400 nếu là Securities cuối cùng hoặc account không tồn tại.</summary>
    [HttpDelete("accounts/{accountId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RemoveAccount(string accountId)
    {
        try
        {
            await _mediator.Send(new RemoveFinancialAccountCommand { UserId = GetUserId(), AccountId = accountId });
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
