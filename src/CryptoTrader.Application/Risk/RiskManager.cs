using CryptoTrader.Application.Abstractions.Risk;
using CryptoTrader.Application.Configuration;
using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.Instruments;
using CryptoTrader.Domain.Risk;
using CryptoTrader.Domain.Strategies;
using CryptoTrader.Domain.Trading;
using Microsoft.Extensions.Options;

namespace CryptoTrader.Application.Risk;

/// <summary>
/// Applies the 0.1 risk gates in the order fixed by <see cref="IRiskManager"/>.
/// Live versus paper is read from <see cref="ExecutionOptions"/>, not from the call.
/// </summary>
public sealed class RiskManager : IRiskManager
{
    private readonly IOptions<ExecutionOptions> _execution;
    private readonly TimeProvider _timeProvider;

    public RiskManager(IOptions<ExecutionOptions> execution, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(execution);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _execution = execution;
        _timeProvider = timeProvider;
    }

    public ValueTask<RiskDecision> EvaluateAsync(
        TradeSignal signal,
        PortfolioSnapshot portfolio,
        MarketSnapshot market,
        Instrument instrument,
        RiskProfile riskProfile,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(signal);
        ArgumentNullException.ThrowIfNull(portfolio);
        ArgumentNullException.ThrowIfNull(market);
        ArgumentNullException.ThrowIfNull(instrument);
        ArgumentNullException.ThrowIfNull(riskProfile);
        var execution = _execution.Value ?? throw new InvalidOperationException("Execution options are not configured.");
        _ = _timeProvider.GetUtcNow();
        return ValueTask.FromResult(Evaluate(signal, portfolio, market, instrument, riskProfile, execution));
    }

    private static RiskDecision Evaluate(
        TradeSignal signal,
        PortfolioSnapshot portfolio,
        MarketSnapshot market,
        Instrument instrument,
        RiskProfile riskProfile,
        ExecutionOptions execution)
    {
        var enter = signal.Action == SignalAction.EnterLong;
        var exit = signal.Action == SignalAction.ExitLong;
        if (!enter && !exit)
        {
            throw new ArgumentOutOfRangeException(nameof(signal), "Signal action is not supported.");
        }

        var passed = new List<string>();

        if (portfolio.EmergencyStop)
        {
            return Reject(RiskRejectionCodes.EmergencyStop, "Emergency stop is engaged.", passed);
        }

        passed.Add(RiskRejectionCodes.EmergencyStop);

        if (enter && !instrument.IsTradingEnabled)
        {
            return Reject(RiskRejectionCodes.InstrumentDisabled, "The instrument is not enabled for trading.", passed);
        }

        passed.Add(RiskRejectionCodes.InstrumentDisabled);

        if (instrument.Type == InstrumentType.LinearPerpetual && execution.Mode == TradingMode.Live)
        {
            return Reject(RiskRejectionCodes.FuturesLiveForbidden, "Live linear perpetual orders are disabled.", passed);
        }

        passed.Add(RiskRejectionCodes.FuturesLiveForbidden);

        if (riskProfile.AllowCrossMargin)
        {
            return Reject(RiskRejectionCodes.CrossMarginForbidden, "Cross margin is disabled.", passed);
        }

        passed.Add(RiskRejectionCodes.CrossMarginForbidden);

        if (execution.LiveTradingEnabled)
        {
            return Reject(RiskRejectionCodes.LiveTradingDisabled, "Live trading is disabled.", passed);
        }

        passed.Add(RiskRejectionCodes.LiveTradingDisabled);

        if (market.Stale)
        {
            return Reject(RiskRejectionCodes.StaleMarketData, "Market data is stale.", passed);
        }

        passed.Add(RiskRejectionCodes.StaleMarketData);

        if (!market.Healthy)
        {
            return Reject(RiskRejectionCodes.ExchangeUnhealthy, "The exchange is not healthy.", passed);
        }

        passed.Add(RiskRejectionCodes.ExchangeUnhealthy);

        if (enter && !PassesEntryLimits(portfolio, riskProfile, passed, out var limitDecision))
        {
            return limitDecision!;
        }

        if (portfolio.ApprovedSignalIds.Contains(signal.SignalId))
        {
            return Reject(RiskRejectionCodes.DuplicateSignal, "The signal was already approved.", passed);
        }

        passed.Add(RiskRejectionCodes.DuplicateSignal);

        return enter
            ? SizeEntry(signal, portfolio, instrument, riskProfile, passed)
            : SizeExit(portfolio, instrument, passed);
    }

