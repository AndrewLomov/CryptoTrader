using CryptoTrader.Infrastructure.Persistence.Configurations;
using CryptoTrader.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CryptoTrader.Infrastructure.Persistence;

public sealed class CryptoTraderDbContext : DbContext
{
    public CryptoTraderDbContext(DbContextOptions<CryptoTraderDbContext> options)
        : base(options)
    {
    }

    public DbSet<ExchangeRow> Exchanges => Set<ExchangeRow>();

    public DbSet<TradingAccountRow> TradingAccounts => Set<TradingAccountRow>();

    public DbSet<InstrumentRow> Instruments => Set<InstrumentRow>();

    public DbSet<ApiCredentialsMetadataRow> ApiCredentialsMetadata => Set<ApiCredentialsMetadataRow>();

    public DbSet<CandleRow> Candles => Set<CandleRow>();

    public DbSet<MarketTradeRow> MarketTrades => Set<MarketTradeRow>();

    public DbSet<FundingRateRow> FundingRates => Set<FundingRateRow>();

    public DbSet<OrderBookSnapshotRow> OrderBookSnapshots => Set<OrderBookSnapshotRow>();

    public DbSet<OrderRow> Orders => Set<OrderRow>();

    public DbSet<ExecutionRow> Executions => Set<ExecutionRow>();

    public DbSet<PositionRow> Positions => Set<PositionRow>();

    public DbSet<BalanceSnapshotRow> BalanceSnapshots => Set<BalanceSnapshotRow>();

    public DbSet<FundingPaymentRow> FundingPayments => Set<FundingPaymentRow>();

    public DbSet<ReconciliationRunRow> ReconciliationRuns => Set<ReconciliationRunRow>();

    public DbSet<StrategyRow> Strategies => Set<StrategyRow>();

    public DbSet<StrategyInstanceRow> StrategyInstances => Set<StrategyInstanceRow>();

    public DbSet<StrategyConfigurationRow> StrategyConfigurations => Set<StrategyConfigurationRow>();

    public DbSet<TradeSignalRow> TradeSignals => Set<TradeSignalRow>();

    public DbSet<RiskDecisionRow> RiskDecisions => Set<RiskDecisionRow>();

    public DbSet<RiskEventRow> RiskEvents => Set<RiskEventRow>();

    public DbSet<BacktestRunRow> BacktestRuns => Set<BacktestRunRow>();

    public DbSet<BacktestTradeRow> BacktestTrades => Set<BacktestTradeRow>();

    public DbSet<BacktestMetricsRow> BacktestMetrics => Set<BacktestMetricsRow>();

    public DbSet<PaperAccountRow> PaperAccounts => Set<PaperAccountRow>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HaveColumnType("numeric");
        configurationBuilder.Properties<decimal?>().HaveColumnType("numeric");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ExchangeRowConfiguration());
        modelBuilder.ApplyConfiguration(new TradingAccountRowConfiguration());
        modelBuilder.ApplyConfiguration(new InstrumentRowConfiguration());
        modelBuilder.ApplyConfiguration(new ApiCredentialsMetadataRowConfiguration());
        modelBuilder.ApplyConfiguration(new CandleRowConfiguration());
        modelBuilder.ApplyConfiguration(new MarketTradeRowConfiguration());
        modelBuilder.ApplyConfiguration(new FundingRateRowConfiguration());
        modelBuilder.ApplyConfiguration(new OrderBookSnapshotRowConfiguration());
        modelBuilder.ApplyConfiguration(new OrderRowConfiguration());
        modelBuilder.ApplyConfiguration(new ExecutionRowConfiguration());
        modelBuilder.ApplyConfiguration(new PositionRowConfiguration());
        modelBuilder.ApplyConfiguration(new BalanceSnapshotRowConfiguration());
        modelBuilder.ApplyConfiguration(new FundingPaymentRowConfiguration());
        modelBuilder.ApplyConfiguration(new ReconciliationRunRowConfiguration());
        modelBuilder.ApplyConfiguration(new StrategyRowConfiguration());
        modelBuilder.ApplyConfiguration(new StrategyInstanceRowConfiguration());
        modelBuilder.ApplyConfiguration(new StrategyConfigurationRowConfiguration());
        modelBuilder.ApplyConfiguration(new TradeSignalRowConfiguration());
        modelBuilder.ApplyConfiguration(new RiskDecisionRowConfiguration());
        modelBuilder.ApplyConfiguration(new RiskEventRowConfiguration());
        modelBuilder.ApplyConfiguration(new BacktestRunRowConfiguration());
        modelBuilder.ApplyConfiguration(new BacktestMetricsRowConfiguration());
        modelBuilder.ApplyConfiguration(new BacktestTradeRowConfiguration());
        modelBuilder.ApplyConfiguration(new PaperAccountRowConfiguration());
    }
}
