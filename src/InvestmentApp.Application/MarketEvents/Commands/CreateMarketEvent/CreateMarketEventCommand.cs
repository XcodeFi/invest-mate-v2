using System.Text.Json.Serialization;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.MarketEvents.Commands.CreateMarketEvent;

public class CreateMarketEventCommand : IRequest<string>
{
    [JsonIgnore]
    public string? UserId { get; set; }
    public string Symbol { get; set; } = null!;
    public string EventType { get; set; } = null!;
    public string Title { get; set; } = null!;
    public DateTime EventDate { get; set; }
    public string? Description { get; set; }
    public string? Source { get; set; }
}

public class CreateMarketEventCommandHandler : IRequestHandler<CreateMarketEventCommand, string>
{
    private readonly IMarketEventRepository _repository;
    private readonly IAuditService _auditService;

    public CreateMarketEventCommandHandler(IMarketEventRepository repository, IAuditService auditService)
    {
        _repository = repository;
        _auditService = auditService;
    }

    public async Task<string> Handle(CreateMarketEventCommand request, CancellationToken cancellationToken)
    {
        var eventType = Enum.Parse<MarketEventType>(request.EventType, ignoreCase: true);

        var marketEvent = new MarketEvent(
            symbol: request.Symbol,
            eventType: eventType,
            title: request.Title,
            eventDate: request.EventDate,
            description: request.Description,
            source: request.Source);

        await _repository.AddAsync(marketEvent, cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            UserId = request.UserId,
            Action = "CreatedMarketEvent",
            EntityId = marketEvent.Id,
            EntityType = "MarketEvent",
            Description = $"{request.EventType} for {request.Symbol}: {request.Title}"
        }, cancellationToken);

        return marketEvent.Id;
    }
}
