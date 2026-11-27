using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.MarketData;
using CryptoTrader.Exchanges.Bybit.Mapping;

namespace CryptoTrader.Exchanges.Bybit.MarketData;

internal readonly record struct OrderBookApplyResult(bool Publish, bool NeedsReload, OrderBookSnapshot? Snapshot);

internal sealed class BybitOrderBookAssembler
{
    private static readonly IComparer<decimal> Descending = Comparer<decimal>.Create((left, right) => right.CompareTo(left));

    private readonly OrderBookSequence _sequence = new();
    private readonly SortedDictionary<decimal, decimal> _bids = new(Descending);
    private readonly SortedDictionary<decimal, decimal> _asks = new();

    public void Reset()
    {
        _sequence.Reset();
        _bids.Clear();
        _asks.Clear();
    }

    public void Install(OrderBookSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _bids.Clear();
        _asks.Clear();
        if (!snapshot.IsConsistent || snapshot.Sequence is not long updateId)
        {
            _sequence.Reset();
            return;
        }

        Copy(snapshot.Bids, _bids);
        Copy(snapshot.Asks, _asks);
        _sequence.AcceptSnapshot(updateId);
    }

    public OrderBookApplyResult Apply(
        string? type,
        ParsedOrderBook parsed,
        bool parsedOk,
        InstrumentId instrument,
        DateTimeOffset exchangeTimeUtc,
        DateTimeOffset receivedAtUtc)
    {
        if (!parsedOk || parsed.UpdateId is not long updateId)
        {
            var wasConsistent = _sequence.IsConsistent;
            Reset();
            if (!wasConsistent)
            {
                return new OrderBookApplyResult(false, true, null);
            }

            return Publish(
                BybitOrderBookParser.Inconsistent(instrument, parsed.UpdateId, exchangeTimeUtc, receivedAtUtc),
                needsReload: true);
        }

        if (string.Equals(type, "snapshot", StringComparison.OrdinalIgnoreCase))
        {
            _sequence.AcceptSnapshot(updateId);
            Replace(parsed);
            return Publish(Build(instrument, true, updateId, exchangeTimeUtc, receivedAtUtc), needsReload: false);
        }

        var decision = _sequence.Observe(updateId);
        switch (decision.Kind)
        {
            case OrderBookUpdateKind.DeltaApplied:
                Merge(parsed);
                return Publish(Build(instrument, true, updateId, exchangeTimeUtc, receivedAtUtc), needsReload: false);
            case OrderBookUpdateKind.Gap:
                _bids.Clear();
                _asks.Clear();
                return Publish(
                    BybitOrderBookParser.Inconsistent(instrument, updateId, exchangeTimeUtc, receivedAtUtc),
                    needsReload: true);
            case OrderBookUpdateKind.DeltaIgnored when !decision.IsConsistent:
                return new OrderBookApplyResult(false, true, null);
            default:
                return new OrderBookApplyResult(false, false, null);
        }
    }

    private void Replace(ParsedOrderBook parsed)
    {
        _bids.Clear();
        _asks.Clear();
        Merge(parsed);
    }

    private void Merge(ParsedOrderBook parsed)
    {
        ApplyLevels(parsed.Bids, _bids);
        ApplyLevels(parsed.Asks, _asks);
    }

    private static void ApplyLevels(List<(decimal Price, decimal Quantity)> levels, SortedDictionary<decimal, decimal> book)
    {
        foreach (var (price, quantity) in levels)
        {
            if (quantity == 0m)
            {
                book.Remove(price);
                continue;
            }

            book[price] = quantity;
        }
    }

    private static void Copy(IReadOnlyList<OrderBookLevel> levels, SortedDictionary<decimal, decimal> book)
    {
        foreach (var level in levels)
        {
            if (level.Quantity > 0m)
            {
                book[level.Price] = level.Quantity;
            }
        }
    }

    private OrderBookSnapshot Build(
        InstrumentId instrument,
        bool consistent,
        long sequence,
        DateTimeOffset exchangeTimeUtc,
        DateTimeOffset receivedAtUtc)
    {
        if (!consistent)
        {
            return BybitOrderBookParser.Inconsistent(instrument, sequence, exchangeTimeUtc, receivedAtUtc);
        }

        return new OrderBookSnapshot(
            ExchangeId.Bybit,
            instrument,
            ToLevels(_bids),
            ToLevels(_asks),
            sequence,
            true,
            exchangeTimeUtc,
            receivedAtUtc);
    }

    private static OrderBookApplyResult Publish(OrderBookSnapshot snapshot, bool needsReload) =>
        new(true, needsReload, snapshot);

    private static OrderBookLevel[] ToLevels(SortedDictionary<decimal, decimal> book)
    {
        var levels = new OrderBookLevel[book.Count];
        var index = 0;
        foreach (var pair in book)
        {
            levels[index++] = new OrderBookLevel(pair.Key, pair.Value);
        }

        return levels;
    }
}
