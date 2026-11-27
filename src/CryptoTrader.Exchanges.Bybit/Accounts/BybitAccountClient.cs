using CryptoTrader.Exchanges.Abstractions.Accounts;
using CryptoTrader.Exchanges.Abstractions.Models;

namespace CryptoTrader.Exchanges.Bybit.Accounts;

public sealed class BybitAccountClient : IAccountClient
{
    public Task<AccountSnapshot> GetAccountAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new NotSupportedException("Bybit private account data is not implemented in CryptoTrader 0.1.");
    }
}
