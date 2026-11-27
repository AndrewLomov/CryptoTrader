using CryptoTrader.Domain.Common;

namespace CryptoTrader.Exchanges.Abstractions;

public sealed class ExchangeRegistry : IExchangeRegistry
{
    private readonly Dictionary<ExchangeId, IExchangeAdapter> _adapters;

    public ExchangeRegistry(IEnumerable<IExchangeAdapter> adapters)
    {
        ArgumentNullException.ThrowIfNull(adapters);
        _adapters = new Dictionary<ExchangeId, IExchangeAdapter>();
        foreach (var adapter in adapters)
        {
            ArgumentNullException.ThrowIfNull(adapter);
            if (!_adapters.TryAdd(adapter.ExchangeId, adapter))
            {
                throw new InvalidOperationException($"Exchange '{adapter.ExchangeId}' is registered more than once.");
            }
        }
    }

    public IReadOnlyCollection<IExchangeAdapter> GetAll() => _adapters.Values.ToArray();

    public IExchangeAdapter GetRequired(ExchangeId exchangeId)
    {
        if (TryGet(exchangeId, out var adapter) && adapter is not null)
        {
            return adapter;
        }

        throw new InvalidOperationException($"Exchange '{exchangeId}' is not registered.");
    }

    public bool TryGet(ExchangeId exchangeId, out IExchangeAdapter? adapter)
    {
        if (_adapters.TryGetValue(exchangeId, out var found))
        {
            adapter = found;
            return true;
        }

        adapter = null;
        return false;
    }
}
