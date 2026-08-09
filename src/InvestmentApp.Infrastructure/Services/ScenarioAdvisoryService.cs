using InvestmentApp.Application.Common;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Infrastructure.Services;

public class ScenarioAdvisoryService : IScenarioAdvisoryService
{
    private readonly ITradePlanRepository _tradePlanRepository;
    private readonly IMarketDataProvider _marketDataProvider;
    private readonly ICorporateActionRepository _corporateActionRepository;

    private static readonly IReadOnlyList<CorporateAction> NoActions = Array.Empty<CorporateAction>();

    public ScenarioAdvisoryService(
        ITradePlanRepository tradePlanRepository,
        IMarketDataProvider marketDataProvider,
        ICorporateActionRepository corporateActionRepository)
    {
        _tradePlanRepository = tradePlanRepository;
        _marketDataProvider = marketDataProvider;
        _corporateActionRepository = corporateActionRepository;
    }

    public async Task<List<ScenarioAdvisory>> GetAdvisoriesAsync(string userId, CancellationToken ct = default)
    {
        var advisories = new List<ScenarioAdvisory>();

        // Get all active plans for user that have Advanced exit strategy
        var activePlans = (await _tradePlanRepository.GetActiveByUserIdAsync(userId, ct))
            .Where(p =>
                p.ExitStrategyMode == ExitStrategyMode.Advanced &&
                p.ScenarioNodes != null &&
                p.ScenarioNodes.Count > 0)
            .ToList();

        if (activePlans.Count == 0) return advisories;

        // Batch fetch prices — deduplicate symbols and fetch in parallel
        var distinctSymbols = activePlans.Select(p => p.Symbol).Distinct().ToList();
        var priceTasks = distinctSymbols.Select(async s =>
        {
            var data = await _marketDataProvider.GetCurrentPriceAsync(s, ct);
            return (Symbol: s, Price: data?.Close);
        });
        var priceResults = await Task.WhenAll(priceTasks);
        var priceMap = priceResults
            .Where(r => r.Price.HasValue)
            .ToDictionary(r => r.Symbol, r => r.Price!.Value);

        // Cùng lý do như bên đánh giá kịch bản: giá thị trường đã điều chỉnh sau ngày GDKHQ,
        // giá trên kế hoạch thì chưa.
        var portfolioIds = activePlans
            .Where(p => !string.IsNullOrEmpty(p.PortfolioId))
            .Select(p => p.PortfolioId!)
            .Distinct()
            .ToList();
        var allActions = portfolioIds.Count == 0
            ? NoActions
            : (await _corporateActionRepository.GetByPortfolioIdsAsync(portfolioIds, ct)).ToList();
        var actionLookup = allActions.ToLookup(a => (a.PortfolioId, a.Symbol.ToUpper()));

        foreach (var plan in activePlans)
        {
            if (!priceMap.TryGetValue(plan.Symbol, out var currentPrice)) continue;

            var actions = string.IsNullOrEmpty(plan.PortfolioId)
                ? NoActions
                : actionLookup[(plan.PortfolioId!, plan.Symbol.ToUpper())].ToList();

            foreach (var node in plan.ScenarioNodes!)
            {
                // Only evaluate Pending nodes
                if (node.Status != ScenarioNodeStatus.Pending) continue;

                // Skip SendNotification — no advisory needed
                if (node.ActionType == ScenarioActionType.SendNotification) continue;

                if (!IsConditionMet(node, plan, currentPrice, actions)) continue;

                var threshold = TradePlanPriceAdjuster.AdjustedConditionValue(node, plan, actions);
                var conditionDesc = FormatConditionDescription(node, threshold);
                var conditionZone = FormatConditionZone(node, threshold);
                var actionDesc = FormatActionDescription(node);
                if (actionDesc == null) continue; // skip unsupported actions

                var message = $"{plan.Symbol} đang ở {currentPrice:N0} (vùng {conditionZone}) — {actionDesc}";

                advisories.Add(new ScenarioAdvisory
                {
                    TradePlanId = plan.Id,
                    Symbol = plan.Symbol,
                    CurrentPrice = currentPrice,
                    NodeId = node.NodeId,
                    NodeLabel = node.Label,
                    ConditionDescription = conditionDesc,
                    ActionDescription = actionDesc,
                    Message = message
                });
            }
        }

        return advisories;
    }

