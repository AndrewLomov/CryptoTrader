using System.Text.Json;
using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.MarketData;
using CryptoTrader.Exchanges.Okx.Rest;

namespace CryptoTrader.Exchanges.Okx.Mapping;

internal static class OkxTickerMapper
{
    public static Ticker Parse(JsonElement item, InstrumentId instrument, DateTimeOffset receivedAtUtc, TimeProvider time)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            throw new OkxApiException("OKX ticker payload was not an object.");
        }

        if (!item.TryGetProperty("last", out var lastElement)
            || !OkxJson.TryReadDecimal(lastElement, out var last)
            || last <= 0m)
        {
            throw new OkxApiException("OKX ticker did not include a last price.");
        }

        return Ticker.Create(
            OkxExchangeAdapter.Id,
            instrument,
            OptionalPrice(item, "bidPx"),
            OptionalPrice(item, "askPx"),
            last,
            OkxJson.ReadUnixTime(item, "ts", time),
            receivedAtUtc);
    }

    private static decimal? OptionalPrice(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var element)
            || !OkxJson.TryReadDecimal(element, out var value)
            || value <= 0m)
        {
            return null;
        }

        return value;
    }
}
