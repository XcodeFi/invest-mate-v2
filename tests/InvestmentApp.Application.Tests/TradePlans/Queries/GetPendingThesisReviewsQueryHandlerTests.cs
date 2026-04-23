using FluentAssertions;
using Moq;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.TradePlans.Queries.GetPendingThesisReviews;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Tests.TradePlans.Queries;

/// <summary>
/// Tests V2.1 — pending thesis review detection.
/// Plan ở state Ready/InProgress có:
///   (a) InvalidationRule với CheckDate ≤ today + 2 AND !IsTriggered → "InvalidationCheck"
///   (b) ExpectedReviewDate ≤ today → "PeriodicReview"
/// được liệt kê để user review.
/// </summary>
public class GetPendingThesisReviewsQueryHandlerTests
{
    private readonly Mock<ITradePlanRepository> _planRepo;
    private readonly GetPendingThesisReviewsQueryHandler _handler;
    private const string UserId = "user-1";

    public GetPendingThesisReviewsQueryHandlerTests()
    {
        _planRepo = new Mock<ITradePlanRepository>();
        _handler = new GetPendingThesisReviewsQueryHandler(_planRepo.Object);
    }

    // ---------------------------------------------------------------
    // Test 1: no plans → empty list
    // ---------------------------------------------------------------
    [Fact]
    public async Task Handle_NoPlans_ReturnsEmptyList()
    {
        _planRepo.Setup(r => r.GetActiveByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<TradePlan>());

        var result = await _handler.Handle(new GetPendingThesisReviewsQuery { UserId = UserId }, CancellationToken.None);

        result.Should().BeEmpty();
    }

    // ---------------------------------------------------------------
    // Test 2: Active plan with no CheckDate nor ExpectedReviewDate → excluded
    // ---------------------------------------------------------------
    [Fact]
    public async Task Handle_ActivePlanWithNoReviewDate_Excluded()
    {
        var plan = MakeReadyPlan();
        _planRepo.Setup(r => r.GetActiveByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { plan });

        var result = await _handler.Handle(new GetPendingThesisReviewsQuery { UserId = UserId }, CancellationToken.None);

        result.Should().BeEmpty();
    }

    // ---------------------------------------------------------------
    // Test 3: CheckDate trong vòng 2 ngày → included với reason "InvalidationCheck"
    // ---------------------------------------------------------------
    [Fact]
    public async Task Handle_RuleWithCheckDateDueSoon_Included()
    {
        var plan = MakeReadyPlan();
        plan.SetInvalidationCriteria(new List<InvalidationRule>
        {
            new() { Trigger = InvalidationTrigger.EarningsMiss,
                    Detail = "BCTC Q1 EPS tăng trưởng dưới 20% YoY so với kỳ vọng",
                    CheckDate = DateTime.UtcNow.AddDays(1), IsTriggered = false }
        });
        _planRepo.Setup(r => r.GetActiveByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { plan });

        var result = await _handler.Handle(new GetPendingThesisReviewsQuery { UserId = UserId }, CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].PlanId.Should().Be(plan.Id);
        result[0].Reasons.Should().Contain(r => r.Kind == "InvalidationCheck");
        result[0].Reasons.Should().Contain(r => r.TriggerType == "EarningsMiss");
    }

