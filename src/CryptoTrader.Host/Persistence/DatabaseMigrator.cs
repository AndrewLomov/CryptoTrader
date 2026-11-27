namespace CryptoTrader.Host.Persistence;

internal static class DatabaseMigrator
{
    public static async Task ApplyAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        await using var scope = services.CreateAsyncScope();
        var context = EfContextLocator.Resolve(scope.ServiceProvider)
            ?? throw new InvalidOperationException(
                "Persistence:ApplyMigrationsOnStartup is true, but no EF Core DbContext is registered.");
        await EfContextLocator.MigrateAsync(context, cancellationToken);
    }
}