    private static bool IsConditionMet(ScenarioNode node, TradePlan plan, decimal currentPrice,
        IReadOnlyList<CorporateAction> actions)
    {
        var threshold = TradePlanPriceAdjuster.AdjustedConditionValue(node, plan, actions);

        return node.ConditionType switch
        {
            ScenarioConditionType.PriceAbove =>
                threshold.HasValue && currentPrice >= threshold.Value,

            ScenarioConditionType.PriceBelow =>
                threshold.HasValue && currentPrice <= threshold.Value,

            ScenarioConditionType.PricePercentChange =>
                EvaluatePricePercentChange(node, plan, currentPrice, actions),

            // TrailingStopHit and TimeElapsed are not advisory-relevant zones
            _ => false
        };
    }

    private static bool EvaluatePricePercentChange(ScenarioNode node, TradePlan plan, decimal currentPrice,
        IReadOnlyList<CorporateAction> actions)
    {
        if (!node.ConditionValue.HasValue) return false;

        var entryPrice = TradePlanPriceAdjuster.AdjustedEntryPrice(plan, actions);
        if (entryPrice <= 0) return false;

        var percentChange = (currentPrice - entryPrice) / entryPrice * 100m;
        return node.ConditionValue.Value >= 0
            ? percentChange >= node.ConditionValue.Value
            : percentChange <= node.ConditionValue.Value;
    }

    /// <summary>
    /// Full description shown in ConditionDescription field: "Giá ≥ 80,000"
    /// </summary>
    private static string FormatConditionDescription(ScenarioNode node, decimal? threshold)
    {
        return node.ConditionType switch
        {
            ScenarioConditionType.PriceAbove =>
                $"Giá ≥ {threshold:N0}",
            ScenarioConditionType.PriceBelow =>
                $"Giá ≤ {threshold:N0}",
            ScenarioConditionType.PricePercentChange =>
                node.ConditionValue >= 0
                    ? $"Tăng ≥ {node.ConditionValue}%"
                    : $"Giảm ≤ {node.ConditionValue}%",
            _ => node.ConditionType.ToString()
        };
    }

    /// <summary>
    /// Short zone description used inside the message: "≥ 80,000"
    /// </summary>
    private static string FormatConditionZone(ScenarioNode node, decimal? threshold)
    {
        return node.ConditionType switch
        {
            ScenarioConditionType.PriceAbove =>
                $"≥ {threshold:N0}",
            ScenarioConditionType.PriceBelow =>
                $"≤ {threshold:N0}",
            ScenarioConditionType.PricePercentChange =>
                node.ConditionValue >= 0
                    ? $"tăng ≥ {node.ConditionValue}%"
                    : $"giảm ≤ {node.ConditionValue}%",
            _ => node.ConditionType.ToString()
        };
    }

    private static string? FormatActionDescription(ScenarioNode node)
    {
        return node.ActionType switch
        {
            ScenarioActionType.SellPercent =>
                $"xem xét bán {node.ActionValue}%",
            ScenarioActionType.SellAll =>
                "xem xét bán toàn bộ",
            ScenarioActionType.AddPosition =>
                $"xem xét mua thêm {node.ActionValue}%",
            ScenarioActionType.MoveStopLoss =>
                "xem xét dời cắt lỗ",
            ScenarioActionType.MoveStopToBreakeven =>
                "xem xét dời cắt lỗ về hòa vốn",
            ScenarioActionType.ActivateTrailingStop =>
                "xem xét kích hoạt trailing stop",
            ScenarioActionType.SendNotification =>
                null, // no advisory
            _ => null
        };
    }
}
