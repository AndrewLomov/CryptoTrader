using System.Data;
using System.Data.Common;
using System.Reflection;
using CryptoTrader.Application.Configuration;
using CryptoTrader.Host.Persistence;
using Microsoft.Extensions.Options;

namespace CryptoTrader.Host.Health;

internal sealed class DatabaseReadyProbe(
    IServiceScopeFactory scopeFactory,
    IOptions<PersistenceOptions> persistence,
    ILogger<DatabaseReadyProbe> logger)
{
    public async Task<bool> CheckAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var context = EfContextLocator.Resolve(scope.ServiceProvider);
            if (context is not null)
            {
                var connected = await EfContextLocator.CanConnectAsync(context, timeout.Token);
                if (connected || context.GetType().GetProperty("Database")?.GetValue(context)?.GetType().GetMethod("CanConnectAsync", [typeof(CancellationToken)]) is not null)
                {
                    return connected;
                }
            }

            return await OpenNpgsqlAsync(persistence.Value.ConnectionString, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Database readiness check timed out.");
            return false;
        }
        catch (Exception exception)
        {
            logger.LogWarning("Database readiness check failed ({ExceptionType}).", exception.GetType().Name);
            return false;
        }
    }

    private static async Task<bool> OpenNpgsqlAsync(string connectionString, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return false;
        }

        var connectionType = Assembly.Load("Npgsql").GetType("Npgsql.NpgsqlConnection", throwOnError: true)
            ?? throw new InvalidOperationException("NpgsqlConnection was not found.");
        var connection = Activator.CreateInstance(connectionType, connectionString) as DbConnection
            ?? throw new InvalidOperationException("Npgsql connection could not be created.");
        await using (connection)
        {
            await connection.OpenAsync(cancellationToken);
            return connection.State == ConnectionState.Open;
        }
    }
}
