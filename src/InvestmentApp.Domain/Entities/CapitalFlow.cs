using System;
using MongoDB.Bson.Serialization.Attributes;

namespace InvestmentApp.Domain.Entities;

/// <summary>
/// Represents a capital flow event (deposit, withdrawal, dividend, etc.)
/// </summary>
public class CapitalFlow : AggregateRoot
{
    public string PortfolioId { get; private set; }
    public string UserId { get; private set; }
    public CapitalFlowType Type { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; }
    public string? Note { get; private set; }
    public DateTime FlowDate { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public bool IsSeedDeposit { get; private set; }

    [BsonConstructor]
    public CapitalFlow() { } // For MongoDB

    public CapitalFlow(string portfolioId, string userId, CapitalFlowType type, decimal amount, string currency = "VND", string? note = null, DateTime? flowDate = null, bool isSeedDeposit = false)
    {
        Id = Guid.NewGuid().ToString();
        PortfolioId = portfolioId ?? throw new ArgumentNullException(nameof(portfolioId));
        UserId = userId ?? throw new ArgumentNullException(nameof(userId));
        Type = type;
        Amount = amount > 0 ? amount : throw new ArgumentException("Amount must be positive", nameof(amount));
        Currency = currency ?? "VND";
        Note = note;
        FlowDate = flowDate?.Date ?? DateTime.UtcNow.Date;
        CreatedAt = DateTime.UtcNow;
        IsSeedDeposit = isSeedDeposit;
    }

    public void UpdateNote(string? note)
    {
        Note = note;
    }

    /// <summary>
    /// Returns the signed amount: positive for inflows, negative for outflows.
    /// </summary>
    public decimal SignedAmount => Type switch
    {
        CapitalFlowType.Deposit => Amount,
        CapitalFlowType.Dividend => Amount,
        CapitalFlowType.Interest => Amount,
        CapitalFlowType.Withdraw => -Amount,
        CapitalFlowType.Fee => -Amount,
        _ => Amount
    };
}

public enum CapitalFlowType
{
    Deposit,
    Withdraw,
    Dividend,
    Interest,
    Fee
}
