using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;

namespace InvestmentApp.Infrastructure.Services;

/// <summary>
/// Analyzes technical indicators for a given symbol and suggests scenario nodes
/// with reasoning based on actual market data.
/// </summary>
public class ScenarioConsultantService : IScenarioConsultantService
{
    private readonly ITechnicalIndicatorService _technicalSvc;

    // Tolerance for confluence zone matching (within ±2% = same zone)
    private const decimal ConfluenceTolerance = 0.02m;

    public ScenarioConsultantService(ITechnicalIndicatorService technicalSvc)
    {
        _technicalSvc = technicalSvc;
    }

    public async Task<ScenarioSuggestion> SuggestAsync(
        string symbol, decimal entryPrice, TimeHorizon timeHorizon, CancellationToken ct = default)
    {
        var months = timeHorizon switch
        {
            TimeHorizon.Short  => 6,
            TimeHorizon.Medium => 12,
            TimeHorizon.Long   => 24,
            _ => 12
        };

        var technical = await _technicalSvc.AnalyzeAsync(symbol, months, ct);

        var suggestion = new ScenarioSuggestion
        {
            Symbol = symbol,
            EntryPrice = entryPrice,
            TimeHorizon = timeHorizon,
            TechnicalBasis = new TechnicalBasis()
        };

        // Fallback: no technical data
        if (technical == null)
        {
            return suggestion;
        }

        // Build TechnicalBasis snapshot
        suggestion.TechnicalBasis = new TechnicalBasis
        {
            Ema20  = technical.Ema20,
            Ema50  = technical.Ema50,
            Ema200 = technical.Ema200,
            Rsi14  = technical.Rsi14,
            BollingerUpper = technical.BollingerUpper,
            BollingerLower = technical.BollingerLower,
            SupportLevels    = technical.SupportLevels.ToList(),
            ResistanceLevels = technical.ResistanceLevels.ToList(),
            Fibonacci = technical.Fibonacci,
            Atr14 = technical.Atr14
        };

        var nodes = new List<SuggestedNode>();
        int order = 0;

        // ── 1. TakeProfit nodes ────────────────────────────────────────────
        var tpNodes = BuildTakeProfitNodes(technical, entryPrice, timeHorizon, ref order);
        nodes.AddRange(tpNodes);

        // ── 2. StopLoss node ──────────────────────────────────────────────
        var slNode = BuildStopLossNode(technical, entryPrice, ref order);
        if (slNode != null) nodes.Add(slNode);

        // ── 3. AddPosition node (RSI < 40 or near strong support) ─────────
        if (technical.Rsi14 < 40m || IsNearStrongSupport(technical, entryPrice))
        {
            var addNode = BuildAddPositionNode(technical, entryPrice, ref order);
            if (addNode != null) nodes.Add(addNode);
        }

        // ── 4. Sideway / TimeElapsed node ─────────────────────────────────
        var sidewayNode = BuildSidewayNode(technical, entryPrice, timeHorizon, ref order);
        if (sidewayNode != null) nodes.Add(sidewayNode);

        suggestion.Nodes = nodes;
        return suggestion;
    }

    // ── TakeProfit builder ─────────────────────────────────────────────────

