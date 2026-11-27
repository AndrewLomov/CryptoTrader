using CryptoTrader.Domain.Common;

namespace CryptoTrader.Domain.Risk;

public sealed record RiskProfile
{
    private RiskProfile(
        decimal riskPerTradePercent,
        decimal maxDailyLossPercent,
        int maxOpenPositions,
        decimal maxExposurePercent,
        decimal feeRate,
        decimal slippageBps,
        bool averagingDownEnabled,
        decimal maxDrawdownPercent,
        bool allowFuturesLive,
        bool allowCrossMargin)
    {
        RiskPerTradePercent = riskPerTradePercent;
        MaxDailyLossPercent = maxDailyLossPercent;
        MaxOpenPositions = maxOpenPositions;
        MaxExposurePercent = maxExposurePercent;
        FeeRate = feeRate;
        SlippageBps = slippageBps;
        AveragingDownEnabled = averagingDownEnabled;
        MaxDrawdownPercent = maxDrawdownPercent;
        AllowFuturesLive = allowFuturesLive;
        AllowCrossMargin = allowCrossMargin;
    }

    public static RiskProfile Default { get; } = Create(
        0.5m,
        1m,
        2,
        100m,
        0.001m,
        5m,
        false,
        10m,
        false,
        false);

    public decimal RiskPerTradePercent { get; }

    public decimal MaxDailyLossPercent { get; }

    public int MaxOpenPositions { get; }

    public decimal MaxExposurePercent { get; }

    public decimal FeeRate { get; }

    public decimal SlippageBps { get; }

    public bool AveragingDownEnabled { get; }

    public decimal MaxDrawdownPercent { get; }

    public bool AllowFuturesLive { get; }

    public bool AllowCrossMargin { get; }

    public static RiskProfile Create(
        decimal riskPerTradePercent,
        decimal maxDailyLossPercent,
        int maxOpenPositions,
        decimal maxExposurePercent,
        decimal feeRate,
        decimal slippageBps,
        bool averagingDownEnabled,
        decimal maxDrawdownPercent,
        bool allowFuturesLive,
        bool allowCrossMargin)
    {
        if (allowFuturesLive)
        {
            throw new DomainException("Futures live trading is disabled in CryptoTrader 0.1.");
        }

        if (allowCrossMargin)
        {
            throw new DomainException("Cross margin is disabled in CryptoTrader 0.1.");
        }

        Guard.Percent(riskPerTradePercent, nameof(riskPerTradePercent));
        Guard.Percent(maxDailyLossPercent, nameof(maxDailyLossPercent));
        Guard.Percent(maxExposurePercent, nameof(maxExposurePercent));
        Guard.Percent(maxDrawdownPercent, nameof(maxDrawdownPercent));
        if (maxOpenPositions < 1)
        {
            throw new DomainException("Max open positions must be at least 1.");
        }

        if (feeRate < 0 || feeRate >= 1)
        {
            throw new DomainException("Fee rate must be greater than or equal to 0 and less than 1.");
        }

        Guard.NotNegative(slippageBps, nameof(slippageBps));
        return new RiskProfile(
            riskPerTradePercent,
            maxDailyLossPercent,
            maxOpenPositions,
            maxExposurePercent,
            feeRate,
            slippageBps,
            averagingDownEnabled,
            maxDrawdownPercent,
            allowFuturesLive,
            allowCrossMargin);
    }
}
