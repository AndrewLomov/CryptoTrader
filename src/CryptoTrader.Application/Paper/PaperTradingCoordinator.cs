using CryptoTrader.Application.Abstractions.Execution;
using CryptoTrader.Application.Abstractions.Paper;
using CryptoTrader.Application.Abstractions.Persistence;
using CryptoTrader.Application.Abstractions.Risk;
using CryptoTrader.Application.Common;
using CryptoTrader.Application.Configuration;
using CryptoTrader.Application.MarketData;
using CryptoTrader.Application.Observability;
using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.Instruments;
using CryptoTrader.Domain.MarketData;
using CryptoTrader.Domain.Risk;
using CryptoTrader.Domain.Strategies;
using CryptoTrader.Domain.Trading;
using CryptoTrader.Strategies.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CryptoTrader.Application.Paper;

/// <summary>
/// Runs one paper account through the shared risk manager, execution venue, and portfolio ledger.
/// Live submission is never used: <c>ITradingClient.SubmitAsync</c> is not called.
/// </summary>
public sealed class PaperTradingCoordinator(
    ITradingAccountRepository accounts,
    IPaperStateRepository paperState,
    IInstrumentRepository instruments,
    ICandleRepository candles,
    IStrategyInstanceRepository strategyInstances,
    ITradeSignalRepository signals,
    IOrderRepository orders,
    IPositionRepository positions,
    IRiskEventRepository riskEvents,
    IUnitOfWork unitOfWork,
    IRiskManager riskManager,
    IExecutionVenue executionVenue,
    IStrategyCatalog strategyCatalog,
    IOptions<ExecutionOptions> executionOptions,
    IOptions<MarketDataOptions> marketDataOptions,
    IOptions<RiskOptions> riskOptions,
    TimeProvider timeProvider,
    PaperTradingGate gate,
    TradingMetrics metrics,
    ILogger<PaperTradingCoordinator> logger)
{
    private const string DefaultQuoteAsset = "USDT";

    public Task OnClosedCandleAsync(Candle candle, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candle);
        return WithGateAsync(cancellationToken, token => HandleCandleAsync(candle, token));
    }

    public Task<EmergencyStopOutcome> EngageEmergencyStopAsync(string? reason, CancellationToken cancellationToken) =>
        WithGateAsync<EmergencyStopOutcome>(cancellationToken, async token =>
        {
            var now = UtcTime.Normalize(timeProvider.GetUtcNow());
            var account = await GetOrCreateAccountAsync(token);
            var ledger = await LoadLedgerAsync(account, DefaultQuoteAsset, token);
            var wasEngaged = ledger.EmergencyStop;
            var normalizedReason = string.IsNullOrWhiteSpace(reason) ? "Emergency stop." : reason.Trim();
            ledger.EngageEmergencyStop(now, normalizedReason);
            if (!wasEngaged)
            {
                await SaveLedgerAsync(account, ledger, token);
                await unitOfWork.SaveChangesAsync(token);
            }

            metrics.RecordEquity(ledger.LastEquity);
            return new EmergencyStopOutcome(ledger.EmergencyStop, ledger.UpdatedAtUtc);
        });

    public Task<PaperTradingSnapshot> GetSnapshotAsync(
        Guid? accountId,
        TradingMode? tradingMode,
        CancellationToken cancellationToken) =>
        WithGateAsync<PaperTradingSnapshot>(cancellationToken, async token =>
        {
            var account = await FindAccountAsync(accountId, token);
            if (account is null)
            {
                return PaperTradingSnapshot.Empty;
            }

            var listedOrders = await orders.ListAsync(account.Id, tradingMode, token);
            var listedPositions = await positions.ListAsync(account.Id, token);
            return new PaperTradingSnapshot(account, listedOrders, listedPositions);
        });

    private async Task HandleCandleAsync(Candle candle, CancellationToken cancellationToken)
    {
        if (!candle.IsClosed)
        {
            return;
        }

        var instrument = await instruments.FindAsync(candle.Instrument, cancellationToken);
        if (instrument is null)
        {
            logger.LogWarning("Closed candle at {CloseTime} has no stored instrument {InstrumentId}.", candle.CloseTimeUtc, candle.Instrument.Value);
            return;
        }

        if (instrument.Exchange != ExchangeId.Bybit)
        {
            logger.LogWarning("Candle exchange {Exchange} is outside the paper BYBIT account.", instrument.Exchange.Value);
            return;
        }

        var asOf = UtcTime.Normalize(candle.CloseTimeUtc);
        var account = await GetOrCreateAccountAsync(cancellationToken);

        var ledger = await LoadLedgerAsync(account, instrument.QuoteAsset, cancellationToken);
        ledger.ObserveMark(instrument.Id, new Price(candle.Close), asOf);
        var running = (await strategyInstances.ListAsync(cancellationToken))
            .Where(instance => instance.Status == StrategyInstanceStatus.Running && instance.InstrumentId == instrument.Id)
            .ToArray();
        if (running.Length == 0)
        {
            await SaveLedgerAsync(account, ledger, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            metrics.RecordEquity(ledger.LastEquity);
            return;
        }

        var marketData = marketDataOptions.Value ?? throw new InvalidOperationException("Market data options are not configured.");
        var profile = (riskOptions.Value ?? throw new InvalidOperationException("Risk options are not configured.")).ToProfile();
        var market = BuildMarket(candle, asOf, marketData.StaleAfterSeconds);
        var historyFrom = DateTimeOffset.UnixEpoch;
        var storedWorking = await candles.GetRangeAsync(
            instrument.Exchange,
            instrument.Id,
            candle.Timeframe,
            historyFrom,
            asOf,
            cancellationToken);
        var working = ClosedUntil(storedWorking, candle, candle.Timeframe, asOf);
        var higherTimeframe = TimeframeSelection.HigherThan(marketData, candle.Timeframe);
        IReadOnlyList<Candle> higher = [];
        if (higherTimeframe is Timeframe higherFrame)
        {
            var storedHigher = await candles.GetRangeAsync(
                instrument.Exchange,
                instrument.Id,
                higherFrame,
                historyFrom,
                asOf,
                cancellationToken);
            higher = ClosedUntil(storedHigher, candle, higherFrame, asOf);
        }

        var approvedIds = new HashSet<Guid>();
        var seen = new HashSet<Guid>();
        foreach (var instance in running)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var strategy = strategyCatalog.Create(instance.StrategyId, instance.ParametersJson);
            var context = new StrategyContext(
                instrument,
                asOf,
                instance.ParametersJson,
                working,
                higher,
                HasOpenLong(ledger, instrument.Id));
            var produced = await strategy.EvaluateAsync(context, cancellationToken);
            foreach (var signal in produced)
            {
                if (signal.InstrumentId != instrument.Id)
                {
                    continue;
                }

                await ProcessSignalAsync(
                    account,
                    ledger,
                    instrument,
                    instance,
                    signal,
                    market,
                    profile,
                    approvedIds,
                    seen,
                    cancellationToken);
            }
        }

        await SaveLedgerAsync(account, ledger, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        metrics.RecordEquity(ledger.LastEquity);
    }

    private async Task ProcessSignalAsync(
        TradingAccount account,
        PortfolioLedger ledger,
        Instrument instrument,
        StrategyInstance instance,
        TradeSignal signal,
        MarketSnapshot market,
        RiskProfile profile,
        HashSet<Guid> approvedIds,
        HashSet<Guid> seen,
        CancellationToken cancellationToken)
    {
        var alreadyStored = seen.Contains(signal.SignalId) || await signals.ExistsAsync(signal.SignalId, cancellationToken);
        if (alreadyStored)
        {
            approvedIds.Add(signal.SignalId);
        }

        var decision = await riskManager.EvaluateAsync(
            signal,
            BuildSnapshot(ledger, instrument, market.Last ?? signal.ReferencePrice.Value, approvedIds),
            market,
            instrument,
            profile,
            cancellationToken);
        metrics.RecordDecision(decision.Approved);
        if (!alreadyStored)
        {
            await signals.AddAsync(signal, instance.Id, decision, cancellationToken);
            seen.Add(signal.SignalId);
        }

        if (!decision.Approved)
        {
            logger.LogInformation(
                "Signal {SignalId} {Action} was rejected with {RejectionCode}.",
                signal.SignalId,
                signal.Action,
                decision.RejectionCode);
            return;
        }

        if (decision.ApprovedQuantity is not Quantity quantity)
        {
            logger.LogWarning("Approved signal {SignalId} did not include a quantity.", signal.SignalId);
            return;
        }

        approvedIds.Add(signal.SignalId);
        if (instrument.Type != InstrumentType.Spot
            || !string.Equals(instrument.QuoteAsset, ledger.QuoteAsset, StringComparison.Ordinal))
        {
            logger.LogWarning(
                "Signal {SignalId} was approved, but the paper ledger only applies spot fills quoted in {QuoteAsset}.",
                signal.SignalId,
                ledger.QuoteAsset);
            return;
        }

        await ExecuteApprovedAsync(account, ledger, instrument, signal, quantity, market, cancellationToken);
        logger.LogInformation("Signal {SignalId} {Action} was approved.", signal.SignalId, signal.Action);
    }

    private async Task ExecuteApprovedAsync(
        TradingAccount account,
        PortfolioLedger ledger,
        Instrument instrument,
        TradeSignal signal,
        Quantity quantity,
        MarketSnapshot market,
        CancellationToken cancellationToken)
    {
        var now = UtcTime.Normalize(timeProvider.GetUtcNow());
        var approvalId = Guid.NewGuid();
        var approved = new ApprovedOrder(
            approvalId,
            signal,
            account.Id,
            quantity,
            OrderType.Market,
            limitPrice: null,
            "p" + Guid.NewGuid().ToString("N"),
            now);
        var result = await executionVenue.ExecuteAsync(approved, instrument, market, cancellationToken);
        if (result.Order is null)
        {
            logger.LogWarning("Approved signal {SignalId} produced no paper order ({RejectionCode}).", signal.SignalId, result.RejectionCode);
            return;
        }

        if (result.Execution is null || !string.IsNullOrWhiteSpace(result.RejectionCode))
        {
            if (result.Order.Status == OrderStatus.PendingSubmit)
            {
                result.Order.MarkRejected(now, result.RejectionCode ?? "Rejected", signal.SignalId, approvalId);
            }

            await orders.AddAsync(result.Order, cancellationToken);
            return;
        }

        var execution = result.Execution;
        var recorded = RecordOrderFill(result.Order, execution, signal.SignalId, approvalId);
        var fill = recorded ?? execution;
        var position = ledger.ApplySpotFill(
            instrument,
            fill.Side,
            fill.Quantity,
            fill.Price,
            fill.Fee,
            instrument.QuoteAsset,
            fill.TimeUtc,
            fill.ExchangeTradeId,
            signal.SignalId,
            approvalId);
        if (position is null)
        {
            logger.LogInformation(
                "Paper fill {TradeId} was already applied to the ledger. Cash was not changed.",
                fill.ExchangeTradeId);
        }

        await orders.AddAsync(result.Order, cancellationToken);
    }

    private static Execution? RecordOrderFill(Order order, Execution execution, Guid correlationId, Guid causationId)
    {
        if (order.Status == OrderStatus.PendingSubmit)
        {
            order.MarkSubmitted(execution.TimeUtc, correlationId: correlationId, causationId: causationId);
        }

        return order.ApplyFill(
            execution.Id,
            execution.Quantity,
            execution.Price,
            execution.Fee,
            execution.FeeAsset,
            execution.TimeUtc,
            execution.ExchangeTradeId,
            correlationId,
            causationId);
    }

    private async Task<TradingAccount> GetOrCreateAccountAsync(CancellationToken cancellationToken)
    {
        var existing = await FindPaperAccountAsync(cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var created = TradingAccount.Create(
            TradingAccountId.New(),
            ExchangeId.Bybit,
            "Paper",
            TradingMode.Paper,
            UtcTime.Normalize(timeProvider.GetUtcNow()));
        await accounts.UpsertAsync(created, cancellationToken);
        return created;
    }

    private async Task<TradingAccount?> FindAccountAsync(Guid? accountId, CancellationToken cancellationToken)
    {
        if (accountId is null)
        {
            return await FindPaperAccountAsync(cancellationToken);
        }

        return await accounts.FindAsync(new TradingAccountId(accountId.Value), cancellationToken)
            ?? throw new NotFoundException($"Trading account '{accountId}' was not found.");
    }

    private async Task<TradingAccount?> FindPaperAccountAsync(CancellationToken cancellationToken)
    {
        var listed = await accounts.ListAsync(cancellationToken);
        return listed.FirstOrDefault(account => account.Mode == TradingMode.Paper && account.Exchange == ExchangeId.Bybit);
    }

    private async Task<PortfolioLedger> LoadLedgerAsync(
        TradingAccount account,
        string quoteAsset,
        CancellationToken cancellationToken)
    {
        var state = await paperState.LoadAsync(account.Id, cancellationToken);
        if (state is null)
        {
            return PortfolioLedger.Create(
                executionOptions.Value.PaperInitialEquity,
                quoteAsset,
                account.Id,
                account.Exchange,
                UtcTime.Normalize(timeProvider.GetUtcNow()));
        }

        if (state.AccountId != account.Id)
        {
            throw new InvalidOperationException("Paper state belongs to another account.");
        }

        var restored = state.Positions
            .Select(item => Position.Rehydrate(
                account.Id,
                state.Exchange,
                item.InstrumentId,
                item.InstrumentType,
                item.Side,
                new Quantity(item.Quantity),
                item.AveragePrice,
                item.RealizedPnl,
                item.Fees,
                item.Funding,
                item.FeeAsset,
                leverage: 1m,
                item.Margin,
                item.ContractMultiplier,
                item.OpenedAtUtc,
                item.UpdatedAtUtc))
            .ToArray();
        return PortfolioLedger.Rehydrate(
            account.Id,
            state.Exchange,
            state.QuoteAsset,
            state.Cash,
            state.PeakEquity,
            state.DayStartEquity,
            state.LastEquity,
            state.DayUtc,
            state.EmergencyStop,
            state.UpdatedAtUtc,
            restored,
            state.MarksByInstrument,
            state.AppliedExchangeTradeIds);
    }

    private async Task SaveLedgerAsync(TradingAccount account, PortfolioLedger ledger, CancellationToken cancellationToken)
    {
        await accounts.UpsertAsync(account, cancellationToken);
        foreach (var position in ledger.Positions)
        {
            await positions.UpsertAsync(position, cancellationToken);
        }

        await paperState.SaveAsync(ToState(ledger), cancellationToken);
        foreach (var domainEvent in ledger.DomainEvents)
        {
            if (domainEvent is RiskLimitTriggered)
            {
                await riskEvents.AddAsync(domainEvent, cancellationToken);
            }
        }

        ledger.ClearDomainEvents();
    }

    private static PaperState ToState(PortfolioLedger ledger)
    {
        var storedPositions = ledger.Positions
            .Select(position => new PaperPositionState(
                position.InstrumentId,
                position.InstrumentType,
                position.Side,
                position.Quantity.Value,
                position.AveragePrice,
                position.RealizedPnl,
                position.Fees,
                position.Funding,
                position.FeeAsset,
                position.ContractMultiplier,
                position.Margin,
                position.OpenedAtUtc,
                position.UpdatedAtUtc))
            .ToArray();
        return new PaperState(
            ledger.AccountId,
            ledger.Exchange,
            ledger.QuoteAsset,
            ledger.Cash,
            ledger.PeakEquity,
            ledger.DayStartEquity,
            ledger.LastEquity,
            ledger.DayUtc,
            ledger.EmergencyStop,
            storedPositions,
            ledger.AppliedExchangeTradeIds.ToArray(),
            new Dictionary<InstrumentId, decimal>(ledger.Marks),
            ledger.UpdatedAtUtc);
    }

    private MarketSnapshot BuildMarket(Candle candle, DateTimeOffset asOf, int staleAfterSeconds)
    {
        var now = UtcTime.Normalize(timeProvider.GetUtcNow());
        var stale = now - asOf > TimeSpan.FromSeconds(staleAfterSeconds);
        return new MarketSnapshot(asOf, asOf, healthy: !stale, stale: stale, bid: null, ask: null, last: candle.Close);
    }

    private static PortfolioSnapshot BuildSnapshot(
        PortfolioLedger ledger,
        Instrument instrument,
        decimal mark,
        IReadOnlyCollection<Guid> approvedSignalIds)
    {
        var position = ledger.FindPosition(instrument.Id);
        var hasPosition = position is not null && position.Side == PositionSide.Long && position.Quantity.Value > 0m;
        var openQuantity = hasPosition ? position!.Quantity.Value : (decimal?)null;
        var losing = hasPosition && position!.UnrealizedPnl(new Price(mark)) < 0m;
        var marks = new Dictionary<InstrumentId, Price>();
        var openCount = 0;
        foreach (var item in ledger.Positions)
        {
            if (item.Side != PositionSide.Long || item.Quantity.Value <= 0m)
            {
                continue;
            }

            openCount++;
            var price = ledger.Marks.TryGetValue(item.InstrumentId, out var stored) ? stored : item.AveragePrice;
            marks[item.InstrumentId] = new Price(price);
        }

        return new PortfolioSnapshot(
            ledger.Equity(marks),
            ledger.Cash,
            ledger.QuoteAsset,
            ledger.DailyPnl,
            ledger.DrawdownFraction,
            openCount,
            ledger.GrossExposure(marks),
            hasPosition,
            losing,
            openQuantity,
            ledger.EmergencyStop,
            approvedSignalIds);
    }

    private static bool HasOpenLong(PortfolioLedger ledger, InstrumentId instrumentId)
    {
        var position = ledger.FindPosition(instrumentId);
        return position is not null && position.Side == PositionSide.Long && position.Quantity.Value > 0m;
    }

    private static IReadOnlyList<Candle> ClosedUntil(
        IReadOnlyList<Candle> stored,
        Candle current,
        Timeframe timeframe,
        DateTimeOffset asOf)
    {
        var items = new List<Candle>();
        var seen = new HashSet<DateTimeOffset>();
        foreach (var candle in stored)
        {
            if (!candle.IsClosed || candle.Timeframe != timeframe || candle.CloseTimeUtc > asOf)
            {
                continue;
            }

            if (seen.Add(candle.OpenTimeUtc))
            {
                items.Add(candle);
            }
        }

        if (current.IsClosed
            && current.Timeframe == timeframe
            && current.CloseTimeUtc <= asOf
            && seen.Add(current.OpenTimeUtc))
        {
            items.Add(current);
        }

        items.Sort(static (left, right) => left.OpenTimeUtc.CompareTo(right.OpenTimeUtc));
        return items;
    }

    private async Task WithGateAsync(CancellationToken cancellationToken, Func<CancellationToken, Task> action)
    {
        ArgumentNullException.ThrowIfNull(gate);
        await gate.WaitAsync(cancellationToken);
        try
        {
            await action(cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<T> WithGateAsync<T>(CancellationToken cancellationToken, Func<CancellationToken, Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(gate);
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await action(cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }
}
