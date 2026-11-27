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

        if (_db.Database.CurrentTransaction is not null)
        {
            await StageAsync(candles, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await StageAsync(candles, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task StageAsync(IReadOnlyList<Candle> candles, CancellationToken cancellationToken)
    {
        var incoming = new Dictionary<CandleIdentity, Candle>(candles.Count);
        foreach (var candle in candles)
        {
            incoming[CandleIdentity.From(candle)] = candle;
        }

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
                    row.VolumeUnit = (int)candle.VolumeUnit;
                    row.QuoteVolume = candle.QuoteVolume;
                    row.BaseVolume = candle.BaseVolume;
                    row.CloseTimeUtc = candle.CloseTimeUtc;
                    row.IsClosed = candle.IsClosed;
                }
                else
                {
                    _db.Candles.Add(ToRow(candle));
                }
            }
        }
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
        VolumeUnit = (int)candle.VolumeUnit,
        QuoteVolume = candle.QuoteVolume,
        BaseVolume = candle.BaseVolume,
        IsClosed = candle.IsClosed,
    };

    internal static Candle ToCandle(CandleRow row) => Candle.Create(
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
        row.IsClosed,
        StoredEnum.FromInt32<VolumeUnit>(row.VolumeUnit, "volume_unit"),
        row.QuoteVolume,
        row.BaseVolume);

    private readonly record struct CandleIdentity(string ExchangeId, string InstrumentId, string Timeframe, DateTimeOffset OpenTimeUtc)
    {
        public static CandleIdentity From(Candle candle) => new(
            candle.Exchange.Value,
            candle.Instrument.Value,
            candle.Timeframe.Code,
            candle.OpenTimeUtc);
    }
}
