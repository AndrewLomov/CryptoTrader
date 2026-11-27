using System.Net.Http;
using CryptoTrader.Exchanges.Abstractions;
using CryptoTrader.Exchanges.Okx.Accounts;
using CryptoTrader.Exchanges.Okx.MarketData;
using CryptoTrader.Exchanges.Okx.Rest;
using CryptoTrader.Exchanges.Okx.Trading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CryptoTrader.Exchanges.Okx;

public static class OkxServiceCollectionExtensions
{
    public static IServiceCollection AddOkx(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<OkxOptions>()
            .Bind(configuration.GetSection(OkxOptions.SectionName))
            .PostConfigure(options =>
            {
                if (options.InstrumentTypes is { Length: > 0 })
                {
                    options.InstrumentTypes = OkxInstrumentTypes.Normalize(options.InstrumentTypes);
                }
            });
        services.TryAddSingleton(TimeProvider.System);

        services.AddHttpClient(OkxOptions.HttpClientName, (provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<OkxOptions>>().Value;
            if (!Uri.TryCreate(options.RestBaseUrl, UriKind.Absolute, out var baseAddress))
            {
                throw new InvalidOperationException("Okx:RestBaseUrl must be an absolute URL.");
            }

            if (options.RequestTimeoutSeconds <= 0)
            {
                throw new InvalidOperationException("Okx:RequestTimeoutSeconds must be positive.");
            }

            client.BaseAddress = baseAddress;
            client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
        });

        if (!IsEnabled(configuration))
        {
            return services;
        }

        services.AddSingleton<OkxRateLimiter>();
        services.AddSingleton<OkxPublicRest>();
        services.AddSingleton(provider => new OkxMarketDataClient(
            provider.GetRequiredService<OkxPublicRest>(),
            provider.GetRequiredService<IOptions<OkxOptions>>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILogger<OkxMarketDataClient>>()));
        services.AddSingleton(provider => new OkxFundingClient(
            provider.GetRequiredService<OkxPublicRest>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILogger<OkxFundingClient>>()));
        services.AddSingleton<OkxTradingClient>();
        services.AddSingleton<OkxAccountClient>();
        services.AddSingleton<OkxInstrumentMetadataClient>();
        services.AddSingleton<OkxExchangeAdapter>();
        services.AddSingleton<IExchangeAdapter>(provider => provider.GetRequiredService<OkxExchangeAdapter>());
        return services;
    }

    private static bool IsEnabled(IConfiguration configuration)
    {
        var text = configuration.GetSection(OkxOptions.SectionName)["Enabled"];
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (bool.TryParse(text, out var enabled))
        {
            return enabled;
        }

        throw new InvalidOperationException("Okx:Enabled must be true or false.");
    }
}
