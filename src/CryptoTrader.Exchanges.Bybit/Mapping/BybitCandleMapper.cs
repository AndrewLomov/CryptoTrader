using System.Text.Json;
using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.MarketData;
using CryptoTrader.Exchanges.Bybit.Rest;

namespace CryptoTrader.Exchanges.Bybit.Mapping;

internal readonly record struct BybitKlineRow(long OpenTimeMs, Candle? Candle);

internal static class BybitCandleMapper
{
    public static BybitKlineRow Map(
        JsonElement row,
        InstrumentId instrument,
        Timeframe timeframe,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc)
    {
        if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() < 6)
        {
            throw new InvalidOperationException("Bybit kline row could not be parsed.");
        }

        if (!BybitJson.TryReadInt64(row[0], out var openMs)
            || !BybitJson.TryReadDecimal(row[1], out var open)
            || !BybitJson.TryReadDecimal(row[2], out var high)
            || !BybitJson.TryReadDecimal(row[3], out var low)
            || !BybitJson.TryReadDecimal(row[4], out var close)
            || !BybitJson.TryReadDecimal(row[5], out var volume))
        {
            throw new InvalidOperationException("Bybit kline row could not be parsed.");
        }

        var openTime = DateTimeOffset.FromUnixTimeMilliseconds(openMs);
        var closeTime = openTime + timeframe.Duration;
        if (openTime < fromUtc || closeTime > toUtc)
        {
            return new BybitKlineRow(openMs, null);
        }

        var candle = Candle.Create(
            ExchangeId.Bybit,
            instrument,
            timeframe,
            openTime,
            closeTime,
            open,
            high,
            low,
            close,
            volume,
            isClosed: true);
        return new BybitKlineRow(openMs, candle);
    }
}
