using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.TradePlans.Queries.GetScenarioHistory;

public class GetScenarioHistoryQuery : IRequest<List<ScenarioHistoryDto>>
{
    public string TradePlanId { get; set; } = null!;
    public string UserId { get; set; } = null!;
}

public class ScenarioHistoryDto
{
    public string NodeId { get; set; } = null!;
    public string Label { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime? TriggeredAt { get; set; }
    public decimal? PriceAtTrigger { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public decimal? ActionValue { get; set; }
    public string ConditionType { get; set; } = string.Empty;
    public decimal? ConditionValue { get; set; }
    public string? ParentId { get; set; }
}

public class GetScenarioHistoryQueryHandler : IRequestHandler<GetScenarioHistoryQuery, List<ScenarioHistoryDto>>
{
    private readonly ITradePlanRepository _tradePlanRepository;
    private readonly IAlertHistoryRepository _alertHistoryRepository;

    public GetScenarioHistoryQueryHandler(
        ITradePlanRepository tradePlanRepository,
        IAlertHistoryRepository alertHistoryRepository)
    {
        _tradePlanRepository = tradePlanRepository;
        _alertHistoryRepository = alertHistoryRepository;
    }

    public async Task<List<ScenarioHistoryDto>> Handle(GetScenarioHistoryQuery request, CancellationToken cancellationToken)
    {
        var plan = await _tradePlanRepository.GetByIdAsync(request.TradePlanId, cancellationToken);
        if (plan == null || plan.UserId != request.UserId)
            throw new KeyNotFoundException($"Trade plan {request.TradePlanId} not found");

        if (plan.ScenarioNodes == null || plan.ScenarioNodes.Count == 0)
            return new List<ScenarioHistoryDto>();

        // Get alert history records for this plan's scenario triggers
        var alertHistories = (await _alertHistoryRepository.GetByAlertRuleIdAsync(
            request.TradePlanId, "ScenarioPlaybook", cancellationToken)).ToList();

        // Build a lookup: match alert to node by node label in alert title
        // Alert title format: "[SYMBOL] Kịch bản: {node.Label}"
        var alertByNodeLabel = new Dictionary<string, AlertHistory>();
        foreach (var alert in alertHistories)
        {
            // Extract node label from title: "[VNM] Kịch bản: Chốt lời 30%"
            var prefix = $"[{plan.Symbol}] Kịch bản: ";
            if (alert.Title.StartsWith(prefix))
            {
                var label = alert.Title[prefix.Length..];
                alertByNodeLabel.TryAdd(label, alert);
            }
        }

        var result = plan.ScenarioNodes.Select(node =>
        {
            alertByNodeLabel.TryGetValue(node.Label, out var matchedAlert);

            return new ScenarioHistoryDto
            {
                NodeId = node.NodeId,
                Label = node.Label,
                Status = node.Status.ToString(),
                TriggeredAt = node.TriggeredAt,
                PriceAtTrigger = matchedAlert?.CurrentValue,
                ActionType = node.ActionType.ToString(),
                ActionValue = node.ActionValue,
                ConditionType = node.ConditionType.ToString(),
                ConditionValue = node.ConditionValue,
                ParentId = node.ParentId
            };
        }).ToList();

        // Sort: triggered nodes first (by TriggeredAt descending), then pending nodes
        result = result
            .OrderByDescending(r => r.TriggeredAt.HasValue ? 1 : 0)
            .ThenByDescending(r => r.TriggeredAt)
            .ToList();

        return result;
    }
}
