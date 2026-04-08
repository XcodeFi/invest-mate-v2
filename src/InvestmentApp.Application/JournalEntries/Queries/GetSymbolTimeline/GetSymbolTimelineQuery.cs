using InvestmentApp.Application.Common.Interfaces;
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
    public List<BehavioralPatternDto> BehavioralPatterns { get; set; } = new();
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

    // P7.1: Emotion ↔ P&L Correlation
    public List<EmotionCorrelationDto> Correlations { get; set; } = new();

    // P7.2: Confidence Calibration
    public List<ConfidenceCalibrationDto> ConfidenceCalibration { get; set; } = new();

    // P7.6: Emotion Trend Over Time
    public List<EmotionTrendDto> Trends { get; set; } = new();
}

public class EmotionCorrelationDto
{
    public string Emotion { get; set; } = null!;
    public int TradeCount { get; set; }
    public decimal AveragePnlPercent { get; set; }
    public double WinRate { get; set; }
    public decimal TotalPnl { get; set; }
}

public class ConfidenceCalibrationDto
{
    public string Range { get; set; } = null!;
    public int EntryCount { get; set; }
    public int TradeCount { get; set; }
    public double WinRate { get; set; }
    public decimal AveragePnlPercent { get; set; }
    public bool IsCalibrated { get; set; }
}

public class EmotionTrendDto
{
    public string Period { get; set; } = null!;
    public string DominantEmotion { get; set; } = null!;
    public double AverageConfidence { get; set; }
    public int EntryCount { get; set; }
    public Dictionary<string, int> Distribution { get; set; } = new();
}

public class GetSymbolTimelineQueryHandler : IRequestHandler<GetSymbolTimelineQuery, SymbolTimelineDto>
{
    private readonly IJournalEntryRepository _journalEntryRepository;
    private readonly ITradeRepository _tradeRepository;
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly IMarketEventRepository _marketEventRepository;
    private readonly IAlertHistoryRepository _alertHistoryRepository;
    private readonly IBehavioralAnalysisService? _behavioralAnalysisService;

