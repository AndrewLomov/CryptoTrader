namespace CryptoTrader.Domain.Common;

public abstract record DomainEvent
{
    protected DomainEvent(DateTimeOffset occurredAtUtc, Guid? correlationId, Guid? causationId)
    {
        EventId = Guid.NewGuid();
        OccurredAtUtc = Guard.Utc(occurredAtUtc, nameof(occurredAtUtc));
        CorrelationId = correlationId;
        CausationId = causationId;
    }

    public Guid EventId { get; init; }

    public DateTimeOffset OccurredAtUtc { get; init; }

    public Guid? CorrelationId { get; init; }

    public Guid? CausationId { get; init; }
}
