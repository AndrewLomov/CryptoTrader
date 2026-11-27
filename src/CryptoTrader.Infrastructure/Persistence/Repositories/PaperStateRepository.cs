using CryptoTrader.Application.Abstractions.Paper;
using CryptoTrader.Application.Abstractions.Persistence;
using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.Instruments;
using CryptoTrader.Domain.Trading;
using CryptoTrader.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CryptoTrader.Infrastructure.Persistence.Repositories;

internal sealed class PaperStateRepository : IPaperStateRepository
{
    private readonly CryptoTraderDbContext _db;

    public PaperStateRepository(CryptoTraderDbContext db)
    {
        _db = db;
    }

    public async Task SaveAsync(PaperState state, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(state.Positions);
        ArgumentNullException.ThrowIfNull(state.AppliedExchangeTradeIds);
        ArgumentNullException.ThrowIfNull(state.MarksByInstrument);
        await ExchangeCatalog.EnsureAsync(_db, state.Exchange.Value, cancellationToken);
        var accountId = state.AccountId.Value;
        var row = await _db.PaperAccounts.FirstOrDefaultAsync(item => item.AccountId == accountId, cancellationToken);
        if (row is null)
        {
            _db.PaperAccounts.Add(ToRow(state));
        }
        else
        {
            Copy(state, row);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PaperState?> LoadAsync(TradingAccountId accountId, CancellationToken cancellationToken)
    {
        var key = accountId.Value;
        var row = await _db.PaperAccounts.AsNoTracking().FirstOrDefaultAsync(item => item.AccountId == key, cancellationToken);
        return row is null ? null : ToState(row);
    }

    private static PaperAccountRow ToRow(PaperState state) => new()
    {
        AccountId = state.AccountId.Value,
        ExchangeId = state.Exchange.Value,
        QuoteAsset = state.QuoteAsset,
        Cash = state.Cash,
        PeakEquity = state.PeakEquity,
        DayStartEquity = state.DayStartEquity,
        LastEquity = state.LastEquity,
        DayUtc = state.DayUtc,
        EmergencyStop = state.EmergencyStop,
        PositionsJson = PersistenceJson.Serialize(state.Positions.Select(ToPositionDocument).ToArray()),
        MarksJson = PersistenceJson.Serialize(state.MarksByInstrument
            .Select(pair => new MarkDocument { InstrumentId = pair.Key.Value, Mark = pair.Value })
            .ToArray()),
        AppliedExchangeTradeIdsJson = PersistenceJson.Serialize(state.AppliedExchangeTradeIds.ToArray()),
        UpdatedAtUtc = state.UpdatedAtUtc,
    };

    private static void Copy(PaperState state, PaperAccountRow row)
    {
        var mapped = ToRow(state);
        row.ExchangeId = mapped.ExchangeId;
        row.QuoteAsset = mapped.QuoteAsset;
        row.Cash = mapped.Cash;
        row.PeakEquity = mapped.PeakEquity;
        row.DayStartEquity = mapped.DayStartEquity;
        row.LastEquity = mapped.LastEquity;
        row.DayUtc = mapped.DayUtc;
        row.EmergencyStop = mapped.EmergencyStop;
        row.PositionsJson = mapped.PositionsJson;
        row.MarksJson = mapped.MarksJson;
        row.AppliedExchangeTradeIdsJson = mapped.AppliedExchangeTradeIdsJson;
        row.UpdatedAtUtc = mapped.UpdatedAtUtc;
    }

    private static PaperState ToState(PaperAccountRow row)
    {
        var positions = PersistenceJson.DeserializeRequired<List<PaperPositionDocument>>(row.PositionsJson, "positions");
        var marks = PersistenceJson.DeserializeRequired<List<MarkDocument>>(row.MarksJson, "marks");
        var tradeIds = PersistenceJson.DeserializeRequired<List<string>>(row.AppliedExchangeTradeIdsJson, "applied_exchange_trade_ids");
        var markMap = new Dictionary<InstrumentId, decimal>(marks.Count);
        foreach (var mark in marks)
        {
            markMap[new InstrumentId(mark.InstrumentId)] = mark.Mark;
        }

        return new PaperState(
            new TradingAccountId(row.AccountId),
            new ExchangeId(row.ExchangeId),
            row.QuoteAsset,
            row.Cash,
            row.PeakEquity,
            row.DayStartEquity,
            row.LastEquity,
            row.DayUtc,
            row.EmergencyStop,
            positions.Select(ToPositionState).ToArray(),
            tradeIds,
            markMap,
            row.UpdatedAtUtc);
    }

    private static PaperPositionDocument ToPositionDocument(PaperPositionState position) => new()
    {
        InstrumentId = position.InstrumentId.Value,
        InstrumentType = (int)position.InstrumentType,
        Side = (int)position.Side,
        Quantity = position.Quantity,
        AveragePrice = position.AveragePrice,
        RealizedPnl = position.RealizedPnl,
        Fees = position.Fees,
        Funding = position.Funding,
        FeeAsset = position.FeeAsset,
        ContractMultiplier = position.ContractMultiplier,
        Margin = position.Margin,
        OpenedAtUtc = position.OpenedAtUtc,
        UpdatedAtUtc = position.UpdatedAtUtc,
    };

    private static PaperPositionState ToPositionState(PaperPositionDocument document) => new(
        new InstrumentId(document.InstrumentId),
        StoredEnum.FromInt32<InstrumentType>(document.InstrumentType, "positions.instrumentType"),
        StoredEnum.FromInt32<PositionSide>(document.Side, "positions.side"),
        document.Quantity,
        document.AveragePrice,
        document.RealizedPnl,
        document.Fees,
        document.Funding,
        document.FeeAsset,
        document.ContractMultiplier,
        document.Margin,
        document.OpenedAtUtc,
        document.UpdatedAtUtc);

    private sealed class PaperPositionDocument
    {
        public string InstrumentId { get; set; } = "";

        public int InstrumentType { get; set; }

        public int Side { get; set; }

        public decimal Quantity { get; set; }

        public decimal AveragePrice { get; set; }

        public decimal RealizedPnl { get; set; }

        public decimal Fees { get; set; }

        public decimal Funding { get; set; }

        public string? FeeAsset { get; set; }

        public decimal? ContractMultiplier { get; set; }

        public decimal? Margin { get; set; }

        public DateTimeOffset OpenedAtUtc { get; set; }

        public DateTimeOffset UpdatedAtUtc { get; set; }
    }

    private sealed class MarkDocument
    {
        public string InstrumentId { get; set; } = "";

        public decimal Mark { get; set; }
    }
}
