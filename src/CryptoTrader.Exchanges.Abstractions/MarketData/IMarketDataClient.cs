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

    /// <summary>
    /// Closed candles for the range, plus whether the adapter proved the range was complete.
    /// The default calls <see cref="GetCandlesAsync"/> and does not prove completeness.
    /// </summary>
    async Task<CandleHistory> GetCandleHistoryAsync(
        InstrumentId instrument,
        Timeframe timeframe,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        var candles = await GetCandlesAsync(instrument, timeframe, fromUtc, toUtc, cancellationToken);
        ArgumentNullException.ThrowIfNull(candles);
        var completeness = candles.Count == 0
            ? CandleHistoryCompleteness.Empty
            : CandleHistoryCompleteness.Unverified;
        return new CandleHistory(
            candles,
            completeness,
            "Adapter did not report whether the requested range was complete.");
    }

    Task<Ticker?> GetTickerAsync(
        InstrumentId instrument,
        CancellationToken cancellationToken);

    IAsyncEnumerable<MarketEvent> SubscribeAsync(
        MarketSubscription subscription,
        CancellationToken cancellationToken);
}
