using System.Text.Json.Serialization;
using InvestmentApp.Application.AiSettings.Commands.SaveAiSettings;
using InvestmentApp.Application.AiSettings.Dtos;
using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.AiSettings.Queries.GetAiSettings;

public class GetAiSettingsQuery : IRequest<AiSettingsDto?>
{
    [JsonIgnore]
    public string UserId { get; set; } = null!;
}

public class GetAiSettingsQueryHandler : IRequestHandler<GetAiSettingsQuery, AiSettingsDto?>
{
    private readonly IAiSettingsRepository _repository;

    public GetAiSettingsQueryHandler(IAiSettingsRepository repository)
    {
        _repository = repository;
    }

    public async Task<AiSettingsDto?> Handle(GetAiSettingsQuery request, CancellationToken cancellationToken)
    {
        var settings = await _repository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (settings == null) return null;

        return SaveAiSettingsCommandHandler.MapToDto(settings);
    }
}
