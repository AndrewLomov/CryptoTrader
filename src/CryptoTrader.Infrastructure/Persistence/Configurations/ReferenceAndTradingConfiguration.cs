using CryptoTrader.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CryptoTrader.Infrastructure.Persistence.Configurations;

internal sealed class ExchangeRowConfiguration : IEntityTypeConfiguration<ExchangeRow>
{
    public void Configure(EntityTypeBuilder<ExchangeRow> builder)
    {
        builder.ToTable("exchanges");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
    }
}

internal sealed class TradingAccountRowConfiguration : IEntityTypeConfiguration<TradingAccountRow>
{
    public void Configure(EntityTypeBuilder<TradingAccountRow> builder)
    {
        builder.ToTable("trading_accounts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.ExchangeId).HasColumnName("exchange_id").IsRequired();
        builder.Property(x => x.DisplayName).HasColumnName("display_name").IsRequired();
        builder.Property(x => x.Mode).HasColumnName("mode");
        builder.Property(x => x.Status).HasColumnName("status");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.HasIndex(x => x.ExchangeId).HasDatabaseName("ix_trading_accounts_exchange_id");
    }
}

internal sealed class InstrumentRowConfiguration : IEntityTypeConfiguration<InstrumentRow>
{
    public void Configure(EntityTypeBuilder<InstrumentRow> builder)
    {
        builder.ToTable("instruments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.ExchangeId).HasColumnName("exchange_id").IsRequired();
        builder.Property(x => x.ExchangeSymbol).HasColumnName("exchange_symbol").IsRequired();
        builder.Property(x => x.BaseAsset).HasColumnName("base_asset").IsRequired();
        builder.Property(x => x.QuoteAsset).HasColumnName("quote_asset").IsRequired();
        builder.Property(x => x.InstrumentType).HasColumnName("instrument_type");
        builder.Property(x => x.PriceTick).HasColumnName("price_tick");
        builder.Property(x => x.QuantityStep).HasColumnName("quantity_step");
        builder.Property(x => x.MinQuantity).HasColumnName("min_quantity");
        builder.Property(x => x.MinNotional).HasColumnName("min_notional");
        builder.Property(x => x.ContractMultiplier).HasColumnName("contract_multiplier");
        builder.Property(x => x.IsTradingEnabled).HasColumnName("is_trading_enabled");
        builder.HasIndex(x => new { x.ExchangeId, x.ExchangeSymbol }).HasDatabaseName("ix_instruments_exchange_symbol");
    }
}

internal sealed class ApiCredentialsMetadataRowConfiguration : IEntityTypeConfiguration<ApiCredentialsMetadataRow>
{
    public void Configure(EntityTypeBuilder<ApiCredentialsMetadataRow> builder)
    {
        builder.ToTable(
            "api_credentials_metadata",
            table => table.HasComment("Key hint and permissions only. Do not add a secret, token, or api key column."));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.TradingAccountId).HasColumnName("trading_account_id");
        builder.Property(x => x.ExchangeId).HasColumnName("exchange_id");
        builder.Property(x => x.KeyHint).HasColumnName("key_hint").IsRequired();
        builder.Property(x => x.Permissions).HasColumnName("permissions").IsRequired();
        builder.HasIndex(x => x.TradingAccountId).HasDatabaseName("ix_api_credentials_metadata_account");
    }
}

internal sealed class OrderRowConfiguration : IEntityTypeConfiguration<OrderRow>
{
    public void Configure(EntityTypeBuilder<OrderRow> builder)
    {
        builder.ToTable("orders");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.ClientOrderId).HasColumnName("client_order_id").HasMaxLength(64).IsRequired();
        builder.Property(x => x.TradingAccountId).HasColumnName("trading_account_id");
        builder.Property(x => x.ExchangeId).HasColumnName("exchange_id").IsRequired();
        builder.Property(x => x.InstrumentId).HasColumnName("instrument_id").IsRequired();
        builder.Property(x => x.InstrumentType).HasColumnName("instrument_type");
        builder.Property(x => x.Side).HasColumnName("side");
        builder.Property(x => x.OrderType).HasColumnName("order_type");
        builder.Property(x => x.Quantity).HasColumnName("quantity");
        builder.Property(x => x.LimitPrice).HasColumnName("limit_price");
        builder.Property(x => x.ReduceOnly).HasColumnName("reduce_only");
        builder.Property(x => x.TradingMode).HasColumnName("trading_mode");
        builder.Property(x => x.Status).HasColumnName("status");
        builder.Property(x => x.FilledQuantity).HasColumnName("filled_quantity");
        builder.Property(x => x.FilledNotional).HasColumnName("filled_notional");
        builder.Property(x => x.AppliedExchangeTradeIdsJson).HasColumnName("applied_exchange_trade_ids").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.ExchangeOrderId).HasColumnName("exchange_order_id");
        builder.Property(x => x.RejectReason).HasColumnName("reject_reason");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.HasIndex(x => new { x.TradingAccountId, x.ClientOrderId })
            .IsUnique()
            .HasDatabaseName("ux_orders_account_client_order_id");
        builder.HasIndex(x => new { x.TradingAccountId, x.TradingMode }).HasDatabaseName("ix_orders_account_mode");
    }
}

