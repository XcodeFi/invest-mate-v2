using MongoDB.Bson.Serialization.Attributes;

namespace InvestmentApp.Domain.Entities;

public class Debt
{
    public string Id { get; private set; } = null!;
    public DebtType Type { get; private set; }
    public string Name { get; private set; } = null!;
    public decimal Principal { get; private set; }
    public decimal? InterestRate { get; private set; }
    public decimal? MonthlyPayment { get; private set; }
    public DateTime? MaturityDate { get; private set; }
    public string? Note { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    [BsonConstructor]
    public Debt() { }

    internal static Debt Create(
        DebtType type,
        string name,
        decimal principal,
        decimal? interestRate = null,
        decimal? monthlyPayment = null,
        DateTime? maturityDate = null,
        string? note = null)
    {
        var now = DateTime.UtcNow;
        return new Debt
        {
            Id = Guid.NewGuid().ToString(),
            Type = type,
            Name = name ?? throw new ArgumentNullException(nameof(name)),
            Principal = principal,
            InterestRate = interestRate,
            MonthlyPayment = monthlyPayment,
            MaturityDate = maturityDate,
            Note = note,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    internal void Update(
        DebtType type,
        string name,
        decimal principal,
        decimal? interestRate,
        decimal? monthlyPayment,
        DateTime? maturityDate,
        string? note)
    {
        Type = type;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Principal = principal;
        InterestRate = interestRate;
        MonthlyPayment = monthlyPayment;
        MaturityDate = maturityDate;
        Note = note;
        UpdatedAt = DateTime.UtcNow;
    }
}
