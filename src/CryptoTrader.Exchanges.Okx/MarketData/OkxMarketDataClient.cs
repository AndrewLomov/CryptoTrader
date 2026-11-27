using System.Runtime.CompilerServices;
using System.Text.Json;
using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.Instruments;
using CryptoTrader.Domain.MarketData;
using CryptoTrader.Exchanges.Abstractions.MarketData;
using CryptoTrader.Exchanges.Abstractions.Models;
using CryptoTrader.Exchanges.Okx.Mapping;
using CryptoTrader.Exchanges.Okx.Rest;
using CryptoTrader.Exchanges.Okx.WebSocket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CryptoTrader.Exchanges.Okx.MarketData;

public sealed class OkxMarketDataClient : IMarketDataClient
{
    private const int OfficialCandlePageCap = 300;

    private readonly OkxPublicRest _rest;
    private readonly IOptions<OkxOptions> _options;
    private readonly TimeProvider _time;
    private readonly ILogger<OkxMarketDataClient> _logger;
    private readonly IOkxSocketConnector _connector;

    internal OkxMarketDataClient(
        OkxPublicRest rest,
        IOptions<OkxOptions> options,
        TimeProvider time,
        ILogger<OkxMarketDataClient> logger)
        : this(rest, options, time, logger, new ClientWebSocketConnector())
    {
    }

    internal OkxMarketDataClient(
        OkxPublicRest rest,
        IOptions<OkxOptions> options,
        TimeProvider time,
        ILogger<OkxMarketDataClient> logger,
        IOkxSocketConnector connector)
    {
        ArgumentNullException.ThrowIfNull(rest);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(connector);
        _rest = rest;
        _options = options;
        _time = time;
        _logger = logger;
        _connector = connector;
    }

    public async Task<IReadOnlyList<Candle>> GetCandlesAsync(
        InstrumentId instrument,
        Timeframe timeframe,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        var history = await GetCandleHistoryAsync(instrument, timeframe, fromUtc, toUtc, cancellationToken);
        if (history.Completeness is CandleHistoryCompleteness.Complete or CandleHistoryCompleteness.Empty)
        {
            return history.Candles;
        }

        throw new IncompleteCandleHistoryException(history.Reason ?? "OKX candle history is incomplete.");
    }

    public async Task<CandleHistory> GetCandleHistoryAsync(
        InstrumentId instrument,
        Timeframe timeframe,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        var market = OkxSymbol.Require(instrument);
        fromUtc = fromUtc.ToUniversalTime();
        toUtc = toUtc.ToUniversalTime();
        if (fromUtc > toUtc)
        {
            return new CandleHistory([], CandleHistoryCompleteness.Empty, null);
        }

        var maxPages = _options.Value.MaxPages;
        if (maxPages <= 0)
        {
            throw new InvalidOperationException("Okx:MaxPages must be positive.");
        }

        var bar = OkxIntervals.ToBar(timeframe);
        var candles = new Dictionary<long, Candle>();
        var reason = await PageRangeAsync(market, timeframe, bar, fromUtc, toUtc, candles, maxPages, cancellationToken);

        var ordered = candles.Values.OrderBy(candle => candle.OpenTimeUtc).ToArray();
        if (reason is not null)
        {
            _logger.LogWarning("OKX candle history is incomplete: {Reason}", reason);
            return new CandleHistory(ordered, CandleHistoryCompleteness.Incomplete, reason);
        }

        if (ordered.Length == 0)
        {
            return new CandleHistory([], CandleHistoryCompleteness.Empty, "No closed candles in the requested range.");
        }

        var gap = GapReason(ordered, fromUtc, toUtc, timeframe);
        if (gap is not null)
        {
            _logger.LogWarning("OKX candle history is incomplete: {Reason}", gap);
            return new CandleHistory(ordered, CandleHistoryCompleteness.Incomplete, gap);
        }

        return new CandleHistory(ordered, CandleHistoryCompleteness.Complete, null);
    }

    public async Task<Ticker?> GetTickerAsync(InstrumentId instrument, CancellationToken cancellationToken)
    {
        var market = OkxSymbol.Require(instrument);
        using var document = await _rest.GetAsync(OkxPaths.Ticker(market.InstId), cancellationToken);
        var data = OkxResponses.RequireData(document);
        if (data.ValueKind != JsonValueKind.Array)
        {
            throw new OkxApiException("OKX ticker response did not include data.");
        }

        foreach (var item in data.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                return OkxTickerMapper.Parse(item, instrument, _time.GetUtcNow(), _time);
            }
        }

