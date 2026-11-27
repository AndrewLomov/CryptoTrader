using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.Instruments;
using CryptoTrader.Domain.MarketData;
using CryptoTrader.Domain.Trading;
using CryptoTrader.Exchanges.Abstractions;
using CryptoTrader.Exchanges.Abstractions.MarketData;
using CryptoTrader.Exchanges.Abstractions.Models;
using CryptoTrader.Exchanges.Okx;
using CryptoTrader.Exchanges.Okx.Accounts;
using CryptoTrader.Exchanges.Okx.Mapping;
using CryptoTrader.Exchanges.Okx.MarketData;
using CryptoTrader.Exchanges.Okx.Rest;
using CryptoTrader.Exchanges.Okx.Trading;
using CryptoTrader.Exchanges.Okx.WebSocket;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;

namespace CryptoTrader.Exchanges.ContractTests;

public class OkxContractTests
{
    private const long SpotOpenMs = 1_597_026_383_085;

    private const long SwapOpenMs = 1_597_026_323_085;

    private const string EmptyEnvelope = """{"code":"0","msg":"","data":[]}""";

    [Fact]
    public async Task Closed_spot_candle_uses_decimal_utc_base_volume_and_drops_the_unclosed_duplicate()
    {
        var body = ReadFixture("candles-spot.json", EmbeddedSpotCandles);
        var open = DateTimeOffset.FromUnixTimeMilliseconds(SpotOpenMs);
        using var harness = MarketHarness.Fixed(body);
        var instrument = new InstrumentId("OKX-SPOT-BTC-USDT");

        var history = await harness.Client.GetCandleHistoryAsync(
            instrument,
            Timeframe.OneMinute,
            open,
            open.AddMinutes(1),
            CancellationToken.None);
        var candles = await harness.Client.GetCandlesAsync(
            instrument,
            Timeframe.OneMinute,
            open,
            open.AddMinutes(1),
            CancellationToken.None);

        harness.AssertLocal();
        history.Completeness.ShouldBe(CandleHistoryCompleteness.Complete);
        AssertSpotCandle(history.Candles, open);
        AssertSpotCandle(candles, open);

        using var document = JsonDocument.Parse(body);
        var rows = document.RootElement.GetProperty("data");
        var unclosed = OkxCandleMapper.Parse(rows[0], instrument, Timeframe.OneMinute, spot: true, open, open.AddMinutes(1));
        unclosed.IsClosed.ShouldBeFalse();
        unclosed.Candle.ShouldBeNull();
        var closed = OkxCandleMapper.Parse(rows[1], instrument, Timeframe.OneMinute, spot: true, open, open.AddMinutes(1));
        closed.IsClosed.ShouldBeTrue();
        closed.Candle.ShouldNotBeNull();
        closed.Candle!.IsClosed.ShouldBeTrue();
    }

    [Fact]
    public async Task Swap_candle_volume_is_contract_and_confirm_0_is_not_closed()
    {
        var body = ReadFixture("candles-swap.json", EmbeddedSwapCandles);
        var open = DateTimeOffset.FromUnixTimeMilliseconds(SwapOpenMs);
        using var harness = MarketHarness.Fixed(body);
        var instrument = new InstrumentId("OKX-LINEAR-SWAP-BTC-USDT");

        var candles = await harness.Client.GetCandlesAsync(
            instrument,
            Timeframe.OneMinute,
            open,
            open.AddMinutes(1),
            CancellationToken.None);

        harness.AssertLocal();
        var candle = candles.ShouldHaveSingleItem();
        candle.IsClosed.ShouldBeTrue();
        candle.VolumeUnit.ShouldBe(VolumeUnit.Contract);
        candle.Volume.ShouldBe(250m);
        candle.BaseVolume.ShouldBe(2.5m);
        candle.QuoteVolume.ShouldBe(9300m);
        candle.Open.ShouldBe(3700m);
        candle.High.ShouldBe(3730m);
        candle.Low.ShouldBe(3690m);
        candle.Close.ShouldBe(3720m);
        candle.OpenTimeUtc.ShouldBe(open);
        candle.OpenTimeUtc.Offset.ShouldBe(TimeSpan.Zero);
        candle.CloseTimeUtc.ShouldBe(open.AddMinutes(1));
        candle.CloseTimeUtc.Offset.ShouldBe(TimeSpan.Zero);

        using var document = JsonDocument.Parse(body);
        var unclosed = OkxCandleMapper.Parse(
            document.RootElement.GetProperty("data")[0],
            instrument,
            Timeframe.OneMinute,
            spot: false,
            open,
            open.AddMinutes(1));
        unclosed.IsClosed.ShouldBeFalse();
        unclosed.Candle.ShouldBeNull();
    }

