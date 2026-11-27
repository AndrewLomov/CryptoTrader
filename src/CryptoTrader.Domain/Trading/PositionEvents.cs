using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.Instruments;

namespace CryptoTrader.Domain.Trading;

public sealed record PositionOpened : DomainEvent
{
    public PositionOpened(
        TradingAccountId accountId,
        InstrumentId instrumentId,
        InstrumentType instrumentType,
        PositionSide side,
        decimal quantity,
        decimal averagePrice,
        DateTimeOffset occurredAtUtc,
        Guid? correlationId = null,
        Guid? causationId = null)
        : base(occurredAtUtc, correlationId, causationId)
    {
        AccountId = accountId;
        InstrumentId = instrumentId;
        InstrumentType = instrumentType;
        Side = side;
        Quantity = quantity;
        AveragePrice = averagePrice;
    }

    public TradingAccountId AccountId { get; init; }

    public InstrumentId InstrumentId { get; init; }

    public InstrumentType InstrumentType { get; init; }

    public PositionSide Side { get; init; }

    public decimal Quantity { get; init; }

    public decimal AveragePrice { get; init; }
}

public sealed record PositionChanged : DomainEvent
{
    public PositionChanged(
        TradingAccountId accountId,
        InstrumentId instrumentId,
        PositionSide side,
        decimal quantity,
        decimal averagePrice,
        decimal realizedPnl,
        DateTimeOffset occurredAtUtc,
        Guid? correlationId = null,
        Guid? causationId = null)
        : base(occurredAtUtc, correlationId, causationId)
    {
        AccountId = accountId;
        InstrumentId = instrumentId;
        Side = side;
        Quantity = quantity;
        AveragePrice = averagePrice;
        RealizedPnl = realizedPnl;
    }

    public TradingAccountId AccountId { get; init; }

    public InstrumentId InstrumentId { get; init; }

    public PositionSide Side { get; init; }

    public decimal Quantity { get; init; }

    public decimal AveragePrice { get; init; }

    public decimal RealizedPnl { get; init; }
}

public sealed record PositionClosed : DomainEvent
{
    public PositionClosed(
        TradingAccountId accountId,
        InstrumentId instrumentId,
        decimal realizedPnl,
        decimal fees,
        DateTimeOffset occurredAtUtc,
        Guid? correlationId = null,
        Guid? causationId = null)
        : base(occurredAtUtc, correlationId, causationId)
    {
        AccountId = accountId;
        InstrumentId = instrumentId;
        RealizedPnl = realizedPnl;
        Fees = fees;
    }

    public TradingAccountId AccountId { get; init; }

    public InstrumentId InstrumentId { get; init; }

    public decimal RealizedPnl { get; init; }

    public decimal Fees { get; init; }
}