internal sealed class ExecutionRowConfiguration : IEntityTypeConfiguration<ExecutionRow>
{
    public void Configure(EntityTypeBuilder<ExecutionRow> builder)
    {
        builder.ToTable("executions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.OrderId).HasColumnName("order_id");
        builder.Property(x => x.InstrumentId).HasColumnName("instrument_id").IsRequired();
        builder.Property(x => x.Side).HasColumnName("side");
        builder.Property(x => x.Quantity).HasColumnName("quantity");
        builder.Property(x => x.Price).HasColumnName("price");
        builder.Property(x => x.Fee).HasColumnName("fee");
        builder.Property(x => x.FeeAsset).HasColumnName("fee_asset").IsRequired();
        builder.Property(x => x.TimeUtc).HasColumnName("time_utc");
        builder.Property(x => x.ExchangeTradeId).HasColumnName("exchange_trade_id").IsRequired();
        builder.HasIndex(x => new { x.OrderId, x.ExchangeTradeId })
            .IsUnique()
            .HasDatabaseName("ux_executions_order_trade");
    }
}

internal sealed class PositionRowConfiguration : IEntityTypeConfiguration<PositionRow>
{
    public void Configure(EntityTypeBuilder<PositionRow> builder)
    {
        builder.ToTable("positions");
        builder.HasKey(x => new { x.TradingAccountId, x.InstrumentId });
        builder.Property(x => x.TradingAccountId).HasColumnName("trading_account_id");
        builder.Property(x => x.ExchangeId).HasColumnName("exchange_id").IsRequired();
        builder.Property(x => x.InstrumentId).HasColumnName("instrument_id");
        builder.Property(x => x.InstrumentType).HasColumnName("instrument_type");
        builder.Property(x => x.Side).HasColumnName("side");
        builder.Property(x => x.Quantity).HasColumnName("quantity");
        builder.Property(x => x.AveragePrice).HasColumnName("average_price");
        builder.Property(x => x.RealizedPnl).HasColumnName("realized_pnl");
        builder.Property(x => x.Fees).HasColumnName("fees");
        builder.Property(x => x.Funding).HasColumnName("funding");
        builder.Property(x => x.FeeAsset).HasColumnName("fee_asset");
        builder.Property(x => x.Leverage).HasColumnName("leverage");
        builder.Property(x => x.Margin).HasColumnName("margin");
        builder.Property(x => x.ContractMultiplier).HasColumnName("contract_multiplier");
        builder.Property(x => x.OpenedAtUtc).HasColumnName("opened_at_utc");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
    }
}

internal sealed class BalanceSnapshotRowConfiguration : IEntityTypeConfiguration<BalanceSnapshotRow>
{
    public void Configure(EntityTypeBuilder<BalanceSnapshotRow> builder)
    {
        builder.ToTable("balances_snapshots");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.TradingAccountId).HasColumnName("trading_account_id");
        builder.Property(x => x.Asset).HasColumnName("asset").IsRequired();
        builder.Property(x => x.Total).HasColumnName("total");
        builder.Property(x => x.Available).HasColumnName("available");
        builder.Property(x => x.CapturedAtUtc).HasColumnName("captured_at_utc");
        builder.HasIndex(x => new { x.TradingAccountId, x.CapturedAtUtc }).HasDatabaseName("ix_balances_snapshots_account_time");
    }
}

internal sealed class FundingPaymentRowConfiguration : IEntityTypeConfiguration<FundingPaymentRow>
{
    public void Configure(EntityTypeBuilder<FundingPaymentRow> builder)
    {
        builder.ToTable("funding_payments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        builder.Property(x => x.TradingAccountId).HasColumnName("trading_account_id");
        builder.Property(x => x.ExchangeId).HasColumnName("exchange_id").IsRequired();
        builder.Property(x => x.InstrumentId).HasColumnName("instrument_id").IsRequired();
        builder.Property(x => x.Amount).HasColumnName("amount");
        builder.Property(x => x.FundingTimeUtc).HasColumnName("funding_time_utc");
        builder.HasIndex(x => new { x.TradingAccountId, x.FundingTimeUtc }).HasDatabaseName("ix_funding_payments_account_time");
    }
}

internal sealed class ReconciliationRunRowConfiguration : IEntityTypeConfiguration<ReconciliationRunRow>
{
    public void Configure(EntityTypeBuilder<ReconciliationRunRow> builder)
    {
        builder.ToTable("reconciliation_runs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.TradingAccountId).HasColumnName("trading_account_id");
        builder.Property(x => x.StartedAtUtc).HasColumnName("started_at_utc");
        builder.Property(x => x.FinishedAtUtc).HasColumnName("finished_at_utc");
        builder.Property(x => x.Status).HasColumnName("status");
        builder.Property(x => x.Summary).HasColumnName("summary");
        builder.HasIndex(x => new { x.TradingAccountId, x.StartedAtUtc }).HasDatabaseName("ix_reconciliation_runs_account_started");
    }
}
