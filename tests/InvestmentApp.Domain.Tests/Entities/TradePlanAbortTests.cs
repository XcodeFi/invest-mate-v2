using FluentAssertions;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.Events;

namespace InvestmentApp.Domain.Tests.Entities;

/// <summary>
/// Tests #11-15 trong plan Vin-discipline §D4.
/// AbortWithThesisInvalidation áp dụng cho Ready/InProgress/Executed.
/// </summary>
public class TradePlanAbortTests
{
    private static TradePlan CreateReadyPlan(decimal? accountBalance = 100_000_000m)
    {
        // size = 100 * 80_000 = 8_000_000 = 8% → require full discipline
        var plan = new TradePlan("user-1", "VNM", "Buy",
            80_000m, 75_000m, 90_000m, 100,
            accountBalance: accountBalance);
        plan.SetThesis("Mua VNM vì EPS Q1 +22% YoY và ROE duy trì trên 20%, ngành sữa hồi phục");
        plan.SetInvalidationCriteria(new List<InvalidationRule>
        {
            new() { Trigger = InvalidationTrigger.EarningsMiss,
                    Detail = "BCTC Q1 EPS tăng trưởng dưới 20% YoY so với kỳ vọng" }
        });
        plan.MarkReady();
        return plan;
    }

    private static TradePlan CreateInProgressPlan()
    {
        var plan = CreateReadyPlan();
        plan.MarkInProgress();
        return plan;
    }

    private static TradePlan CreateExecutedPlan()
    {
        var plan = CreateInProgressPlan();
        plan.Execute("trade-1");
        return plan;
    }

    private static CampaignReviewData MakeReview() => new()
    {
        PnLAmount = 1_000_000m,
        PnLPercent = 1.25m,
        HoldingDays = 10,
        PnLPerDay = 100_000m,
        AnnualizedReturnPercent = 40m,
        TargetAchievementPercent = 10m,
        TotalInvested = 80_000_000m,
        TotalReturned = 81_000_000m,
        TotalFees = 100_000m,
        ReviewedAt = DateTime.UtcNow
    };

