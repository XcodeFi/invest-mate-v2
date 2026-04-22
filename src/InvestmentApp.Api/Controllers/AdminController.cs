using InvestmentApp.Api.Authorization;
using InvestmentApp.Application.Admin.Commands.StartImpersonation;
using InvestmentApp.Application.Admin.Commands.StopImpersonation;
using InvestmentApp.Application.Admin.Queries.GetUsersOverview;
using InvestmentApp.Application.Admin.Queries.SearchUsers;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class AdminController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string GetUserId() =>
        User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException();

    /// <summary>
    /// Search users by email (partial, case-insensitive). Returns up to 10 results,
    /// excluding the caller. Empty query returns empty list.
    /// </summary>
    [HttpGet("users")]
    [RequireAdmin]
    [ProducesResponseType(typeof(List<AdminUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SearchUsers([FromQuery] string? email)
    {
        var result = await _mediator.Send(new SearchUsersQuery
        {
            CallerUserId = GetUserId(),
            EmailQuery = email ?? string.Empty
        });
        return Ok(result);
    }

    /// <summary>
    /// Paginated list of all users with aggregate activity stats
    /// (# portfolios, # trades, last trade, last login, last impersonated).
    /// </summary>
    [HttpGet("users/overview")]
    [RequireAdmin]
    [ProducesResponseType(typeof(UsersOverviewResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUsersOverview([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetUsersOverviewQuery
        {
            CallerUserId = GetUserId(),
            Page = page,
            PageSize = pageSize
        });
        return Ok(result);
    }

    /// <summary>
    /// Start impersonating a user. Only callable by admins (role=Admin, no active amr=impersonate).
    /// Returns a JWT valid for 1h with actor=adminId and amr=impersonate.
    /// </summary>
    [HttpPost("impersonate")]
    [RequireAdmin]
    [ProducesResponseType(typeof(StartImpersonationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> StartImpersonate([FromBody] ImpersonateRequest request)
    {
        var command = new StartImpersonationCommand
        {
            AdminUserId = GetUserId(),
            TargetUserId = request.TargetUserId,
            Reason = request.Reason,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
            UserAgent = Request.Headers.UserAgent.ToString()
        };

        try
        {
            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Stop the current impersonation session.
    /// Must be called with the impersonation token (has impersonation_id + actor claims).
    /// </summary>
    [HttpPost("impersonate/stop")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> StopImpersonate()
    {
        var impersonationId = User.FindFirst("impersonation_id")?.Value;
        var actorId = User.FindFirst("actor")?.Value;

        if (string.IsNullOrEmpty(impersonationId) || string.IsNullOrEmpty(actorId))
            return BadRequest(new { error = "Not an impersonation session" });

        try
        {
            await _mediator.Send(new StopImpersonationCommand
            {
                ImpersonationId = impersonationId,
                AdminUserId = actorId
            });
            return Ok(new { message = "Impersonation stopped" });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public class ImpersonateRequest
{
    public string TargetUserId { get; set; } = null!;
    public string Reason { get; set; } = null!;
}
