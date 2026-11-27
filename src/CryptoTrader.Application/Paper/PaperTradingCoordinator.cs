using System.Globalization;
using System.Security.Cryptography;
using System.Text;
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
    IPaperTradingControl tradingControl,
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

    public Task<PaperCandleResult> OnClosedCandleAsync(Candle candle, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candle);
        return WithGateAsync(cancellationToken, token => HandleCandleAsync(candle, token));
    }

    public Task<EmergencyStopOutcome> EngageEmergencyStopAsync(
        string? reason,
        string? exchange,
        string? quoteAsset,
        CancellationToken cancellationToken) =>
        WithGateAsync(cancellationToken, token => EngageEmergencyStopCoreAsync(reason, exchange, quoteAsset, token));

    public Task<EmergencyStopOutcome> ReleaseEmergencyStopAsync(
        string? exchange,
        string? quoteAsset,
        CancellationToken cancellationToken) =>
        WithGateAsync(cancellationToken, token => ReleaseEmergencyStopCoreAsync(exchange, quoteAsset, token));

    public Task<IReadOnlyList<PaperAccountSummary>> ListAccountsAsync(CancellationToken cancellationToken) =>
        WithGateAsync(cancellationToken, ListAccountsCoreAsync);

    public Task<PaperTradingSnapshot> GetSnapshotAsync(
        Guid? accountId,
        TradingMode? tradingMode,
        string? exchange,
        string? quoteAsset,
        CancellationToken cancellationToken) =>
        WithGateAsync(cancellationToken, token => GetSnapshotCoreAsync(accountId, tradingMode, exchange, quoteAsset, token));

    private async Task<PaperCandleResult> HandleCandleAsync(Candle candle, CancellationToken cancellationToken)
    {
        if (!candle.IsClosed)
        {
            return PaperCandleResult.Skipped("Candle is not closed.");
        }

        var instrument = await instruments.FindAsync(candle.Instrument, cancellationToken);
        if (instrument is null)
        {
            logger.LogWarning("Closed candle at {CloseTime} has no stored instrument {InstrumentId}.", candle.CloseTimeUtc, candle.Instrument.Value);
            return PaperCandleResult.Skipped("Stored instrument was not found.");
        }

        if (!PaperAccountScope.SameExchange(instrument.Exchange, candle.Exchange) || candle.Instrument != instrument.Id)
        {
            logger.LogWarning(
                "Candle {CandleInstrument} on {CandleExchange} does not match instrument {InstrumentId} on {InstrumentExchange}.",
                candle.Instrument.Value,
                candle.Exchange.Value,
                instrument.Id.Value,
                instrument.Exchange.Value);
            return PaperCandleResult.Skipped("Candle identity does not match the stored instrument.");
        }

        if (instrument.Type != InstrumentType.Spot || candle.VolumeUnit != VolumeUnit.BaseAsset)
        {
            logger.LogInformation(
                "Paper trading skipped for {InstrumentId}: type {InstrumentType}, volume unit {VolumeUnit}.",
                instrument.Id.Value,
                instrument.Type,
                candle.VolumeUnit);
            return PaperCandleResult.Skipped("Paper trading accepts spot base-asset volume only.");
        }

        var marketData = marketDataOptions.Value ?? throw new InvalidOperationException("Market data options are not configured.");
        var asOf = UtcTime.Normalize(candle.CloseTimeUtc);
        var admission = CandleProcessingDecision.Evaluate(
            candle.IsClosed,
            asOf,
            receivedAtUtc: null,
            UtcTime.Normalize(timeProvider.GetUtcNow()),
            CandleProcessingDecision.Window(marketData.StaleAfterSeconds));
        if (!admission.CanUpdatePaper)
        {
            logger.LogInformation(
                "Paper trading skipped stale {Exchange} {InstrumentId} candle closed at {CloseTime}: {Reason}.",
                candle.Exchange.Value,
                instrument.Id.Value,
                asOf,
                admission.Reason);
            return PaperCandleResult.Skipped(admission.Reason);
        }

        var account = await GetOrCreateAccountAsync(instrument.Exchange, instrument.QuoteAsset, cancellationToken);
        var existingState = await paperState.LoadAsync(account.Id, cancellationToken);
        var ledger = await LoadLedgerAsync(account, instrument.QuoteAsset, cancellationToken);
        var candleKey = PaperCandleKey.For(candle);
        if (ledger.HasAppliedCandle(candleKey))
        {
            logger.LogInformation(
                "Paper candle {CandleKey} was already applied to {Exchange} {QuoteAsset}.",
                candleKey,
                ledger.Exchange.Value,
                ledger.QuoteAsset);
            return PaperCandleResult.Applied("Candle was already applied.");
        }

        if (ledger.EmergencyStop)
        {
            if (existingState is null || !existingState.EmergencyStop)
            {
                await SaveLedgerAsync(account, ledger, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            logger.LogInformation(
                "Paper trading deferred for {Exchange} {QuoteAsset}: emergency stop is engaged.",
                ledger.Exchange.Value,
                ledger.QuoteAsset);
            return PaperCandleResult.Deferred("Emergency stop is engaged.");
        }

        ledger.ObserveMark(instrument.Id, new Price(candle.Close), asOf);
        var running = (await strategyInstances.ListAsync(cancellationToken))
            .Where(instance => instance.Status == StrategyInstanceStatus.Running && instance.InstrumentId == instrument.Id)
            .ToArray();
        if (running.Length == 0)
        {
            ledger.MarkCandleApplied(candleKey);
            await SaveLedgerAsync(account, ledger, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            metrics.RecordEquity(ledger.LastEquity);
            return PaperCandleResult.Applied(null);
        }

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
                    candle,
                    instance,
                    signal,
                    market,
                    profile,
                    approvedIds,
                    seen,
                    cancellationToken);
            }
        }

        ledger.MarkCandleApplied(candleKey);
        await SaveLedgerAsync(account, ledger, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        metrics.RecordEquity(ledger.LastEquity);
        return PaperCandleResult.Applied(null);
    }

    private async Task ProcessSignalAsync(
        TradingAccount account,
        PortfolioLedger ledger,
        Instrument instrument,
        Candle candle,
        StrategyInstance instance,
        TradeSignal signal,
        MarketSnapshot market,
        RiskProfile profile,
        HashSet<Guid> approvedIds,
        HashSet<Guid> seen,
        CancellationToken cancellationToken)
    {
        var clientOrderId = PaperClientOrderId(account, candle, instance, signal.Action);
        var alreadyStored = seen.Contains(signal.SignalId) || await signals.ExistsAsync(signal.SignalId, cancellationToken);
        if (alreadyStored)
        {
            await ReconcileExistingOrderAsync(account, ledger, instrument, signal, clientOrderId, cancellationToken);
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
            if (string.Equals(decision.RejectionCode, RiskRejectionCodes.DuplicateSignal, StringComparison.Ordinal))
            {
                await ReconcileExistingOrderAsync(account, ledger, instrument, signal, clientOrderId, cancellationToken);
            }

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

        await ExecuteApprovedAsync(account, ledger, instrument, signal, quantity, market, clientOrderId, cancellationToken);
        logger.LogInformation("Signal {SignalId} {Action} was approved.", signal.SignalId, signal.Action);
    }

    private async Task ExecuteApprovedAsync(
        TradingAccount account,
        PortfolioLedger ledger,
        Instrument instrument,
        TradeSignal signal,
        Quantity quantity,
        MarketSnapshot market,
        string clientOrderId,
        CancellationToken cancellationToken)
    {
        if (await ReconcileExistingOrderAsync(account, ledger, instrument, signal, clientOrderId, cancellationToken))
        {
            return;
        }

        var now = UtcTime.Normalize(timeProvider.GetUtcNow());
        var approvalId = Guid.NewGuid();
        var approved = new ApprovedOrder(
            approvalId,
            signal,
            account.Id,
            quantity,
            OrderType.Market,
            limitPrice: null,
            clientOrderId,
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
        if (!string.Equals(fill.ExchangeTradeId, PaperTradeId(clientOrderId), StringComparison.Ordinal))
        {
            logger.LogWarning(
                "Paper fill {TradeId} does not match the deterministic id for {ClientOrderId}.",
                fill.ExchangeTradeId,
                clientOrderId);
        }

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

    private async Task<EmergencyStopOutcome> EngageEmergencyStopCoreAsync(
        string? reason,
        string? exchange,
        string? quoteAsset,
        CancellationToken cancellationToken)
    {
        var now = UtcTime.Normalize(timeProvider.GetUtcNow());
        var exchangeFilter = ParseExchange(exchange);
        var quoteFilter = string.IsNullOrWhiteSpace(quoteAsset) ? null : PaperAccountScope.NormalizeQuote(quoteAsset);
        var normalizedReason = string.IsNullOrWhiteSpace(reason) ? "Emergency stop." : reason.Trim();
        if (exchangeFilter is null && quoteFilter is null)
        {
            await tradingControl.EngageAsync(normalizedReason, now, cancellationToken);
            var existing = await SelectAccountsAsync(null, null, cancellationToken);
            if (existing.Count == 0)
            {
                return new EmergencyStopOutcome(true, now);
            }

            return await EngageLedgersAsync(existing, quoteFilter, normalizedReason, now, cancellationToken);
        }

        if (exchangeFilter is not ExchangeId selected || quoteFilter is null)
        {
            return new EmergencyStopOutcome(false, now);
        }

        var targets = await SelectAccountsAsync(selected, quoteFilter, cancellationToken);
        if (targets.Count == 0)
        {
            targets = [await GetOrCreateAccountAsync(selected, quoteFilter, cancellationToken)];
        }

        return await EngageLedgersAsync(targets, quoteFilter, normalizedReason, now, cancellationToken);
    }

    private async Task<EmergencyStopOutcome> ReleaseEmergencyStopCoreAsync(
        string? exchange,
        string? quoteAsset,
        CancellationToken cancellationToken)
    {
        var now = UtcTime.Normalize(timeProvider.GetUtcNow());
        var exchangeFilter = ParseExchange(exchange);
        var quoteFilter = string.IsNullOrWhiteSpace(quoteAsset) ? null : PaperAccountScope.NormalizeQuote(quoteAsset);
        if (exchangeFilter is null && quoteFilter is null)
        {
            await tradingControl.ReleaseAsync(now, cancellationToken);
            foreach (var account in await SelectAccountsAsync(null, null, cancellationToken))
            {
                var quote = await QuoteForAsync(account, cancellationToken) ?? DefaultQuoteAsset;
                var ledger = await LoadLedgerAsync(account, quote, cancellationToken);
                if (!ledger.EmergencyStop)
                {
                    continue;
                }

                ledger.ReleaseEmergencyStop(now);
                await SaveLedgerAsync(account, ledger, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return new EmergencyStopOutcome(false, now);
        }

        if (exchangeFilter is not ExchangeId selected || quoteFilter is null)
        {
            var control = await tradingControl.GetAsync(cancellationToken);
            return new EmergencyStopOutcome(control.EmergencyStop, control.UpdatedAtUtc ?? now);
        }

        if ((await tradingControl.GetAsync(cancellationToken)).EmergencyStop)
        {
            return new EmergencyStopOutcome(true, now);
        }

        foreach (var account in await SelectAccountsAsync(selected, quoteFilter, cancellationToken))
        {
            var ledger = await LoadLedgerAsync(account, quoteFilter, cancellationToken);
            if (!ledger.EmergencyStop)
            {
                continue;
            }

            ledger.ReleaseEmergencyStop(now);
            await SaveLedgerAsync(account, ledger, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new EmergencyStopOutcome(false, now);
    }

    private async Task<EmergencyStopOutcome> EngageLedgersAsync(
        IReadOnlyList<TradingAccount> targets,
        string? quoteFilter,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var engaged = false;
        var occurredAt = now;
        foreach (var account in targets)
        {
            var quote = quoteFilter ?? await QuoteForAsync(account, cancellationToken) ?? DefaultQuoteAsset;
            var ledger = await LoadLedgerAsync(account, quote, cancellationToken);
            var wasEngaged = ledger.EmergencyStop;
            ledger.EngageEmergencyStop(now, reason);
            if (!wasEngaged)
            {
                await SaveLedgerAsync(account, ledger, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            metrics.RecordEquity(ledger.LastEquity);
            engaged = ledger.EmergencyStop;
            occurredAt = ledger.UpdatedAtUtc;
        }

        return new EmergencyStopOutcome(engaged, occurredAt);
    }

    private async Task<IReadOnlyList<PaperAccountSummary>> ListAccountsCoreAsync(CancellationToken cancellationToken)
    {
        var listed = await accounts.ListAsync(cancellationToken);
        var summaries = new List<PaperAccountSummary>();
        foreach (var account in listed)
        {
            if (account.Mode != TradingMode.Paper)
            {
                continue;
            }

            var state = await paperState.LoadAsync(account.Id, cancellationToken);
            if (state is null)
            {
                continue;
            }

            var openPositions = state.Positions.Count(position =>
                position.Side == PositionSide.Long && position.Quantity > 0m);
            var drawdown = state.PeakEquity <= 0m
                ? 0m
                : Math.Max(0m, (state.PeakEquity - state.LastEquity) / state.PeakEquity);
            summaries.Add(new PaperAccountSummary(
                account.Id.Value,
                account.Exchange.Value,
                state.QuoteAsset,
                state.Cash,
                state.LastEquity,
                state.LastEquity - state.DayStartEquity,
                drawdown,
                state.EmergencyStop,
                openPositions));
        }

        return summaries
            .OrderBy(item => item.Exchange, StringComparer.Ordinal)
            .ThenBy(item => item.QuoteAsset, StringComparer.Ordinal)
            .ToArray();
    }

    private async Task<PaperTradingSnapshot> GetSnapshotCoreAsync(
        Guid? accountId,
        TradingMode? tradingMode,
        string? exchange,
        string? quoteAsset,
        CancellationToken cancellationToken)
    {
        var selected = await SelectSnapshotAccountsAsync(accountId, exchange, quoteAsset, cancellationToken);
        if (selected.Count == 0)
        {
            return PaperTradingSnapshot.Empty;
        }

        var listedOrders = new List<Order>();
        var listedPositions = new List<Position>();
        foreach (var account in selected)
        {
            listedOrders.AddRange(await orders.ListAsync(account.Id, tradingMode, cancellationToken));
            listedPositions.AddRange(await positions.ListAsync(account.Id, cancellationToken));
        }

        return new PaperTradingSnapshot(selected.Count == 1 ? selected[0] : null, listedOrders, listedPositions);
    }

    private async Task<IReadOnlyList<TradingAccount>> SelectSnapshotAccountsAsync(
        Guid? accountId,
        string? exchange,
        string? quoteAsset,
        CancellationToken cancellationToken)
    {
        if (accountId is Guid id)
        {
            var account = await accounts.FindAsync(new TradingAccountId(id), cancellationToken)
                ?? throw new NotFoundException($"Trading account '{accountId}' was not found.");
            return [account];
        }

        return await SelectAccountsAsync(ParseExchange(exchange), string.IsNullOrWhiteSpace(quoteAsset) ? null : PaperAccountScope.NormalizeQuote(quoteAsset), cancellationToken);
    }

    private async Task<List<TradingAccount>> SelectAccountsAsync(
        ExchangeId? exchange,
        string? quoteAsset,
        CancellationToken cancellationToken)
    {
        var listed = await accounts.ListAsync(cancellationToken);
        var selected = new List<TradingAccount>();
        foreach (var account in listed)
        {
            if (account.Mode != TradingMode.Paper)
            {
                continue;
            }

            if (exchange is ExchangeId required && !PaperAccountScope.SameExchange(account.Exchange, required))
            {
                continue;
            }

            if (quoteAsset is null)
            {
                selected.Add(account);
                continue;
            }

            var state = await paperState.LoadAsync(account.Id, cancellationToken);
            if (state is not null && PaperAccountScope.SameQuote(state.QuoteAsset, quoteAsset))
            {
                selected.Add(account);
            }
        }

        return selected;
    }

    private async Task<string?> QuoteForAsync(TradingAccount account, CancellationToken cancellationToken)
    {
        var state = await paperState.LoadAsync(account.Id, cancellationToken);
        return state?.QuoteAsset;
    }

    private async Task<bool> ReconcileExistingOrderAsync(
        TradingAccount account,
        PortfolioLedger ledger,
        Instrument instrument,
        TradeSignal signal,
        string clientOrderId,
        CancellationToken cancellationToken)
    {
        var existing = await orders.FindByClientOrderIdAsync(account.Id, clientOrderId, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        var tradeId = existing.AppliedExchangeTradeIds.FirstOrDefault() ?? PaperTradeId(clientOrderId);
        if (ledger.AppliedExchangeTradeIds.Contains(tradeId) || existing.AverageFillPrice is not decimal price)
        {
            return true;
        }

        var feeRate = riskOptions.Value?.FeeRate ?? 0m;
        var fee = price * existing.FilledQuantity.Value * feeRate;
        if (fee < 0m)
        {
            fee = 0m;
        }

        ledger.ApplySpotFill(
            instrument,
            existing.Side,
            existing.FilledQuantity,
            new Price(price),
            fee,
            instrument.QuoteAsset,
            existing.UpdatedAtUtc,
            tradeId,
            signal.SignalId,
            causationId: null);
        logger.LogInformation("Recovered paper fill {TradeId} onto {Exchange} {QuoteAsset}.", tradeId, ledger.Exchange.Value, ledger.QuoteAsset);
        return true;
    }

    private static string PaperClientOrderId(
        TradingAccount account,
        Candle candle,
        StrategyInstance instance,
        SignalAction action)
    {
        var raw = string.Create(
            CultureInfo.InvariantCulture,
            $"{account.Id.Value:N}|{candle.Exchange.Value}|{candle.Instrument.Value}|{candle.Timeframe.Code}|{candle.OpenTimeUtc.UtcDateTime:O}|{instance.Id.Value:N}|{action}");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
        return "p" + hash[..32].ToLowerInvariant();
    }

    private static string PaperTradeId(string clientOrderId) => "paper-fill-" + clientOrderId;

    private static ExchangeId? ParseExchange(string? exchange)
    {
        if (string.IsNullOrWhiteSpace(exchange))
        {
            return null;
        }

        return new ExchangeId(PaperAccountScope.NormalizeExchange(exchange));
    }

    private async Task<TradingAccount> GetOrCreateAccountAsync(
        ExchangeId exchange,
        string quoteAsset,
        CancellationToken cancellationToken)
    {
        var existing = await FindPaperAccountAsync(exchange, quoteAsset, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var scopedId = PaperAccountScope.IdFor(exchange, quoteAsset);
        var reserved = await accounts.FindAsync(scopedId, cancellationToken);
        if (reserved is not null)
        {
            if (reserved.Mode != TradingMode.Paper || !PaperAccountScope.SameExchange(reserved.Exchange, exchange))
            {
                throw new InvalidOperationException("The paper account id is already used by a different account.");
            }

            return reserved;
        }

        var created = TradingAccount.Create(
            scopedId,
            new ExchangeId(PaperAccountScope.NormalizeExchange(exchange.Value)),
            PaperAccountScope.DisplayName(exchange, quoteAsset),
            TradingMode.Paper,
            UtcTime.Normalize(timeProvider.GetUtcNow()));
        await accounts.UpsertAsync(created, cancellationToken);
        return created;
    }

    private async Task<TradingAccount?> FindPaperAccountAsync(
        ExchangeId exchange,
        string quoteAsset,
        CancellationToken cancellationToken)
    {
        var listed = await accounts.ListAsync(cancellationToken);
        TradingAccount? oldest = null;
        foreach (var account in listed)
        {
            if (account.Mode != TradingMode.Paper || !PaperAccountScope.SameExchange(account.Exchange, exchange))
            {
                continue;
            }

            var state = await paperState.LoadAsync(account.Id, cancellationToken);
            if (state is null || !PaperAccountScope.SameQuote(state.QuoteAsset, quoteAsset))
            {
                continue;
            }

            if (oldest is null || account.CreatedAtUtc < oldest.CreatedAtUtc)
            {
                oldest = account;
            }
        }

        return oldest;
    }

    private async Task<PortfolioLedger> LoadLedgerAsync(
        TradingAccount account,
        string quoteAsset,
        CancellationToken cancellationToken)
    {
        var control = await tradingControl.GetAsync(cancellationToken);
        var now = UtcTime.Normalize(timeProvider.GetUtcNow());
        var state = await paperState.LoadAsync(account.Id, cancellationToken);
        if (state is null)
        {
            var created = PortfolioLedger.Create(
                executionOptions.Value.PaperInitialEquity,
                quoteAsset,
                account.Id,
                account.Exchange,
                now);
            if (control.EmergencyStop)
            {
                created.EngageEmergencyStop(now, control.Reason ?? "Emergency stop.");
            }

            return created;
        }

        if (state.AccountId != account.Id || !PaperAccountScope.SameExchange(state.Exchange, account.Exchange))
        {
            throw new InvalidOperationException("Paper state belongs to another account.");
        }

        if (!PaperAccountScope.SameQuote(state.QuoteAsset, quoteAsset))
        {
            throw new InvalidOperationException("Paper state quote asset does not match the requested account scope.");
        }

        var restoredPositions = state.Positions
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
        var restored = PortfolioLedger.Rehydrate(
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
            restoredPositions,
            state.MarksByInstrument,
            state.AppliedExchangeTradeIds,
            state.AppliedCandleKeys);
        if (control.EmergencyStop && !restored.EmergencyStop)
        {
            restored.EngageEmergencyStop(now, control.Reason ?? "Emergency stop.");
        }

        return restored;
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
            ledger.UpdatedAtUtc,
            ledger.AppliedCandleKeys.ToArray());
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
