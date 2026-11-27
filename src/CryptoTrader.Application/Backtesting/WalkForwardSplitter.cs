namespace CryptoTrader.Application.Backtesting;

/// <summary>
/// Returns the in-sample boundary. In-sample is [from, boundary) and out-of-sample starts at the boundary.
/// </summary>
public static class WalkForwardSplitter
{
    public static DateTimeOffset Split(DateTimeOffset from, DateTimeOffset to, decimal inSampleFraction)
    {
        if (from.Offset != TimeSpan.Zero || to.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Walk-forward bounds must be UTC.");
        }

        if (to <= from)
        {
            throw new ArgumentOutOfRangeException(nameof(to), "The end of the range must be after the start.");
        }

        if ((to - from).Ticks < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(to), "The range is too short to split.");
        }

        if (inSampleFraction <= 0m || inSampleFraction >= 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(inSampleFraction), "In-sample fraction must be greater than 0 and less than 1.");
        }

        var ticks = decimal.Floor((decimal)(to - from).Ticks * inSampleFraction);
        if (ticks < 1m)
        {
            ticks = 1m;
        }

        var boundary = from.AddTicks((long)ticks);
        if (boundary >= to)
        {
            boundary = to.AddTicks(-1);
        }

        if (boundary <= from)
        {
            throw new ArgumentOutOfRangeException(nameof(inSampleFraction), "In-sample boundary is outside the range.");
        }

        return boundary;
    }
}
