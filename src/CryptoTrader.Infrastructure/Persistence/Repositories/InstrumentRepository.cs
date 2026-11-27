using CryptoTrader.Application.Abstractions.Persistence;
using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.Instruments;
using CryptoTrader.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CryptoTrader.Infrastructure.Persistence.Repositories;

internal sealed class InstrumentRepository : IInstrumentRepository
{
    private readonly CryptoTraderDbContext _db;

    public InstrumentRepository(CryptoTraderDbContext db)
    {
        _db = db;
    }

    public async Task UpsertAsync(Instrument instrument, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instrument);
        await ExchangeCatalog.EnsureAsync(_db, instrument.Exchange.Value, cancellationToken);
        var id = instrument.Id.Value;
        var row = await _db.Instruments.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (row is null)
        {
            _db.Instruments.Add(ToRow(instrument));
        }
        else
        {
            Copy(instrument, row);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Instrument?> FindAsync(InstrumentId id, CancellationToken cancellationToken)
    {
        var key = id.Value;
        var row = await _db.Instruments.AsNoTracking().FirstOrDefaultAsync(item => item.Id == key, cancellationToken);
        return row is null ? null : ToInstrument(row);
    }

    public async Task<Instrument?> FindByExchangeSymbolAsync(
        ExchangeId exchange,
        string exchangeSymbol,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchangeSymbol);
        var exchangeId = exchange.Value;
        var symbol = exchangeSymbol.Trim();
        var row = await _db.Instruments
            .AsNoTracking()
            .Where(item => item.ExchangeId == exchangeId && item.ExchangeSymbol == symbol)
            .OrderBy(item => item.InstrumentType)
            .ThenBy(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : ToInstrument(row);
    }

    public async Task<IReadOnlyList<Instrument>> ListAsync(ExchangeId? exchange, CancellationToken cancellationToken)
    {
        var query = _db.Instruments.AsNoTracking().AsQueryable();
        if (exchange is { } exchangeId)
        {
            var value = exchangeId.Value;
            query = query.Where(item => item.ExchangeId == value);
        }

        var rows = await query
            .OrderBy(item => item.ExchangeId)
            .ThenBy(item => item.ExchangeSymbol)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        return rows.Select(ToInstrument).ToArray();
    }

    private static InstrumentRow ToRow(Instrument instrument) => new()
    {
        Id = instrument.Id.Value,
        ExchangeId = instrument.Exchange.Value,
        ExchangeSymbol = instrument.ExchangeSymbol,
        BaseAsset = instrument.BaseAsset,
        QuoteAsset = instrument.QuoteAsset,
        InstrumentType = (int)instrument.Type,
        PriceTick = instrument.PriceTick,
        QuantityStep = instrument.QuantityStep,
        MinQuantity = instrument.MinQuantity,
        MinNotional = instrument.MinNotional,
        ContractMultiplier = instrument.ContractMultiplier,
        ContractValue = instrument.ContractValue,
        ContractValueCurrency = instrument.ContractValueCurrency,
        SettlementCurrency = instrument.SettlementCurrency,
        ContractKind = instrument.ContractKind,
        IsTradingEnabled = instrument.IsTradingEnabled,
    };

    private static void Copy(Instrument instrument, InstrumentRow row)
    {
        row.ExchangeId = instrument.Exchange.Value;
        row.ExchangeSymbol = instrument.ExchangeSymbol;
        row.BaseAsset = instrument.BaseAsset;
        row.QuoteAsset = instrument.QuoteAsset;
        row.InstrumentType = (int)instrument.Type;
        row.PriceTick = instrument.PriceTick;
        row.QuantityStep = instrument.QuantityStep;
        row.MinQuantity = instrument.MinQuantity;
        row.MinNotional = instrument.MinNotional;
        row.ContractMultiplier = instrument.ContractMultiplier;
        row.ContractValue = instrument.ContractValue;
        row.ContractValueCurrency = instrument.ContractValueCurrency;
        row.SettlementCurrency = instrument.SettlementCurrency;
        row.ContractKind = instrument.ContractKind;
        row.IsTradingEnabled = instrument.IsTradingEnabled;
    }

    private static Instrument ToInstrument(InstrumentRow row) => Instrument.Create(
        new InstrumentId(row.Id),
        new ExchangeId(row.ExchangeId),
        row.ExchangeSymbol,
        row.BaseAsset,
        row.QuoteAsset,
        StoredEnum.FromInt32<InstrumentType>(row.InstrumentType, "instrument_type"),
        row.PriceTick,
        row.QuantityStep,
        row.MinQuantity,
        row.MinNotional,
        row.ContractMultiplier,
        row.IsTradingEnabled,
        row.ContractValue,
        row.ContractValueCurrency,
        row.SettlementCurrency,
        row.ContractKind);
}
