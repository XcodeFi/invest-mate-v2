using InvestmentApp.Application.ApiKeys.Commands.CreateApiKey;
using InvestmentApp.Application.ApiKeys.Commands.RevokeApiKey;
using InvestmentApp.Application.ApiKeys.Queries.GetApiKeys;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

[ApiController]
[Route("api/v1/api-keys")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class ApiKeysController : ControllerBase
{
    private readonly IMediator _mediator;

    public ApiKeysController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string GetUserId() =>
        User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException();

    /// <summary>Danh sách khóa API của người dùng (chỉ metadata, không kèm token/hash).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ApiKeyDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List()
    {
        var result = await _mediator.Send(new GetApiKeysQuery { UserId = GetUserId() });
        return Ok(result);
    }

    /// <summary>Tạo khóa API mới. Token gốc chỉ trả về một lần duy nhất tại đây.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreatedApiKeyDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateApiKeyCommand command)
    {
        command.UserId = GetUserId();
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(List), new { id = result.Id }, result);
    }

    /// <summary>Thu hồi khóa API. Chỉ chủ sở hữu mới thu hồi được.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Revoke(string id)
    {
        await _mediator.Send(new RevokeApiKeyCommand { Id = id, UserId = GetUserId() });
        return NoContent();
    }
}
