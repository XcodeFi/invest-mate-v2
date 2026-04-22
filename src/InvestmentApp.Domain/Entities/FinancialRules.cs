using MongoDB.Bson.Serialization.Attributes;

namespace InvestmentApp.Domain.Entities;

public class FinancialRules
{
    public int EmergencyFundMonths { get; private set; }
    public decimal MaxInvestmentPercent { get; private set; }
    public decimal MinSavingsPercent { get; private set; }

    [BsonConstructor]
    public FinancialRules() { }

    public FinancialRules(int emergencyFundMonths, decimal maxInvestmentPercent, decimal minSavingsPercent)
    {
        if (emergencyFundMonths <= 0)
            throw new ArgumentOutOfRangeException(nameof(emergencyFundMonths), "EmergencyFundMonths phải > 0");
        if (maxInvestmentPercent < 0m || maxInvestmentPercent > 100m)
            throw new ArgumentOutOfRangeException(nameof(maxInvestmentPercent), "MaxInvestmentPercent phải trong [0, 100]");
        if (minSavingsPercent < 0m || minSavingsPercent > 100m)
            throw new ArgumentOutOfRangeException(nameof(minSavingsPercent), "MinSavingsPercent phải trong [0, 100]");

        EmergencyFundMonths = emergencyFundMonths;
        MaxInvestmentPercent = maxInvestmentPercent;
        MinSavingsPercent = minSavingsPercent;
    }

    public static FinancialRules Default() => new(
        emergencyFundMonths: 6,
        maxInvestmentPercent: 50m,
        minSavingsPercent: 30m);

    public FinancialRules With(int? emergencyFundMonths = null, decimal? maxInvestmentPercent = null, decimal? minSavingsPercent = null)
    {
        return new FinancialRules(
            emergencyFundMonths ?? EmergencyFundMonths,
            maxInvestmentPercent ?? MaxInvestmentPercent,
            minSavingsPercent ?? MinSavingsPercent);
    }
}