    [Fact]
    public async Task Nonzero_envelope_code_fails()
    {
        using var harness = MarketHarness.Fixed(ReadFixture("envelope-error.json", EmbeddedError));

        var error = await Should.ThrowAsync<OkxApiException>(() => harness.Client.GetCandlesAsync(
            new InstrumentId("OKX-SPOT-BTC-USDT"),
            Timeframe.OneMinute,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddMinutes(1),
            CancellationToken.None));

        error.Code.ShouldBe("51000");
        harness.AssertLocal();
    }

    [Fact]
    public async Task Http_error_fails()
    {
        using var harness = MarketHarness.Fixed(EmptyEnvelope, HttpStatusCode.InternalServerError);

        var error = await Should.ThrowAsync<OkxApiException>(() => harness.Client.GetCandlesAsync(
            new InstrumentId("OKX-SPOT-BTC-USDT"),
            Timeframe.OneMinute,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddMinutes(1),
            CancellationToken.None));

        error.Message.ShouldContain("500");
        harness.Handler.Calls.ShouldBe(1);
        harness.AssertLocal();
    }

    [Fact]
    public async Task Malformed_json_fails()
    {
        using var harness = MarketHarness.Fixed("{");

        var error = await Should.ThrowAsync<OkxApiException>(() => harness.Client.GetCandlesAsync(
            new InstrumentId("OKX-SPOT-BTC-USDT"),
            Timeframe.OneMinute,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddMinutes(1),
            CancellationToken.None));

        error.Message.ShouldContain("not valid JSON");
        harness.AssertLocal();
    }

    [Fact]
    public async Task Pagination_cursor_that_does_not_advance_is_not_complete()
    {
        var open = DateTimeOffset.FromUnixTimeMilliseconds(SpotOpenMs);
        using var harness = MarketHarness.Fixed(ReadFixture("candles-spot.json", EmbeddedSpotCandles));

        var history = await harness.Client.GetCandleHistoryAsync(
            new InstrumentId("OKX-SPOT-BTC-USDT"),
            Timeframe.OneMinute,
            open.AddMinutes(-1),
            open,
            CancellationToken.None);

        history.Completeness.ShouldBe(CandleHistoryCompleteness.Incomplete);
        history.Completeness.ShouldNotBe(CandleHistoryCompleteness.Complete);
        history.Reason.ShouldNotBeNull();
        history.Reason.ShouldContain("did not advance");
        await Should.ThrowAsync<IncompleteCandleHistoryException>(() => harness.Client.GetCandlesAsync(
            new InstrumentId("OKX-SPOT-BTC-USDT"),
            Timeframe.OneMinute,
            open.AddMinutes(-1),
            open,
            CancellationToken.None));
        harness.AssertLocal();
    }

    [Fact]
    public async Task Incomplete_range_is_not_complete()
    {
        var open = DateTimeOffset.FromUnixTimeMilliseconds(SpotOpenMs);
        using var harness = MarketHarness.Fixed(ReadFixture("candles-spot.json", EmbeddedSpotCandles));
        var instrument = new InstrumentId("OKX-SPOT-BTC-USDT");

        var history = await harness.Client.GetCandleHistoryAsync(
            instrument,
            Timeframe.OneMinute,
            open,
            open.AddMinutes(2),
            CancellationToken.None);

        history.Completeness.ShouldBe(CandleHistoryCompleteness.Incomplete);
        history.Completeness.ShouldNotBe(CandleHistoryCompleteness.Complete);
        history.Reason.ShouldNotBeNull();
        history.Reason.ShouldContain("after the last");
        await Should.ThrowAsync<IncompleteCandleHistoryException>(() => harness.Client.GetCandlesAsync(
            instrument,
            Timeframe.OneMinute,
            open,
            open.AddMinutes(2),
            CancellationToken.None));
        harness.AssertLocal();
    }

