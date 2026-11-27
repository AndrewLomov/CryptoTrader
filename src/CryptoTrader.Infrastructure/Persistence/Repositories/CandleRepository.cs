using CryptoTrader.Application.Abstractions.Persistence;
using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.MarketData;
using CryptoTrader.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CryptoTrader.Infrastructure.Persistence.Repositories;

internal sealed class CandleRepository : ICandleRepository
{
    private const int LookupChunkSize = 500;

    private readonly CryptoTraderDbContext _db;

    public CandleRepository(CryptoTraderDbContext db)
    {
        _db = db;
    }

    public async Task UpsertRangeAsync(IReadOnlyList<Candle> candles, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candles);
        if (candles.Count == 0)
        {
            return;
        }

        var incoming = new Dictionary<CandleIdentity, Candle>(candles.Count);
        foreach (var candle in candles)
        {
            incoming[CandleIdentity.From(candle)] = candle;
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        foreach (var group in incoming.GroupBy(pair => (
            ExchangeId: pair.Key.ExchangeId,
            InstrumentId: pair.Key.InstrumentId,
            Timeframe: pair.Key.Timeframe)))
        {
            var exchangeId = group.Key.ExchangeId;
            var instrumentId = group.Key.InstrumentId;
            var timeframe = group.Key.Timeframe;
            var openTimes = group.Select(pair => pair.Key.OpenTimeUtc).ToArray();
            var existingByTime = new Dictionary<DateTimeOffset, CandleRow>();
            foreach (var timeChunk in openTimes.Chunk(LookupChunkSize))
            {
                var times = timeChunk.ToList();
                var rows = await _db.Candles
                    .Where(row => row.ExchangeId == exchangeId
                        && row.InstrumentId == instrumentId
                        && row.Timeframe == timeframe
                        && times.Contains(row.OpenTimeUtc))
                    .ToListAsync(cancellationToken);
                foreach (var row in rows)
                {
                    existingByTime[row.OpenTimeUtc] = row;
                }
            }

            foreach (var (identity, candle) in group)
            {
                if (existingByTime.TryGetValue(identity.OpenTimeUtc, out var row))
                {
                    row.Open = candle.Open;
                    row.High = candle.High;
                    row.Low = candle.Low;
                    row.Close = candle.Close;
                    row.Volume = candle.Volume;
                    row.CloseTimeUtc = candle.CloseTimeUtc;
                    row.IsClosed = candle.IsClosed;
                }
                else
                {
                    _db.Candles.Add(ToRow(candle));
                }
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Candle>> GetRangeAsync(
        ExchangeId exchange,
        InstrumentId instrument,
        Timeframe timeframe,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        var exchangeId = exchange.Value;
        var instrumentId = instrument.Value;
        var timeframeCode = timeframe.Code;
        var rows = await _db.Candles
            .AsNoTracking()
            .Where(row => row.ExchangeId == exchangeId
                && row.InstrumentId == instrumentId
                && row.Timeframe == timeframeCode
                && row.OpenTimeUtc >= fromUtc
                && row.OpenTimeUtc <= toUtc)
            .OrderBy(row => row.OpenTimeUtc)
            .ToListAsync(cancellationToken);

        var candles = new Candle[rows.Count];
        for (var index = 0; index < rows.Count; index++)
        {
            candles[index] = ToCandle(rows[index]);
        }

        return candles;
    }

    private static CandleRow ToRow(Candle candle) => new()
    {
        ExchangeId = candle.Exchange.Value,
        InstrumentId = candle.Instrument.Value,
        Timeframe = candle.Timeframe.Code,
        OpenTimeUtc = candle.OpenTimeUtc,
        CloseTimeUtc = candle.CloseTimeUtc,
        Open = candle.Open,
        High = candle.High,
        Low = candle.Low,
        Close = candle.Close,
        Volume = candle.Volume,
        IsClosed = candle.IsClosed,
    };

    private static Candle ToCandle(CandleRow row) => Candle.Create(
        new ExchangeId(row.ExchangeId),
        new InstrumentId(row.InstrumentId),
        Timeframe.Parse(row.Timeframe),
        row.OpenTimeUtc,
        row.CloseTimeUtc,
        row.Open,
        row.High,
        row.Low,
        row.Close,
        row.Volume,
        row.IsClosed);

    private readonly record struct CandleIdentity(string ExchangeId, string InstrumentId, string Timeframe, DateTimeOffset OpenTimeUtc)
    {
        public static CandleIdentity From(Candle candle) => new(
            candle.Exchange.Value,
            candle.Instrument.Value,
            candle.Timeframe.Code,
            candle.OpenTimeUtc);
    }
}
