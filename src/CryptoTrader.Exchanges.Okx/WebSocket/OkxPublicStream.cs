using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.MarketData;
using CryptoTrader.Domain.Trading;
using CryptoTrader.Exchanges.Abstractions.Models;
using CryptoTrader.Exchanges.Okx.Mapping;
using CryptoTrader.Exchanges.Okx.Rest;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CryptoTrader.Exchanges.Okx.WebSocket;

internal sealed class OkxPublicStream
{
    private const int MaxMessageBytes = 2_000_000;
    private const int MaxAckAttempts = 3;

    private readonly ILogger _logger;
    private readonly TimeProvider _time;
    private readonly IOptions<OkxOptions> _options;
    private readonly IOkxSocketConnector _connector;
    private readonly object _tickerGate = new();
    private TickerUpdated? _latestTicker;

    public OkxPublicStream(
        ILogger logger,
        TimeProvider time,
        IOptions<OkxOptions> options,
        IOkxSocketConnector connector)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(connector);
        _logger = logger;
        _time = time;
        _options = options;
        _connector = connector;
    }

    public async IAsyncEnumerable<MarketEvent> ReadAsync(
        MarketSubscription subscription,
        OkxMarket market,
        Uri? publicUri,
        IReadOnlyList<OkxChannelSubscription> publicChannels,
        Uri? businessUri,
        IReadOnlyList<OkxChannelSubscription> businessChannels,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        ArgumentNullException.ThrowIfNull(publicChannels);
        ArgumentNullException.ThrowIfNull(businessChannels);
        var options = _options.Value;
        if (options.ChannelCapacity <= 0)
        {
            throw new InvalidOperationException("Okx:ChannelCapacity must be positive.");
        }

        if (options.AckTimeoutSeconds <= 0)
        {
            throw new InvalidOperationException("Okx:AckTimeoutSeconds must be positive.");
        }

        if (options.PingIntervalSeconds <= 0 || options.PingIntervalSeconds >= 30)
        {
            throw new InvalidOperationException("Okx:PingIntervalSeconds must be greater than zero and less than 30.");
        }

        var channel = Channel.CreateBounded<MarketEvent>(new BoundedChannelOptions(options.ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });
        var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var pump = PumpAsync(subscription, market, publicUri, publicChannels, businessUri, businessChannels, channel.Writer, linked.Token);
        try
        {
            await foreach (var marketEvent in channel.Reader.ReadAllAsync(linked.Token))
            {
                yield return marketEvent;
            }
        }
        finally
        {
            linked.Cancel();
            try
            {
                await pump;
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                linked.Dispose();
            }
        }
    }

    private async Task PumpAsync(
        MarketSubscription subscription,
        OkxMarket market,
        Uri? publicUri,
        IReadOnlyList<OkxChannelSubscription> publicChannels,
        Uri? businessUri,
        IReadOnlyList<OkxChannelSubscription> businessChannels,
        ChannelWriter<MarketEvent> writer,
        CancellationToken cancellationToken)
    {
        using var session = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var tasks = new List<Task>();
        if (publicUri is not null && publicChannels.Count > 0)
        {
            tasks.Add(RunConnectionAsync(subscription, market, publicUri, publicChannels, writer, session));
        }

        if (businessUri is not null && businessChannels.Count > 0)
        {
            tasks.Add(RunConnectionAsync(subscription, market, businessUri, businessChannels, writer, session));
        }

        if (publicChannels.Any(channel => string.Equals(channel.Channel, "tickers", StringComparison.Ordinal)))
        {
            tasks.Add(FlushTickersAsync(writer, session.Token));
        }

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (Exception exception)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var error = exception is AggregateException aggregate
                ? aggregate.Flatten().InnerExceptions.FirstOrDefault(item => item is not OperationCanceledException) ?? exception
                : exception;
            if (error is OperationCanceledException && cancellationToken.IsCancellationRequested)
            {
                return;
            }

            writer.TryComplete(error);
            return;
        }
        finally
        {
            session.Cancel();
            writer.TryComplete();
        }
    }

    private async Task RunConnectionAsync(
        MarketSubscription subscription,
        OkxMarket market,
        Uri uri,
        IReadOnlyList<OkxChannelSubscription> channels,
        ChannelWriter<MarketEvent> writer,
        CancellationTokenSource session)
    {
        var cancellationToken = session.Token;
        try
        {
            var delaySeconds = 1;
            var ackFailures = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                var opened = false;
                var acknowledged = false;
                try
                {
                    await RunSessionAsync(
                        subscription,
                        market,
                        uri,
                        channels,
                        () => opened = true,
                        () => acknowledged = true,
                        writer,
                        cancellationToken);
                    if (acknowledged)
                    {
                        ackFailures = 0;
                        delaySeconds = 1;
                    }
                    else
                    {
                        ackFailures++;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logger.LogWarning("OKX public websocket session ended ({ExceptionType}).", exception.GetType().Name);
                    if (acknowledged)
                    {
                        ackFailures = 0;
                        delaySeconds = 1;
                    }
                    else
                    {
                        ackFailures++;
                    }
                }

                if (ackFailures >= MaxAckAttempts)
                {
                    throw new InvalidOperationException("OKX public websocket subscription failed.");
                }

                var wait = TimeSpan.FromSeconds(delaySeconds) + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 250));
                await Task.Delay(wait, _time, cancellationToken);
                if (!(opened && acknowledged))
                {
                    delaySeconds = Math.Min(delaySeconds * 2, 30);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            session.Cancel();
            throw;
        }
    }

    private async Task RunSessionAsync(
        MarketSubscription subscription,
        OkxMarket market,
        Uri uri,
        IReadOnlyList<OkxChannelSubscription> channels,
        Action onOpened,
        Action onAcknowledged,
        ChannelWriter<MarketEvent> writer,
        CancellationToken cancellationToken)
    {
        var socket = await _connector.ConnectAsync(uri, cancellationToken);
        await using (socket)
        {
            onOpened();
            var pending = new HashSet<string>(channels.Select(channel => channel.Key), StringComparer.Ordinal);
            using var sendLock = new SemaphoreSlim(1, 1);
            await SendTextAsync(socket, SubscribePayload(channels), sendLock, cancellationToken);
            using var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var awaitingPong = new PongState();
            var receiveTask = ReceiveAsync(socket, sendLock, subscription, market, pending, awaitingPong, onAcknowledged, writer, sessionCts.Token);
            var pingTask = PingAsync(socket, sendLock, awaitingPong, sessionCts.Token);
            var ackTask = WatchAckAsync(pending, onAcknowledged, sessionCts.Token);
            var finished = await Task.WhenAny(receiveTask, pingTask, ackTask);
            sessionCts.Cancel();
            socket.Abort();
            try
            {
                await Task.WhenAll(receiveTask, pingTask, ackTask);
            }
            catch (Exception)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                if (finished.IsFaulted)
                {
                    var error = finished.Exception!.GetBaseException();
                    if (error is not OperationCanceledException)
                    {
                        throw error;
                    }
                }
            }
        }
    }

    private async Task WatchAckAsync(HashSet<string> pending, Action onAcknowledged, CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(_options.Value.AckTimeoutSeconds), _time, cancellationToken);
        bool acknowledged;
        lock (pending)
        {
            acknowledged = pending.Count == 0;
        }

        if (!acknowledged)
        {
            throw new InvalidOperationException("OKX public websocket subscription was not acknowledged before the timeout.");
        }

        onAcknowledged();
        await Task.Delay(Timeout.InfiniteTimeSpan, _time, cancellationToken);
    }

    private async Task PingAsync(IOkxSocket socket, SemaphoreSlim sendLock, PongState awaitingPong, CancellationToken cancellationToken)
    {
        var interval = TimeSpan.FromSeconds(_options.Value.PingIntervalSeconds);
        while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(interval, _time, cancellationToken);
            if (awaitingPong.Waiting)
            {
                throw new InvalidOperationException("OKX public websocket pong was not received.");
            }

            awaitingPong.Waiting = true;
            await SendTextAsync(socket, "ping", sendLock, cancellationToken);
        }
    }

    private async Task ReceiveAsync(
        IOkxSocket socket,
        SemaphoreSlim sendLock,
        MarketSubscription subscription,
        OkxMarket market,
        HashSet<string> pending,
        PongState awaitingPong,
        Action onAcknowledged,
        ChannelWriter<MarketEvent> writer,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        using var message = new MemoryStream();
        while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            message.SetLength(0);
            ValueWebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(buffer.AsMemory(), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return;
                }

                if (message.Length + result.Count > MaxMessageBytes)
                {
                    throw new InvalidOperationException("OKX public websocket message exceeded the size limit.");
                }

                if (result.Count > 0)
                {
                    message.Write(buffer, 0, result.Count);
                }
            }
            while (!result.EndOfMessage);

            if (result.MessageType != WebSocketMessageType.Text || message.Length == 0)
            {
                continue;
            }

            var text = Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length).Trim();
            if (string.Equals(text, "pong", StringComparison.OrdinalIgnoreCase))
            {
                awaitingPong.Waiting = false;
                continue;
            }

            if (string.Equals(text, "ping", StringComparison.OrdinalIgnoreCase))
            {
                await SendTextAsync(socket, "pong", sendLock, cancellationToken);
                continue;
            }

            if (!await HandleTextAsync(subscription, market, pending, onAcknowledged, writer, text, cancellationToken))
            {
                return;
            }
        }
    }

    private async Task<bool> HandleTextAsync(
        MarketSubscription subscription,
        OkxMarket market,
        HashSet<string> pending,
        Action onAcknowledged,
        ChannelWriter<MarketEvent> writer,
        string json,
        CancellationToken cancellationToken)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            _logger.LogWarning("OKX public websocket message was discarded.");
            return true;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return true;
            }

            var eventName = OkxJson.ReadString(root, "event");
            if (string.Equals(eventName, "error", StringComparison.Ordinal))
            {
                var code = OkxJson.ReadString(root, "code");
                var message = OkxResponses.SafeText(OkxJson.ReadString(root, "msg"));
                throw new InvalidOperationException(message ?? (code is null ? "OKX public websocket error." : "OKX public websocket error " + code + "."));
            }

            if (string.Equals(eventName, "subscribe", StringComparison.Ordinal))
            {
                if (!TryReadArg(root, out var channelName, out var instId))
                {
                    throw new InvalidOperationException("OKX public websocket subscribe ack did not include channel and instId.");
                }

                var key = channelName + "\u001f" + instId;
                lock (pending)
                {
                    pending.Remove(key);
                    if (pending.Count == 0)
                    {
                        onAcknowledged();
                    }
                }

                return true;
            }

            if (eventName is not null || !root.TryGetProperty("data", out _))
            {
                return true;
            }

            if (!TryReadArg(root, out var channel, out var instIdValue)
                || !string.Equals(instIdValue, market.InstId, StringComparison.Ordinal))
            {
                return true;
            }

            var received = _time.GetUtcNow();
            if (channel.StartsWith("candle", StringComparison.Ordinal))
            {
                return await HandleCandlesAsync(subscription, market, writer, root, received, cancellationToken);
            }

            if (string.Equals(channel, "tickers", StringComparison.Ordinal))
            {
                StageTicker(subscription, root, received);
                return true;
            }

            if (string.Equals(channel, "trades", StringComparison.Ordinal))
            {
                return await HandleTradesAsync(subscription, writer, root, received, cancellationToken);
            }

            if (string.Equals(channel, "books5", StringComparison.Ordinal) && subscription.IncludeOrderBook)
            {
                return await HandleBookAsync(subscription, writer, root, received, cancellationToken);
            }

            return true;
        }
    }

    private async Task<bool> HandleCandlesAsync(
        MarketSubscription subscription,
        OkxMarket market,
        ChannelWriter<MarketEvent> writer,
        JsonElement root,
        DateTimeOffset received,
        CancellationToken cancellationToken)
    {
        if (!subscription.IncludeCandles
            || subscription.Timeframe is null
            || !root.TryGetProperty("data", out var data)
            || data.ValueKind != JsonValueKind.Array)
        {
            return true;
        }

        foreach (var row in data.EnumerateArray())
        {
            if (!OkxCandleMapper.TryParse(
                    row,
                    subscription.Instrument,
                    subscription.Timeframe.Value,
                    market.IsSpot,
                    DateTimeOffset.MinValue,
                    DateTimeOffset.MaxValue,
                    out var parsed)
                || parsed.Candle is null)
            {
                continue;
            }

            if (!await WriteEventAsync(
                    writer,
                    new CandleObserved(
                        OkxExchangeAdapter.Id,
                        subscription.Instrument,
                        parsed.Candle.OpenTimeUtc,
                        received,
                        parsed.Candle),
                    cancellationToken))
            {
                return false;
            }
        }

        return true;
    }

    private void StageTicker(MarketSubscription subscription, JsonElement root, DateTimeOffset received)
    {
        if (!subscription.IncludeTicker || !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in data.EnumerateArray())
        {
            Ticker ticker;
            try
            {
                ticker = OkxTickerMapper.Parse(item, subscription.Instrument, received, _time);
            }
            catch (OkxApiException)
            {
                continue;
            }
            catch (DomainException)
            {
                continue;
            }

            var update = new TickerUpdated(OkxExchangeAdapter.Id, subscription.Instrument, ticker.ExchangeTimeUtc, received, ticker);
            lock (_tickerGate)
            {
                _latestTicker = update;
            }
        }
    }

    private async Task FlushTickersAsync(ChannelWriter<MarketEvent> writer, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), _time, cancellationToken);
            TickerUpdated? ticker;
            lock (_tickerGate)
            {
                ticker = _latestTicker;
                _latestTicker = null;
            }

            if (ticker is null)
            {
                continue;
            }

            if (!writer.TryWrite(ticker))
            {
                lock (_tickerGate)
                {
                    _latestTicker ??= ticker;
                }
            }
        }
    }

    private async Task<bool> HandleTradesAsync(
        MarketSubscription subscription,
        ChannelWriter<MarketEvent> writer,
        JsonElement root,
        DateTimeOffset received,
        CancellationToken cancellationToken)
    {
        if (!subscription.IncludeTrades || !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return true;
        }

        foreach (var item in data.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object
                || !item.TryGetProperty("px", out var priceElement)
                || !OkxJson.TryReadDecimal(priceElement, out var price)
                || price <= 0m
                || !item.TryGetProperty("sz", out var sizeElement)
                || !OkxJson.TryReadDecimal(sizeElement, out var size)
                || size <= 0m)
            {
                continue;
            }

            var tradeTime = OkxJson.ReadOptionalUnixMilliseconds(item, "ts") is long tradeMs
                ? DateTimeOffset.FromUnixTimeMilliseconds(tradeMs)
                : received;
            var side = OkxJson.ReadString(item, "side") switch
            {
                "buy" => OrderSide.Buy,
                "sell" => OrderSide.Sell,
                _ => (OrderSide?)null,
            };
            try
            {
                if (!await WriteEventAsync(
                        writer,
                        new PublicTradeObserved(
                            OkxExchangeAdapter.Id,
                            subscription.Instrument,
                            tradeTime,
                            received,
                            new Price(price),
                            new Quantity(size),
                            side),
                        cancellationToken))
                {
                    return false;
                }
            }
            catch (DomainException)
            {
            }
            catch (ArgumentOutOfRangeException)
            {
            }
        }

        return true;
    }

    private async Task<bool> HandleBookAsync(
        MarketSubscription subscription,
        ChannelWriter<MarketEvent> writer,
        JsonElement root,
        DateTimeOffset received,
        CancellationToken cancellationToken)
    {
        if (!OkxOrderBookParser.IsSnapshotMessage(root)
            || !root.TryGetProperty("data", out var data)
            || data.ValueKind != JsonValueKind.Array)
        {
            return true;
        }

        foreach (var item in data.EnumerateArray())
        {
            OrderBookSnapshot snapshot;
            try
            {
                snapshot = OkxOrderBookParser.Parse(item, subscription.Instrument, received, _time);
            }
            catch (DomainException)
            {
                snapshot = new OrderBookSnapshot(
                    OkxExchangeAdapter.Id,
                    subscription.Instrument,
                    [],
                    [],
                    sequence: null,
                    isConsistent: false,
                    received,
                    received);
            }

            if (!await WriteEventAsync(
                    writer,
                    new OrderBookUpdated(
                        OkxExchangeAdapter.Id,
                        subscription.Instrument,
                        snapshot.ExchangeTimeUtc,
                        received,
                        snapshot),
                    cancellationToken))
            {
                return false;
            }
        }

        return true;
    }

    private async Task<bool> WriteEventAsync(
        ChannelWriter<MarketEvent> writer,
        MarketEvent marketEvent,
        CancellationToken cancellationToken)
    {
        if (writer.TryWrite(marketEvent))
        {
            return true;
        }

        _logger.LogWarning(
            "OKX public websocket channel is full; waiting to write {EventType}.",
            marketEvent.GetType().Name);
        try
        {
            await writer.WriteAsync(marketEvent, cancellationToken);
            return true;
        }
        catch (ChannelClosedException)
        {
            return false;
        }
    }

    private static async Task SendTextAsync(
        IOkxSocket socket,
        string text,
        SemaphoreSlim sendLock,
        CancellationToken cancellationToken)
    {
        await sendLock.WaitAsync(cancellationToken);
        try
        {
            await socket.SendTextAsync(text, cancellationToken);
        }
        finally
        {
            sendLock.Release();
        }
    }

    private static bool TryReadArg(JsonElement root, out string channel, out string instId)
    {
        channel = string.Empty;
        instId = string.Empty;
        if (!root.TryGetProperty("arg", out var arg) || arg.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var channelName = OkxJson.ReadString(arg, "channel");
        var instrument = OkxJson.ReadString(arg, "instId");
        if (channelName is null || instrument is null)
        {
            return false;
        }

        channel = channelName;
        instId = instrument;
        return true;
    }

    private static string SubscribePayload(IReadOnlyList<OkxChannelSubscription> channels)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("id", Guid.NewGuid().ToString("N"));
            writer.WriteString("op", "subscribe");
            writer.WriteStartArray("args");
            foreach (var channel in channels)
            {
                writer.WriteStartObject();
                writer.WriteString("channel", channel.Channel);
                writer.WriteString("instId", channel.InstId);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private sealed class PongState
    {
        public volatile bool Waiting;
    }
}
