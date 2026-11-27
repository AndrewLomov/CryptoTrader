using System.Text.Json;
using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.MarketData;
using CryptoTrader.Exchanges.Okx.Rest;

namespace CryptoTrader.Exchanges.Okx.Mapping;

internal readonly record struct OkxCandleRow(long OpenTimeMs, bool IsClosed, Candle? Candle);

internal static class OkxCandleMapper
{
    public static OkxCandleRow Parse(
        JsonElement row,
        InstrumentId instrument,
        Timeframe timeframe,
        bool spot,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc)
    {
        if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() < 9)
        {
            throw new OkxApiException("OKX candle row is missing fields.");
        }

        if (!OkxJson.TryReadInt64(row[0], out var openMs)
            || !OkxJson.TryReadDecimal(row[1], out var open)
            || !OkxJson.TryReadDecimal(row[2], out var high)
            || !OkxJson.TryReadDecimal(row[3], out var low)
            || !OkxJson.TryReadDecimal(row[4], out var close)
            || !OkxJson.TryReadDecimal(row[5], out var volume))
        {
            throw new OkxApiException("OKX candle row could not be parsed.");
        }

        var confirm = row[8].ValueKind == JsonValueKind.String ? row[8].GetString() : null;
        if (confirm is not ("0" or "1"))
        {
            throw new OkxApiException("OKX candle confirm flag is missing.");
        }

        var closed = confirm == "1";
        if (volume < 0m)
        {
            throw new OkxApiException("OKX candle volume is negative.");
        }

        DateTimeOffset openTime;
        try
        {
            openTime = DateTimeOffset.FromUnixTimeMilliseconds(openMs);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new OkxApiException("OKX candle open time is invalid.", exception);
        }

        if (!closed || openTime < fromUtc)
        {
            return new OkxCandleRow(openMs, closed, null);
        }

        DateTimeOffset closeTime;
        try
        {
            closeTime = openTime + timeframe.Duration;
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new OkxApiException("OKX candle close time is invalid.", exception);
        }

        if (closeTime > toUtc)
        {
            return new OkxCandleRow(openMs, closed, null);
        }

        decimal? quoteVolume = spot
            ? OptionalVolume(row[6]) ?? OptionalVolume(row[7])
            : OptionalVolume(row[7]);
        decimal? baseVolume = spot ? null : OptionalVolume(row[6]);
        try
        {
            var candle = Candle.Create(
                OkxExchangeAdapter.Id,
                instrument,
                timeframe,
                openTime,
                closeTime,
                open,
                high,
                low,
                close,
                volume,
                isClosed: true,
                volumeUnit: spot ? VolumeUnit.BaseAsset : VolumeUnit.Contract,
                quoteVolume: quoteVolume,
                baseVolume: baseVolume);
            return new OkxCandleRow(openMs, true, candle);
        }
        catch (DomainException exception)
        {
            throw new OkxApiException("OKX candle row failed validation.", exception);
        }
    }

    public static bool TryParse(
        JsonElement row,
        InstrumentId instrument,
        Timeframe timeframe,
        bool spot,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        out OkxCandleRow parsed)
    {
        try
        {
            parsed = Parse(row, instrument, timeframe, spot, fromUtc, toUtc);
            return true;
        }
        catch (OkxApiException)
        {
            parsed = default;
            return false;
        }
    }

    private static decimal? OptionalVolume(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(element.GetString()))
        {
            return null;
        }

        if (element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (!OkxJson.TryReadDecimal(element, out var value))
        {
            throw new OkxApiException("OKX candle volume could not be parsed.");
        }

        if (value < 0m)
        {
            throw new OkxApiException("OKX candle volume is negative.");
        }

        return value;
    }
}
