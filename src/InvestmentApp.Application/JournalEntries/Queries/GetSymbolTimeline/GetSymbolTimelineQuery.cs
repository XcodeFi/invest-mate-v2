using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.JournalEntries.Queries.GetSymbolTimeline;

public class GetSymbolTimelineQuery : IRequest<SymbolTimelineDto>
{
    public string UserId { get; set; } = null!;
    public string Symbol { get; set; } = null!;
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

public class SymbolTimelineDto
{
    public string Symbol { get; set; } = null!;
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public List<TimelineItemDto> Items { get; set; } = new();
    public List<HoldingPeriodDto> HoldingPeriods { get; set; } = new();
    public EmotionSummaryDto? EmotionSummary { get; set; }
}

public class TimelineItemDto
{
    public string Type { get; set; } = null!; // "journal", "trade", "alert", "event"
    public DateTime Timestamp { get; set; }
    public object Data { get; set; } = null!;
}

public class HoldingPeriodDto
{
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal StartQuantity { get; set; }
    public decimal CurrentQuantity { get; set; }
    public List<HoldingChangeDto> Changes { get; set; } = new();
}

public class HoldingChangeDto
{
    public DateTime Date { get; set; }
    public string Type { get; set; } = null!; // "BUY" or "SELL"
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal Remaining { get; set; }
}

public class EmotionSummaryDto
{
    public Dictionary<string, int> Distribution { get; set; } = new();
    public double? AverageConfidence { get; set; }
    public int TotalEntries { get; set; }
}

public class GetSymbolTimelineQueryHandler : IRequestHandler<GetSymbolTimelineQuery, SymbolTimelineDto>
{
    private readonly IJournalEntryRepository _journalEntryRepository;
    private readonly ITradeRepository _tradeRepository;
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly IMarketEventRepository _marketEventRepository;
    private readonly IAlertHistoryRepository _alertHistoryRepository;

    public GetSymbolTimelineQueryHandler(
        IJournalEntryRepository journalEntryRepository,
        ITradeRepository tradeRepository,
        IPortfolioRepository portfolioRepository,
        IMarketEventRepository marketEventRepository,
        IAlertHistoryRepository alertHistoryRepository)
    {
        _journalEntryRepository = journalEntryRepository;
        _tradeRepository = tradeRepository;
        _portfolioRepository = portfolioRepository;
        _marketEventRepository = marketEventRepository;
        _alertHistoryRepository = alertHistoryRepository;
    }

    public async Task<SymbolTimelineDto> Handle(GetSymbolTimelineQuery request, CancellationToken cancellationToken)
    {
        var items = new List<TimelineItemDto>();

        // 1. Journal entries
        var journalEntries = await _journalEntryRepository.GetByUserIdAndSymbolAsync(
            request.UserId, request.Symbol, request.From, request.To, cancellationToken);

        foreach (var entry in journalEntries)
        {
            items.Add(new TimelineItemDto
            {
                Type = "journal",
                Timestamp = entry.Timestamp,
                Data = new
                {
                    entry.Id,
                    entry.EntryType,
                    entry.Title,
                    entry.Content,
                    entry.EmotionalState,
                    entry.ConfidenceLevel,
                    entry.PriceAtTime,
                    entry.MarketContext,
                    entry.Tags,
                    entry.Rating
                }
            });
        }

        // 2. Trades — single query across all user's portfolios (H4 fix: avoid N+1)
        var portfolios = await _portfolioRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        var portfolioIds = portfolios.Select(p => p.Id);
        var allTrades = (await _tradeRepository.GetByUserPortfoliosAndSymbolAsync(
            portfolioIds, request.Symbol, cancellationToken)).ToList();

        // Filter by date range
        if (request.From.HasValue)
            allTrades = allTrades.Where(t => t.TradeDate >= request.From.Value).ToList();
        if (request.To.HasValue)
            allTrades = allTrades.Where(t => t.TradeDate <= request.To.Value).ToList();

        foreach (var trade in allTrades)
        {
            items.Add(new TimelineItemDto
            {
                Type = "trade",
                Timestamp = trade.TradeDate,
                Data = new
                {
                    trade.Id,
                    TradeType = trade.TradeType.ToString(),
                    trade.Quantity,
                    trade.Price,
                    trade.Fee,
                    trade.PortfolioId
                }
            });
        }

        // 3. Market events
        var events = await _marketEventRepository.GetBySymbolAsync(
            request.Symbol, request.From, request.To, cancellationToken);

        foreach (var evt in events)
        {
            items.Add(new TimelineItemDto
            {
                Type = "event",
                Timestamp = evt.EventDate,
                Data = new
                {
                    evt.Id,
                    EventType = evt.EventType.ToString(),
                    evt.Title,
                    evt.Description,
                    evt.Source
                }
            });
        }

        // 4. Alert history — filtered query by symbol+date at DB level (H5 fix)
        var symbolAlerts = await _alertHistoryRepository.GetByUserIdAndSymbolAsync(
            request.UserId, request.Symbol, request.From, request.To, cancellationToken);

        foreach (var alert in symbolAlerts)
        {
            items.Add(new TimelineItemDto
            {
                Type = "alert",
                Timestamp = alert.TriggeredAt,
                Data = new
                {
                    alert.Id,
                    alert.AlertType,
                    alert.Message
                }
            });
        }

        // Sort all items by timestamp
        items = items.OrderBy(i => i.Timestamp).ToList();

        // Calculate holding periods from trades
        var holdingPeriods = CalculateHoldingPeriods(allTrades.OrderBy(t => t.TradeDate).ToList());

        // Calculate emotion summary
        var emotionSummary = CalculateEmotionSummary(journalEntries);

        return new SymbolTimelineDto
        {
            Symbol = request.Symbol.ToUpper().Trim(),
            From = request.From,
            To = request.To,
            Items = items,
            HoldingPeriods = holdingPeriods,
            EmotionSummary = emotionSummary
        };
    }

