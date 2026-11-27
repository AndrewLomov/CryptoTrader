using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CryptoTrader.Infrastructure.Persistence;

public sealed class CryptoTraderDbContextFactory : IDesignTimeDbContextFactory<CryptoTraderDbContext>
{
    public const string ConnectionEnvironmentVariable = "CRYPTO_TRADER_CONNECTION";

    private const string FallbackConnectionString =
        "Host=localhost;Port=5432;Database=cryptotrader;Username=cryptotrader;Password=cryptotrader";

    public CryptoTraderDbContext CreateDbContext(string[] args)
    {
        _ = args;
        var connectionString = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = FallbackConnectionString;
        }

        var options = new DbContextOptionsBuilder<CryptoTraderDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new CryptoTraderDbContext(options);
    }
}
