using CryptoTrader.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CryptoTrader.Infrastructure.Persistence.Repositories;

internal static class ExchangeCatalog
{
    public static async Task EnsureAsync(CryptoTraderDbContext db, string exchangeId, CancellationToken cancellationToken)
    {
        if (db.Exchanges.Local.Any(row => row.Id == exchangeId))
        {
            return;
        }

        var exists = await db.Exchanges.AsNoTracking().AnyAsync(row => row.Id == exchangeId, cancellationToken);
        if (!exists)
        {
            db.Exchanges.Add(new ExchangeRow { Id = exchangeId });
        }
    }
}
