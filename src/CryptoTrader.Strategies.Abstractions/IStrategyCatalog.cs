namespace CryptoTrader.Strategies.Abstractions;

public interface IStrategyCatalog
{
    IReadOnlyList<StrategyDescriptor> List();

    ITradingStrategy Create(string strategyId, string parametersJson);
}
