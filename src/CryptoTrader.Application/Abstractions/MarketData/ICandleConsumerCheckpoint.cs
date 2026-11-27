using CryptoTrader.Domain.MarketData;

namespace CryptoTrader.Application.Abstractions.MarketData;

/// <summary>
/// Tracks candles this pipeline has marked pending. Replay returns only those rows,
/// not the historical catalog. Implementations persist their own changes.
/// </summary>
public interface ICandleConsumerCheckpoint
{
    public const string PaperConsumer = "paper";

    /// <summary>
    /// Marks the candle pending. Returns false when the row is already Processed or Skipped
    /// so a duplicate delivery cannot reopen a terminal candle.
    /// </summary>
    Task<bool> MarkPendingAsync(
        Candle candle,
        string consumer,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken);

    Task MarkProcessedAsync(
        Candle candle,
        string consumer,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken);

    Task MarkSkippedAsync(
        Candle candle,
        string consumer,
        string reason,
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
    Skipped = 3,
}
