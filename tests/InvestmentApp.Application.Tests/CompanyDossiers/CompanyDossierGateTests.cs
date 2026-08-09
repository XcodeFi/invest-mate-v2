using FluentAssertions;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.CompanyDossiers.Gate;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.CompanyDossiers;

public class CompanyDossierGateTests
{
    private readonly Mock<ICompanyDossierRepository> _repo = new();

    private CompanyDossierGate Sut() => new(_repo.Object);

    private static CompanyDossier Dossier(
        string businessModel = "Bán thép xây dựng và HRC cho nhà thầu nội địa toàn quốc",
        int moatLength = 40,
        int riskCount = 3,
        int signalLength = 30,
        bool confirmed = true,
        int ageDays = 0)
    {
        var risks = Enumerable.Range(1, riskCount).Select(i => new RiskFactor
        {
            Rank = i,
            Description = $"Rủi ro số {i}",
            ObservableSignal = new string('x', signalLength)
        }).ToList();

        var d = new CompanyDossier("user-1", "HPG", businessModel,
            new List<MoatItem> { new() { Description = new string('m', moatLength) } },
            risks);

        if (confirmed) d.Confirm();
        if (ageDays > 0) typeof(CompanyDossier)
            .GetProperty(nameof(CompanyDossier.ReviewedAt))!
            .SetValue(d, DateTime.UtcNow.AddDays(-ageDays));
        return d;
    }

    private void Setup(CompanyDossier? dossier)
        => _repo.Setup(r => r.GetAsync("user-1", "HPG")).ReturnsAsync(dossier);

    // planSize 12_000_000 trên account 100_000_000 = 12% → tầng lớn
    private const decimal LargeSize = 12_000_000m;
    private const decimal SmallSize = 2_000_000m;
    private const decimal Account = 100_000_000m;

    [Fact]
    public async Task NoDossier_ShouldReturnMissing()
    {
        Setup(null);
        var result = await Sut().EvaluateAsync("user-1", "HPG", SmallSize, Account, default);

        result.Passed.Should().BeFalse();
        result.Reason.Should().Be("missing");
    }

    [Fact]
    public async Task Unconfirmed_ShouldReturnUnconfirmed()
    {
        Setup(Dossier(confirmed: false));
        var result = await Sut().EvaluateAsync("user-1", "HPG", SmallSize, Account, default);

        result.Reason.Should().Be("unconfirmed");
    }

    [Fact]
    public async Task Expired_ShouldReturnExpired()
    {
        Setup(Dossier(ageDays: 200));
        var result = await Sut().EvaluateAsync("user-1", "HPG", SmallSize, Account, default);

        result.Reason.Should().Be("expired");
    }

    [Fact]
    public async Task AgentUpsertedAfterSigning_ShouldReturnUnconfirmed()
    {
        var d = Dossier();
        d.UpdateByAgent("Agent vừa viết lại mô hình kinh doanh của doanh nghiệp",
            d.Moats.ToList(), d.RiskFactors.ToList(), null);
        Setup(d);

        var result = await Sut().EvaluateAsync("user-1", "HPG", SmallSize, Account, default);

        result.Reason.Should().Be("unconfirmed");
    }

