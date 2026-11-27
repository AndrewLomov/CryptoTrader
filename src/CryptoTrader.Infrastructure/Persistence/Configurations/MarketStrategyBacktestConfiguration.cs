using CryptoTrader.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CryptoTrader.Infrastructure.Persistence.Configurations;

internal sealed class CandleRowConfiguration : IEntityTypeConfiguration<CandleRow>
{
    public void Configure(EntityTypeBuilder<CandleRow> builder)
    {
        builder.ToTable("candles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        builder.Property(x => x.ExchangeId).HasColumnName("exchange_id").IsRequired();
        builder.Property(x => x.InstrumentId).HasColumnName("instrument_id").IsRequired();
        builder.Property(x => x.Timeframe).HasColumnName("timeframe").HasMaxLength(16).IsRequired();
        builder.Property(x => x.OpenTimeUtc).HasColumnName("open_time_utc");
        builder.Property(x => x.CloseTimeUtc).HasColumnName("close_time_utc");
        builder.Property(x => x.Open).HasColumnName("open");
        builder.Property(x => x.High).HasColumnName("high");
        builder.Property(x => x.Low).HasColumnName("low");
        builder.Property(x => x.Close).HasColumnName("close");
        builder.Property(x => x.Volume).HasColumnName("volume");
        builder.Property(x => x.IsClosed).HasColumnName("is_closed");
        builder.HasIndex(x => new { x.ExchangeId, x.InstrumentId, x.Timeframe, x.OpenTimeUtc })
            .IsUnique()
            .HasDatabaseName("ux_candles_identity");
    }
}

internal sealed class MarketTradeRowConfiguration : IEntityTypeConfiguration<MarketTradeRow>
{
    public void Configure(EntityTypeBuilder<MarketTradeRow> builder)
    {
        builder.ToTable("market_trades");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        builder.Property(x => x.ExchangeId).HasColumnName("exchange_id").IsRequired();
        builder.Property(x => x.InstrumentId).HasColumnName("instrument_id").IsRequired();
        builder.Property(x => x.ExchangeTradeId).HasColumnName("exchange_trade_id").IsRequired();
        builder.Property(x => x.Price).HasColumnName("price");
        builder.Property(x => x.Quantity).HasColumnName("quantity");
        builder.Property(x => x.Side).HasColumnName("side");
        builder.Property(x => x.TimeUtc).HasColumnName("time_utc");
        builder.HasIndex(x => new { x.ExchangeId, x.InstrumentId, x.ExchangeTradeId })
            .IsUnique()
            .HasDatabaseName("ux_market_trades_identity");
        builder.HasIndex(x => new { x.InstrumentId, x.TimeUtc }).HasDatabaseName("ix_market_trades_instrument_time");
    }
}

internal sealed class FundingRateRowConfiguration : IEntityTypeConfiguration<FundingRateRow>
{
    public void Configure(EntityTypeBuilder<FundingRateRow> builder)
    {
        builder.ToTable("funding_rates");
        builder.HasKey(x => new { x.ExchangeId, x.InstrumentId, x.FundingTimeUtc });
        builder.Property(x => x.ExchangeId).HasColumnName("exchange_id");
        builder.Property(x => x.InstrumentId).HasColumnName("instrument_id");
        builder.Property(x => x.FundingTimeUtc).HasColumnName("funding_time_utc");
        builder.Property(x => x.Rate).HasColumnName("rate");
        builder.Property(x => x.ReceivedAtUtc).HasColumnName("received_at_utc");
    }
}

internal sealed class OrderBookSnapshotRowConfiguration : IEntityTypeConfiguration<OrderBookSnapshotRow>
{
    public void Configure(EntityTypeBuilder<OrderBookSnapshotRow> builder)
    {
        builder.ToTable("order_book_snapshots");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        builder.Property(x => x.ExchangeId).HasColumnName("exchange_id").IsRequired();
        builder.Property(x => x.InstrumentId).HasColumnName("instrument_id").IsRequired();
        builder.Property(x => x.Sequence).HasColumnName("sequence");
        builder.Property(x => x.IsConsistent).HasColumnName("is_consistent");
        builder.Property(x => x.ExchangeTimeUtc).HasColumnName("exchange_time_utc");
        builder.Property(x => x.ReceivedAtUtc).HasColumnName("received_at_utc");
        builder.Property(x => x.BidsJson).HasColumnName("bids").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.AsksJson).HasColumnName("asks").HasColumnType("jsonb").IsRequired();
        builder.HasIndex(x => new { x.ExchangeId, x.InstrumentId, x.ExchangeTimeUtc })
            .HasDatabaseName("ix_order_book_snapshots_instrument_time");
    }
}

