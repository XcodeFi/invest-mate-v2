using System;
using System.Collections.Generic;
using InvestmentApp.Domain.Events;

namespace InvestmentApp.Domain.Entities;

public abstract class AggregateRoot
{
    public string Id { get; protected set; } = null!;
    public int Version { get; protected set; }

    private readonly List<IDomainEvent> _domainEvents = new();

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    protected void IncrementVersion()
    {
        Version++;
    }
}