    [Fact]
    public async Task OwnerUpdatedAfterSigning_ShouldStillPass()
    {
        var d = Dossier();
        d.UpdateByOwner("Người dùng tự sửa lại mô hình kinh doanh cho rõ hơn",
            d.Moats.ToList(), d.RiskFactors.ToList(), null);
        Setup(d);

        var result = await Sut().EvaluateAsync("user-1", "HPG", SmallSize, Account, default);

        result.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task SmallTier_MinimalContent_ShouldPass()
    {
        Setup(Dossier(businessModel: "Bán thép", moatLength: 5, riskCount: 1, signalLength: 10));
        var result = await Sut().EvaluateAsync("user-1", "HPG", SmallSize, Account, default);

        result.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task SmallTier_EmptyBusinessModel_ShouldBlock()
    {
        Setup(Dossier(businessModel: "   ", riskCount: 1));
        var result = await Sut().EvaluateAsync("user-1", "HPG", SmallSize, Account, default);

        result.Passed.Should().BeFalse();
        result.Reason.Should().Be("insufficient");
        result.Missing.Should().Contain(m => m.Contains("businessModel"));
    }

    [Fact]
    public async Task LargeTier_TwoRiskFactors_ShouldBlockWithCounts()
    {
        Setup(Dossier(riskCount: 2));
        var result = await Sut().EvaluateAsync("user-1", "HPG", LargeSize, Account, default);

        result.Passed.Should().BeFalse();
        result.Missing.Should().Contain("riskFactors: cần ≥ 3, đang có 2");
    }

    [Fact]
    public async Task LargeTier_ShortObservableSignal_ShouldBlock()
    {
        Setup(Dossier(signalLength: 19));
        var result = await Sut().EvaluateAsync("user-1", "HPG", LargeSize, Account, default);

        result.Passed.Should().BeFalse();
        result.Missing.Should().Contain(m => m.Contains("observableSignal"));
    }

    [Fact]
    public async Task NullAccountBalance_ShouldUseSmallTier()
    {
        Setup(Dossier(businessModel: "Bán thép", moatLength: 5, riskCount: 1, signalLength: 10));
        var result = await Sut().EvaluateAsync("user-1", "HPG", LargeSize, null, default);

        result.Passed.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1_000_000)]
    public async Task NonPositiveAccountBalance_ShouldUseSmallTier(decimal balance)
    {
        // Khớp guard `AccountBalance.Value > 0m` của TradePlan.EnsureDisciplineGate:179.
        // Thiếu guard này thì threshold = 0 và mọi lệnh rơi vào tầng lớn.
        Setup(Dossier(businessModel: "Bán thép", moatLength: 5, riskCount: 1, signalLength: 10));
        var result = await Sut().EvaluateAsync("user-1", "HPG", LargeSize, balance, default);

        result.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task LargeTier_VietnameseBusinessModelOf30Chars_ShouldPass()
    {
        // 30 ký tự có dấu — phải đếm bằng ký tự, không lệch
        const string vn = "Bán thép xây dựng cho nhà thầu";
        vn.Length.Should().Be(30);
        Setup(Dossier(businessModel: vn));

        var result = await Sut().EvaluateAsync("user-1", "HPG", LargeSize, Account, default);

        result.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task ExpiredAndThin_ShouldReportExpiredNotInsufficient()
    {
        // Ghim thứ tự ưu tiên. Code hiện đúng vì switch freshness return trước
        // mọi kiểm tra nội dung, nhưng không có test thì refactor đảo thứ tự sẽ lặng lẽ hồi quy.
        Setup(Dossier(ageDays: 200, riskCount: 1, businessModel: "Ngắn"));
        var result = await Sut().EvaluateAsync("user-1", "HPG", LargeSize, Account, default);

        result.Reason.Should().Be("expired");
    }

    [Fact]
    public async Task SizeExactlyAtFivePercent_ShouldUseLargeTier()
    {
        // Ghim `>=` chứ không phải `>`. Không có test ở đúng mốc thì off-by-one
        // ở biên ngưỡng lọt qua toàn bộ suite.
        Setup(Dossier(businessModel: "Bán thép", moatLength: 5, riskCount: 1, signalLength: 10));
        var result = await Sut().EvaluateAsync("user-1", "HPG", Account * 0.05m, Account, default);

        result.Passed.Should().BeFalse();
        result.Reason.Should().Be("insufficient");
    }

    [Fact]
    public async Task NeedsReview_ShouldStillPassTheGate()
    {
        // 90–179 ngày chỉ nhắc, không chặn. Chỉ Expired mới chặn.
        Setup(Dossier(ageDays: 120));
        var result = await Sut().EvaluateAsync("user-1", "HPG", LargeSize, Account, default);

        result.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task LargeTier_ShortMoat_ShouldReportCurrentLongestLength()
    {
        Setup(Dossier(moatLength: 12));
        var result = await Sut().EvaluateAsync("user-1", "HPG", LargeSize, Account, default);

        result.Missing.Should().Contain(m => m.Contains("moats") && m.Contains("12"));
    }

    [Fact]
    public async Task LargeTier_ShortSignal_ShouldReportCurrentLength()
    {
        Setup(Dossier(signalLength: 19));
        var result = await Sut().EvaluateAsync("user-1", "HPG", LargeSize, Account, default);

        result.Missing.Should().Contain(m => m.Contains("observableSignal") && m.Contains("19 ký tự"));
    }

    [Fact]
    public async Task EnsureAsync_WhenBlocked_ShouldThrowWithPayload()
    {
        Setup(null);
        var act = () => Sut().EnsureAsync("user-1", "HPG", SmallSize, Account, default);

        var ex = (await act.Should().ThrowAsync<DossierGateException>()).Which;
        ex.Symbol.Should().Be("HPG");
        ex.Result.Reason.Should().Be("missing");
    }
}
