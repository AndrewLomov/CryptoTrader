using CryptoTrader.Application.Abstractions.MarketData;
using CryptoTrader.Application.Abstractions.Persistence;
using CryptoTrader.Application.Configuration;
using CryptoTrader.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CryptoTrader.Infrastructure.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<PersistenceOptions>()
            .Bind(configuration.GetSection(PersistenceOptions.SectionName));

        services.AddDbContext<CryptoTraderDbContext>((provider, options) =>
        {
            var connectionString = provider.GetRequiredService<IOptions<PersistenceOptions>>().Value.ConnectionString;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Persistence:ConnectionString is required.");
            }

            options.UseNpgsql(connectionString);
        });

        services.AddScoped<ICandleRepository, CandleRepository>();
        services.AddScoped<ICandleConsumerCheckpoint, CandleConsumerCheckpoint>();
        services.AddScoped<IInstrumentRepository, InstrumentRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IPositionRepository, PositionRepository>();
        services.AddScoped<IStrategyInstanceRepository, StrategyInstanceRepository>();
        services.AddScoped<ITradeSignalRepository, TradeSignalRepository>();
        services.AddScoped<IBacktestRunRepository, BacktestRunRepository>();
        services.AddScoped<IPaperStateRepository, PaperStateRepository>();
        services.AddScoped<IRiskEventRepository, RiskEventRepository>();
        services.AddScoped<ITradingAccountRepository, TradingAccountRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        services.AddHealthChecks()
            .AddDbContextCheck<CryptoTraderDbContext>("database", failureStatus: null);

        services.AddHostedService<PersistenceMigrationHostedService>();
        return services;
    }
}
