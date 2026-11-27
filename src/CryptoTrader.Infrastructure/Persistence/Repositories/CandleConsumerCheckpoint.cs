using CryptoTrader.Application.Abstractions.MarketData;
using CryptoTrader.Domain.MarketData;
using CryptoTrader.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CryptoTrader.Infrastructure.Persistence.Repositories;

internal sealed class CandleConsumerCheckpoint : ICandleConsumerCheckpoint
{
    private readonly CryptoTraderDbContext _db;

    public CandleConsumerCheckpoint(CryptoTraderDbContext db)
    {
        _db = db;
    }

    public Task<bool> MarkPendingAsync(
        Candle candle,
        string consumer,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken)
    {
        return UpsertAsync(candle, consumer, CandleConsumerState.Pending, reason: null, updatedAtUtc, cancellationToken);
    }

    public Task MarkProcessedAsync(
        Candle candle,
        string consumer,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken)
    {
        return UpsertAsync(candle, consumer, CandleConsumerState.Processed, reason: null, updatedAtUtc, cancellationToken);
    }

    public Task MarkSkippedAsync(
        Candle candle,
        string consumer,
        string reason,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return UpsertAsync(candle, consumer, CandleConsumerState.Skipped, reason.Trim(), updatedAtUtc, cancellationToken);
    }

    public async Task<IReadOnlyList<Candle>> ListPendingCandlesAsync(
        string consumer,
        int limit,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consumer);
        if (limit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        var pending = (int)CandleConsumerState.Pending;
        var rows = await (
            from checkpoint in _db.CandleConsumerCheckpoints.AsNoTracking()
            join candle in _db.Candles.AsNoTracking()
                on new
                {
                    checkpoint.ExchangeId,
                    checkpoint.InstrumentId,
                    checkpoint.Timeframe,
                    checkpoint.OpenTimeUtc,
                }
                equals new
                {
                    candle.ExchangeId,
                    candle.InstrumentId,
                    candle.Timeframe,
                    candle.OpenTimeUtc,
                }
            where checkpoint.Consumer == consumer && checkpoint.State == pending
            orderby checkpoint.OpenTimeUtc, checkpoint.ExchangeId, checkpoint.InstrumentId, checkpoint.Timeframe
            select candle)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var candles = new Candle[rows.Count];
        for (var index = 0; index < rows.Count; index++)
        {
            candles[index] = CandleRepository.ToCandle(rows[index]);
        }

        return candles;
    }

    private async Task<bool> UpsertAsync(
        Candle candle,
        string consumer,
        CandleConsumerState state,
        string? reason,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candle);
        ArgumentException.ThrowIfNullOrWhiteSpace(consumer);

        var exchangeId = candle.Exchange.Value;
        var instrumentId = candle.Instrument.Value;
        var timeframe = candle.Timeframe.Code;
        var openTimeUtc = candle.OpenTimeUtc;
        var row = await _db.CandleConsumerCheckpoints.FindAsync(
            [exchangeId, instrumentId, timeframe, openTimeUtc, consumer],
            cancellationToken);
        var current = row is null ? (CandleConsumerState?)null : (CandleConsumerState)row.State;
        if (current is CandleConsumerState.Processed or CandleConsumerState.Skipped)
        {
            return false;
        }

        var skipReason = state == CandleConsumerState.Skipped ? TrimReason(reason) : null;
        if (row is null)
        {
            _db.CandleConsumerCheckpoints.Add(new CandleConsumerCheckpointRow
            {
                ExchangeId = exchangeId,
                InstrumentId = instrumentId,
                Timeframe = timeframe,
                OpenTimeUtc = openTimeUtc,
                Consumer = consumer,
                State = (int)state,
                SkipReason = skipReason,
                UpdatedAtUtc = updatedAtUtc,
            });
        }
        else
        {
            row.State = (int)state;
            row.SkipReason = skipReason;
            row.UpdatedAtUtc = updatedAtUtc;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return state == CandleConsumerState.Pending;
    }

    private static string TrimReason(string? reason)
    {
        var text = string.IsNullOrWhiteSpace(reason) ? "Skipped." : reason.Trim();
        return text.Length <= 512 ? text : text[..512];
    }
}
