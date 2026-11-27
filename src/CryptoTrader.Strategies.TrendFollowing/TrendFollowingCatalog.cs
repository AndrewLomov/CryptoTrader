using CryptoTrader.Strategies.Abstractions;

namespace CryptoTrader.Strategies.TrendFollowing;

public sealed class TrendFollowingCatalog : IStrategyCatalog
{
    private static readonly IReadOnlyList<StrategyDescriptor> Descriptors =
    [
        new(
            TrendFollowingStrategy.StrategyId,
            TrendFollowingStrategy.StrategyVersion,
            TrendFollowingStrategy.StrategyDisplayName),
    ];

    public IReadOnlyList<StrategyDescriptor> List() => Descriptors;

    public ITradingStrategy Create(string strategyId, string parametersJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(strategyId);
        if (!string.Equals(strategyId, TrendFollowingStrategy.StrategyId, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Strategy '{strategyId}' is not registered.", nameof(strategyId));
        }

        return new TrendFollowingStrategy(parametersJson);
    }
}
