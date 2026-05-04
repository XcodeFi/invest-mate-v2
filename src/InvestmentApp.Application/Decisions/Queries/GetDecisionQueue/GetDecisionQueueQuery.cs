using System.Text.Json.Serialization;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Decisions.DTOs;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.TradePlans.Queries.GetPendingThesisReviews;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.Decisions.Queries.GetDecisionQueue;

/// <summary>
/// Aggregate 3 nguồn alert (StopLoss / Scenario trigger / Thesis review) thành 1 Decision Queue
/// duy nhất cho Dashboard. Xem `docs/plans/dashboard-decision-engine.md` §5 (P3).
///
/// Logic:
///   1. Stop-loss: iterate user portfolios → IRiskCalculationService.GetPortfolioRiskSummaryAsync,
///      filter positions DistanceToStopLossPercent ≤ 2 (≤ 0 = Critical, ≤ 2 = Warning).
///   2. Scenario advisories: IScenarioAdvisoryService.GetAdvisoriesAsync (Warning).
///   3. Pending thesis reviews: GetPendingThesisReviewsQuery (DaysOverdue ≥ 3 = Critical, else Warning).
///   4. Dedupe by (symbol, portfolioId) — giữ severity cao nhất, tie-break ưu tiên StopLossHit.
///   5. Sort: Severity desc → DueAt asc (overdue/oldest lên đầu).
/// </summary>
public class GetDecisionQueueQuery : IRequest<DecisionQueueDto>
{
    [JsonIgnore]
    public string UserId { get; set; } = null!;
}

public class GetDecisionQueueQueryHandler : IRequestHandler<GetDecisionQueueQuery, DecisionQueueDto>
{
    private readonly IPortfolioRepository _portfolioRepo;
    private readonly ITradePlanRepository _planRepo;
    private readonly IRiskCalculationService _riskService;
    private readonly IScenarioAdvisoryService _advisoryService;
    private readonly IMediator _mediator;

    /// <summary>≤ 2% distance to SL → đưa vào queue.</summary>
    private const decimal StopLossWarningThresholdPercent = 2m;

    /// <summary>≤ 0% (đã chạm hoặc xuyên SL) → Critical.</summary>
    private const decimal StopLossCriticalThresholdPercent = 0m;

    /// <summary>Thesis quá hạn ≥ 3 ngày → Critical (match plan v1.1 spec).</summary>
    private const int ThesisOverdueCriticalDays = 3;

    public GetDecisionQueueQueryHandler(
        IPortfolioRepository portfolioRepo,
        ITradePlanRepository planRepo,
        IRiskCalculationService riskService,
        IScenarioAdvisoryService advisoryService,
        IMediator mediator)
    {
        _portfolioRepo = portfolioRepo;
        _planRepo = planRepo;
        _riskService = riskService;
        _advisoryService = advisoryService;
        _mediator = mediator;
    }

    public async Task<DecisionQueueDto> Handle(GetDecisionQueueQuery request, CancellationToken cancellationToken)
    {
        var portfolios = (await _portfolioRepo.GetByUserIdAsync(request.UserId, cancellationToken)).ToList();

        var stopLossTask = LoadStopLossItemsAsync(portfolios, cancellationToken);
        var advisoriesTask = LoadAdvisoryItemsAsync(request.UserId, portfolios, cancellationToken);
        var reviewsTask = LoadThesisReviewItemsAsync(request.UserId, cancellationToken);

        await Task.WhenAll(stopLossTask, advisoriesTask, reviewsTask);

        var combined = stopLossTask.Result
            .Concat(advisoriesTask.Result)
            .Concat(reviewsTask.Result)
            .ToList();

        var deduped = Dedupe(combined);
        var sorted = deduped
            .OrderByDescending(i => (int)i.Severity == (int)DecisionSeverity.Critical ? 2
                                  : (int)i.Severity == (int)DecisionSeverity.Warning ? 1 : 0)
            .ThenBy(i => i.DueAt ?? DateTime.MaxValue)
            .ToList();

        return new DecisionQueueDto
        {
            Items = sorted,
            TotalCount = sorted.Count
        };
    }

    private async Task<List<DecisionItemDto>> LoadStopLossItemsAsync(
        IReadOnlyList<Portfolio> portfolios,
        CancellationToken ct)
    {
        if (portfolios.Count == 0) return new List<DecisionItemDto>();

        var tasks = portfolios.Select(p => _riskService.GetPortfolioRiskSummaryAsync(p.Id, ct)).ToList();
        var summaries = await Task.WhenAll(tasks);

        var items = new List<DecisionItemDto>();
        var now = DateTime.UtcNow;

        for (int i = 0; i < portfolios.Count; i++)
        {
            var portfolio = portfolios[i];
            var summary = summaries[i];
            if (summary?.Positions == null) continue;

            foreach (var pos in summary.Positions)
            {
                if (pos.StopLossPrice == null) continue;
                // Guard: RiskCalculationService returns DistanceToStopLossPercent=0 when CurrentPrice<=0
                // (illiquid symbol or price-fetch failure) — would otherwise false-positive as Critical "thủng SL".
                if (pos.CurrentPrice <= 0) continue;
                if (pos.DistanceToStopLossPercent > StopLossWarningThresholdPercent) continue;

                var hit = pos.DistanceToStopLossPercent <= StopLossCriticalThresholdPercent;
                items.Add(new DecisionItemDto
                {
                    Id = $"StopLossHit:{portfolio.Id}:{pos.Symbol}",
                    Type = DecisionType.StopLossHit,
                    Severity = hit ? DecisionSeverity.Critical : DecisionSeverity.Warning,
                    Symbol = pos.Symbol,
                    PortfolioId = portfolio.Id,
                    PortfolioName = portfolio.Name,
                    Headline = hit
                        ? $"{pos.Symbol} đã thủng SL {pos.StopLossPrice:N0} (giá {pos.CurrentPrice:N0})"
                        : $"{pos.Symbol} cách SL {pos.DistanceToStopLossPercent:0.0}% (SL {pos.StopLossPrice:N0})",
                    ThesisOrReason = null,
                    CurrentPrice = pos.CurrentPrice,
                    PlannedExitPrice = pos.StopLossPrice,
                    TradePlanId = null,
                    DueAt = now,
                    CreatedAt = now
                });
            }
        }

        return items;
    }

