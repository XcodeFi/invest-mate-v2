using System.Text.Json.Serialization;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.TradePlans.Queries.GetPendingThesisReviews;

/// <summary>
/// V2.1 — list plan đang active (Ready/InProgress) cần review thesis.
/// Trigger:
///   (a) InvalidationRule.CheckDate ≤ today + 2 AND !IsTriggered → "InvalidationCheck"
///   (b) ExpectedReviewDate ≤ today → "PeriodicReview"
/// Sort theo DaysOverdue DESC (urgent lên đầu).
/// </summary>
public class GetPendingThesisReviewsQuery : IRequest<List<PendingThesisReviewDto>>
{
    [JsonIgnore]
    public string UserId { get; set; } = null!;
}

public class PendingThesisReviewDto
{
    public string PlanId { get; set; } = null!;
    public string Symbol { get; set; } = null!;
    public string Direction { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string? Thesis { get; set; }
    public int Quantity { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal StopLoss { get; set; }
    public decimal Target { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Ngày overdue nhiều nhất trong các reasons — dùng để sort urgent lên đầu.</summary>
    public int DaysOverdue { get; set; }

    public List<PendingReviewReason> Reasons { get; set; } = new();
}

public class PendingReviewReason
{
    /// <summary>"InvalidationCheck" | "PeriodicReview"</summary>
    public string Kind { get; set; } = null!;

    /// <summary>Chỉ có giá trị khi Kind == "InvalidationCheck": "EarningsMiss" | "TrendBreak" | etc.</summary>
    public string? TriggerType { get; set; }

    /// <summary>Rule detail (Kind=InvalidationCheck) hoặc mô tả plan (Kind=PeriodicReview).</summary>
    public string Detail { get; set; } = null!;

    public DateTime DueDate { get; set; }

    /// <summary>Số ngày quá hạn (âm = chưa tới, dương = overdue).</summary>
    public int DaysOverdue { get; set; }
}

public class GetPendingThesisReviewsQueryHandler : IRequestHandler<GetPendingThesisReviewsQuery, List<PendingThesisReviewDto>>
{
    private readonly ITradePlanRepository _tradePlanRepository;
    private const int CheckDateLookAheadDays = 2;

    /// <summary>Timezone cho user VN — so sánh "today" theo local date để tránh off-by-one.</summary>
    private static readonly TimeZoneInfo VnTimeZone =
        TryGetVnTimeZone();

    private static TimeZoneInfo TryGetVnTimeZone()
    {
        // Windows: "SE Asia Standard Time"; Linux/Mac: "Asia/Ho_Chi_Minh".
        try { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); } catch { }
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); } catch { }
        return TimeZoneInfo.Utc;
    }

    public GetPendingThesisReviewsQueryHandler(ITradePlanRepository tradePlanRepository)
    {
        _tradePlanRepository = tradePlanRepository;
    }

    public async Task<List<PendingThesisReviewDto>> Handle(GetPendingThesisReviewsQuery request, CancellationToken cancellationToken)
    {
        // GetActiveByUserIdAsync đã filter Draft/Cancelled/Reviewed + IsDeleted ở DB level.
        // Ta chỉ cần iterate Ready/InProgress/Executed và skip Executed + LegacyExempt trong memory.
        var plans = await _tradePlanRepository.GetActiveByUserIdAsync(request.UserId, cancellationToken);

        // Day granularity theo VN local — tránh off-by-one cho user UTC+7.
        var todayVn = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VnTimeZone).Date;
        var checkHorizonVn = todayVn.AddDays(CheckDateLookAheadDays);

        var result = new List<PendingThesisReviewDto>();

        foreach (var plan in plans)
        {
            if (plan.Status != TradePlanStatus.Ready && plan.Status != TradePlanStatus.InProgress) continue;
            if (plan.LegacyExempt) continue;  // legacy plan chưa có thesis thật, không nag user

            var reasons = new List<PendingReviewReason>();

            // (a) InvalidationRule.CheckDate ≤ today + 2 (VN), chưa triggered
            if (plan.InvalidationCriteria != null)
            {
                foreach (var rule in plan.InvalidationCriteria)
                {
                    if (rule.IsTriggered) continue;
                    if (!rule.CheckDate.HasValue) continue;
                    var dueDayVn = TimeZoneInfo.ConvertTimeFromUtc(
                        DateTime.SpecifyKind(rule.CheckDate.Value, DateTimeKind.Utc), VnTimeZone).Date;
                    if (dueDayVn > checkHorizonVn) continue;

                    reasons.Add(new PendingReviewReason
                    {
                        Kind = "InvalidationCheck",
                        TriggerType = rule.Trigger.ToString(),
                        Detail = rule.Detail,
                        DueDate = rule.CheckDate.Value,
                        DaysOverdue = (todayVn - dueDayVn).Days
                    });
                }
            }

            // (b) ExpectedReviewDate ≤ today (VN)
            if (plan.ExpectedReviewDate.HasValue)
            {
                var dueDayVn = TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.SpecifyKind(plan.ExpectedReviewDate.Value, DateTimeKind.Utc), VnTimeZone).Date;
                if (dueDayVn <= todayVn)
                {
                    reasons.Add(new PendingReviewReason
                    {
                        Kind = "PeriodicReview",
                        TriggerType = null,
                        Detail = $"Đến ngày review định kỳ của {plan.Symbol}. Lý do đầu tư còn đúng không?",
                        DueDate = plan.ExpectedReviewDate.Value,
                        DaysOverdue = (todayVn - dueDayVn).Days
                    });
                }
            }

            if (reasons.Count == 0) continue;

            result.Add(new PendingThesisReviewDto
            {
                PlanId = plan.Id,
                Symbol = plan.Symbol,
                Direction = plan.Direction,
                Status = plan.Status.ToString(),
                Thesis = plan.Thesis,
                Quantity = plan.Quantity,
                EntryPrice = plan.EntryPrice,
                StopLoss = plan.StopLoss,
                Target = plan.Target,
                CreatedAt = plan.CreatedAt,
                DaysOverdue = reasons.Max(r => r.DaysOverdue),
                Reasons = reasons
            });
        }

        // Sort urgent (DaysOverdue cao nhất) lên đầu
        return result.OrderByDescending(r => r.DaysOverdue).ToList();
    }
}
