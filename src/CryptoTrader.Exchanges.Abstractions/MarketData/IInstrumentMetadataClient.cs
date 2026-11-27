using CryptoTrader.Domain.Instruments;

namespace CryptoTrader.Exchanges.Abstractions.MarketData;

public interface IInstrumentMetadataClient
{
    Task<IReadOnlyList<Instrument>> ListAsync(CancellationToken cancellationToken);

    Task<Instrument?> FindByExchangeSymbolAsync(string exchangeSymbol, CancellationToken cancellationToken);
}