internal sealed class StrategyRowConfiguration : IEntityTypeConfiguration<StrategyRow>
{
    public void Configure(EntityTypeBuilder<StrategyRow> builder)
    {
        builder.ToTable("strategies");
        builder.HasKey(x => new { x.StrategyId, x.Version });
        builder.Property(x => x.StrategyId).HasColumnName("strategy_id");
        builder.Property(x => x.Version).HasColumnName("version");
        builder.Property(x => x.DisplayName).HasColumnName("display_name");
    }
}

internal sealed class StrategyInstanceRowConfiguration : IEntityTypeConfiguration<StrategyInstanceRow>
{
    public void Configure(EntityTypeBuilder<StrategyInstanceRow> builder)
    {
        builder.ToTable("strategy_instances");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.StrategyId).HasColumnName("strategy_id").IsRequired();
        builder.Property(x => x.StrategyVersion).HasColumnName("strategy_version").IsRequired();
        builder.Property(x => x.InstrumentId).HasColumnName("instrument_id").IsRequired();
        builder.Property(x => x.ParametersJson).HasColumnName("parameters_json").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.HasIndex(x => new { x.StrategyId, x.StrategyVersion }).HasDatabaseName("ix_strategy_instances_strategy");
    }
}

internal sealed class StrategyConfigurationRowConfiguration : IEntityTypeConfiguration<StrategyConfigurationRow>
{
    public void Configure(EntityTypeBuilder<StrategyConfigurationRow> builder)
    {
        builder.ToTable("strategy_configurations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.StrategyId).HasColumnName("strategy_id").IsRequired();
        builder.Property(x => x.StrategyVersion).HasColumnName("strategy_version").IsRequired();
        builder.Property(x => x.ParametersJson).HasColumnName("parameters_json").IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.HasIndex(x => new { x.StrategyId, x.StrategyVersion }).HasDatabaseName("ix_strategy_configurations_strategy");
    }
}

internal sealed class TradeSignalRowConfiguration : IEntityTypeConfiguration<TradeSignalRow>
{
    public void Configure(EntityTypeBuilder<TradeSignalRow> builder)
    {
        builder.ToTable("trade_signals");
        builder.HasKey(x => x.SignalId);
        builder.Property(x => x.SignalId).HasColumnName("signal_id").ValueGeneratedNever();
        builder.Property(x => x.StrategyInstanceId).HasColumnName("strategy_instance_id");
        builder.Property(x => x.StrategyId).HasColumnName("strategy_id").IsRequired();
        builder.Property(x => x.StrategyVersion).HasColumnName("strategy_version").IsRequired();
        builder.Property(x => x.InstrumentId).HasColumnName("instrument_id").IsRequired();
        builder.Property(x => x.Action).HasColumnName("action");
        builder.Property(x => x.OccurredAtUtc).HasColumnName("occurred_at_utc");
        builder.Property(x => x.ReferencePrice).HasColumnName("reference_price");
        builder.Property(x => x.StopLossPrice).HasColumnName("stop_loss_price");
        builder.Property(x => x.TakeProfitPrice).HasColumnName("take_profit_price");
        builder.Property(x => x.Atr).HasColumnName("atr");
        builder.Property(x => x.Explanation).HasColumnName("explanation").IsRequired();
        builder.HasIndex(x => new { x.StrategyId, x.InstrumentId, x.OccurredAtUtc })
            .HasDatabaseName("ix_trade_signals_strategy_instrument_time");
    }
}

internal sealed class RiskDecisionRowConfiguration : IEntityTypeConfiguration<RiskDecisionRow>
{
    public void Configure(EntityTypeBuilder<RiskDecisionRow> builder)
    {
        builder.ToTable("risk_decisions");
        builder.HasKey(x => x.SignalId);
        builder.Property(x => x.SignalId).HasColumnName("signal_id").ValueGeneratedNever();
        builder.Property(x => x.StrategyInstanceId).HasColumnName("strategy_instance_id");
        builder.Property(x => x.Approved).HasColumnName("approved");
        builder.Property(x => x.ApprovedQuantity).HasColumnName("approved_quantity");
        builder.Property(x => x.EstimatedMaxLoss).HasColumnName("estimated_max_loss");
        builder.Property(x => x.AppliedConstraintsJson).HasColumnName("applied_constraints").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.RejectionCode).HasColumnName("rejection_code");
        builder.Property(x => x.Reason).HasColumnName("reason");
        builder.Property(x => x.OccurredAtUtc).HasColumnName("occurred_at_utc");
    }
}

