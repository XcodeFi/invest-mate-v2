using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.PersonalFinance.Dtos;

internal static class PersonalFinanceMapper
{
    public static FinancialProfileDto ToDto(FinancialProfile profile) => new()
    {
        Id = profile.Id,
        UserId = profile.UserId,
        MonthlyExpense = profile.MonthlyExpense,
        Accounts = profile.Accounts.Select(a => ToDto(a)).ToList(),
        Debts = profile.Debts.Select(ToDto).ToList(),
        Rules = ToDto(profile.Rules),
        CreatedAt = profile.CreatedAt,
        UpdatedAt = profile.UpdatedAt,
    };

    public static DebtDto ToDto(Debt debt) => new()
    {
        Id = debt.Id,
        Type = debt.Type,
        Name = debt.Name,
        Principal = debt.Principal,
        InterestRate = debt.InterestRate,
        MonthlyPayment = debt.MonthlyPayment,
        MaturityDate = debt.MaturityDate,
        Note = debt.Note,
        UpdatedAt = debt.UpdatedAt,
    };

    public static FinancialAccountDto ToDto(FinancialAccount account, decimal? balanceOverride = null) => new()
    {
        Id = account.Id,
        Type = account.Type,
        Name = account.Name,
        Balance = balanceOverride ?? account.Balance,
        InterestRate = account.InterestRate,
        Note = account.Note,
        GoldBrand = account.GoldBrand,
        GoldType = account.GoldType,
        GoldQuantity = account.GoldQuantity,
        UpdatedAt = account.UpdatedAt,
    };

    public static FinancialRulesDto ToDto(FinancialRules rules) => new()
    {
        EmergencyFundMonths = rules.EmergencyFundMonths,
        MaxInvestmentPercent = rules.MaxInvestmentPercent,
        MinSavingsPercent = rules.MinSavingsPercent,
    };
}
