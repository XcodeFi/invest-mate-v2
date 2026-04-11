using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.MarketEvents.Queries.GetMarketEvents;

public class GetMarketEventsQuery : IRequest<List<MarketEventDto>>
{
    public string Symbol { get; set; } = null!;
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

public class MarketEventDto
{
    public string Id { get; set; } = null!;
    public string Symbol { get; set; } = null!;
    public string EventType { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string? Source { get; set; }
    public DateTime EventDate { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class GetMarketEventsQueryHandler : IRequestHandler<GetMarketEventsQuery, List<MarketEventDto>>
{
    private readonly IMarketEventRepository _repository;

    public GetMarketEventsQueryHandler(IMarketEventRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<MarketEventDto>> Handle(GetMarketEventsQuery request, CancellationToken cancellationToken)
    {
        // Adjust 'to' to end-of-day so events with time components aren't excluded
        var to = request.To.HasValue
            ? request.To.Value.Date.AddDays(1).AddTicks(-1)
            : request.To;

        var events = await _repository.GetBySymbolAsync(request.Symbol, request.From, to, cancellationToken);

        return events.Select(e => new MarketEventDto
        {
            Id = e.Id,
            Symbol = e.Symbol,
            EventType = e.EventType.ToString(),
            Title = e.Title,
            Description = e.Description,
            Source = e.Source,
            EventDate = e.EventDate,
            CreatedAt = e.CreatedAt
        }).ToList();
    }
}