    private List<SuggestedNode> BuildTakeProfitNodes(
        TechnicalAnalysisResult tech, decimal entryPrice, TimeHorizon horizon, ref int order)
    {
        var nodes = new List<SuggestedNode>();

        // Collect all potential TP price levels above entry
        var candidates = CollectResistanceCandidates(tech, entryPrice, horizon);

        if (candidates.Count == 0) return nodes;

        // Sort by confluence score descending, then by price ascending
        candidates.Sort((a, b) =>
        {
            int cmp = b.ConfluenceCount.CompareTo(a.ConfluenceCount);
            return cmp != 0 ? cmp : a.Price.CompareTo(b.Price);
        });

        // First TP: 30% at highest-confluence zone
        var first = candidates[0];
        nodes.Add(new SuggestedNode
        {
            NodeId = Guid.NewGuid().ToString(),
            ParentId = null,
            Order = order++,
            Label = $"Chốt lời 30% tại {FormatPrice(first.Price)}",
            Category = "TakeProfit",
            ConditionType = "PriceAbove",
            ConditionValue = first.Price,
            ActionType = "SellPercent",
            ActionValue = 30m,
            Reasoning = BuildTpReasoning(first)
        });

        // Second TP: if there's a second level
        if (candidates.Count > 1)
        {
            var second = candidates[1];
            nodes.Add(new SuggestedNode
            {
                NodeId = Guid.NewGuid().ToString(),
                ParentId = null,
                Order = order++,
                Label = $"Chốt thêm 30% tại {FormatPrice(second.Price)} + trailing",
                Category = "TakeProfit",
                ConditionType = "PriceAbove",
                ConditionValue = second.Price,
                ActionType = "SellPercent",
                ActionValue = 30m,
                Reasoning = BuildTpReasoning(second)
            });
        }

        return nodes;
    }

    private List<PriceCandidate> CollectResistanceCandidates(
        TechnicalAnalysisResult tech, decimal entryPrice, TimeHorizon horizon)
    {
        var candidates = new List<PriceCandidate>();

        // Resistance levels from swing analysis
        foreach (var r in tech.ResistanceLevels.Where(r => r > entryPrice))
        {
            var c = GetOrCreate(candidates, r);
            c.Sources.Add($"kháng cự tại {FormatPrice(r)}");
        }

        // Bollinger upper
        if (tech.BollingerUpper.HasValue && tech.BollingerUpper > entryPrice)
        {
            var c = GetOrCreate(candidates, tech.BollingerUpper.Value);
            c.Sources.Add($"Bollinger Upper {FormatPrice(tech.BollingerUpper.Value)}");
        }

        // Fibonacci extension levels (for TP targets)
        if (tech.Fibonacci != null)
        {
            var fibExtensions = new[]
            {
                (tech.Fibonacci.Extension1272, "Fib 127.2%"),
                (tech.Fibonacci.Extension1618, "Fib 161.8%")
            };
            foreach (var (price, label) in fibExtensions)
            {
                if (price > entryPrice)
                {
                    var c = GetOrCreate(candidates, price);
                    c.Sources.Add($"{label} ({FormatPrice(price)})");
                }
            }

            // Medium/Long: also use Fib retracements as TP if they're above entry
            if (horizon != TimeHorizon.Short)
            {
                var fibRetrace = new[]
                {
                    (tech.Fibonacci.Retracement236, "Fib 23.6%"),
                    (tech.Fibonacci.Retracement382, "Fib 38.2%")
                };
                foreach (var (price, label) in fibRetrace)
                {
                    if (price > entryPrice)
                    {
                        var c = GetOrCreate(candidates, price);
                        c.Sources.Add($"{label} ({FormatPrice(price)})");
                    }
                }
            }
        }

        // EMA200 as TP if it's above entry (price recovering to long-term MA)
        if ((horizon == TimeHorizon.Medium || horizon == TimeHorizon.Long)
            && tech.Ema200.HasValue && tech.Ema200 > entryPrice)
        {
            var c = GetOrCreate(candidates, tech.Ema200.Value);
            c.Sources.Add($"EMA200 {FormatPrice(tech.Ema200.Value)}");
        }

        return candidates;
    }

    /// <summary>
    /// Gets or creates a PriceCandidate within ConfluenceTolerance of the given price.
    /// Merges nearby levels into a single zone.
    /// </summary>
    private static PriceCandidate GetOrCreate(List<PriceCandidate> list, decimal price)
    {
        var existing = list.FirstOrDefault(c =>
            Math.Abs(c.Price - price) / Math.Max(c.Price, 1m) <= ConfluenceTolerance);
        if (existing != null) return existing;

        var newCandidate = new PriceCandidate { Price = price };
        list.Add(newCandidate);
        return newCandidate;
    }

