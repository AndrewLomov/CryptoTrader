using System.Runtime.CompilerServices;
using System.Text.Json;
using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.Instruments;
using CryptoTrader.Domain.MarketData;
using CryptoTrader.Exchanges.Abstractions.MarketData;
using CryptoTrader.Exchanges.Abstractions.Models;
using CryptoTrader.Exchanges.Bybit.Mapping;
using CryptoTrader.Exchanges.Bybit.Rest;
using CryptoTrader.Exchanges.Bybit.WebSocket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CryptoTrader.Exchanges.Bybit.MarketData;

public sealed class BybitMarketDataClient : IMarketDataClient
{
    public const int MaxKlinePages = 20;

    private readonly BybitPublicRest _rest;
    private readonly IOptions<BybitOptions> _options;
    private readonly TimeProvider _time;
    private readonly ILogger<BybitMarketDataClient> _logger;

    internal BybitMarketDataClient(
        BybitPublicRest rest,
        IOptions<BybitOptions> options,
        TimeProvider time,
        ILogger<BybitMarketDataClient> logger)
    {
        ArgumentNullException.ThrowIfNull(rest);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);
        _rest = rest;
        _options = options;
        _time = time;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Candle>> GetCandlesAsync(
        InstrumentId instrument,
        Timeframe timeframe,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        var market = BybitSymbol.Require(instrument);
        var interval = BybitIntervals.ToExchange(timeframe);
        fromUtc = fromUtc.ToUniversalTime();
        toUtc = toUtc.ToUniversalTime();
        if (fromUtc > toUtc)
        {
            return [];
        }

        var candles = new Dictionary<long, Candle>();
        var cursor = fromUtc;
        for (var page = 0; page < MaxKlinePages && cursor <= toUtc; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var windowEnd = WindowEnd(cursor, toUtc, timeframe);
            var startMs = cursor.ToUnixTimeMilliseconds();
            var endMs = windowEnd.ToUnixTimeMilliseconds();
            if (endMs < startMs)
            {
                break;
            }

            using var document = await _rest.GetAsync(
                BybitPaths.Kline(market.Category, market.Symbol, interval, startMs, endMs),
                cancellationToken);
            var result = BybitResponses.RequireResult(document, _logger);
            if (!result.TryGetProperty("list", out var list) || list.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException("Bybit kline response did not include a list.");
            }

            var rows = new List<BybitKlineRow>();
            foreach (var row in list.EnumerateArray())
            {
                rows.Add(BybitCandleMapper.Map(row, instrument, timeframe, fromUtc, toUtc));
            }

            if (rows.Count == 0)
            {
                break;
            }

            var newest = long.MinValue;
            foreach (var row in rows.OrderBy(item => item.OpenTimeMs))
            {
                if (row.OpenTimeMs > newest)
                {
                    newest = row.OpenTimeMs;
                }

                if (row.Candle is not null)
                {
                    candles[row.OpenTimeMs] = row.Candle;
                }
            }

            var next = DateTimeOffset.FromUnixTimeMilliseconds(newest) + timeframe.Duration;
            if (next <= cursor)
            {
                break;
            }

            cursor = next;
            if (page == MaxKlinePages - 1 && cursor <= toUtc)
            {
                _logger.LogWarning("Bybit kline page cap of {PageCap} was reached before the requested range ended.", MaxKlinePages);
            }
        }

        return candles.Values.OrderBy(candle => candle.OpenTimeUtc).ToArray();
    }

    public async Task<Ticker?> GetTickerAsync(InstrumentId instrument, CancellationToken cancellationToken)
    {
        var market = BybitSymbol.Require(instrument);
        using var document = await _rest.GetAsync(BybitPaths.Tickers(market.Category, market.Symbol), cancellationToken);
        var result = BybitResponses.RequireResult(document, _logger);
        if (!BybitJson.TryFirstObject(result, out var item))
        {
            return null;
        }

        var received = _time.GetUtcNow();
        var exchangeTime = BybitJson.ReadUnixTime(document.RootElement, "time", document.RootElement, _time);
        if (!item.TryGetProperty("lastPrice", out var lastElement)
            || !BybitJson.TryReadDecimal(lastElement, out var last)
            || last <= 0m)
        {
            throw new InvalidOperationException("Bybit ticker did not include a last price.");
        }

        return Ticker.Create(
            ExchangeId.Bybit,
            instrument,
            OptionalPrice(item, "bid1Price"),
            OptionalPrice(item, "ask1Price"),
            last,
            exchangeTime,
            received);
    }

