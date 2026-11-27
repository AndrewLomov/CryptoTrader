using Microsoft.Extensions.DependencyInjection;
using CryptoTrader.Strategies.Abstractions;

namespace CryptoTrader.Strategies.TrendFollowing;

public static class TrendFollowingServiceCollectionExtensions
{
    public static IServiceCollection AddTrendFollowing(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IStrategyCatalog, TrendFollowingCatalog>();
        return services;
    }
}
