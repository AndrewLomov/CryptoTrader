using System.Net;
using System.Reflection;
using System.Text;
using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.MarketData;
using CryptoTrader.Domain.Trading;
using CryptoTrader.Exchanges.Abstractions;
using CryptoTrader.Exchanges.Abstractions.MarketData;
using CryptoTrader.Exchanges.Abstractions.Models;
using CryptoTrader.Exchanges.Bybit;
using CryptoTrader.Exchanges.Bybit.Trading;
using Microsoft.Extensions.Options;
using Shouldly;

namespace CryptoTrader.Exchanges.ContractTests;

public class BybitContractTests
{
    private const string KlineJson = """
        {
          "retCode": 0,
          "retMsg": "OK",
          "result": {
            "category": "spot",
            "symbol": "BTCUSDT",
            "list": [
              ["1704067200000", "25000.1", "25100.1", "24900.1", "25050.3", "0.1", "2505.03"]
            ]
          },
          "time": 1704067260000
        }
        """;

    [Fact]
    public async Task SubmitAsync_returns_live_trading_disabled_and_known_state()
    {
        var client = new BybitTradingClient();

        var result = await client.SubmitAsync(
            new ExchangeOrderRequest(
                "client-1",
                new InstrumentId("BYBIT-SPOT-BTCUSDT"),
                OrderSide.Buy,
                OrderType.Market,
                new Quantity(0.01m),
                LimitPrice: null,
                ReduceOnly: false),
            CancellationToken.None);

        result.ErrorCode.ShouldBe(ExchangeErrorCodes.LiveTradingDisabled);
        result.UnknownState.ShouldBeFalse();
        result.Accepted.ShouldBeFalse();
    }

    [Fact]
    public async Task Kline_numeric_strings_parse_to_decimal_and_utc()
    {
        var expectedOpen = DateTimeOffset.FromUnixTimeMilliseconds(1_704_067_200_000);
        var mapper = FindPublicMapper(typeof(BybitTradingClient).Assembly);
        if (mapper is not null)
        {
            AssertCandle(InvokeMapper(mapper), expectedOpen);
            return;
        }

        var handler = new RecordingHandler(KlineJson);
        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1:9/"),
            Timeout = TimeSpan.FromSeconds(2),
        };
        var client = CreateMarketDataClient(http);
        var candles = await client.GetCandlesAsync(
            new InstrumentId("BYBIT-SPOT-BTCUSDT"),
            Timeframe.OneHour,
            expectedOpen,
            expectedOpen.AddHours(2),
            CancellationToken.None);

        handler.Calls.ShouldBeGreaterThan(0);
        foreach (var request in handler.Requests)
        {
            request.RequestUri.ShouldNotBeNull();
            request.RequestUri!.Host.ShouldBe("127.0.0.1");
        }
        AssertCandle(candles, expectedOpen);
    }

    private static void AssertCandle(IReadOnlyList<Candle> candles, DateTimeOffset expectedOpen)
    {
        var candle = candles.ShouldHaveSingleItem();
        candle.Open.ShouldBe(25000.1m);
        candle.High.ShouldBe(25100.1m);
        candle.Low.ShouldBe(24900.1m);
        candle.Close.ShouldBe(25050.3m);
        candle.Volume.ShouldBe(0.1m);
        candle.OpenTimeUtc.ShouldBe(expectedOpen);
        candle.OpenTimeUtc.Offset.ShouldBe(TimeSpan.Zero);
        candle.CloseTimeUtc.Offset.ShouldBe(TimeSpan.Zero);
    }

    private static IMarketDataClient CreateMarketDataClient(HttpClient http)
    {
        var assembly = typeof(BybitTradingClient).Assembly;
        var options = Options.Create(new BybitOptions
        {
            RestBaseUrl = "http://127.0.0.1:9/",
            MaxRequestsPerSecond = 10,
        });
        var rateGateType = assembly.GetType("CryptoTrader.Exchanges.Bybit.Rest.BybitRateGate", throwOnError: true)!;
        var restType = assembly.GetType("CryptoTrader.Exchanges.Bybit.Rest.BybitPublicRest", throwOnError: true)!;
        var marketType = assembly.GetType("CryptoTrader.Exchanges.Bybit.MarketData.BybitMarketDataClient", throwOnError: true)!;
        var rateGate = Activator.CreateInstance(rateGateType, options, TimeProvider.System)!;
        var rest = Activator.CreateInstance(restType, new StubHttpClientFactory(http), rateGate, LoggerFor(restType))!;
        return (IMarketDataClient)Activator.CreateInstance(
            marketType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            [rest, options, TimeProvider.System, LoggerFor(marketType)],
            culture: null)!;
    }

    private static object LoggerFor(Type category)
    {
        var assembly = Assembly.Load("Microsoft.Extensions.Logging.Abstractions");
        var open = assembly.GetExportedTypes()
            .Single(type => type.IsGenericTypeDefinition && type.Name == "NullLogger`1");
        return open.MakeGenericType(category)
            .GetField("Instance", BindingFlags.Public | BindingFlags.Static)!
            .GetValue(null)!;
    }

    private static MethodInfo? FindPublicMapper(Assembly assembly)
    {
        var names = new HashSet<string>(StringComparer.Ordinal)
        {
            "ParseKline",
            "ParseKlines",
            "ParseCandles",
            "ToCandle",
            "ToCandles",
            "MapKline",
            "MapKlines",
            "MapCandles",
        };
        var matches = assembly.GetTypes()
            .Where(type => type.IsPublic && type is { IsClass: true, IsAbstract: false })
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(method => names.Contains(method.Name))
            .ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static IReadOnlyList<Candle> InvokeMapper(MethodInfo mapper)
    {
        var args = mapper.GetParameters().Select(parameter => ArgumentFor(parameter.ParameterType)).ToArray();
        var target = mapper.IsStatic ? null : Activator.CreateInstance(mapper.DeclaringType!);
        var result = mapper.Invoke(target, args);
        return result switch
        {
            Candle candle => [candle],
            IReadOnlyList<Candle> candles => candles,
            IEnumerable<Candle> candles => candles.ToArray(),
            _ => throw new InvalidOperationException($"Mapper {mapper.DeclaringType?.Name}.{mapper.Name} did not return candles."),
        };
    }

    private static object? ArgumentFor(Type type)
    {
        if (type == typeof(string))
        {
            return KlineJson;
        }

        if (type == typeof(InstrumentId))
        {
            return new InstrumentId("BYBIT-SPOT-BTCUSDT");
        }

        if (type == typeof(Timeframe))
        {
            return Timeframe.OneHour;
        }

        if (type == typeof(ExchangeId))
        {
            return ExchangeId.Bybit;
        }

        if (type == typeof(CancellationToken))
        {
            return CancellationToken.None;
        }

        if (type == typeof(DateTimeOffset))
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(1_704_067_200_000);
        }

        throw new InvalidOperationException($"Public kline mapper expects unsupported argument {type}.");
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _http;

        public StubHttpClientFactory(HttpClient http) => _http = http;

        public HttpClient CreateClient(string name) => _http;
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly string _body;

        public RecordingHandler(string body) => _body = body;

        public int Calls { get; private set; }

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
            });
        }
    }
}
