using CryptoTrader.Domain.Common;

namespace CryptoTrader.Domain.MarketData;

public static class CandleGapDetector
{
    public static IReadOnlyList<DateTimeOffset> FindMissingOpenTimes(IReadOnlyList<Candle> candles)
    {
        ArgumentNullException.ThrowIfNull(candles);
        if (candles.Count == 0)
        {
            return [];
        }

        var first = candles[0];
        if (first.Timeframe.Duration <= TimeSpan.Zero)
        {
            throw new DomainException("Timeframe duration must be positive.");
        }

        if (candles.Count == 1)
        {
            return [];
        }

        var missing = new List<DateTimeOffset>();
        for (var index = 1; index < candles.Count; index++)
        {
            var previous = candles[index - 1];
            var current = candles[index];
            EnsureSameSeries(first, current);
            if (current.OpenTimeUtc <= previous.OpenTimeUtc)
            {
                throw new DomainException("Candles must be strictly ordered by open time without duplicates.");
            }

            var step = previous.Timeframe.Duration;
            var expected = previous.OpenTimeUtc.Add(step);
            while (expected < current.OpenTimeUtc)
            {
                missing.Add(expected);
                var next = expected.Add(step);
                if (next <= expected)
                {
                    throw new DomainException("Timeframe duration must be positive.");
                }

                expected = next;
            }
        }

        return missing;
    }

    private static void EnsureSameSeries(Candle first, Candle current)
    {
        if (current.Exchange != first.Exchange
            || current.Instrument != first.Instrument
            || current.Timeframe != first.Timeframe)
        {
            throw new DomainException("Candle gap detection requires one exchange, instrument, and timeframe.");
        }
    }
}
