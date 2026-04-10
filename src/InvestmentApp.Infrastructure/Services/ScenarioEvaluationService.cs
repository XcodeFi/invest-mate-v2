using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace InvestmentApp.Infrastructure.Services;

public class ScenarioEvaluationService : IScenarioEvaluationService
{
    private readonly ITradePlanRepository _tradePlanRepository;
    private readonly IStockPriceRepository _stockPriceRepository;
    private readonly IAlertHistoryRepository _alertHistoryRepository;
    private readonly ITechnicalIndicatorService _technicalIndicatorService;
    private readonly ILogger<ScenarioEvaluationService> _logger;

    public ScenarioEvaluationService(
        ITradePlanRepository tradePlanRepository,
        IStockPriceRepository stockPriceRepository,
        IAlertHistoryRepository alertHistoryRepository,
        ITechnicalIndicatorService technicalIndicatorService,
        ILogger<ScenarioEvaluationService> logger)
    {
        _tradePlanRepository = tradePlanRepository;
        _stockPriceRepository = stockPriceRepository;
        _alertHistoryRepository = alertHistoryRepository;
        _technicalIndicatorService = technicalIndicatorService;
        _logger = logger;
    }

    public async Task<List<ScenarioEvaluationResult>> EvaluateAllAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<ScenarioEvaluationResult>();

        // Get only advanced in-progress plans (filtered at DB level)
        var advancedPlans = (await _tradePlanRepository.GetAdvancedInProgressAsync(cancellationToken))
            .Where(p => p.ScenarioNodes != null && p.ScenarioNodes.Count > 0)
            .ToList();

        if (advancedPlans.Count == 0) return results;

        // Fetch latest prices for all symbols
        var symbols = advancedPlans.Select(p => p.Symbol).Distinct();
        var latestPrices = await _stockPriceRepository.GetLatestPricesAsync(symbols, cancellationToken);
        var priceMap = latestPrices.ToDictionary(p => p.Symbol.ToUpper(), p => p.Close);

        foreach (var plan in advancedPlans)
        {
            if (!priceMap.TryGetValue(plan.Symbol.ToUpper(), out var currentPrice))
            {
                _logger.LogWarning("No price data for {Symbol}, skipping scenario evaluation for plan {PlanId}",
                    plan.Symbol, plan.Id);
                continue;
            }

            var triggered = await EvaluatePlan(plan, currentPrice, cancellationToken);
            results.AddRange(triggered);
        }

