using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.MarketData;
using CryptoTrader.Domain.Trading;
using CryptoTrader.Exchanges.Abstractions.Models;
using CryptoTrader.Exchanges.Bybit.Mapping;
using CryptoTrader.Exchanges.Bybit.MarketData;
using CryptoTrader.Exchanges.Bybit.Rest;
using Microsoft.Extensions.Logging;

namespace CryptoTrader.Exchanges.Bybit.WebSocket;

internal sealed class BybitPublicStream
{
    private const int MaxMessageBytes = 2_000_000;
    private const int MaxOrderBookReloads = 3;

    private readonly ILogger _logger;
    private readonly TimeProvider _time;
    private readonly Func<CancellationToken, Task<OrderBookSnapshot>> _reloadOrderBook;

    public BybitPublicStream(
        ILogger logger,
        TimeProvider time,
        Func<CancellationToken, Task<OrderBookSnapshot>> reloadOrderBook)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(reloadOrderBook);
        _logger = logger;
        _time = time;
        _reloadOrderBook = reloadOrderBook;
    }

    public async IAsyncEnumerable<MarketEvent> ReadAsync(
        MarketSubscription subscription,
        BybitMarket market,
        Uri webSocketUri,
        IReadOnlyList<string> topics,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        ArgumentNullException.ThrowIfNull(webSocketUri);
        ArgumentNullException.ThrowIfNull(topics);
        var channel = Channel.CreateUnbounded<MarketEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true,
        });
        var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var pump = PumpAsync(subscription, market, webSocketUri, topics, channel.Writer, linked.Token);
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
        BybitMarket market,
        Uri webSocketUri,
        IReadOnlyList<string> topics,
        ChannelWriter<MarketEvent> writer,
        CancellationToken cancellationToken)
    {
        try
        {
            var delaySeconds = 1;
            while (!cancellationToken.IsCancellationRequested)
            {
                var opened = false;
                try
                {
                    await RunSessionAsync(
                        subscription,
                        market,
                        webSocketUri,
                        topics,
                        () => opened = true,
                        writer,
                        cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(
                        "Bybit public websocket disconnected ({ExceptionType}).",
                        exception.GetType().Name);
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                if (opened)
                {
                    delaySeconds = 1;
                }

                var wait = TimeSpan.FromSeconds(delaySeconds) + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 250));
                await Task.Delay(wait, cancellationToken);
                delaySeconds = Math.Min(delaySeconds * 2, 30);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            writer.TryComplete();
        }
    }

    private async Task RunSessionAsync(
        MarketSubscription subscription,
        BybitMarket market,
        Uri webSocketUri,
        IReadOnlyList<string> topics,
        Action onOpen,
        ChannelWriter<MarketEvent> writer,
        CancellationToken cancellationToken)
    {
        using var socket = new ClientWebSocket();
        using var sendLock = new SemaphoreSlim(1, 1);
        await socket.ConnectAsync(webSocketUri, cancellationToken);
        onOpen();
        _logger.LogInformation("Bybit public websocket connected.");
        await SendAsync(socket, SubscribePayload(topics), sendLock, cancellationToken);

        var session = new SessionState();
        using var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var receiveTask = ReceiveAsync(socket, sendLock, subscription, market, session, writer, sessionCts.Token);
        var pingTask = PingAsync(socket, sendLock, sessionCts.Token);
        var finished = await Task.WhenAny(receiveTask, pingTask);
        sessionCts.Cancel();
        Abort(socket);
        try
        {
            await Task.WhenAll(receiveTask, pingTask);
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

    private async Task ReceiveAsync(
        ClientWebSocket socket,
        SemaphoreSlim sendLock,
        MarketSubscription subscription,
        BybitMarket market,
        SessionState session,
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
                    throw new InvalidOperationException("Bybit public websocket message exceeded the size limit.");
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

            var json = Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
            if (!await HandleTextAsync(socket, sendLock, subscription, market, session, writer, json, cancellationToken))
            {
                return;
            }
        }
    }

    private async Task<bool> HandleTextAsync(
        ClientWebSocket socket,
        SemaphoreSlim sendLock,
        MarketSubscription subscription,
        BybitMarket market,
        SessionState session,
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
            _logger.LogWarning("Bybit public websocket message was discarded.");
            return true;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return true;
            }

            if (root.TryGetProperty("op", out var operation) && operation.ValueKind == JsonValueKind.String)
            {
                if (string.Equals(operation.GetString(), "ping", StringComparison.OrdinalIgnoreCase))
                {
                    await SendAsync(socket, "{\"op\":\"pong\"}", sendLock, cancellationToken);
                }
                else if (root.TryGetProperty("success", out var success) && success.ValueKind == JsonValueKind.False)
                {
                    var reason = SafeText(ReadString(root, "ret_msg") ?? BybitResponses.ReadMessage(root));
                    _logger.LogWarning("Bybit public websocket operation was rejected ({Reason}).", reason ?? "no message");
                }

                return true;
            }

            if (!root.TryGetProperty("topic", out var topicElement) || topicElement.ValueKind != JsonValueKind.String)
            {
                return true;
            }

            var topic = topicElement.GetString() ?? string.Empty;
            var received = _time.GetUtcNow();
            var exchangeTime = BybitJson.ReadUnixTime(root, "ts", root, _time);
            if (topic.StartsWith("kline.", StringComparison.Ordinal))
            {
                return HandleKline(subscription, writer, root, exchangeTime, received);
            }

            if (topic.StartsWith("tickers.", StringComparison.Ordinal))
            {
                return HandleTicker(subscription, session, writer, root, exchangeTime, received);
            }

            if (topic.StartsWith("publicTrade.", StringComparison.Ordinal))
            {
                return HandleTrades(subscription, writer, root, exchangeTime, received);
            }

            if (topic.StartsWith("orderbook.", StringComparison.Ordinal) && subscription.IncludeOrderBook)
            {
                return await HandleOrderBookAsync(subscription, session, writer, root, exchangeTime, received, cancellationToken);
            }
        }

        return true;
    }

    private bool HandleKline(
        MarketSubscription subscription,
        ChannelWriter<MarketEvent> writer,
        JsonElement root,
        DateTimeOffset exchangeTime,
        DateTimeOffset received)
    {
        if (subscription.Timeframe is null
            || !root.TryGetProperty("data", out var data)
            || data.ValueKind != JsonValueKind.Array)
        {
            return true;
        }

        var timeframe = subscription.Timeframe.Value;
        foreach (var item in data.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || !IsConfirmed(item))
            {
                continue;
            }

            if (!TryMapCandle(item, subscription.Instrument, timeframe, out var candle))
            {
                _logger.LogWarning("Bybit kline was discarded because it failed validation.");
                continue;
            }

            if (!Publish(writer, new CandleObserved(
                ExchangeId.Bybit,
                subscription.Instrument,
                exchangeTime,
                received,
                candle)))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HandleTicker(
        MarketSubscription subscription,
        SessionState session,
        ChannelWriter<MarketEvent> writer,
        JsonElement root,
        DateTimeOffset exchangeTime,
        DateTimeOffset received)
    {
        if (!subscription.IncludeTicker || !root.TryGetProperty("data", out var data))
        {
            return true;
        }

        if (data.ValueKind == JsonValueKind.Object)
        {
            session.Ticker.Apply(data);
        }
        else if (data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                {
                    session.Ticker.Apply(item);
                }
            }
        }

        if (session.Ticker.Last is not decimal last)
        {
            return true;
        }

        var ticker = Ticker.Create(
            ExchangeId.Bybit,
            subscription.Instrument,
            session.Ticker.Bid,
            session.Ticker.Ask,
            last,
            exchangeTime,
            received);
        return Publish(writer, new TickerUpdated(ExchangeId.Bybit, subscription.Instrument, exchangeTime, received, ticker));
    }

    private static bool HandleTrades(
        MarketSubscription subscription,
        ChannelWriter<MarketEvent> writer,
        JsonElement root,
        DateTimeOffset exchangeTime,
        DateTimeOffset received)
    {
        if (!subscription.IncludeTrades
            || !root.TryGetProperty("data", out var data)
            || data.ValueKind != JsonValueKind.Array)
        {
            return true;
        }

        foreach (var item in data.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object
                || !TryDecimalProperty(item, "p", out var price)
                || price <= 0m
                || !TryDecimalProperty(item, "v", out var quantity)
                || quantity <= 0m)
            {
                continue;
            }

            var tradeTime = BybitJson.ReadOptionalUnixMilliseconds(item, "T") is long tradeMs
                ? DateTimeOffset.FromUnixTimeMilliseconds(tradeMs)
                : exchangeTime;
            var side = ReadString(item, "S") switch
            {
                "Buy" => OrderSide.Buy,
                "Sell" => OrderSide.Sell,
                _ => (OrderSide?)null,
            };
            if (!Publish(writer, new PublicTradeObserved(
                ExchangeId.Bybit,
                subscription.Instrument,
                tradeTime,
                received,
                new Price(price),
                new Quantity(quantity),
                side)))
            {
                return false;
            }
        }

        return true;
    }

    private async Task<bool> HandleOrderBookAsync(
        MarketSubscription subscription,
        SessionState session,
        ChannelWriter<MarketEvent> writer,
        JsonElement root,
        DateTimeOffset exchangeTime,
        DateTimeOffset received,
        CancellationToken cancellationToken)
    {
        if (!root.TryGetProperty("data", out var data))
        {
            return true;
        }

        var parsedOk = BybitOrderBookParser.TryParse(data, out var parsed);
        var applied = session.Book.Apply(
            ReadString(root, "type"),
            parsed,
            parsedOk,
            subscription.Instrument,
            exchangeTime,
            received);
        if (applied.Publish && applied.Snapshot is not null)
        {
            if (!Publish(writer, new OrderBookUpdated(
                ExchangeId.Bybit,
                subscription.Instrument,
                exchangeTime,
                received,
                applied.Snapshot)))
            {
                return false;
            }

            if (applied.Snapshot.IsConsistent && !applied.NeedsReload)
            {
                session.ConsecutiveReloads = 0;
            }
        }

        if (!applied.NeedsReload)
        {
            return true;
        }

        if (session.ConsecutiveReloads >= MaxOrderBookReloads)
        {
            throw new InvalidOperationException("Bybit order book sequence did not recover.");
        }

        session.ConsecutiveReloads++;
        var snapshot = await _reloadOrderBook(cancellationToken);
        if (!snapshot.IsConsistent || snapshot.Sequence is null)
        {
            var inconsistent = snapshot.IsConsistent
                ? BybitOrderBookParser.Inconsistent(subscription.Instrument, snapshot.Sequence, snapshot.ExchangeTimeUtc, _time.GetUtcNow())
                : snapshot;
            Publish(writer, new OrderBookUpdated(
                ExchangeId.Bybit,
                subscription.Instrument,
                inconsistent.ExchangeTimeUtc,
                inconsistent.ReceivedAtUtc,
                inconsistent));
            throw new InvalidOperationException("Bybit order book snapshot was inconsistent.");
        }

        session.Book.Install(snapshot);
        return Publish(writer, new OrderBookUpdated(
            ExchangeId.Bybit,
            subscription.Instrument,
            snapshot.ExchangeTimeUtc,
            snapshot.ReceivedAtUtc,
            snapshot));
    }

    private static async Task PingAsync(ClientWebSocket socket, SemaphoreSlim sendLock, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(20), cancellationToken);
            await SendAsync(socket, "{\"op\":\"ping\"}", sendLock, cancellationToken);
        }
    }

    private static async Task SendAsync(
        ClientWebSocket socket,
        string json,
        SemaphoreSlim sendLock,
        CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        await sendLock.WaitAsync(cancellationToken);
        try
        {
            if (socket.State != WebSocketState.Open)
            {
                throw new WebSocketException("Bybit public websocket is not open.");
            }

            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
        }
        finally
        {
            sendLock.Release();
        }
    }

    private static string SubscribePayload(IReadOnlyList<string> topics)
    {
        return JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["op"] = "subscribe",
            ["args"] = topics,
        });
    }

    private static bool TryMapCandle(JsonElement item, InstrumentId instrument, Timeframe timeframe, out Candle candle)
    {
        candle = null!;
        if (!item.TryGetProperty("start", out var start) || !BybitJson.TryReadInt64(start, out var openMs))
        {
            return false;
        }

        if (!TryDecimalProperty(item, "open", out var open)
            || !TryDecimalProperty(item, "high", out var high)
            || !TryDecimalProperty(item, "low", out var low)
            || !TryDecimalProperty(item, "close", out var close)
            || !TryDecimalProperty(item, "volume", out var volume))
        {
            return false;
        }

        try
        {
            var openTime = DateTimeOffset.FromUnixTimeMilliseconds(openMs);
            candle = Candle.Create(
                ExchangeId.Bybit,
                instrument,
                timeframe,
                openTime,
                openTime + timeframe.Duration,
                open,
                high,
                low,
                close,
                volume,
                isClosed: true);
            return true;
        }
        catch (DomainException)
        {
            return false;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static bool IsConfirmed(JsonElement item)
    {
        if (!item.TryGetProperty("confirm", out var confirm))
        {
            return false;
        }

        return confirm.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.String => string.Equals(confirm.GetString(), "true", StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }

    private static bool TryDecimalProperty(JsonElement item, string name, out decimal value)
    {
        value = 0m;
        return item.TryGetProperty(name, out var element) && BybitJson.TryReadDecimal(element, out value);
    }

    private static string? ReadString(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return element.GetString();
    }

    private static string? SafeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (value.Contains("sign", StringComparison.OrdinalIgnoreCase)
            || value.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || value.Contains("apiKey", StringComparison.OrdinalIgnoreCase)
            || value.Contains("api_key", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return value.Length <= 180 ? value : value[..180];
    }

    private static bool Publish(ChannelWriter<MarketEvent> writer, MarketEvent marketEvent) =>
        writer.TryWrite(marketEvent);

    private static void Abort(ClientWebSocket socket)
    {
        try
        {
            socket.Abort();
        }
        catch (WebSocketException)
        {
        }
    }

    private sealed class SessionState
    {
        public int ConsecutiveReloads { get; set; }

        public TickerState Ticker { get; } = new();

        public BybitOrderBookAssembler Book { get; } = new();
    }

    private sealed class TickerState
    {
        public decimal? Bid { get; private set; }

        public decimal? Ask { get; private set; }

        public decimal? Last { get; private set; }

        public void Apply(JsonElement item)
        {
            if (TryPrice(item, "bid1Price", out var bid))
            {
                Bid = bid;
            }

            if (TryPrice(item, "ask1Price", out var ask))
            {
                Ask = ask;
            }

            if (TryPrice(item, "lastPrice", out var last))
            {
                Last = last;
            }
        }

        private static bool TryPrice(JsonElement item, string name, out decimal value)
        {
            value = 0m;
            return item.TryGetProperty(name, out var element)
                && BybitJson.TryReadDecimal(element, out value)
                && value > 0m;
        }
    }
}