    private static string BuildTpReasoning(PriceCandidate candidate)
    {
        var count = candidate.ConfluenceCount;
        var sources = string.Join(" + ", candidate.Sources);

        if (count >= 2)
            return $"Vùng hợp lưu {count} chỉ báo: {sources}";

        return $"Kháng cự tại {FormatPrice(candidate.Price)} ({sources})";
    }

    // ── StopLoss builder ───────────────────────────────────────────────────

    private SuggestedNode? BuildStopLossNode(
        TechnicalAnalysisResult tech, decimal entryPrice, ref int order)
    {
        // Find nearest support below entry
        var supportsBelow = tech.SupportLevels
            .Where(s => s < entryPrice)
            .OrderByDescending(s => s)
            .ToList();

        if (supportsBelow.Count == 0)
        {
            // Fallback: ATR-based stop loss
            if (tech.Atr14.HasValue)
            {
                var atrStop = entryPrice - 2 * tech.Atr14.Value;
                return new SuggestedNode
                {
                    NodeId = Guid.NewGuid().ToString(),
                    Order = order++,
                    Label = $"Cắt lỗ tại {FormatPrice(atrStop)} (ATR)",
                    Category = "StopLoss",
                    ConditionType = "PriceBelow",
                    ConditionValue = atrStop,
                    ActionType = "SellAll",
                    Reasoning = $"Dưới 2×ATR14 ({FormatPrice(tech.Atr14.Value)}) — không có hỗ trợ rõ ràng"
                };
            }
            return null;
        }

        var nearestSupport = supportsBelow[0];
        var reasonParts = new List<string> { $"hỗ trợ tại {FormatPrice(nearestSupport)}" };

        // Check Fibonacci 61.8% confluence
        if (tech.Fibonacci != null)
        {
            var fib618 = tech.Fibonacci.Retracement618;
            if (IsNear(nearestSupport, fib618))
                reasonParts.Add($"Fib 61.8% {FormatPrice(fib618)}");
        }

        // Check EMA200 confluence
        if (tech.Ema200.HasValue && IsNear(nearestSupport, tech.Ema200.Value))
            reasonParts.Add($"EMA200 {FormatPrice(tech.Ema200.Value)}");

        var confluenceCount = reasonParts.Count;
        var reasonSuffix = confluenceCount >= 2
            ? " — phá vỡ cấu trúc quan trọng"
            : " — phá vỡ cấu trúc";

        return new SuggestedNode
        {
            NodeId = Guid.NewGuid().ToString(),
            Order = order++,
            Label = $"Cắt lỗ toàn bộ tại {FormatPrice(nearestSupport)}",
            Category = "StopLoss",
            ConditionType = "PriceBelow",
            ConditionValue = nearestSupport,
            ActionType = "SellAll",
            Reasoning = $"Dưới {string.Join(" + ", reasonParts)}{reasonSuffix}"
        };
    }

    // ── AddPosition builder ────────────────────────────────────────────────

