using System;

namespace InvestmentApp.Domain.ValueObjects;

public class StockSymbol : IEquatable<StockSymbol>
{
    public string Value { get; }

    public StockSymbol(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Stock symbol cannot be empty", nameof(value));

        Value = value.ToUpper().Trim();
    }

    public bool Equals(StockSymbol? other)
    {
        return other != null && Value == other.Value;
    }

    public override bool Equals(object? obj) => Equals(obj as StockSymbol);

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Value;

    public static bool operator ==(StockSymbol left, StockSymbol right) => left.Equals(right);
    public static bool operator !=(StockSymbol left, StockSymbol right) => !left.Equals(right);

    public static implicit operator string(StockSymbol symbol) => symbol.Value;
    public static explicit operator StockSymbol(string value) => new StockSymbol(value);
}