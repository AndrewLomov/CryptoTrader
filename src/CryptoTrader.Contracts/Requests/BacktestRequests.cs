namespace CryptoTrader.Contracts.Requests;

public sealed record RiskSettingsRequest(
    decimal RiskPerTradePercent,
    decimal MaxDailyLossPercent,
    int MaxOpenPositions,
    decimal MaxExposurePercent,
    decimal FeeRate,
    decimal SlippageBps,
    bool AveragingDownEnabled,
    decimal MaxDrawdownPercent);

public sealed record CreateBacktestRequest(
    string StrategyId,
    string StrategyVersion,
    string ParametersJson,
    string Exchange,
    string Instrument,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    decimal InitialEquity,
    decimal FeeRate,
    decimal SlippageBps,
    bool ExitOnTrendBreak,
    RiskSettingsRequest Risk);