    public GetSymbolTimelineQueryHandler(
        IJournalEntryRepository journalEntryRepository,
        ITradeRepository tradeRepository,
        IPortfolioRepository portfolioRepository,
        IMarketEventRepository marketEventRepository,
        IAlertHistoryRepository alertHistoryRepository,
        IBehavioralAnalysisService? behavioralAnalysisService = null)
    {
        _journalEntryRepository = journalEntryRepository;
        _tradeRepository = tradeRepository;
        _portfolioRepository = portfolioRepository;
        _marketEventRepository = marketEventRepository;
        _alertHistoryRepository = alertHistoryRepository;
        _behavioralAnalysisService = behavioralAnalysisService;
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

        // Calculate emotion summary with correlations, calibration, and trends
        var emotionSummary = CalculateEmotionSummary(journalEntries);
        if (emotionSummary != null)
        {
            emotionSummary.Correlations = CalculateEmotionCorrelations(journalEntries, allTrades);
            emotionSummary.ConfidenceCalibration = CalculateConfidenceCalibration(journalEntries, allTrades);
            emotionSummary.Trends = CalculateEmotionTrends(journalEntries);
        }

        // Detect behavioral patterns (P7.3)
        var behavioralPatterns = _behavioralAnalysisService != null
            ? _behavioralAnalysisService.DetectPatterns(journalEntries, allTrades)
            : new List<BehavioralPatternDto>();

        return new SymbolTimelineDto
        {
            Symbol = request.Symbol.ToUpper().Trim(),
            From = request.From,
            To = request.To,
            Items = items,
            HoldingPeriods = holdingPeriods,
            EmotionSummary = emotionSummary,
            BehavioralPatterns = behavioralPatterns
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

    /// <summary>P7.1: Correlate emotion at journal entry time with trade P&L outcome</summary>
    private static List<EmotionCorrelationDto> CalculateEmotionCorrelations(
        IEnumerable<JournalEntry> entries, List<Trade> trades)
    {
        var tradeById = trades.ToDictionary(t => t.Id);

        // Find journal entries linked to BUY trades via TradeId
        var linkedJournals = entries
            .Where(j => !string.IsNullOrEmpty(j.TradeId)
                        && !string.IsNullOrWhiteSpace(j.EmotionalState)
                        && tradeById.ContainsKey(j.TradeId))
            .ToList();

        if (linkedJournals.Count == 0) return new List<EmotionCorrelationDto>();

        // Build BUY→SELL pairs for P&L: match sequential BUY→SELL by time
        var buyTrades = trades.Where(t => t.TradeType == TradeType.BUY).OrderBy(t => t.TradeDate).ToList();

        // Map each BUY trade to its P&L from next available SELL
        var buyPnl = new Dictionary<string, (decimal pnlPercent, bool isWin)>();
        decimal runningQty = 0;
        decimal weightedBuyPrice = 0;

        foreach (var trade in trades.OrderBy(t => t.TradeDate))
        {
            if (trade.TradeType == TradeType.BUY)
            {
                weightedBuyPrice = runningQty + trade.Quantity > 0
                    ? (weightedBuyPrice * runningQty + trade.Price * trade.Quantity) / (runningQty + trade.Quantity)
                    : trade.Price;
                runningQty += trade.Quantity;
            }
            else // SELL
            {
                if (runningQty > 0 && weightedBuyPrice > 0)
                {
                    var pnlPercent = (trade.Price - weightedBuyPrice) / weightedBuyPrice * 100;
                    var totalPnl = (trade.Price - weightedBuyPrice) * trade.Quantity;

                    // Attribute this SELL to all BUY trades that contributed
                    foreach (var buy in buyTrades.Where(b => b.TradeDate <= trade.TradeDate && !buyPnl.ContainsKey(b.Id)))
                    {
                        buyPnl[buy.Id] = (pnlPercent, pnlPercent > 0);
                    }

                    runningQty -= trade.Quantity;
                    if (runningQty <= 0)
                    {
                        runningQty = 0;
                        weightedBuyPrice = 0;
                    }
                }
            }
        }

        // Group by emotion and calculate correlation
        var grouped = linkedJournals
            .Where(j => buyPnl.ContainsKey(j.TradeId!))
            .GroupBy(j => j.EmotionalState!)
            .Select(g =>
            {
                var pnls = g.Select(j => buyPnl[j.TradeId!]).ToList();
                return new EmotionCorrelationDto
                {
                    Emotion = g.Key,
                    TradeCount = pnls.Count,
                    AveragePnlPercent = pnls.Count > 0 ? Math.Round(pnls.Average(p => p.pnlPercent), 2) : 0,
                    WinRate = pnls.Count > 0 ? Math.Round((double)pnls.Count(p => p.isWin) / pnls.Count * 100, 2) : 0,
                    TotalPnl = pnls.Sum(p => p.pnlPercent) // simplified total
                };
            })
            .OrderByDescending(c => c.TradeCount)
            .ToList();

        return grouped;
    }

    /// <summary>P7.2: Compare confidence level ranges with actual win rate</summary>
    private static List<ConfidenceCalibrationDto> CalculateConfidenceCalibration(
        IEnumerable<JournalEntry> entries, List<Trade> trades)
    {
        var tradeById = trades.ToDictionary(t => t.Id);

        // Reuse same BUY→P&L mapping logic
        var buyPnl = new Dictionary<string, (decimal pnlPercent, bool isWin)>();
        decimal runningQty = 0;
        decimal weightedBuyPrice = 0;
        var buyTrades = trades.Where(t => t.TradeType == TradeType.BUY).OrderBy(t => t.TradeDate).ToList();

        foreach (var trade in trades.OrderBy(t => t.TradeDate))
        {
            if (trade.TradeType == TradeType.BUY)
            {
                weightedBuyPrice = runningQty + trade.Quantity > 0
                    ? (weightedBuyPrice * runningQty + trade.Price * trade.Quantity) / (runningQty + trade.Quantity)
                    : trade.Price;
                runningQty += trade.Quantity;
            }
            else
            {
                if (runningQty > 0 && weightedBuyPrice > 0)
                {
                    var pnlPercent = (trade.Price - weightedBuyPrice) / weightedBuyPrice * 100;
                    foreach (var buy in buyTrades.Where(b => b.TradeDate <= trade.TradeDate && !buyPnl.ContainsKey(b.Id)))
                    {
                        buyPnl[buy.Id] = (pnlPercent, pnlPercent > 0);
                    }
                    runningQty -= trade.Quantity;
                    if (runningQty <= 0) { runningQty = 0; weightedBuyPrice = 0; }
                }
            }
        }

        // Get journals with confidence + linked to closed trades
        var linkedJournals = entries
            .Where(j => !string.IsNullOrEmpty(j.TradeId)
                        && j.ConfidenceLevel.HasValue
                        && buyPnl.ContainsKey(j.TradeId))
            .ToList();

        if (linkedJournals.Count == 0) return new List<ConfidenceCalibrationDto>();

        var ranges = new (string label, int min, int max)[]
        {
            ("Low (1-3)", 1, 3),
            ("Medium (4-6)", 4, 6),
            ("High (7-8)", 7, 8),
            ("Very High (9-10)", 9, 10)
        };

        var result = new List<ConfidenceCalibrationDto>();
        foreach (var (label, min, max) in ranges)
        {
            var inRange = linkedJournals
                .Where(j => j.ConfidenceLevel!.Value >= min && j.ConfidenceLevel.Value <= max)
                .ToList();

            if (inRange.Count == 0) continue;

            var pnls = inRange.Select(j => buyPnl[j.TradeId!]).ToList();
            var winRate = Math.Round((double)pnls.Count(p => p.isWin) / pnls.Count * 100, 2);
            var confidenceMidpoint = (min + max) / 2.0 * 10; // convert to 0-100 scale

            result.Add(new ConfidenceCalibrationDto
            {
                Range = label,
                EntryCount = inRange.Count,
                TradeCount = pnls.Count,
                WinRate = winRate,
                AveragePnlPercent = Math.Round(pnls.Average(p => p.pnlPercent), 2),
                IsCalibrated = Math.Abs(confidenceMidpoint - winRate) < 15
            });
        }

        return result;
    }

    /// <summary>P7.6: Emotion distribution trend grouped by month</summary>
    private static List<EmotionTrendDto> CalculateEmotionTrends(IEnumerable<JournalEntry> entries)
    {
        var list = entries.ToList();
        if (list.Count == 0) return new List<EmotionTrendDto>();

        return list
            .GroupBy(e => e.Timestamp.ToString("yyyy-MM"))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var distribution = new Dictionary<string, int>();
                var confidences = new List<int>();

                foreach (var entry in g)
                {
                    if (!string.IsNullOrWhiteSpace(entry.EmotionalState))
                    {
                        if (distribution.ContainsKey(entry.EmotionalState))
                            distribution[entry.EmotionalState]++;
                        else
                            distribution[entry.EmotionalState] = 1;
                    }
                    if (entry.ConfidenceLevel.HasValue)
                        confidences.Add(entry.ConfidenceLevel.Value);
                }

                var dominantEmotion = distribution.Count > 0
                    ? distribution.OrderByDescending(kv => kv.Value).First().Key
                    : "";

                return new EmotionTrendDto
                {
                    Period = g.Key,
                    EntryCount = g.Count(),
                    DominantEmotion = dominantEmotion,
                    AverageConfidence = confidences.Count > 0 ? confidences.Average() : 0,
                    Distribution = distribution
                };
            })
            .ToList();
    }
}
