using CryptoTrader.Application.Abstractions.Persistence;
using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.Instruments;
using CryptoTrader.Domain.Trading;
using CryptoTrader.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CryptoTrader.Infrastructure.Persistence.Repositories;

internal sealed class PositionRepository : IPositionRepository
{
    private readonly CryptoTraderDbContext _db;

    public PositionRepository(CryptoTraderDbContext db)
    {
        _db = db;
    }

    public async Task<Position?> FindOpenAsync(
        TradingAccountId accountId,
        InstrumentId instrumentId,
        CancellationToken cancellationToken)
    {
        var account = accountId.Value;
        var instrument = instrumentId.Value;
        var row = await _db.Positions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.TradingAccountId == account && item.InstrumentId == instrument,
                cancellationToken);
        if (row is null || row.Side == (int)PositionSide.Flat || row.Quantity == 0)
        {
            return null;
        }

        return ToPosition(row);
    }

    public async Task<IReadOnlyList<Position>> ListAsync(TradingAccountId accountId, CancellationToken cancellationToken)
    {
        var account = accountId.Value;
        var rows = await _db.Positions
            .AsNoTracking()
            .Where(item => item.TradingAccountId == account)
            .OrderBy(item => item.InstrumentId)
            .ToListAsync(cancellationToken);
        return rows.Select(ToPosition).ToArray();
    }

    public async Task UpsertAsync(Position position, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(position);
        var account = position.AccountId.Value;
        var instrument = position.InstrumentId.Value;
        var row = await _db.Positions.FirstOrDefaultAsync(
            item => item.TradingAccountId == account && item.InstrumentId == instrument,
            cancellationToken);
        if (row is null)
        {
            _db.Positions.Add(ToRow(position));
        }
        else
        {
            Copy(position, row);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static PositionRow ToRow(Position position) => new()
    {
        TradingAccountId = position.AccountId.Value,
        ExchangeId = position.Exchange.Value,
        InstrumentId = position.InstrumentId.Value,
        InstrumentType = (int)position.InstrumentType,
        Side = (int)position.Side,
        Quantity = position.Quantity.Value,
        AveragePrice = position.AveragePrice,
        RealizedPnl = position.RealizedPnl,
        Fees = position.Fees,
        Funding = position.Funding,
        FeeAsset = position.FeeAsset,
        Leverage = position.Leverage,
        Margin = position.Margin,
        ContractMultiplier = position.ContractMultiplier,
        OpenedAtUtc = position.OpenedAtUtc,
        UpdatedAtUtc = position.UpdatedAtUtc,
    };

    private static void Copy(Position position, PositionRow row)
    {
        row.ExchangeId = position.Exchange.Value;
        row.InstrumentType = (int)position.InstrumentType;
        row.Side = (int)position.Side;
        row.Quantity = position.Quantity.Value;
        row.AveragePrice = position.AveragePrice;
        row.RealizedPnl = position.RealizedPnl;
        row.Fees = position.Fees;
        row.Funding = position.Funding;
        row.FeeAsset = position.FeeAsset;
        row.Leverage = position.Leverage;
        row.Margin = position.Margin;
        row.ContractMultiplier = position.ContractMultiplier;
        row.OpenedAtUtc = position.OpenedAtUtc;
        row.UpdatedAtUtc = position.UpdatedAtUtc;
    }

    private static Position ToPosition(PositionRow row) => Position.Rehydrate(
        new TradingAccountId(row.TradingAccountId),
        new ExchangeId(row.ExchangeId),
        new InstrumentId(row.InstrumentId),
        StoredEnum.FromInt32<InstrumentType>(row.InstrumentType, "instrument_type"),
        StoredEnum.FromInt32<PositionSide>(row.Side, "side"),
        new Quantity(row.Quantity),
        row.AveragePrice,
        row.RealizedPnl,
        row.Fees,
        row.Funding,
        row.FeeAsset,
        row.Leverage,
        row.Margin,
        row.ContractMultiplier,
        row.OpenedAtUtc,
        row.UpdatedAtUtc);
}
