using System.Globalization;
using CryptoTrader.Application.Abstractions.Backtesting;
using CryptoTrader.Application.Abstractions.Risk;
using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.Instruments;
using CryptoTrader.Domain.MarketData;
using CryptoTrader.Domain.Strategies;
using CryptoTrader.Domain.Trading;
using CryptoTrader.Strategies.Abstractions;
using Microsoft.Extensions.Logging;

namespace CryptoTrader.Backtesting;

public sealed class BacktestEngine : IBacktestEngine
{
    private static readonly TradingAccountId BacktestAccountId = new(Guid.Parse("00000000-0000-4000-8000-0000000000b1"));

    private readonly IRiskManager _riskManager;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<BacktestEngine>? _logger;

    public BacktestEngine(IRiskManager riskManager, TimeProvider timeProvider, ILogger<BacktestEngine>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(riskManager);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _riskManager = riskManager;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    internal TimeProvider Clock => _timeProvider;

    public async Task<BacktestReport> RunAsync(
        BacktestRequest request,
        ITradingStrategy strategy,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(strategy);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.InstrumentSpec.Type != InstrumentType.Spot)
        {
            throw new InvalidOperationException("BacktestEngine 0.1 applies spot fills only.");
        }

        var instrument = request.InstrumentSpec;
        var working = CandleWindow.Working(request.WorkingCandles, request.FromUtc, request.ToUtc);
        var higher = CandleWindow.Higher(request.HigherCandles);
        var higherCloseOrdered = CandleWindow.IsCloseTimeOrdered(higher);
        var (stopMultiplier, takeMultiplier) = ExitMultiplierReader.Read(request.ParametersJson);
        var runId = BacktestRunIdentity.Create(request);
        var assumptions = BacktestAssumptionsFactory.Create();
        var barDuration = working.Count == 0 ? TimeSpan.Zero : working[0].Timeframe.Duration;

        if (working.Count == 0)
        {
            return Report(request, runId, assumptions, BacktestMetricsCalculator.Compute(request.InitialEquity, [], [], barDuration), [], []);
        }

        var ledger = PortfolioLedger.Create(
            request.InitialEquity,
            instrument.QuoteAsset,
            BacktestAccountId,
            request.Exchange,
            request.FromUtc.ToUniversalTime());
        var approvedSignalIds = new List<Guid>();
        var trades = new List<BacktestTrade>();
        var equityCurve = new List<EquityPoint>(working.Count);
        OpenTrade? position = null;
        PendingEntry? pendingEntry = null;
        int? pendingTrendExitIndex = null;
        var nextTradeNumber = 1;

        for (var index = 0; index < working.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candle = working[index];

            if (pendingEntry is not null && pendingEntry.CandleIndex == index)
            {
                position = TryOpen(
                    ledger,
                    instrument,
                    candle,
                    pendingEntry,
                    nextTradeNumber,
                    stopMultiplier,
                    takeMultiplier,
                    request.FeeRate,
                    request.SlippageBps);
                pendingEntry = null;
                if (position is not null)
                {
                    nextTradeNumber++;
                }
            }

            if (pendingTrendExitIndex == index)
            {
                if (position is not null)
                {
                    trades.Add(Close(ledger, instrument, request.Instrument, position, candle, BacktestExitReason.TrendBreak, request.FeeRate, request.SlippageBps));
                    position = null;
                }

                pendingTrendExitIndex = null;
            }

            if (position is not null && ProtectiveReason(position, candle) is BacktestExitReason protectiveReason)
            {
                trades.Add(Close(ledger, instrument, request.Instrument, position, candle, protectiveReason, request.FeeRate, request.SlippageBps));
                position = null;
                pendingTrendExitIndex = null;
            }

            ledger.ObserveMark(instrument.Id, new Price(candle.Close), candle.CloseTimeUtc);

            var hasNext = index < working.Count - 1;
            IReadOnlyList<Candle> visibleHigher = higherCloseOrdered
                ? new ReadOnlyPrefix<Candle>(higher, CandleWindow.CountClosedBy(higher, candle.CloseTimeUtc))
                : CandleWindow.ClosedBy(higher, candle.CloseTimeUtc);
            var context = new StrategyContext(
                instrument,
                candle.CloseTimeUtc,
                request.ParametersJson,
                new ReadOnlyPrefix<Candle>(working, index + 1),
                visibleHigher,
                position is not null);
            var signals = await strategy.EvaluateAsync(context, cancellationToken);
            if (signals is not null)
            {
                foreach (var signal in signals)
                {
                    if (signal.InstrumentId != instrument.Id)
                    {
                        continue;
                    }

                    if (signal.Action == SignalAction.EnterLong)
                    {
                        if (position is not null || pendingEntry is not null)
                        {
                            continue;
                        }

                        var decision = await _riskManager.EvaluateAsync(
                            signal,
                            CreateSnapshot(ledger, instrument, candle.Close, approvedSignalIds),
                            CreateMarket(candle),
                            instrument,
                            request.RiskProfile,
                            cancellationToken);
                        if (!decision.Approved || decision.ApprovedQuantity is not Quantity quantity || quantity.Value <= 0)
                        {
                            continue;
                        }

                        approvedSignalIds.Add(signal.SignalId);
                        if (hasNext)
                        {
                            pendingEntry = new PendingEntry(signal, quantity, index + 1);
                        }
                    }
                    else if (signal.Action == SignalAction.ExitLong
                        && request.ExitOnTrendBreak
                        && hasNext
                        && position is not null
                        && pendingTrendExitIndex is null)
                    {
                        pendingTrendExitIndex = index + 1;
                    }
                }
            }

            if (!hasNext && position is not null)
            {
                trades.Add(Close(ledger, instrument, request.Instrument, position, candle, BacktestExitReason.EndOfData, request.FeeRate, request.SlippageBps));
                position = null;
            }

            equityCurve.Add(new EquityPoint(candle.CloseTimeUtc, ledger.LastEquity));
        }

        var metrics = BacktestMetricsCalculator.Compute(request.InitialEquity, equityCurve, trades, barDuration);
        _logger?.LogInformation(
            "Backtest {StrategyId} {EngineVersion} produced {TradeCount} trades.",
            request.StrategyId,
            BacktestEngineInfo.Version,
            trades.Count);
        return Report(request, runId, assumptions, metrics, trades, equityCurve);
    }