        return null;
    }

    public async Task<OrderBookSnapshot> GetOrderBookAsync(InstrumentId instrument, CancellationToken cancellationToken)
    {
        var market = OkxSymbol.Require(instrument);
        using var document = await _rest.GetAsync(OkxPaths.Books(market.InstId), cancellationToken);
        var data = OkxResponses.RequireData(document);
        var received = _time.GetUtcNow();
        if (data.ValueKind != JsonValueKind.Array)
        {
            return OkxOrderBookParser.Parse(default, instrument, received, _time);
        }

        foreach (var item in data.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                return OkxOrderBookParser.Parse(item, instrument, received, _time);
            }
        }

        return OkxOrderBookParser.Parse(default, instrument, received, _time);
    }

    public async IAsyncEnumerable<MarketEvent> SubscribeAsync(
        MarketSubscription subscription,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        var market = OkxSymbol.Require(subscription.Instrument);
        var options = _options.Value;
        if (options.ChannelCapacity <= 0)
        {
            throw new InvalidOperationException("Okx:ChannelCapacity must be positive.");
        }

        var publicChannels = new List<OkxChannelSubscription>();
        var businessChannels = new List<OkxChannelSubscription>();
        if (subscription.IncludeCandles)
        {
            if (subscription.Timeframe is null || string.IsNullOrWhiteSpace(subscription.Timeframe.Value.Code))
            {
                throw new InvalidOperationException("A candle subscription requires a timeframe.");
            }

            businessChannels.Add(new OkxChannelSubscription(
                OkxIntervals.ToCandleChannel(subscription.Timeframe.Value),
                market.InstId));
        }

        if (subscription.IncludeTicker)
        {
            publicChannels.Add(new OkxChannelSubscription("tickers", market.InstId));
        }

        if (subscription.IncludeTrades)
        {
            publicChannels.Add(new OkxChannelSubscription("trades", market.InstId));
        }

        if (subscription.IncludeOrderBook)
        {
            publicChannels.Add(new OkxChannelSubscription("books5", market.InstId));
        }

        if (publicChannels.Count == 0 && businessChannels.Count == 0)
        {
            yield break;
        }

        var publicUri = publicChannels.Count == 0 ? null : RequireWebSocketUri(options.PublicWebSocketUrl, "Okx:PublicWebSocketUrl");
        var businessUri = businessChannels.Count == 0 ? null : RequireWebSocketUri(options.BusinessWebSocketUrl, "Okx:BusinessWebSocketUrl");
        var stream = new OkxPublicStream(_logger, _time, _options, _connector);
        await foreach (var marketEvent in stream.ReadAsync(
            subscription,
            market,
            publicUri,
            publicChannels,
            businessUri,
            businessChannels,
            cancellationToken))
        {
            yield return marketEvent;
        }
    }

    internal async Task<IReadOnlyList<Instrument>> ListInstrumentsAsync(CancellationToken cancellationToken)
    {
        var types = _options.Value.InstrumentTypes ?? [];
        var instruments = new List<Instrument>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in types)
        {
            if (type is not ("SPOT" or "SWAP"))
            {
                continue;
            }

            using var document = await _rest.GetAsync(OkxPaths.Instruments(type), cancellationToken);
            foreach (var instrument in ReadInstruments(document))
            {
                if (seen.Add(instrument.Id.Value))
                {
                    instruments.Add(instrument);
                }
            }
        }

        return instruments;
    }

    internal async Task<Instrument?> FindInstrumentBySymbolAsync(string exchangeSymbol, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(exchangeSymbol))
        {
            throw new ArgumentException("Exchange symbol is required.", nameof(exchangeSymbol));
        }

        var symbol = exchangeSymbol.Trim();
        if (symbol.EndsWith("-SWAP", StringComparison.Ordinal))
        {
            return await LoadInstrumentAsync("SWAP", symbol, cancellationToken);
        }

        return await LoadInstrumentAsync("SPOT", symbol, cancellationToken)
            ?? await LoadInstrumentAsync("SWAP", symbol, cancellationToken);
    }

    private async Task<Instrument?> LoadInstrumentAsync(string instType, string instId, CancellationToken cancellationToken)
    {
        using var document = await _rest.GetAsync(OkxPaths.Instruments(instType, instId), cancellationToken);
        foreach (var instrument in ReadInstruments(document))
        {
            if (string.Equals(instrument.ExchangeSymbol, instId, StringComparison.Ordinal))
            {
                return instrument;
            }
        }

        return null;
    }

    private static IEnumerable<Instrument> ReadInstruments(JsonDocument document)
    {
        var data = OkxResponses.RequireData(document);
        if (data.ValueKind != JsonValueKind.Array)
        {
            throw new OkxApiException("OKX instrument response did not include data.");
        }

        foreach (var item in data.EnumerateArray())
        {
            var instrument = OkxInstrumentMapper.Map(item);
            if (instrument is not null)
            {
                yield return instrument;
            }
        }
    }

    private async Task<string?> PageRangeAsync(
        OkxMarket market,
        Timeframe timeframe,
        string bar,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        Dictionary<long, Candle> candles,
        int maxPages,
        CancellationToken cancellationToken)
    {
        var cursor = toUtc.ToUnixTimeMilliseconds();
        var fromMs = fromUtc.ToUnixTimeMilliseconds();
        var history = false;
        var covered = false;
        for (var page = 0; page < maxPages && !covered; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var document = await _rest.GetAsync(
                OkxPaths.Candles(history, market.InstId, bar, cursor, PageLimit(history)),
                cancellationToken);
            var data = OkxResponses.RequireData(document);
            if (data.ValueKind != JsonValueKind.Array)
            {
                throw new OkxApiException("OKX candle response did not include a list.");
            }

            var count = 0;
            var oldest = long.MaxValue;
            foreach (var row in data.EnumerateArray())
            {
                count++;
                var parsed = OkxCandleMapper.Parse(row, market.Instrument, timeframe, market.IsSpot, fromUtc, toUtc);
                if (parsed.OpenTimeMs < oldest)
                {
                    oldest = parsed.OpenTimeMs;
                }

                if (parsed.Candle is not null)
                {
                    candles[parsed.OpenTimeMs] = parsed.Candle;
                }
            }

            if (count == 0)
            {
                if (!history)
                {
                    history = true;
                    page--;
                    continue;
                }

                covered = true;
                break;
            }

            if (oldest >= cursor)
            {
                return "OKX candle cursor did not advance before the requested range ended.";
            }

            cursor = oldest;
            if (oldest <= fromMs)
            {
                covered = true;
            }
        }

        return covered
            ? null
            : $"OKX candle page cap of {maxPages} was reached before the requested range ended.";
    }

    private int PageLimit(bool history)
    {
        var configured = history
            ? _options.Value.MaxHistoryCandlesPerRequest
            : _options.Value.MaxCandlesPerRequest;
        if (configured < 1)
        {
            configured = 1;
        }

        return configured > OfficialCandlePageCap ? OfficialCandlePageCap : configured;
    }

    private static string? GapReason(
        IReadOnlyList<Candle> candles,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        Timeframe timeframe)
    {
        var missing = CandleGapDetector.FindMissingOpenTimes(candles);
        if (missing.Count > 0)
        {
            return $"OKX candle history is missing {missing.Count} confirmed candles.";
        }

        var first = candles[0];
        var last = candles[^1];
        try
        {
            if (first.OpenTimeUtc - timeframe.Duration >= fromUtc)
            {
                return "OKX candle history is missing confirmed candles before the first returned open.";
            }
        }
        catch (ArgumentOutOfRangeException)
        {
            return "OKX candle history could not prove the range was covered.";
        }

        try
        {
            if (last.CloseTimeUtc + timeframe.Duration <= toUtc)
            {
                return "OKX candle history is missing confirmed candles after the last returned close.";
            }
        }
        catch (ArgumentOutOfRangeException)
        {
            return "OKX candle history could not prove the range was covered.";
        }

        return null;
    }

    private static Uri RequireWebSocketUri(string configured, string optionName)
    {
        if (!Uri.TryCreate(configured, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, "wss", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{optionName} must be an absolute wss URL.");
        }

        return uri;
    }
}