internal sealed class RiskEventRowConfiguration : IEntityTypeConfiguration<RiskEventRow>
{
    public void Configure(EntityTypeBuilder<RiskEventRow> builder)
    {
        builder.ToTable("risk_events");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.EventType).HasColumnName("event_type").IsRequired();
        builder.Property(x => x.OccurredAtUtc).HasColumnName("occurred_at_utc");
        builder.Property(x => x.CorrelationId).HasColumnName("correlation_id");
        builder.Property(x => x.CausationId).HasColumnName("causation_id");
        builder.Property(x => x.PayloadJson).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.HasIndex(x => x.OccurredAtUtc).HasDatabaseName("ix_risk_events_occurred_at");
    }
}

internal sealed class BacktestRunRowConfiguration : IEntityTypeConfiguration<BacktestRunRow>
{
    public void Configure(EntityTypeBuilder<BacktestRunRow> builder)
    {
        builder.ToTable("backtest_runs");
        builder.HasKey(x => x.RunId);
        builder.Property(x => x.RunId).HasColumnName("run_id").ValueGeneratedNever();
        builder.Property(x => x.StrategyId).HasColumnName("strategy_id").IsRequired();
        builder.Property(x => x.StrategyVersion).HasColumnName("strategy_version").IsRequired();
        builder.Property(x => x.EngineVersion).HasColumnName("engine_version").IsRequired();
        builder.Property(x => x.ParametersJson).HasColumnName("parameters_json").IsRequired();
        builder.Property(x => x.FromUtc).HasColumnName("from_utc");
        builder.Property(x => x.ToUtc).HasColumnName("to_utc");
        builder.Property(x => x.AssumptionsEngineVersion).HasColumnName("assumptions_engine_version").IsRequired();
        builder.Property(x => x.FundingModeled).HasColumnName("funding_modeled");
        builder.Property(x => x.PartialFillsModeled).HasColumnName("partial_fills_modeled");
        builder.Property(x => x.FillPriceModel).HasColumnName("fill_price_model").IsRequired();
        builder.Property(x => x.StopAndTakeProfitConflict).HasColumnName("stop_and_take_profit_conflict").IsRequired();
        builder.Property(x => x.AssumptionsNotes).HasColumnName("assumptions_notes").IsRequired();
        builder.Property(x => x.EquityCurveJson).HasColumnName("equity_curve").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.HasOne(x => x.Metrics)
            .WithOne()
            .HasForeignKey<BacktestMetricsRow>(x => x.RunId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Trades)
            .WithOne()
            .HasForeignKey(x => x.RunId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class BacktestMetricsRowConfiguration : IEntityTypeConfiguration<BacktestMetricsRow>
{
    public void Configure(EntityTypeBuilder<BacktestMetricsRow> builder)
    {
        builder.ToTable("backtest_metrics");
        builder.HasKey(x => x.RunId);
        builder.Property(x => x.RunId).HasColumnName("run_id").ValueGeneratedNever();
        builder.Property(x => x.InitialEquity).HasColumnName("initial_equity");
        builder.Property(x => x.FinalEquity).HasColumnName("final_equity");
        builder.Property(x => x.NetProfit).HasColumnName("net_profit");
        builder.Property(x => x.RoiFraction).HasColumnName("roi_fraction");
        builder.Property(x => x.MaxDrawdownFraction).HasColumnName("max_drawdown_fraction");
        builder.Property(x => x.Sharpe).HasColumnName("sharpe");
        builder.Property(x => x.Sortino).HasColumnName("sortino");
        builder.Property(x => x.ProfitFactor).HasColumnName("profit_factor");
        builder.Property(x => x.WinRateFraction).HasColumnName("win_rate_fraction");
        builder.Property(x => x.Expectancy).HasColumnName("expectancy");
        builder.Property(x => x.TradeCount).HasColumnName("trade_count");
        builder.Property(x => x.TotalFees).HasColumnName("total_fees");
        builder.Property(x => x.TotalSlippage).HasColumnName("total_slippage");
        builder.Property(x => x.AverageHoldingTicks).HasColumnName("average_holding_ticks");
    }
}

internal sealed class BacktestTradeRowConfiguration : IEntityTypeConfiguration<BacktestTradeRow>
{
    public void Configure(EntityTypeBuilder<BacktestTradeRow> builder)
    {
        builder.ToTable("backtest_trades");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        builder.Property(x => x.RunId).HasColumnName("run_id");
        builder.Property(x => x.TradeNumber).HasColumnName("trade_number");
        builder.Property(x => x.InstrumentId).HasColumnName("instrument_id").IsRequired();
        builder.Property(x => x.EntryTimeUtc).HasColumnName("entry_time_utc");
        builder.Property(x => x.ExitTimeUtc).HasColumnName("exit_time_utc");
        builder.Property(x => x.EntryPrice).HasColumnName("entry_price");
        builder.Property(x => x.ExitPrice).HasColumnName("exit_price");
        builder.Property(x => x.Quantity).HasColumnName("quantity");
        builder.Property(x => x.Fees).HasColumnName("fees");
        builder.Property(x => x.SlippageCost).HasColumnName("slippage_cost");
        builder.Property(x => x.NetPnl).HasColumnName("net_pnl");
        builder.Property(x => x.ExitReason).HasColumnName("exit_reason");
        builder.HasIndex(x => new { x.RunId, x.TradeNumber })
            .IsUnique()
            .HasDatabaseName("ux_backtest_trades_run_number");
    }
}

internal sealed class PaperAccountRowConfiguration : IEntityTypeConfiguration<PaperAccountRow>
{
    public void Configure(EntityTypeBuilder<PaperAccountRow> builder)
    {
        builder.ToTable("paper_accounts");
        builder.HasKey(x => x.AccountId);
        builder.Property(x => x.AccountId).HasColumnName("account_id").ValueGeneratedNever();
        builder.Property(x => x.ExchangeId).HasColumnName("exchange_id").IsRequired();
        builder.Property(x => x.QuoteAsset).HasColumnName("quote_asset").IsRequired();
        builder.Property(x => x.Cash).HasColumnName("cash");
        builder.Property(x => x.PeakEquity).HasColumnName("peak_equity");
        builder.Property(x => x.DayStartEquity).HasColumnName("day_start_equity");
        builder.Property(x => x.LastEquity).HasColumnName("last_equity");
        builder.Property(x => x.DayUtc).HasColumnName("day_utc");
        builder.Property(x => x.EmergencyStop).HasColumnName("emergency_stop");
        builder.Property(x => x.PositionsJson).HasColumnName("positions").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.MarksJson).HasColumnName("marks").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.AppliedExchangeTradeIdsJson).HasColumnName("applied_exchange_trade_ids").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
    }
}
