using CryptoTrader.Domain.Instruments;
using CryptoTrader.Exchanges.Abstractions.MarketData;

namespace CryptoTrader.Exchanges.Bybit.MarketData;

public sealed class BybitInstrumentMetadataClient : IInstrumentMetadataClient
{
    private readonly BybitMarketDataClient _marketData;

    public BybitInstrumentMetadataClient(BybitMarketDataClient marketData)
    {
        ArgumentNullException.ThrowIfNull(marketData);
        _marketData = marketData;
    }

    public Task<IReadOnlyList<Instrument>> ListAsync(CancellationToken cancellationToken) =>
        _marketData.ListConfiguredInstrumentsAsync(cancellationToken);

    public Task<Instrument?> FindByExchangeSymbolAsync(string exchangeSymbol, CancellationToken cancellationToken) =>
        _marketData.FindInstrumentBySymbolAsync(exchangeSymbol, cancellationToken);
}
