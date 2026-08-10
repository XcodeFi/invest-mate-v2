using FluentAssertions;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.JournalEntries.Queries.GetSymbolTimeline;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.JournalEntries;

/// <summary>
/// Mốc hồ sơ công ty trên dòng thời gian của mã.
///
/// Giới hạn phải nói thẳng: CompanyDossier KHÔNG lưu lịch sử — chỉ có ReviewedAt,
/// ConfirmedAt, AgentDraftedAt. Nên timeline dựng được tối đa 2 mốc (lần ký gần
/// nhất, lần agent sửa gần nhất), không phải lịch sử tiến hoá của luận điểm.
/// Muốn có lịch sử thật thì phải lưu snapshot mỗi lần ký — việc riêng.
/// </summary>
public class GetSymbolTimelineDossierTests
{
    private readonly Mock<IJournalEntryRepository> _journalRepo = new();
    private readonly Mock<ITradeRepository> _tradeRepo = new();
    private readonly Mock<IPortfolioRepository> _portfolioRepo = new();
    private readonly Mock<IMarketEventRepository> _marketEventRepo = new();
    private readonly Mock<IAlertHistoryRepository> _alertRepo = new();
    private readonly Mock<ICompanyDossierRepository> _dossierRepo = new();

    public GetSymbolTimelineDossierTests()
    {
        _journalRepo.Setup(r => r.GetByUserIdAndSymbolAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JournalEntry>());
        _portfolioRepo.Setup(r => r.GetByUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Portfolio>());
        _tradeRepo.Setup(r => r.GetByUserPortfoliosAndSymbolAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Trade>());
        _marketEventRepo.Setup(r => r.GetBySymbolAsync(
                It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MarketEvent>());
        _alertRepo.Setup(r => r.GetByUserIdAndSymbolAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AlertHistory>());
    }

    private GetSymbolTimelineQueryHandler Sut() => new(
        _journalRepo.Object, _tradeRepo.Object, _portfolioRepo.Object,
        _marketEventRepo.Object, _alertRepo.Object, _dossierRepo.Object);

    private static CompanyDossier Dossier() => new(
        "user-1", "HPG", "Bán thép xây dựng và HRC cho nhà thầu nội địa",
        new List<MoatItem> { new() { Description = "Chi phí đơn vị thấp nhất ngành" } },
        new List<RiskFactor> { new() { Rank = 1, ObservableSignal = "Giá HRC giảm quá 10% trong một tháng" } });

    private void HasDossier(CompanyDossier? d)
        => _dossierRepo.Setup(r => r.GetAsync("user-1", "HPG")).ReturnsAsync(d);

    private static GetSymbolTimelineQuery Query(DateTime? from = null, DateTime? to = null)
        => new() { UserId = "user-1", Symbol = "HPG", From = from, To = to };

    [Fact]
    public async Task Timeline_WhenDossierConfirmed_ShouldIncludeSignedMarker()
    {
        // ConfirmedAt là mốc duy nhất chắc chắn có nghĩa: "người này đã đọc và chịu trách nhiệm".
        var d = Dossier();
        d.Confirm();
        HasDossier(d);

        var result = await Sut().Handle(Query(), default);

        result.Items.Should().ContainSingle(i => i.Type == "dossier");
    }

    [Fact]
    public async Task Timeline_WithoutDossier_ShouldNotAddAnyDossierItem()
    {
        HasDossier(null);

        var result = await Sut().Handle(Query(), default);

        result.Items.Should().NotContain(i => i.Type == "dossier");
    }

    [Fact]
    public async Task Timeline_UnconfirmedDossier_ShouldNotAddSignedMarker()
    {
        // Hồ sơ tồn tại nhưng chưa ký thì không có mốc nào — "đã viết" không phải một sự kiện
        // trên dòng thời gian, "đã chịu trách nhiệm" mới là.
        HasDossier(Dossier());

        var result = await Sut().Handle(Query(), default);

        result.Items.Should().NotContain(i => i.Type == "dossier");
    }

    private static void AgentDrafts(CompanyDossier d) => d.UpdateByAgent(
        "Bản agent soạn lại sau khi đọc báo cáo quý",
        new List<MoatItem> { new() { Description = "Chi phí đơn vị thấp nhất ngành" } },
        new List<RiskFactor> { new() { Rank = 1, ObservableSignal = "Giá HRC giảm quá 10% trong một tháng" } },
        null);

    [Fact]
    public async Task Timeline_AgentDraftAfterConfirm_ShouldLeaveOnlyTheAgentMarker()
    {
        // Trạng thái "vừa đã ký vừa vừa bị agent sửa" KHÔNG tồn tại được: UpdateByAgent
        // đặt ConfirmedAt = null (người dùng chưa đọc bản mới). Nên sau khi agent sửa,
        // mốc ký không còn dữ liệu để dựng — không phải bug của timeline, mà là hệ quả
        // trực tiếp của việc hồ sơ không lưu lịch sử.
        var d = Dossier();
        d.Confirm();
        AgentDrafts(d);
        HasDossier(d);

        var result = await Sut().Handle(Query(), default);

        var markers = result.Items.Where(i => i.Type == "dossier").ToList();
        markers.Should().ContainSingle();
        markers[0].Timestamp.Should().Be(d.AgentDraftedAt!.Value);
    }

    [Fact]
    public async Task Timeline_ConfirmAfterAgentDraft_ShouldShowBothMarkers()
    {
        // Đây mới là ca hai mốc thật, vì Confirm() KHÔNG xoá AgentDraftedAt:
        // agent soạn → người dùng đọc và ký lại → còn cả hai mốc.
        var d = Dossier();
        AgentDrafts(d);
        d.Confirm();
        HasDossier(d);

        var result = await Sut().Handle(Query(), default);

        result.Items.Count(i => i.Type == "dossier").Should().Be(2);
    }

    [Fact]
    public async Task Timeline_DossierMarkers_ShouldRespectDateRangeLikeEveryOtherSource()
    {
        // Lọc khoảng ngày mà mốc hồ sơ vẫn hiện là nó nói dối về khoảng đang xem.
        var d = Dossier();
        d.Confirm();
        HasDossier(d);

        var result = await Sut().Handle(
            Query(from: DateTime.UtcNow.AddYears(-5), to: DateTime.UtcNow.AddYears(-4)), default);

        result.Items.Should().NotContain(i => i.Type == "dossier");
    }

    [Fact]
    public async Task Timeline_DossierMarker_ShouldSortAmongOtherItemsByTimestamp()
    {
        // Mốc phải nằm đúng chỗ theo thời gian, không phải bị nối vào cuối danh sách.
        var d = Dossier();
        d.Confirm();
        HasDossier(d);
        _marketEventRepo.Setup(r => r.GetBySymbolAsync(
                It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MarketEvent>
            {
                new("HPG", MarketEventType.News, "Sự kiện cũ", DateTime.UtcNow.AddDays(-30))
            });

        var result = await Sut().Handle(Query(), default);

        result.Items.Should().BeInAscendingOrder(i => i.Timestamp);
        result.Items.Last().Type.Should().Be("dossier");
    }
}
