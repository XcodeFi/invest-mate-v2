using FluentAssertions;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Domain.Tests.Entities;

public class FinancialProfileTests
{
    #region Create

    [Fact]
    public void Create_WithValidInputs_ShouldSetDefaults()
    {
        // Act
        var profile = FinancialProfile.Create("user-1", monthlyExpense: 20_000_000m);

        // Assert
        profile.Id.Should().NotBeNullOrEmpty();
        profile.UserId.Should().Be("user-1");
        profile.MonthlyExpense.Should().Be(20_000_000m);
        profile.IsDeleted.Should().BeFalse();
        profile.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        profile.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Create_ShouldSeed4DefaultAccounts()
    {
        // Act
        var profile = FinancialProfile.Create("user-1", 20_000_000m);

        // Assert — 4 default accounts: Securities, Savings, Emergency, IdleCash (Gold on-demand)
        profile.Accounts.Should().HaveCount(4);
        profile.Accounts.Select(a => a.Type).Should().BeEquivalentTo(new[]
        {
            FinancialAccountType.Securities,
            FinancialAccountType.Savings,
            FinancialAccountType.Emergency,
            FinancialAccountType.IdleCash,
        });
        profile.Accounts.Select(a => a.Id).Should().OnlyHaveUniqueItems();
        profile.Accounts.Should().AllSatisfy(a => a.Balance.Should().Be(0m));
    }

    [Fact]
    public void Create_ShouldSeedDefaultRules()
    {
        // Act
        var profile = FinancialProfile.Create("user-1", 20_000_000m);

        // Assert — match plan defaults
        profile.Rules.EmergencyFundMonths.Should().Be(6);
        profile.Rules.MaxInvestmentPercent.Should().Be(50m);
        profile.Rules.MinSavingsPercent.Should().Be(30m);
    }

    [Fact]
    public void Create_NullUserId_ShouldThrow()
    {
        var action = () => FinancialProfile.Create(null!, 20_000_000m);
        action.Should().Throw<ArgumentNullException>().WithParameterName("userId");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-1_000_000)]
    public void Create_NonPositiveMonthlyExpense_ShouldThrow(decimal monthlyExpense)
    {
        var action = () => FinancialProfile.Create("user-1", monthlyExpense);
        action.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("monthlyExpense");
    }

    #endregion

    #region UpdateMonthlyExpense

