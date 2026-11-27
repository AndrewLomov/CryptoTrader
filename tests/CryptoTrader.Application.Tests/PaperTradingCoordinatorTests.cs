using System.Security.Cryptography;
using System.Text;
using CryptoTrader.Application.Abstractions.Execution;
using CryptoTrader.Application.Abstractions.MarketData;
using CryptoTrader.Application.Abstractions.Paper;
using CryptoTrader.Application.Abstractions.Persistence;
using CryptoTrader.Application.Abstractions.Risk;
using CryptoTrader.Application.Configuration;
using CryptoTrader.Application.MarketData;
using CryptoTrader.Application.Observability;
using CryptoTrader.Application.Paper;
using CryptoTrader.Application.Risk;
using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.Instruments;
using CryptoTrader.Domain.MarketData;
using CryptoTrader.Domain.Risk;
using CryptoTrader.Domain.Strategies;
using CryptoTrader.Domain.Trading;
using CryptoTrader.Strategies.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;

namespace CryptoTrader.Application.Tests;

public sealed class PaperTradingCoordinatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Stale_candle_keeps_history_eligibility_but_does_not_change_portfolio_or_call_strategy()
    {
        var world = new World(Now);
        var bybit = world.AddSpot(ExchangeId.Bybit, "BYBIT-SPOT-BTCUSDT", "BTCUSDT", "BTC", "USDT");
        world.Run(bybit);
        var fresh = world.Candle(bybit, Now - TimeSpan.FromSeconds(30), 100m);
        var first = await world.Coordinator.OnClosedCandleAsync(fresh, CancellationToken.None);
        var afterFresh = world.State(ExchangeId.Bybit, "USDT");

        var stale = world.Candle(bybit, Now - TimeSpan.FromSeconds(181), 50m);
        var calls = world.Strategy.Calls;
        var second = await world.Coordinator.OnClosedCandleAsync(stale, CancellationToken.None);
        var afterStale = world.State(ExchangeId.Bybit, "USDT");

        first.WasApplied.ShouldBeTrue();
        second.WasApplied.ShouldBeFalse();
        second.Reason.ShouldNotBeNullOrWhiteSpace();
        world.Strategy.Calls.ShouldBe(calls);
        afterStale.Cash.ShouldBe(afterFresh.Cash);
        afterStale.LastEquity.ShouldBe(afterFresh.LastEquity);
        afterStale.PeakEquity.ShouldBe(afterFresh.PeakEquity);
        afterStale.DayStartEquity.ShouldBe(afterFresh.DayStartEquity);
        afterStale.MarksByInstrument[bybit.Id].ShouldBe(afterFresh.MarksByInstrument[bybit.Id]);
        afterStale.MarksByInstrument[bybit.Id].ShouldNotBe(50m);
        world.Orders.Count.ShouldBe(1);
        world.Signals.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Close_exactly_at_the_stale_boundary_still_updates_the_ledger()
    {
        var world = new World(Now, staleAfterSeconds: 180);
        var bybit = world.AddSpot(ExchangeId.Bybit, "BYBIT-SPOT-BTCUSDT", "BTCUSDT", "BTC", "USDT");
        var candle = world.Candle(bybit, Now - TimeSpan.FromSeconds(180), 100m);

        var result = await world.Coordinator.OnClosedCandleAsync(candle, CancellationToken.None);

        result.WasApplied.ShouldBeTrue();
        world.State(ExchangeId.Bybit, "USDT").MarksByInstrument[bybit.Id].ShouldBe(100m);
    }

    [Fact]
    public async Task Fresh_spot_candles_open_independent_bybit_and_okx_accounts()
    {
        var world = new World(Now);
        var bybit = world.AddSpot(ExchangeId.Bybit, "BYBIT-SPOT-BTCUSDT", "BTCUSDT", "BTC", "USDT");
        var okx = world.AddSpot(ExchangeId.Okx, "OKX-SPOT-BTC-USDT", "BTC-USDT", "BTC", "USDT");
        world.Run(bybit);
        world.Run(okx);

        var bybitResult = await world.Coordinator.OnClosedCandleAsync(world.Candle(bybit, Now - TimeSpan.FromSeconds(30), 100m), CancellationToken.None);
        var okxResult = await world.Coordinator.OnClosedCandleAsync(world.Candle(okx, Now - TimeSpan.FromSeconds(30), 200m), CancellationToken.None);

        bybitResult.WasApplied.ShouldBeTrue();
        okxResult.WasApplied.ShouldBeTrue();
        var bybitState = world.State(ExchangeId.Bybit, "USDT");
        var okxState = world.State(ExchangeId.Okx, "USDT");
        bybitState.AccountId.ShouldNotBe(okxState.AccountId);
        bybitState.AccountId.ShouldBe(PaperAccountScope.IdFor(ExchangeId.Bybit, "USDT"));
        okxState.AccountId.ShouldBe(PaperAccountScope.IdFor(ExchangeId.Okx, "USDT"));
        bybitState.Cash.ShouldBeLessThan(10_000m);
        okxState.Cash.ShouldBeLessThan(10_000m);
        bybitState.Positions.Single().InstrumentId.ShouldBe(bybit.Id);
        okxState.Positions.Single().InstrumentId.ShouldBe(okx.Id);
        world.Orders.Select(order => order.Exchange.Value).OrderBy(value => value).ShouldBe(["BYBIT", "OKX"]);
        world.Strategy.Instruments.ShouldBe([bybit.Id, okx.Id]);
    }

    [Fact]
    public async Task Quote_assets_and_emergency_stops_stay_on_their_own_ledgers()
    {
        var world = new World(Now);
        var usdt = world.AddSpot(ExchangeId.Bybit, "BYBIT-SPOT-BTCUSDT", "BTCUSDT", "BTC", "USDT");
        var usdc = world.AddSpot(ExchangeId.Bybit, "BYBIT-SPOT-BTCUSDC", "BTCUSDC", "BTC", "USDC");
        var okx = world.AddSpot(ExchangeId.Okx, "OKX-SPOT-BTC-USDT", "BTC-USDT", "BTC", "USDT");
        world.Run(usdt);

        await world.Coordinator.OnClosedCandleAsync(world.Candle(usdt, Now - TimeSpan.FromSeconds(30), 100m), CancellationToken.None);
        await world.Coordinator.OnClosedCandleAsync(world.Candle(usdc, Now - TimeSpan.FromSeconds(40), 100m), CancellationToken.None);
        await world.Coordinator.OnClosedCandleAsync(world.Candle(okx, Now - TimeSpan.FromSeconds(50), 100m), CancellationToken.None);
        var usdcBefore = world.State(ExchangeId.Bybit, "USDC").Cash;
        var okxBefore = world.State(ExchangeId.Okx, "USDT").Cash;

        await world.Coordinator.EngageEmergencyStopAsync("halt", "BYBIT", "USDT", CancellationToken.None);

        world.State(ExchangeId.Bybit, "USDT").EmergencyStop.ShouldBeTrue();
        world.State(ExchangeId.Bybit, "USDC").EmergencyStop.ShouldBeFalse();
        world.State(ExchangeId.Okx, "USDT").EmergencyStop.ShouldBeFalse();
        world.State(ExchangeId.Bybit, "USDC").Cash.ShouldBe(usdcBefore);
        world.State(ExchangeId.Okx, "USDT").Cash.ShouldBe(okxBefore);
        world.State(ExchangeId.Bybit, "USDT").AccountId.ShouldNotBe(world.State(ExchangeId.Bybit, "USDC").AccountId);
    }

    [Fact]
    public async Task Max_open_positions_applies_to_the_account_that_owns_the_candle()
    {
        var world = new World(Now, maxOpenPositions: 1);
        var btc = world.AddSpot(ExchangeId.Bybit, "BYBIT-SPOT-BTCUSDT", "BTCUSDT", "BTC", "USDT");
        var eth = world.AddSpot(ExchangeId.Bybit, "BYBIT-SPOT-ETHUSDT", "ETHUSDT", "ETH", "USDT");
        var okx = world.AddSpot(ExchangeId.Okx, "OKX-SPOT-BTC-USDT", "BTC-USDT", "BTC", "USDT");
        world.Run(btc);
        world.Run(eth);
        world.Run(okx);

        await world.Coordinator.OnClosedCandleAsync(world.Candle(btc, Now - TimeSpan.FromSeconds(20), 100m), CancellationToken.None);
        await world.Coordinator.OnClosedCandleAsync(world.Candle(eth, Now - TimeSpan.FromSeconds(30), 100m), CancellationToken.None);
        await world.Coordinator.OnClosedCandleAsync(world.Candle(okx, Now - TimeSpan.FromSeconds(40), 100m), CancellationToken.None);

        world.Orders.Count(order => order.Exchange == ExchangeId.Bybit).ShouldBe(1);
        world.Orders.Count(order => order.Exchange == ExchangeId.Okx).ShouldBe(1);
        world.State(ExchangeId.Bybit, "USDT").Positions.Count.ShouldBe(1);
        world.State(ExchangeId.Okx, "USDT").Positions.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Replaying_a_fresh_candle_does_not_create_a_second_fill()
    {
        var world = new World(Now);
        var bybit = world.AddSpot(ExchangeId.Bybit, "BYBIT-SPOT-BTCUSDT", "BTCUSDT", "BTC", "USDT");
        world.Run(bybit);
        var candle = world.Candle(bybit, Now - TimeSpan.FromSeconds(30), 100m);

        await world.Coordinator.OnClosedCandleAsync(candle, CancellationToken.None);
        var cash = world.State(ExchangeId.Bybit, "USDT").Cash;
        var restarted = world.Restart();
        var replay = await restarted.Coordinator.OnClosedCandleAsync(candle, CancellationToken.None);

        replay.WasApplied.ShouldBeTrue();
        restarted.Strategy.Calls.ShouldBe(0);
        restarted.Orders.Count.ShouldBe(1);
        restarted.State(ExchangeId.Bybit, "USDT").Cash.ShouldBe(cash);
        restarted.State(ExchangeId.Bybit, "USDT").AppliedCandleKeys.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Futures_and_contract_volume_do_not_open_a_paper_account()
    {
        var world = new World(Now);
        var linear = world.AddLinear(ExchangeId.Okx, "OKX-LINEAR-SWAP-BTC-USDT", "BTC-USDT-SWAP");
        var spot = world.AddSpot(ExchangeId.Bybit, "BYBIT-SPOT-BTCUSDT", "BTCUSDT", "BTC", "USDT");

        var futures = await world.Coordinator.OnClosedCandleAsync(world.Candle(linear, Now - TimeSpan.FromMinutes(5), 100m, VolumeUnit.Contract), CancellationToken.None);
        var contractSpot = await world.Coordinator.OnClosedCandleAsync(world.Candle(spot, Now - TimeSpan.FromMinutes(5), 100m, VolumeUnit.Contract), CancellationToken.None);

        futures.WasApplied.ShouldBeFalse();
        contractSpot.WasApplied.ShouldBeFalse();
        world.Accounts.ShouldBeEmpty();
        world.Strategy.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task Existing_quote_account_is_reused_instead_of_creating_another()
    {
        var world = new World(Now);
        var bybit = world.AddSpot(ExchangeId.Bybit, "BYBIT-SPOT-BTCUSDT", "BTCUSDT", "BTC", "USDT");
        var legacyId = TradingAccountId.New();
        world.SeedAccount(legacyId, ExchangeId.Bybit, "USDT", 10_000m);

        var result = await world.Coordinator.OnClosedCandleAsync(world.Candle(bybit, Now - TimeSpan.FromSeconds(30), 100m), CancellationToken.None);

        result.WasApplied.ShouldBeTrue();
        world.Accounts.Count.ShouldBe(1);
        world.State(ExchangeId.Bybit, "USDT").AccountId.ShouldBe(legacyId);
        world.State(ExchangeId.Bybit, "USDT").MarksByInstrument[bybit.Id].ShouldBe(100m);
    }

    [Fact]
    public void Backlog_longer_than_the_window_is_not_paper_eligible()
    {
        var decision = CandleProcessingDecision.Evaluate(
            true,
            Now,
            Now - TimeSpan.FromSeconds(181),
            Now,
            TimeSpan.FromSeconds(180));

        decision.CanPersistHistory.ShouldBeTrue();
        decision.CanUpdatePaper.ShouldBeFalse();
        decision.Admission.ShouldBe(CandleAdmission.ConsumerBehind);
    }

    [Fact]
    public async Task Global_emergency_stop_blocks_accounts_created_afterwards_until_release()
    {
        var world = new World(Now);
        var bybitUsdt = world.AddSpot(ExchangeId.Bybit, "BYBIT-SPOT-BTCUSDT", "BTCUSDT", "BTC", "USDT");
        var bybitUsdc = world.AddSpot(ExchangeId.Bybit, "BYBIT-SPOT-BTCUSDC", "BTCUSDC", "BTC", "USDC");
        var okx = world.AddSpot(ExchangeId.Okx, "OKX-SPOT-BTC-USDT", "BTC-USDT", "BTC", "USDT");
        world.Run(bybitUsdt);
        world.Run(bybitUsdc);
        world.Run(okx);

        var engaged = await world.Coordinator.EngageEmergencyStopAsync("halt", null, null, CancellationToken.None);

        engaged.EmergencyStop.ShouldBeTrue();
        world.Accounts.ShouldBeEmpty();
        await world.Processor.PersistAndTradeAsync(world.Candle(bybitUsdt, Now - TimeSpan.FromSeconds(20), 100m), Eligible(), CancellationToken.None);
        await world.Processor.PersistAndTradeAsync(world.Candle(bybitUsdc, Now - TimeSpan.FromSeconds(30), 100m), Eligible(), CancellationToken.None);
        await world.Processor.PersistAndTradeAsync(world.Candle(okx, Now - TimeSpan.FromSeconds(40), 100m), Eligible(), CancellationToken.None);

        world.Orders.ShouldBeEmpty();
        world.Strategy.Calls.ShouldBe(0);
        world.State(ExchangeId.Bybit, "USDT").EmergencyStop.ShouldBeTrue();
        world.State(ExchangeId.Bybit, "USDC").EmergencyStop.ShouldBeTrue();
        world.State(ExchangeId.Okx, "USDT").EmergencyStop.ShouldBeTrue();
        world.Checkpoint(bybitUsdt, Now - TimeSpan.FromSeconds(20)).ShouldBe(CandleConsumerState.Pending);

        var released = await world.Coordinator.ReleaseEmergencyStopAsync(null, null, CancellationToken.None);
        var replay = await world.Processor.ReplayPendingAsync(CancellationToken.None);

        released.EmergencyStop.ShouldBeFalse();
        replay.LeftPending.ShouldBeFalse();
        world.State(ExchangeId.Bybit, "USDT").EmergencyStop.ShouldBeFalse();
        world.State(ExchangeId.Bybit, "USDC").EmergencyStop.ShouldBeFalse();
        world.State(ExchangeId.Okx, "USDT").EmergencyStop.ShouldBeFalse();
        world.Orders.Count.ShouldBe(3);
        world.Strategy.Calls.ShouldBe(3);
        world.Checkpoint(bybitUsdt, Now - TimeSpan.FromSeconds(20)).ShouldBe(CandleConsumerState.Processed);
    }

    [Fact]
    public async Task Stale_ingestion_stores_the_candle_and_skips_the_checkpoint()
    {
        var world = new World(Now);
        var bybit = world.AddSpot(ExchangeId.Bybit, "BYBIT-SPOT-BTCUSDT", "BTCUSDT", "BTC", "USDT");
        world.Run(bybit);
        var close = Now - TimeSpan.FromSeconds(181);
        var decision = CandleProcessingDecision.Evaluate(true, close, Now, Now, TimeSpan.FromSeconds(180));

        await world.Processor.PersistAndTradeAsync(world.Candle(bybit, close, 100m), decision, CancellationToken.None);

        world.PersistedCandles.Count.ShouldBe(1);
        world.Checkpoint(bybit, close).ShouldBe(CandleConsumerState.Skipped);
        world.Orders.ShouldBeEmpty();
        world.Strategy.Calls.ShouldBe(0);
        world.Accounts.ShouldBeEmpty();
    }

    [Fact]
    public async Task Fresh_pending_candle_is_applied_when_replay_runs_again()
    {
        var world = new World(Now);
        var bybit = world.AddSpot(ExchangeId.Bybit, "BYBIT-SPOT-BTCUSDT", "BTCUSDT", "BTC", "USDT");
        world.Run(bybit);
        var candle = world.Candle(bybit, Now - TimeSpan.FromSeconds(20), 100m);
        await world.Processor.PersistAndTradeAsync(candle, Eligible(), CancellationToken.None);

        world.Checkpoint(bybit, candle.CloseTimeUtc).ShouldBe(CandleConsumerState.Processed);
        world.Orders.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Stale_pending_candle_is_skipped_on_replay()
    {
        var world = new World(Now);
        var bybit = world.AddSpot(ExchangeId.Bybit, "BYBIT-SPOT-BTCUSDT", "BTCUSDT", "BTC", "USDT");
        var close = Now - TimeSpan.FromSeconds(181);
        var candle = world.Candle(bybit, close, 100m);
        await world.StorePending(candle);

        var replay = await world.Processor.ReplayPendingAsync(CancellationToken.None);

        replay.LeftPending.ShouldBeFalse();
        world.Checkpoint(bybit, close).ShouldBe(CandleConsumerState.Skipped);
        world.Orders.ShouldBeEmpty();
    }

    [Fact]
    public async Task Temporary_processing_failure_is_retried_without_restarting()
    {
        var world = new World(Now);
        var bybit = world.AddSpot(ExchangeId.Bybit, "BYBIT-SPOT-BTCUSDT", "BTCUSDT", "BTC", "USDT");
        world.Run(bybit);
        world.Strategy.FailNext = true;
        var candle = world.Candle(bybit, Now - TimeSpan.FromSeconds(20), 100m);

        await Should.ThrowAsync<InvalidOperationException>(() =>
            world.Processor.PersistAndTradeAsync(candle, Eligible(), CancellationToken.None));
        world.Checkpoint(bybit, candle.CloseTimeUtc).ShouldBe(CandleConsumerState.Pending);
        world.Orders.ShouldBeEmpty();

        var replay = await world.Processor.ReplayPendingAsync(CancellationToken.None);

        replay.LeftPending.ShouldBeFalse();
        world.Checkpoint(bybit, candle.CloseTimeUtc).ShouldBe(CandleConsumerState.Processed);
        world.Orders.Count.ShouldBe(1);
        world.Strategy.Calls.ShouldBe(2);
    }

    [Fact]
    public async Task Intake_failure_before_commit_leaves_no_checkpoint()
    {
        var world = new World(Now);
        var bybit = world.AddSpot(ExchangeId.Bybit, "BYBIT-SPOT-BTCUSDT", "BTCUSDT", "BTC", "USDT");
        world.FailNextIntake = true;

        await Should.ThrowAsync<InvalidOperationException>(() =>
            world.Processor.PersistAndTradeAsync(world.Candle(bybit, Now - TimeSpan.FromSeconds(20), 100m), Eligible(), CancellationToken.None));

        world.PersistedCandles.ShouldBeEmpty();
        world.PendingCount.ShouldBe(0);
    }

    private static CandleProcessingDecision Eligible() =>
        new(true, true, CandleAdmission.Eligible, null);

    private sealed class World
    {
        private readonly Store _store = new();
        private readonly FrozenTime _time;
        private readonly CountingStrategy _strategy = new();
        private readonly int _staleAfterSeconds;
        private readonly int _maxOpenPositions;

        public World(DateTimeOffset now, int staleAfterSeconds = 180, int maxOpenPositions = 2)
        {
            _time = new FrozenTime(now);
            _staleAfterSeconds = staleAfterSeconds;
            _maxOpenPositions = maxOpenPositions;
            Coordinator = Build(_strategy);
            Processor = BuildProcessor();
        }

        public PaperTradingCoordinator Coordinator { get; private set; }

        public CandleIngestionProcessor Processor { get; private set; }

        public CountingStrategy Strategy => _strategy;

        public IReadOnlyList<Order> Orders => _store.Orders.Values.ToArray();

        public IReadOnlyCollection<Guid> Signals => _store.SignalIds;

        public IReadOnlyList<TradingAccount> Accounts => _store.Accounts.Values.ToArray();

        public Instrument AddSpot(ExchangeId exchange, string id, string symbol, string baseAsset, string quote)
        {
            var instrument = Instrument.Create(
                new InstrumentId(id),
                exchange,
                symbol,
                baseAsset,
                quote,
                InstrumentType.Spot,
                0.01m,
                0.001m,
                0.001m,
                5m,
                null,
                true);
            _store.Instruments[instrument.Id] = instrument;
            return instrument;
        }

        public Instrument AddLinear(ExchangeId exchange, string id, string symbol)
        {
            var instrument = Instrument.Create(
                new InstrumentId(id),
                exchange,
                symbol,
                "BTC",
                "USDT",
                InstrumentType.LinearPerpetual,
                0.01m,
                0.001m,
                0.001m,
                5m,
                1m,
                true);
            _store.Instruments[instrument.Id] = instrument;
            return instrument;
        }

        public void Run(Instrument instrument)
        {
            var instance = StrategyInstance.Create(StrategyInstanceId.New(), "test", "1", instrument.Id, "{}", _time.Utc);
            instance.Start(_time.Utc);
            _store.Instances.Add(instance);
        }

        public Candle Candle(Instrument instrument, DateTimeOffset close, decimal price, VolumeUnit unit = VolumeUnit.BaseAsset) =>
            global::CryptoTrader.Domain.MarketData.Candle.Create(
                instrument.Exchange,
                instrument.Id,
                Timeframe.OneHour,
                close.AddHours(-1),
                close,
                price,
                price,
                price,
                price,
                1m,
                true,
                unit);

        public PaperState State(ExchangeId exchange, string quote) =>
            _store.States.Values.Single(state =>
                PaperAccountScope.SameExchange(state.Exchange, exchange) && PaperAccountScope.SameQuote(state.QuoteAsset, quote));

        public CandleConsumerState Checkpoint(Instrument instrument, DateTimeOffset close) =>
            _store.CheckpointState(instrument, close);

        public IReadOnlyList<Candle> PersistedCandles => _store.Persisted;

        public int PendingCount => _store.PendingCount;

        public bool FailNextIntake
        {
            get => _store.FailNextIntake;
            set => _store.FailNextIntake = value;
        }

        public Task StorePending(Candle candle) => _store.PersistPendingAsync(candle, ICandleConsumerCheckpoint.PaperConsumer, _time.Utc, CancellationToken.None);

        public void SeedAccount(TradingAccountId id, ExchangeId exchange, string quote, decimal cash)
        {
            var account = TradingAccount.Create(id, exchange, "Paper", TradingMode.Paper, _time.Utc);
            _store.Accounts[id] = account;
            _store.States[id] = new PaperState(
                id,
                exchange,
                quote,
                cash,
                cash,
                cash,
                cash,
                DateOnly.FromDateTime(_time.Utc.UtcDateTime),
                false,
                [],
                [],
                new Dictionary<InstrumentId, decimal>(),
                _time.Utc,
                []);
        }

        public World Restart()
        {
            var restarted = new World(_time.Utc, _staleAfterSeconds, _maxOpenPositions);
            restarted._store.CopyFrom(_store);
            restarted.Coordinator = restarted.Build(restarted._strategy);
            restarted.Processor = restarted.BuildProcessor();
            return restarted;
        }

        private CandleIngestionProcessor BuildProcessor() => new(
            _store,
            _store,
            Coordinator,
            _time,
            Options.Create(new MarketDataOptions { StaleAfterSeconds = _staleAfterSeconds, DefaultTimeframes = ["1h"] }),
            NullLogger<CandleIngestionProcessor>.Instance);

        private PaperTradingCoordinator Build(CountingStrategy strategy)
        {
            var execution = Options.Create(new ExecutionOptions { Mode = TradingMode.Paper, LiveTradingEnabled = false, PaperInitialEquity = 10_000m });
            var risk = Options.Create(new RiskOptions
            {
                FeeRate = 0m,
                SlippageBps = 0m,
                MaxOpenPositions = _maxOpenPositions,
                MaxExposurePercent = 100m,
                RiskPerTradePercent = 0.5m,
            });
            return new PaperTradingCoordinator(
                _store,
                _store,
                _store,
                _store,
                _store,
                _store,
                _store,
                _store,
                _store,
                _store,
                _store,
                new RiskManager(execution, _time),
                new FillVenue(_time),
                new Catalog(strategy),
                execution,
                Options.Create(new MarketDataOptions { StaleAfterSeconds = _staleAfterSeconds, DefaultTimeframes = ["1h"] }),
                risk,
                _time,
                new PaperTradingGate(),
                new TradingMetrics(),
                NullLogger<PaperTradingCoordinator>.Instance);
        }
    }

    private sealed class CountingStrategy : ITradingStrategy
    {
        public int Calls { get; private set; }

        public bool FailNext { get; set; }

        public List<InstrumentId> Instruments { get; } = [];

        public string Id => "test";

        public string Version => "1";

        public string DisplayName => "test";

        public ValueTask<IReadOnlyList<TradeSignal>> EvaluateAsync(StrategyContext context, CancellationToken cancellationToken)
        {
            Calls++;
            if (FailNext)
            {
                FailNext = false;
                throw new InvalidOperationException("temporary strategy failure");
            }

            Instruments.Add(context.Instrument.Id);
            var close = context.ClosedWorkingCandles[^1].Close;
            var signalId = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(context.Instrument.Id.Value)).AsSpan(0, 16));
            var signal = TradeSignal.Create(
                signalId,
                Id,
                Version,
                context.Instrument.Id,
                SignalAction.EnterLong,
                context.AsOfUtc,
                new Price(close),
                new Price(close * 0.9m),
                null,
                null,
                "enter");
            return ValueTask.FromResult<IReadOnlyList<TradeSignal>>([signal]);
        }
    }

    private sealed class Catalog(CountingStrategy strategy) : IStrategyCatalog
    {
        public IReadOnlyList<StrategyDescriptor> List() => [];

        public ITradingStrategy Create(string strategyId, string parametersJson) => strategy;
    }

    private sealed class FillVenue(TimeProvider time) : IExecutionVenue
    {
        public TradingMode Mode => TradingMode.Paper;

        public Task<VenueExecutionResult> ExecuteAsync(
            ApprovedOrder order,
            Instrument instrument,
            MarketSnapshot market,
            CancellationToken cancellationToken)
        {
            var now = time.GetUtcNow();
            var price = market.Last ?? order.Signal.ReferencePrice.Value;
            var paperOrder = Order.Create(
                OrderId.New(),
                order.ClientOrderId,
                order.AccountId,
                instrument.Exchange,
                instrument,
                order.Signal.Action == SignalAction.EnterLong ? OrderSide.Buy : OrderSide.Sell,
                order.OrderType,
                order.Quantity,
                null,
                false,
                TradingMode.Paper,
                now);
            paperOrder.MarkSubmitted(now);
            paperOrder.MarkAccepted(now, "paper-" + order.ClientOrderId);
            var execution = paperOrder.ApplyFill(
                ExecutionId.New(),
                order.Quantity,
                new Price(price),
                0m,
                instrument.QuoteAsset,
                now,
                "paper-fill-" + order.ClientOrderId);
            return Task.FromResult(new VenueExecutionResult(paperOrder, execution, null));
        }

        public Task ReconcileAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FrozenTime(DateTimeOffset utc) : TimeProvider
    {
        public DateTimeOffset Utc { get; } = utc;

        public override DateTimeOffset GetUtcNow() => Utc;
    }

    private sealed class Store :
        ITradingAccountRepository,
        IPaperStateRepository,
        IPaperTradingControl,
        IInstrumentRepository,
        ICandleRepository,
        ICandleIntake,
        ICandleConsumerCheckpoint,
        IStrategyInstanceRepository,
        ITradeSignalRepository,
        IOrderRepository,
        IPositionRepository,
        IRiskEventRepository,
        IUnitOfWork
    {
        public Dictionary<TradingAccountId, TradingAccount> Accounts { get; } = [];

        public Dictionary<TradingAccountId, PaperState> States { get; } = [];

        public Dictionary<InstrumentId, Instrument> Instruments { get; } = [];

        public List<StrategyInstance> Instances { get; } = [];

        public Dictionary<Guid, Order> Orders { get; } = [];

        public HashSet<Guid> SignalIds { get; } = [];

        public List<Candle> Persisted { get; } = [];

        public bool FailNextIntake { get; set; }

        public bool GlobalEmergencyStop { get; private set; }

        public string? GlobalReason { get; private set; }

        public DateTimeOffset? GlobalUpdatedAt { get; private set; }

        private readonly Dictionary<string, CheckpointRow> _checkpoints = [];

        public int PendingCount => _checkpoints.Values.Count(row => row.State == CandleConsumerState.Pending);

        public CandleConsumerState CheckpointState(Instrument instrument, DateTimeOffset close) =>
            _checkpoints.Values.Single(row => row.Candle.Instrument == instrument.Id && row.Candle.CloseTimeUtc == close).State;

        public Task<TradingAccount?> FindAsync(TradingAccountId id, CancellationToken cancellationToken) =>
            Task.FromResult(Accounts.TryGetValue(id, out var account) ? account : null);

        public Task UpsertAsync(TradingAccount account, CancellationToken cancellationToken)
        {
            Accounts[account.Id] = account;
            return Task.CompletedTask;
        }

        Task<IReadOnlyList<TradingAccount>> ITradingAccountRepository.ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TradingAccount>>(Accounts.Values.ToArray());

        public Task SaveAsync(PaperState state, CancellationToken cancellationToken)
        {
            States[state.AccountId] = state;
            return Task.CompletedTask;
        }

        public Task<PaperState?> LoadAsync(TradingAccountId accountId, CancellationToken cancellationToken) =>
            Task.FromResult(States.TryGetValue(accountId, out var state) ? state : null);

        public Task UpsertAsync(Instrument instrument, CancellationToken cancellationToken)
        {
            Instruments[instrument.Id] = instrument;
            return Task.CompletedTask;
        }

        public Task<Instrument?> FindAsync(InstrumentId id, CancellationToken cancellationToken) =>
            Task.FromResult(Instruments.TryGetValue(id, out var instrument) ? instrument : null);

        public Task<Instrument?> FindByExchangeSymbolAsync(ExchangeId exchange, string exchangeSymbol, CancellationToken cancellationToken) =>
            Task.FromResult(Instruments.Values.FirstOrDefault(item => item.Exchange == exchange && item.ExchangeSymbol == exchangeSymbol));

        public Task<IReadOnlyList<Instrument>> ListAsync(ExchangeId? exchange, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Instrument>>(Instruments.Values.Where(item => exchange is null || item.Exchange == exchange.Value).ToArray());

        public Task UpsertRangeAsync(IReadOnlyList<Candle> candles, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<Candle>> GetRangeAsync(
            ExchangeId exchange,
            InstrumentId instrument,
            Timeframe timeframe,
            DateTimeOffset fromUtc,
            DateTimeOffset toUtc,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Candle>>([]);

        public Task AddAsync(StrategyInstance instance, CancellationToken cancellationToken)
        {
            Instances.Add(instance);
            return Task.CompletedTask;
        }

        public Task<StrategyInstance?> FindAsync(StrategyInstanceId id, CancellationToken cancellationToken) =>
            Task.FromResult(Instances.FirstOrDefault(instance => instance.Id == id));

        Task<IReadOnlyList<StrategyInstance>> IStrategyInstanceRepository.ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StrategyInstance>>(Instances.ToArray());

        public Task UpdateAsync(StrategyInstance instance, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task AddAsync(TradeSignal signal, StrategyInstanceId strategyInstanceId, RiskDecision decision, CancellationToken cancellationToken)
        {
            SignalIds.Add(signal.SignalId);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(Guid signalId, CancellationToken cancellationToken) =>
            Task.FromResult(SignalIds.Contains(signalId));

        public Task<Order?> FindByIdAsync(OrderId id, CancellationToken cancellationToken) =>
            Task.FromResult(Orders.TryGetValue(id.Value, out var order) ? order : null);

        public Task<Order?> FindByClientOrderIdAsync(TradingAccountId accountId, string clientOrderId, CancellationToken cancellationToken) =>
            Task.FromResult(Orders.Values.FirstOrDefault(order => order.AccountId == accountId && order.ClientOrderId == clientOrderId));

        public Task AddAsync(Order order, CancellationToken cancellationToken)
        {
            Orders[order.Id.Value] = order;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Order order, CancellationToken cancellationToken)
        {
            Orders[order.Id.Value] = order;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Order>> ListAsync(TradingAccountId? accountId, TradingMode? tradingMode, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Order>>(Orders.Values.Where(order =>
                (accountId is null || order.AccountId == accountId) &&
                (tradingMode is null || order.TradingMode == tradingMode)).ToArray());

        public Task<Position?> FindOpenAsync(TradingAccountId accountId, InstrumentId instrumentId, CancellationToken cancellationToken) =>
            Task.FromResult<Position?>(null);

        public Task<IReadOnlyList<Position>> ListAsync(TradingAccountId accountId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Position>>([]);

        public Task UpsertAsync(Position position, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task AddAsync(DomainEvent riskEvent, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<PaperTradingControlState> GetAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new PaperTradingControlState(GlobalEmergencyStop, GlobalReason, GlobalUpdatedAt));

        public Task EngageAsync(string reason, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken)
        {
            GlobalEmergencyStop = true;
            GlobalReason = reason;
            GlobalUpdatedAt = updatedAtUtc;
            return Task.CompletedTask;
        }

        public Task ReleaseAsync(DateTimeOffset updatedAtUtc, CancellationToken cancellationToken)
        {
            GlobalEmergencyStop = false;
            GlobalReason = null;
            GlobalUpdatedAt = updatedAtUtc;
            return Task.CompletedTask;
        }

        public Task<bool> PersistPendingAsync(Candle candle, string consumer, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken)
        {
            if (FailNextIntake)
            {
                FailNextIntake = false;
                throw new InvalidOperationException("intake stopped before commit");
            }

            Persisted.Add(candle);
            return MarkPendingAsync(candle, consumer, updatedAtUtc, cancellationToken);
        }

        public Task<bool> MarkPendingAsync(Candle candle, string consumer, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken)
        {
            var key = CheckpointKey(candle, consumer);
            if (_checkpoints.TryGetValue(key, out var existing)
                && existing.State is CandleConsumerState.Processed or CandleConsumerState.Skipped)
            {
                return Task.FromResult(false);
            }

            _checkpoints[key] = new CheckpointRow(candle, consumer, CandleConsumerState.Pending, null);
            return Task.FromResult(true);
        }

        public Task MarkProcessedAsync(Candle candle, string consumer, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken)
        {
            _checkpoints[CheckpointKey(candle, consumer)] = new CheckpointRow(candle, consumer, CandleConsumerState.Processed, null);
            return Task.CompletedTask;
        }

        public Task MarkSkippedAsync(Candle candle, string consumer, string reason, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken)
        {
            _checkpoints[CheckpointKey(candle, consumer)] = new CheckpointRow(candle, consumer, CandleConsumerState.Skipped, reason);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Candle>> ListPendingCandlesAsync(string consumer, int limit, CancellationToken cancellationToken)
        {
            var candles = _checkpoints.Values
                .Where(row => row.Consumer == consumer && row.State == CandleConsumerState.Pending)
                .OrderBy(row => row.Candle.OpenTimeUtc)
                .Take(limit)
                .Select(row => row.Candle)
                .ToArray();
            return Task.FromResult<IReadOnlyList<Candle>>(candles);
        }

        private static string CheckpointKey(Candle candle, string consumer) =>
            string.Join('|', candle.Exchange.Value, candle.Instrument.Value, candle.Timeframe.Code, candle.OpenTimeUtc.UtcTicks, consumer);

        public void CopyFrom(Store other)
        {
            foreach (var (id, account) in other.Accounts)
            {
                Accounts[id] = account;
            }

            foreach (var (id, state) in other.States)
            {
                States[id] = state;
            }

            foreach (var (id, instrument) in other.Instruments)
            {
                Instruments[id] = instrument;
            }

            Instances.AddRange(other.Instances);
            foreach (var (id, order) in other.Orders)
            {
                Orders[id] = order;
            }

            foreach (var signal in other.SignalIds)
            {
                SignalIds.Add(signal);
            }

            GlobalEmergencyStop = other.GlobalEmergencyStop;
            GlobalReason = other.GlobalReason;
            GlobalUpdatedAt = other.GlobalUpdatedAt;
            Persisted.AddRange(other.Persisted);
            foreach (var (key, row) in other._checkpoints)
            {
                _checkpoints[key] = row;
            }
        }

        private sealed record CheckpointRow(Candle Candle, string Consumer, CandleConsumerState State, string? Reason);
    }
}
