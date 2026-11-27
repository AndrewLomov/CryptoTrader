using CryptoTrader.Domain.Common;

namespace CryptoTrader.Domain.Trading;

public sealed record OrderSubmitted : DomainEvent
{
    public OrderSubmitted(
        OrderId orderId,
        string clientOrderId,
        TradingMode tradingMode,
        DateTimeOffset occurredAtUtc,
        Guid? correlationId = null,
        Guid? causationId = null)
        : base(occurredAtUtc, correlationId, causationId)
    {
        OrderId = orderId;
        ClientOrderId = clientOrderId;
        TradingMode = tradingMode;
    }

    public OrderId OrderId { get; init; }

    public string ClientOrderId { get; init; }

    public TradingMode TradingMode { get; init; }
}

public sealed record OrderAccepted : DomainEvent
{
    public OrderAccepted(
        OrderId orderId,
        string clientOrderId,
        string? exchangeOrderId,
        DateTimeOffset occurredAtUtc,
        Guid? correlationId = null,
        Guid? causationId = null)
        : base(occurredAtUtc, correlationId, causationId)
    {
        OrderId = orderId;
        ClientOrderId = clientOrderId;
        ExchangeOrderId = exchangeOrderId;
    }

    public OrderId OrderId { get; init; }

    public string ClientOrderId { get; init; }

    public string? ExchangeOrderId { get; init; }
}

public sealed record OrderPartiallyFilled : DomainEvent
{
    public OrderPartiallyFilled(
        OrderId orderId,
        ExecutionId executionId,
        string exchangeTradeId,
        decimal fillQuantity,
        decimal fillPrice,
        decimal filledQuantity,
        DateTimeOffset occurredAtUtc,
        Guid? correlationId = null,
        Guid? causationId = null)
        : base(occurredAtUtc, correlationId, causationId)
    {
        OrderId = orderId;
        ExecutionId = executionId;
        ExchangeTradeId = exchangeTradeId;
        FillQuantity = fillQuantity;
        FillPrice = fillPrice;
        FilledQuantity = filledQuantity;
    }

    public OrderId OrderId { get; init; }

    public ExecutionId ExecutionId { get; init; }

    public string ExchangeTradeId { get; init; }

    public decimal FillQuantity { get; init; }

    public decimal FillPrice { get; init; }

    public decimal FilledQuantity { get; init; }
}

public sealed record OrderFilled : DomainEvent
{
    public OrderFilled(
        OrderId orderId,
        ExecutionId executionId,
        string exchangeTradeId,
        decimal fillQuantity,
        decimal fillPrice,
        decimal averageFillPrice,
        DateTimeOffset occurredAtUtc,
        Guid? correlationId = null,
        Guid? causationId = null)
        : base(occurredAtUtc, correlationId, causationId)
    {
        OrderId = orderId;
        ExecutionId = executionId;
        ExchangeTradeId = exchangeTradeId;
        FillQuantity = fillQuantity;
        FillPrice = fillPrice;
        AverageFillPrice = averageFillPrice;
    }

    public OrderId OrderId { get; init; }

    public ExecutionId ExecutionId { get; init; }

    public string ExchangeTradeId { get; init; }

    public decimal FillQuantity { get; init; }

    public decimal FillPrice { get; init; }

    public decimal AverageFillPrice { get; init; }
}

public sealed record OrderCanceled : DomainEvent
{
    public OrderCanceled(
        OrderId orderId,
        string clientOrderId,
        decimal filledQuantity,
        DateTimeOffset occurredAtUtc,
        Guid? correlationId = null,
        Guid? causationId = null)
        : base(occurredAtUtc, correlationId, causationId)
    {
        OrderId = orderId;
        ClientOrderId = clientOrderId;
        FilledQuantity = filledQuantity;
    }

    public OrderId OrderId { get; init; }

    public string ClientOrderId { get; init; }

    public decimal FilledQuantity { get; init; }
}

public sealed record OrderRejected : DomainEvent
{
    public OrderRejected(
        OrderId orderId,
        string clientOrderId,
        string reason,
        DateTimeOffset occurredAtUtc,
        Guid? correlationId = null,
        Guid? causationId = null)
        : base(occurredAtUtc, correlationId, causationId)
    {
        OrderId = orderId;
        ClientOrderId = clientOrderId;
        Reason = reason;
    }

    public OrderId OrderId { get; init; }

    public string ClientOrderId { get; init; }

    public string Reason { get; init; }
}

public sealed record OrderMarkedUnknown : DomainEvent
{
    public OrderMarkedUnknown(
        OrderId orderId,
        string clientOrderId,
        DateTimeOffset occurredAtUtc,
        Guid? correlationId = null,
        Guid? causationId = null)
        : base(occurredAtUtc, correlationId, causationId)
    {
        OrderId = orderId;
        ClientOrderId = clientOrderId;
    }

    public OrderId OrderId { get; init; }

    public string ClientOrderId { get; init; }
}
