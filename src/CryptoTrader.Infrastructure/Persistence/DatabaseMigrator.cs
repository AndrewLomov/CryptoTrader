using Microsoft.EntityFrameworkCore;

namespace CryptoTrader.Infrastructure.Persistence;

public static class DatabaseMigrator
{
    public static Task ApplyAsync(CryptoTraderDbContext db, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        return db.Database.MigrateAsync(cancellationToken);
    }
}
