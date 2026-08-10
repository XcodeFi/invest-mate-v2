using System.Reflection;
using FluentAssertions;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.CompanyDossiers.Queries.GetDossiersNeedingReview;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.CompanyDossiers;

/// <summary>
/// Cổng hồ sơ chỉ bắn lúc người dùng lập kế hoạch — nghĩa là một hồ sơ hết hạn chỉ lộ ra đúng lúc
/// người ta đang muốn mua, tức lúc tệ nhất để phải ngồi đọc lại. Danh sách này là đường duy nhất
/// biết trước, nên thứ tự phải đưa cái sắp chặn mình lên đầu.
/// </summary>
public class GetDossiersNeedingReviewQueryTests
{
    private readonly Mock<ICompanyDossierRepository> _repo = new();

    private GetDossiersNeedingReviewQueryHandler Sut() => new(_repo.Object);

    private static CompanyDossier Build(string symbol, bool confirmed, int ageDays)
    {
        var d = new CompanyDossier("user-1", symbol,
            "Bán thép xây dựng và HRC cho nhà thầu nội địa",
            new List<MoatItem> { new() { Description = "Chi phí đơn vị thấp nhất ngành nội địa" } },
            new List<RiskFactor>
            {
                new() { Rank = 1, Description = "Giá HRC", ObservableSignal = "Giá HRC giảm quá 10% một tháng" }
            });
        if (confirmed) d.Confirm();
        typeof(CompanyDossier)
            .GetProperty(nameof(CompanyDossier.ReviewedAt))!
            .SetValue(d, DateTime.UtcNow.AddDays(-ageDays));
        return d;
    }

    private static CompanyDossier Aged(string symbol, int days) => Build(symbol, confirmed: true, days);
    private static CompanyDossier Unconfirmed(string symbol) => Build(symbol, confirmed: false, 1);

    private Task<List<DossierReviewItemDto>> Handle() =>
        Sut().Handle(new GetDossiersNeedingReviewQuery { UserId = "user-1" }, default);

    [Fact]
    public async Task ShouldReturnExpiredFirstThenNeedsReview_SortedByOverdueDesc()
    {
        _repo.Setup(r => r.GetByUserIdAsync("user-1")).ReturnsAsync(new List<CompanyDossier>
        {
            Aged("AAA", days: 95),   // NeedsReview, quá hạn 5
            Aged("BBB", days: 200),  // Expired, quá hạn 110
            Aged("CCC", days: 150),  // NeedsReview, quá hạn 60
            Aged("DDD", days: 300),  // Expired, quá hạn 210
            Aged("EEE", days: 10),   // Fresh — không được xuất hiện
        });

        var result = await Handle();

        result.Select(r => r.Symbol).Should().Equal("DDD", "BBB", "CCC", "AAA");
    }

    [Fact]
    public async Task FreshDossier_IsExcluded()
    {
        // Tách khỏi test thứ tự: nếu chỉ kiểm thứ tự thì một bug lọc sai vẫn có thể lọt khi Fresh
        // tình cờ xếp cuối.
        _repo.Setup(r => r.GetByUserIdAsync("user-1")).ReturnsAsync(new List<CompanyDossier>
        {
            Aged("EEE", days: 10)
        });

        var result = await Handle();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task UnconfirmedDossier_ShouldAppearInList()
    {
        // Hồ sơ đã viết mà chưa ai ký thì cổng coi như KHÔNG có hồ sơ — nó phải nằm trong danh sách
        // nhắc, nếu không người dùng tưởng viết xong là xong.
        _repo.Setup(r => r.GetByUserIdAsync("user-1")).ReturnsAsync(new List<CompanyDossier>
        {
            Unconfirmed("FFF")
        });

        var result = await Handle();

        result.Should().ContainSingle().Which.Freshness.Should().Be("Unconfirmed");
    }

    [Fact]
    public async Task Unconfirmed_RanksBetweenExpiredAndNeedsReview()
    {
        _repo.Setup(r => r.GetByUserIdAsync("user-1")).ReturnsAsync(new List<CompanyDossier>
        {
            Aged("NEEDS", days: 95),
            Unconfirmed("UNC"),
            Aged("EXP", days: 300),
        });

        var result = await Handle();

        result.Select(r => r.Symbol).Should().Equal("EXP", "UNC", "NEEDS");
    }

    [Fact]
    public async Task DaysOverdue_CountsFromTheNinetyDayMark_NotFromReviewDate()
    {
        _repo.Setup(r => r.GetByUserIdAsync("user-1")).ReturnsAsync(new List<CompanyDossier>
        {
            Aged("AAA", days: 95)
        });

        var result = await Handle();

        result[0].DaysOverdue.Should().Be(5);
    }

    [Fact]
    public async Task Unconfirmed_HasNoOverdueCount()
    {
        // Chưa ký thì đồng hồ hạn tươi chưa chạy — hiện một con số "quá hạn" ở đây là bịa.
        _repo.Setup(r => r.GetByUserIdAsync("user-1")).ReturnsAsync(new List<CompanyDossier>
        {
            Unconfirmed("FFF")
        });

        var result = await Handle();

        result[0].DaysOverdue.Should().Be(0);
    }

    [Fact]
    public async Task NoDossiers_ReturnsEmptyList()
    {
        _repo.Setup(r => r.GetByUserIdAsync("user-1")).ReturnsAsync(new List<CompanyDossier>());

        var result = await Handle();

        result.Should().BeEmpty();
    }
}
