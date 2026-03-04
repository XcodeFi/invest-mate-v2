using InvestmentApp.Domain.ValueObjects;

namespace InvestmentApp.Domain.ValueObjects;

public class Position : IEquatable<Position>
{
    public StockSymbol Symbol { get; private set; }
    public decimal Quantity { get; private set; }
    public Money AverageCost { get; private set; }
    public Money CurrentValue { get; private set; }
    public Money UnrealizedPnL { get; private set; }
    public decimal UnrealizedPnLPercentage { get; private set; }

    private Position() { }

    public Position(StockSymbol symbol, decimal quantity, Money averageCost, Money currentPrice)
    {
        Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
        Quantity = quantity;
        AverageCost = averageCost ?? throw new ArgumentNullException(nameof(averageCost));

        UpdateCurrentValue(currentPrice);
    }

    public void UpdateCurrentValue(Money currentPrice)
    {
        CurrentValue = new Money(Quantity * currentPrice.Amount, currentPrice.Currency);
        UnrealizedPnL = new Money(CurrentValue.Amount - (Quantity * AverageCost.Amount), currentPrice.Currency);
        UnrealizedPnLPercentage = AverageCost.Amount > 0 ? (UnrealizedPnL.Amount / (Quantity * AverageCost.Amount)) * 100 : 0;
    }

    public void AddShares(decimal additionalQuantity, Money price)
    {
        if (additionalQuantity <= 0)
            throw new ArgumentException("Additional quantity must be positive", nameof(additionalQuantity));

        var totalCost = (Quantity * AverageCost.Amount) + (additionalQuantity * price.Amount);
        var totalQuantity = Quantity + additionalQuantity;

        AverageCost = new Money(totalCost / totalQuantity, AverageCost.Currency);
        Quantity = totalQuantity;
    }

    public void RemoveShares(decimal quantityToRemove)
    {
        if (quantityToRemove <= 0)
            throw new ArgumentException("Quantity to remove must be positive", nameof(quantityToRemove));

        if (quantityToRemove > Quantity)
            throw new InvalidOperationException("Cannot remove more shares than owned");

        Quantity -= quantityToRemove;
    }

    public bool IsClosed => Quantity == 0;

    public override bool Equals(object? obj) => Equals(obj as Position);

    public bool Equals(Position? other)
    {
        if (other is null) return false;
        return Symbol.Equals(other.Symbol);
    }

    public override int GetHashCode() => Symbol.GetHashCode();

    public static bool operator ==(Position left, Position right) => left.Equals(right);
    public static bool operator !=(Position left, Position right) => !left.Equals(right);
}