    [Fact]
    public async Task Duplicate_open_times_collapse()
    {
        const string body = """
            {"code":"0","msg":"","data":[
              ["1597026383085","3.731","3.799","3.494","3.70","1","2","3","1"],
              ["1597026383085","3.731","3.799","3.494","3.75","9","8","7","1"]
            ]}
            """;
        var open = DateTimeOffset.FromUnixTimeMilliseconds(SpotOpenMs);
        using var harness = MarketHarness.Fixed(body);

        var candles = await harness.Client.GetCandlesAsync(
            new InstrumentId("OKX-SPOT-BTC-USDT"),
            Timeframe.OneMinute,
            open,
            open.AddMinutes(1),
            CancellationToken.None);

        var candle = candles.ShouldHaveSingleItem();
        candle.Close.ShouldBe(3.75m);
        candle.Volume.ShouldBe(9m);
        harness.AssertLocal();
    }

    [Fact]
    public async Task Supported_instrument_ids_skip_inverse_option_dated_and_missing_ctVal()
    {
        var unsupportedRequested = false;
        using var harness = MarketHarness.Route(request =>
        {
            var path = request.RequestUri?.PathAndQuery ?? string.Empty;
            if (path.Contains("instType=SPOT", StringComparison.Ordinal))
            {
                return MarketHarness.Json(ReadFixture("instruments-spot.json", EmbeddedSpotInstrument));
            }

            if (path.Contains("instType=SWAP", StringComparison.Ordinal))
            {
                return MarketHarness.Json(SupportedSwapEnvelope);
            }

            if (path.Contains("instType=OPTION", StringComparison.Ordinal)
                || path.Contains("instType=FUTURES", StringComparison.Ordinal))
            {
                unsupportedRequested = true;
            }

            return MarketHarness.Json(EmptyEnvelope);
        });

        var instruments = await harness.Client.ListInstrumentsAsync(CancellationToken.None);

        harness.AssertLocal();
        unsupportedRequested.ShouldBeFalse();
        var ids = instruments.Select(instrument => instrument.Id.Value).ToArray();
        ids.ShouldContain("OKX-SPOT-BTC-USDT");
        ids.ShouldContain("OKX-LINEAR-SWAP-BTC-USDT");
        ids.ShouldNotContain("OKX-LINEAR-SWAP-ETH-USDT");
        instruments.Select(instrument => instrument.ExchangeSymbol).ShouldNotContain("BTC-USD-SWAP");
        instruments.Select(instrument => instrument.ExchangeSymbol).ShouldNotContain("BTC-USD-241227-100000-C");
        instruments.Select(instrument => instrument.ExchangeSymbol).ShouldNotContain("BTC-USDT-241227");
        var spot = instruments.Single(instrument => instrument.Id.Value == "OKX-SPOT-BTC-USDT");
        spot.Type.ShouldBe(InstrumentType.Spot);
        spot.ExchangeSymbol.ShouldBe("BTC-USDT");
        var swap = instruments.Single(instrument => instrument.Id.Value == "OKX-LINEAR-SWAP-BTC-USDT");
        swap.Type.ShouldBe(InstrumentType.LinearPerpetual);
        swap.ExchangeSymbol.ShouldBe("BTC-USDT-SWAP");
        swap.ContractValue.ShouldBe(0.01m);
        swap.ContractValue.ShouldNotBe(1m);
        swap.ContractMultiplier.ShouldBe(1m);
        instruments.ShouldNotContain(instrument => instrument.ContractValue == 1m && instrument.ExchangeSymbol == "ETH-USDT-SWAP");
    }

