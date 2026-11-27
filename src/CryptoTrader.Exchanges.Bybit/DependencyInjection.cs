using System.Net.Http;
using CryptoTrader.Exchanges.Abstractions;
using CryptoTrader.Exchanges.Abstractions.Accounts;
using CryptoTrader.Exchanges.Abstractions.MarketData;
using CryptoTrader.Exchanges.Abstractions.Trading;
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
        services.AddSingleton<IMarketDataClient>(provider => provider.GetRequiredService<BybitExchangeAdapter>().MarketData);
        services.AddSingleton<ITradingClient>(provider => provider.GetRequiredService<BybitExchangeAdapter>().Trading);
        services.AddSingleton<IAccountClient>(provider => provider.GetRequiredService<BybitExchangeAdapter>().Account);
        services.AddSingleton<IInstrumentMetadataClient>(provider => provider.GetRequiredService<BybitExchangeAdapter>().Instruments);
        services.AddSingleton<IFundingRateClient>(provider => provider.GetRequiredService<BybitExchangeAdapter>().Funding);
        return services;
    }
}
