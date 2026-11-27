using CryptoTrader.Application.Abstractions.Backtesting;
using CryptoTrader.Application.Abstractions.Persistence;
using CryptoTrader.Domain.Common;
using CryptoTrader.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CryptoTrader.Infrastructure.Persistence.Repositories;

internal sealed class BacktestRunRepository : IBacktestRunRepository
{
    private readonly CryptoTraderDbContext _db;

    public BacktestRunRepository(CryptoTraderDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(BacktestRunRecord run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var report = run.Report;
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(report.Metrics);
        ArgumentNullException.ThrowIfNull(report.Assumptions);
        ArgumentNullException.ThrowIfNull(report.Trades);
        ArgumentNullException.ThrowIfNull(report.EquityCurve);
        if (run.RunId != report.RunId)
        {
            throw new InvalidOperationException("Backtest run id does not match the report run id.");
        }

        var numbers = new HashSet<int>();
        foreach (var trade in report.Trades)
        {
            if (!numbers.Add(trade.Number))
            {
                throw new InvalidOperationException($"Backtest run '{run.RunId}' repeats trade number {trade.Number}.");
            }

            if (!Enum.IsDefined(trade.ExitReason))
            {
                throw new InvalidOperationException($"Backtest trade {trade.Number} has an undefined exit reason.");
            }
        }

        var metrics = report.Metrics;
        var assumptions = report.Assumptions;
        var row = new BacktestRunRow
        {
            RunId = run.RunId,
            StrategyId = report.StrategyId,
            StrategyVersion = report.StrategyVersion,
            EngineVersion = report.EngineVersion,
            ParametersJson = report.ParametersJson,
            FromUtc = report.FromUtc,
            ToUtc = report.ToUtc,
            AssumptionsEngineVersion = assumptions.EngineVersion,
            FundingModeled = assumptions.FundingModeled,
            PartialFillsModeled = assumptions.PartialFillsModeled,
            FillPriceModel = assumptions.FillPriceModel,
            StopAndTakeProfitConflict = assumptions.StopAndTakeProfitConflict,
            AssumptionsNotes = assumptions.Notes,
            EquityCurveJson = PersistenceJson.Serialize(report.EquityCurve
                .Select(point => new EquityPointDocument { TimeUtc = point.TimeUtc, Equity = point.Equity })
                .ToArray()),
            CreatedAtUtc = run.CreatedAtUtc,
            Metrics = new BacktestMetricsRow
            {
                RunId = run.RunId,
                InitialEquity = metrics.InitialEquity,
                FinalEquity = metrics.FinalEquity,
                NetProfit = metrics.NetProfit,
                RoiFraction = metrics.RoiFraction,
                MaxDrawdownFraction = metrics.MaxDrawdownFraction,
                Sharpe = metrics.Sharpe,
                Sortino = metrics.Sortino,
                ProfitFactor = metrics.ProfitFactor,
                WinRateFraction = metrics.WinRateFraction,
                Expectancy = metrics.Expectancy,
                TradeCount = metrics.TradeCount,
                TotalFees = metrics.TotalFees,
                TotalSlippage = metrics.TotalSlippage,
                AverageHoldingTicks = metrics.AverageHoldingTime.Ticks,
            },
            Trades = report.Trades.Select(trade => new BacktestTradeRow
            {
                RunId = run.RunId,
                TradeNumber = trade.Number,
                InstrumentId = trade.Instrument.Value,
                EntryTimeUtc = trade.EntryTimeUtc,
                ExitTimeUtc = trade.ExitTimeUtc,
                EntryPrice = trade.EntryPrice,
                ExitPrice = trade.ExitPrice,
                Quantity = trade.Quantity,
                Fees = trade.Fees,
                SlippageCost = trade.SlippageCost,
                NetPnl = trade.NetPnl,
                ExitReason = (int)trade.ExitReason,
            }).ToList(),
        };
        _db.BacktestRuns.Add(row);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<BacktestRunRecord?> FindAsync(Guid runId, CancellationToken cancellationToken)
    {
        var row = await _db.BacktestRuns
            .AsNoTracking()
            .Include(item => item.Metrics)
            .Include(item => item.Trades)
            .FirstOrDefaultAsync(item => item.RunId == runId, cancellationToken);
        return row is null ? null : ToRecord(row);
    }

    private static BacktestRunRecord ToRecord(BacktestRunRow row)
    {
        if (row.Metrics is null)
        {
            throw new InvalidOperationException($"Backtest run '{row.RunId}' is missing metrics.");
        }

        var metrics = row.Metrics;
        var trades = row.Trades
            .OrderBy(trade => trade.TradeNumber)
            .Select(trade => new BacktestTrade(
                trade.TradeNumber,
                new InstrumentId(trade.InstrumentId),
                trade.EntryTimeUtc,
                trade.ExitTimeUtc,
                trade.EntryPrice,
                trade.ExitPrice,
                trade.Quantity,
                trade.Fees,
                trade.SlippageCost,
                trade.NetPnl,
                StoredEnum.FromInt32<BacktestExitReason>(trade.ExitReason, "exit_reason")))
            .ToArray();
        var curve = PersistenceJson
            .DeserializeRequired<List<EquityPointDocument>>(row.EquityCurveJson, "equity_curve")
            .Select(point => new EquityPoint(point.TimeUtc, point.Equity))
            .ToArray();
        var report = new BacktestReport(
            row.RunId,
            row.StrategyId,
            row.StrategyVersion,
            row.EngineVersion,
            row.ParametersJson,
            row.FromUtc,
            row.ToUtc,
            new BacktestMetrics(
                metrics.InitialEquity,
                metrics.FinalEquity,
                metrics.NetProfit,
                metrics.RoiFraction,
                metrics.MaxDrawdownFraction,
                metrics.Sharpe,
                metrics.Sortino,
                metrics.ProfitFactor,
                metrics.WinRateFraction,
                metrics.Expectancy,
                metrics.TradeCount,
                metrics.TotalFees,
                metrics.TotalSlippage,
                TimeSpan.FromTicks(metrics.AverageHoldingTicks)),
            new BacktestAssumptions(
                row.AssumptionsEngineVersion,
                row.FundingModeled,
                row.PartialFillsModeled,
                row.FillPriceModel,
                row.StopAndTakeProfitConflict,
                row.AssumptionsNotes),
            trades,
            curve);
        return new BacktestRunRecord(row.RunId, report, row.CreatedAtUtc);
    }

    private sealed class EquityPointDocument
    {
        public DateTimeOffset TimeUtc { get; set; }

        public decimal Equity { get; set; }
    }
}
