using CryptoTrader.Application.Abstractions.MarketData;
using CryptoTrader.Application.Abstractions.Persistence;
using CryptoTrader.Domain.MarketData;
using Microsoft.EntityFrameworkCore;

namespace CryptoTrader.Infrastructure.Persistence.Repositories;

internal sealed class CandleIntake : ICandleIntake
{
    internal const string CrashBeforeCommitMessage = "Candle intake stopped before commit.";

    private readonly CryptoTraderDbContext _db;
    private readonly ICandleRepository _candles;
    private readonly ICandleConsumerCheckpoint _checkpoint;

    public CandleIntake(
        CryptoTraderDbContext db,
        ICandleRepository candles,
        ICandleConsumerCheckpoint checkpoint)
    {
        _db = db;
        _candles = candles;
        _checkpoint = checkpoint;
    }

    internal bool CrashBeforeCommit { get; set; }

    public async Task<bool> PersistPendingAsync(
        Candle candle,
        string consumer,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candle);
        if (_db.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException("Candle intake starts its own transaction.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await _candles.UpsertRangeAsync([candle], cancellationToken);
        var pending = await _checkpoint.MarkPendingAsync(candle, consumer, updatedAtUtc, cancellationToken);
        if (CrashBeforeCommit)
        {
            throw new InvalidOperationException(CrashBeforeCommitMessage);
        }

        await transaction.CommitAsync(cancellationToken);
        return pending;
    }
}
