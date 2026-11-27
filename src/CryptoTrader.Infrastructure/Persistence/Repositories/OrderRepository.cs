using CryptoTrader.Application.Abstractions.Persistence;
using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.Instruments;
using CryptoTrader.Domain.Trading;
using CryptoTrader.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CryptoTrader.Infrastructure.Persistence.Repositories;

internal sealed class OrderRepository : IOrderRepository
{
    private readonly CryptoTraderDbContext _db;

    public OrderRepository(CryptoTraderDbContext db)
    {
        _db = db;
    }

    public async Task<Order?> FindByIdAsync(OrderId id, CancellationToken cancellationToken)
    {
        var key = id.Value;
        var row = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(item => item.Id == key, cancellationToken);
        return row is null ? null : ToOrder(row);
    }

    public async Task<Order?> FindByClientOrderIdAsync(
        TradingAccountId accountId,
        string clientOrderId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientOrderId);
        var account = accountId.Value;
        var clientId = clientOrderId.Trim();
        var row = await _db.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TradingAccountId == account && item.ClientOrderId == clientId, cancellationToken);
        return row is null ? null : ToOrder(row);
    }

    public async Task AddAsync(Order order, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);
        _db.Orders.Add(ToRow(order));
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Order order, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);
        var key = order.Id.Value;
        var row = await _db.Orders.FirstOrDefaultAsync(item => item.Id == key, cancellationToken);
        if (row is null)
        {
            throw new InvalidOperationException($"Order '{order.Id}' was not found.");
        }

        Copy(order, row);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Order>> ListAsync(
        TradingAccountId? accountId,
        TradingMode? tradingMode,
        CancellationToken cancellationToken)
    {
        var query = _db.Orders.AsNoTracking().AsQueryable();
        if (accountId is { } account)
        {
            var accountKey = account.Value;
            query = query.Where(item => item.TradingAccountId == accountKey);
        }

        if (tradingMode is { } mode)
        {
            var modeValue = (int)mode;
            query = query.Where(item => item.TradingMode == modeValue);
        }

        var rows = await query
            .OrderBy(item => item.CreatedAtUtc)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        return rows.Select(ToOrder).ToArray();
    }

    private static OrderRow ToRow(Order order) => new()
    {
        Id = order.Id.Value,
        ClientOrderId = order.ClientOrderId,
        TradingAccountId = order.AccountId.Value,
        ExchangeId = order.Exchange.Value,
        InstrumentId = order.InstrumentId.Value,
        InstrumentType = (int)order.InstrumentType,
        Side = (int)order.Side,
        OrderType = (int)order.Type,
        Quantity = order.Quantity.Value,
        LimitPrice = order.LimitPrice?.Value,
        ReduceOnly = order.ReduceOnly,
        TradingMode = (int)order.TradingMode,
        Status = (int)order.Status,
        FilledQuantity = order.FilledQuantity.Value,
        FilledNotional = order.FilledNotional,
        AppliedExchangeTradeIdsJson = PersistenceJson.Serialize(order.AppliedExchangeTradeIds.ToArray()),
        ExchangeOrderId = order.ExchangeOrderId,
        RejectReason = order.RejectReason,
        CreatedAtUtc = order.CreatedAtUtc,
        UpdatedAtUtc = order.UpdatedAtUtc,
    };

    private static void Copy(Order order, OrderRow row)
    {
        var mapped = ToRow(order);
        row.ClientOrderId = mapped.ClientOrderId;
        row.TradingAccountId = mapped.TradingAccountId;
        row.ExchangeId = mapped.ExchangeId;
        row.InstrumentId = mapped.InstrumentId;
        row.InstrumentType = mapped.InstrumentType;
        row.Side = mapped.Side;
        row.OrderType = mapped.OrderType;
        row.Quantity = mapped.Quantity;
        row.LimitPrice = mapped.LimitPrice;
        row.ReduceOnly = mapped.ReduceOnly;
        row.TradingMode = mapped.TradingMode;
        row.Status = mapped.Status;
        row.FilledQuantity = mapped.FilledQuantity;
        row.FilledNotional = mapped.FilledNotional;
        row.AppliedExchangeTradeIdsJson = mapped.AppliedExchangeTradeIdsJson;
        row.ExchangeOrderId = mapped.ExchangeOrderId;
        row.RejectReason = mapped.RejectReason;
        row.CreatedAtUtc = mapped.CreatedAtUtc;
        row.UpdatedAtUtc = mapped.UpdatedAtUtc;
    }

    private static Order ToOrder(OrderRow row)
    {
        var tradeIds = PersistenceJson.DeserializeRequired<List<string>>(row.AppliedExchangeTradeIdsJson, "applied_exchange_trade_ids");
        return Order.Rehydrate(
            new OrderId(row.Id),
            row.ClientOrderId,
            new TradingAccountId(row.TradingAccountId),
            new ExchangeId(row.ExchangeId),
            new InstrumentId(row.InstrumentId),
            StoredEnum.FromInt32<InstrumentType>(row.InstrumentType, "instrument_type"),
            StoredEnum.FromInt32<OrderSide>(row.Side, "side"),
            StoredEnum.FromInt32<OrderType>(row.OrderType, "order_type"),
            new Quantity(row.Quantity),
            row.LimitPrice is { } limitPrice ? new Price(limitPrice) : null,
            row.ReduceOnly,
            StoredEnum.FromInt32<TradingMode>(row.TradingMode, "trading_mode"),
            StoredEnum.FromInt32<OrderStatus>(row.Status, "status"),
            new Quantity(row.FilledQuantity),
            row.FilledNotional,
            tradeIds,
            row.ExchangeOrderId,
            row.RejectReason,
            row.CreatedAtUtc,
            row.UpdatedAtUtc);
    }
}
