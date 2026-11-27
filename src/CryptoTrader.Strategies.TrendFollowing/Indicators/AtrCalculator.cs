using CryptoTrader.Domain.MarketData;

namespace CryptoTrader.Strategies.TrendFollowing.Indicators;

/// <summary>
/// Wilder ATR. True range uses the previous close, so index 0 has no true range.
/// The first ATR is the SMA of the first <paramref name="period"/> true ranges and is stored at index <paramref name="period"/>.
/// </summary>
public static class AtrCalculator
{
    public static IReadOnlyList<decimal?> Compute(IReadOnlyList<Candle> candles, int period)
    {
        ArgumentNullException.ThrowIfNull(candles);
        if (period < 1)
        {
            throw new ArgumentException("Period must be at least 1.", nameof(period));
        }

        var result = new decimal?[candles.Count];
        if (candles.Count <= period)
        {
            return result;
        }

        decimal sum = 0;
        for (var i = 1; i <= period; i++)
        {
            sum += TrueRange(candles[i], candles[i - 1].Close);
        }

        var atr = sum / period;
        result[period] = atr;

        for (var i = period + 1; i < candles.Count; i++)
        {
            var trueRange = TrueRange(candles[i], candles[i - 1].Close);
            atr = ((atr * (period - 1)) + trueRange) / period;
            result[i] = atr;
        }

        return result;
    }

    private static decimal TrueRange(Candle candle, decimal previousClose)
    {
        var highLow = candle.High - candle.Low;
        var highPrevious = Math.Abs(candle.High - previousClose);
        var lowPrevious = Math.Abs(candle.Low - previousClose);
        return Math.Max(highLow, Math.Max(highPrevious, lowPrevious));
    }
}
