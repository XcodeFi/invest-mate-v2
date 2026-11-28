using FluentAssertions;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.Events;

namespace InvestmentApp.Domain.Tests.Entities;

public class TradePlanReviewTests
{
    #region Helpers

    private static TradePlan CreateDefaultPlan(
        string userId = "user-1",
        decimal entryPrice = 80_000m,
        decimal target = 90_000m)
    {
        return new TradePlan(userId, "VNM", "Buy",
            entryPrice, 75_000m, target, 100,
            thesis: "Mua mẫu cho test backward compat — luận điểm đủ dài tối thiểu");
    }

    private static TradePlan CreateExecutedPlan(string userId = "user-1")
    {
        var plan = CreateDefaultPlan(userId);
        plan.MarkReady();
        plan.MarkInProgress();
        plan.Execute("trade-1");
        return plan;
    }

    private static CampaignReviewData CreateReviewData(
        decimal pnlAmount = 8_000_000m,
        decimal pnlPercent = 10m,
        int holdingDays = 30,
        string? lessons = null)
    {
        return new CampaignReviewData
        {
            PnLAmount = pnlAmount,
            PnLPercent = pnlPercent,
            HoldingDays = holdingDays,
            PnLPerDay = holdingDays > 0 ? pnlAmount / holdingDays : 0,
            AnnualizedReturnPercent = 121.67m,
            TargetAchievementPercent = 80m,
            TotalInvested = 80_000_000m,
            TotalReturned = 88_000_000m,
            TotalFees = 200_000m,
            LessonsLearned = lessons,
            ReviewedAt = DateTime.UtcNow
        };
    }

    #endregion

    // =====================================================================
    // RecomputeReview ("Tính lại review" do người dùng bấm)
    // =====================================================================

    private static TradePlan ReviewedWith(string lessons, DateTime reviewedAt)
    {
        var plan = CreateExecutedPlan();
        var data = CreateReviewData(pnlAmount: -13_500_000m, pnlPercent: -45m, lessons: lessons);
        data.ReviewedAt = reviewedAt;
        data.RealizedR = -4.5m;
        plan.MarkReviewed(data);
        return plan;
    }

    [Fact]
    public void recompute_keeps_user_text_and_original_review_date_and_stores_legacy()
    {
        var reviewedAt = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var plan = ReviewedWith("Bài học của tôi", reviewedAt);
        plan.ClearDomainEvents();

        plan.RecomputeReview(new CampaignReviewData { PnLAmount = 1_500_000m, PnLPercent = 10m, RealizedR = 1.0m, LessonsLearned = "bị ghi đè?" });

        plan.ReviewData!.PnLAmount.Should().Be(1_500_000m);
        plan.ReviewData.RealizedR.Should().Be(1.0m);
        plan.ReviewData.LessonsLearned.Should().Be("Bài học của tôi");
        plan.ReviewData.ReviewedAt.Should().Be(reviewedAt);
        plan.Status.Should().Be(TradePlanStatus.Reviewed);
        plan.ReviewData.Legacy.Should().NotBeNull();
        plan.ReviewData.Legacy!.PnLAmount.Should().Be(-13_500_000m);
        plan.ReviewData.Legacy.PnLPercent.Should().Be(-45m);
        plan.ReviewData.Legacy.RealizedR.Should().Be(-4.5m);
        plan.DomainEvents.Should().BeEmpty("không phát lại PlanReviewedEvent");
    }

    [Fact]
    public void recompute_legacy_snapshot_keeps_null_r_of_a_new_format_review_not_a_fake_zero()
    {
        // Review mới tính, còn mở, chưa bán gì: RealizedR lưu null. Bản lưu cũ phải giữ null, không suy ra 0R.
        var plan = CreateExecutedPlan();
        var data = CreateReviewData(pnlAmount: 0m, pnlPercent: 0m, lessons: "x");
        data.RealizedR = null;
        data.OpenQuantity = 100m;
        plan.MarkReviewed(data);

        plan.RecomputeReview(new CampaignReviewData { PnLAmount = 0m, PnLPercent = 0m, RealizedR = null, OpenQuantity = 100m });

        plan.ReviewData!.Legacy!.RealizedR.Should().BeNull();
    }

    [Fact]
    public void recompute_stores_legacy_once()
    {
        var plan = ReviewedWith("x", DateTime.UtcNow);
        plan.RecomputeReview(new CampaignReviewData { PnLAmount = 1_500_000m, PnLPercent = 10m, RealizedR = 1.0m });

        plan.RecomputeReview(new CampaignReviewData { PnLAmount = 2_000_000m, PnLPercent = 13m, RealizedR = 1.3m });

        plan.ReviewData!.PnLAmount.Should().Be(2_000_000m);
        plan.ReviewData.Legacy!.PnLAmount.Should().Be(-13_500_000m, "bản ghi đầu tiên không bị ghi đè");
        plan.ReviewData.Legacy.RealizedR.Should().Be(-4.5m);
    }

    [Fact]
    public void recompute_fails_for_a_plan_that_is_not_reviewed()
    {
        var plan = CreateExecutedPlan();

        var act = () => plan.RecomputeReview(new CampaignReviewData());

        act.Should().Throw<InvalidOperationException>();
    }

    // =====================================================================
    // MarkReviewed with CampaignReviewData
    // =====================================================================

    [Fact]
    public void MarkReviewed_WithData_ShouldSetReviewDataAndStatus()
    {
        var plan = CreateExecutedPlan();
        var reviewData = CreateReviewData(pnlAmount: 5_000_000m, pnlPercent: 6.25m);

        plan.MarkReviewed(reviewData);

        plan.Status.Should().Be(TradePlanStatus.Reviewed);
        plan.ReviewData.Should().NotBeNull();
        plan.ReviewData!.PnLAmount.Should().Be(5_000_000m);
        plan.ReviewData.PnLPercent.Should().Be(6.25m);
        plan.ReviewData.HoldingDays.Should().Be(30);
        plan.ReviewData.TotalInvested.Should().Be(80_000_000m);
        plan.ReviewData.TotalReturned.Should().Be(88_000_000m);
    }

