using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.MarketData;

namespace CryptoTrader.Exchanges.Abstractions.MarketData;

public interface IFundingRateClient
{
    Task<FundingRate?> GetCurrentAsync(
        InstrumentId instrument,
        CancellationToken cancellationToken);
}
