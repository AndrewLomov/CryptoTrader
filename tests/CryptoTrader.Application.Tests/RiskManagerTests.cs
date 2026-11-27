using CryptoTrader.Application.Configuration;
using CryptoTrader.Application.Risk;
using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.Instruments;
using CryptoTrader.Domain.Risk;
using CryptoTrader.Domain.Strategies;
using CryptoTrader.Domain.Trading;
using Microsoft.Extensions.Options;
using Shouldly;

namespace CryptoTrader.Application.Tests;

public class RiskManagerTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Emergency_stop_rejects_entry()
    {
        var decision = await Evaluate(
            EnterLong(),
            Portfolio(emergencyStop: true),
            FreshMarket());

        decision.Approved.ShouldBeFalse();
        decision.RejectionCode.ShouldBe(RiskRejectionCodes.EmergencyStop);
    }

    [Fact]
    public async Task Stale_market_data_rejects_entry()
    {
        var decision = await Evaluate(
            EnterLong(),
            Portfolio(),
            new MarketSnapshot(Now, Now, healthy: false, stale: true, bid: 100m, ask: 100.1m, last: 100m));

        decision.Approved.ShouldBeFalse();
        decision.RejectionCode.ShouldBe(RiskRejectionCodes.StaleMarketData);
    }

    [Fact]
    public async Task Daily_loss_limit_rejects_entry()
    {
        // Day start is equity - day PnL = 10_000. Loss of 200 is 2%, above the 1% profile limit.
        var decision = await Evaluate(
            EnterLong(),
            Portfolio(equity: 9_800m, availableCash: 9_800m, dayPnl: -200m),
            FreshMarket());

        decision.Approved.ShouldBeFalse();
        decision.RejectionCode.ShouldBe(RiskRejectionCodes.DailyLossLimit);
    }

    [Fact]
    public async Task Max_open_positions_rejects_a_new_entry()
    {
        var decision = await Evaluate(
            EnterLong(),
            Portfolio(openCount: RiskProfile.Default.MaxOpenPositions),
            FreshMarket());

        decision.Approved.ShouldBeFalse();
        decision.RejectionCode.ShouldBe(RiskRejectionCodes.MaxOpenPositions);
    }

    [Fact]
    public async Task Exit_without_a_position_is_rejected()
    {
        var decision = await Evaluate(
            ExitLong(),
            Portfolio(hasPosition: false, openQuantity: null),
            FreshMarket());

        decision.Approved.ShouldBeFalse();
        decision.RejectionCode.ShouldBe(RiskRejectionCodes.NoOpenPosition);
    }

    private static Task<RiskDecision> Evaluate(
        TradeSignal signal,
        PortfolioSnapshot portfolio,
        MarketSnapshot market)
    {
        var manager = new RiskManager(
            Options.Create(new ExecutionOptions
            {
                Mode = TradingMode.Paper,
                LiveTradingEnabled = false,
                PaperInitialEquity = 10_000m,
            }),
            TimeProvider.System);
        return manager.EvaluateAsync(signal, portfolio, market, SpotBtc(), RiskProfile.Default, CancellationToken.None).AsTask();
    }

    private static PortfolioSnapshot Portfolio(
        decimal equity = 10_000m,
        decimal availableCash = 10_000m,
        decimal dayPnl = 0m,
        int openCount = 0,
        bool hasPosition = false,
        decimal? openQuantity = null,
        bool emergencyStop = false) => new(
        equity,
        availableCash,
        "USDT",
        dayPnl,
        drawdownFraction: 0m,
        openCount,
        grossExposure: 0m,
        hasPosition,
        positionIsLosing: false,
        openQuantity,
        emergencyStop,
        approvedSignalIds: []);

    private static MarketSnapshot FreshMarket() => new(Now, Now, healthy: true, stale: false, 100m, 100.1m, 100m);

    private static TradeSignal EnterLong() => TradeSignal.Create(
        Guid.Parse("10000000-0000-0000-0000-000000000001"),
        "trend-following",
        "0.1.0",
        new InstrumentId("BTCUSDT"),
        SignalAction.EnterLong,
        Now,
        new Price(100m),
        new Price(90m),
        new Price(120m),
        5m,
        "entry fixture");

    private static TradeSignal ExitLong() => TradeSignal.Create(
        Guid.Parse("10000000-0000-0000-0000-000000000002"),
        "trend-following",
        "0.1.0",
        new InstrumentId("BTCUSDT"),
        SignalAction.ExitLong,
        Now,
        new Price(100m),
        stopLossPrice: null,
        takeProfitPrice: null,
        atr: null,
        "exit fixture");

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
        contractMultiplier: null,
        isTradingEnabled: true);
}
