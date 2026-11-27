namespace CryptoTrader.Exchanges.Bybit.MarketData;

internal enum OrderBookUpdateKind
{
    SnapshotAccepted = 1,
    DeltaApplied = 2,
    DeltaIgnored = 3,
    Gap = 4,
}

internal readonly record struct OrderBookUpdateDecision(OrderBookUpdateKind Kind, long? LastUpdateId, bool IsConsistent);

/// <summary>
/// Pure update-id tracker. It does not read the clock or the network.
/// </summary>
internal sealed class OrderBookSequence
{
    public long? LastUpdateId { get; private set; }

    public bool IsConsistent { get; private set; }

    public void Reset()
    {
        LastUpdateId = null;
        IsConsistent = false;
    }

    public OrderBookUpdateDecision AcceptSnapshot(long updateId)
    {
        LastUpdateId = updateId;
        IsConsistent = true;
        return new OrderBookUpdateDecision(OrderBookUpdateKind.SnapshotAccepted, LastUpdateId, true);
    }

    public OrderBookUpdateDecision Observe(long updateId)
    {
        if (!IsConsistent || LastUpdateId is null)
        {
            return new OrderBookUpdateDecision(OrderBookUpdateKind.DeltaIgnored, LastUpdateId, false);
        }

        if (updateId <= LastUpdateId.Value)
        {
            return new OrderBookUpdateDecision(OrderBookUpdateKind.DeltaIgnored, LastUpdateId, true);
        }

        if (updateId == LastUpdateId.Value + 1)
        {
            LastUpdateId = updateId;
            return new OrderBookUpdateDecision(OrderBookUpdateKind.DeltaApplied, LastUpdateId, true);
        }

        IsConsistent = false;
        return new OrderBookUpdateDecision(OrderBookUpdateKind.Gap, updateId, false);
    }
}
