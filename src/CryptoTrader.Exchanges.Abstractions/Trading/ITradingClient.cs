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

    /// <summary>
    /// Private order lookup. A public-only adapter throws <see cref="NotSupportedException"/>
    /// and must not return null as if the exchange confirmed that the order is absent.
    /// </summary>
    Task<OrderStatusSnapshot?> GetOrderAsync(
        InstrumentId instrument,
        string clientOrderId,
        CancellationToken cancellationToken);

    /// <summary>Private cancel. A public-only adapter throws <see cref="NotSupportedException"/>.</summary>
    Task CancelAsync(
        InstrumentId instrument,
        string exchangeOrderId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Private open-order query. A public-only adapter throws <see cref="NotSupportedException"/>
    /// and must not return an empty list as a successful response.
    /// </summary>
    Task<IReadOnlyList<OrderStatusSnapshot>> GetOpenOrdersAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// Private position query. A public-only adapter throws <see cref="NotSupportedException"/>
    /// and must not return an empty list as a successful response.
    /// </summary>
    Task<IReadOnlyList<ExchangePositionSnapshot>> GetPositionsAsync(
        CancellationToken cancellationToken);
}
