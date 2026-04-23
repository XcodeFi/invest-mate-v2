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

    public GetPendingThesisReviewsQueryHandler(ITradePlanRepository tradePlanRepository)
    {
        _tradePlanRepository = tradePlanRepository;
    }

    public async Task<List<PendingThesisReviewDto>> Handle(GetPendingThesisReviewsQuery request, CancellationToken cancellationToken)
    {
        var plans = await _tradePlanRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        var now = DateTime.UtcNow;
        var checkHorizon = now.AddDays(CheckDateLookAheadDays);

        var result = new List<PendingThesisReviewDto>();

        foreach (var plan in plans)
        {
            if (plan.IsDeleted) continue;
            if (plan.Status != TradePlanStatus.Ready && plan.Status != TradePlanStatus.InProgress) continue;

            var reasons = new List<PendingReviewReason>();

            // (a) InvalidationRule.CheckDate ≤ today + 2, chưa triggered
            if (plan.InvalidationCriteria != null)
            {
                foreach (var rule in plan.InvalidationCriteria)
                {
                    if (rule.IsTriggered) continue;
                    if (!rule.CheckDate.HasValue) continue;
                    if (rule.CheckDate.Value > checkHorizon) continue;

                    reasons.Add(new PendingReviewReason
                    {
                        Kind = "InvalidationCheck",
                        TriggerType = rule.Trigger.ToString(),
                        Detail = rule.Detail,
                        DueDate = rule.CheckDate.Value,
                        DaysOverdue = (int)Math.Floor((now - rule.CheckDate.Value).TotalDays)
                    });
                }
            }

            // (b) ExpectedReviewDate ≤ today
            if (plan.ExpectedReviewDate.HasValue && plan.ExpectedReviewDate.Value <= now)
            {
                reasons.Add(new PendingReviewReason
                {
                    Kind = "PeriodicReview",
                    TriggerType = null,
                    Detail = $"Đến ngày review định kỳ của {plan.Symbol}. Thesis còn đúng không?",
                    DueDate = plan.ExpectedReviewDate.Value,
                    DaysOverdue = (int)Math.Floor((now - plan.ExpectedReviewDate.Value).TotalDays)
                });
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
