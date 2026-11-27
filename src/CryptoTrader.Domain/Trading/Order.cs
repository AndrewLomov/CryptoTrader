using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.Instruments;

namespace CryptoTrader.Domain.Trading;

public sealed class Order : AggregateRoot
{
    private readonly HashSet<string> _appliedTradeIds;
    private OrderStatus _status;
    private Quantity _filledQuantity;
    private decimal _filledNotional;

    private Order(
        OrderId id,
        string clientOrderId,
        TradingAccountId accountId,
        ExchangeId exchange,
        InstrumentId instrumentId,
        InstrumentType instrumentType,
        OrderSide side,
        OrderType type,
        Quantity quantity,
        Price? limitPrice,
        bool reduceOnly,
        TradingMode tradingMode,
        OrderStatus status,
        Quantity filledQuantity,
        decimal filledNotional,
        IEnumerable<string> appliedExchangeTradeIds,
        string? exchangeOrderId,
        string? rejectReason,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        Id = id;
        ClientOrderId = clientOrderId;
        AccountId = accountId;
        Exchange = exchange;
        InstrumentId = instrumentId;
        InstrumentType = instrumentType;
        Side = side;
        Type = type;
        Quantity = quantity;
        LimitPrice = limitPrice;
        ReduceOnly = reduceOnly;
        TradingMode = tradingMode;
        _status = status;
        _filledQuantity = filledQuantity;
        _filledNotional = filledNotional;
        _appliedTradeIds = new HashSet<string>(appliedExchangeTradeIds, StringComparer.Ordinal);
        ExchangeOrderId = exchangeOrderId;
        RejectReason = rejectReason;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public OrderId Id { get; }

    public string ClientOrderId { get; }

    public TradingAccountId AccountId { get; }

    public ExchangeId Exchange { get; }

    public InstrumentId InstrumentId { get; }

    public InstrumentType InstrumentType { get; }

    public OrderSide Side { get; }

    public OrderType Type { get; }

    public Quantity Quantity { get; }

    public Price? LimitPrice { get; }

    public bool ReduceOnly { get; }

    public TradingMode TradingMode { get; }

    public OrderStatus Status => _status;

    public Quantity FilledQuantity => _filledQuantity;

    public decimal FilledNotional => _filledNotional;

    public Quantity RemainingQuantity => new(Quantity.Value - _filledQuantity.Value);

    public decimal? AverageFillPrice => _filledQuantity.Value == 0 ? null : _filledNotional / _filledQuantity.Value;

    public IReadOnlyCollection<string> AppliedExchangeTradeIds => _appliedTradeIds;

    public string? ExchangeOrderId { get; private set; }

    public string? RejectReason { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static Order Create(
        OrderId id,
        string clientOrderId,
        TradingAccountId accountId,
        ExchangeId exchange,
        Instrument instrument,
        OrderSide side,
        OrderType type,
        Quantity quantity,
        Price? limitPrice,
        bool reduceOnly,
        TradingMode tradingMode,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(instrument);
        if (instrument.Exchange != exchange)
        {
            throw new DomainException("Order exchange does not match the instrument.");
        }

        now = Guard.Utc(now, nameof(now));
        ValidateShape(
            clientOrderId,
            instrument.Type,
            side,
            type,
            quantity,
            limitPrice,
            reduceOnly,
            tradingMode,
            OrderStatus.PendingSubmit,
            Quantity.Zero,
            0,
            [],
            null,
            null,
            now,
            now,
            out var normalizedClientOrderId,
            out _,
            out _);

        return new Order(
            id,
            normalizedClientOrderId,
            accountId,
            exchange,
            instrument.Id,
            instrument.Type,
            side,
            type,
            quantity,
            limitPrice,
            reduceOnly,
            tradingMode,
            OrderStatus.PendingSubmit,
            Quantity.Zero,
            0,
            [],
            null,
            null,
            now,
            now);
    }

    public static Order Rehydrate(
        OrderId id,
        string clientOrderId,
        TradingAccountId accountId,
        ExchangeId exchange,
        InstrumentId instrumentId,
        InstrumentType instrumentType,
        OrderSide side,
        OrderType type,
        Quantity quantity,
        Price? limitPrice,
        bool reduceOnly,
        TradingMode tradingMode,
        OrderStatus status,
        Quantity filledQuantity,
        decimal filledNotional,
        IReadOnlyCollection<string> appliedExchangeTradeIds,
        string? exchangeOrderId,
        string? rejectReason,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(appliedExchangeTradeIds);
        ValidateShape(
            clientOrderId,
            instrumentType,
            side,
            type,
            quantity,
            limitPrice,
            reduceOnly,
            tradingMode,
            status,
            filledQuantity,
            filledNotional,
            appliedExchangeTradeIds,
            exchangeOrderId,
            rejectReason,
            createdAtUtc,
            updatedAtUtc,
            out var normalizedClientOrderId,
            out var normalizedTradeIds,
            out var normalizedExchangeOrderId);

        return new Order(
            id,
            normalizedClientOrderId,
            accountId,
            exchange,
            instrumentId,
            instrumentType,
            side,
            type,
            quantity,
            limitPrice,
            reduceOnly,
            tradingMode,
            status,
            filledQuantity,
            filledNotional,
            normalizedTradeIds,
            normalizedExchangeOrderId,
            status == OrderStatus.Rejected ? Guard.Required(rejectReason, nameof(rejectReason)) : null,
            Guard.Utc(createdAtUtc, nameof(createdAtUtc)),
            Guard.Utc(updatedAtUtc, nameof(updatedAtUtc)));
    }

    public void MarkSubmitted(
        DateTimeOffset now,
        string? exchangeOrderId = null,
        Guid? correlationId = null,
        Guid? causationId = null)
    {
        now = Guard.Utc(now, nameof(now));
        EnsureTransition(OrderStatus.Submitted);
        AssignExchangeOrderId(exchangeOrderId);
        _status = OrderStatus.Submitted;
        UpdatedAtUtc = now;
        Raise(new OrderSubmitted(Id, ClientOrderId, TradingMode, now, correlationId, causationId));
    }

    public void MarkAccepted(
        DateTimeOffset now,
        string? exchangeOrderId = null,
        Guid? correlationId = null,
        Guid? causationId = null)
    {
        now = Guard.Utc(now, nameof(now));
        if (_filledQuantity.Value > 0)
        {
            throw new DomainException($"Order '{Id}' already has fills and cannot return to Accepted.");
        }

        EnsureTransition(OrderStatus.Accepted);
        AssignExchangeOrderId(exchangeOrderId);
        _status = OrderStatus.Accepted;
        UpdatedAtUtc = now;
        Raise(new OrderAccepted(Id, ClientOrderId, ExchangeOrderId, now, correlationId, causationId));
    }

    public void MarkUnknown(
        DateTimeOffset now,
        Guid? correlationId = null,
        Guid? causationId = null)
    {
        now = Guard.Utc(now, nameof(now));
        EnsureTransition(OrderStatus.Unknown);
        _status = OrderStatus.Unknown;
        UpdatedAtUtc = now;
        Raise(new OrderMarkedUnknown(Id, ClientOrderId, now, correlationId, causationId));
    }

    public Execution? ApplyFill(
        ExecutionId executionId,
        Quantity fillQuantity,
        Price fillPrice,
        decimal fee,
        string feeAsset,
        DateTimeOffset time,
        string exchangeTradeId,
        Guid? correlationId = null,
        Guid? causationId = null)
    {
        time = Guard.Utc(time, nameof(time));
        var tradeId = Guard.Required(exchangeTradeId, nameof(exchangeTradeId));
        if (_appliedTradeIds.Contains(tradeId))
        {
            return null;
        }

        if (fillQuantity.Value <= 0)
        {
            throw new DomainException("Fill quantity must be greater than zero.");
        }

        Guard.NotNegative(fee, nameof(fee));
        var normalizedFeeAsset = Guard.Required(feeAsset, nameof(feeAsset));
        var remaining = Quantity.Value - _filledQuantity.Value;
        if (fillQuantity.Value > remaining)
        {
            throw new DomainException($"Fill quantity {fillQuantity.Value} exceeds remaining {remaining}.");
        }

        var nextFilled = _filledQuantity.Value + fillQuantity.Value;
        var target = nextFilled == Quantity.Value ? OrderStatus.Filled : OrderStatus.PartiallyFilled;
        EnsureTransition(target);
        var execution = Execution.Create(
            executionId,
            Id,
            InstrumentId,
            Side,
            fillQuantity,
            fillPrice,
            fee,
            normalizedFeeAsset,
            time,
            tradeId);

        _appliedTradeIds.Add(tradeId);
        _filledQuantity = new Quantity(nextFilled);
        _filledNotional += fillPrice.Value * fillQuantity.Value;
        _status = target;
        UpdatedAtUtc = time;

        if (target == OrderStatus.Filled)
        {
            Raise(new OrderFilled(
                Id,
                executionId,
                tradeId,
                fillQuantity.Value,
                fillPrice.Value,
                _filledNotional / _filledQuantity.Value,
                time,
                correlationId,
                causationId));
        }
        else
        {
            Raise(new OrderPartiallyFilled(
                Id,
                executionId,
                tradeId,
                fillQuantity.Value,
                fillPrice.Value,
                _filledQuantity.Value,
                time,
                correlationId,
                causationId));
        }

        return execution;
    }

    public void MarkRejected(
        DateTimeOffset now,
        string reason,
        Guid? correlationId = null,
        Guid? causationId = null)
    {
        now = Guard.Utc(now, nameof(now));
        if (_filledQuantity.Value > 0)
        {
            throw new DomainException($"Order '{Id}' has fills and cannot be rejected.");
        }

        var normalized = Guard.Required(reason, nameof(reason));
        EnsureTransition(OrderStatus.Rejected);
        _status = OrderStatus.Rejected;
        RejectReason = normalized;
        UpdatedAtUtc = now;
        Raise(new OrderRejected(Id, ClientOrderId, normalized, now, correlationId, causationId));
    }

    public void RequestCancel(DateTimeOffset now)
    {
        now = Guard.Utc(now, nameof(now));
        EnsureTransition(OrderStatus.CancelPending);
        _status = OrderStatus.CancelPending;
        UpdatedAtUtc = now;
    }

    public void MarkCanceled(
        DateTimeOffset now,
        Guid? correlationId = null,
        Guid? causationId = null)
    {
        now = Guard.Utc(now, nameof(now));
        EnsureTransition(OrderStatus.Canceled);
        _status = OrderStatus.Canceled;
        UpdatedAtUtc = now;
        Raise(new OrderCanceled(Id, ClientOrderId, _filledQuantity.Value, now, correlationId, causationId));
    }

    private void AssignExchangeOrderId(string? exchangeOrderId)
    {
        if (string.IsNullOrWhiteSpace(exchangeOrderId))
        {
            return;
        }

        var normalized = exchangeOrderId.Trim();
        if (ExchangeOrderId is null)
        {
            ExchangeOrderId = normalized;
            return;
        }

        if (!string.Equals(ExchangeOrderId, normalized, StringComparison.Ordinal))
        {
            throw new DomainException("Exchange order id cannot change.");
        }
    }

    private void EnsureTransition(OrderStatus next)
    {
        if (!IsAllowed(_status, next))
        {
            throw new DomainException($"Order '{Id}' cannot transition from {_status} to {next}.");
        }
    }

    private static bool IsAllowed(OrderStatus from, OrderStatus to) => from switch
    {
        OrderStatus.PendingSubmit => to is OrderStatus.Submitted or OrderStatus.Rejected or OrderStatus.Unknown,
        OrderStatus.Submitted => to is OrderStatus.Accepted or OrderStatus.Rejected or OrderStatus.Unknown
            or OrderStatus.PartiallyFilled or OrderStatus.Filled or OrderStatus.CancelPending,
        OrderStatus.Unknown => to is OrderStatus.Accepted or OrderStatus.Rejected
            or OrderStatus.PartiallyFilled or OrderStatus.Filled or OrderStatus.Canceled,
        OrderStatus.Accepted => to is OrderStatus.PartiallyFilled or OrderStatus.Filled
            or OrderStatus.CancelPending or OrderStatus.Canceled,
        OrderStatus.PartiallyFilled => to is OrderStatus.PartiallyFilled or OrderStatus.Filled
            or OrderStatus.CancelPending or OrderStatus.Canceled,
        OrderStatus.CancelPending => to is OrderStatus.Canceled or OrderStatus.PartiallyFilled or OrderStatus.Filled,
        _ => false,
    };

    private static void ValidateShape(
        string clientOrderId,
        InstrumentType instrumentType,
        OrderSide side,
        OrderType type,
        Quantity quantity,
        Price? limitPrice,
        bool reduceOnly,
        TradingMode tradingMode,
        OrderStatus status,
        Quantity filledQuantity,
        decimal filledNotional,
        IReadOnlyCollection<string> appliedExchangeTradeIds,
        string? exchangeOrderId,
        string? rejectReason,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        out string normalizedClientOrderId,
        out List<string> normalizedTradeIds,
        out string? normalizedExchangeOrderId)
    {
        normalizedClientOrderId = Guard.Required(clientOrderId, nameof(clientOrderId));
        if (normalizedClientOrderId.Length > 64)
        {
            throw new DomainException("Client order id cannot exceed 64 characters.");
        }

        Guard.Defined(instrumentType, nameof(instrumentType));
        Guard.Defined(side, nameof(side));
        Guard.Defined(type, nameof(type));
        Guard.Defined(tradingMode, nameof(tradingMode));
        Guard.Defined(status, nameof(status));
        if (tradingMode == TradingMode.Live)
        {
            throw new DomainException("Live trading is disabled in CryptoTrader 0.1.");
        }

        if (quantity.Value <= 0)
        {
            throw new DomainException("Order quantity must be greater than zero.");
        }

        if (type == OrderType.Limit && limitPrice is null)
        {
            throw new DomainException("Limit orders require a limit price.");
        }

        if (type == OrderType.Market && limitPrice is not null)
        {
            throw new DomainException("Market orders cannot include a limit price.");
        }

        if (reduceOnly && instrumentType == InstrumentType.Spot)
        {
            throw new DomainException("Spot orders cannot be reduce-only.");
        }

        createdAtUtc = Guard.Utc(createdAtUtc, nameof(createdAtUtc));
        updatedAtUtc = Guard.Utc(updatedAtUtc, nameof(updatedAtUtc));
        if (updatedAtUtc < createdAtUtc)
        {
            throw new DomainException("Order update time cannot be earlier than creation time.");
        }

        if (filledQuantity.Value > quantity.Value)
        {
            throw new DomainException("Filled quantity cannot exceed order quantity.");
        }

        normalizedTradeIds = [];
        foreach (var tradeId in appliedExchangeTradeIds)
        {
            var normalized = Guard.Required(tradeId, nameof(appliedExchangeTradeIds));
            if (normalizedTradeIds.Contains(normalized, StringComparer.Ordinal))
            {
                throw new DomainException($"Duplicate exchange trade id '{normalized}'.");
            }

            normalizedTradeIds.Add(normalized);
        }

        var hasFills = filledQuantity.Value > 0;
        if (hasFills && (filledNotional <= 0 || normalizedTradeIds.Count == 0))
        {
            throw new DomainException("A filled quantity requires notional and exchange trade ids.");
        }

        if (!hasFills && (filledNotional != 0 || normalizedTradeIds.Count > 0))
        {
            throw new DomainException("An order without fills cannot carry notional or trade ids.");
        }

        switch (status)
        {
            case OrderStatus.PendingSubmit:
            case OrderStatus.Submitted:
            case OrderStatus.Accepted:
            case OrderStatus.Rejected:
                if (hasFills)
                {
                    throw new DomainException($"Order status {status} cannot have fills.");
                }

                break;
            case OrderStatus.PartiallyFilled:
                if (filledQuantity.Value <= 0 || filledQuantity.Value >= quantity.Value)
                {
                    throw new DomainException("Partially filled orders must have a remaining quantity.");
                }

                break;
            case OrderStatus.Filled:
                if (filledQuantity.Value != quantity.Value)
                {
                    throw new DomainException("Filled orders must be completely filled.");
                }

                break;
            case OrderStatus.CancelPending:
            case OrderStatus.Canceled:
            case OrderStatus.Unknown:
                if (filledQuantity.Value >= quantity.Value)
                {
                    throw new DomainException($"Order status {status} cannot already be completely filled.");
                }

                break;
        }

        if (status == OrderStatus.Rejected)
        {
            _ = Guard.Required(rejectReason, nameof(rejectReason));
        }
        else if (!string.IsNullOrWhiteSpace(rejectReason))
        {
            throw new DomainException("Reject reason is only valid for a rejected order.");
        }

        normalizedExchangeOrderId = string.IsNullOrWhiteSpace(exchangeOrderId) ? null : exchangeOrderId.Trim();
    }
}