    [Fact]
    public void UpdateMonthlyExpense_Valid_ShouldUpdateAndIncrementVersion()
    {
        // Arrange
        var profile = FinancialProfile.Create("user-1", 20_000_000m);
        var initialVersion = profile.Version;

        // Act
        profile.UpdateMonthlyExpense(25_000_000m);

        // Assert
        profile.MonthlyExpense.Should().Be(25_000_000m);
        profile.Version.Should().Be(initialVersion + 1);
        profile.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void UpdateMonthlyExpense_NonPositive_ShouldThrow(decimal amount)
    {
        var profile = FinancialProfile.Create("user-1", 20_000_000m);
        var action = () => profile.UpdateMonthlyExpense(amount);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    #endregion

    #region UpdateRules

    [Fact]
    public void UpdateRules_Partial_ShouldUpdateOnlySpecifiedFields()
    {
        // Arrange
        var profile = FinancialProfile.Create("user-1", 20_000_000m);
        var initialVersion = profile.Version;

        // Act — chỉ sửa MaxInvestmentPercent
        profile.UpdateRules(maxInvestmentPercent: 60m);

        // Assert
        profile.Rules.EmergencyFundMonths.Should().Be(6);       // unchanged
        profile.Rules.MaxInvestmentPercent.Should().Be(60m);    // changed
        profile.Rules.MinSavingsPercent.Should().Be(30m);       // unchanged
        profile.Version.Should().Be(initialVersion + 1);
    }

    [Fact]
    public void UpdateRules_AllFields_ShouldUpdateAll()
    {
        var profile = FinancialProfile.Create("user-1", 20_000_000m);

        profile.UpdateRules(emergencyFundMonths: 12, maxInvestmentPercent: 40m, minSavingsPercent: 40m);

        profile.Rules.EmergencyFundMonths.Should().Be(12);
        profile.Rules.MaxInvestmentPercent.Should().Be(40m);
        profile.Rules.MinSavingsPercent.Should().Be(40m);
    }

    #endregion

    #region UpsertAccount — new/existing

    [Fact]
    public void UpsertAccount_NewAccount_ShouldAddToList()
    {
        // Arrange
        var profile = FinancialProfile.Create("user-1", 20_000_000m);
        var initialCount = profile.Accounts.Count;

        // Act
        var account = profile.UpsertAccount(
            accountId: null,
            type: FinancialAccountType.Savings,
            name: "Tiết kiệm VCB",
            balance: 100_000_000m,
            interestRate: 5.5m);

        // Assert
        profile.Accounts.Should().HaveCount(initialCount + 1);
        account.Id.Should().NotBeNullOrEmpty();
        account.Type.Should().Be(FinancialAccountType.Savings);
        account.Name.Should().Be("Tiết kiệm VCB");
        account.Balance.Should().Be(100_000_000m);
        account.InterestRate.Should().Be(5.5m);
    }

    [Fact]
    public void UpsertAccount_ExistingAccount_ShouldUpdateInPlace()
    {
        // Arrange
        var profile = FinancialProfile.Create("user-1", 20_000_000m);
        var savingsAccount = profile.Accounts.First(a => a.Type == FinancialAccountType.Savings);
        var initialCount = profile.Accounts.Count;

        // Act — update existing
        profile.UpsertAccount(
            accountId: savingsAccount.Id,
            type: FinancialAccountType.Savings,
            name: "Tiết kiệm mới",
            balance: 50_000_000m,
            interestRate: 6m);

        // Assert
        profile.Accounts.Should().HaveCount(initialCount); // no new item
        var updated = profile.Accounts.First(a => a.Id == savingsAccount.Id);
        updated.Name.Should().Be("Tiết kiệm mới");
        updated.Balance.Should().Be(50_000_000m);
        updated.InterestRate.Should().Be(6m);
    }

    [Fact]
    public void UpsertAccount_ExistingAccountNotFound_ShouldThrow()
    {
        var profile = FinancialProfile.Create("user-1", 20_000_000m);
        var action = () => profile.UpsertAccount(
            accountId: "non-existent-id",
            type: FinancialAccountType.Savings,
            name: "x",
            balance: 1m);
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void UpsertAccount_NegativeBalance_ShouldThrow()
    {
        var profile = FinancialProfile.Create("user-1", 20_000_000m);
        var action = () => profile.UpsertAccount(
            accountId: null, type: FinancialAccountType.Savings, name: "x", balance: -1m);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void UpsertAccount_ShouldIncrementVersion()
    {
        var profile = FinancialProfile.Create("user-1", 20_000_000m);
        var initialVersion = profile.Version;

        profile.UpsertAccount(null, FinancialAccountType.IdleCash, "Tiền mặt", 10_000_000m);

        profile.Version.Should().Be(initialVersion + 1);
    }

    #endregion

    #region UpsertAccount — InterestRate validation

    [Fact]
    public void UpsertAccount_InterestRateOnNonSavings_ShouldThrow()
    {
        var profile = FinancialProfile.Create("user-1", 20_000_000m);
        var action = () => profile.UpsertAccount(
            accountId: null,
            type: FinancialAccountType.IdleCash,
            name: "x",
            balance: 1m,
            interestRate: 5m);  // invalid — IdleCash không có interest rate
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void UpsertAccount_InterestRateOnGold_ShouldThrow()
    {
        var profile = FinancialProfile.Create("user-1", 20_000_000m);
        var action = () => profile.UpsertAccount(
            accountId: null,
            type: FinancialAccountType.Gold,
            name: "SJC Miếng",
            balance: 170_000_000m,
            interestRate: 1m);  // invalid
        action.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region UpsertAccount — Gold validation

    [Fact]
    public void UpsertAccount_Gold_WithAll3Fields_ShouldSucceed()
    {
        var profile = FinancialProfile.Create("user-1", 20_000_000m);

        var account = profile.UpsertAccount(
            accountId: null,
            type: FinancialAccountType.Gold,
            name: "SJC Miếng",
            balance: 340_000_000m,  // 2 × 170tr
            goldBrand: GoldBrand.SJC,
            goldType: GoldType.Mieng,
            goldQuantity: 2m);

        account.GoldBrand.Should().Be(GoldBrand.SJC);
        account.GoldType.Should().Be(GoldType.Mieng);
        account.GoldQuantity.Should().Be(2m);
    }

    [Fact]
    public void UpsertAccount_Gold_ManualBalanceOnly_ShouldSucceed()
    {
        // Fallback path: không có 3 Gold field, user nhập tay Balance
        var profile = FinancialProfile.Create("user-1", 20_000_000m);

        var account = profile.UpsertAccount(
            accountId: null,
            type: FinancialAccountType.Gold,
            name: "Vàng manual",
            balance: 200_000_000m);

        account.Balance.Should().Be(200_000_000m);
        account.GoldBrand.Should().BeNull();
        account.GoldType.Should().BeNull();
        account.GoldQuantity.Should().BeNull();
    }

    [Fact]
    public void UpsertAccount_Gold_PartialFields_ShouldThrow()
    {
        // Thiếu 1 trong 3 → invalid
        var profile = FinancialProfile.Create("user-1", 20_000_000m);
        var action = () => profile.UpsertAccount(
            accountId: null,
            type: FinancialAccountType.Gold,
            name: "x",
            balance: 100m,
            goldBrand: GoldBrand.SJC,
            goldType: GoldType.Mieng
            // goldQuantity missing
        );
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void UpsertAccount_Gold_ZeroQuantity_ShouldThrow()
    {
        var profile = FinancialProfile.Create("user-1", 20_000_000m);
        var action = () => profile.UpsertAccount(
            accountId: null,
            type: FinancialAccountType.Gold,
            name: "x",
            balance: 0m,
            goldBrand: GoldBrand.SJC,
            goldType: GoldType.Mieng,
            goldQuantity: 0m);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void UpsertAccount_NonGold_WithGoldFields_ShouldThrow()
    {
        var profile = FinancialProfile.Create("user-1", 20_000_000m);
        var action = () => profile.UpsertAccount(
            accountId: null,
            type: FinancialAccountType.Savings,
            name: "x",
            balance: 1m,
            goldBrand: GoldBrand.SJC);
        action.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region RemoveAccount

    [Fact]
    public void RemoveAccount_Valid_ShouldRemoveAndIncrementVersion()
    {
        var profile = FinancialProfile.Create("user-1", 20_000_000m);
        var goldAccount = profile.UpsertAccount(null, FinancialAccountType.Gold, "SJC Miếng", 340_000_000m,
            goldBrand: GoldBrand.SJC, goldType: GoldType.Mieng, goldQuantity: 2m);
        var versionBefore = profile.Version;

        profile.RemoveAccount(goldAccount.Id);

        profile.Accounts.Should().NotContain(a => a.Id == goldAccount.Id);
        profile.Version.Should().Be(versionBefore + 1);
    }

    [Fact]
    public void RemoveAccount_LastSecurities_ShouldThrow()
    {
        var profile = FinancialProfile.Create("user-1", 20_000_000m);
        var securities = profile.Accounts.First(a => a.Type == FinancialAccountType.Securities);

        var action = () => profile.RemoveAccount(securities.Id);
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RemoveAccount_NotFound_ShouldThrow()
    {
        var profile = FinancialProfile.Create("user-1", 20_000_000m);
        var action = () => profile.RemoveAccount("non-existent-id");
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RemoveAccount_Gold_ShouldAlwaysSucceed()
    {
        // Gold không có constraint "last" — khác với Securities
        var profile = FinancialProfile.Create("user-1", 20_000_000m);
        var g1 = profile.UpsertAccount(null, FinancialAccountType.Gold, "SJC", 100m,
            goldBrand: GoldBrand.SJC, goldType: GoldType.Mieng, goldQuantity: 1m);

        var action = () => profile.RemoveAccount(g1.Id);
        action.Should().NotThrow();
    }

    #endregion

    #region GetTotalAssets

    [Fact]
    public void GetTotalAssets_WithoutGold_ShouldSumNonSecuritiesPlusLiveValue()
    {
        var profile = FinancialProfile.Create("user-1", 20_000_000m);
        // Default accounts all 0. Set some balances:
        var savings = profile.Accounts.First(a => a.Type == FinancialAccountType.Savings);
        profile.UpsertAccount(savings.Id, FinancialAccountType.Savings, savings.Name, 100_000_000m);
        var emergency = profile.Accounts.First(a => a.Type == FinancialAccountType.Emergency);
        profile.UpsertAccount(emergency.Id, FinancialAccountType.Emergency, emergency.Name, 120_000_000m);
        var idle = profile.Accounts.First(a => a.Type == FinancialAccountType.IdleCash);
        profile.UpsertAccount(idle.Id, FinancialAccountType.IdleCash, idle.Name, 30_000_000m);

        // Act
        var total = profile.GetTotalAssets(securitiesValue: 200_000_000m);

        // Assert — Savings + Emergency + IdleCash + Securities (live) = 100 + 120 + 30 + 200
        total.Should().Be(450_000_000m);
    }

    [Fact]
    public void GetTotalAssets_WithGold_ShouldIncludeGoldBalance()
    {
        var profile = FinancialProfile.Create("user-1", 20_000_000m);
        profile.UpsertAccount(null, FinancialAccountType.Gold, "SJC", 340_000_000m,
            goldBrand: GoldBrand.SJC, goldType: GoldType.Mieng, goldQuantity: 2m);

        // Act
        var total = profile.GetTotalAssets(securitiesValue: 200_000_000m);

        // Assert — all default 0 + Gold 340 + Securities 200
        total.Should().Be(540_000_000m);
    }

    #endregion

    #region CalculateHealthScore

    [Fact]
    public void CalculateHealthScore_AllRulesPass_ShouldReturn100()
    {
        // Arrange — build profile meeting all 3 rules
        var profile = FinancialProfile.Create("user-1", 10_000_000m);
        // Need totalAssets such that: Emergency >= 60tr, Savings >= 30%, Investment <= 50%
        // Let totalAssets = 1_000_000_000 (1tỷ), securities = 200tr, savings = 400tr, emergency = 100tr, idle = 300tr
        var savings = profile.Accounts.First(a => a.Type == FinancialAccountType.Savings);
        profile.UpsertAccount(savings.Id, FinancialAccountType.Savings, savings.Name, 400_000_000m);
        var emergency = profile.Accounts.First(a => a.Type == FinancialAccountType.Emergency);
        profile.UpsertAccount(emergency.Id, FinancialAccountType.Emergency, emergency.Name, 100_000_000m);
        var idle = profile.Accounts.First(a => a.Type == FinancialAccountType.IdleCash);
        profile.UpsertAccount(idle.Id, FinancialAccountType.IdleCash, idle.Name, 300_000_000m);

        // Act — securitiesValue = 200tr
        var score = profile.CalculateHealthScore(securitiesValue: 200_000_000m);

        // Assert
        // Emergency required = 10tr × 6 = 60tr; has 100tr ✓
        // Investment cap = 1_000_000_000 × 50% = 500tr; investment = 200tr ✓
        // Savings required = 1_000_000_000 × 30% = 300tr; has 400tr ✓
        score.Should().Be(100);
    }

    [Fact]
    public void CalculateHealthScore_EmergencyShortfall_ShouldDeductProportionally()
    {
        // Emergency 30tr / required 60tr = 50% thiếu → trừ 50% × 40 = 20 điểm
        // Other rules pass
        var profile = FinancialProfile.Create("user-1", 10_000_000m);
        var savings = profile.Accounts.First(a => a.Type == FinancialAccountType.Savings);
        profile.UpsertAccount(savings.Id, FinancialAccountType.Savings, savings.Name, 400_000_000m);
        var emergency = profile.Accounts.First(a => a.Type == FinancialAccountType.Emergency);
        profile.UpsertAccount(emergency.Id, FinancialAccountType.Emergency, emergency.Name, 30_000_000m);
        var idle = profile.Accounts.First(a => a.Type == FinancialAccountType.IdleCash);
        profile.UpsertAccount(idle.Id, FinancialAccountType.IdleCash, idle.Name, 370_000_000m);

        var score = profile.CalculateHealthScore(securitiesValue: 200_000_000m);

        // Total = 200 + 400 + 30 + 370 = 1000tr
        // Emergency deficit = (60-30)/60 = 0.5 → trừ 0.5 × 40 = 20
        // Score = 100 - 20 = 80
        score.Should().Be(80);
    }

    [Fact]
    public void CalculateHealthScore_InvestmentOverCap_ShouldDeductProportionally()
    {
        // Arrange: Total = 800tr, emergency pass, investment 75% (vượt 50% cap), savings 15% (dưới 30% floor).
        // Verify exact score để lock in math, không chỉ "< 100".
        var profile = FinancialProfile.Create("user-1", 10_000_000m);
        var savings = profile.Accounts.First(a => a.Type == FinancialAccountType.Savings);
        profile.UpsertAccount(savings.Id, FinancialAccountType.Savings, savings.Name, 120_000_000m);
        var emergency = profile.Accounts.First(a => a.Type == FinancialAccountType.Emergency);
        profile.UpsertAccount(emergency.Id, FinancialAccountType.Emergency, emergency.Name, 60_000_000m);
        var idle = profile.Accounts.First(a => a.Type == FinancialAccountType.IdleCash);
        profile.UpsertAccount(idle.Id, FinancialAccountType.IdleCash, idle.Name, 20_000_000m);

        var score = profile.CalculateHealthScore(securitiesValue: 600_000_000m);

        // Total = 600 + 120 + 60 + 20 = 800tr
        // Rule 1 (Emergency): required = 10×6 = 60tr; actual = 60tr → pass, -0
        // Rule 2 (Investment): cap = 800 × 50% = 400tr; actual = 600tr; excess/cap = 200/400 = 0.5 → -15
        // Rule 3 (Savings): required = 800 × 30% = 240tr; actual = 120tr; deficit/required = 120/240 = 0.5 → -15
        // Score = 100 - 15 - 15 = 70
        score.Should().Be(70);
    }

    [Fact]
    public void CalculateHealthScore_ShouldClampToZero_WhenAllRulesFailHeavily()
    {
        // All 3 rules fail maximally → score có thể âm → clamp về 0
        var profile = FinancialProfile.Create("user-1", 10_000_000m);
        // totalAssets = 1tỷ, securities = 1tỷ (100% investment), 0 emergency, 0 savings
        var score = profile.CalculateHealthScore(securitiesValue: 1_000_000_000m);

        score.Should().BeGreaterThanOrEqualTo(0);
        score.Should().Be(0);  // emergency -40, investment -30, savings -30 = -100 → clamp 0
    }

    [Fact]
    public void CalculateHealthScore_ZeroTotalAssets_ShouldReturn0()
    {
        // Không có asset nào → không đánh giá được → 0
        var profile = FinancialProfile.Create("user-1", 10_000_000m);

        var score = profile.CalculateHealthScore(securitiesValue: 0m);

        score.Should().Be(0);
    }

    [Fact]
    public void CalculateHealthScore_GoldCountsInInvestmentTotal()
    {
        // Verify: vàng cộng vào investment (cùng Securities) cho rule MaxInvestmentPercent
        // Setup: totalAssets = 1tỷ, securities = 300tr + gold 300tr = 600tr (60% > 50% cap)
        // So với không có gold thì chỉ 30% — nên test phải phân biệt được.
        var profile = FinancialProfile.Create("user-1", 10_000_000m);
        var savings = profile.Accounts.First(a => a.Type == FinancialAccountType.Savings);
        profile.UpsertAccount(savings.Id, FinancialAccountType.Savings, savings.Name, 300_000_000m);
        var emergency = profile.Accounts.First(a => a.Type == FinancialAccountType.Emergency);
        profile.UpsertAccount(emergency.Id, FinancialAccountType.Emergency, emergency.Name, 60_000_000m);
        var idle = profile.Accounts.First(a => a.Type == FinancialAccountType.IdleCash);
        profile.UpsertAccount(idle.Id, FinancialAccountType.IdleCash, idle.Name, 40_000_000m);
        profile.UpsertAccount(null, FinancialAccountType.Gold, "SJC", 300_000_000m,
            goldBrand: GoldBrand.SJC, goldType: GoldType.Mieng, goldQuantity: 1.765m);

        var scoreWithGold = profile.CalculateHealthScore(securitiesValue: 300_000_000m);

        // Total = 300 + 60 + 40 + 300 + 300 = 1000tr
        // Investment = securities 300 + gold 300 = 600tr = 60% > 50% → violation
        // Nếu KHÔNG tính gold vào investment: 300/1000 = 30% < 50% → pass
        // Kỳ vọng: có violation → score < 100
        scoreWithGold.Should().BeLessThan(100);
    }

    [Fact]
    public void CalculateHealthScore_GoldAlone_WithoutSecurities_StillCountsAsInvestment()
    {
        // Edge case: user chỉ có vàng, không có CK. Vàng vẫn phải tính là đầu tư.
        var profile = FinancialProfile.Create("user-1", 10_000_000m);
        // Total 1tỷ: Gold 800tr (80% — vi phạm 50% cap rất nặng), savings 100tr, emergency 60tr, idle 40tr
        var savings = profile.Accounts.First(a => a.Type == FinancialAccountType.Savings);
        profile.UpsertAccount(savings.Id, FinancialAccountType.Savings, savings.Name, 100_000_000m);
        var emergency = profile.Accounts.First(a => a.Type == FinancialAccountType.Emergency);
        profile.UpsertAccount(emergency.Id, FinancialAccountType.Emergency, emergency.Name, 60_000_000m);
        var idle = profile.Accounts.First(a => a.Type == FinancialAccountType.IdleCash);
        profile.UpsertAccount(idle.Id, FinancialAccountType.IdleCash, idle.Name, 40_000_000m);
        profile.UpsertAccount(null, FinancialAccountType.Gold, "SJC", 800_000_000m,
            goldBrand: GoldBrand.SJC, goldType: GoldType.Mieng, goldQuantity: 4.706m);

        var score = profile.CalculateHealthScore(securitiesValue: 0m);

        // Investment ratio = 800/1000 = 80% → vi phạm cap 50% nặng (trừ max 30)
        // Savings 100/1000 = 10% < 30% → vi phạm (trừ proportional)
        // Emergency 60 >= 60 → pass
        score.Should().BeLessThan(70);  // chắc chắn trừ cả investment + savings
    }

    #endregion

    #region SoftDelete / Restore

    [Fact]
    public void SoftDelete_ShouldSetIsDeletedAndIncrementVersion()
    {
        var profile = FinancialProfile.Create("user-1", 20_000_000m);
        var initialVersion = profile.Version;

        profile.SoftDelete();

        profile.IsDeleted.Should().BeTrue();
        profile.Version.Should().Be(initialVersion + 1);
    }

    [Fact]
    public void Restore_AfterSoftDelete_ShouldResetIsDeleted()
    {
        var profile = FinancialProfile.Create("user-1", 20_000_000m);
        profile.SoftDelete();

        profile.Restore();

        profile.IsDeleted.Should().BeFalse();
    }

    #endregion

    #region Debts — UpsertDebt / RemoveDebt

    [Fact]
    public void Create_ShouldInitializeEmptyDebtsList()
    {
        var profile = FinancialProfile.Create("user-1", 20_000_000m);
        profile.Debts.Should().NotBeNull();
        profile.Debts.Should().BeEmpty();
    }

    [Fact]
    public void UpsertDebt_NewDebt_ShouldAddToList()
    {
        var profile = FinancialProfile.Create("user-1", 20_000_000m);

        var debt = profile.UpsertDebt(
            debtId: null,
            type: DebtType.CreditCard,
            name: "Thẻ tín dụng VCB",
            principal: 15_000_000m,
            interestRate: 28m);

        profile.Debts.Should().HaveCount(1);
        debt.Id.Should().NotBeNullOrEmpty();
        debt.Type.Should().Be(DebtType.CreditCard);
        debt.Name.Should().Be("Thẻ tín dụng VCB");
        debt.Principal.Should().Be(15_000_000m);
        debt.InterestRate.Should().Be(28m);
    }

    [Fact]
    public void UpsertDebt_ExistingById_ShouldUpdateInPlace()
    {
        var profile = FinancialProfile.Create("user-1", 20_000_000m);
        var existing = profile.UpsertDebt(null, DebtType.Mortgage, "Vay nhà", 2_000_000_000m, interestRate: 9m);
        var countBefore = profile.Debts.Count;

        profile.UpsertDebt(existing.Id, DebtType.Mortgage, "Vay nhà BIDV", 1_950_000_000m, interestRate: 9.5m);

        profile.Debts.Should().HaveCount(countBefore);
        var updated = profile.Debts.First(d => d.Id == existing.Id);
        updated.Name.Should().Be("Vay nhà BIDV");
        updated.Principal.Should().Be(1_950_000_000m);
        updated.InterestRate.Should().Be(9.5m);
    }

    [Fact]
    public void UpsertDebt_NotFound_ShouldThrow()
    {
        var profile = FinancialProfile.Create("user-1", 20_000_000m);
        var action = () => profile.UpsertDebt("non-existent", DebtType.Other, "x", 1m);
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void UpsertDebt_NegativePrincipal_ShouldThrow()
    {
        var profile = FinancialProfile.Create("user-1", 20_000_000m);
        var action = () => profile.UpsertDebt(null, DebtType.CreditCard, "x", -1m);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void UpsertDebt_NegativeInterestRate_ShouldThrow()
    {
        var profile = FinancialProfile.Create("user-1", 20_000_000m);
        var action = () => profile.UpsertDebt(null, DebtType.CreditCard, "x", 1m, interestRate: -0.01m);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void UpsertDebt_ShouldIncrementVersion()
    {
        var profile = FinancialProfile.Create("user-1", 20_000_000m);
        var versionBefore = profile.Version;

        profile.UpsertDebt(null, DebtType.CreditCard, "x", 1m);

        profile.Version.Should().Be(versionBefore + 1);
    }

    [Fact]
    public void RemoveDebt_WithZeroPrincipal_ShouldSucceed()
    {
        var profile = FinancialProfile.Create("user-1", 20_000_000m);
        var d = profile.UpsertDebt(null, DebtType.CreditCard, "Đã trả xong", 0m);

        var action = () => profile.RemoveDebt(d.Id);

        action.Should().NotThrow();
        profile.Debts.Should().NotContain(x => x.Id == d.Id);
    }

    [Fact]
    public void RemoveDebt_WithPositivePrincipal_ShouldThrow()
    {
        var profile = FinancialProfile.Create("user-1", 20_000_000m);
        var d = profile.UpsertDebt(null, DebtType.CreditCard, "Còn nợ", 5_000_000m);

        var action = () => profile.RemoveDebt(d.Id);

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RemoveDebt_NotFound_ShouldThrow()
    {
        var profile = FinancialProfile.Create("user-1", 20_000_000m);
        var action = () => profile.RemoveDebt("non-existent");
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RemoveDebt_ShouldIncrementVersion()
    {
        var profile = FinancialProfile.Create("user-1", 20_000_000m);
        var d = profile.UpsertDebt(null, DebtType.CreditCard, "x", 0m);
        var versionBefore = profile.Version;

        profile.RemoveDebt(d.Id);

        profile.Version.Should().Be(versionBefore + 1);
    }

    #endregion

    #region GetTotalDebt / GetNetWorth

    [Fact]
    public void GetTotalDebt_NoDebts_ShouldReturnZero()
    {
        var profile = FinancialProfile.Create("user-1", 20_000_000m);
        profile.GetTotalDebt().Should().Be(0m);
    }

    [Fact]
    public void GetTotalDebt_MultipleDebts_ShouldSumPrincipal()
    {
        var profile = FinancialProfile.Create("user-1", 20_000_000m);
        profile.UpsertDebt(null, DebtType.CreditCard, "CC1", 10_000_000m, interestRate: 28m);
        profile.UpsertDebt(null, DebtType.Mortgage, "House", 1_500_000_000m, interestRate: 9m);
        profile.UpsertDebt(null, DebtType.Auto, "Car", 200_000_000m, interestRate: 8m);

        profile.GetTotalDebt().Should().Be(1_710_000_000m);
    }

    [Fact]
    public void GetNetWorth_AssetsMinusDebts()
    {
        var profile = FinancialProfile.Create("user-1", 20_000_000m);
        var savings = profile.Accounts.First(a => a.Type == FinancialAccountType.Savings);
        profile.UpsertAccount(savings.Id, FinancialAccountType.Savings, savings.Name, 500_000_000m);
        profile.UpsertDebt(null, DebtType.CreditCard, "CC", 15_000_000m, interestRate: 28m);

        // totalAssets = 500M savings + 389M securities (live) = 889M; debts = 15M; netWorth = 874M
        var netWorth = profile.GetNetWorth(securitiesValue: 389_000_000m);
        netWorth.Should().Be(874_000_000m);
    }

    [Fact]
    public void GetNetWorth_CanBeNegative_WhenDebtExceedsAssets()
    {
        var profile = FinancialProfile.Create("user-1", 20_000_000m);
        profile.UpsertDebt(null, DebtType.Mortgage, "House", 2_000_000_000m, interestRate: 9m);

        var netWorth = profile.GetNetWorth(securitiesValue: 100_000_000m);
        netWorth.Should().Be(-1_900_000_000m);
    }

    #endregion

    #region HasHighInterestConsumerDebt

    [Fact]
    public void HasHighInterestConsumerDebt_CreditCardAbove20_ShouldReturnTrue()
    {
        var profile = FinancialProfile.Create("user-1", 20_000_000m);
        profile.UpsertDebt(null, DebtType.CreditCard, "CC", 5_000_000m, interestRate: 28m);

        profile.HasHighInterestConsumerDebt().Should().BeTrue();
    }

    [Fact]
    public void HasHighInterestConsumerDebt_PersonalLoanAbove20_ShouldReturnTrue()
    {
        var profile = FinancialProfile.Create("user-1", 20_000_000m);
        profile.UpsertDebt(null, DebtType.PersonalLoan, "Vay tiêu dùng", 50_000_000m, interestRate: 24m);

        profile.HasHighInterestConsumerDebt().Should().BeTrue();
    }

    [Fact]
    public void HasHighInterestConsumerDebt_AtThreshold20_ShouldReturnFalse()
    {
        // Ngưỡng strict > 20. Đúng 20% coi như OK (nhiều vay tiêu dùng ở mức này).
        var profile = FinancialProfile.Create("user-1", 20_000_000m);
        profile.UpsertDebt(null, DebtType.CreditCard, "CC", 5_000_000m, interestRate: 20m);

        profile.HasHighInterestConsumerDebt().Should().BeFalse();
    }

    [Fact]
    public void HasHighInterestConsumerDebt_MortgageAbove20_ShouldReturnFalse()
    {
        // Mortgage không thuộc consumer debt — dù lãi cao cũng không tính
        var profile = FinancialProfile.Create("user-1", 20_000_000m);
        profile.UpsertDebt(null, DebtType.Mortgage, "Nhà lãi cao", 1_000_000_000m, interestRate: 25m);

        profile.HasHighInterestConsumerDebt().Should().BeFalse();
    }

    [Fact]
    public void HasHighInterestConsumerDebt_NullInterestRate_ShouldReturnFalse()
    {
        var profile = FinancialProfile.Create("user-1", 20_000_000m);
        profile.UpsertDebt(null, DebtType.CreditCard, "CC chưa nhập lãi", 5_000_000m);

        profile.HasHighInterestConsumerDebt().Should().BeFalse();
    }

    [Fact]
    public void HasHighInterestConsumerDebt_NoDebts_ShouldReturnFalse()
    {
        var profile = FinancialProfile.Create("user-1", 20_000_000m);
        profile.HasHighInterestConsumerDebt().Should().BeFalse();
    }

    #endregion

    #region Health score rule 4 — high-interest consumer debt

    [Fact]
    public void CalculateHealthScore_WithHighInterestConsumerDebt_ShouldDeduct20()
    {
        // Setup: perfect finances (3 rules pass) → score 100, then add CC debt 28% → score 80
        var profile = FinancialProfile.Create("user-1", 10_000_000m);
        var emergency = profile.Accounts.First(a => a.Type == FinancialAccountType.Emergency);
        profile.UpsertAccount(emergency.Id, FinancialAccountType.Emergency, emergency.Name, 60_000_000m); // 6 months × 10M
        var savings = profile.Accounts.First(a => a.Type == FinancialAccountType.Savings);
        profile.UpsertAccount(savings.Id, FinancialAccountType.Savings, savings.Name, 30_000_000m);       // 30% of 100M assets

        // Total: 60 emergency + 30 savings + 10 idle assumed → actually default idle=0; let's add
        var idle = profile.Accounts.First(a => a.Type == FinancialAccountType.IdleCash);
        profile.UpsertAccount(idle.Id, FinancialAccountType.IdleCash, idle.Name, 10_000_000m);
        // Total assets = 100M, investment=0 (under 50% cap), savings=30 (=30% floor), emergency=60 (=6mo).

        var scoreBefore = profile.CalculateHealthScore(securitiesValue: 0m);
        scoreBefore.Should().Be(100);

        profile.UpsertDebt(null, DebtType.CreditCard, "CC", 5_000_000m, interestRate: 28m);

        var scoreAfter = profile.CalculateHealthScore(securitiesValue: 0m);
        scoreAfter.Should().Be(80);
    }

    [Fact]
    public void CalculateHealthScore_WithHighInterestDebtAndOtherViolations_ShouldClampAt0()
    {
        // Both Investment cap (30) + Savings floor (30) + Emergency (40) fail → -100 already at 0
        // Add high-interest debt (-20) → clamped at 0, not negative
        var profile = FinancialProfile.Create("user-1", 10_000_000m);
        profile.UpsertDebt(null, DebtType.CreditCard, "CC", 5_000_000m, interestRate: 36m);

        // 100M fully in gold (investment) → cap violated; zero emergency + zero savings
        profile.UpsertAccount(null, FinancialAccountType.Gold, "SJC", 100_000_000m);

        var score = profile.CalculateHealthScore(securitiesValue: 0m);
        score.Should().Be(0);
    }

    [Fact]
    public void CalculateHealthScore_WithoutHighInterestDebt_ShouldNotDeduct20()
    {
        // Mortgage 10% lãi không trừ điểm rule 4
        var profile = FinancialProfile.Create("user-1", 10_000_000m);
        var emergency = profile.Accounts.First(a => a.Type == FinancialAccountType.Emergency);
        profile.UpsertAccount(emergency.Id, FinancialAccountType.Emergency, emergency.Name, 60_000_000m);
        var savings = profile.Accounts.First(a => a.Type == FinancialAccountType.Savings);
        profile.UpsertAccount(savings.Id, FinancialAccountType.Savings, savings.Name, 30_000_000m);
        var idle = profile.Accounts.First(a => a.Type == FinancialAccountType.IdleCash);
        profile.UpsertAccount(idle.Id, FinancialAccountType.IdleCash, idle.Name, 10_000_000m);

        profile.UpsertDebt(null, DebtType.Mortgage, "House", 500_000_000m, interestRate: 10m);

        var score = profile.CalculateHealthScore(securitiesValue: 0m);
        score.Should().Be(100);
    }

    #endregion
}