    // ---------------------------------------------------------------
    // Test #11 — từ Ready → Status=Cancelled, rule được append
    // ---------------------------------------------------------------
    [Fact]
    public void AbortWithThesisInvalidation_FromReady_ShouldCancelAndAppendRule()
    {
        var plan = CreateReadyPlan();
        var rulesBefore = plan.InvalidationCriteria!.Count;

        plan.AbortWithThesisInvalidation(
            InvalidationTrigger.EarningsMiss,
            "BCTC Q1 lỗ lần đầu tiên sau 13 năm, LN giảm > 80% YoY");

        plan.Status.Should().Be(TradePlanStatus.Cancelled);
        plan.InvalidationCriteria!.Count.Should().Be(rulesBefore + 1);
        var appended = plan.InvalidationCriteria!.Last();
        appended.Trigger.Should().Be(InvalidationTrigger.EarningsMiss);
        appended.IsTriggered.Should().BeTrue();
        appended.TriggeredAt.Should().NotBeNull();
        appended.TriggeredAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // ---------------------------------------------------------------
    // Test #12 — từ InProgress → rule appended, event raised
    // ---------------------------------------------------------------
    [Fact]
    public void AbortWithThesisInvalidation_FromInProgress_ShouldRaiseEvent()
    {
        var plan = CreateInProgressPlan();

        plan.AbortWithThesisInvalidation(
            InvalidationTrigger.NewsShock,
            "Chủ tịch bị khởi tố, cổ đông lớn bán phá giá trên sàn");

        plan.Status.Should().Be(TradePlanStatus.Cancelled);
        var evt = plan.DomainEvents.OfType<TradePlanThesisInvalidatedEvent>().SingleOrDefault();
        evt.Should().NotBeNull();
        evt!.Trigger.Should().Be(InvalidationTrigger.NewsShock);
        evt.TradePlanId.Should().Be(plan.Id);
    }

    // ---------------------------------------------------------------
    // Test #13 — từ Executed (multi-lot partial) → event raised, không throw (B3 fix)
    // ---------------------------------------------------------------
    [Fact]
    public void AbortWithThesisInvalidation_FromExecuted_ShouldRaiseEventNotThrow()
    {
        var plan = CreateExecutedPlan();

        var action = () => plan.AbortWithThesisInvalidation(
            InvalidationTrigger.TrendBreak,
            "Giá đóng cửa dưới MA200 kèm volume > 2× TB20, tín hiệu downtrend");

        action.Should().NotThrow();
        // Status stays Executed (position still open, exit handled by service layer)
        plan.Status.Should().Be(TradePlanStatus.Executed);
        var evt = plan.DomainEvents.OfType<TradePlanThesisInvalidatedEvent>().SingleOrDefault();
        evt.Should().NotBeNull();
        evt!.Trigger.Should().Be(InvalidationTrigger.TrendBreak);
        plan.InvalidationCriteria!.Last().IsTriggered.Should().BeTrue();
    }

    // ---------------------------------------------------------------
    // Test #14 — từ Reviewed/Cancelled → throw
    // ---------------------------------------------------------------
    [Fact]
    public void AbortWithThesisInvalidation_FromReviewed_ShouldThrow()
    {
        var plan = CreateExecutedPlan();
        plan.MarkReviewed(MakeReview());

        var action = () => plan.AbortWithThesisInvalidation(
            InvalidationTrigger.Manual,
            "Tự nhận thesis sai sau khi review lại danh mục cuối tháng");

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AbortWithThesisInvalidation_FromCancelled_ShouldThrow()
    {
        var plan = CreateReadyPlan();
        plan.Cancel();

        var action = () => plan.AbortWithThesisInvalidation(
            InvalidationTrigger.Manual,
            "Tự nhận thesis sai sau khi review lại danh mục cuối tháng");

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AbortWithThesisInvalidation_FromDraft_ShouldThrow()
    {
        // Draft doesn't qualify — use Cancel() instead
        var plan = new TradePlan("user-1", "VNM", "Buy", 80_000m, 75_000m, 90_000m, 100);

        var action = () => plan.AbortWithThesisInvalidation(
            InvalidationTrigger.Manual,
            "Tự nhận thesis sai trước khi chuyển sang Ready");

        action.Should().Throw<InvalidOperationException>();
    }

    // ---------------------------------------------------------------
    // Test #15 — Restore sau Abort clear IsTriggered
    // ---------------------------------------------------------------
    [Fact]
    public void Restore_AfterAbort_ShouldClearIsTriggeredOnAllRules()
    {
        var plan = CreateReadyPlan();
        plan.AbortWithThesisInvalidation(
            InvalidationTrigger.EarningsMiss,
            "BCTC Q1 EPS âm, lỗ lần đầu tiên sau 13 năm, LN giảm > 80% YoY");

        plan.Status.Should().Be(TradePlanStatus.Cancelled);
        plan.InvalidationCriteria!.Any(r => r.IsTriggered).Should().BeTrue();

        plan.Restore();

        plan.Status.Should().Be(TradePlanStatus.Draft);
        plan.InvalidationCriteria!.All(r => !r.IsTriggered).Should().BeTrue();
        plan.InvalidationCriteria!.All(r => r.TriggeredAt == null).Should().BeTrue();
        // Rule text still preserved
        plan.InvalidationCriteria!.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void AbortWithThesisInvalidation_NullDetail_ShouldThrow()
    {
        var plan = CreateReadyPlan();

        var actionNull = () => plan.AbortWithThesisInvalidation(InvalidationTrigger.Manual, null!);
        var actionEmpty = () => plan.AbortWithThesisInvalidation(InvalidationTrigger.Manual, "");

        actionNull.Should().Throw<ArgumentException>();
        actionEmpty.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AbortWithThesisInvalidation_ShortDetail_ShouldThrow()
    {
        // Plan spec §D4: abort detail phải ≥ 20 chars để đảm bảo falsifiable, tránh
        // "too short" placeholder — giống yêu cầu của InvalidationRule.Detail khi gate check.
        var plan = CreateReadyPlan();

        var actionShort = () => plan.AbortWithThesisInvalidation(
            InvalidationTrigger.Manual,
            "too short");  // 9 chars < 20

        actionShort.Should().Throw<ArgumentException>()
            .WithMessage("*20*");
    }

    [Fact]
    public void AbortWithThesisInvalidation_ExactlyTwentyChars_ShouldPass()
    {
        var plan = CreateReadyPlan();
        var detail = new string('a', 20);  // exactly 20 chars

        var action = () => plan.AbortWithThesisInvalidation(InvalidationTrigger.Manual, detail);

        action.Should().NotThrow();
    }

    [Fact]
    public void SetInvalidationCriteria_OnReviewedPlan_ShouldThrow()
    {
        var plan = CreateExecutedPlan();
        plan.MarkReviewed(MakeReview());

        var action = () => plan.SetInvalidationCriteria(new List<InvalidationRule>());

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SetExpectedReviewDate_ShouldStoreValue()
    {
        var plan = new TradePlan("user-1", "VNM", "Buy", 80_000m, 75_000m, 90_000m, 100);
        var date = DateTime.UtcNow.AddDays(30);

        plan.SetExpectedReviewDate(date);

        plan.ExpectedReviewDate.Should().Be(date);
    }
}
