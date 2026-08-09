using FluentAssertions;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.CompanyDossiers.Commands.ConfirmCompanyDossier;
using InvestmentApp.Application.CompanyDossiers.Commands.UpsertCompanyDossier;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.CompanyDossiers;

public class CompanyDossierCommandTests
{
    private readonly Mock<ICompanyDossierRepository> _repo = new();

    private static UpsertCompanyDossierCommand Command(bool byAgent) => new()
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

    [Fact]
    public async Task Upsert_WhenNoExisting_ShouldCreateUnconfirmed()
    {
        _repo.Setup(r => r.GetAsync("user-1", "hpg")).ReturnsAsync((CompanyDossier?)null);
        var handler = new UpsertCompanyDossierCommandHandler(_repo.Object);

        await handler.Handle(Command(byAgent: false), default);

        _repo.Verify(r => r.CreateAsync(It.Is<CompanyDossier>(d =>
            d.Symbol == "HPG" && d.ConfirmedAt == null)), Times.Once);
    }

    [Fact]
    public async Task Upsert_ByAgent_ShouldClearConfirmation()
    {
        var existing = Existing(confirmed: true);
        _repo.Setup(r => r.GetAsync("user-1", "hpg")).ReturnsAsync(existing);
        var handler = new UpsertCompanyDossierCommandHandler(_repo.Object);

        await handler.Handle(Command(byAgent: true), default);

        existing.ConfirmedAt.Should().BeNull();
        existing.AgentDraftedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Upsert_ByOwner_ShouldKeepConfirmation()
    {
        var existing = Existing(confirmed: true);
        _repo.Setup(r => r.GetAsync("user-1", "hpg")).ReturnsAsync(existing);
        var handler = new UpsertCompanyDossierCommandHandler(_repo.Object);

        await handler.Handle(Command(byAgent: false), default);

        existing.ConfirmedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Confirm_WhenMissing_ShouldThrowKeyNotFound()
    {
        _repo.Setup(r => r.GetAsync("user-1", "HPG")).ReturnsAsync((CompanyDossier?)null);
        var handler = new ConfirmCompanyDossierCommandHandler(_repo.Object);

        var act = () => handler.Handle(
            new ConfirmCompanyDossierCommand { UserId = "user-1", Symbol = "HPG" }, default);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Confirm_WhenExists_ShouldSetConfirmedAtAndSave()
    {
        var existing = Existing(confirmed: false);
        _repo.Setup(r => r.GetAsync("user-1", "HPG")).ReturnsAsync(existing);
        var handler = new ConfirmCompanyDossierCommandHandler(_repo.Object);

        await handler.Handle(
            new ConfirmCompanyDossierCommand { UserId = "user-1", Symbol = "HPG" }, default);

        existing.ConfirmedAt.Should().NotBeNull();
        _repo.Verify(r => r.UpdateAsync(existing), Times.Once);
    }

    [Fact]
    public async Task Upsert_WhenCreateHitsDuplicateKey_ShouldThrowConflict()
    {
        // Mô phỏng race hiếm: 2 request PUT trùng (userId, symbol) đến gần như đồng thời,
        // cả hai đều GetAsync ra null rồi cùng CreateAsync — cái sau va unique index
        // ux_user_symbol. Không có type MongoWriteException cụ thể ở tầng Application
        // (Application không phụ thuộc MongoDB.Driver), nên dùng message signature "E11000"
        // mà driver luôn phát ra để nhận diện, thay vì bắt riêng theo type.
        _repo.Setup(r => r.GetAsync("user-1", "hpg")).ReturnsAsync((CompanyDossier?)null);
        _repo.Setup(r => r.CreateAsync(It.IsAny<CompanyDossier>()))
            .ThrowsAsync(new Exception(
                "E11000 duplicate key error collection: company_dossiers index: ux_user_symbol dup key"));
        var handler = new UpsertCompanyDossierCommandHandler(_repo.Object);

        var act = () => handler.Handle(Command(byAgent: false), default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*đã tồn tại*");
    }

    [Fact]
    public async Task Upsert_WhenCreateThrowsUnrelatedError_ShouldPropagateOriginal()
    {
        _repo.Setup(r => r.GetAsync("user-1", "hpg")).ReturnsAsync((CompanyDossier?)null);
        _repo.Setup(r => r.CreateAsync(It.IsAny<CompanyDossier>()))
            .ThrowsAsync(new TimeoutException("network hiccup"));
        var handler = new UpsertCompanyDossierCommandHandler(_repo.Object);

        var act = () => handler.Handle(Command(byAgent: false), default);

        await act.Should().ThrowAsync<TimeoutException>();
    }

    private static CompanyDossier Existing(bool confirmed)
    {
        var d = new CompanyDossier("user-1", "HPG", "Mô hình cũ",
            new List<MoatItem> { new() { Description = "Moat cũ" } },
            new List<RiskFactor> { new() { Rank = 1, Description = "Rủi ro cũ", ObservableSignal = "Dấu hiệu cũ đủ dài" } });
        if (confirmed) d.Confirm();
        return d;
    }
}
