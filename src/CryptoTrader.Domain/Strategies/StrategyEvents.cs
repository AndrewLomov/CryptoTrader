using CryptoTrader.Domain.Common;

namespace CryptoTrader.Domain.Strategies;

public sealed record TradeSignalGenerated : DomainEvent
{
    public TradeSignalGenerated(
        Guid signalId,
        string strategyId,
        string strategyVersion,
        InstrumentId instrumentId,
        SignalAction action,
        DateTimeOffset occurredAtUtc,
        Guid? correlationId = null,
        Guid? causationId = null)
        : base(occurredAtUtc, correlationId, causationId)
    {
        if (signalId == Guid.Empty)
        {
            throw new DomainException("Signal id cannot be empty.");
        }

        SignalId = signalId;
        StrategyId = Guard.Required(strategyId, nameof(strategyId));
        StrategyVersion = Guard.Required(strategyVersion, nameof(strategyVersion));
        InstrumentId = instrumentId;
        Action = action;
        Guard.Defined(action, nameof(action));
    }

    public Guid SignalId { get; init; }

    public string StrategyId { get; init; }

    public string StrategyVersion { get; init; }

    public InstrumentId InstrumentId { get; init; }

    public SignalAction Action { get; init; }
}
