namespace CryptoTrader.Contracts.Responses;

public sealed record CreateBacktestResponse(Guid RunId);

public sealed record BacktestTradeResponse(
    int Number,
    string Instrument,
    DateTimeOffset EntryTimeUtc,
    DateTimeOffset ExitTimeUtc,
    decimal EntryPrice,
    decimal ExitPrice,
    decimal Quantity,
    decimal Fees,
    decimal SlippageCost,
    decimal NetPnl,
    string ExitReason);

public sealed record BacktestMetricsResponse(
    decimal InitialEquity,
    decimal FinalEquity,
    decimal NetProfit,
    decimal RoiFraction,
    decimal MaxDrawdownFraction,
    decimal? Sharpe,
    decimal? Sortino,
    decimal? ProfitFactor,
    decimal WinRateFraction,
    decimal Expectancy,
    int TradeCount,
    decimal TotalFees,
    decimal TotalSlippage,
    TimeSpan AverageHoldingTime);

public sealed record EquityPointResponse(DateTimeOffset TimeUtc, decimal Equity);

public sealed record BacktestReportResponse(
    Guid RunId,
    string StrategyId,
    string StrategyVersion,
    string EngineVersion,
    string ParametersJson,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    BacktestMetricsResponse Metrics,
    bool FundingModeled,
    string FillPriceModel,
    string StopAndTakeProfitConflict,
    IReadOnlyList<BacktestTradeResponse> Trades,
    IReadOnlyList<EquityPointResponse> EquityCurve);
