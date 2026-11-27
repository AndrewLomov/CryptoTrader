using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.MarketData;
using CryptoTrader.Exchanges.Abstractions.Models;

namespace CryptoTrader.Exchanges.Abstractions.MarketData;

public interface IMarketDataClient
{
    /// <summary>
    /// Returns closed candles only, ordered by open time ascending.
    /// Candles that share an open time are merged instead of duplicated.
    /// </summary>
    Task<IReadOnlyList<Candle>> GetCandlesAsync(
        InstrumentId instrument,
        Timeframe timeframe,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken);

    Task<Ticker?> GetTickerAsync(
        InstrumentId instrument,
        CancellationToken cancellationToken);

    IAsyncEnumerable<MarketEvent> SubscribeAsync(
        MarketSubscription subscription,
        CancellationToken cancellationToken);
}
