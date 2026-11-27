using System.Text.Json;
using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.MarketData;
using CryptoTrader.Exchanges.Okx.Rest;

namespace CryptoTrader.Exchanges.Okx.Mapping;

internal static class OkxOrderBookParser
{
    public static bool IsSnapshotMessage(JsonElement root)
    {
        if (!root.TryGetProperty("action", out var action))
        {
            return true;
        }

        return action.ValueKind == JsonValueKind.String
            && string.Equals(action.GetString(), "snapshot", StringComparison.Ordinal);
    }

    public static OrderBookSnapshot Parse(
        JsonElement book,
        InstrumentId instrument,
        DateTimeOffset receivedAtUtc,
        TimeProvider time)
    {
        var exchangeTime = OkxJson.ReadUnixTime(book, "ts", time);
        long? sequence = null;
        if (book.ValueKind == JsonValueKind.Object
            && book.TryGetProperty("seqId", out var seq)
            && OkxJson.TryReadInt64(seq, out var seqId))
        {
            sequence = seqId;
        }

        if (book.ValueKind != JsonValueKind.Object
            || !TryReadSide(book, "bids", out var bids)
            || !TryReadSide(book, "asks", out var asks)
            || HasDuplicatePrice(bids)
            || HasDuplicatePrice(asks))
        {
            return Inconsistent(instrument, sequence, exchangeTime, receivedAtUtc);
        }

        var bidLevels = ToLevels(bids, descending: true);
        var askLevels = ToLevels(asks, descending: false);
        if (bidLevels.Length == 0 && askLevels.Length == 0)
        {
            return Inconsistent(instrument, sequence, exchangeTime, receivedAtUtc);
        }

        return new OrderBookSnapshot(
            OkxExchangeAdapter.Id,
            instrument,
            bidLevels,
            askLevels,
            sequence,
            isConsistent: true,
            exchangeTime,
            receivedAtUtc);
    }

    private static OrderBookSnapshot Inconsistent(
        InstrumentId instrument,
        long? sequence,
        DateTimeOffset exchangeTimeUtc,
        DateTimeOffset receivedAtUtc)
    {
        return new OrderBookSnapshot(
            OkxExchangeAdapter.Id,
            instrument,
            [],
            [],
            sequence,
            isConsistent: false,
            exchangeTimeUtc,
            receivedAtUtc);
    }

    private static bool TryReadSide(JsonElement book, string name, out List<(decimal Price, decimal Quantity)> levels)
    {
        levels = [];
        if (!book.TryGetProperty(name, out var side) || side.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var level in side.EnumerateArray())
        {
            if (level.ValueKind != JsonValueKind.Array || level.GetArrayLength() < 2)
            {
                return false;
            }

            if (!OkxJson.TryReadDecimal(level[0], out var price) || price <= 0m)
            {
                return false;
            }

            if (!OkxJson.TryReadDecimal(level[1], out var quantity) || quantity < 0m)
            {
                return false;
            }

            levels.Add((price, quantity));
        }

        return true;
    }

    private static bool HasDuplicatePrice(List<(decimal Price, decimal Quantity)> levels)
    {
        var seen = new HashSet<decimal>();
        foreach (var level in levels)
        {
            if (level.Quantity == 0m)
            {
                continue;
            }

            if (!seen.Add(level.Price))
            {
                return true;
            }
        }

        return false;
    }

    private static OrderBookLevel[] ToLevels(List<(decimal Price, decimal Quantity)> levels, bool descending)
    {
        IEnumerable<(decimal Price, decimal Quantity)> ordered = descending
            ? levels.OrderByDescending(level => level.Price)
            : levels.OrderBy(level => level.Price);
        return ordered
            .Where(level => level.Quantity > 0m)
            .Select(level => new OrderBookLevel(level.Price, level.Quantity))
            .ToArray();
    }
}
