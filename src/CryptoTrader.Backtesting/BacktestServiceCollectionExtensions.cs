using CryptoTrader.Application.Abstractions.Backtesting;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoTrader.Backtesting;

public static class BacktestServiceCollectionExtensions
{
    public static IServiceCollection AddBacktesting(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IBacktestEngine, BacktestEngine>();
        return services;
    }
}