    // ---------------------------------------------------------------
    // Test 4: ExpectedReviewDate in past → included với reason "PeriodicReview"
    // ---------------------------------------------------------------
    [Fact]
    public async Task Handle_ExpectedReviewDatePast_Included()
    {
        var plan = MakeReadyPlan();
        plan.SetExpectedReviewDate(DateTime.UtcNow.AddDays(-3));
        _planRepo.Setup(r => r.GetActiveByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { plan });

        var result = await _handler.Handle(new GetPendingThesisReviewsQuery { UserId = UserId }, CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Reasons.Should().Contain(r => r.Kind == "PeriodicReview");
        result[0].DaysOverdue.Should().BeGreaterThanOrEqualTo(3);
    }

    // ---------------------------------------------------------------
    // Test 5: CheckDate far future (> 2 days) → excluded
    // ---------------------------------------------------------------
    [Fact]
    public async Task Handle_RuleFarFuture_Excluded()
    {
        var plan = MakeReadyPlan();
        plan.SetInvalidationCriteria(new List<InvalidationRule>
        {
            new() { Trigger = InvalidationTrigger.EarningsMiss,
                    Detail = "BCTC Q1 EPS tăng trưởng dưới 20% YoY so với kỳ vọng",
                    CheckDate = DateTime.UtcNow.AddDays(30), IsTriggered = false }
        });
        _planRepo.Setup(r => r.GetActiveByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { plan });

        var result = await _handler.Handle(new GetPendingThesisReviewsQuery { UserId = UserId }, CancellationToken.None);

        result.Should().BeEmpty();
    }

    // ---------------------------------------------------------------
    // Test 6: Executed plan (active repo trả về, handler skip)
    // ---------------------------------------------------------------
    [Fact]
    public async Task Handle_ExecutedPlan_Excluded()
    {
        var plan = MakeReadyPlan();
        plan.SetInvalidationCriteria(new List<InvalidationRule>
        {
            new() { Trigger = InvalidationTrigger.Manual,
                    Detail = "User tự nhận xét lý do đầu tư sai sau khi review",
                    CheckDate = DateTime.UtcNow.AddDays(-1), IsTriggered = false }
        });
        plan.MarkInProgress();
        plan.Execute("trade-1");  // → Executed (active, không phải terminal Cancelled/Reviewed)

        _planRepo.Setup(r => r.GetActiveByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { plan });

        var result = await _handler.Handle(new GetPendingThesisReviewsQuery { UserId = UserId }, CancellationToken.None);

        result.Should().BeEmpty();
    }

    // ---------------------------------------------------------------
    // Test 7: Rule đã triggered → excluded (đã review rồi)
    // ---------------------------------------------------------------
    [Fact]
    public async Task Handle_RuleAlreadyTriggered_Excluded()
    {
        var plan = MakeReadyPlan();
        plan.SetInvalidationCriteria(new List<InvalidationRule>
        {
            new() { Trigger = InvalidationTrigger.EarningsMiss,
                    Detail = "BCTC Q1 EPS tăng trưởng dưới 20% YoY so với kỳ vọng",
                    CheckDate = DateTime.UtcNow.AddDays(-1),
                    IsTriggered = true,  // ← đã review
                    TriggeredAt = DateTime.UtcNow.AddDays(-1) }
        });
        _planRepo.Setup(r => r.GetActiveByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { plan });

        var result = await _handler.Handle(new GetPendingThesisReviewsQuery { UserId = UserId }, CancellationToken.None);

        result.Should().BeEmpty();
    }

    // ---------------------------------------------------------------
    // Test 8: Multiple due rules on one plan → single entry with all reasons
    // ---------------------------------------------------------------
    [Fact]
    public async Task Handle_MultipleDueRules_SingleEntryWithAllReasons()
    {
        var plan = MakeReadyPlan();
        plan.SetInvalidationCriteria(new List<InvalidationRule>
        {
            new() { Trigger = InvalidationTrigger.EarningsMiss,
                    Detail = "BCTC Q1 EPS tăng trưởng dưới 20% YoY so với kỳ vọng",
                    CheckDate = DateTime.UtcNow.AddDays(-1), IsTriggered = false },
            new() { Trigger = InvalidationTrigger.TrendBreak,
                    Detail = "Giá đóng cửa dưới MA200 kèm volume gấp hơn 2 lần TB20",
                    CheckDate = DateTime.UtcNow.AddDays(1), IsTriggered = false }
        });
        plan.SetExpectedReviewDate(DateTime.UtcNow.AddDays(-5));  // cũng due
        _planRepo.Setup(r => r.GetActiveByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { plan });

        var result = await _handler.Handle(new GetPendingThesisReviewsQuery { UserId = UserId }, CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Reasons.Should().HaveCount(3);  // 2 rules + 1 periodic
        result[0].Reasons.Count(r => r.Kind == "InvalidationCheck").Should().Be(2);
        result[0].Reasons.Count(r => r.Kind == "PeriodicReview").Should().Be(1);
    }

    // ---------------------------------------------------------------
    // Test 9: Sort theo DaysOverdue DESC (urgent nhất lên đầu)
    // ---------------------------------------------------------------
    [Fact]
    public async Task Handle_MultiplePlans_SortedByUrgency()
    {
        var planA = MakeReadyPlan("VNM");
        planA.SetExpectedReviewDate(DateTime.UtcNow.AddDays(-1));  // 1 ngày overdue

        var planB = MakeReadyPlan("HPG");
        planB.SetExpectedReviewDate(DateTime.UtcNow.AddDays(-10)); // 10 ngày overdue — urgent hơn

        _planRepo.Setup(r => r.GetActiveByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { planA, planB });

        var result = await _handler.Handle(new GetPendingThesisReviewsQuery { UserId = UserId }, CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Symbol.Should().Be("HPG");  // urgent nhất lên đầu
        result[1].Symbol.Should().Be("VNM");
    }

    // ---------------------------------------------------------------
    // Test 10: Draft plan với CheckDate due → excluded (gate chưa pass)
    // ---------------------------------------------------------------
    [Fact]
    public async Task Handle_DraftPlan_Excluded()
    {
        // Plan không call MarkReady → state = Draft
        var plan = new TradePlan(UserId, "VNM", "Buy", 80_000m, 75_000m, 90_000m, 100,
            accountBalance: 100_000_000m);
        plan.SetInvalidationCriteria(new List<InvalidationRule>
        {
            new() { Trigger = InvalidationTrigger.EarningsMiss,
                    Detail = "BCTC Q1 EPS tăng trưởng dưới 20% YoY so với kỳ vọng",
                    CheckDate = DateTime.UtcNow.AddDays(-1), IsTriggered = false }
        });
        _planRepo.Setup(r => r.GetActiveByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { plan });

        var result = await _handler.Handle(new GetPendingThesisReviewsQuery { UserId = UserId }, CancellationToken.None);

        result.Should().BeEmpty();  // Draft: user chưa commit vào vị thế, chưa cần nhắc
    }

    // =================================================================
    // Helpers
    // =================================================================

    private static TradePlan MakeReadyPlan(string symbol = "VNM")
    {
        var plan = new TradePlan(UserId, symbol, "Buy",
            80_000m, 75_000m, 90_000m, 100,
            accountBalance: 100_000_000m);
        plan.SetThesis("Mua " + symbol + " vì EPS Q1 +22% YoY và ROE duy trì trên 20%, ngành tốt");
        plan.SetInvalidationCriteria(new List<InvalidationRule>
        {
            new() { Trigger = InvalidationTrigger.EarningsMiss,
                    Detail = "BCTC Q1 EPS tăng trưởng dưới 20% YoY so với kỳ vọng" }
        });
        plan.MarkReady();
        return plan;
    }
}
