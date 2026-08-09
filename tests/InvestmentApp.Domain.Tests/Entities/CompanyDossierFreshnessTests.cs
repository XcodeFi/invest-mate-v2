using FluentAssertions;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Domain.Tests.Entities;

public class CompanyDossierFreshnessTests
{
    private static CompanyDossier Confirmed()
    {
        var dossier = new CompanyDossier("user-1", "HPG", "Bán thép xây dựng cho nhà thầu",
            new List<MoatItem> { new() { Description = "Chi phí đơn vị thấp nhất ngành" } },
            new List<RiskFactor>
            {
                new() { Rank = 1, Description = "Giá HRC Trung Quốc", ObservableSignal = "Giá HRC giảm quá 10% trong 1 tháng" }
            });
        dossier.Confirm();
        return dossier;
    }

    [Fact]
    public void Unconfirmed_ShouldBeUnconfirmedRegardlessOfReviewedAt()
    {
        var dossier = new CompanyDossier("user-1", "HPG", "abc", new(), new());
        dossier.GetFreshness(DateTime.UtcNow).Should().Be(DossierFreshness.Unconfirmed);
    }

    [Theory]
    [InlineData(0, DossierFreshness.Fresh)]
    [InlineData(89, DossierFreshness.Fresh)]
    [InlineData(90, DossierFreshness.NeedsReview)]
    [InlineData(179, DossierFreshness.NeedsReview)]
    [InlineData(180, DossierFreshness.Expired)]
    [InlineData(400, DossierFreshness.Expired)]
    public void GetFreshness_ShouldFollowDayBoundaries(int daysElapsed, DossierFreshness expected)
    {
        var dossier = Confirmed();
        var now = dossier.ReviewedAt.AddDays(daysElapsed);

        dossier.GetFreshness(now).Should().Be(expected);
    }

    [Fact]
    public void GetFreshness_ShouldUseVietnamCalendarDay()
    {
        // Ký lúc 18:00 UTC ngày 1 = 01:00 VN ngày 2. 89 ngày VN sau vẫn Fresh,
        // trong khi so sánh thuần UTC sẽ ra 89.x ngày và dễ lệch một ngày.
        var dossier = Confirmed();
        var reviewedVnDate = dossier.ReviewedAt.AddHours(7).Date;
        var nowUtc = reviewedVnDate.AddDays(89).AddHours(-7).AddHours(20);

        dossier.GetFreshness(nowUtc).Should().Be(DossierFreshness.Fresh);
    }

    [Fact]
    public void Confirm_OnExpiredDossier_ShouldReturnToFresh()
    {
        var dossier = Confirmed();
        var later = dossier.ReviewedAt.AddDays(200);
        dossier.GetFreshness(later).Should().Be(DossierFreshness.Expired);

        dossier.Confirm();

        dossier.GetFreshness(DateTime.UtcNow).Should().Be(DossierFreshness.Fresh);
    }
}
