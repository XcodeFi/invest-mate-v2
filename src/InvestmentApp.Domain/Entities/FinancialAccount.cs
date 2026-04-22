using MongoDB.Bson.Serialization.Attributes;

namespace InvestmentApp.Domain.Entities;

public class FinancialAccount
{
    public string Id { get; private set; } = null!;
    public FinancialAccountType Type { get; private set; }
    public string Name { get; private set; } = null!;
    public decimal Balance { get; private set; }
    public decimal? InterestRate { get; private set; }
    public string? Note { get; private set; }
    public GoldBrand? GoldBrand { get; private set; }
    public GoldType? GoldType { get; private set; }
    public decimal? GoldQuantity { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    [BsonConstructor]
    public FinancialAccount() { }

    internal static FinancialAccount Create(
        FinancialAccountType type,
        string name,
        decimal balance,
        decimal? interestRate = null,
        string? note = null,
        GoldBrand? goldBrand = null,
        GoldType? goldType = null,
        decimal? goldQuantity = null)
    {
        return new FinancialAccount
        {
            Id = Guid.NewGuid().ToString(),
            Type = type,
            Name = name ?? throw new ArgumentNullException(nameof(name)),
            Balance = balance,
            InterestRate = interestRate,
            Note = note,
            GoldBrand = goldBrand,
            GoldType = goldType,
            GoldQuantity = goldQuantity,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    internal void Update(
        FinancialAccountType type,
        string name,
        decimal balance,
        decimal? interestRate,
        string? note,
        GoldBrand? goldBrand,
        GoldType? goldType,
        decimal? goldQuantity)
    {
        Type = type;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Balance = balance;
        InterestRate = interestRate;
        Note = note;
        GoldBrand = goldBrand;
        GoldType = goldType;
        GoldQuantity = goldQuantity;
        UpdatedAt = DateTime.UtcNow;
    }
}