    [Fact]
    public void MarkReviewed_WhenNotExecuted_ShouldThrow()
    {
        var plan = CreateDefaultPlan();
        var reviewData = CreateReviewData();

        var action = () => plan.MarkReviewed(reviewData);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*executed*");
    }

    [Fact]
    public void MarkReviewed_NullData_ShouldThrow()
    {
        var plan = CreateExecutedPlan();

        var action = () => plan.MarkReviewed(null!);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("reviewData");
    }

    [Fact]
    public void MarkReviewed_ShouldRaisePlanReviewedEvent()
    {
        var plan = CreateExecutedPlan();
        var reviewData = CreateReviewData(pnlPercent: 15.5m);

        plan.MarkReviewed(reviewData);

        plan.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PlanReviewedEvent>()
            .Which.PnLPercent.Should().Be(15.5m);
    }

    [Fact]
    public void MarkReviewed_ShouldPreserveLessonsLearned()
    {
        var plan = CreateExecutedPlan();
        var reviewData = CreateReviewData(lessons: "Nên set trailing stop thay vì fixed target");

        plan.MarkReviewed(reviewData);

        plan.ReviewData!.LessonsLearned.Should().Be("Nên set trailing stop thay vì fixed target");
    }

    [Fact]
    public void MarkReviewed_WithNegativePnL_ShouldAccept()
    {
        var plan = CreateExecutedPlan();
        var reviewData = CreateReviewData(pnlAmount: -3_000_000m, pnlPercent: -3.75m);

        plan.MarkReviewed(reviewData);

        plan.ReviewData!.PnLAmount.Should().Be(-3_000_000m);
        plan.ReviewData.PnLPercent.Should().Be(-3.75m);
    }

    // =====================================================================
    // UpdateReviewLessons
    // =====================================================================

    [Fact]
    public void UpdateReviewLessons_OnReviewedPlan_ShouldUpdateLessons()
    {
        var plan = CreateExecutedPlan();
        plan.MarkReviewed(CreateReviewData());

        plan.UpdateReviewLessons("Bán sớm quá, lần sau dùng trailing stop");

        plan.ReviewData!.LessonsLearned.Should().Be("Bán sớm quá, lần sau dùng trailing stop");
    }

    [Fact]
    public void UpdateReviewLessons_OnNonReviewedPlan_ShouldThrow()
    {
        var plan = CreateExecutedPlan();

        var action = () => plan.UpdateReviewLessons("test");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*reviewed*");
    }

    [Fact]
    public void UpdateReviewLessons_ShouldIncrementVersion()
    {
        var plan = CreateExecutedPlan();
        plan.MarkReviewed(CreateReviewData());
        var vBefore = plan.Version;

        plan.UpdateReviewLessons("updated");

        plan.Version.Should().Be(vBefore + 1);
    }

    // =====================================================================
    // TimeHorizon
    // =====================================================================

    [Fact]
    public void Constructor_WithTimeHorizon_ShouldSetValue()
    {
        var plan = new TradePlan("user-1", "VNM", "Buy",
            80_000m, 75_000m, 90_000m, 100,
            timeHorizon: TimeHorizon.ShortTerm);

        plan.TimeHorizon.Should().Be(TimeHorizon.ShortTerm);
    }

    [Fact]
    public void Constructor_WithoutTimeHorizon_ShouldBeNull()
    {
        var plan = CreateDefaultPlan();

        plan.TimeHorizon.Should().BeNull();
    }

    [Fact]
    public void SetTimeHorizon_OnDraft_ShouldSet()
    {
        var plan = CreateDefaultPlan();

        plan.SetTimeHorizon(TimeHorizon.MediumTerm);

        plan.TimeHorizon.Should().Be(TimeHorizon.MediumTerm);
    }

    [Fact]
    public void SetTimeHorizon_OnExecuted_ShouldSucceed()
    {
        var plan = CreateExecutedPlan();

        plan.SetTimeHorizon(TimeHorizon.LongTerm);

        plan.TimeHorizon.Should().Be(TimeHorizon.LongTerm);
    }

    [Fact]
    public void SetTimeHorizon_OnReviewed_ShouldThrow()
    {
        var plan = CreateExecutedPlan();
        plan.MarkReviewed(CreateReviewData());

        var action = () => plan.SetTimeHorizon(TimeHorizon.ShortTerm);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*reviewed*");
    }

    [Fact]
    public void SetTimeHorizon_ShouldIncrementVersion()
    {
        var plan = CreateDefaultPlan();
        var vBefore = plan.Version;

        plan.SetTimeHorizon(TimeHorizon.ShortTerm);

        plan.Version.Should().Be(vBefore + 1);
    }

    [Fact]
    public void Update_WithTimeHorizon_ShouldSetValue()
    {
        var plan = CreateDefaultPlan();

        plan.Update(timeHorizon: TimeHorizon.LongTerm);

        plan.TimeHorizon.Should().Be(TimeHorizon.LongTerm);
    }

    [Theory]
    [InlineData(TimeHorizon.ShortTerm)]
    [InlineData(TimeHorizon.MediumTerm)]
    [InlineData(TimeHorizon.LongTerm)]
    public void SetTimeHorizon_AllValues_ShouldWork(TimeHorizon horizon)
    {
        var plan = CreateDefaultPlan();

        plan.SetTimeHorizon(horizon);

        plan.TimeHorizon.Should().Be(horizon);
    }
}
