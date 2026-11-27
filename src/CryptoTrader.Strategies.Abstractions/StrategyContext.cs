using CryptoTrader.Domain.Instruments;
using CryptoTrader.Domain.MarketData;

namespace CryptoTrader.Strategies.Abstractions;

public sealed record StrategyContext(
    Instrument Instrument,
    DateTimeOffset AsOfUtc,
    string ParametersJson,
    IReadOnlyList<Candle> ClosedWorkingCandles,
    IReadOnlyList<Candle> ClosedHigherTimeframeCandles,
    bool HasOpenPosition);
