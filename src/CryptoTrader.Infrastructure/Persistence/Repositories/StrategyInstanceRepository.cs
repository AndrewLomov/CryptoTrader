using CryptoTrader.Application.Abstractions.Persistence;
using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.Strategies;
using CryptoTrader.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CryptoTrader.Infrastructure.Persistence.Repositories;

internal sealed class StrategyInstanceRepository : IStrategyInstanceRepository
{
    private readonly CryptoTraderDbContext _db;

    public StrategyInstanceRepository(CryptoTraderDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(StrategyInstance instance, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instance);
        await EnsureStrategyAsync(instance, cancellationToken);
        _db.StrategyInstances.Add(ToRow(instance));
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<StrategyInstance?> FindAsync(StrategyInstanceId id, CancellationToken cancellationToken)
    {
        var key = id.Value;
        var row = await _db.StrategyInstances.AsNoTracking().FirstOrDefaultAsync(item => item.Id == key, cancellationToken);
        return row is null ? null : ToInstance(row);
    }

    public async Task<IReadOnlyList<StrategyInstance>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await _db.StrategyInstances
            .AsNoTracking()
            .OrderBy(item => item.CreatedAtUtc)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        return rows.Select(ToInstance).ToArray();
    }

    public async Task UpdateAsync(StrategyInstance instance, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instance);
        await EnsureStrategyAsync(instance, cancellationToken);
        var key = instance.Id.Value;
        var row = await _db.StrategyInstances.FirstOrDefaultAsync(item => item.Id == key, cancellationToken);
        if (row is null)
        {
            throw new InvalidOperationException($"Strategy instance '{instance.Id}' was not found.");
        }

        row.StrategyId = instance.StrategyId;
        row.StrategyVersion = instance.StrategyVersion;
        row.InstrumentId = instance.InstrumentId.Value;
        row.ParametersJson = instance.ParametersJson;
        row.Status = (int)instance.Status;
        row.CreatedAtUtc = instance.CreatedAtUtc;
        row.UpdatedAtUtc = instance.UpdatedAtUtc;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureStrategyAsync(StrategyInstance instance, CancellationToken cancellationToken)
    {
        var strategyId = instance.StrategyId;
        var version = instance.StrategyVersion;
        if (_db.Strategies.Local.Any(row => row.StrategyId == strategyId && row.Version == version))
        {
            return;
        }

        var exists = await _db.Strategies
            .AsNoTracking()
            .AnyAsync(row => row.StrategyId == strategyId && row.Version == version, cancellationToken);
        if (!exists)
        {
            _db.Strategies.Add(new StrategyRow
            {
                StrategyId = strategyId,
                Version = version,
            });
        }
    }

    private static StrategyInstanceRow ToRow(StrategyInstance instance) => new()
    {
        Id = instance.Id.Value,
        StrategyId = instance.StrategyId,
        StrategyVersion = instance.StrategyVersion,
        InstrumentId = instance.InstrumentId.Value,
        ParametersJson = instance.ParametersJson,
        Status = (int)instance.Status,
        CreatedAtUtc = instance.CreatedAtUtc,
        UpdatedAtUtc = instance.UpdatedAtUtc,
    };

    private static StrategyInstance ToInstance(StrategyInstanceRow row) => StrategyInstance.Rehydrate(
        new StrategyInstanceId(row.Id),
        row.StrategyId,
        row.StrategyVersion,
        new InstrumentId(row.InstrumentId),
        row.ParametersJson,
        StoredEnum.FromInt32<StrategyInstanceStatus>(row.Status, "status"),
        row.CreatedAtUtc,
        row.UpdatedAtUtc);
}
