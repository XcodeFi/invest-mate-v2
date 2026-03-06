using MongoDB.Bson.Serialization.Attributes;

namespace InvestmentApp.Domain.Entities;

public class ExchangeRate : AggregateRoot
{
    public string BaseCurrency { get; private set; }
    public string TargetCurrency { get; private set; }
    public decimal Rate { get; private set; }
    public DateTime Date { get; private set; }
    public string Source { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    [BsonConstructor]
    private ExchangeRate() { }

    public ExchangeRate(string baseCurrency, string targetCurrency, decimal rate, DateTime date, string source = "manual")
    {
        Id = Guid.NewGuid().ToString();
        BaseCurrency = baseCurrency?.ToUpperInvariant() ?? throw new ArgumentNullException(nameof(baseCurrency));
        TargetCurrency = targetCurrency?.ToUpperInvariant() ?? throw new ArgumentNullException(nameof(targetCurrency));
        Rate = rate > 0 ? rate : throw new ArgumentException("Rate must be positive", nameof(rate));
        Date = date.Date;
        Source = source;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateRate(decimal newRate, string source)
    {
        Rate = newRate > 0 ? newRate : throw new ArgumentException("Rate must be positive");
        Source = source;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }
}
