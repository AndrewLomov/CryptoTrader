using CryptoTrader.Domain.MarketData;

namespace CryptoTrader.Exchanges.Abstractions.MarketData;

public enum CandleHistoryCompleteness
{
    Complete = 1,
    Incomplete = 2,
    Empty = 3,
    Unverified = 4,
}

public sealed record CandleHistory(
    IReadOnlyList<Candle> Candles,
    CandleHistoryCompleteness Completeness,
    string? Reason);

public sealed class IncompleteCandleHistoryException : InvalidOperationException
{
    public IncompleteCandleHistoryException(string message)
        : base(message)
    {
    }
}
