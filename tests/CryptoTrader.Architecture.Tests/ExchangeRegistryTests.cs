using CryptoTrader.Application.Abstractions.MarketData;
using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.MarketData;
using CryptoTrader.Exchanges.Abstractions;
using CryptoTrader.Exchanges.Abstractions.Accounts;
using CryptoTrader.Exchanges.Abstractions.MarketData;
using CryptoTrader.Exchanges.Abstractions.Models;
using CryptoTrader.Exchanges.Abstractions.Trading;
using Shouldly;

namespace CryptoTrader.Architecture.Tests;

public class ExchangeRegistryTests
{
    [Fact]
    public void Resolves_adapters_by_exchange_id()
    {
        var okx = new StubAdapter("OKX");
        var bybit = new StubAdapter("BYBIT");
        var registry = new ExchangeRegistry([okx, bybit]);

        registry.GetRequired(ExchangeId.Bybit).ShouldBeSameAs(bybit);
        registry.GetRequired(ExchangeId.Okx).ShouldBeSameAs(okx);
        registry.TryGet(ExchangeId.Okx, out var found).ShouldBeTrue();
        found.ShouldBeSameAs(okx);
        registry.GetAll()
            .Select(item => item.ExchangeCode)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ShouldBe(["BYBIT", "OKX"]);
        IExchangeAdapter okxPort = okx;
        okxPort.ExchangeId.ShouldBe(ExchangeId.Okx);
        okxPort.OrderBook.ShouldBeNull();
        okxPort.FundingRates.ShouldBeNull();
    }

    [Fact]
    public void Duplicate_exchange_is_rejected()
    {
        var exception = Should.Throw<InvalidOperationException>(() =>
            new ExchangeRegistry([new StubAdapter("BYBIT"), new StubAdapter("BYBIT")]));

        exception.Message.ShouldBe("Exchange 'BYBIT' is registered more than once.");
    }

    [Fact]
    public void Unknown_exchange_is_rejected()
    {
        var registry = new ExchangeRegistry([new StubAdapter("BYBIT")]);

        registry.TryGet(ExchangeId.Okx, out var missing).ShouldBeFalse();
        missing.ShouldBeNull();
        var exception = Should.Throw<InvalidOperationException>(() => registry.GetRequired(ExchangeId.Okx));
        exception.Message.ShouldBe("Exchange 'OKX' is not registered.");
    }

    [Fact]
    public void Null_adapter_is_rejected()
    {
        Should.Throw<ArgumentNullException>(() => new ExchangeRegistry(null!));
        Should.Throw<ArgumentNullException>(() => new ExchangeRegistry(new IExchangeAdapter[] { null! }));
    }

    [Fact]
    public void Futures_accounting_defaults_to_false()
    {
        var capabilities = new ExchangeCapabilities(
            SupportsSpot: true,
            SupportsLinearPerpetual: true,
            SupportsLiveOrders: false,
            SupportsOrderBook: true,
            SupportsFundingRates: true,
            SupportsWebSocket: true,
            SupportsPrivateAccount: false);

        capabilities.SupportsFuturesAccounting.ShouldBeFalse();
        capabilities.SupportsLiveOrders.ShouldBeFalse();
        capabilities.SupportsPrivateAccount.ShouldBeFalse();
        capabilities.SupportsLinearPerpetual.ShouldBeTrue();
    }

    [Fact]
    public void Checkpoint_names_the_paper_consumer()
    {
        ICandleConsumerCheckpoint.PaperConsumer.ShouldBe("paper");
        ((int)CandleConsumerState.Pending).ShouldBe(1);
        ((int)CandleConsumerState.Processed).ShouldBe(2);
        ((int)CandleConsumerState.Skipped).ShouldBe(3);
    }

    [Fact]
    public async Task Default_history_is_unverified_or_empty()
    {
        var open = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
        var candle = Candle.Create(
            ExchangeId.Bybit,
            new InstrumentId("BTCUSDT"),
            Timeframe.OneHour,
            open,
            open.AddHours(1),
            10m,
            11m,
            9m,
            10m,
            1m,
            true);
        IMarketDataClient populated = new FixedCandles([candle]);
        var unverified = await populated.GetCandleHistoryAsync(
            candle.Instrument,
            candle.Timeframe,
            open,
            open.AddHours(1),
            CancellationToken.None);

        unverified.Completeness.ShouldBe(CandleHistoryCompleteness.Unverified);
        unverified.Candles.Count.ShouldBe(1);
        unverified.Candles[0].ShouldBeSameAs(candle);
        unverified.Reason.ShouldBe("Adapter did not report whether the requested range was complete.");

        IMarketDataClient empty = new FixedCandles([]);
        var none = await empty.GetCandleHistoryAsync(
            candle.Instrument,
            candle.Timeframe,
            open,
            open.AddHours(1),
            CancellationToken.None);

        none.Completeness.ShouldBe(CandleHistoryCompleteness.Empty);
        none.Candles.ShouldBeEmpty();
        none.Reason.ShouldBe(unverified.Reason);
    }

    private sealed class StubAdapter : IExchangeAdapter
    {
        public StubAdapter(string code) => ExchangeCode = code;

        public string ExchangeCode { get; }

        public IMarketDataClient MarketData => throw new NotSupportedException();

        public ITradingClient Trading => throw new NotSupportedException();

        public IAccountClient Account => throw new NotSupportedException();

        public IInstrumentMetadataClient Instruments => throw new NotSupportedException();

        public ExchangeCapabilities Capabilities => new(true, false, false, false, false, false, false);
    }

    private sealed class FixedCandles : IMarketDataClient
    {
        private readonly IReadOnlyList<Candle> _candles;

        public FixedCandles(IReadOnlyList<Candle> candles) => _candles = candles;

        public Task<IReadOnlyList<Candle>> GetCandlesAsync(
            InstrumentId instrument,
            Timeframe timeframe,
            DateTimeOffset fromUtc,
            DateTimeOffset toUtc,
            CancellationToken cancellationToken) => Task.FromResult(_candles);

        public Task<Ticker?> GetTickerAsync(InstrumentId instrument, CancellationToken cancellationToken) =>
            Task.FromResult<Ticker?>(null);

        public IAsyncEnumerable<MarketEvent> SubscribeAsync(
            MarketSubscription subscription,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