    public async Task<OrderBookSnapshot> GetOrderBookAsync(InstrumentId instrument, CancellationToken cancellationToken)
    {
        var market = BybitSymbol.Require(instrument);
        using var document = await _rest.GetAsync(BybitPaths.OrderBook(market.Category, market.Symbol), cancellationToken);
        var received = _time.GetUtcNow();
        var result = BybitResponses.RequireResult(document, _logger);
        var exchangeTime = BybitJson.ReadUnixTime(result, "ts", document.RootElement, _time);
        if (!BybitOrderBookParser.TryParse(result, out var parsed) || parsed.UpdateId is null)
        {
            return BybitOrderBookParser.Inconsistent(instrument, parsed.UpdateId, exchangeTime, received);
        }

        return BybitOrderBookParser.Consistent(instrument, parsed, exchangeTime, received);
    }

    public async IAsyncEnumerable<MarketEvent> SubscribeAsync(
        MarketSubscription subscription,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        var market = BybitSymbol.Require(subscription.Instrument);
        var topics = BuildTopics(subscription, market);
        if (topics.Count == 0)
        {
            yield break;
        }

        var options = _options.Value;
        var configuredUrl = market.Category == "linear" ? options.LinearWebSocketUrl : options.SpotWebSocketUrl;
        if (!Uri.TryCreate(configuredUrl, UriKind.Absolute, out var webSocketUri)
            || !string.Equals(webSocketUri.Scheme, "wss", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Bybit public websocket URL must be an absolute wss URL.");
        }

        var stream = new BybitPublicStream(
            _logger,
            _time,
            cancellation => GetOrderBookAsync(subscription.Instrument, cancellation));
        await foreach (var marketEvent in stream.ReadAsync(
            subscription,
            market,
            webSocketUri,
            topics,
            cancellationToken))
        {
            yield return marketEvent;
        }
    }

    public async Task<IReadOnlyList<Instrument>> ListConfiguredInstrumentsAsync(CancellationToken cancellationToken)
    {
        var symbols = _options.Value.Symbols ?? [];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var instruments = new List<Instrument>();
        foreach (var symbol in symbols)
        {
            if (string.IsNullOrWhiteSpace(symbol) || !seen.Add(symbol.Trim()))
            {
                continue;
            }

            var normalized = symbol.Trim();
            var spot = await LoadInstrumentAsync("spot", normalized, cancellationToken);
            if (spot is not null)
            {
                instruments.Add(spot);
            }

            var linear = await LoadInstrumentAsync("linear", normalized, cancellationToken);
            if (linear is not null)
            {
                instruments.Add(linear);
            }
        }

        return instruments;
    }

    public async Task<Instrument?> FindInstrumentBySymbolAsync(string exchangeSymbol, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(exchangeSymbol))
        {
            throw new ArgumentException("Exchange symbol is required.", nameof(exchangeSymbol));
        }

        var symbol = exchangeSymbol.Trim();
        var spot = await LoadInstrumentAsync("spot", symbol, cancellationToken);
        return spot ?? await LoadInstrumentAsync("linear", symbol, cancellationToken);
    }

    private async Task<Instrument?> LoadInstrumentAsync(string category, string symbol, CancellationToken cancellationToken)
    {
        using var document = await _rest.GetAsync(BybitPaths.Instruments(category, symbol), cancellationToken);
        var result = BybitResponses.RequireResult(document, _logger);
        if (!BybitJson.TryFirstObject(result, out var item))
        {
            return null;
        }

        return BybitInstrumentMapper.Map(item, category);
    }

    private static List<string> BuildTopics(MarketSubscription subscription, BybitMarket market)
    {
        var topics = new List<string>();
        if (subscription.IncludeCandles)
        {
            if (subscription.Timeframe is null || string.IsNullOrWhiteSpace(subscription.Timeframe.Value.Code))
            {
                throw new InvalidOperationException("A candle subscription requires a timeframe.");
            }

            topics.Add($"kline.{BybitIntervals.ToExchange(subscription.Timeframe.Value)}.{market.Symbol}");
        }

        if (subscription.IncludeTicker)
        {
            topics.Add($"tickers.{market.Symbol}");
        }

        if (subscription.IncludeTrades)
        {
            topics.Add($"publicTrade.{market.Symbol}");
        }

        if (subscription.IncludeOrderBook)
        {
            topics.Add($"orderbook.50.{market.Symbol}");
        }

        return topics;
    }

    private static DateTimeOffset WindowEnd(DateTimeOffset cursor, DateTimeOffset toUtc, Timeframe timeframe)
    {
        var ticks = timeframe.Duration.Ticks;
        if (ticks <= 0)
        {
            throw new InvalidOperationException("Timeframe duration must be positive.");
        }

        const int spans = 999;
        if (ticks > long.MaxValue / spans)
        {
            return toUtc;
        }

        DateTimeOffset candidate;
        try
        {
            candidate = cursor + TimeSpan.FromTicks(ticks * spans);
        }
        catch (ArgumentOutOfRangeException)
        {
            return toUtc;
        }

        return candidate > toUtc ? toUtc : candidate;
    }

    private static decimal? OptionalPrice(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var element)
            || !BybitJson.TryReadDecimal(element, out var value)
            || value <= 0m)
        {
            return null;
        }

        return value;
    }
}