    private SuggestedNode? BuildAddPositionNode(
        TechnicalAnalysisResult tech, decimal entryPrice, ref int order)
    {
        // Find confluence zone below entry (Fib retracement + support + EMA)
        decimal? addZone = null;
        var sources = new List<string>();

        // Try Fibonacci 38.2% retracement as add-position zone
        if (tech.Fibonacci != null)
        {
            var fib382 = tech.Fibonacci.Retracement382;
            if (fib382 < entryPrice)
            {
                addZone = fib382;
                sources.Add($"Fib 38.2% {FormatPrice(fib382)}");
            }
        }

        // Check for support near that zone
        if (addZone.HasValue)
        {
            var nearSupport = tech.SupportLevels.FirstOrDefault(s => IsNear(s, addZone.Value));
            if (nearSupport != 0)
                sources.Add($"hỗ trợ {FormatPrice(nearSupport)}");

            // EMA convergence
            if (tech.Ema50.HasValue && IsNear(tech.Ema50.Value, addZone.Value))
                sources.Add($"EMA50 {FormatPrice(tech.Ema50.Value)}");
            else if (tech.Ema20.HasValue && IsNear(tech.Ema20.Value, addZone.Value))
                sources.Add($"EMA20 {FormatPrice(tech.Ema20.Value)}");
        }
        else
        {
            // Fallback: use nearest support below entry
            var support = tech.SupportLevels.Where(s => s < entryPrice)
                              .OrderByDescending(s => s).FirstOrDefault();
            if (support == 0) return null;
            addZone = support;
            sources.Add($"hỗ trợ {FormatPrice(support)}");
        }

        var rsiNote = tech.Rsi14.HasValue && tech.Rsi14 < 40m
            ? $", RSI {tech.Rsi14:F0} (quá bán)"
            : string.Empty;

        return new SuggestedNode
        {
            NodeId = Guid.NewGuid().ToString(),
            Order = order++,
            Label = $"Mua thêm 20% tại {FormatPrice(addZone.Value)}",
            Category = "AddPosition",
            ConditionType = "PriceBelow",
            ConditionValue = addZone.Value,
            ActionType = "AddPosition",
            ActionValue = 20m,
            Reasoning = $"Vùng hợp lưu: {string.Join(" + ", sources)}{rsiNote} — cơ hội tích lũy thêm"
        };
    }

    // ── Sideway / TimeElapsed builder ──────────────────────────────────────

    private SuggestedNode? BuildSidewayNode(
        TechnicalAnalysisResult tech, decimal entryPrice, TimeHorizon horizon, ref int order)
    {
        // Determine range from nearest support and resistance
        var nearestResistance = tech.ResistanceLevels
            .Where(r => r > entryPrice).OrderBy(r => r).FirstOrDefault();
        var nearestSupport = tech.SupportLevels
            .Where(s => s < entryPrice).OrderByDescending(s => s).FirstOrDefault();

        var (daysLabel, daysValue) = horizon switch
        {
            TimeHorizon.Short  => ("2 tuần", 14m),
            TimeHorizon.Medium => ("3 tháng", 90m),
            TimeHorizon.Long   => ("6 tháng", 180m),
            _ => ("3 tháng", 90m)
        };

        var rangeDesc = nearestResistance > 0 && nearestSupport > 0
            ? $"vùng {FormatPrice(nearestSupport)}-{FormatPrice(nearestResistance)}"
            : "vùng hiện tại";

        return new SuggestedNode
        {
            NodeId = Guid.NewGuid().ToString(),
            Order = order++,
            Label = $"Đánh giá lại sau {daysLabel} nếu giá đi ngang",
            Category = "Sideway",
            ConditionType = "TimeElapsed",
            ConditionValue = daysValue,
            ActionType = "SendNotification",
            Reasoning = $"Sau {daysLabel}, giá vẫn trong {rangeDesc} — cần đánh giá lại thời gian và chiến lược"
        };
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static bool IsNear(decimal a, decimal b)
        => Math.Abs(a - b) / Math.Max(Math.Abs(b), 1m) <= ConfluenceTolerance;

    private static bool IsNearStrongSupport(TechnicalAnalysisResult tech, decimal entryPrice)
    {
        return tech.SupportLevels.Any(s =>
            s <= entryPrice && Math.Abs(entryPrice - s) / Math.Max(entryPrice, 1m) <= 0.03m);
    }

    private static string FormatPrice(decimal price)
        => price.ToString("N0");

    // ── Inner types ────────────────────────────────────────────────────────

    private class PriceCandidate
    {
        public decimal Price { get; set; }
        public List<string> Sources { get; set; } = new();
        public int ConfluenceCount => Sources.Count;
    }
}