    private static BacktestReport Report(
        BacktestRequest request,
        Guid runId,
        BacktestAssumptions assumptions,
        BacktestMetrics metrics,
        IReadOnlyList<BacktestTrade> trades,
        IReadOnlyList<EquityPoint> equityCurve) =>
        new(
            runId,
            request.StrategyId,
            request.StrategyVersion,
            BacktestEngineInfo.Version,
            request.ParametersJson,
            request.FromUtc,
            request.ToUtc,
            metrics,
            assumptions,
            trades,
            equityCurve);

    private static OpenTrade? TryOpen(
        PortfolioLedger ledger,
        Instrument instrument,
        Candle candle,
        PendingEntry pending,
        int tradeNumber,
        decimal stopMultiplier,
        decimal takeMultiplier,
        decimal feeRate,
        decimal slippageBps)
    {
        if (pending.Signal.Atr is not decimal atr || atr < 0)
        {
            return null;
        }

        // Flooring the buy to the price tick would improve the fill. Keep the exact slipped price.
        var rawPrice = candle.Open;
        var fillPrice = rawPrice * (1m + (slippageBps / 10_000m));
        if (fillPrice <= 0)
        {
            return null;
        }

        var stop = fillPrice - (atr * stopMultiplier);
        if (stop <= 0)
        {
            return null;
        }

        decimal? take = null;
        if (takeMultiplier > 0)
        {
            var takePrice = fillPrice + (atr * takeMultiplier);
            if (takePrice > fillPrice)
            {
                take = takePrice;
            }
        }

        var quantity = pending.Quantity;
        var notional = fillPrice * quantity.Value;
        var fee = notional * feeRate;
        var cashBefore = ledger.Cash;
        var tradeId = "bt-entry-" + tradeNumber.ToString(CultureInfo.InvariantCulture);
        var filled = ledger.ApplySpotFill(
            instrument,
            OrderSide.Buy,
            quantity,
            new Price(fillPrice),
            fee,
            instrument.QuoteAsset,
            candle.OpenTimeUtc,
            tradeId,
            pending.Signal.SignalId);
        ledger.ClearDomainEvents();
        if (filled is null)
        {
            throw new InvalidOperationException($"Entry trade '{tradeId}' was already applied.");
        }

        return new OpenTrade(
            tradeNumber,
            candle.OpenTimeUtc,
            fillPrice,
            quantity.Value,
            stop,
            take,
            fee,
            Math.Abs(fillPrice - rawPrice) * quantity.Value,
            cashBefore);
    }

