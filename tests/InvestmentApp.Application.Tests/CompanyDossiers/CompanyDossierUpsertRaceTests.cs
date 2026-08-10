using FluentAssertions;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.CompanyDossiers.Commands.UpsertCompanyDossier;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.CompanyDossiers;

/// <summary>
/// Upsert là find → insert-on-miss. Hai request đồng thời cho cùng (UserId, Symbol)
/// đều thấy null, đều insert; index unique ux_user_symbol chặn cái thứ hai. Người
/// dùng bấm Lưu hai lần nhanh là gặp. Handler phải tìm lại và cập nhật lên document
/// đã thắng, không để lỗi thoát ra.
///
/// Test nằm ở tầng handler chứ không phải repository: WriteError trong
/// MongoDB.Driver 3.6.0 chỉ có ctor non-public không tham số nên không dựng được
/// MongoWriteException thật, còn ICompanyDossierRepository thì mock thẳng được.
/// </summary>
public class CompanyDossierUpsertRaceTests
{
    private readonly Mock<ICompanyDossierRepository> _repo = new();

    private static UpsertCompanyDossierCommand Command(bool byAgent = false) => new()
    {
        UserId = "user-1",
        Symbol = "hpg",
        BusinessModel = "Bán thép xây dựng và HRC cho nhà thầu nội địa",
        Moats = new List<MoatItem> { new() { Description = "Chi phí đơn vị thấp nhất ngành nội địa" } },
        RiskFactors = new List<RiskFactor>
        {
            new() { Rank = 1, Description = "Giá HRC Trung Quốc", ObservableSignal = "Giá HRC giảm quá 10% trong một tháng" }
        },
        ByAgent = byAgent
    };

    /// <summary>Document mà request thắng cuộc đã ghi xuống trước.</summary>
    private static CompanyDossier Winner()
    {
        var d = new CompanyDossier("user-1", "HPG", "Bản của request thắng cuộc",
            new List<MoatItem> { new() { Description = "Moat cũ" } },
            new List<RiskFactor> { new() { Rank = 1, ObservableSignal = "Dấu hiệu cũ đủ dài để qua entity" } });
        d.Confirm();
        return d;
    }

    private void LosesRaceThenFinds(CompanyDossier? onRetry)
    {
        _repo.SetupSequence(r => r.GetAsync("user-1", "hpg"))
            .ReturnsAsync((CompanyDossier?)null)
            .ReturnsAsync(onRetry);

        _repo.Setup(r => r.CreateAsync(It.IsAny<CompanyDossier>()))
            .ThrowsAsync(new DuplicateDossierException("user-1", "HPG"));
    }

    [Fact]
    public async Task Upsert_WhenLosesInsertRace_ShouldUpdateWinnerInsteadOfThrowing()
    {
        var winner = Winner();
        LosesRaceThenFinds(winner);
        var handler = new UpsertCompanyDossierCommandHandler(_repo.Object);

        var id = await handler.Handle(Command(), default);

        id.Should().Be(winner.Id);
        _repo.Verify(r => r.UpdateAsync(winner), Times.Once);
    }

    [Fact]
    public async Task Upsert_WhenLosesInsertRace_ShouldNotInsertASecondTime()
    {
        // Thử lại bằng find, không phải bằng insert lần nữa — insert lại chỉ va vào
        // đúng index unique đó và lần này thì không ai bắt.
        LosesRaceThenFinds(Winner());
        var handler = new UpsertCompanyDossierCommandHandler(_repo.Object);

        await handler.Handle(Command(), default);

        _repo.Verify(r => r.CreateAsync(It.IsAny<CompanyDossier>()), Times.Once);
    }

    [Fact]
    public async Task Upsert_ByAgentAfterLosingRace_ShouldStillClearConfirmation()
    {
        // Đường thử lại phải áp cùng ngữ nghĩa với đường cập nhật thường: agent sửa
        // thì hồ sơ mất chữ ký và cổng chặn lại. Nếu đường này đi tắt, agent ghi đè
        // được lên hồ sơ đã ký mà không ai phải ký lại.
        var winner = Winner();
        winner.ConfirmedAt.Should().NotBeNull("tiền đề của test: document thắng cuộc đang có chữ ký");
        LosesRaceThenFinds(winner);
        var handler = new UpsertCompanyDossierCommandHandler(_repo.Object);

        await handler.Handle(Command(byAgent: true), default);

        winner.ConfirmedAt.Should().BeNull();
    }

    [Fact]
    public async Task Upsert_WhenRetryFindsNothing_ShouldPropagateInsteadOfLooping()
    {
        // Thử lại đúng MỘT lần. Vòng lặp không giới hạn ở đây là đổi 500 thành treo
        // request — tệ hơn hẳn cái đang sửa.
        LosesRaceThenFinds(onRetry: null);
        var handler = new UpsertCompanyDossierCommandHandler(_repo.Object);

        var act = () => handler.Handle(Command(), default);

        await act.Should().ThrowAsync<DuplicateDossierException>();
        _repo.Verify(r => r.CreateAsync(It.IsAny<CompanyDossier>()), Times.Once);

        // Đúng HAI lần find: lần đầu thấy null, lần thử lại vẫn null rồi mới chịu thua.
        // Thiếu assertion này thì test xanh cả với code chưa có retry — "ném ra ngoài"
        // và "không thử lại lần nào" nhìn giống hệt nhau từ phía caller.
        _repo.Verify(r => r.GetAsync("user-1", "hpg"), Times.Exactly(2));
    }
}
