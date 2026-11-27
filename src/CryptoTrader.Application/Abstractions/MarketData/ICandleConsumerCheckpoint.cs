using CryptoTrader.Domain.MarketData;

namespace CryptoTrader.Application.Abstractions.MarketData;

/// <summary>
/// Tracks candles this pipeline has marked pending. Replay returns only those rows,
/// not the historical catalog. Implementations persist their own changes.
/// </summary>
public interface ICandleConsumerCheckpoint
{
    public const string PaperConsumer = "paper";

    Task MarkPendingAsync(
        Candle candle,
        string consumer,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken);

    Task MarkProcessedAsync(
        Candle candle,
        string consumer,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Candle>> ListPendingCandlesAsync(
        string consumer,
        int limit,
        CancellationToken cancellationToken);
}

public enum CandleConsumerState
{
    Pending = 1,
    Processed = 2,
}
