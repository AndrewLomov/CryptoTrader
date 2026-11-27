using CryptoTrader.Domain.Common;
using CryptoTrader.Exchanges.Abstractions;
using CryptoTrader.Exchanges.Abstractions.Models;
using CryptoTrader.Exchanges.Abstractions.Trading;

namespace CryptoTrader.Exchanges.Bybit.Trading;

public sealed class BybitTradingClient : ITradingClient
{
    public Task<OrderSubmissionResult> SubmitAsync(ExchangeOrderRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        return Task.FromResult(new OrderSubmissionResult(
            Accepted: false,
            ExchangeOrderId: null,
            ErrorCode: ExchangeErrorCodes.LiveTradingDisabled,
            UnknownState: false,
            Message: "Live trading is disabled in CryptoTrader 0.1."));
    }

    public Task<OrderStatusSnapshot?> GetOrderAsync(
        InstrumentId instrument,
        string clientOrderId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<OrderStatusSnapshot?>(null);
    }

    public Task CancelAsync(InstrumentId instrument, string exchangeOrderId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new NotSupportedException("Bybit private order cancel is not implemented in CryptoTrader 0.1.");
    }

    public Task<IReadOnlyList<OrderStatusSnapshot>> GetOpenOrdersAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<OrderStatusSnapshot>>([]);
    }

    public Task<IReadOnlyList<ExchangePositionSnapshot>> GetPositionsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<ExchangePositionSnapshot>>([]);
    }
}
