using CryptoTrader.Domain.Strategies;

namespace CryptoTrader.Strategies.Abstractions;

public interface ITradingStrategy
{
    string Id { get; }

    string Version { get; }

    string DisplayName { get; }

    ValueTask<IReadOnlyList<TradeSignal>> EvaluateAsync(
        StrategyContext context,
        CancellationToken cancellationToken);
}
