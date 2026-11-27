using System.Collections.Concurrent;
using CryptoTrader.Contracts.Responses;

namespace CryptoTrader.Host.MarketData;

public sealed class MarketDataFeedHealth
{
    public const string Healthy = "Healthy";

    public const string Degraded = "Degraded";

    public const string Unhealthy = "Unhealthy";

    private readonly ConcurrentDictionary<FeedKey, FeedState> _feeds = new();

    public void Report(
        string exchange,
        string instrument,
        string timeframe,
        string status,
        string? detail,
        DateTimeOffset? lastEventUtc)
    {
        var key = new FeedKey(exchange, instrument, timeframe);
        _feeds.AddOrUpdate(
            key,
            _ => new FeedState(status, detail, lastEventUtc),
            (_, previous) => new FeedState(status, detail, lastEventUtc ?? previous.LastEventUtc));
    }

    public FeedHealthResponse Snapshot(TimeSpan staleAfter, DateTimeOffset utcNow)
    {
        var items = _feeds
            .ToArray()
            .OrderBy(pair => pair.Key.Exchange, StringComparer.Ordinal)
            .ThenBy(pair => pair.Key.Instrument, StringComparer.Ordinal)
            .ThenBy(pair => pair.Key.Timeframe, StringComparer.Ordinal)
            .Select(pair => ToItem(pair.Key, pair.Value, staleAfter, utcNow))
            .ToArray();
        var status = items.Any(item => item.Status == Unhealthy)
            ? Unhealthy
            : items.Any(item => item.Status == Degraded)
                ? Degraded
                : Healthy;
        return new FeedHealthResponse(status, items);
    }

    private static FeedHealthItemResponse ToItem(FeedKey key, FeedState state, TimeSpan staleAfter, DateTimeOffset utcNow)
    {
        var status = state.Status;
        var detail = state.Detail;
        if (status == Healthy && state.LastEventUtc is DateTimeOffset lastEvent && utcNow - lastEvent > staleAfter)
        {
            status = Degraded;
            detail = "No closed candle inside the stale window.";
        }

        return new FeedHealthItemResponse(key.Exchange, key.Instrument, key.Timeframe, status, detail, state.LastEventUtc);
    }

    private readonly record struct FeedKey(string Exchange, string Instrument, string Timeframe);

    private sealed record FeedState(string Status, string? Detail, DateTimeOffset? LastEventUtc);
}
