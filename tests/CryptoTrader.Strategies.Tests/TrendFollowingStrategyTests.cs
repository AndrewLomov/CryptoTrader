using System.Text.Json;
using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.Instruments;
using CryptoTrader.Domain.MarketData;
using CryptoTrader.Domain.Strategies;
using CryptoTrader.Strategies.Abstractions;
using CryptoTrader.Strategies.TrendFollowing;
using Shouldly;

namespace CryptoTrader.Strategies.Tests;

public class TrendFollowingStrategyTests
{
    private static readonly DateTimeOffset SignalOpen = new(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Unfinished_warm_up_emits_no_signal()
    {
        var (working, higher) = Fixture();
        var signals = await Evaluate(
            ParametersJson(),
            working.Take(2).ToArray(),
            higher.Take(2).ToArray(),
            working[1].CloseTimeUtc);

        signals.ShouldBeEmpty();
    }

    [Fact]
    public async Task Rsi_cross_up_in_an_uptrend_emits_enter_long_without_a_future_spike()
    {
        var (working, higher) = Fixture();
        var signalClose = working[^1].CloseTimeUtc;
        var spike = Hour(working[^1].OpenTimeUtc.AddHours(1), close: 1_000m);
        var extended = working.Append(spike).ToArray();
        var truncated = extended.Where(candle => candle.CloseTimeUtc <= signalClose).ToArray();

        var json = ParametersJson();
        var fromPrefix = await Evaluate(json, working, higher, signalClose);
        var fromTruncated = await Evaluate(json, truncated, higher, signalClose);
        var withFutureSpike = await Evaluate(json, extended, higher, signalClose);

        var signal = fromPrefix.ShouldHaveSingleItem();
        signal.Action.ShouldBe(SignalAction.EnterLong);
        signal.ReferencePrice.Value.ShouldBe(98m);
        signal.OccurredAtUtc.ShouldBe(signalClose);
        signal.InstrumentId.ShouldBe(new InstrumentId("BTCUSDT"));
        signal.StopLossPrice.ShouldNotBeNull();
        signal.StopLossPrice.Value.Value.ShouldBeLessThan(signal.ReferencePrice.Value);

        var truncatedSignal = fromTruncated.ShouldHaveSingleItem();
        truncatedSignal.Action.ShouldBe(signal.Action);
        truncatedSignal.ReferencePrice.ShouldBe(signal.ReferencePrice);
        truncatedSignal.OccurredAtUtc.ShouldBe(signal.OccurredAtUtc);
        truncatedSignal.StopLossPrice.ShouldBe(signal.StopLossPrice);

        var future = withFutureSpike.ShouldHaveSingleItem();
        future.Action.ShouldBe(signal.Action);
        future.ReferencePrice.ShouldBe(signal.ReferencePrice);
        future.OccurredAtUtc.ShouldBe(signal.OccurredAtUtc);
        future.StopLossPrice.ShouldBe(signal.StopLossPrice);
        working.Any(candle => candle.Close == 1_000m).ShouldBeFalse();
        truncated.Any(candle => candle.Close == 1_000m).ShouldBeFalse();
        spike.CloseTimeUtc.ShouldBeGreaterThan(signalClose);
    }

    private static async Task<IReadOnlyList<TradeSignal>> Evaluate(
        string parametersJson,
        IReadOnlyList<Candle> working,
        IReadOnlyList<Candle> higher,
        DateTimeOffset asOf)
    {
        var strategy = new TrendFollowingStrategy(parametersJson);
        var context = new StrategyContext(
            SpotBtc(),
            asOf,
            parametersJson,
            working,
            higher,
            HasOpenPosition: false);
        return await strategy.EvaluateAsync(context, CancellationToken.None);
    }

    private static string ParametersJson() => JsonSerializer.Serialize(new TrendFollowingParameters
    {
        EmaFastPeriod = 3,
        EmaSlowPeriod = 5,
        RsiPeriod = 3,
        AtrPeriod = 3,
        RsiCrossThreshold = 40m,
        AtrStopMultiplier = 2m,
        AtrTakeProfitMultiplier = 3m,
    });

    private static (Candle[] Working, Candle[] Higher) Fixture()
    {
        // 1h closes fall to 95 (RSI stays 0) and the last close 98 crosses back through 40.
        var closes = new decimal[] { 100m, 99m, 98m, 97m, 96m, 95m, 98m };
        var working = closes
            .Select((close, index) => Hour(SignalOpen.AddHours(index - (closes.Length - 1)), close))
            .ToArray();

        // Monotonic 4h closes: EMA 3 ends above EMA 5, and the last close is above EMA 5.
        var higherCloses = new decimal[] { 10m, 12m, 14m, 16m, 18m, 20m, 22m, 24m };
        var lastHigherOpen = new DateTimeOffset(2026, 3, 1, 4, 0, 0, TimeSpan.Zero);
        var higher = higherCloses
            .Select((close, index) => FourHour(lastHigherOpen.AddHours(4 * (index - (higherCloses.Length - 1))), close))
            .ToArray();
        higher[^1].CloseTimeUtc.ShouldBeLessThanOrEqualTo(working[^1].CloseTimeUtc);
        return (working, higher);
    }

    private static Candle Hour(DateTimeOffset open, decimal close) => Candle.Create(
        ExchangeId.Bybit,
        new InstrumentId("BTCUSDT"),
        Timeframe.OneHour,
        open,
        open.AddHours(1),
        close,
        close + 1m,
        close - 1m,
        close,
        1m,
        true);

    private static Candle FourHour(DateTimeOffset open, decimal close) => Candle.Create(
        ExchangeId.Bybit,
        new InstrumentId("BTCUSDT"),
        Timeframe.FourHours,
        open,
        open.AddHours(4),
        close,
        close + 1m,
        close - 1m,
        close,
        1m,
        true);

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
}