    private static BacktestTrade Close(
        PortfolioLedger ledger,
        Instrument instrument,
        InstrumentId instrumentId,
        OpenTrade position,
        Candle candle,
        BacktestExitReason reason,
        decimal feeRate,
        decimal slippageBps)
    {
        var rawPrice = reason switch
        {
            BacktestExitReason.StopLoss => position.Stop,
            BacktestExitReason.TakeProfit => position.Take ?? throw new InvalidOperationException("Take profit is not set."),
            BacktestExitReason.TrendBreak => candle.Open,
            BacktestExitReason.EndOfData => candle.Close,
            _ => throw new InvalidOperationException($"Exit reason '{reason}' is not supported."),
        };
        var time = reason == BacktestExitReason.TrendBreak ? candle.OpenTimeUtc : candle.CloseTimeUtc;
        var fillPrice = rawPrice * (1m - (slippageBps / 10_000m));
        if (fillPrice <= 0)
        {
            throw new InvalidOperationException("Exit price after adverse slippage is not positive.");
        }

        var held = ledger.FindPosition(instrument.Id);
        if (held is not { Side: PositionSide.Long } || held.Quantity.Value <= 0)
        {
            throw new InvalidOperationException("Cannot exit a flat backtest position.");
        }

        var quantity = held.Quantity;
        var notional = fillPrice * quantity.Value;
        var fee = notional * feeRate;
        var tradeId = "bt-exit-" + position.Number.ToString(CultureInfo.InvariantCulture);
        var filled = ledger.ApplySpotFill(
            instrument,
            OrderSide.Sell,
            quantity,
            new Price(fillPrice),
            fee,
            instrument.QuoteAsset,
            time,
            tradeId);
        ledger.ClearDomainEvents();
        if (filled is null)
        {
            throw new InvalidOperationException($"Exit trade '{tradeId}' was already applied.");
        }

        return new BacktestTrade(
            position.Number,
            instrumentId,
            position.EntryTimeUtc,
            time,
            position.EntryPrice,
            fillPrice,
            quantity.Value,
            position.EntryFee + fee,
            position.EntrySlippage + (Math.Abs(fillPrice - rawPrice) * quantity.Value),
            ledger.Cash - position.CashBeforeEntry,
            reason);
    }

    private static BacktestExitReason? ProtectiveReason(OpenTrade position, Candle candle)
    {
        if (candle.Low <= position.Stop)
        {
            return BacktestExitReason.StopLoss;
        }

        if (position.Take is decimal take && candle.High >= take)
        {
            return BacktestExitReason.TakeProfit;
        }

        return null;
    }

    private static PortfolioSnapshot CreateSnapshot(
        PortfolioLedger ledger,
        Instrument instrument,
        decimal mark,
        IReadOnlyList<Guid> approvedSignalIds)
    {
        var position = ledger.FindPosition(instrument.Id);
        var hasPosition = position is { Side: PositionSide.Long, Quantity.Value: > 0 };
        var marks = new Dictionary<InstrumentId, Price>();
        decimal? openQuantity = null;
        var losing = false;
        if (hasPosition && position is not null)
        {
            var marked = new Price(mark);
            marks[instrument.Id] = marked;
            openQuantity = position.Quantity.Value;
            losing = position.UnrealizedPnl(marked) < 0;
        }

        var openCount = 0;
        foreach (var candidate in ledger.Positions)
        {
            if (candidate.Side == PositionSide.Long && candidate.Quantity.Value > 0)
            {
                openCount++;
            }
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
            approvedSignalIds.ToArray());
    }

    private static MarketSnapshot CreateMarket(Candle candle) =>
        new(candle.CloseTimeUtc, candle.CloseTimeUtc, true, false, null, null, candle.Close);

    private sealed record PendingEntry(TradeSignal Signal, Quantity Quantity, int CandleIndex);

    private sealed record OpenTrade(
        int Number,
        DateTimeOffset EntryTimeUtc,
        decimal EntryPrice,
        decimal Quantity,
        decimal Stop,
        decimal? Take,
        decimal EntryFee,
        decimal EntrySlippage,
        decimal CashBeforeEntry);
}
