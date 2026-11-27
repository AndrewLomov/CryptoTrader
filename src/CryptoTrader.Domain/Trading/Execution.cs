using CryptoTrader.Domain.Common;

namespace CryptoTrader.Domain.Trading;

public sealed record Execution
{
    private Execution(
        ExecutionId id,
        OrderId orderId,
        InstrumentId instrumentId,
        OrderSide side,
        Quantity quantity,
        Price price,
        decimal fee,
        string feeAsset,
        DateTimeOffset timeUtc,
        string exchangeTradeId)
    {
        Id = id;
        OrderId = orderId;
        InstrumentId = instrumentId;
        Side = side;
        Quantity = quantity;
        Price = price;
        Fee = fee;
        FeeAsset = feeAsset;
        TimeUtc = timeUtc;
        ExchangeTradeId = exchangeTradeId;
    }

    public ExecutionId Id { get; }

    public OrderId OrderId { get; }

    public InstrumentId InstrumentId { get; }

    public OrderSide Side { get; }

    public Quantity Quantity { get; }

    public Price Price { get; }

    public decimal Fee { get; }

    public string FeeAsset { get; }

    public DateTimeOffset TimeUtc { get; }

    public string ExchangeTradeId { get; }

    public static Execution Create(
        ExecutionId id,
        OrderId orderId,
        InstrumentId instrumentId,
        OrderSide side,
        Quantity quantity,
        Price price,
        decimal fee,
        string feeAsset,
        DateTimeOffset timeUtc,
        string exchangeTradeId)
    {
        Guard.Defined(side, nameof(side));
        if (quantity.Value <= 0)
        {
            throw new DomainException("Execution quantity must be greater than zero.");
        }

        Guard.NotNegative(fee, nameof(fee));
        return new Execution(
            id,
            orderId,
            instrumentId,
            side,
            quantity,
            price,
            fee,
            Guard.Required(feeAsset, nameof(feeAsset)),
            Guard.Utc(timeUtc, nameof(timeUtc)),
            Guard.Required(exchangeTradeId, nameof(exchangeTradeId)));
    }
}
