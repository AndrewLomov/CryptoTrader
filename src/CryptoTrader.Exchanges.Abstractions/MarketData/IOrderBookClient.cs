using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.MarketData;

namespace CryptoTrader.Exchanges.Abstractions.MarketData;

public interface IOrderBookClient
{
    Task<OrderBookSnapshot> GetSnapshotAsync(InstrumentId instrument, CancellationToken cancellationToken);
}
