using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.MarketData;

namespace CryptoTrader.Exchanges.Abstractions.MarketData;

/// <summary>
/// Reads one order book snapshot. Implementations are not required to apply deltas,
/// validate sequence gaps, or rebuild a full incremental book.
/// </summary>
public interface IOrderBookClient
{
    Task<OrderBookSnapshot> GetSnapshotAsync(InstrumentId instrument, CancellationToken cancellationToken);
}
