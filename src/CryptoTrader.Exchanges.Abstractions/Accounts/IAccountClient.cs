using CryptoTrader.Exchanges.Abstractions.Models;

namespace CryptoTrader.Exchanges.Abstractions.Accounts;

public interface IAccountClient
{
    /// <summary>
    /// Reads account state. A public-only adapter throws <see cref="NotSupportedException"/>
    /// instead of inventing balances.
    /// </summary>
    Task<AccountSnapshot> GetAccountAsync(CancellationToken cancellationToken);
}
