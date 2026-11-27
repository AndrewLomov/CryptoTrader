using System.Text.Json;
using CryptoTrader.Domain.Common;

namespace CryptoTrader.Domain.Strategies;

public enum StrategyInstanceStatus
{
    Created = 1,
    Running = 2,
    Stopped = 3,
}

public sealed class StrategyInstance : AggregateRoot
{
    private StrategyInstance(
        StrategyInstanceId id,
        string strategyId,
        string strategyVersion,
        InstrumentId instrumentId,
        string parametersJson,
        StrategyInstanceStatus status,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        Id = id;
        StrategyId = strategyId;
        StrategyVersion = strategyVersion;
        InstrumentId = instrumentId;
        ParametersJson = parametersJson;
        Status = status;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public StrategyInstanceId Id { get; }

    public string StrategyId { get; }

    public string StrategyVersion { get; }

    public InstrumentId InstrumentId { get; }

    public string ParametersJson { get; }

    public StrategyInstanceStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static StrategyInstance Create(
        StrategyInstanceId id,
        string strategyId,
        string strategyVersion,
        InstrumentId instrumentId,
        string parametersJson,
        DateTimeOffset now)
    {
        now = Guard.Utc(now, nameof(now));
        return new StrategyInstance(
            id,
            Guard.Required(strategyId, nameof(strategyId)),
            Guard.Required(strategyVersion, nameof(strategyVersion)),
            instrumentId,
            ValidateParameters(parametersJson),
            StrategyInstanceStatus.Created,
            now,
            now);
    }

    public static StrategyInstance Rehydrate(
        StrategyInstanceId id,
        string strategyId,
        string strategyVersion,
        InstrumentId instrumentId,
        string parametersJson,
        StrategyInstanceStatus status,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        Guard.Defined(status, nameof(status));
        createdAtUtc = Guard.Utc(createdAtUtc, nameof(createdAtUtc));
        updatedAtUtc = Guard.Utc(updatedAtUtc, nameof(updatedAtUtc));
        if (updatedAtUtc < createdAtUtc)
        {
            throw new DomainException("Strategy instance update time cannot be earlier than creation time.");
        }

        return new StrategyInstance(
            id,
            Guard.Required(strategyId, nameof(strategyId)),
            Guard.Required(strategyVersion, nameof(strategyVersion)),
            instrumentId,
            ValidateParameters(parametersJson),
            status,
            createdAtUtc,
            updatedAtUtc);
    }

    public void Start(DateTimeOffset now)
    {
        now = Guard.Utc(now, nameof(now));
        if (Status is not (StrategyInstanceStatus.Created or StrategyInstanceStatus.Stopped))
        {
            throw new DomainException($"Strategy instance '{Id}' cannot start from {Status}.");
        }

        Status = StrategyInstanceStatus.Running;
        UpdatedAtUtc = now;
    }

    public void Stop(DateTimeOffset now)
    {
        now = Guard.Utc(now, nameof(now));
        if (Status != StrategyInstanceStatus.Running)
        {
            throw new DomainException($"Strategy instance '{Id}' cannot stop from {Status}.");
        }

        Status = StrategyInstanceStatus.Stopped;
        UpdatedAtUtc = now;
    }

    private static string ValidateParameters(string parametersJson)
    {
        var json = Guard.Required(parametersJson, nameof(parametersJson));
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new DomainException("Strategy parameters must be a JSON object.");
            }
        }
        catch (JsonException exception)
        {
            throw new DomainException("Strategy parameters must be valid JSON.", exception);
        }

        return json;
    }
}
