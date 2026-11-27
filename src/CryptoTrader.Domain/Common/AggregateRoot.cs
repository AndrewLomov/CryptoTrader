namespace CryptoTrader.Domain.Common;

public interface IHasDomainEvents
{
    IReadOnlyList<DomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}

public abstract class AggregateRoot : IHasDomainEvents
{
    private readonly List<DomainEvent> _domainEvents = [];

    public IReadOnlyList<DomainEvent> DomainEvents => _domainEvents;

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void Raise(DomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }
}
