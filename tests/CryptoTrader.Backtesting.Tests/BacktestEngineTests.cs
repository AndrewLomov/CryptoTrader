using CryptoTrader.Application.Abstractions.Backtesting;
using CryptoTrader.Application.Configuration;
using CryptoTrader.Application.Risk;
using CryptoTrader.Backtesting;
using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.Instruments;
using CryptoTrader.Domain.MarketData;
using CryptoTrader.Domain.Risk;
using CryptoTrader.Domain.Strategies;
using CryptoTrader.Strategies.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;

namespace CryptoTrader.Backtesting.Tests;

public class BacktestEngineTests
{
    private static readonly DateTimeOffset Start = new(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);
    private const decimal SlippageBps = 100m;
    private const decimal Atr = 10m;

    [Fact]
    public async Task Same_input_run_twice_matches_metrics_and_trade_prices()
    {
        var engine = CreateEngine();
        var strategy = new EnterOnceStrategy(Start);
        var request = Request(fee: 0m);

        var first = await engine.RunAsync(request, strategy, CancellationToken.None);
        var second = await engine.RunAsync(request, strategy, CancellationToken.None);

        second.Metrics.ShouldBe(first.Metrics);
        second.Trades.Count.ShouldBe(first.Trades.Count);
        second.Trades.Count.ShouldBeGreaterThan(0);
        for (var index = 0; index < first.Trades.Count; index++)
        {
            second.Trades[index].EntryPrice.ShouldBe(first.Trades[index].EntryPrice);
            second.Trades[index].ExitPrice.ShouldBe(first.Trades[index].ExitPrice);
            second.Trades[index].Quantity.ShouldBe(first.Trades[index].Quantity);
            second.Trades[index].NetPnl.ShouldBe(first.Trades[index].NetPnl);
            second.Trades[index].ExitReason.ShouldBe(first.Trades[index].ExitReason);
        }

        first.EngineVersion.ShouldBe(BacktestEngineInfo.Version);
        first.Assumptions.FundingModeled.ShouldBeFalse();
    }

    [Fact]
    public async Task Bar_touching_stop_and_take_profit_exits_at_the_slipped_stop()
    {
        var report = await CreateEngine().RunAsync(Request(fee: 0m), new EnterOnceStrategy(Start), CancellationToken.None);
        var trade = report.Trades.ShouldHaveSingleItem();
        var fill = 110m * (1m + SlippageBps / 10_000m);
        var stop = fill - (Atr * 2m);

        trade.ExitReason.ShouldBe(BacktestExitReason.StopLoss);
        trade.EntryPrice.ShouldBe(fill);
        trade.ExitPrice.ShouldBe(stop * (1m - SlippageBps / 10_000m));
        trade.ExitPrice.ShouldBeLessThan(stop);
    }

    [Fact]
    public async Task Fees_reduce_final_equity()
    {
        var strategy = new EnterOnceStrategy(Start);
        var withoutFees = await CreateEngine().RunAsync(Request(0m), strategy, CancellationToken.None);
        var withFees = await CreateEngine().RunAsync(Request(0.001m), strategy, CancellationToken.None);

        withoutFees.Metrics.TotalFees.ShouldBe(0m);
        withFees.Metrics.TotalFees.ShouldBeGreaterThan(0m);
        withFees.Metrics.FinalEquity.ShouldBeLessThan(withoutFees.Metrics.FinalEquity);
        withFees.Trades.ShouldHaveSingleItem().Quantity.ShouldBe(withoutFees.Trades.Single().Quantity);
    }

    [Fact]
    public async Task Candle_after_the_decision_is_not_used_as_the_signal_price()
    {
        var strategy = new EnterOnceStrategy(Start);
        var report = await CreateEngine().RunAsync(Request(0m), strategy, CancellationToken.None);

        strategy.ClosesAtSignal.ShouldNotBeNull();
        strategy.ClosesAtSignal.ShouldBe([100m]);
        strategy.ClosesAtSignal.ShouldNotContain(5_000m);
        var trade = report.Trades.ShouldHaveSingleItem();
        trade.EntryPrice.ShouldBe(110m * (1m + SlippageBps / 10_000m));
        trade.EntryPrice.ShouldNotBe(5_000m);
        trade.EntryPrice.ShouldNotBe(100m);
    }

