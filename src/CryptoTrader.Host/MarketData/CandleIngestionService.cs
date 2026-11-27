using System.Diagnostics.Metrics;
using CryptoTrader.Application.Abstractions.MarketData;
using CryptoTrader.Application.Abstractions.Persistence;
using CryptoTrader.Application.Configuration;
using CryptoTrader.Application.MarketData;
using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.Instruments;
using Instrument = CryptoTrader.Domain.Instruments.Instrument;
using CryptoTrader.Domain.MarketData;
using CryptoTrader.Exchanges.Abstractions;
using CryptoTrader.Exchanges.Abstractions.Models;
using Microsoft.Extensions.Options;

namespace CryptoTrader.Host.MarketData;

public sealed class CandleIngestionService : BackgroundService
{
    public const string MeterName = "CryptoTrader.MarketData";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<MarketDataOptions> _marketDataOptions;
    private readonly IOptions<BybitOptions> _bybitOptions;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CandleIngestionService> _logger;
    private readonly MarketDataFeedHealth _feedHealth;
    private readonly Meter _meter = new(MeterName);
    private int _activeSubscriptions;
    private long _newestCloseUnixMilliseconds;

    public CandleIngestionService(
        IServiceScopeFactory scopeFactory,
        IOptions<MarketDataOptions> marketDataOptions,
        IOptions<BybitOptions> bybitOptions,
        TimeProvider timeProvider,
        ILogger<CandleIngestionService> logger,
        MarketDataFeedHealth feedHealth)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _marketDataOptions = marketDataOptions ?? throw new ArgumentNullException(nameof(marketDataOptions));
        _bybitOptions = bybitOptions ?? throw new ArgumentNullException(nameof(bybitOptions));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _feedHealth = feedHealth ?? throw new ArgumentNullException(nameof(feedHealth));
        _meter.CreateObservableGauge(
            "cryptotrader.market_data.websocket_connected",
            () => Interlocked.CompareExchange(ref _activeSubscriptions, 0, 0) > 0 ? 1 : 0,
            description: "1 while at least one candle subscription is running.");
        _meter.CreateObservableGauge(
            "cryptotrader.market_data.age_seconds",
            ObserveNewestCandleAgeSeconds,
            description: "Age in seconds of the newest closed candle observed by ingestion.");
    }

    public override void Dispose()
    {
        _meter.Dispose();
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ReplayPendingAsync(stoppingToken);
        if (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        var subscriptions = SelectSubscriptions();
        if (subscriptions.Count == 0)
        {
            _logger.LogWarning("Market data subscriptions are empty. Candle ingestion is idle.");
            return;
        }

        var work = subscriptions.Select(subscription => IngestAsync(subscription, stoppingToken)).ToList();
        work.Add(RetryPendingAsync(stoppingToken));
        await Task.WhenAll(work);
    }

    private async Task ReplayPendingAsync(CancellationToken stoppingToken)
    {
        var delaySeconds = 1;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReplayPendingBatchesAsync(stoppingToken);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception) when (!IsMissingCheckpoint(exception))
            {
                _logger.LogWarning(exception, "Pending candle replay failed. Retrying.");
                if (!await DelayAsync(delaySeconds, stoppingToken))
                {
                    return;
                }

                delaySeconds = Math.Min(delaySeconds * 2, 60);
            }
        }
    }

    private async Task ReplayPendingBatchesAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var processor = scope.ServiceProvider.GetRequiredService<CandleIngestionProcessor>();
            var batch = await processor.ReplayPendingAsync(stoppingToken);
            if (batch.Count == 0 || batch.LeftPending || batch.Count < CandleIngestionProcessor.PendingReplayLimit)
            {
                return;
            }
        }
    }

    private async Task RetryPendingAsync(CancellationToken stoppingToken)
    {
        var delaySeconds = 1;
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!await DelayAsync(delaySeconds, stoppingToken))
            {
                return;
            }

            try
            {
                var leftPending = false;
                while (!stoppingToken.IsCancellationRequested)
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var processor = scope.ServiceProvider.GetRequiredService<CandleIngestionProcessor>();
                    var batch = await processor.ReplayPendingAsync(stoppingToken);
                    if (batch.LeftPending)
                    {
                        leftPending = true;
                    }

                    if (batch.Count == 0 || batch.LeftPending || batch.Count < CandleIngestionProcessor.PendingReplayLimit)
                    {
                        break;
                    }
                }

                delaySeconds = leftPending ? Math.Min(delaySeconds * 2, 60) : 1;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception) when (!IsMissingCheckpoint(exception))
            {
                _logger.LogWarning(exception, "Pending candle replay failed. Retrying.");
                delaySeconds = Math.Min(delaySeconds * 2, 60);
            }
        }
    }

    private IReadOnlyList<IngestionTarget> SelectSubscriptions()
    {
        var configured = _marketDataOptions.Value.Subscriptions ?? [];
        if (configured.Length > 0)
        {
            var targets = new List<IngestionTarget>();
            foreach (var subscription in configured)
            {
                CollectConfigured(subscription, targets);
            }

            return targets
                .DistinctBy(target => (target.Exchange.Value, target.Instrument, target.Timeframe.Code))
                .ToArray();
        }

        if (!_bybitOptions.Value.Enabled)
        {
            return [];
        }

        return (_bybitOptions.Value.Symbols ?? [])
            .Where(symbol => !string.IsNullOrWhiteSpace(symbol))
            .Select(symbol => symbol.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .Select(symbol => new IngestionTarget(ExchangeId.Bybit, symbol, Timeframe.OneHour))
            .ToArray();
    }

    private void CollectConfigured(MarketDataSubscriptionOptions subscription, List<IngestionTarget> targets)
    {
        var exchangeText = subscription.Exchange?.Trim() ?? "";
        var instrument = subscription.Instrument?.Trim() ?? "";
        if (exchangeText.Length == 0 || instrument.Length == 0)
        {
            _logger.LogWarning("Market data subscription is missing an exchange or instrument.");
            return;
        }

        ExchangeId exchange;
        try
        {
            exchange = new ExchangeId(exchangeText.ToUpperInvariant());
        }
        catch (DomainException exception)
        {
            _logger.LogWarning(exception, "Market data subscription exchange {Exchange} was rejected.", exchangeText);
            return;
        }

        foreach (var code in subscription.Timeframes ?? [])
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            try
            {
                targets.Add(new IngestionTarget(exchange, instrument, Timeframe.Parse(code)));
            }
            catch (DomainException exception)
            {
                _logger.LogWarning(exception, "Market data timeframe {Timeframe} was rejected.", code);
            }
        }
    }

    private async Task IngestAsync(IngestionTarget target, CancellationToken stoppingToken)
    {
        var delaySeconds = 1;
        while (!stoppingToken.IsCancellationRequested)
        {
            var failed = false;
            try
            {
                var subscribed = await TrySubscribeAsync(target, stoppingToken);
                if (!subscribed)
                {
                    failed = true;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                failed = true;
                var detail = IsAcknowledgementFailure(exception)
                    ? "Subscription acknowledgement failed."
                    : "Subscribe failed.";
                Report(target, MarketDataFeedHealth.Unhealthy, detail, _timeProvider.GetUtcNow());
                _logger.LogWarning(
                    exception,
                    "Candle ingestion for {Exchange} {Instrument} {Timeframe} failed. Retrying.",
                    target.Exchange.Value,
                    target.Instrument,
                    target.Timeframe.Code);
            }

            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            if (!await DelayAsync(delaySeconds, stoppingToken))
            {
                break;
            }

            delaySeconds = failed ? Math.Min(delaySeconds * 2, 60) : 1;
        }
    }

    private async Task<bool> TrySubscribeAsync(IngestionTarget target, CancellationToken stoppingToken)
    {
        Report(target, MarketDataFeedHealth.Degraded, "Subscribing.", null);
        IExchangeAdapter adapter;
        Instrument instrument;
        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            var registry = scope.ServiceProvider.GetRequiredService<IExchangeRegistry>();
            try
            {
                adapter = registry.GetRequired(target.Exchange);
            }
            catch (InvalidOperationException exception)
            {
                _logger.LogWarning(exception, "Exchange {Exchange} is not registered.", target.Exchange.Value);
                Report(target, MarketDataFeedHealth.Unhealthy, "Exchange adapter is not registered.", _timeProvider.GetUtcNow());
                return false;
            }

            var resolved = await ResolveInstrumentAsync(scope.ServiceProvider, adapter, target, stoppingToken);
            if (resolved.Value is not Instrument resolvedInstrument)
            {
                Report(target, MarketDataFeedHealth.Degraded, resolved.Detail, null);
                return false;
            }

            instrument = resolvedInstrument;
        }

        var subscription = new MarketSubscription(
            instrument.Id,
            target.Timeframe,
            IncludeCandles: true,
            IncludeTicker: false,
            IncludeTrades: false,
            IncludeOrderBook: false);
        Report(target, MarketDataFeedHealth.Degraded, "Waiting for a closed candle.", null);
        Interlocked.Increment(ref _activeSubscriptions);
        try
        {
            await foreach (var marketEvent in adapter.MarketData.SubscribeAsync(subscription, stoppingToken))
            {
                if (marketEvent is not CandleObserved observed
                    || !observed.Candle.IsClosed
                    || observed.Candle.Timeframe != target.Timeframe
                    || observed.Candle.Instrument != instrument.Id)
                {
                    continue;
                }

                var decision = EvaluateClosedCandle(target, observed);
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var processor = scope.ServiceProvider.GetRequiredService<CandleIngestionProcessor>();
                    await processor.PersistAndTradeAsync(observed.Candle, decision, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(
                        exception,
                        "Closed {Exchange} {Instrument} {Timeframe} candle at {CloseTime} was not processed.",
                        target.Exchange.Value,
                        target.Instrument,
                        target.Timeframe.Code,
                        observed.Candle.CloseTimeUtc);
                }
            }
        }
        finally
        {
            Interlocked.Decrement(ref _activeSubscriptions);
        }

        _logger.LogWarning(
            "Candle subscription for {Exchange} {Instrument} {Timeframe} ended. Resubscribing.",
            target.Exchange.Value,
            target.Instrument,
            target.Timeframe.Code);
        Report(target, MarketDataFeedHealth.Degraded, "Subscription ended.", null);
        return true;
    }

    private async Task<InstrumentResolution> ResolveInstrumentAsync(
        IServiceProvider provider,
        IExchangeAdapter adapter,
        IngestionTarget target,
        CancellationToken cancellationToken)
    {
        var repository = provider.GetRequiredService<IInstrumentRepository>();
        var instrument = await repository.FindByExchangeSymbolAsync(target.Exchange, target.Instrument, cancellationToken);
        if (instrument is null)
        {
            instrument = await adapter.Instruments.FindByExchangeSymbolAsync(target.Instrument, cancellationToken);
            if (instrument is null)
            {
                _logger.LogWarning(
                    "Instrument {Symbol} on {Exchange} was not found. Candle ingestion will retry.",
                    target.Instrument,
                    target.Exchange.Value);
                return new InstrumentResolution(null, "Instrument was not found.");
            }

            await repository.UpsertAsync(instrument, cancellationToken);
            var unitOfWork = provider.GetRequiredService<IUnitOfWork>();
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        if (instrument.Exchange != target.Exchange)
        {
            _logger.LogWarning(
                "Instrument {Symbol} belongs to {ActualExchange}, not {Exchange}. Candle ingestion will retry.",
                target.Instrument,
                instrument.Exchange.Value,
                target.Exchange.Value);
            return new InstrumentResolution(null, "Instrument exchange does not match the subscription.");
        }

        if (!instrument.IsTradingEnabled)
        {
            _logger.LogWarning(
                "Instrument {Symbol} on {Exchange} is not enabled for trading. Candle ingestion will retry.",
                target.Instrument,
                target.Exchange.Value);
            return new InstrumentResolution(null, "Instrument trading is disabled.");
        }

        if (instrument.Type == InstrumentType.LinearPerpetual && !instrument.HasCompleteLinearContractSpec)
        {
            _logger.LogWarning(
                "Linear instrument {Symbol} on {Exchange} does not have a complete contract specification. Candle ingestion will retry.",
                target.Instrument,
                target.Exchange.Value);
            return new InstrumentResolution(null, "Linear contract specification is incomplete.");
        }

        return new InstrumentResolution(instrument, null);
    }

    private CandleProcessingDecision EvaluateClosedCandle(IngestionTarget target, CandleObserved observed)
    {
        var now = _timeProvider.GetUtcNow();
        RememberClose(observed.Candle.CloseTimeUtc);
        var decision = CandleProcessingDecision.Evaluate(
            observed.Candle.IsClosed,
            observed.Candle.CloseTimeUtc,
            observed.ReceivedAtUtc,
            now,
            StaleAfter());
        if (decision.CanUpdatePaper)
        {
            Report(target, MarketDataFeedHealth.Healthy, null, observed.ReceivedAtUtc);
            return decision;
        }

        var lastEvent = decision.Admission == CandleAdmission.StaleClose
            ? observed.Candle.CloseTimeUtc
            : observed.ReceivedAtUtc;
        Report(target, MarketDataFeedHealth.Degraded, decision.Reason, lastEvent);
        return decision;
    }

    private void RememberClose(DateTimeOffset closeTimeUtc)
    {
        var milliseconds = closeTimeUtc.ToUnixTimeMilliseconds();
        long current;
        do
        {
            current = Interlocked.Read(ref _newestCloseUnixMilliseconds);
            if (milliseconds <= current)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(ref _newestCloseUnixMilliseconds, milliseconds, current) != current);
    }

    private double ObserveNewestCandleAgeSeconds()
    {
        var milliseconds = Interlocked.Read(ref _newestCloseUnixMilliseconds);
        if (milliseconds == 0)
        {
            return TimeSpan.FromHours(24).TotalSeconds;
        }

        var age = _timeProvider.GetUtcNow() - DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
        return NonNegative(age).TotalSeconds;
    }

    private TimeSpan StaleAfter() => CandleProcessingDecision.Window(_marketDataOptions.Value.StaleAfterSeconds);

    private void Report(IngestionTarget target, string status, string? detail, DateTimeOffset? lastEventUtc) =>
        _feedHealth.Report(target.Exchange.Value, target.Instrument, target.Timeframe.Code, status, detail, lastEventUtc);

    private static bool IsMissingCheckpoint(Exception exception) =>
        exception is InvalidOperationException
        && exception.Message.Contains(nameof(ICandleConsumerCheckpoint), StringComparison.Ordinal);

    private static bool IsAcknowledgementFailure(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current.GetType().Name.Contains("Ack", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("ack", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<bool> DelayAsync(int delaySeconds, CancellationToken stoppingToken)
    {
        try
        {
            var jitterMs = Random.Shared.Next(0, 500);
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds) + TimeSpan.FromMilliseconds(jitterMs), stoppingToken);
            return true;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private static TimeSpan NonNegative(TimeSpan value) => value < TimeSpan.Zero ? TimeSpan.Zero : value;

    private readonly record struct IngestionTarget(ExchangeId Exchange, string Instrument, Timeframe Timeframe);

    private readonly record struct InstrumentResolution(Instrument? Value, string? Detail);
}
