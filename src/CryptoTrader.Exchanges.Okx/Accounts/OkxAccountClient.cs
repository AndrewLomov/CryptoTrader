using CryptoTrader.Exchanges.Abstractions.Accounts;
using CryptoTrader.Exchanges.Abstractions.Models;

namespace CryptoTrader.Exchanges.Okx.Accounts;

public sealed class OkxAccountClient : IAccountClient
{
    public Task<AccountSnapshot> GetAccountAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new NotSupportedException("OKX private account data is not implemented in CryptoTrader 0.1.");
    }
}
