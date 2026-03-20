using InvestmentApp.Application.AiSettings.Commands.SaveAiSettings;
using InvestmentApp.Application.AiSettings.Dtos;
using InvestmentApp.Application.AiSettings.Queries.GetAiSettings;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

[ApiController]
[Route("api/v1/ai-settings")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class AiSettingsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IAiSettingsRepository _repository;
    private readonly IAiKeyEncryptionService _encryption;
    private readonly IAiChatServiceFactory _chatServiceFactory;

    public AiSettingsController(
        IMediator mediator,
        IAiSettingsRepository repository,
        IAiKeyEncryptionService encryption,
        IAiChatServiceFactory chatServiceFactory)
    {
        _mediator = mediator;
        _repository = repository;
        _encryption = encryption;
        _chatServiceFactory = chatServiceFactory;
    }

    private string GetUserId() =>
        User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException();

    /// <summary>
    /// Get AI settings (masked API keys + usage stats)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(AiSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettings()
    {
        var query = new GetAiSettingsQuery { UserId = GetUserId() };
        var result = await _mediator.Send(query);
        return Ok(result ?? new AiSettingsDto());
    }

    /// <summary>
    /// Save/update provider, API keys, and model preference
    /// </summary>
    [HttpPut]
    [ProducesResponseType(typeof(AiSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> SaveSettings([FromBody] SaveAiSettingsCommand command)
    {
        command.UserId = GetUserId();
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Delete AI settings (soft delete)
    /// </summary>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteSettings()
    {
        var userId = GetUserId();
        var settings = await _repository.GetByUserIdAsync(userId);
        if (settings != null)
        {
            settings.SoftDelete();
            await _repository.UpdateAsync(settings);
        }
        return NoContent();
    }

    /// <summary>
    /// Test API key validity by sending a simple message to the active provider
    /// </summary>
    [HttpPost("test")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TestConnection()
    {
        var userId = GetUserId();
        var settings = await _repository.GetByUserIdAsync(userId);
        if (settings == null)
            return BadRequest(new { message = "Chưa cấu hình AI." });

        var provider = settings.Provider ?? "claude";
        var encryptedKey = settings.GetActiveEncryptedApiKey();
        if (string.IsNullOrEmpty(encryptedKey))
            return BadRequest(new { message = $"Chưa cấu hình API key cho {(provider == "gemini" ? "Google Gemini" : "Anthropic Claude")}." });

        try
        {
            var apiKey = _encryption.Decrypt(encryptedKey);
            var chatService = _chatServiceFactory.GetService(provider);
            var messages = new List<AiChatMessage>
            {
                new() { Role = "user", Content = "Xin chào! Trả lời ngắn gọn 1 câu." }
            };

            string? responseText = null;
            await foreach (var chunk in chatService.StreamChatAsync(
                apiKey, settings.Model, "Bạn là trợ lý AI.", messages))
            {
                if (chunk.Type == "text" && chunk.Text != null)
                    responseText = (responseText ?? "") + chunk.Text;
                if (chunk.Type == "error")
                    return BadRequest(new { message = chunk.ErrorMessage ?? "Lỗi kết nối API." });
            }

            return Ok(new { success = true, message = "Kết nối thành công!", response = responseText });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = $"Lỗi: {ex.Message}" });
        }
    }
}
