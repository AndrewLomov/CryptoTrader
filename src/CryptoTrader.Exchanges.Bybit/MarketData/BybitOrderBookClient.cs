using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.MarketData;
using CryptoTrader.Exchanges.Abstractions.MarketData;

namespace CryptoTrader.Exchanges.Bybit.MarketData;

internal sealed class BybitOrderBookClient : IOrderBookClient
{
    private readonly BybitMarketDataClient _marketData;

    public BybitOrderBookClient(BybitMarketDataClient marketData)
    {
        ArgumentNullException.ThrowIfNull(marketData);
        _marketData = marketData;
    }

    public Task<OrderBookSnapshot> GetSnapshotAsync(InstrumentId instrument, CancellationToken cancellationToken) =>
        _marketData.GetOrderBookAsync(instrument, cancellationToken);
}