    [Fact]
    public async Task Linear_instrument_or_contract_volume_candle_does_not_run_as_a_spot_backtest()
    {
        var engine = CreateEngine();
        var strategy = new EnterOnceStrategy(Start);

        var linear = await Should.ThrowAsync<InvalidOperationException>(
            () => engine.RunAsync(
                Request(0m, LinearBtc(), [Bar(Start, 100m, 101m, 99m, 100m)]),
                strategy,
                CancellationToken.None));
        linear.Message.ShouldBe("BacktestEngine 0.1 applies spot fills only.");

        var contractVolume = await Should.ThrowAsync<InvalidOperationException>(
            () => engine.RunAsync(
                Request(0m, SpotBtc(), [ContractVolumeBar(Start)]),
                strategy,
                CancellationToken.None));
        contractVolume.Message.ShouldBe("BacktestEngine 0.1 accepts base-asset volume only.");
    }

    private static BacktestEngine CreateEngine() =>
        new(new RiskManager(Options.Create(new ExecutionOptions()), TimeProvider.System), TimeProvider.System);

    private static BacktestRequest Request(decimal fee) =>
        Request(
            fee,
            SpotBtc(),
            [
                Bar(Start, 100m, 101m, 99m, 100m),
                Bar(Start.AddHours(1), 110m, 112m, 109m, 111m),
                Bar(Start.AddHours(2), 110m, 5_000m, 50m, 5_000m),
            ]);

    private static BacktestRequest Request(decimal fee, Instrument instrument, IReadOnlyList<Candle> candles) =>
        new(
            "scripted",
            "1",
            """{"AtrStopMultiplier":2,"AtrTakeProfitMultiplier":3}""",
            ExchangeId.Bybit,
            instrument.Id,
            instrument,
            Start,
            candles[^1].CloseTimeUtc,
            InitialEquity: 10_000m,
            fee,
            SlippageBps,
            Profile(fee),
            candles,
            HigherCandles: [],
            ExitOnTrendBreak: false);

    private static RiskProfile Profile(decimal fee) => RiskProfile.Create(
        0.5m,
        1m,
        2,
        100m,
        fee,
        SlippageBps,
        false,
        10m,
        false,
        false);

    private static Candle Bar(DateTimeOffset open, decimal openPrice, decimal high, decimal low, decimal close) =>
        Candle.Create(
            ExchangeId.Bybit,
            new InstrumentId("BTCUSDT"),
            Timeframe.OneHour,
            open,
            open.AddHours(1),
            openPrice,
            high,
            low,
            close,
            1m,
            true);

    private static Candle ContractVolumeBar(DateTimeOffset open) =>
        Candle.Create(
            ExchangeId.Bybit,
            new InstrumentId("BTCUSDT"),
            Timeframe.OneHour,
            open,
            open.AddHours(1),
            open: 100m,
            high: 101m,
            low: 99m,
            close: 100m,
            volume: 3m,
            isClosed: true,
            VolumeUnit.Contract);

    private static Instrument SpotBtc() => Instrument.Create(
        new InstrumentId("BTCUSDT"),
        ExchangeId.Bybit,
        "BTCUSDT",
        "BTC",
        "USDT",
        InstrumentType.Spot,
        0.01m,
        0.001m,
        0.001m,
        5m,
        null,
        true);

    private static Instrument LinearBtc() => Instrument.Create(
        new InstrumentId("BYBIT-LINEAR-BTCUSDT"),
        ExchangeId.Bybit,
        "BTCUSDT",
        "BTC",
        "USDT",
        InstrumentType.LinearPerpetual,
        0.01m,
        0.001m,
        0.001m,
        5m,
        1m,
        true);

    private sealed class EnterOnceStrategy : ITradingStrategy
    {
        private readonly DateTimeOffset _signalOpen;

        public EnterOnceStrategy(DateTimeOffset signalOpen) => _signalOpen = signalOpen;

        public string Id => "scripted";

        public string Version => "1";

        public string DisplayName => "Scripted";

        public IReadOnlyList<decimal>? ClosesAtSignal { get; private set; }

        public ValueTask<IReadOnlyList<TradeSignal>> EvaluateAsync(
            StrategyContext context,
            CancellationToken cancellationToken)
        {
            var last = context.ClosedWorkingCandles[^1];
            if (context.HasOpenPosition || last.OpenTimeUtc != _signalOpen)
            {
                return ValueTask.FromResult<IReadOnlyList<TradeSignal>>([]);
            }

            ClosesAtSignal = context.ClosedWorkingCandles.Select(candle => candle.Close).ToArray();
            var signal = TradeSignal.Create(
                Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
                Id,
                Version,
                context.Instrument.Id,
                SignalAction.EnterLong,
                last.CloseTimeUtc,
                new Price(last.Close),
                new Price(last.Close - (Atr * 2m)),
                new Price(last.Close + (Atr * 3m)),
                Atr,
                "decision candle");
            return ValueTask.FromResult<IReadOnlyList<TradeSignal>>([signal]);
        }
    }
}
