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
    public string? ApiKey { get; set; }
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
            if (!string.IsNullOrWhiteSpace(request.ApiKey))
                existing.UpdateApiKey(_encryption.Encrypt(request.ApiKey));
            if (!string.IsNullOrWhiteSpace(request.Model))
                existing.UpdateModel(request.Model);
            await _repository.UpdateAsync(existing, cancellationToken);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.ApiKey))
                throw new Exception("API key là bắt buộc khi cấu hình lần đầu.");

            var model = request.Model ?? "claude-sonnet-4-6-20250514";
            existing = Domain.Entities.AiSettings.Create(request.UserId, _encryption.Encrypt(request.ApiKey), model);
            await _repository.AddAsync(existing, cancellationToken);
        }

        return MapToDto(existing);
    }

    private static AiSettingsDto MapToDto(Domain.Entities.AiSettings settings)
    {
        string? masked = null;
        if (!string.IsNullOrEmpty(settings.EncryptedApiKey))
        {
            masked = "sk-ant-•••••••";
        }

        return new AiSettingsDto
        {
            HasApiKey = !string.IsNullOrEmpty(settings.EncryptedApiKey),
            MaskedApiKey = masked,
            Model = settings.Model,
            TotalInputTokens = settings.TotalInputTokens,
            TotalOutputTokens = settings.TotalOutputTokens,
            EstimatedCostUsd = settings.EstimatedCostUsd
        };
    }
}
