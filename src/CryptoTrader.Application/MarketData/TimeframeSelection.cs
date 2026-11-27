using CryptoTrader.Application.Configuration;
using CryptoTrader.Domain.MarketData;

namespace CryptoTrader.Application.MarketData;

internal static class TimeframeSelection
{
    public static (Timeframe Working, Timeframe? Higher) ForBacktest(MarketDataOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var parsed = Parse(options);
        var working = parsed[0];
        return (working, NextLonger(parsed, working));
    }

    public static Timeframe? HigherThan(MarketDataOptions options, Timeframe working)
    {
        ArgumentNullException.ThrowIfNull(options);
        return NextLonger(Parse(options), working);
    }

    private static Timeframe[] Parse(MarketDataOptions options)
    {
        var parsed = options.DefaultTimeframes
            .Select(Timeframe.Parse)
            .Distinct()
            .OrderBy(timeframe => timeframe.Duration)
            .ToArray();
        if (parsed.Length == 0)
        {
            throw new InvalidOperationException("MarketData:DefaultTimeframes requires at least one timeframe.");
        }

        return parsed;
    }

    private static Timeframe? NextLonger(IReadOnlyList<Timeframe> timeframes, Timeframe working)
    {
        foreach (var timeframe in timeframes)
        {
            if (timeframe.Duration > working.Duration)
            {
                return timeframe;
            }
        }

        return null;
    }
}
