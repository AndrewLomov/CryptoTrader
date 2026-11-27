using System.Diagnostics.Metrics;
using CryptoTrader.Application.Abstractions.Persistence;
using CryptoTrader.Application.Configuration;
using CryptoTrader.Application.Paper;
using CryptoTrader.Domain.Common;
using Instrument = CryptoTrader.Domain.Instruments.Instrument;
using CryptoTrader.Domain.MarketData;
using CryptoTrader.Exchanges.Abstractions;
using CryptoTrader.Exchanges.Abstractions.MarketData;
using CryptoTrader.Exchanges.Abstractions.Models;
using Microsoft.Extensions.Options;

namespace CryptoTrader.Host.MarketData;

public sealed class CandleIngestionService : BackgroundService
{
    public const string MeterName = "CryptoTrader.MarketData";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<BybitOptions> _bybitOptions;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CandleIngestionService> _logger;
    private readonly Meter _meter = new(MeterName);
    private int _activeSubscriptions;
    private long _ageTicks = TimeSpan.FromHours(24).Ticks;

    public CandleIngestionService(
        IServiceScopeFactory scopeFactory,
        IOptions<BybitOptions> bybitOptions,
        TimeProvider timeProvider,
        ILogger<CandleIngestionService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _bybitOptions = bybitOptions ?? throw new ArgumentNullException(nameof(bybitOptions));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _meter.CreateObservableGauge(
            "cryptotrader.market_data.websocket_connected",
            () => Interlocked.CompareExchange(ref _activeSubscriptions, 0, 0) > 0 ? 1 : 0,
            description: "1 while a Bybit 1h candle subscription is running.");
        _meter.CreateObservableGauge(
            "cryptotrader.market_data.age_seconds",
            () => TimeSpan.FromTicks(Interlocked.Read(ref _ageTicks)).TotalSeconds,
            description: "Age of the newest closed 1h candle observed by ingestion.");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var symbols = (_bybitOptions.Value.Symbols ?? [])
            .Where(symbol => !string.IsNullOrWhiteSpace(symbol))
            .Select(symbol => symbol.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (symbols.Length == 0)
        {
            _logger.LogWarning("Bybit symbols are empty. Candle ingestion is idle.");
            return;
        }

        await Task.WhenAll(symbols.Select(symbol => IngestSymbolAsync(symbol, stoppingToken)));
    }

    private async Task IngestSymbolAsync(string symbol, CancellationToken stoppingToken)
    {
        var delaySeconds = 1;
        while (!stoppingToken.IsCancellationRequested)
        {
            var failed = false;
            try
            {
                var subscribed = await TrySubscribeAsync(symbol, stoppingToken);
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
                _logger.LogWarning(exception, "Candle ingestion for {Symbol} failed. Retrying.", symbol);
            }

            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                var jitterMs = Random.Shared.Next(0, 500);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds) + TimeSpan.FromMilliseconds(jitterMs), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            delaySeconds = failed ? Math.Min(delaySeconds * 2, 60) : 1;
        }
    }

    private async Task<bool> TrySubscribeAsync(string symbol, CancellationToken stoppingToken)
    {
        IMarketDataClient? client = null;
        Instrument? instrument = null;
        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            client = ResolveMarketData(scope.ServiceProvider);
            if (client is null)
            {
                _logger.LogWarning("Market data client is not registered. Candle ingestion for {Symbol} will retry.", symbol);
                return false;
            }

            instrument = await ResolveInstrumentAsync(scope.ServiceProvider, symbol, stoppingToken);
        }

        if (client is null || instrument is null)
        {
            _logger.LogWarning("Bybit symbol {Symbol} is not available yet. Candle ingestion will retry.", symbol);
            return false;
        }

        var subscription = new MarketSubscription(
            instrument.Id,
            Timeframe.OneHour,
            IncludeCandles: true,
            IncludeTicker: false,
            IncludeTrades: false,
            IncludeOrderBook: false);
        Interlocked.Increment(ref _activeSubscriptions);
        try
        {
            await foreach (var marketEvent in client.SubscribeAsync(subscription, stoppingToken))
            {
                if (marketEvent is not CandleObserved observed || !observed.Candle.IsClosed || observed.Candle.Timeframe != Timeframe.OneHour)
                {
                    continue;
                }

                var age = _timeProvider.GetUtcNow() - observed.Candle.CloseTimeUtc;
                if (age < TimeSpan.Zero)
                {
                    age = TimeSpan.Zero;
                }

                Interlocked.Exchange(ref _ageTicks, age.Ticks);
                try
                {
                    await PersistAndTradeAsync(observed.Candle, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(
                        exception,
                        "Closed {Symbol} candle at {CloseTime} was not processed.",
                        symbol,
                        observed.Candle.CloseTimeUtc);
                }
            }
        }
        finally
        {
            Interlocked.Decrement(ref _activeSubscriptions);
        }

        _logger.LogWarning("Candle subscription for {Symbol} ended. Resubscribing.", symbol);
        return true;
    }

    private async Task PersistAndTradeAsync(Candle candle, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICandleRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var coordinator = scope.ServiceProvider.GetRequiredService<PaperTradingCoordinator>();
        await repository.UpsertRangeAsync([candle], cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await coordinator.OnClosedCandleAsync(candle, cancellationToken);
    }

    private static IMarketDataClient? ResolveMarketData(IServiceProvider provider) =>
        provider.GetService<IMarketDataClient>() ?? provider.GetService<IExchangeAdapter>()?.MarketData;

    private static async Task<Instrument?> ResolveInstrumentAsync(
        IServiceProvider provider,
        string symbol,
        CancellationToken cancellationToken)
    {
        var repository = provider.GetService<IInstrumentRepository>();
        if (repository is not null)
        {
            var stored = await repository.FindByExchangeSymbolAsync(ExchangeId.Bybit, symbol, cancellationToken);
            if (stored is not null)
            {
                return stored;
            }
        }

        var metadata = provider.GetService<IInstrumentMetadataClient>() ?? provider.GetService<IExchangeAdapter>()?.Instruments;
        if (metadata is null)
        {
            return null;
        }

        var remote = await metadata.FindByExchangeSymbolAsync(symbol, cancellationToken);
        if (remote is null || remote.Exchange != ExchangeId.Bybit)
        {
            return null;
        }

        if (repository is not null)
        {
            await repository.UpsertAsync(remote, cancellationToken);
            var unitOfWork = provider.GetService<IUnitOfWork>();
            if (unitOfWork is not null)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        return remote;
    }
}