        return results;
    }

    private async Task<List<ScenarioEvaluationResult>> EvaluatePlan(
        TradePlan plan, decimal currentPrice, CancellationToken cancellationToken)
    {
        var results = new List<ScenarioEvaluationResult>();
        var nodes = plan.ScenarioNodes!;
        var modified = false;

        // Update trailing stop data for already-triggered trailing nodes
        await UpdateTrailingStopsAsync(plan, currentPrice, cancellationToken);

        // Iterate in rounds: evaluate, trigger, then re-evaluate newly-evaluable children
        bool anyTriggered;
        do
        {
            anyTriggered = false;

            // Find evaluable nodes: Pending AND (root OR parent is Triggered)
            var evaluableNodes = nodes.Where(n =>
                n.Status == ScenarioNodeStatus.Pending &&
                (n.ParentId == null || nodes.Any(p => p.NodeId == n.ParentId && p.Status == ScenarioNodeStatus.Triggered))
            ).ToList();

            foreach (var node in evaluableNodes)
            {
                if (EvaluateCondition(node, plan, currentPrice))
                {
                    try
                    {
                        plan.TriggerScenarioNode(node.NodeId);
                        modified = true;
                        anyTriggered = true;

                        var result = new ScenarioEvaluationResult
                        {
                            TradePlanId = plan.Id,
                            NodeId = node.NodeId,
                            UserId = plan.UserId,
                            Symbol = plan.Symbol,
                            ActionType = node.ActionType.ToString(),
                            Label = node.Label,
                            CurrentPrice = currentPrice,
                            ConditionValue = node.ConditionValue
                        };
                        results.Add(result);

                        // Create alert history for notification
                        await CreateAlertHistory(plan, node, currentPrice, cancellationToken);

                        _logger.LogInformation(
                            "Scenario triggered: Plan {PlanId} ({Symbol}), Node '{Label}', Price {Price}",
                            plan.Id, plan.Symbol, node.Label, currentPrice);

                        // After triggering, update trailing stops for the newly triggered node
                        if (node.ActionType == ScenarioActionType.ActivateTrailingStop)
                            await UpdateTrailingStopsAsync(plan, currentPrice, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error triggering scenario node {NodeId} for plan {PlanId}",
                            node.NodeId, plan.Id);
                    }
                }
            }
        } while (anyTriggered);

        if (modified)
        {
            await _tradePlanRepository.UpdateAsync(plan, cancellationToken);
        }

        return results;
    }

    private bool EvaluateCondition(ScenarioNode node, TradePlan plan, decimal currentPrice)
    {
        return node.ConditionType switch
        {
            ScenarioConditionType.PriceAbove =>
                node.ConditionValue.HasValue && currentPrice >= node.ConditionValue.Value,

            ScenarioConditionType.PriceBelow =>
                node.ConditionValue.HasValue && currentPrice <= node.ConditionValue.Value,

            ScenarioConditionType.PricePercentChange =>
                EvaluatePricePercentChange(node, plan, currentPrice),

            ScenarioConditionType.TrailingStopHit =>
                EvaluateTrailingStopHit(node, plan, currentPrice),

            ScenarioConditionType.TimeElapsed =>
                EvaluateTimeElapsed(node, plan),

            _ => false
        };
    }

    private bool EvaluatePricePercentChange(ScenarioNode node, TradePlan plan, decimal currentPrice)
    {
        if (!node.ConditionValue.HasValue || plan.EntryPrice == 0) return false;
        var percentChange = (currentPrice - plan.EntryPrice) / plan.EntryPrice * 100m;
        return node.ConditionValue.Value >= 0
            ? percentChange >= node.ConditionValue.Value
            : percentChange <= node.ConditionValue.Value;
    }

    private bool EvaluateTrailingStopHit(ScenarioNode node, TradePlan plan, decimal currentPrice)
    {
        // Find parent node with ActivateTrailingStop that has a computed trailing stop
        if (node.ParentId == null) return false;
        var parent = plan.ScenarioNodes?.FirstOrDefault(n => n.NodeId == node.ParentId);
        if (parent?.TrailingStopConfig?.CurrentTrailingStop == null) return false;
        return currentPrice <= parent.TrailingStopConfig.CurrentTrailingStop.Value;
    }

    private bool EvaluateTimeElapsed(ScenarioNode node, TradePlan plan)
    {
        if (!node.ConditionValue.HasValue) return false;
        var referenceTime = plan.ExecutedAt ?? plan.CreatedAt;
        if (node.ParentId != null)
        {
            var parent = plan.ScenarioNodes?.FirstOrDefault(n => n.NodeId == node.ParentId);
            if (parent?.TriggeredAt != null) referenceTime = parent.TriggeredAt.Value;
        }
        var daysPassed = (DateTime.UtcNow - referenceTime).TotalDays;
        return daysPassed >= (double)node.ConditionValue.Value;
    }

    private async Task UpdateTrailingStopsAsync(TradePlan plan, decimal currentPrice,
        CancellationToken cancellationToken)
    {
        var nodes = plan.ScenarioNodes!;

        // Find triggered nodes with ActivateTrailingStop action
        var trailingNodes = nodes.Where(n =>
            n.Status == ScenarioNodeStatus.Triggered &&
            n.ActionType == ScenarioActionType.ActivateTrailingStop &&
            n.TrailingStopConfig != null).ToList();

        // Check if any node uses ATR method — fetch ATR once per symbol
        decimal? atr14 = null;
        bool atrFetched = false;

        foreach (var trailingNode in trailingNodes)
        {
            var config = trailingNode.TrailingStopConfig!;

            // Check activation price
            if (config.ActivationPrice.HasValue && currentPrice < config.ActivationPrice.Value)
                continue;

            // Update highest price
            if (!config.HighestPrice.HasValue || currentPrice > config.HighestPrice.Value)
            {
                // Check step size before updating
                if (config.StepSize.HasValue && config.HighestPrice.HasValue)
                {
                    if (currentPrice - config.HighestPrice.Value < config.StepSize.Value)
                        continue;
                }
                config.HighestPrice = currentPrice;

                // Fetch ATR lazily (only when an ATR node is encountered)
                if (config.Method == TrailingStopMethod.ATR && !atrFetched)
                {
                    atrFetched = true;
                    try
                    {
                        var analysis = await _technicalIndicatorService.AnalyzeAsync(plan.Symbol, cancellationToken: cancellationToken);
                        atr14 = analysis.Atr14;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to fetch ATR for {Symbol}, using proxy fallback", plan.Symbol);
                    }
                }

                // Compute trailing stop
                config.CurrentTrailingStop = config.Method switch
                {
                    TrailingStopMethod.Percentage =>
                        config.HighestPrice.Value * (1 - config.TrailValue / 100m),
                    TrailingStopMethod.FixedAmount =>
                        config.HighestPrice.Value - config.TrailValue,
                    TrailingStopMethod.ATR => ComputeAtrTrailingStop(config, plan.EntryPrice, atr14),
                    _ => config.HighestPrice.Value * (1 - config.TrailValue / 100m)
                };
            }

            // Check if trailing stop is hit on child nodes
            if (config.CurrentTrailingStop.HasValue && currentPrice <= config.CurrentTrailingStop.Value)
            {
                // Find pending child nodes with TrailingStopHit condition
                var trailingChildren = nodes.Where(n =>
                    n.ParentId == trailingNode.NodeId &&
                    n.Status == ScenarioNodeStatus.Pending &&
                    n.ConditionType == ScenarioConditionType.TrailingStopHit).ToList();

                foreach (var child in trailingChildren)
                {
                    // Mark as ready to trigger (condition met)
                    // The actual trigger happens in the evaluable nodes loop
                    child.TrailingStopConfig = config; // pass config for reference
                }
            }
        }
    }

    private decimal ComputeAtrTrailingStop(TrailingStopConfig config, decimal entryPrice, decimal? atr14)
    {
        if (atr14.HasValue)
        {
            return config.HighestPrice!.Value - config.TrailValue * atr14.Value;
        }

        // Fallback: use entry price × 2% as ATR proxy
        _logger.LogWarning(
            "ATR(14) not available, using proxy (entryPrice × 0.02) for trailing stop calculation");
        return config.HighestPrice!.Value - config.TrailValue * (entryPrice * 0.02m);
    }

    private async Task CreateAlertHistory(TradePlan plan, ScenarioNode node, decimal currentPrice,
        CancellationToken cancellationToken)
    {
        var actionText = node.ActionType switch
        {
            ScenarioActionType.SellPercent => $"Bán {node.ActionValue}% vị thế",
            ScenarioActionType.SellAll => "Bán toàn bộ vị thế",
            ScenarioActionType.MoveStopLoss => $"Dời SL đến {node.ActionValue:N0}đ",
            ScenarioActionType.MoveStopToBreakeven => "Dời SL về giá hòa vốn",
            ScenarioActionType.ActivateTrailingStop => $"Kích hoạt trailing stop {node.TrailingStopConfig?.TrailValue}%",
            ScenarioActionType.AddPosition => $"Thêm {node.ActionValue}% vị thế",
            ScenarioActionType.SendNotification => "Thông báo",
            _ => node.ActionType.ToString()
        };

        var alert = new AlertHistory(
            plan.UserId,
            plan.Id, // alertRuleId — use planId as reference
            "ScenarioPlaybook",
            $"[{plan.Symbol}] Kịch bản: {node.Label}",
            $"{actionText}. Giá hiện tại: {currentPrice:N0}đ",
            symbol: plan.Symbol,
            currentValue: currentPrice,
            thresholdValue: node.ConditionValue
        );

        await _alertHistoryRepository.AddAsync(alert, cancellationToken);
    }
}
