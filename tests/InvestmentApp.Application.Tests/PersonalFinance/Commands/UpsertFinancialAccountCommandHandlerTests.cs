using FluentAssertions;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.PersonalFinance.Commands.UpsertFinancialAccount;
using InvestmentApp.Application.PersonalFinance.Dtos;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.PersonalFinance.Commands;

public class UpsertFinancialAccountCommandHandlerTests
{
    private readonly Mock<IFinancialProfileRepository> _repo = new();
    private readonly Mock<IGoldPriceProvider> _goldProvider = new();
    private readonly UpsertFinancialAccountCommandHandler _handler;

    public UpsertFinancialAccountCommandHandlerTests()
    {
        _handler = new UpsertFinancialAccountCommandHandler(_repo.Object, _goldProvider.Object);
    }

    [Fact]
    public async Task Handle_ProfileNotExist_Throws()
    {
        _repo.Setup(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync((FinancialProfile?)null);

        var cmd = new UpsertFinancialAccountCommand
        {
            UserId = "u1",
            Type = FinancialAccountType.Savings,
            Name = "x",
            Balance = 1m,
        };
        var act = () => _handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Handle_NewNonGoldAccount_UsesProvidedBalance()
    {
        var profile = FinancialProfile.Create("u1", 10_000_000m);
        _repo.Setup(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(profile);

        var cmd = new UpsertFinancialAccountCommand
        {
            UserId = "u1",
            Type = FinancialAccountType.Savings,
            Name = "Tiết kiệm VCB",
            Balance = 100_000_000m,
            InterestRate = 5.5m,
        };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.Name.Should().Be("Tiết kiệm VCB");
        result.Balance.Should().Be(100_000_000m);
        result.InterestRate.Should().Be(5.5m);
        _goldProvider.Verify(g => g.GetPriceAsync(It.IsAny<GoldBrand>(), It.IsAny<GoldType>(), It.IsAny<CancellationToken>()), Times.Never);
        _repo.Verify(r => r.UpdateAsync(profile, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NonGoldAccount_MissingBalance_Throws()
    {
        // Regression guard: Savings/Emergency/IdleCash + Gold manual mode phải có Balance, không silent 0.
        var profile = FinancialProfile.Create("u1", 10_000_000m);
        _repo.Setup(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(profile);

        var cmd = new UpsertFinancialAccountCommand
        {
            UserId = "u1",
            Type = FinancialAccountType.Savings,
            Name = "x",
            // Balance not provided
        };
        var act = () => _handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Balance*");
    }

    [Fact]
    public async Task Handle_ExistingAccount_UpdatesInPlace()
    {
        var profile = FinancialProfile.Create("u1", 10_000_000m);
        var savings = profile.Accounts.First(a => a.Type == FinancialAccountType.Savings);
        _repo.Setup(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(profile);

        var cmd = new UpsertFinancialAccountCommand
        {
            UserId = "u1",
            AccountId = savings.Id,
            Type = FinancialAccountType.Savings,
            Name = "Tiết kiệm mới",
            Balance = 200_000_000m,
        };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.Id.Should().Be(savings.Id);
        result.Name.Should().Be("Tiết kiệm mới");
        result.Balance.Should().Be(200_000_000m);
        profile.Accounts.Should().HaveCount(4); // no new account added
    }

    [Fact]
    public async Task Handle_Gold_AutoCalcBalance_FromProvider()
    {
        // User nhập 2 lượng SJC Miếng — handler gọi provider để lấy giá → Balance = 2 × sellPrice
        var profile = FinancialProfile.Create("u1", 10_000_000m);
        _repo.Setup(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        _goldProvider.Setup(g => g.GetPriceAsync(GoldBrand.SJC, GoldType.Mieng, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoldPriceDto
            {
                Brand = GoldBrand.SJC,
                Type = GoldType.Mieng,
                BuyPrice = 167_000_000m,
                SellPrice = 169_500_000m,
                UpdatedAt = DateTime.UtcNow,
            });

        var cmd = new UpsertFinancialAccountCommand
        {
            UserId = "u1",
            Type = FinancialAccountType.Gold,
            Name = "SJC Miếng",
            GoldBrand = GoldBrand.SJC,
            GoldType = GoldType.Mieng,
            GoldQuantity = 2m,
            // Balance NOT provided — should be computed from provider
        };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.Balance.Should().Be(339_000_000m); // 2 × 169,500,000
        result.GoldQuantity.Should().Be(2m);
        result.GoldBrand.Should().Be(GoldBrand.SJC);
        result.GoldType.Should().Be(GoldType.Mieng);
        _goldProvider.Verify(g => g.GetPriceAsync(GoldBrand.SJC, GoldType.Mieng, It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(r => r.UpdateAsync(profile, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Gold_ProviderReturnsNull_Throws()
    {
        // Provider fail (crawler down) → throw, không silent fallback
        var profile = FinancialProfile.Create("u1", 10_000_000m);
        _repo.Setup(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        _goldProvider.Setup(g => g.GetPriceAsync(It.IsAny<GoldBrand>(), It.IsAny<GoldType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GoldPriceDto?)null);

        var cmd = new UpsertFinancialAccountCommand
        {
            UserId = "u1",
            Type = FinancialAccountType.Gold,
            Name = "x",
            GoldBrand = GoldBrand.SJC,
            GoldType = GoldType.Mieng,
            GoldQuantity = 1m,
        };
        var act = () => _handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Handle_Gold_ManualMode_UsesProvidedBalance()
    {
        // Không set 3 Gold field → fallback dùng Balance do user nhập, không gọi provider
        var profile = FinancialProfile.Create("u1", 10_000_000m);
        _repo.Setup(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(profile);

        var cmd = new UpsertFinancialAccountCommand
        {
            UserId = "u1",
            Type = FinancialAccountType.Gold,
            Name = "Vàng nhập tay",
            Balance = 300_000_000m,
        };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.Balance.Should().Be(300_000_000m);
        result.GoldBrand.Should().BeNull();
        result.GoldQuantity.Should().BeNull();
        _goldProvider.Verify(g => g.GetPriceAsync(It.IsAny<GoldBrand>(), It.IsAny<GoldType>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
