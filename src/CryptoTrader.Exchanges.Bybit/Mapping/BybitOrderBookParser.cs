using System.Text.Json;
using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.MarketData;
using CryptoTrader.Exchanges.Bybit.Rest;

namespace CryptoTrader.Exchanges.Bybit.Mapping;

internal readonly record struct ParsedOrderBook(
    long? UpdateId,
    List<(decimal Price, decimal Quantity)> Bids,
    List<(decimal Price, decimal Quantity)> Asks);

internal static class BybitOrderBookParser
{
    public static bool TryParse(JsonElement payload, out ParsedOrderBook book)
    {
        book = default;
        if (payload.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!TryReadSide(payload, "b", out var bids) || !TryReadSide(payload, "a", out var asks))
        {
            return false;
        }

        long? updateId = null;
        if (payload.TryGetProperty("u", out var update) && BybitJson.TryReadInt64(update, out var id))
        {
            updateId = id;
        }

        book = new ParsedOrderBook(updateId, bids, asks);
        return true;
    }

    public static OrderBookSnapshot Consistent(
        InstrumentId instrument,
        ParsedOrderBook book,
        DateTimeOffset exchangeTimeUtc,
        DateTimeOffset receivedAtUtc)
    {
        return new OrderBookSnapshot(
            ExchangeId.Bybit,
            instrument,
            ToLevels(book.Bids, bids: true),
            ToLevels(book.Asks, bids: false),
            book.UpdateId,
            true,
            exchangeTimeUtc,
            receivedAtUtc);
    }

    public static OrderBookSnapshot Inconsistent(
        InstrumentId instrument,
        long? sequence,
        DateTimeOffset exchangeTimeUtc,
        DateTimeOffset receivedAtUtc)
    {
        return new OrderBookSnapshot(
            ExchangeId.Bybit,
            instrument,
            [],
            [],
            sequence,
            false,
            exchangeTimeUtc,
            receivedAtUtc);
    }

    private static bool TryReadSide(JsonElement payload, string name, out List<(decimal Price, decimal Quantity)> levels)
    {
        levels = [];
        if (!payload.TryGetProperty(name, out var side) || side.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var level in side.EnumerateArray())
        {
            if (level.ValueKind != JsonValueKind.Array || level.GetArrayLength() < 2)
            {
                return false;
            }

            if (!BybitJson.TryReadDecimal(level[0], out var price) || price <= 0m)
            {
                return false;
            }

            if (!BybitJson.TryReadDecimal(level[1], out var quantity) || quantity < 0m)
            {
                return false;
            }

            levels.Add((price, quantity));
        }

        return true;
    }

    private static OrderBookLevel[] ToLevels(List<(decimal Price, decimal Quantity)> levels, bool bids)
    {
        IEnumerable<(decimal Price, decimal Quantity)> ordered = bids
            ? levels.OrderByDescending(level => level.Price)
            : levels.OrderBy(level => level.Price);
        return ordered
            .Where(level => level.Quantity > 0m)
            .Select(level => new OrderBookLevel(level.Price, level.Quantity))
            .ToArray();
    }
}
