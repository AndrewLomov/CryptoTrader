using CryptoTrader.Domain.Instruments;
using CryptoTrader.Domain.Risk;
using CryptoTrader.Domain.Strategies;

namespace CryptoTrader.Application.Abstractions.Risk;

/// <summary>
/// Decides whether a signal may become an order.
/// </summary>
/// <remarks>
/// Check order: EmergencyStop, InstrumentDisabled, FuturesLiveForbidden, StaleMarketData,
/// ExchangeUnhealthy, DailyLossLimit, MaxDrawdown, MaxOpenPositions, MaxExposure,
/// DuplicateSignal, existing position / AveragingDownForbidden or NoOpenPosition, then PositionSizer
/// (InvalidStop, MinQuantity, MinNotional, InsufficientBalance). After sizing, reject with
/// MaxExposure when the new notional does not fit the remaining exposure room.
/// Live versus paper is not a method argument. Read ExecutionOptions from the composition root.
/// FuturesLiveForbidden applies to a linear perpetual only when execution mode is Live.
/// AllowFuturesLive false does not by itself reject paper or backtest futures.
/// PortfolioSnapshot.HasPosition and PositionIsLosing refer to the signal instrument.
/// OpenCount, equity, exposure, and emergency stop are account-level.
/// DrawdownFraction and DayPnl come from the same ledger observation.
/// Compare drawdown with MaxDrawdownPercent / 100. Daily loss uses day-start equity
/// reconstructed as Equity - DayPnl. Percent configuration uses 0.5 = 0.5%.
/// Emergency stop, stale data, and an unhealthy exchange block entries and exits.
/// A disabled instrument blocks entries; exits remain allowed.
/// </remarks>
public interface IRiskManager
{
    ValueTask<RiskDecision> EvaluateAsync(
        TradeSignal signal,
        PortfolioSnapshot portfolio,
        MarketSnapshot market,
        Instrument instrument,
        RiskProfile riskProfile,
        CancellationToken cancellationToken);
}
