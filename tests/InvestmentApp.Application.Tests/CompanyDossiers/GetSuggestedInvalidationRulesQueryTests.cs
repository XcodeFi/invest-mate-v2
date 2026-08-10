using FluentAssertions;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.CompanyDossiers.Queries.GetSuggestedInvalidationRules;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.CompanyDossiers;

/// <summary>
/// Hồ sơ đã buộc người dùng viết "rủi ro nào + biết nó đang xảy ra bằng dấu hiệu gì". Đó chính là
/// nguyên liệu của `InvalidationRule` trên trade plan, nên bắt gõ lại lần thứ hai là chỗ duy nhất
/// tính năng này trả lại thời gian. Đề xuất, KHÔNG tự áp: người dùng tick mới vào plan.
/// </summary>
public class GetSuggestedInvalidationRulesQueryTests
{
    private readonly Mock<ICompanyDossierRepository> _repo = new();

    private GetSuggestedInvalidationRulesQueryHandler Sut() => new(_repo.Object);

    private static CompanyDossier Dossier(List<RiskFactor> risks) =>
        new("user-1", "HPG", "Bán thép xây dựng và HRC cho nhà thầu nội địa",
            new List<MoatItem> { new() { Description = "Chi phí đơn vị thấp nhất ngành nội địa" } },
            risks);

    private Task<List<SuggestedInvalidationRuleDto>> Handle() =>
        Sut().Handle(new GetSuggestedInvalidationRulesQuery { UserId = "user-1", Symbol = "HPG" }, default);

    [Fact]
    public async Task ShouldReturnTopThreeByRankWithComposedDetail()
    {
        var risks = Enumerable.Range(1, 5).Select(i => new RiskFactor
        {
            Rank = i,
            Description = $"Rủi ro {i}",
            ObservableSignal = $"Dấu hiệu quan sát được số {i}",
            SuggestedTrigger = i == 1 ? InvalidationTrigger.EarningsMiss : null
        }).ToList();
        _repo.Setup(r => r.GetAsync("user-1", "HPG")).ReturnsAsync(Dossier(risks));

        var result = await Handle();

        result.Should().HaveCount(3);
        result[0].Trigger.Should().Be(InvalidationTrigger.EarningsMiss);
        // Không có kịch bản gợi ý thì về Manual — không bỏ rủi ro đó đi.
        result[1].Trigger.Should().Be(InvalidationTrigger.Manual);
        result[0].Detail.Should().Be("Rủi ro 1 — dấu hiệu: Dấu hiệu quan sát được số 1");
        result.Select(r => r.SourceRank).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task ShortDetail_ShouldBeFlaggedNotDropped()
    {
        // Gate kỷ luật đòi Detail ≥ 20 ký tự. Tầng nhỏ cho phép dấu hiệu ngắn, nên đề xuất có thể
        // không đạt — vẫn phải trả về để người dùng bổ sung, chứ không lặng lẽ tạo một rule sẽ bị
        // từ chối lúc lưu plan.
        _repo.Setup(r => r.GetAsync("user-1", "HPG")).ReturnsAsync(Dossier(new List<RiskFactor>
        {
            new() { Rank = 1, Description = "A", ObservableSignal = "B" }
        }));

        var result = await Handle();

        result.Should().HaveCount(1);
        result[0].MeetsMinLength.Should().BeFalse();
    }

    [Fact]
    public async Task DetailAtExactlyTwentyChars_MeetsMinLength()
    {
        // Ngưỡng là >= 20, không phải > 20. Lệch một ký tự ở đây là chặn oan đúng ca sát ngưỡng.
        _repo.Setup(r => r.GetAsync("user-1", "HPG")).ReturnsAsync(Dossier(new List<RiskFactor>
        {
            new() { Rank = 1, Description = "Giá thép", ObservableSignal = "A" }   // 8 + 12 + 1 = 21
        }));

        var result = await Handle();

        result[0].Detail.Length.Should().BeGreaterThanOrEqualTo(20);
        result[0].MeetsMinLength.Should().BeTrue();
    }

    [Fact]
    public async Task NoDossier_ShouldReturnEmptyList()
    {
        _repo.Setup(r => r.GetAsync("user-1", "HPG")).ReturnsAsync((CompanyDossier?)null);

        var result = await Handle();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ShouldNormalizeSymbolBeforeLookup()
    {
        _repo.Setup(r => r.GetAsync("user-1", "HPG")).ReturnsAsync(Dossier(new List<RiskFactor>
        {
            new() { Rank = 1, Description = "Giá thép giảm sâu", ObservableSignal = "HRC giảm quá 10% một tháng" }
        }));

        var result = await Sut().Handle(
            new GetSuggestedInvalidationRulesQuery { UserId = "user-1", Symbol = " hpg " }, default);

        result.Should().HaveCount(1);
        _repo.Verify(r => r.GetAsync("user-1", "HPG"), Times.Once);
    }

    [Fact]
    public async Task DossierWithoutRiskFactors_ReturnsEmpty_NotAnEntryWithBlankDetail()
    {
        _repo.Setup(r => r.GetAsync("user-1", "HPG")).ReturnsAsync(Dossier(new List<RiskFactor>()));

        var result = await Handle();

        result.Should().BeEmpty();
    }
}
