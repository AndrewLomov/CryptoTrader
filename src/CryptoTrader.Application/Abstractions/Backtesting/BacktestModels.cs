using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.Instruments;
using CryptoTrader.Domain.MarketData;
using CryptoTrader.Domain.Risk;

namespace CryptoTrader.Application.Abstractions.Backtesting;

public static class BacktestEngineInfo
{
    public const string Version = "0.1.0";
}

public enum BacktestExitReason
{
    StopLoss = 1,
    TakeProfit = 2,
    TrendBreak = 3,
    EndOfData = 4,
}

public sealed record BacktestRequest(
    string StrategyId,
    string StrategyVersion,
    string ParametersJson,
    ExchangeId Exchange,
    InstrumentId Instrument,
    Instrument InstrumentSpec,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    decimal InitialEquity,
    decimal FeeRate,
    decimal SlippageBps,
    RiskProfile RiskProfile,
    IReadOnlyList<Candle> WorkingCandles,
    IReadOnlyList<Candle> HigherCandles,
    bool ExitOnTrendBreak);

public sealed record BacktestTrade(
    int Number,
    InstrumentId Instrument,
    DateTimeOffset EntryTimeUtc,
    DateTimeOffset ExitTimeUtc,
    decimal EntryPrice,
    decimal ExitPrice,
    decimal Quantity,
    decimal Fees,
    decimal SlippageCost,
    decimal NetPnl,
    BacktestExitReason ExitReason);

public sealed record BacktestMetrics(
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

public sealed record BacktestAssumptions(
    string EngineVersion,
    bool FundingModeled,
    bool PartialFillsModeled,
    string FillPriceModel,
    string StopAndTakeProfitConflict,
    string Notes);

public sealed record EquityPoint(DateTimeOffset TimeUtc, decimal Equity);

public sealed record BacktestReport(
    Guid RunId,
    string StrategyId,
    string StrategyVersion,
    string EngineVersion,
    string ParametersJson,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    BacktestMetrics Metrics,
    BacktestAssumptions Assumptions,
    IReadOnlyList<BacktestTrade> Trades,
    IReadOnlyList<EquityPoint> EquityCurve);

public sealed record BacktestRunRecord(
    Guid RunId,
    BacktestReport Report,
    DateTimeOffset CreatedAtUtc);
