using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Infrastructure.Services;

public class BehavioralAnalysisService : IBehavioralAnalysisService
{
    private static readonly HashSet<string> FomoEmotions = new(StringComparer.OrdinalIgnoreCase)
    {
        "FOMO", "Hào hứng", "Tham lam"
    };

    private static readonly HashSet<string> PanicEmotions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Sợ hãi", "Lo lắng"
    };

    public List<BehavioralPatternDto> DetectPatterns(
        IEnumerable<JournalEntry> journalEntries, IEnumerable<Trade> trades)
    {
        var patterns = new List<BehavioralPatternDto>();
        var journalList = journalEntries.OrderBy(j => j.Timestamp).ToList();
        var tradeList = trades.OrderBy(t => t.TradeDate).ToList();

        patterns.AddRange(DetectFomoEntries(journalList, tradeList));
        patterns.AddRange(DetectPanicSells(journalList, tradeList));
        patterns.AddRange(DetectRevengeTrading(journalList, tradeList));
        patterns.AddRange(DetectOvertrading(tradeList));

        return patterns.OrderBy(p => p.OccurredAt).ToList();
    }

    /// <summary>FOMO Entry: Journal (PreTrade, emotion=FOMO/Hào hứng) → BUY trade within 24h</summary>
    internal static List<BehavioralPatternDto> DetectFomoEntries(
        List<JournalEntry> journals, List<Trade> trades)
    {
        var patterns = new List<BehavioralPatternDto>();
        var buyTrades = trades.Where(t => t.TradeType == TradeType.BUY).ToList();

        foreach (var journal in journals)
        {
            if (string.IsNullOrEmpty(journal.EmotionalState)) continue;
            if (!FomoEmotions.Contains(journal.EmotionalState)) continue;
            if (journal.EntryType != JournalEntryType.PreTrade &&
                journal.EntryType != JournalEntryType.DuringTrade) continue;

            // Check if BUY trade within 24h after journal
            var matchingBuy = buyTrades.FirstOrDefault(t =>
                t.TradeDate >= journal.Timestamp &&
                t.TradeDate <= journal.Timestamp.AddHours(24));

            if (matchingBuy != null)
            {
                patterns.Add(new BehavioralPatternDto
                {
                    PatternType = "FOMO",
                    Severity = "Warning",
                    Description = $"Mua khi {journal.EmotionalState} — {journal.Title}",
                    OccurredAt = matchingBuy.TradeDate,
                    RelatedTradeId = matchingBuy.Id,
                    RelatedJournalId = journal.Id
                });
            }
        }

        return patterns;
    }

    /// <summary>Panic Sell: Journal (DuringTrade, emotion=Sợ hãi) → SELL trade within 24h</summary>
    internal static List<BehavioralPatternDto> DetectPanicSells(
        List<JournalEntry> journals, List<Trade> trades)
    {
        var patterns = new List<BehavioralPatternDto>();
        var sellTrades = trades.Where(t => t.TradeType == TradeType.SELL).ToList();

        foreach (var journal in journals)
        {
            if (string.IsNullOrEmpty(journal.EmotionalState)) continue;
            if (!PanicEmotions.Contains(journal.EmotionalState)) continue;

            var matchingSell = sellTrades.FirstOrDefault(t =>
                t.TradeDate >= journal.Timestamp &&
                t.TradeDate <= journal.Timestamp.AddHours(24));

            if (matchingSell != null)
            {
                patterns.Add(new BehavioralPatternDto
                {
                    PatternType = "PanicSell",
                    Severity = "Critical",
                    Description = $"Bán vội khi {journal.EmotionalState} — {journal.Title}",
                    OccurredAt = matchingSell.TradeDate,
                    RelatedTradeId = matchingSell.Id,
                    RelatedJournalId = journal.Id
                });
            }
        }

        return patterns;
    }

    /// <summary>Revenge Trading: SELL at loss → BUY within 4h without PreTrade journal</summary>
    internal static List<BehavioralPatternDto> DetectRevengeTrading(
        List<JournalEntry> journals, List<Trade> trades)
    {
        var patterns = new List<BehavioralPatternDto>();
        var tradeList = trades.OrderBy(t => t.TradeDate).ToList();

        decimal runningQty = 0;
        decimal weightedAvg = 0;

        for (int i = 0; i < tradeList.Count; i++)
        {
            var trade = tradeList[i];
            if (trade.TradeType == TradeType.BUY)
            {
                weightedAvg = runningQty + trade.Quantity > 0
                    ? (weightedAvg * runningQty + trade.Price * trade.Quantity) / (runningQty + trade.Quantity)
                    : trade.Price;
                runningQty += trade.Quantity;
                continue;
            }

            // SELL trade — check if at loss
            var isLoss = trade.Price < weightedAvg && runningQty > 0;
            runningQty -= trade.Quantity;
            if (runningQty <= 0) { runningQty = 0; weightedAvg = 0; }

            if (!isLoss) continue;

            // Check for BUY within 4h after loss SELL
            var nextBuy = tradeList.Skip(i + 1).FirstOrDefault(t =>
                t.TradeType == TradeType.BUY &&
                t.TradeDate >= trade.TradeDate &&
                t.TradeDate <= trade.TradeDate.AddHours(4));

            if (nextBuy == null) continue;

            // Check no PreTrade journal between sell and buy (planned trade)
            var hasPreTradeJournal = journals.Any(j =>
                j.EntryType == JournalEntryType.PreTrade &&
                j.Timestamp >= trade.TradeDate.AddHours(-1) &&
                j.Timestamp <= nextBuy.TradeDate);

            if (!hasPreTradeJournal)
            {
                patterns.Add(new BehavioralPatternDto
                {
                    PatternType = "RevengeTrading",
                    Severity = "Critical",
                    Description = "Mua lại ngay sau khi bán lỗ — không có kế hoạch trước",
                    OccurredAt = nextBuy.TradeDate,
                    RelatedTradeId = nextBuy.Id
                });
            }
        }

        return patterns;
    }

    /// <summary>Overtrading: > 3 BUY trades same day for same symbol</summary>
    internal static List<BehavioralPatternDto> DetectOvertrading(List<Trade> trades)
    {
        var patterns = new List<BehavioralPatternDto>();
        var buysByDay = trades
            .Where(t => t.TradeType == TradeType.BUY)
            .GroupBy(t => t.TradeDate.Date);

        foreach (var group in buysByDay)
        {
            if (group.Count() > 3)
            {
                patterns.Add(new BehavioralPatternDto
                {
                    PatternType = "Overtrading",
                    Severity = "Warning",
                    Description = $"Mua {group.Count()} lần trong ngày {group.Key:dd/MM/yyyy}",
                    OccurredAt = group.Key
                });
            }
        }

        return patterns;
    }
}