    [Fact]
    public async Task Empty_ctMult_fixture_does_not_become_one()
    {
        using var harness = MarketHarness.Route(request =>
        {
            var path = request.RequestUri?.PathAndQuery ?? string.Empty;
            if (path.Contains("instType=SWAP", StringComparison.Ordinal))
            {
                return MarketHarness.Json(ReadFixture("instruments-swap-linear.json", EmbeddedEmptyMultiplier));
            }

            return MarketHarness.Json(EmptyEnvelope);
        });

        var instruments = await harness.Client.ListInstrumentsAsync(CancellationToken.None);

        var swap = instruments.ShouldHaveSingleItem();
        swap.Id.Value.ShouldBe("OKX-LINEAR-SWAP-BTC-USDT");
        swap.ContractValue.ShouldBe(0.01m);
        swap.ContractMultiplier.ShouldBeNull();
        swap.HasCompleteLinearContractSpec.ShouldBeTrue();
        harness.AssertLocal();
    }

    [Fact]
    public async Task SubmitAsync_returns_live_trading_disabled_without_calling_the_handler()
    {
        var handler = new RecordingHandler(_ => throw new InvalidOperationException("live trading called HTTP"));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:9/") };
        var client = new OkxTradingClient();

        var result = await client.SubmitAsync(
            new ExchangeOrderRequest(
                "client-1",
                new InstrumentId("OKX-SPOT-BTC-USDT"),
                OrderSide.Buy,
                OrderType.Market,
                new Quantity(0.01m),
                LimitPrice: null,
                ReduceOnly: false),
            CancellationToken.None);

        result.ErrorCode.ShouldBe(ExchangeErrorCodes.LiveTradingDisabled);
        result.UnknownState.ShouldBeFalse();
        result.Accepted.ShouldBeFalse();
        handler.Calls.ShouldBe(0);
        HasHttpClientField(typeof(OkxTradingClient)).ShouldBeFalse();
        _ = http;
    }

    [Fact]
    public async Task Cancel_and_account_do_not_call_the_network()
    {
        var handler = new RecordingHandler(_ => throw new InvalidOperationException("private OKX called HTTP"));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:9/") };
        var trading = new OkxTradingClient();
        var account = new OkxAccountClient();

        await Should.ThrowAsync<NotSupportedException>(() => trading.CancelAsync(
            new InstrumentId("OKX-SPOT-BTC-USDT"),
            "exchange-order-1",
            CancellationToken.None));
        await Should.ThrowAsync<NotSupportedException>(() => account.GetAccountAsync(CancellationToken.None));

        handler.Calls.ShouldBe(0);
        HasHttpClientField(typeof(OkxTradingClient)).ShouldBeFalse();
        HasHttpClientField(typeof(OkxAccountClient)).ShouldBeFalse();
        _ = http;
    }

