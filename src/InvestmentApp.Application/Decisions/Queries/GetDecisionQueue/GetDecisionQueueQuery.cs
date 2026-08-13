using System.Text.Json.Serialization;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Decisions.DTOs;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.TradePlans.Queries.GetPendingThesisReviews;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.ValueObjects;
using MediatR;

namespace InvestmentApp.Application.Decisions.Queries.GetDecisionQueue;

/// <summary>
/// Aggregate 5 nguồn alert thành 1 Decision Queue duy nhất cho Dashboard.
/// Xem `docs/plans/dashboard-decision-engine.md` §5 (P3) và
/// `docs/adr/0009-decision-queue-entry-side-signals.md`.
///
/// Logic:
///   1. Stop-loss: iterate user portfolios → IRiskCalculationService.GetPortfolioRiskSummaryAsync,
///      filter positions DistanceToStopLossPercent ≤ 2 (≤ 0 = Critical, ≤ 2 = Warning).
///   2. Scenario advisories: IScenarioAdvisoryService.GetAdvisoriesAsync (Warning).
///   3. Pending thesis reviews: GetPendingThesisReviewsQuery (DaysOverdue ≥ 3 = Critical, else Warning).
///   4. Missing stop-loss: cùng vòng lặp (1), vị thế StopLossPrice == null (Warning).
///   5. Buy opportunities: watchlist có TargetBuyPrice > 0 và giá ≤ mục tiêu (Info).
///   6. Dedupe by (symbol, portfolioId) — giữ severity cao nhất, tie-break ưu tiên StopLossHit.
///      BuyOpportunity có portfolioId rỗng nên thoát dedupe (khác việc với cảnh báo rủi ro cùng mã).
///   7. Sort: Severity desc → DueAt asc (overdue/oldest lên đầu). Info luôn xuống dưới rủi ro.
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
    private readonly IJournalEntryRepository _journalRepo;
    private readonly IMediator _mediator;
    private readonly IWatchlistRepository _watchlistRepo;
    private readonly IStockPriceService _priceService;

    /// <summary>Trần chờ giá watchlist — quá hạn thì phần cơ hội vắng mặt, queue vẫn trả.</summary>
    private const int WatchlistPriceTimeoutSeconds = 5;

    /// <summary>≤ 2% distance to SL → đưa vào queue.</summary>
    private const decimal StopLossWarningThresholdPercent = 2m;

    /// <summary>≤ 0% (đã chạm hoặc xuyên SL) → Critical.</summary>
    private const decimal StopLossCriticalThresholdPercent = 0m;

    /// <summary>Thesis quá hạn ≥ 3 ngày → Critical (match plan v1.1 spec).</summary>
    private const int ThesisOverdueCriticalDays = 3;

    /// <summary>VN timezone offset — local day boundary for daily suppression reset.</summary>
    private const int VnUtcOffsetHours = 7;

    /// <summary>Tag mà ResolveDecision ghi kèm mọi journal resolve: "trigger:{DecisionType}".</summary>
    private const string TriggerTagPrefix = "trigger:";

    public GetDecisionQueueQueryHandler(
        IPortfolioRepository portfolioRepo,
        ITradePlanRepository planRepo,
        IRiskCalculationService riskService,
        IScenarioAdvisoryService advisoryService,
        IJournalEntryRepository journalRepo,
        IMediator mediator,
        IWatchlistRepository watchlistRepo,
        IStockPriceService priceService)
    {
        _portfolioRepo = portfolioRepo;
        _planRepo = planRepo;
        _riskService = riskService;
        _advisoryService = advisoryService;
        _journalRepo = journalRepo;
        _mediator = mediator;
        _watchlistRepo = watchlistRepo;
        _priceService = priceService;
    }

    public async Task<DecisionQueueDto> Handle(GetDecisionQueueQuery request, CancellationToken cancellationToken)
    {
        var portfolios = (await _portfolioRepo.GetByUserIdAsync(request.UserId, cancellationToken)).ToList();

        var stopLossTask = LoadStopLossItemsAsync(portfolios, cancellationToken);
        var advisoriesTask = LoadAdvisoryItemsAsync(request.UserId, portfolios, cancellationToken);
        var reviewsTask = LoadThesisReviewItemsAsync(request.UserId, cancellationToken);
        var opportunitiesTask = LoadBuyOpportunityItemsAsync(request.UserId, cancellationToken);
        var resolvedTodayTask = LoadResolvedTodayAsync(request.UserId, cancellationToken);

        await Task.WhenAll(stopLossTask, advisoriesTask, reviewsTask, opportunitiesTask, resolvedTodayTask);

        var combined = stopLossTask.Result
            .Concat(advisoriesTask.Result)
            .Concat(reviewsTask.Result)
            .Concat(opportunitiesTask.Result)
            .ToList();

        var deduped = Dedupe(combined);

        // Per-day suppression: items resolved today (Decision journal in last VN day) drop out
        // until next VN midnight. Refresh-after-resolve no longer surfaces the same item.
        var (suppressedPlanIds, suppressedSymbolPortfolio, suppressedSymbolType) = resolvedTodayTask.Result;
        var unsuppressed = deduped.Where(i =>
            !(i.TradePlanId != null && suppressedPlanIds.Contains(i.TradePlanId))
            && !suppressedSymbolPortfolio.Contains((i.Symbol, i.PortfolioId))
            // symType chỉ áp cho item KHÔNG thuộc danh mục nào (BuyOpportunity). Item có PortfolioId
            // phải suppress qua symPort để giữ phạm vi danh mục — nếu không, resolve một mã ở danh mục
            // A sẽ giấu cảnh báo cùng mã ở danh mục B suốt ngày.
            && !(string.IsNullOrEmpty(i.PortfolioId)
                 && suppressedSymbolType.Contains((i.Symbol, i.Type.ToString())))
        ).ToList();

        var sorted = unsuppressed
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

    /// <summary>
    /// Load Decision-type journal entries created on or after start-of-VN-day. Build three
    /// suppression sets:
    ///   - planIds: matches ScenarioTrigger / ThesisReviewDue items by TradePlanId.
    ///   - symPort: (Symbol, PortfolioId) pairs — items resolved kèm portfolio.
    ///   - symType: (Symbol, DecisionType) từ tag "trigger:" — dành riêng cho entry không có
    ///     cả portfolioId lẫn tradePlanId, thứ mà hai tập trên không bắt được.
    /// </summary>
    private async Task<(HashSet<string> PlanIds,
                        HashSet<(string Symbol, string PortfolioId)> SymPort,
                        HashSet<(string Symbol, string Type)> SymType)>
        LoadResolvedTodayAsync(string userId, CancellationToken ct)
    {
        var vnNow = DateTime.UtcNow.AddHours(VnUtcOffsetHours);
        var vnDayStartUtc = vnNow.Date.AddHours(-VnUtcOffsetHours);

        var journals = await _journalRepo.GetByUserIdAsync(userId, ct);
        var todayDecisions = journals
            .Where(j => j.EntryType == JournalEntryType.Decision && j.Timestamp >= vnDayStartUtc)
            .ToList();

        var planIds = todayDecisions
            .Where(j => !string.IsNullOrEmpty(j.TradePlanId))
            .Select(j => j.TradePlanId!)
            .ToHashSet();

        var symPort = todayDecisions
            .Where(j => !string.IsNullOrEmpty(j.PortfolioId))
            .Select(j => (j.Symbol, j.PortfolioId!))
            .ToHashSet();

        var symType = todayDecisions
            .Where(j => string.IsNullOrEmpty(j.PortfolioId) && string.IsNullOrEmpty(j.TradePlanId))
            .Select(j => (
                j.Symbol,
                Tag: j.Tags.FirstOrDefault(t => t.StartsWith(TriggerTagPrefix, StringComparison.Ordinal))))
            .Where(x => x.Tag != null)
            .Select(x => (x.Symbol, Type: x.Tag![TriggerTagPrefix.Length..]))
            .ToHashSet();

        return (planIds, symPort, symType);
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
                // Guard: RiskCalculationService returns DistanceToStopLossPercent=0 when CurrentPrice<=0
                // (illiquid symbol or price-fetch failure). Giá không biết thì không kết luận gì —
                // đặt trước mọi nhánh để áp cho cả MissingStopLoss.
                if (pos.CurrentPrice <= 0) continue;

                // Vị thế chưa đặt SL: rủi ro không giới hạn, trước đây bị bỏ qua im lặng.
                if (pos.StopLossPrice == null)
                {
                    items.Add(new DecisionItemDto
                    {
                        Id = $"MissingStopLoss:{portfolio.Id}:{pos.Symbol}",
                        Type = DecisionType.MissingStopLoss,
                        Severity = DecisionSeverity.Warning,
                        Symbol = pos.Symbol,
                        PortfolioId = portfolio.Id,
                        PortfolioName = portfolio.Name,
                        Headline = $"{pos.Symbol} chưa đặt stop-loss (giá {pos.CurrentPrice:N0})",
                        ThesisOrReason = null,
                        CurrentPrice = pos.CurrentPrice,
                        PlannedExitPrice = null,
                        TradePlanId = null,
                        DueAt = now,
                        CreatedAt = now
                    });
                    continue;
                }

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
                    // Kế hoạch cấp ngưỡng này — thẻ cần id để dời SL ngay tại chỗ (ADR-0017).
                    TradePlanId = pos.TradePlanId,
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

    /// <summary>
    /// Cơ hội mua từ watchlist: mã có mục tiêu mua và giá hiện tại đã ≤ mục tiêu.
    /// Chỉ fetch giá cho mã CÓ mục tiêu — mã không đặt mục tiêu không thể sinh cơ hội.
    /// </summary>
    private async Task<List<DecisionItemDto>> LoadBuyOpportunityItemsAsync(string userId, CancellationToken ct)
    {
        var watchlists = await _watchlistRepo.GetByUserIdAsync(userId, ct);

        var targets = watchlists
            .SelectMany(w => w.Items)
            .Where(i => i.TargetBuyPrice is > 0)
            .GroupBy(i => i.Symbol, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        if (targets.Count == 0) return new List<DecisionItemDto>();

        Dictionary<string, Money> raw;
        try
        {
            // GetCurrentPricesAsync không nhận CancellationToken — bọc timeout thủ công.
            raw = await _priceService
                .GetCurrentPricesAsync(targets.Select(t => new StockSymbol(t.Symbol)))
                .WaitAsync(TimeSpan.FromSeconds(WatchlistPriceTimeoutSeconds), ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // Giá hỏng hoặc quá chậm → phần cơ hội vắng mặt, các nguồn khác vẫn trả bình thường.
            return new List<DecisionItemDto>();
        }

        // Provider có thể trả key khác hoa/thường với symbol đã gửi.
        var prices = new Dictionary<string, Money>(raw, StringComparer.OrdinalIgnoreCase);

        var now = DateTime.UtcNow;
        var items = new List<DecisionItemDto>();

        foreach (var t in targets)
        {
            if (!prices.TryGetValue(t.Symbol, out var money)) continue;
            var price = money.Amount;
            if (price <= 0) continue;
            if (price > t.TargetBuyPrice!.Value) continue;

            items.Add(new DecisionItemDto
            {
                Id = $"BuyOpportunity:{t.Symbol}",
                Type = DecisionType.BuyOpportunity,
                Severity = DecisionSeverity.Info,
                Symbol = t.Symbol,
                PortfolioId = string.Empty,
                PortfolioName = string.Empty,
                Headline = $"{t.Symbol} giá {price:N0} ≤ mục tiêu mua {t.TargetBuyPrice.Value:N0}",
                ThesisOrReason = t.Note,
                CurrentPrice = price,
                PlannedExitPrice = null,
                TradePlanId = null,
                DueAt = now,
                CreatedAt = now
            });
        }

        return items;
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
    /// Thứ tự ưu tiên khi hai item cùng (Symbol, PortfolioId) và cùng severity. Càng cao càng thắng.
    /// MissingStopLoss xếp thấp nhất: "chưa đặt SL" là tình trạng nền, còn SL đã thủng hay kịch bản
    /// đã trigger là sự kiện cụ thể và cần xử lý trước. Không có bảng này thì thứ tự concat quyết
    /// định kẻ thắng và một advisory có thể bị nuốt im lặng.
    /// </summary>
    private static int DedupeRank(DecisionType type) => type switch
    {
        DecisionType.StopLossHit => 3,
        DecisionType.ScenarioTrigger => 2,
        DecisionType.ThesisReviewDue => 1,
        _ => 0
    };

    /// <summary>
    /// Dedupe theo (Symbol, PortfolioId). Giữ item severity cao nhất; tie-break theo <see cref="DedupeRank"/>.
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
                .ThenByDescending(i => DedupeRank(i.Type))
                .First();
            result.Add(winner);
        }

        return result;
    }
}
