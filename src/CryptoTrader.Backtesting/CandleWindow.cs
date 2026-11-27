using System.Collections;
using CryptoTrader.Domain.MarketData;

namespace CryptoTrader.Backtesting;

internal static class CandleWindow
{
    public static List<Candle> Working(IReadOnlyList<Candle> candles, DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        ArgumentNullException.ThrowIfNull(candles);
        var selected = new List<Candle>();
        foreach (var candle in candles)
        {
            if (!candle.IsClosed || candle.OpenTimeUtc < fromUtc || candle.CloseTimeUtc > toUtc)
            {
                continue;
            }

            selected.Add(candle);
        }

        return DedupeStable(selected);
    }

    public static List<Candle> Higher(IReadOnlyList<Candle> candles)
    {
        ArgumentNullException.ThrowIfNull(candles);
        var selected = new List<Candle>();
        foreach (var candle in candles)
        {
            if (!candle.IsClosed)
            {
                continue;
            }

            selected.Add(candle);
        }

        return DedupeStable(selected);
    }

    public static bool IsCloseTimeOrdered(IReadOnlyList<Candle> candles)
    {
        for (var index = 1; index < candles.Count; index++)
        {
            if (candles[index].CloseTimeUtc < candles[index - 1].CloseTimeUtc)
            {
                return false;
            }
        }

        return true;
    }

    public static int CountClosedBy(IReadOnlyList<Candle> candles, DateTimeOffset asOfUtc)
    {
        var lower = 0;
        var upper = candles.Count;
        while (lower < upper)
        {
            var mid = lower + ((upper - lower) / 2);
            if (candles[mid].CloseTimeUtc <= asOfUtc)
            {
                lower = mid + 1;
            }
            else
            {
                upper = mid;
            }
        }

        return lower;
    }

    public static List<Candle> ClosedBy(IReadOnlyList<Candle> candles, DateTimeOffset asOfUtc)
    {
        var selected = new List<Candle>();
        foreach (var candle in candles)
        {
            if (candle.CloseTimeUtc <= asOfUtc)
            {
                selected.Add(candle);
            }
        }

        return selected;
    }

    private static List<Candle> DedupeStable(List<Candle> candles)
    {
        var ordered = candles.OrderBy(candle => candle.OpenTimeUtc).ToList();
        var result = new List<Candle>(ordered.Count);
        DateTimeOffset? seenOpen = null;
        foreach (var candle in ordered)
        {
            if (seenOpen == candle.OpenTimeUtc)
            {
                continue;
            }

            seenOpen = candle.OpenTimeUtc;
            result.Add(candle);
        }

        return result;
    }
}

internal sealed class ReadOnlyPrefix<T> : IReadOnlyList<T>
{
    private readonly IReadOnlyList<T> _source;
    private readonly int _count;

    public ReadOnlyPrefix(IReadOnlyList<T> source, int count)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (count < 0 || count > source.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        _source = source;
        _count = count;
    }

    public int Count => _count;

    public T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _source[index];
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        for (var index = 0; index < _count; index++)
        {
            yield return _source[index];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
