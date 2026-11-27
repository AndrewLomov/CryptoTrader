using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.MarketData;
using CryptoTrader.Exchanges.Abstractions.MarketData;

namespace CryptoTrader.Exchanges.Okx.MarketData;

/// <summary>
/// OKX order book access is a REST snapshot of 50 levels. The public socket only subscribes to
/// books5 (5 levels). Neither path reconstructs incremental depth or recovers sequence gaps.
/// </summary>
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
