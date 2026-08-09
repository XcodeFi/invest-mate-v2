using InvestmentApp.Application.Common;
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
    private readonly ICorporateActionRepository _corporateActionRepository;
    private readonly ILogger<ScenarioEvaluationService> _logger;

    private static readonly IReadOnlyList<CorporateAction> NoActions = Array.Empty<CorporateAction>();

    public ScenarioEvaluationService(
        ITradePlanRepository tradePlanRepository,
        IStockPriceRepository stockPriceRepository,
        IAlertHistoryRepository alertHistoryRepository,
        ITechnicalIndicatorService technicalIndicatorService,
        ICorporateActionRepository corporateActionRepository,
        ILogger<ScenarioEvaluationService> logger)
    {
        _tradePlanRepository = tradePlanRepository;
        _stockPriceRepository = stockPriceRepository;
        _alertHistoryRepository = alertHistoryRepository;
        _technicalIndicatorService = technicalIndicatorService;
        _corporateActionRepository = corporateActionRepository;
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

        // Giá thị trường đã điều chỉnh tại ngày GDKHQ còn giá trên kế hoạch thì chưa —
        // so thẳng hai mặt bằng khác nhau sẽ kích hoạt kịch bản sai.
        var actionsByPlanKey = await LoadCorporateActionsAsync(advancedPlans, cancellationToken);

        foreach (var plan in advancedPlans)
        {
            if (!priceMap.TryGetValue(plan.Symbol.ToUpper(), out var currentPrice))
            {
                _logger.LogWarning("No price data for {Symbol}, skipping scenario evaluation for plan {PlanId}",
                    plan.Symbol, plan.Id);
                continue;
            }

            var actions = ActionsFor(actionsByPlanKey, plan);
            var triggered = await EvaluatePlan(plan, currentPrice, actions, cancellationToken);
            results.AddRange(triggered);
        }

        return results;
    }

    private async Task<ILookup<(string PortfolioId, string Symbol), CorporateAction>> LoadCorporateActionsAsync(
        IReadOnlyCollection<TradePlan> plans, CancellationToken cancellationToken)
    {
        var portfolioIds = plans
            .Where(p => !string.IsNullOrEmpty(p.PortfolioId))
            .Select(p => p.PortfolioId!)
            .Distinct()
            .ToList();

        var actions = portfolioIds.Count == 0
            ? NoActions
            : (await _corporateActionRepository.GetByPortfolioIdsAsync(portfolioIds, cancellationToken)).ToList();

        return actions.ToLookup(a => (a.PortfolioId, a.Symbol.ToUpper()));
    }

    private static IReadOnlyList<CorporateAction> ActionsFor(
        ILookup<(string PortfolioId, string Symbol), CorporateAction> lookup, TradePlan plan)
        => string.IsNullOrEmpty(plan.PortfolioId)
            ? NoActions
            : lookup[(plan.PortfolioId!, plan.Symbol.ToUpper())].ToList();

    private async Task<List<ScenarioEvaluationResult>> EvaluatePlan(
        TradePlan plan, decimal currentPrice, IReadOnlyList<CorporateAction> actions,
        CancellationToken cancellationToken)
    {
        var results = new List<ScenarioEvaluationResult>();
        var nodes = plan.ScenarioNodes!;
        var modified = false;

        // Update trailing stop data for already-triggered trailing nodes
        modified |= await UpdateTrailingStopsAsync(plan, currentPrice, actions, cancellationToken);

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
                if (EvaluateCondition(node, plan, currentPrice, actions))
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
                            ConditionValue = TradePlanPriceAdjuster.AdjustedConditionValue(node, plan, actions)
                        };
                        results.Add(result);

                        // Create alert history for notification
                        await CreateAlertHistory(plan, node, currentPrice, actions, cancellationToken);

                        _logger.LogInformation(
                            "Scenario triggered: Plan {PlanId} ({Symbol}), Node '{Label}', Price {Price}",
                            plan.Id, plan.Symbol, node.Label, currentPrice);

                        // After triggering, update trailing stops for the newly triggered node
                        if (node.ActionType == ScenarioActionType.ActivateTrailingStop)
                            await UpdateTrailingStopsAsync(plan, currentPrice, actions, cancellationToken);
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

    private bool EvaluateCondition(ScenarioNode node, TradePlan plan, decimal currentPrice,
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

            ScenarioConditionType.TrailingStopHit =>
                EvaluateTrailingStopHit(node, plan, currentPrice),

            ScenarioConditionType.TimeElapsed =>
                EvaluateTimeElapsed(node, plan),

            _ => false
        };
    }

    private bool EvaluatePricePercentChange(ScenarioNode node, TradePlan plan, decimal currentPrice,
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

    /// <returns><c>true</c> nếu trạng thái trượt vừa được quy về mặt bằng giá mới và cần lưu lại.</returns>
    private async Task<bool> UpdateTrailingStopsAsync(TradePlan plan, decimal currentPrice,
        IReadOnlyList<CorporateAction> actions, CancellationToken cancellationToken)
    {
        var nodes = plan.ScenarioNodes!;
        var rebased = false;

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

            // Đỉnh giá và mức trượt đang lưu là giá ghi nhận TRƯỚC ngày GDKHQ —
            // quy về mặt bằng mới một lần, nếu không mức trượt cũ sẽ cắt lỗ oan ngay hôm điều chỉnh.
            rebased |= TradePlanPriceAdjuster.RebaseTrailingState(config, plan, actions);

            // Check activation price
            var activationPrice = TradePlanPriceAdjuster.AdjustedActivationPrice(config, plan, actions);
            if (activationPrice.HasValue && currentPrice < activationPrice.Value)
                continue;

            // Update highest price
            if (!config.HighestPrice.HasValue || currentPrice > config.HighestPrice.Value)
            {
                // Check step size before updating
                if (config.StepSize.HasValue && config.HighestPrice.HasValue)
                {
                    var stepSize = TradePlanPriceAdjuster.AdjustedDelta(config.StepSize.Value, plan, actions);
                    if (currentPrice - config.HighestPrice.Value < stepSize)
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
                var trailValue = TradePlanPriceAdjuster.AdjustedTrailValue(config, plan, actions);
                var entryPrice = TradePlanPriceAdjuster.AdjustedEntryPrice(plan, actions);
                config.CurrentTrailingStop = config.Method switch
                {
                    TrailingStopMethod.Percentage =>
                        config.HighestPrice.Value * (1 - trailValue / 100m),
                    TrailingStopMethod.FixedAmount =>
                        config.HighestPrice.Value - trailValue,
                    TrailingStopMethod.ATR => ComputeAtrTrailingStop(config, entryPrice, atr14),
                    _ => config.HighestPrice.Value * (1 - trailValue / 100m)
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

        return rebased;
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
        IReadOnlyList<CorporateAction> actions, CancellationToken cancellationToken)
    {
        var actionValue = TradePlanPriceAdjuster.AdjustedActionValue(node, plan, actions);
        var actionText = node.ActionType switch
        {
            ScenarioActionType.SellPercent => $"Bán {node.ActionValue}% vị thế",
            ScenarioActionType.SellAll => "Bán toàn bộ vị thế",
            ScenarioActionType.MoveStopLoss => $"Dời SL đến {actionValue:N0}đ",
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
            thresholdValue: TradePlanPriceAdjuster.AdjustedConditionValue(node, plan, actions)
        );

        await _alertHistoryRepository.AddAsync(alert, cancellationToken);
    }
}
