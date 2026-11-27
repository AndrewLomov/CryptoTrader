using CryptoTrader.Domain.Common;

namespace CryptoTrader.Domain.Strategies;

public sealed record TradeSignal
{
    private TradeSignal(
        Guid signalId,
        string strategyId,
        string strategyVersion,
        InstrumentId instrumentId,
        SignalAction action,
        DateTimeOffset occurredAtUtc,
        Price referencePrice,
        Price? stopLossPrice,
        Price? takeProfitPrice,
        decimal? atr,
        string explanation)
    {
        SignalId = signalId;
        StrategyId = strategyId;
        StrategyVersion = strategyVersion;
        InstrumentId = instrumentId;
        Action = action;
        OccurredAtUtc = occurredAtUtc;
        ReferencePrice = referencePrice;
        StopLossPrice = stopLossPrice;
        TakeProfitPrice = takeProfitPrice;
        Atr = atr;
        Explanation = explanation;
    }

    public Guid SignalId { get; }

    public string StrategyId { get; }

    public string StrategyVersion { get; }

    public InstrumentId InstrumentId { get; }

    public SignalAction Action { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    public Price ReferencePrice { get; }

    public Price? StopLossPrice { get; }

    public Price? TakeProfitPrice { get; }

    public decimal? Atr { get; }

    public string Explanation { get; }

    public static TradeSignal Create(
        Guid signalId,
        string strategyId,
        string strategyVersion,
        InstrumentId instrumentId,
        SignalAction action,
        DateTimeOffset occurredAtUtc,
        Price referencePrice,
        Price? stopLossPrice,
        Price? takeProfitPrice,
        decimal? atr,
        string explanation)
    {
        if (signalId == Guid.Empty)
        {
            throw new DomainException("Signal id cannot be empty.");
        }

        Guard.Defined(action, nameof(action));
        occurredAtUtc = Guard.Utc(occurredAtUtc, nameof(occurredAtUtc));
        if (atr is < 0)
        {
            throw new DomainException("ATR cannot be negative.");
        }

        if (action == SignalAction.EnterLong)
        {
            if (stopLossPrice is null || stopLossPrice.Value.Value >= referencePrice.Value)
            {
                throw new DomainException("Enter long requires a stop below the reference price.");
            }

            if (takeProfitPrice is not null && takeProfitPrice.Value.Value <= referencePrice.Value)
            {
                throw new DomainException("Take profit must be above the reference price.");
            }
        }

        return new TradeSignal(
            signalId,
            Guard.Required(strategyId, nameof(strategyId)),
            Guard.Required(strategyVersion, nameof(strategyVersion)),
            instrumentId,
            action,
            occurredAtUtc,
            referencePrice,
            stopLossPrice,
            takeProfitPrice,
            atr,
            Guard.Required(explanation, nameof(explanation)));
    }
}
