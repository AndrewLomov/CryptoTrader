using System.Globalization;
using CryptoTrader.Domain.MarketData;

namespace CryptoTrader.Application.Paper;

/// <summary>
/// Identity of one closed bar for paper idempotency. Exchange is part of the key so
/// Bybit and OKX bars that share an open time stay independent.
/// </summary>
public static class PaperCandleKey
{
    public static string For(Candle candle)
    {
        ArgumentNullException.ThrowIfNull(candle);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{candle.Exchange.Value}|{candle.Instrument.Value}|{candle.Timeframe.Code}|{candle.OpenTimeUtc.UtcDateTime:O}");
    }
}
