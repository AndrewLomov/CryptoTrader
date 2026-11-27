namespace CryptoTrader.Infrastructure.Persistence.Entities;

public sealed class ExchangeRow
{
    public required string Id { get; set; }
}

public sealed class TradingAccountRow
{
    public Guid Id { get; set; }

    public required string ExchangeId { get; set; }

    public required string DisplayName { get; set; }

    public int Mode { get; set; }

    public int Status { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class InstrumentRow
{
    public required string Id { get; set; }

    public required string ExchangeId { get; set; }

    public required string ExchangeSymbol { get; set; }

    public required string BaseAsset { get; set; }

    public required string QuoteAsset { get; set; }

    public int InstrumentType { get; set; }

    public decimal PriceTick { get; set; }

    public decimal QuantityStep { get; set; }

    public decimal MinQuantity { get; set; }

    public decimal? MinNotional { get; set; }

    public decimal? ContractMultiplier { get; set; }

    public decimal? ContractValue { get; set; }

    public string? ContractValueCurrency { get; set; }

    public string? SettlementCurrency { get; set; }

    public string? ContractKind { get; set; }

    public bool IsTradingEnabled { get; set; }
}

public sealed class ApiCredentialsMetadataRow
{
    public Guid Id { get; set; }

    public Guid? TradingAccountId { get; set; }

    public string? ExchangeId { get; set; }

    public required string KeyHint { get; set; }

    public required string Permissions { get; set; }
}

public sealed class CandleRow
{
    public long Id { get; set; }

    public required string ExchangeId { get; set; }

    public required string InstrumentId { get; set; }

    public required string Timeframe { get; set; }

    public DateTimeOffset OpenTimeUtc { get; set; }

    public DateTimeOffset CloseTimeUtc { get; set; }

    public decimal Open { get; set; }

    public decimal High { get; set; }

    public decimal Low { get; set; }

    public decimal Close { get; set; }

    public decimal Volume { get; set; }

    public int VolumeUnit { get; set; } = 1;

    public decimal? QuoteVolume { get; set; }

    public decimal? BaseVolume { get; set; }

    public bool IsClosed { get; set; }
}

public sealed class CandleConsumerCheckpointRow
{
    public required string ExchangeId { get; set; }

    public required string InstrumentId { get; set; }

    public required string Timeframe { get; set; }

    public DateTimeOffset OpenTimeUtc { get; set; }

    public required string Consumer { get; set; }

    public int State { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class MarketTradeRow
{
    public long Id { get; set; }

    public required string ExchangeId { get; set; }

    public required string InstrumentId { get; set; }

    public required string ExchangeTradeId { get; set; }

    public decimal Price { get; set; }

    public decimal Quantity { get; set; }

    public int? Side { get; set; }

    public DateTimeOffset TimeUtc { get; set; }
}

public sealed class FundingRateRow
{
    public required string ExchangeId { get; set; }

    public required string InstrumentId { get; set; }

    public DateTimeOffset FundingTimeUtc { get; set; }

    public decimal Rate { get; set; }

    public DateTimeOffset ReceivedAtUtc { get; set; }
}

public sealed class OrderBookSnapshotRow
{
    public long Id { get; set; }

    public required string ExchangeId { get; set; }

    public required string InstrumentId { get; set; }

    public long? Sequence { get; set; }

    public bool IsConsistent { get; set; }

    public DateTimeOffset ExchangeTimeUtc { get; set; }

    public DateTimeOffset ReceivedAtUtc { get; set; }

    public required string BidsJson { get; set; }

    public required string AsksJson { get; set; }
}

public sealed class OrderRow
{
    public Guid Id { get; set; }

    public required string ClientOrderId { get; set; }

    public Guid TradingAccountId { get; set; }

    public required string ExchangeId { get; set; }

    public required string InstrumentId { get; set; }

    public int InstrumentType { get; set; }

    public int Side { get; set; }

    public int OrderType { get; set; }

    public decimal Quantity { get; set; }

    public decimal? LimitPrice { get; set; }

    public bool ReduceOnly { get; set; }

    public int TradingMode { get; set; }

    public int Status { get; set; }

    public decimal FilledQuantity { get; set; }

    public decimal FilledNotional { get; set; }

    public required string AppliedExchangeTradeIdsJson { get; set; }

    public string? ExchangeOrderId { get; set; }

    public string? RejectReason { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class ExecutionRow
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public required string InstrumentId { get; set; }

    public int Side { get; set; }

    public decimal Quantity { get; set; }

    public decimal Price { get; set; }

    public decimal Fee { get; set; }

    public required string FeeAsset { get; set; }

    public DateTimeOffset TimeUtc { get; set; }

    public required string ExchangeTradeId { get; set; }
}

public sealed class PositionRow
{
    public Guid TradingAccountId { get; set; }

    public required string ExchangeId { get; set; }

    public required string InstrumentId { get; set; }

    public int InstrumentType { get; set; }

    public int Side { get; set; }

    public decimal Quantity { get; set; }

    public decimal AveragePrice { get; set; }

    public decimal RealizedPnl { get; set; }

    public decimal Fees { get; set; }

    public decimal Funding { get; set; }

    public string? FeeAsset { get; set; }

    public decimal Leverage { get; set; }

    public decimal? Margin { get; set; }

    public decimal? ContractMultiplier { get; set; }

    public DateTimeOffset OpenedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class BalanceSnapshotRow
{
    public Guid Id { get; set; }

    public Guid TradingAccountId { get; set; }

    public required string Asset { get; set; }

    public decimal Total { get; set; }

    public decimal Available { get; set; }

    public DateTimeOffset CapturedAtUtc { get; set; }
}

public sealed class FundingPaymentRow
{
    public long Id { get; set; }

    public Guid TradingAccountId { get; set; }

    public required string ExchangeId { get; set; }

    public required string InstrumentId { get; set; }

    public decimal Amount { get; set; }

    public DateTimeOffset FundingTimeUtc { get; set; }
}

public sealed class ReconciliationRunRow
{
    public Guid Id { get; set; }

    public Guid TradingAccountId { get; set; }

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? FinishedAtUtc { get; set; }

    public int Status { get; set; }

    public string? Summary { get; set; }
}

public sealed class StrategyRow
{
    public required string StrategyId { get; set; }

    public required string Version { get; set; }

    public string? DisplayName { get; set; }
}

public sealed class StrategyInstanceRow
{
    public Guid Id { get; set; }

    public required string StrategyId { get; set; }

    public required string StrategyVersion { get; set; }

    public required string InstrumentId { get; set; }

    public required string ParametersJson { get; set; }

    public int Status { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class StrategyConfigurationRow
{
    public Guid Id { get; set; }

    public required string StrategyId { get; set; }

    public required string StrategyVersion { get; set; }

    public required string ParametersJson { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class TradeSignalRow
{
    public Guid SignalId { get; set; }

    public Guid StrategyInstanceId { get; set; }

    public required string StrategyId { get; set; }

    public required string StrategyVersion { get; set; }

    public required string InstrumentId { get; set; }

    public int Action { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    public decimal ReferencePrice { get; set; }

    public decimal? StopLossPrice { get; set; }

    public decimal? TakeProfitPrice { get; set; }

    public decimal? Atr { get; set; }

    public required string Explanation { get; set; }
}

public sealed class RiskDecisionRow
{
    public Guid SignalId { get; set; }

    public Guid StrategyInstanceId { get; set; }

    public bool Approved { get; set; }

    public decimal? ApprovedQuantity { get; set; }

    public decimal? EstimatedMaxLoss { get; set; }

    public required string AppliedConstraintsJson { get; set; }

    public string? RejectionCode { get; set; }

    public string? Reason { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }
}

public sealed class RiskEventRow
{
    public Guid Id { get; set; }

    public required string EventType { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    public Guid? CorrelationId { get; set; }

    public Guid? CausationId { get; set; }

    public required string PayloadJson { get; set; }
}

public sealed class BacktestRunRow
{
    public Guid RunId { get; set; }

    public required string StrategyId { get; set; }

    public required string StrategyVersion { get; set; }

    public required string EngineVersion { get; set; }

    public required string ParametersJson { get; set; }

    public DateTimeOffset FromUtc { get; set; }

    public DateTimeOffset ToUtc { get; set; }

    public required string AssumptionsEngineVersion { get; set; }

    public bool FundingModeled { get; set; }

    public bool PartialFillsModeled { get; set; }

    public required string FillPriceModel { get; set; }

    public required string StopAndTakeProfitConflict { get; set; }

    public required string AssumptionsNotes { get; set; }

    public required string EquityCurveJson { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public BacktestMetricsRow? Metrics { get; set; }

    public List<BacktestTradeRow> Trades { get; set; } = [];
}

public sealed class BacktestMetricsRow
{
    public Guid RunId { get; set; }

    public decimal InitialEquity { get; set; }

    public decimal FinalEquity { get; set; }

    public decimal NetProfit { get; set; }

    public decimal RoiFraction { get; set; }

    public decimal MaxDrawdownFraction { get; set; }

    public decimal? Sharpe { get; set; }

    public decimal? Sortino { get; set; }

    public decimal? ProfitFactor { get; set; }

    public decimal WinRateFraction { get; set; }

    public decimal Expectancy { get; set; }

    public int TradeCount { get; set; }

    public decimal TotalFees { get; set; }

    public decimal TotalSlippage { get; set; }

    public long AverageHoldingTicks { get; set; }
}

public sealed class BacktestTradeRow
{
    public long Id { get; set; }

    public Guid RunId { get; set; }

    public int TradeNumber { get; set; }

    public required string InstrumentId { get; set; }

    public DateTimeOffset EntryTimeUtc { get; set; }

    public DateTimeOffset ExitTimeUtc { get; set; }

    public decimal EntryPrice { get; set; }

    public decimal ExitPrice { get; set; }

    public decimal Quantity { get; set; }

    public decimal Fees { get; set; }

    public decimal SlippageCost { get; set; }

    public decimal NetPnl { get; set; }

    public int ExitReason { get; set; }
}

public sealed class PaperAccountRow
{
    public Guid AccountId { get; set; }

    public required string ExchangeId { get; set; }

    public required string QuoteAsset { get; set; }

    public decimal Cash { get; set; }

    public decimal PeakEquity { get; set; }

    public decimal DayStartEquity { get; set; }

    public decimal LastEquity { get; set; }

    public DateOnly DayUtc { get; set; }

    public bool EmergencyStop { get; set; }

    public required string PositionsJson { get; set; }

    public required string MarksJson { get; set; }

    public required string AppliedExchangeTradeIdsJson { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
