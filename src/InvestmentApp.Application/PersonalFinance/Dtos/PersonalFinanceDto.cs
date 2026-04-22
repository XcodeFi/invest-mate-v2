using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.PersonalFinance.Dtos;

public class FinancialProfileDto
{
    public string Id { get; set; } = null!;
    public string UserId { get; set; } = null!;
    public decimal MonthlyExpense { get; set; }
    public List<FinancialAccountDto> Accounts { get; set; } = new();
    public FinancialRulesDto Rules { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class FinancialAccountDto
{
    public string Id { get; set; } = null!;
    public FinancialAccountType Type { get; set; }
    public string Name { get; set; } = null!;
    public decimal Balance { get; set; }
    public decimal? InterestRate { get; set; }
    public string? Note { get; set; }
    public GoldBrand? GoldBrand { get; set; }
    public GoldType? GoldType { get; set; }
    public decimal? GoldQuantity { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class FinancialRulesDto
{
    public int EmergencyFundMonths { get; set; } = 6;
    public decimal MaxInvestmentPercent { get; set; } = 50m;
    public decimal MinSavingsPercent { get; set; } = 30m;
}

public class NetWorthSummaryDto
{
    public decimal TotalAssets { get; set; }
    public decimal SecuritiesValue { get; set; }
    public decimal GoldTotal { get; set; }
    public decimal SavingsTotal { get; set; }
    public decimal EmergencyTotal { get; set; }
    public decimal IdleCashTotal { get; set; }
    public decimal MonthlyExpense { get; set; }
    public int HealthScore { get; set; }
    public List<RuleCheckResultDto> RuleChecks { get; set; } = new();
    public List<FinancialAccountDto> Accounts { get; set; } = new();
}

public class RuleCheckResultDto
{
    public string RuleName { get; set; } = null!;
    public bool IsPassing { get; set; }
    public string Description { get; set; } = null!;
    public decimal CurrentValue { get; set; }
    public decimal RequiredValue { get; set; }
}

public class GoldPriceDto
{
    public GoldBrand Brand { get; set; }
    public GoldType Type { get; set; }
    public decimal BuyPrice { get; set; }
    public decimal SellPrice { get; set; }
    public DateTime UpdatedAt { get; set; }
}