    private static bool PassesEntryLimits(
        PortfolioSnapshot portfolio,
        RiskProfile riskProfile,
        List<string> passed,
        out RiskDecision? decision)
    {
        var dayStart = portfolio.Equity - portfolio.DayPnl;
        if (dayStart <= 0m || portfolio.DayPnl <= -(dayStart * riskProfile.MaxDailyLossPercent / 100m))
        {
            decision = Reject(RiskRejectionCodes.DailyLossLimit, "Daily loss limit has been reached.", passed);
            return false;
        }

        passed.Add(RiskRejectionCodes.DailyLossLimit);

        if (portfolio.DrawdownFraction >= riskProfile.MaxDrawdownPercent / 100m)
        {
            decision = Reject(RiskRejectionCodes.MaxDrawdown, "Maximum drawdown has been reached.", passed);
            return false;
        }

        passed.Add(RiskRejectionCodes.MaxDrawdown);

        if (!portfolio.HasPosition && portfolio.OpenCount >= riskProfile.MaxOpenPositions)
        {
            decision = Reject(RiskRejectionCodes.MaxOpenPositions, "The maximum number of open positions has been reached.", passed);
            return false;
        }

        passed.Add(RiskRejectionCodes.MaxOpenPositions);

        if (portfolio.Equity > 0m && portfolio.GrossExposure / portfolio.Equity * 100m > riskProfile.MaxExposurePercent)
        {
            decision = Reject(RiskRejectionCodes.MaxExposure, "Gross exposure exceeds the maximum.", passed);
            return false;
        }

        passed.Add(RiskRejectionCodes.MaxExposure);
        decision = null;
        return true;
    }

    private static RiskDecision SizeEntry(
        TradeSignal signal,
        PortfolioSnapshot portfolio,
        Instrument instrument,
        RiskProfile riskProfile,
        List<string> passed)
    {
        if (portfolio.HasPosition && portfolio.PositionIsLosing)
        {
            return Reject(RiskRejectionCodes.AveragingDownForbidden, "Adding to a losing position is forbidden.", passed);
        }

        if (portfolio.HasPosition && !riskProfile.AveragingDownEnabled)
        {
            return Reject(RiskRejectionCodes.AveragingDownForbidden, "Averaging into an open position is disabled.", passed);
        }

        passed.Add(RiskRejectionCodes.AveragingDownForbidden);

        if (signal.StopLossPrice is not Price stop)
        {
            return Reject(RiskRejectionCodes.InvalidStop, "Enter long requires a stop price.", passed);
        }

        var sized = PositionSizer.TrySize(
            signal.ReferencePrice,
            stop,
            portfolio.Equity,
            portfolio.AvailableCash,
            riskProfile,
            instrument.QuantityStep,
            instrument.MinQuantity,
            instrument.MinNotional);
        if (!sized.IsApproved
            || sized.Quantity is not Quantity quantity
            || sized.EstimatedMaxLoss is not decimal estimatedMaxLoss)
        {
            return Reject(
                sized.RejectionCode ?? RiskRejectionCodes.InvalidStop,
                sized.Reason ?? "Position size was rejected.",
                passed);
        }

        passed.Add("PositionSizer");

        var slippedEntry = signal.ReferencePrice.Value * (1m + riskProfile.SlippageBps / 10000m);
        if (portfolio.Equity <= 0m
            || (portfolio.GrossExposure + quantity.Value * slippedEntry) / portfolio.Equity * 100m > riskProfile.MaxExposurePercent)
        {
            return Reject(RiskRejectionCodes.MaxExposure, "The order would exceed the maximum exposure.", passed);
        }

        return Approve(quantity, estimatedMaxLoss, passed);
    }

    private static RiskDecision SizeExit(PortfolioSnapshot portfolio, Instrument instrument, List<string> passed)
    {
        if (!portfolio.HasPosition || portfolio.OpenQuantity is not decimal openQuantity)
        {
            return Reject(RiskRejectionCodes.NoOpenPosition, "There is no open position to close.", passed);
        }

        passed.Add(RiskRejectionCodes.NoOpenPosition);
        var floored = DecimalRounding.FloorToStep(openQuantity, instrument.QuantityStep);
        if (floored < instrument.MinQuantity)
        {
            return Reject(RiskRejectionCodes.MinQuantity, "Exit quantity is below the minimum.", passed);
        }

        passed.Add(RiskRejectionCodes.MinQuantity);
        return Approve(new Quantity(floored), 0m, passed);
    }

    private static RiskDecision Approve(Quantity quantity, decimal estimatedMaxLoss, List<string> passed) =>
        RiskDecision.Approve(quantity, estimatedMaxLoss, passed.ToArray());

    private static RiskDecision Reject(string rejectionCode, string reason, List<string> passed) =>
        RiskDecision.Reject(rejectionCode, reason, passed.ToArray());
}
