using CryptoTrader.Domain.Common;

namespace CryptoTrader.Exchanges.Abstractions;

public interface IExchangeRegistry
{
    IReadOnlyCollection<IExchangeAdapter> GetAll();

    IExchangeAdapter GetRequired(ExchangeId exchangeId);

    bool TryGet(ExchangeId exchangeId, out IExchangeAdapter? adapter);
}
