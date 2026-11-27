using CryptoTrader.Domain.Instruments;
using CryptoTrader.Exchanges.Abstractions.MarketData;

namespace CryptoTrader.Exchanges.Okx.MarketData;

public sealed class OkxInstrumentMetadataClient : IInstrumentMetadataClient
{
    private readonly OkxMarketDataClient _marketData;

    public OkxInstrumentMetadataClient(OkxMarketDataClient marketData)
    {
        ArgumentNullException.ThrowIfNull(marketData);
        _marketData = marketData;
    }

    public Task<IReadOnlyList<Instrument>> ListAsync(CancellationToken cancellationToken) =>
        _marketData.ListInstrumentsAsync(cancellationToken);

    public Task<Instrument?> FindByExchangeSymbolAsync(string exchangeSymbol, CancellationToken cancellationToken) =>
        _marketData.FindInstrumentBySymbolAsync(exchangeSymbol, cancellationToken);
}
