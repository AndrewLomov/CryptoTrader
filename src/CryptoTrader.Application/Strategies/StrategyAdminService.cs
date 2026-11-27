using CryptoTrader.Application.Abstractions.Persistence;
using CryptoTrader.Application.Common;
using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.Strategies;
using CryptoTrader.Strategies.Abstractions;

namespace CryptoTrader.Application.Strategies;

public sealed class StrategyAdminService(
    IStrategyCatalog catalog,
    IStrategyInstanceRepository instances,
    IInstrumentRepository instruments,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    private readonly IStrategyCatalog _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    private readonly IStrategyInstanceRepository _instances = instances ?? throw new ArgumentNullException(nameof(instances));
    private readonly IInstrumentRepository _instruments = instruments ?? throw new ArgumentNullException(nameof(instruments));
    private readonly IUnitOfWork _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public IReadOnlyList<StrategyDescriptor> ListStrategies() => _catalog.List();

    public Task<IReadOnlyList<StrategyInstance>> ListInstancesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _instances.ListAsync(cancellationToken);
    }

    public async Task<StrategyInstance> CreateInstanceAsync(
        string strategyId,
        string instrument,
        string parametersJson,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var descriptor = RequireDescriptor(strategyId);
        var resolved = await InstrumentLookup.RequireAsync(_instruments, instrument, null, cancellationToken);
        var created = StrategyInstance.Create(
            StrategyInstanceId.New(),
            descriptor.Id,
            descriptor.Version,
            resolved.Id,
            parametersJson,
            UtcTime.Normalize(_timeProvider.GetUtcNow()));
        await _instances.AddAsync(created, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return created;
    }

    public Task<StrategyInstance> StartAsync(Guid instanceId, CancellationToken cancellationToken) =>
        ChangeStatusAsync(instanceId, static (instance, now) => instance.Start(now), cancellationToken);

    public Task<StrategyInstance> StopAsync(Guid instanceId, CancellationToken cancellationToken) =>
        ChangeStatusAsync(instanceId, static (instance, now) => instance.Stop(now), cancellationToken);

    private async Task<StrategyInstance> ChangeStatusAsync(
        Guid instanceId,
        Action<StrategyInstance, DateTimeOffset> change,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var instance = await _instances.FindAsync(new StrategyInstanceId(instanceId), cancellationToken)
            ?? throw new NotFoundException($"Strategy instance '{instanceId}' was not found.");
        change(instance, UtcTime.Normalize(_timeProvider.GetUtcNow()));
        await _instances.UpdateAsync(instance, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return instance;
    }

    private StrategyDescriptor RequireDescriptor(string strategyId)
    {
        if (string.IsNullOrWhiteSpace(strategyId))
        {
            throw new ArgumentException("Strategy id is required.", nameof(strategyId));
        }

        return _catalog.List().FirstOrDefault(descriptor => string.Equals(descriptor.Id, strategyId, StringComparison.Ordinal))
            ?? throw new NotFoundException($"Strategy '{strategyId}' was not found.");
    }
}
