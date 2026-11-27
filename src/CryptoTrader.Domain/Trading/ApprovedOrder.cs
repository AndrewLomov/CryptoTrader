using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.Strategies;

namespace CryptoTrader.Domain.Trading;

public sealed record ApprovedOrder
{
    public ApprovedOrder(
        Guid approvalId,
        TradeSignal signal,
        TradingAccountId accountId,
        Quantity quantity,
        OrderType orderType,
        Price? limitPrice,
        string clientOrderId,
        DateTimeOffset approvedAtUtc)
    {
        if (approvalId == Guid.Empty)
        {
            throw new DomainException("Approval id cannot be empty.");
        }

        ArgumentNullException.ThrowIfNull(signal);
        Guard.Defined(orderType, nameof(orderType));
        if (quantity.Value <= 0)
        {
            throw new DomainException("Approved quantity must be greater than zero.");
        }

        if (orderType == OrderType.Limit && limitPrice is null)
        {
            throw new DomainException("Approved limit order requires a limit price.");
        }

        if (orderType == OrderType.Market && limitPrice is not null)
        {
            throw new DomainException("Approved market order cannot include a limit price.");
        }

        ApprovalId = approvalId;
        Signal = signal;
        AccountId = accountId;
        Quantity = quantity;
        OrderType = orderType;
        LimitPrice = limitPrice;
        ClientOrderId = Guard.Required(clientOrderId, nameof(clientOrderId));
        ApprovedAtUtc = Guard.Utc(approvedAtUtc, nameof(approvedAtUtc));
    }

    public Guid ApprovalId { get; }

    public TradeSignal Signal { get; }

    public TradingAccountId AccountId { get; }

    public Quantity Quantity { get; }

    public OrderType OrderType { get; }

    public Price? LimitPrice { get; }

    public string ClientOrderId { get; }

    public DateTimeOffset ApprovedAtUtc { get; }
}
