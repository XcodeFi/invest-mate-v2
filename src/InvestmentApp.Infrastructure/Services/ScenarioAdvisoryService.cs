using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Infrastructure.Services;

public class ScenarioAdvisoryService : IScenarioAdvisoryService
{
    private readonly ITradePlanRepository _tradePlanRepository;
    private readonly IMarketDataProvider _marketDataProvider;

    public ScenarioAdvisoryService(
        ITradePlanRepository tradePlanRepository,
        IMarketDataProvider marketDataProvider)
    {
        _tradePlanRepository = tradePlanRepository;
        _marketDataProvider = marketDataProvider;
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

        foreach (var plan in activePlans)
        {
            var priceData = await _marketDataProvider.GetCurrentPriceAsync(plan.Symbol, ct);
            if (priceData == null) continue;

            var currentPrice = priceData.Close;

            foreach (var node in plan.ScenarioNodes!)
            {
                // Only evaluate Pending nodes
                if (node.Status != ScenarioNodeStatus.Pending) continue;

                // Skip SendNotification — no advisory needed
                if (node.ActionType == ScenarioActionType.SendNotification) continue;

                if (!IsConditionMet(node, plan, currentPrice)) continue;

                var conditionDesc = FormatConditionDescription(node);
                var conditionZone = FormatConditionZone(node);
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

    private static bool IsConditionMet(ScenarioNode node, TradePlan plan, decimal currentPrice)
    {
        return node.ConditionType switch
        {
            ScenarioConditionType.PriceAbove =>
                node.ConditionValue.HasValue && currentPrice >= node.ConditionValue.Value,

            ScenarioConditionType.PriceBelow =>
                node.ConditionValue.HasValue && currentPrice <= node.ConditionValue.Value,

            ScenarioConditionType.PricePercentChange =>
                EvaluatePricePercentChange(node, plan, currentPrice),

            // TrailingStopHit and TimeElapsed are not advisory-relevant zones
            _ => false
        };
    }

    private static bool EvaluatePricePercentChange(ScenarioNode node, TradePlan plan, decimal currentPrice)
    {
        if (!node.ConditionValue.HasValue || plan.EntryPrice == 0) return false;
        var percentChange = (currentPrice - plan.EntryPrice) / plan.EntryPrice * 100m;
        return node.ConditionValue.Value >= 0
            ? percentChange >= node.ConditionValue.Value
            : percentChange <= node.ConditionValue.Value;
    }

    /// <summary>
    /// Full description shown in ConditionDescription field: "Giá ≥ 80,000"
    /// </summary>
    private static string FormatConditionDescription(ScenarioNode node)
    {
        return node.ConditionType switch
        {
            ScenarioConditionType.PriceAbove =>
                $"Giá ≥ {node.ConditionValue:N0}",
            ScenarioConditionType.PriceBelow =>
                $"Giá ≤ {node.ConditionValue:N0}",
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
    private static string FormatConditionZone(ScenarioNode node)
    {
        return node.ConditionType switch
        {
            ScenarioConditionType.PriceAbove =>
                $"≥ {node.ConditionValue:N0}",
            ScenarioConditionType.PriceBelow =>
                $"≤ {node.ConditionValue:N0}",
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