    private async Task<List<DecisionItemDto>> LoadAdvisoryItemsAsync(
        string userId,
        IReadOnlyList<Portfolio> portfolios,
        CancellationToken ct)
    {
        var advisories = await _advisoryService.GetAdvisoriesAsync(userId, ct);
        if (advisories.Count == 0) return new List<DecisionItemDto>();

        // ScenarioAdvisory không carry PortfolioId trực tiếp. Lookup TradePlan để map về portfolio thật
        // (cần thiết cho dedupe đúng khi cùng symbol có ở nhiều portfolio).
        var planIds = advisories.Select(a => a.TradePlanId).Distinct().ToList();
        var plansById = (await _planRepo.GetByUserIdAsync(userId, ct))
            .Where(p => planIds.Contains(p.Id))
            .ToDictionary(p => p.Id, p => p);
        var portfoliosById = portfolios.ToDictionary(p => p.Id, p => p);

        var now = DateTime.UtcNow;

        return advisories.Select(adv =>
        {
            string portfolioId = string.Empty;
            string portfolioName = string.Empty;
            if (plansById.TryGetValue(adv.TradePlanId, out var plan) && plan.PortfolioId != null
                && portfoliosById.TryGetValue(plan.PortfolioId, out var portfolio))
            {
                portfolioId = portfolio.Id;
                portfolioName = portfolio.Name;
            }

            return new DecisionItemDto
            {
                Id = $"ScenarioTrigger:{adv.TradePlanId}:{adv.NodeId}",
                Type = DecisionType.ScenarioTrigger,
                Severity = DecisionSeverity.Warning,
                Symbol = adv.Symbol,
                PortfolioId = portfolioId,
                PortfolioName = portfolioName,
                Headline = adv.Message,
                ThesisOrReason = adv.ConditionDescription,
                CurrentPrice = adv.CurrentPrice,
                PlannedExitPrice = null,
                TradePlanId = adv.TradePlanId,
                DueAt = now,
                CreatedAt = now
            };
        }).ToList();
    }

    private async Task<List<DecisionItemDto>> LoadThesisReviewItemsAsync(string userId, CancellationToken ct)
    {
        var reviews = await _mediator.Send(new GetPendingThesisReviewsQuery { UserId = userId }, ct);
        if (reviews.Count == 0) return new List<DecisionItemDto>();

        var now = DateTime.UtcNow;
        return reviews.Select(r =>
        {
            var critical = r.DaysOverdue >= ThesisOverdueCriticalDays;
            var topReason = r.Reasons.OrderByDescending(reason => reason.DaysOverdue).FirstOrDefault();
            return new DecisionItemDto
            {
                Id = $"ThesisReviewDue:{r.PlanId}",
                Type = DecisionType.ThesisReviewDue,
                Severity = critical ? DecisionSeverity.Critical : DecisionSeverity.Warning,
                Symbol = r.Symbol,
                PortfolioId = string.Empty,
                PortfolioName = string.Empty,
                Headline = r.DaysOverdue > 0
                    ? $"{r.Symbol} thesis quá hạn review {r.DaysOverdue} ngày"
                    : $"{r.Symbol} đến hạn review thesis",
                ThesisOrReason = r.Thesis ?? topReason?.Detail,
                CurrentPrice = null,
                PlannedExitPrice = null,
                TradePlanId = r.PlanId,
                DueAt = topReason?.DueDate ?? now,
                CreatedAt = now
            };
        }).ToList();
    }

    /// <summary>
    /// Dedupe theo (Symbol, PortfolioId). Giữ item severity cao nhất; tie-break ưu tiên StopLossHit.
    /// </summary>
    private static List<DecisionItemDto> Dedupe(List<DecisionItemDto> items)
    {
        var groups = items.GroupBy(i => (i.Symbol, i.PortfolioId));
        var result = new List<DecisionItemDto>();

        foreach (var group in groups)
        {
            // Empty PortfolioId (thesis review không link portfolio) → không dedupe với risk
            if (string.IsNullOrEmpty(group.Key.PortfolioId))
            {
                result.AddRange(group);
                continue;
            }

            var winner = group
                .OrderByDescending(i => i.Severity == DecisionSeverity.Critical ? 2
                                      : i.Severity == DecisionSeverity.Warning ? 1 : 0)
                .ThenByDescending(i => i.Type == DecisionType.StopLossHit ? 1 : 0)
                .First();
            result.Add(winner);
        }

        return result;
    }
}
