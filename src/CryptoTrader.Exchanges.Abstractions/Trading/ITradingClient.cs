using CryptoTrader.Domain.Common;
using CryptoTrader.Exchanges.Abstractions.Models;

namespace CryptoTrader.Exchanges.Abstractions.Trading;

public interface ITradingClient
{
    /// <summary>
    /// Submits an order. In CryptoTrader 0.1 live submission does not open a network call
    /// and returns <see cref="ExchangeErrorCodes.LiveTradingDisabled"/> with <c>UnknownState = false</c>.
    /// </summary>
    Task<OrderSubmissionResult> SubmitAsync(
        ExchangeOrderRequest request,
        CancellationToken cancellationToken);

    Task<OrderStatusSnapshot?> GetOrderAsync(
        InstrumentId instrument,
        string clientOrderId,
        CancellationToken cancellationToken);

    Task CancelAsync(
        InstrumentId instrument,
        string exchangeOrderId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<OrderStatusSnapshot>> GetOpenOrdersAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ExchangePositionSnapshot>> GetPositionsAsync(
        CancellationToken cancellationToken);
}
