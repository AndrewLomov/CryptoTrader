using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.MarketData;
using CryptoTrader.Exchanges.Abstractions.MarketData;

namespace CryptoTrader.Exchanges.Okx.MarketData;

internal sealed class OkxOrderBookClient : IOrderBookClient
{
    private readonly OkxMarketDataClient _marketData;

    public OkxOrderBookClient(OkxMarketDataClient marketData)
    {
        ArgumentNullException.ThrowIfNull(marketData);
        _marketData = marketData;
    }

    public Task<OrderBookSnapshot> GetSnapshotAsync(InstrumentId instrument, CancellationToken cancellationToken) =>
        _marketData.GetOrderBookAsync(instrument, cancellationToken);
}