    private static List<HoldingPeriodDto> CalculateHoldingPeriods(List<Trade> trades)
    {
        var periods = new List<HoldingPeriodDto>();
        HoldingPeriodDto? current = null;
        decimal runningQty = 0;

        foreach (var trade in trades)
        {
            var qty = trade.Quantity;

            if (trade.TradeType == TradeType.BUY)
            {
                runningQty += qty;
                if (current == null)
                {
                    current = new HoldingPeriodDto
                    {
                        StartDate = trade.TradeDate,
                        StartQuantity = qty,
                        CurrentQuantity = runningQty,
                        Changes = new List<HoldingChangeDto>()
                    };
                }
                else
                {
                    current.CurrentQuantity = runningQty;
                }
                // M2 fix: always add BUY (including initial) to Changes
                current.Changes.Add(new HoldingChangeDto
                {
                    Date = trade.TradeDate,
                    Type = "BUY",
                    Quantity = qty,
                    Price = trade.Price,
                    Remaining = runningQty
                });
            }
            else // SELL
            {
                runningQty -= qty;
                if (current != null)
                {
                    current.CurrentQuantity = Math.Max(0, runningQty);
                    current.Changes.Add(new HoldingChangeDto
                    {
                        Date = trade.TradeDate,
                        Type = "SELL",
                        Quantity = qty,
                        Price = trade.Price,
                        Remaining = Math.Max(0, runningQty)
                    });

                    if (runningQty <= 0)
                    {
                        current.EndDate = trade.TradeDate;
                        periods.Add(current);
                        current = null;
                        runningQty = 0;
                    }
                }
            }
        }

        // If still holding
        if (current != null)
            periods.Add(current);

        return periods;
    }

    private static EmotionSummaryDto? CalculateEmotionSummary(IEnumerable<JournalEntry> entries)
    {
        var list = entries.ToList();
        if (list.Count == 0) return null;

        var distribution = new Dictionary<string, int>();
        var confidenceLevels = new List<int>();

        foreach (var entry in list)
        {
            if (!string.IsNullOrWhiteSpace(entry.EmotionalState))
            {
                if (distribution.ContainsKey(entry.EmotionalState))
                    distribution[entry.EmotionalState]++;
                else
                    distribution[entry.EmotionalState] = 1;
            }

            if (entry.ConfidenceLevel.HasValue)
                confidenceLevels.Add(entry.ConfidenceLevel.Value);
        }

        return new EmotionSummaryDto
        {
            Distribution = distribution,
            AverageConfidence = confidenceLevels.Count > 0 ? confidenceLevels.Average() : null,
            TotalEntries = list.Count
        };
    }
}
