using System.Net.Http;
using CryptoTrader.Exchanges.Abstractions;
using CryptoTrader.Exchanges.Bybit.Accounts;
using CryptoTrader.Exchanges.Bybit.MarketData;
using CryptoTrader.Exchanges.Bybit.Rest;
using CryptoTrader.Exchanges.Bybit.Trading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CryptoTrader.Exchanges.Bybit;

public static class BybitServiceCollectionExtensions
{
    public static IServiceCollection AddBybit(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<BybitOptions>()
            .Bind(configuration.GetSection(BybitOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);

        services.AddHttpClient(BybitOptions.HttpClientName, (provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<BybitOptions>>().Value;
            if (!Uri.TryCreate(options.RestBaseUrl, UriKind.Absolute, out var baseAddress))
            {
                throw new InvalidOperationException("Bybit:RestBaseUrl must be an absolute URL.");
            }

            client.BaseAddress = baseAddress;
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        if (!IsEnabled(configuration, BybitOptions.SectionName, defaultValue: true))
        {
            return services;
        }

        services.AddSingleton<BybitRateGate>();
        services.AddSingleton<BybitPublicRest>();
        services.AddSingleton(provider => new BybitMarketDataClient(
            provider.GetRequiredService<BybitPublicRest>(),
            provider.GetRequiredService<IOptions<BybitOptions>>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILogger<BybitMarketDataClient>>()));
        services.AddSingleton(provider => new BybitFundingClient(
            provider.GetRequiredService<BybitPublicRest>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILogger<BybitFundingClient>>()));
        services.AddSingleton<BybitTradingClient>();
        services.AddSingleton<BybitAccountClient>();
        services.AddSingleton<BybitInstrumentMetadataClient>();
        services.AddSingleton<BybitExchangeAdapter>();
        services.AddSingleton<IExchangeAdapter>(provider => provider.GetRequiredService<BybitExchangeAdapter>());
        return services;
    }

    private static bool IsEnabled(IConfiguration configuration, string sectionName, bool defaultValue)
    {
        var text = configuration.GetSection(sectionName)["Enabled"];
        if (string.IsNullOrWhiteSpace(text))
        {
            return defaultValue;
        }

        if (bool.TryParse(text, out var enabled))
        {
            return enabled;
        }

        throw new InvalidOperationException($"{sectionName}:Enabled must be true or false.");
    }
}
