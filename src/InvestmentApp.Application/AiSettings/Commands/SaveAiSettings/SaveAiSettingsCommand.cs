using System.Text.Json.Serialization;
using InvestmentApp.Application.AiSettings.Dtos;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.AiSettings.Commands.SaveAiSettings;

public class SaveAiSettingsCommand : IRequest<AiSettingsDto>
{
    [JsonIgnore]
    public string UserId { get; set; } = null!;
    public string? Provider { get; set; }
    public string? ClaudeApiKey { get; set; }
    public string? GeminiApiKey { get; set; }
    public string? Model { get; set; }
}

public class SaveAiSettingsCommandHandler : IRequestHandler<SaveAiSettingsCommand, AiSettingsDto>
{
    private readonly IAiSettingsRepository _repository;
    private readonly IAiKeyEncryptionService _encryption;

    public SaveAiSettingsCommandHandler(IAiSettingsRepository repository, IAiKeyEncryptionService encryption)
    {
        _repository = repository;
        _encryption = encryption;
    }

    public async Task<AiSettingsDto> Handle(SaveAiSettingsCommand request, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetByUserIdAsync(request.UserId, cancellationToken);

        if (existing != null)
        {
            if (!string.IsNullOrWhiteSpace(request.Provider))
                existing.UpdateProvider(request.Provider);
            if (!string.IsNullOrWhiteSpace(request.ClaudeApiKey))
                existing.UpdateClaudeApiKey(_encryption.Encrypt(request.ClaudeApiKey));
            if (!string.IsNullOrWhiteSpace(request.GeminiApiKey))
                existing.UpdateGeminiApiKey(_encryption.Encrypt(request.GeminiApiKey));
            if (!string.IsNullOrWhiteSpace(request.Model))
                existing.UpdateModel(request.Model);
            await _repository.UpdateAsync(existing, cancellationToken);
        }
        else
        {
            var provider = request.Provider ?? "claude";
            var apiKey = provider == "gemini" ? request.GeminiApiKey : request.ClaudeApiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new Exception("API key là bắt buộc khi cấu hình lần đầu.");

            var model = request.Model ?? (provider == "gemini" ? "gemini-2.0-flash" : "claude-sonnet-4-6-20250514");
            existing = Domain.Entities.AiSettings.Create(request.UserId, _encryption.Encrypt(apiKey), provider, model);

            // Also save the other provider's key if provided
            if (provider == "gemini" && !string.IsNullOrWhiteSpace(request.ClaudeApiKey))
                existing.UpdateClaudeApiKey(_encryption.Encrypt(request.ClaudeApiKey));
            else if (provider == "claude" && !string.IsNullOrWhiteSpace(request.GeminiApiKey))
                existing.UpdateGeminiApiKey(_encryption.Encrypt(request.GeminiApiKey));

            await _repository.AddAsync(existing, cancellationToken);
        }

        return MapToDto(existing);
    }

    internal static AiSettingsDto MapToDto(Domain.Entities.AiSettings settings)
    {
        return new AiSettingsDto
        {
            Provider = settings.Provider ?? "claude",
            HasClaudeApiKey = !string.IsNullOrEmpty(settings.EncryptedClaudeApiKey),
            MaskedClaudeApiKey = !string.IsNullOrEmpty(settings.EncryptedClaudeApiKey) ? "sk-ant-•••••••" : null,
            HasGeminiApiKey = !string.IsNullOrEmpty(settings.EncryptedGeminiApiKey),
            MaskedGeminiApiKey = !string.IsNullOrEmpty(settings.EncryptedGeminiApiKey) ? "AIza•••••••" : null,
            Model = settings.Model,
            TotalInputTokens = settings.TotalInputTokens,
            TotalOutputTokens = settings.TotalOutputTokens,
            EstimatedCostUsd = settings.EstimatedCostUsd
        };
    }
}
