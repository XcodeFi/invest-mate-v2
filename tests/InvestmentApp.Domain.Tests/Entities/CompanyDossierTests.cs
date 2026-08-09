using FluentAssertions;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Domain.Tests.Entities;

public class CompanyDossierTests
{
    private static RiskFactor Risk(int rank, string signal = "Biên gộp 2 quý liên tiếp giảm hơn 3 điểm",
        bool dealBreaker = false)
        => new() { Rank = rank, Description = $"Rủi ro {rank}", ObservableSignal = signal, IsDealBreaker = dealBreaker };

    private static CompanyDossier Create(
        string businessModel = "Bán thép xây dựng và HRC cho nhà thầu nội địa",
        List<RiskFactor>? risks = null)
        => new("user-1", " hpg ", businessModel,
            new List<MoatItem> { new() { Description = "Lò cao quy mô lớn nhất nội địa, chi phí đơn vị thấp" } },
            risks ?? new List<RiskFactor> { Risk(1) });

    [Fact]
    public void Ctor_ShouldNormalizeSymbol()
        => Create().Symbol.Should().Be("HPG");

    [Fact]
    public void Ctor_EmptySymbol_ShouldThrow()
    {
        var action = () => new CompanyDossier("user-1", "   ", "abc", new(), new());
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_RiskFactorWithoutObservableSignal_ShouldThrow()
    {
        var action = () => Create(risks: new List<RiskFactor> { Risk(1, signal: "  ") });
        action.Should().Throw<ArgumentException>()
            .WithMessage("*dấu hiệu*");
    }

    [Fact]
    public void Ctor_TwoDealBreakers_ShouldThrow()
    {
        var action = () => Create(risks: new List<RiskFactor>
        {
            Risk(1, dealBreaker: true),
            Risk(2, dealBreaker: true)
        });
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*hủy diệt*");
    }

    [Fact]
    public void Ctor_SparseRanks_ShouldBeDensifiedByOrder()
    {
        var dossier = Create(risks: new List<RiskFactor> { Risk(9), Risk(3), Risk(7) });

        dossier.RiskFactors.Select(r => r.Rank).Should().Equal(1, 2, 3);
        dossier.RiskFactors.Select(r => r.Description).Should().Equal("Rủi ro 3", "Rủi ro 7", "Rủi ro 9");
    }

    [Fact]
    public void UpdateByAgent_ShouldClearConfirmation()
    {
        var dossier = Create();
        dossier.Confirm();

        dossier.UpdateByAgent("Mô hình mới do agent viết lại", dossier.Moats.ToList(),
            dossier.RiskFactors.ToList(), null);

        dossier.ConfirmedAt.Should().BeNull();
        dossier.AgentDraftedAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdateByOwner_ShouldKeepConfirmation()
    {
        var dossier = Create();
        dossier.Confirm();
        var signedAt = dossier.ConfirmedAt;

        dossier.UpdateByOwner("Người dùng tự sửa lại mô hình kinh doanh", dossier.Moats.ToList(),
            dossier.RiskFactors.ToList(), null);

        dossier.ConfirmedAt.Should().Be(signedAt);
        dossier.AgentDraftedAt.Should().BeNull();
    }

    [Fact]
    public void UpdateByOwner_ShouldNotPushTheFreshnessClock()
    {
        // Chỉ Confirm() đẩy đồng hồ. Nếu sửa nội dung cũng đẩy thì hồ sơ đã
        // hết hạn chỉ cần sửa một ký tự ở ô ghi chú là hồi sinh mà không ai đọc tin mới.
        var dossier = Create();
        dossier.Confirm();
        var reviewedAt = dossier.ReviewedAt;

        dossier.UpdateByOwner("Người dùng sửa lại đúng một chỗ nhỏ", dossier.Moats.ToList(),
            dossier.RiskFactors.ToList(), "thêm ghi chú");

        dossier.ReviewedAt.Should().Be(reviewedAt);
    }

    [Fact]
    public void UpdateByOwner_OnExpiredDossier_ShouldStayExpiredUntilSigned()
    {
        var dossier = Create();
        dossier.Confirm();
        var now = dossier.ReviewedAt.AddDays(200);
        dossier.GetFreshness(now).Should().Be(DossierFreshness.Expired);

        dossier.UpdateByOwner("Sửa nội dung nhưng chưa ký lại", dossier.Moats.ToList(),
            dossier.RiskFactors.ToList(), null);

        dossier.GetFreshness(now).Should().Be(DossierFreshness.Expired);
    }

    [Fact]
    public void UpdateByAgent_ShouldNotPushTheFreshnessClock()
    {
        var dossier = Create();
        dossier.Confirm();
        var reviewedAt = dossier.ReviewedAt;

        dossier.UpdateByAgent("Agent viết lại mô hình kinh doanh", dossier.Moats.ToList(),
            dossier.RiskFactors.ToList(), null);

        dossier.ReviewedAt.Should().Be(reviewedAt);
    }

    [Fact]
    public void Confirm_ShouldSetBothTimestamps()
    {
        var dossier = Create();
        dossier.Confirm();

        dossier.ConfirmedAt.Should().NotBeNull();
        dossier.ReviewedAt.Should().BeCloseTo(dossier.ConfirmedAt!.Value, TimeSpan.FromSeconds(1));
    }
}
