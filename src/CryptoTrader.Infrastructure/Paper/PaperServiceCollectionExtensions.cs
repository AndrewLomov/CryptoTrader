using CryptoTrader.Application.Abstractions.Execution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CryptoTrader.Infrastructure.Paper;

public static class PaperServiceCollectionExtensions
{
    public static IServiceCollection AddPaperExecution(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IExecutionVenue, PaperExecutionVenue>();
        return services;
    }
}
