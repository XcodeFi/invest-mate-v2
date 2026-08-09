using FluentAssertions;
using InvestmentApp.Application.CompanyDossiers.Gate;
using InvestmentApp.Application.CompanyDossiers.Queries.GetCompanyDossier;
using InvestmentApp.Application.CompanyDossiers.Queries.GetDossierGateStatus;
using InvestmentApp.Application.CompanyDossiers.Queries.ListCompanyDossiers;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.CompanyDossiers;

public class CompanyDossierQueryTests
{
    private readonly Mock<ICompanyDossierRepository> _repo = new();
    private readonly Mock<ICompanyDossierGate> _gate = new();

    private static CompanyDossier NewDossier(bool confirm = false) => new(
        "user-1", "hpg", "Bán thép xây dựng và HRC cho nhà thầu nội địa",
        new List<MoatItem> { new() { Description = "Chi phí đơn vị thấp nhất ngành" } },
        new List<RiskFactor>
        {
            new() { Rank = 1, Description = "Giá HRC Trung Quốc", ObservableSignal = "Giá HRC giảm quá 10% trong một tháng" }
        });

    [Fact]
    public async Task GetCompanyDossier_WhenMissing_ShouldReturnNull()
    {
        _repo.Setup(r => r.GetAsync("user-1", "HPG")).ReturnsAsync((CompanyDossier?)null);
        var handler = new GetCompanyDossierQueryHandler(_repo.Object);

        var result = await handler.Handle(
            new GetCompanyDossierQuery { UserId = "user-1", Symbol = "HPG" }, default);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCompanyDossier_WhenFound_ShouldMapFreshnessUnconfirmed()
    {
        var dossier = NewDossier();
        _repo.Setup(r => r.GetAsync("user-1", "HPG")).ReturnsAsync(dossier);
        var handler = new GetCompanyDossierQueryHandler(_repo.Object);

        var result = await handler.Handle(
            new GetCompanyDossierQuery { UserId = "user-1", Symbol = "HPG" }, default);

        result.Should().NotBeNull();
        result!.Symbol.Should().Be("HPG");
        result.Freshness.Should().Be("Unconfirmed");
        result.ConfirmedAt.Should().BeNull();
    }

    [Fact]
    public async Task GetCompanyDossier_WhenConfirmed_ShouldMapFreshnessFresh()
    {
        var dossier = NewDossier();
        dossier.Confirm();
        _repo.Setup(r => r.GetAsync("user-1", "HPG")).ReturnsAsync(dossier);
        var handler = new GetCompanyDossierQueryHandler(_repo.Object);

        var result = await handler.Handle(
            new GetCompanyDossierQuery { UserId = "user-1", Symbol = "HPG" }, default);

        result!.Freshness.Should().Be("Fresh");
        result.ConfirmedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ListCompanyDossiers_ShouldMapEveryItem()
    {
        _repo.Setup(r => r.GetByUserIdAsync("user-1"))
            .ReturnsAsync(new List<CompanyDossier> { NewDossier() });
        var handler = new ListCompanyDossiersQueryHandler(_repo.Object);

        var result = await handler.Handle(
            new ListCompanyDossiersQuery { UserId = "user-1" }, default);

        result.Should().HaveCount(1);
        result[0].Symbol.Should().Be("HPG");
    }

    [Fact]
    public async Task GateStatus_ShouldComputePlanSizeFromQuantityAndEntryPrice()
    {
        _gate.Setup(g => g.EvaluateAsync("user-1", "HPG", 20_000_000m, 100_000_000m, default))
            .ReturnsAsync(DossierGateResult.Ok());
        var handler = new GetDossierGateStatusQueryHandler(_gate.Object);

        var result = await handler.Handle(new GetDossierGateStatusQuery
        {
            UserId = "user-1",
            Symbol = "HPG",
            Quantity = 2000,
            EntryPrice = 10_000m,
            AccountBalance = 100_000_000m
        }, default);

        result.Symbol.Should().Be("HPG");
        result.Passed.Should().BeTrue();
        _gate.Verify(g => g.EvaluateAsync("user-1", "HPG", 20_000_000m, 100_000_000m, default), Times.Once);
    }

    [Fact]
    public async Task GateStatus_WhenNoQuantityOrPrice_ShouldUseZeroPlanSize()
    {
        _gate.Setup(g => g.EvaluateAsync("user-1", "HPG", 0m, null, default))
            .ReturnsAsync(DossierGateResult.Fail("missing"));
        var handler = new GetDossierGateStatusQueryHandler(_gate.Object);

        var result = await handler.Handle(
            new GetDossierGateStatusQuery { UserId = "user-1", Symbol = "HPG" }, default);

        result.Passed.Should().BeFalse();
        result.Reason.Should().Be("missing");
    }
}
