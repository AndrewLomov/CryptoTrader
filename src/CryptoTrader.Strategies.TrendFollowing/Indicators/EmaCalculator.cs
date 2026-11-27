namespace CryptoTrader.Strategies.TrendFollowing.Indicators;

/// <summary>
/// Exponential moving average. Index <c>period - 1</c> is the SMA of the first <paramref name="period"/>
/// values. Earlier indexes are null. Later values do not use future inputs.
/// </summary>
public static class EmaCalculator
{
    public static IReadOnlyList<decimal?> Compute(IReadOnlyList<decimal> values, int period)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (period < 1)
        {
            throw new ArgumentException("Period must be at least 1.", nameof(period));
        }

        var result = new decimal?[values.Count];
        if (values.Count < period)
        {
            return result;
        }

        decimal sum = 0;
        for (var i = 0; i < period; i++)
        {
            sum += values[i];
        }

        var ema = sum / period;
        result[period - 1] = ema;
        var k = 2m / (period + 1m);
        for (var i = period; i < values.Count; i++)
        {
            ema = (values[i] * k) + (ema * (1m - k));
            result[i] = ema;
        }

        return result;
    }
}
