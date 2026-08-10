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

    private static CompanyDossier Existing(bool confirmed)
    {
        var d = new CompanyDossier("user-1", "HPG", "Mô hình cũ",
            new List<MoatItem> { new() { Description = "Moat cũ" } },
            new List<RiskFactor> { new() { Rank = 1, Description = "Rủi ro cũ", ObservableSignal = "Dấu hiệu cũ đủ dài" } });
        if (confirmed) d.Confirm();
        return d;
    }
}
