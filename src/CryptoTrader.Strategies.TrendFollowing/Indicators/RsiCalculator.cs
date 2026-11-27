namespace CryptoTrader.Strategies.TrendFollowing.Indicators;

/// <summary>
/// Wilder RSI. The first value is at index <paramref name="period"/>, after <paramref name="period"/> close-to-close changes.
/// Earlier indexes are null.
/// </summary>
public static class RsiCalculator
{
    public static IReadOnlyList<decimal?> Compute(IReadOnlyList<decimal> closes, int period)
    {
        ArgumentNullException.ThrowIfNull(closes);
        if (period < 1)
        {
            throw new ArgumentException("Period must be at least 1.", nameof(period));
        }

        var result = new decimal?[closes.Count];
        if (closes.Count <= period)
        {
            return result;
        }

        decimal gainSum = 0;
        decimal lossSum = 0;
        for (var i = 1; i <= period; i++)
        {
            var change = closes[i] - closes[i - 1];
            if (change > 0)
            {
                gainSum += change;
            }
            else
            {
                lossSum -= change;
            }
        }

        var avgGain = gainSum / period;
        var avgLoss = lossSum / period;
        result[period] = ToRsi(avgGain, avgLoss);

        for (var i = period + 1; i < closes.Count; i++)
        {
            var change = closes[i] - closes[i - 1];
            var gain = change > 0 ? change : 0m;
            var loss = change < 0 ? -change : 0m;
            avgGain = ((avgGain * (period - 1)) + gain) / period;
            avgLoss = ((avgLoss * (period - 1)) + loss) / period;
            result[i] = ToRsi(avgGain, avgLoss);
        }

        return result;
    }

    private static decimal ToRsi(decimal avgGain, decimal avgLoss)
    {
        if (avgLoss == 0)
        {
            return avgGain == 0 ? 50m : 100m;
        }

        return 100m - (100m / (1m + (avgGain / avgLoss)));
    }
}
