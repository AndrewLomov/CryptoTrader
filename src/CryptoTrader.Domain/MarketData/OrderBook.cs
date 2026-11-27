using CryptoTrader.Domain.Common;

namespace CryptoTrader.Domain.MarketData;

public sealed record OrderBookLevel
{
    public OrderBookLevel(decimal price, decimal quantity)
    {
        if (price <= 0)
        {
            throw new DomainException("Order book price must be greater than zero.");
        }

        Guard.NotNegative(quantity, nameof(quantity));
        Price = price;
        Quantity = quantity;
    }

    public decimal Price { get; }

    public decimal Quantity { get; }
}

public sealed record OrderBookSnapshot
{
    public OrderBookSnapshot(
        ExchangeId exchange,
        InstrumentId instrument,
        IReadOnlyList<OrderBookLevel> bids,
        IReadOnlyList<OrderBookLevel> asks,
        long? sequence,
        bool isConsistent,
        DateTimeOffset exchangeTimeUtc,
        DateTimeOffset receivedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(bids);
        ArgumentNullException.ThrowIfNull(asks);
        Exchange = exchange;
        Instrument = instrument;
        Bids = bids;
        Asks = asks;
        Sequence = sequence;
        IsConsistent = isConsistent;
        ExchangeTimeUtc = Guard.Utc(exchangeTimeUtc, nameof(exchangeTimeUtc));
        ReceivedAtUtc = Guard.Utc(receivedAtUtc, nameof(receivedAtUtc));
    }

    public ExchangeId Exchange { get; }

    public InstrumentId Instrument { get; }

    public IReadOnlyList<OrderBookLevel> Bids { get; }

    public IReadOnlyList<OrderBookLevel> Asks { get; }

    public long? Sequence { get; }

    /// <summary>
    /// True when this snapshot's own levels parsed. It does not mean a sequence of deltas was applied
    /// without a gap. Callers that need a continuous book must not treat a parsed snapshot as one.
    /// </summary>
    public bool IsConsistent { get; }

    public DateTimeOffset ExchangeTimeUtc { get; }

    public DateTimeOffset ReceivedAtUtc { get; }

    public void EnsureTradable()
    {
        if (!IsConsistent)
        {
            throw new DomainException("Order book snapshot is inconsistent and cannot be used for trading.");
        }
    }
}
