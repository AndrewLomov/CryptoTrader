using CryptoTrader.Application.Abstractions.Risk;
using CryptoTrader.Application.Backtesting;
using CryptoTrader.Application.Configuration;
using CryptoTrader.Application.MarketData;
using CryptoTrader.Application.Observability;
using CryptoTrader.Application.Paper;
using CryptoTrader.Application.Risk;
using CryptoTrader.Application.Strategies;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CryptoTrader.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var execution = ApplicationOptionsReader.ReadExecution(configuration);
        var risk = ApplicationOptionsReader.ReadRisk(configuration);
        var marketData = ApplicationOptionsReader.ReadMarketData(configuration);
        var persistence = ApplicationOptionsReader.ReadPersistence(configuration);
        var api = ApplicationOptionsReader.ReadApi(configuration);
        var bybit = ApplicationOptionsReader.ReadBybit(configuration);
        var observability = ApplicationOptionsReader.ReadObservability(configuration);
        var okx = ApplicationOptionsReader.ReadOkx(configuration);
        StartupConfigurationValidator.Validate(execution, risk, marketData, persistence, api, bybit, observability, okx);

        services.AddSingleton<IOptions<ExecutionOptions>>(Options.Create(execution));
        services.AddSingleton<IOptions<RiskOptions>>(Options.Create(risk));
        services.AddSingleton<IOptions<MarketDataOptions>>(Options.Create(marketData));
        services.AddSingleton<IOptions<PersistenceOptions>>(Options.Create(persistence));
        services.AddSingleton<IOptions<ApiOptions>>(Options.Create(api));
        services.AddSingleton<IOptions<BybitOptions>>(Options.Create(bybit));
        services.AddSingleton<IOptions<OkxOptions>>(Options.Create(okx));
        services.AddSingleton<IOptions<ObservabilityOptions>>(Options.Create(observability));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<TradingMetrics>();
        services.AddSingleton<PaperTradingGate>();
        services.AddSingleton<IRiskManager, RiskManager>();
        services.AddScoped<CandleAdminService>();
        services.AddScoped<StrategyAdminService>();
        services.AddScoped<BacktestAdminService>();
        services.AddScoped<PaperTradingCoordinator>();
        services.AddScoped<CandleIngestionProcessor>();
        return services;
    }
}