    [Fact(Timeout = 15000)]
    public async Task Subscribe_ack_success_is_recognized()
    {
        var connector = new ScriptedConnector(_ => ReadFixture("ws-subscribe-ack.json", EmbeddedSubscribeAck));
        using var harness = MarketHarness.Sockets(connector, ackTimeoutSeconds: 1, pingIntervalSeconds: 29);
        using var cts = new CancellationTokenSource();
        var pump = PumpAsync(harness.Client, cts.Token);

        await WaitUntil(() => connector.Sent.Any(text => text.Contains("subscribe", StringComparison.Ordinal)));
        await Task.Delay(3000);

        pump.Exception?.GetBaseException().Message.ShouldBeNull();
        connector.Sockets.Count.ShouldBe(1);
        connector.Sent.Any(text => text.Contains("tickers", StringComparison.Ordinal) && text.Contains("BTC-USDT", StringComparison.Ordinal))
            .ShouldBeTrue();
        harness.Handler.Calls.ShouldBe(0);
        cts.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => pump);
    }

    [Fact(Timeout = 15000)]
    public async Task Subscribe_error_is_a_failure()
    {
        var connector = new ScriptedConnector(_ => ReadFixture("ws-subscribe-error.json", EmbeddedSubscribeError));
        using var harness = MarketHarness.Sockets(connector, ackTimeoutSeconds: 5, pingIntervalSeconds: 29);
        using var cts = new CancellationTokenSource();
        var pump = PumpAsync(harness.Client, cts.Token);

        var error = await Should.ThrowAsync<InvalidOperationException>(() => pump);

        error.Message.ShouldContain("subscription failed");
        connector.Sockets.Count.ShouldBeGreaterThanOrEqualTo(3);
        harness.Handler.Calls.ShouldBe(0);
    }

    [Fact(Timeout = 15000)]
    public async Task Ping_pong_is_recognized()
    {
        var connector = new ScriptedConnector(_ => ReadFixture("ws-subscribe-ack.json", EmbeddedSubscribeAck));
        using var harness = MarketHarness.Sockets(connector, ackTimeoutSeconds: 8, pingIntervalSeconds: 1);
        using var cts = new CancellationTokenSource();
        var pump = PumpAsync(harness.Client, cts.Token);

        await WaitUntil(() => connector.Sent.Any(text => text.Contains("subscribe", StringComparison.Ordinal)));
        connector.Sockets[0].Enqueue("ping");
        await WaitUntil(() => connector.Sent.Contains("pong") || pump.IsFaulted);
        pump.Exception?.GetBaseException().Message.ShouldBeNull();

        await WaitUntil(() => connector.Sent.Contains("ping") || pump.IsFaulted);
        pump.Exception?.GetBaseException().Message.ShouldBeNull();
        connector.Sockets[0].Enqueue(ReadPong());
        await WaitUntil(() => connector.Sent.Count(text => text == "ping") >= 2 || pump.IsFaulted);

        pump.Exception?.GetBaseException().Message.ShouldBeNull();
        connector.Sent.Count(text => text == "ping").ShouldBeGreaterThanOrEqualTo(2);
        connector.Sent.ShouldContain("pong");
        harness.Handler.Calls.ShouldBe(0);
        cts.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => pump);
    }

    private static void AssertSpotCandle(IReadOnlyList<Candle> candles, DateTimeOffset open)
    {
        var candle = candles.ShouldHaveSingleItem();
        candle.IsClosed.ShouldBeTrue();
        candle.VolumeUnit.ShouldBe(VolumeUnit.BaseAsset);
        candle.Open.ShouldBe(3.731m);
        candle.High.ShouldBe(3.799m);
        candle.Low.ShouldBe(3.494m);
        candle.Close.ShouldBe(3.72m);
        candle.Volume.ShouldBe(24912403m);
        candle.QuoteVolume.ShouldBe(67632347.24399722m);
        candle.BaseVolume.ShouldBeNull();
        candle.OpenTimeUtc.ShouldBe(open);
        candle.OpenTimeUtc.Offset.ShouldBe(TimeSpan.Zero);
        candle.CloseTimeUtc.ShouldBe(open.AddMinutes(1));
        candle.CloseTimeUtc.Offset.ShouldBe(TimeSpan.Zero);
    }

    private static async Task PumpAsync(OkxMarketDataClient client, CancellationToken cancellationToken)
    {
        await foreach (var marketEvent in client.SubscribeAsync(
            new MarketSubscription(
                new InstrumentId("OKX-SPOT-BTC-USDT"),
                Timeframe: null,
                IncludeCandles: false,
                IncludeTicker: true,
                IncludeTrades: false,
                IncludeOrderBook: false),
            cancellationToken))
        {
            _ = marketEvent;
        }
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100 && !condition(); attempt++)
        {
            await Task.Delay(50);
        }

        condition().ShouldBeTrue();
    }

    private static bool HasHttpClientField(Type type) =>
        type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
            .Any(field => field.FieldType == typeof(HttpClient) || field.FieldType == typeof(HttpMessageInvoker));

    private static string ReadPong()
    {
        var text = ReadFixture("ws-pong.json", "\"pong\"").Trim();
        return text.StartsWith('"') ? JsonSerializer.Deserialize<string>(text)! : text;
    }

    private static string ReadFixture(string name, string embedded)
    {
        foreach (var path in new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "Okx", name),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Fixtures", "Okx", name)),
        })
        {
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }
        }

        return embedded;
    }

    private const string EmbeddedSpotCandles = """
        {"code":"0","msg":"","data":[
          ["1597026383085","3.721","3.743","3.677","3.708","8422410","22698348.04828491","12698348.04828491","0"],
          ["1597026383085","3.731","3.799","3.494","3.72","24912403","67632347.24399722","37632347.24399722","1"]
        ]}
        """;

    private const string EmbeddedSwapCandles = """
        {"code":"0","msg":"","data":[
          ["1597026383085","3721","3743","3677","3708","10","0.1","370.8","0"],
          ["1597026323085","3700","3730","3690","3720","250","2.5","9300","1"]
        ]}
        """;

    private const string EmbeddedError = """
        {"code":"51000","msg":"Incorrect type of posSide","data":[]}
        """;

    private const string EmbeddedSpotInstrument = """
        {"code":"0","msg":"","data":[{
          "instType":"SPOT","instId":"BTC-USDT","baseCcy":"BTC","quoteCcy":"USDT","state":"live",
          "tickSz":"0.1","lotSz":"0.00000001","minSz":"0.00001"
        }]}
        """;

    private const string EmbeddedEmptyMultiplier = """
        {"code":"0","msg":"","data":[{
          "instType":"SWAP","instId":"BTC-USDT-SWAP","state":"live","ctType":"linear",
          "ctVal":"0.01","ctMult":"","ctValCcy":"BTC","settleCcy":"USDT",
          "tickSz":"0.1","lotSz":"","minSz":""
        }]}
        """;

    private const string EmbeddedSubscribeAck = """
        {"event":"subscribe","arg":{"channel":"tickers","instId":"BTC-USDT"}}
        """;

    private const string EmbeddedSubscribeError = """
        {"event":"error","code":"60012","msg":"Invalid request","connId":"a4d3ae55"}
        """;

    private const string SupportedSwapEnvelope = """
        {"code":"0","msg":"","data":[
          {"instType":"SWAP","instId":"BTC-USDT-SWAP","state":"live","ctType":"linear","ctVal":"0.01","ctMult":"1","ctValCcy":"BTC","settleCcy":"USDT","tickSz":"0.1","lotSz":"1","minSz":"1"},
          {"instType":"SWAP","instId":"ETH-USDT-SWAP","state":"live","ctType":"linear","ctVal":"","ctMult":"1","ctValCcy":"ETH","settleCcy":"USDT","tickSz":"0.01","lotSz":"1","minSz":"1"},
          {"instType":"SWAP","instId":"BTC-USD-SWAP","state":"live","ctType":"inverse","ctVal":"100","ctMult":"1","ctValCcy":"USD","settleCcy":"BTC","tickSz":"0.1","lotSz":"1","minSz":"1"},
          {"instType":"OPTION","instId":"BTC-USD-241227-100000-C","state":"live","ctType":"linear","ctVal":"0.01","ctMult":"1","ctValCcy":"BTC","settleCcy":"USDT","tickSz":"0.1","lotSz":"1","minSz":"1","baseCcy":"BTC","quoteCcy":"USDT"},
          {"instType":"FUTURES","instId":"BTC-USDT-241227","state":"live","ctType":"linear","ctVal":"0.01","ctMult":"1","ctValCcy":"BTC","settleCcy":"USDT","tickSz":"0.1","lotSz":"1","minSz":"1"}
        ]}
        """;

    private sealed class MarketHarness : IDisposable
    {
        private readonly HttpClient _http;

        private MarketHarness(OkxMarketDataClient client, RecordingHandler handler, HttpClient http)
        {
            Client = client;
            Handler = handler;
            _http = http;
        }

        public OkxMarketDataClient Client { get; }

        public RecordingHandler Handler { get; }

        public static MarketHarness Fixed(string body, HttpStatusCode status = HttpStatusCode.OK) =>
            Route(_ => Json(body, status));

        public static MarketHarness Route(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
            Create(respond, new OkxOptions
            {
                RestBaseUrl = "http://127.0.0.1:9/",
                Enabled = true,
                MaxPages = 4,
                MaxCandlesPerRequest = 100,
                MaxHistoryCandlesPerRequest = 100,
                MaxRequestsPerWindow = 100,
                RateLimitWindowSeconds = 2,
                InstrumentTypes = ["SPOT", "SWAP"],
            }, connector: null);

        public static MarketHarness Sockets(ScriptedConnector connector, int ackTimeoutSeconds, int pingIntervalSeconds) =>
            Create(
                _ => throw new InvalidOperationException("websocket test called HTTP"),
                new OkxOptions
                {
                    RestBaseUrl = "http://127.0.0.1:9/",
                    PublicWebSocketUrl = "wss://127.0.0.1/ws/v5/public",
                    BusinessWebSocketUrl = "wss://127.0.0.1/ws/v5/business",
                    Enabled = true,
                    MaxRequestsPerWindow = 100,
                    RateLimitWindowSeconds = 2,
                    ChannelCapacity = 8,
                    AckTimeoutSeconds = ackTimeoutSeconds,
                    PingIntervalSeconds = pingIntervalSeconds,
                    InstrumentTypes = ["SPOT", "SWAP"],
                },
                connector);

        public void AssertLocal()
        {
            Handler.Calls.ShouldBeGreaterThan(0);
            foreach (var request in Handler.Requests)
            {
                request.RequestUri.ShouldNotBeNull();
                request.RequestUri!.Host.ShouldBe("127.0.0.1");
            }
        }

        public void Dispose() => _http.Dispose();

        public static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
            new(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };

        private static MarketHarness Create(
            Func<HttpRequestMessage, HttpResponseMessage> respond,
            OkxOptions options,
            IOkxSocketConnector? connector)
        {
            var handler = new RecordingHandler(respond);
            var http = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://127.0.0.1:9/"),
                Timeout = TimeSpan.FromSeconds(5),
            };
            var clock = TimeProvider.System;
            var configured = Options.Create(options);
            var rest = new OkxPublicRest(
                http,
                new OkxRateLimiter(configured, clock),
                clock,
                NullLogger<OkxPublicRest>.Instance);
            var client = connector is null
                ? new OkxMarketDataClient(rest, configured, clock, NullLogger<OkxMarketDataClient>.Instance)
                : new OkxMarketDataClient(rest, configured, clock, NullLogger<OkxMarketDataClient>.Instance, connector);
            return new MarketHarness(client, handler, http);
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        public int Calls { get; private set; }

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Requests.Add(request);
            return Task.FromResult(_respond(request));
        }
    }

    private sealed class ScriptedConnector : IOkxSocketConnector
    {
        private readonly Func<ScriptedSocket, string?> _firstMessage;

        public ScriptedConnector(Func<ScriptedSocket, string?> firstMessage) => _firstMessage = firstMessage;

        public List<ScriptedSocket> Sockets { get; } = [];

        public IEnumerable<string> Sent => Sockets.SelectMany(socket => socket.Sent);

        public Task<IOkxSocket> ConnectAsync(Uri uri, CancellationToken cancellationToken)
        {
            uri.Scheme.ShouldBe("wss");
            uri.Host.ShouldBe("127.0.0.1");
            var socket = new ScriptedSocket();
            Sockets.Add(socket);
            var first = _firstMessage(socket);
            if (first is not null)
            {
                socket.Enqueue(first);
            }

            return Task.FromResult<IOkxSocket>(socket);
        }
    }

    private sealed class ScriptedSocket : IOkxSocket
    {
        private readonly Channel<string> _incoming = Channel.CreateUnbounded<string>();
        private readonly List<string> _sent = [];
        private int _state = (int)WebSocketState.Open;

        public WebSocketState State => (WebSocketState)Volatile.Read(ref _state);

        public string[] Sent
        {
            get
            {
                lock (_sent)
                {
                    return _sent.ToArray();
                }
            }
        }

        public Task SendTextAsync(string text, CancellationToken cancellationToken)
        {
            lock (_sent)
            {
                _sent.Add(text);
            }

            return Task.CompletedTask;
        }

        public async ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            var text = await _incoming.Reader.ReadAsync(cancellationToken);
            var bytes = Encoding.UTF8.GetBytes(text);
            if (bytes.Length > buffer.Length)
            {
                throw new InvalidOperationException("Scripted websocket message does not fit the receive buffer.");
            }

            bytes.CopyTo(buffer.Span);
            return new ValueWebSocketReceiveResult(bytes.Length, WebSocketMessageType.Text, endOfMessage: true);
        }

        public void Enqueue(string text) => _incoming.Writer.TryWrite(text);

        public void Abort() => Volatile.Write(ref _state, (int)WebSocketState.Aborted);

        public ValueTask DisposeAsync()
        {
            Volatile.Write(ref _state, (int)WebSocketState.Closed);
            return ValueTask.CompletedTask;
        }
    }
}
