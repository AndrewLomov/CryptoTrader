using CryptoTrader.Application.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CryptoTrader.Infrastructure.Persistence;

internal sealed class PersistenceMigrationHostedService : IHostedLifecycleService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<PersistenceOptions> _options;
    private readonly ILogger<PersistenceMigrationHostedService> _logger;

    public PersistenceMigrationHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<PersistenceOptions> options,
        ILogger<PersistenceMigrationHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        if (!_options.Value.ApplyMigrationsOnStartup)
        {
            return;
        }

        try
        {
            _logger.LogInformation("Applying database migrations.");
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CryptoTraderDbContext>();
            await DatabaseMigrator.ApplyAsync(db, cancellationToken);
            _logger.LogInformation("Database migrations applied.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Applying database migrations failed.");
            throw;
        }
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
