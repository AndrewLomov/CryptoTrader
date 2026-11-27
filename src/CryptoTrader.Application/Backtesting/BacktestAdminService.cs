using CryptoTrader.Application.Abstractions.Backtesting;
using CryptoTrader.Application.Abstractions.Persistence;
using CryptoTrader.Application.Common;
using CryptoTrader.Application.Configuration;
using CryptoTrader.Application.MarketData;
using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.Instruments;
using CryptoTrader.Domain.MarketData;
using CryptoTrader.Domain.Risk;
using CryptoTrader.Strategies.Abstractions;
using Microsoft.Extensions.Options;

namespace CryptoTrader.Application.Backtesting;

public sealed class BacktestAdminService(
    IBacktestEngine engine,
    IBacktestRunRepository runs,
    ICandleRepository candles,
    IInstrumentRepository instruments,
    IStrategyCatalog catalog,
    IUnitOfWork unitOfWork,
    IOptions<MarketDataOptions> marketData,
    TimeProvider timeProvider)
{
    private readonly IBacktestEngine _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    private readonly IBacktestRunRepository _runs = runs ?? throw new ArgumentNullException(nameof(runs));
    private readonly ICandleRepository _candles = candles ?? throw new ArgumentNullException(nameof(candles));
    private readonly IInstrumentRepository _instruments = instruments ?? throw new ArgumentNullException(nameof(instruments));
    private readonly IStrategyCatalog _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    private readonly IUnitOfWork _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    private readonly IOptions<MarketDataOptions> _marketData = marketData ?? throw new ArgumentNullException(nameof(marketData));
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public async Task<BacktestReport> RunAsync(
        string strategyId,
        string strategyVersion,
        string parametersJson,
        string exchange,
        string instrument,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        decimal initialEquity,
        decimal feeRate,
        decimal slippageBps,
        bool exitOnTrendBreak,
        decimal riskPerTradePercent,
        decimal maxDailyLossPercent,
        int maxOpenPositions,
        decimal maxExposurePercent,
        decimal riskFeeRate,
        decimal riskSlippageBps,
        bool averagingDownEnabled,
        decimal maxDrawdownPercent,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var from = UtcTime.Normalize(fromUtc);
        var to = UtcTime.Normalize(toUtc);
        if (to <= from)
        {
            throw new InvalidOperationException("Backtest end must be after the start.");
        }

        if (initialEquity <= 0m)
        {
            throw new InvalidOperationException("Backtest initial equity must be greater than zero.");
        }

        var descriptor = _catalog.List().FirstOrDefault(item => string.Equals(item.Id, strategyId, StringComparison.Ordinal))
            ?? throw new NotFoundException($"Strategy '{strategyId}' was not found.");
        if (!string.Equals(descriptor.Version, strategyVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Strategy '{descriptor.Id}' version is '{descriptor.Version}', not '{strategyVersion}'.");
        }

        var exchangeId = ExchangeCode.Parse(exchange);
        var resolved = await InstrumentLookup.RequireAsync(_instruments, instrument, exchangeId, cancellationToken);
        var options = _marketData.Value ?? throw new InvalidOperationException("Market data options are not configured.");
        var (workingTimeframe, higherTimeframe) = TimeframeSelection.ForBacktest(options);
        var working = await LoadClosedAsync(resolved, workingTimeframe, from, to, cancellationToken);
        var higher = higherTimeframe is Timeframe higherFrame
            ? await LoadClosedAsync(resolved, higherFrame, from, to, cancellationToken)
            : [];
        var profile = RiskProfile.Create(
            riskPerTradePercent,
            maxDailyLossPercent,
            maxOpenPositions,
            maxExposurePercent,
            riskFeeRate,
            riskSlippageBps,
            averagingDownEnabled,
            maxDrawdownPercent,
            allowFuturesLive: false,
            allowCrossMargin: false);
        var strategy = _catalog.Create(descriptor.Id, parametersJson);
        var request = new BacktestRequest(
            descriptor.Id,
            descriptor.Version,
            parametersJson,
            resolved.Exchange,
            resolved.Id,
            resolved,
            from,
            to,
            initialEquity,
            feeRate,
            slippageBps,
            profile,
            working,
            higher,
            exitOnTrendBreak);
        var report = await _engine.RunAsync(request, strategy, cancellationToken);
        await _runs.AddAsync(
            new BacktestRunRecord(report.RunId, report, UtcTime.Normalize(_timeProvider.GetUtcNow())),
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return report;
    }

    public async Task<BacktestReport> GetAsync(Guid runId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var run = await _runs.FindAsync(runId, cancellationToken)
            ?? throw new NotFoundException($"Backtest '{runId}' was not found.");
        return run.Report;
    }

    private async Task<IReadOnlyList<Candle>> LoadClosedAsync(
        Instrument instrument,
        Timeframe timeframe,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        var loaded = await _candles.GetRangeAsync(instrument.Exchange, instrument.Id, timeframe, from, to, cancellationToken);
        var series = loaded
            .Where(candle => candle.IsClosed && candle.Timeframe == timeframe)
            .OrderBy(candle => candle.OpenTimeUtc)
            .ToArray();
        if (series.Length == 0)
        {
            throw new InvalidOperationException(
                $"No closed {timeframe.Code} candles for {instrument.ExchangeSymbol} between {from:O} and {to:O}. Import or sync candles before running a backtest. Missing candles are not fabricated.");
        }

        for (var index = 1; index < series.Length; index++)
        {
            if (series[index].OpenTimeUtc <= series[index - 1].OpenTimeUtc)
            {
                throw new InvalidOperationException(
                    $"Candle history for {instrument.ExchangeSymbol} {timeframe.Code} contains duplicate open times. Candles are not fabricated.");
            }
        }

        var missing = CandleGapDetector.FindMissingOpenTimes(series);
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Candle history for {instrument.ExchangeSymbol} {timeframe.Code} is missing {missing.Count} bar(s). The first missing open is {missing[0]:O}. Import the missing candles. They are not fabricated.");
        }

        return series;
    }
}
