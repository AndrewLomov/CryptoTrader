using CryptoTrader.Domain.MarketData;

namespace CryptoTrader.Application.Abstractions.MarketData;

/// <summary>
/// Writes one closed candle and its pending checkpoint in a single database transaction.
/// A failure before commit leaves neither row behind.
/// </summary>
public interface ICandleIntake
{
    /// <summary>
    /// Returns false when the checkpoint is already Processed or Skipped.
    /// The candle upsert and the checkpoint write commit together.
    /// </summary>
    Task<bool> PersistPendingAsync(
        Candle candle,
        string consumer,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken);
}
