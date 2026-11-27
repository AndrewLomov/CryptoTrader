using CryptoTrader.Strategies.Abstractions;

namespace CryptoTrader.Application.Abstractions.Backtesting;

public interface IBacktestEngine
{
    Task<BacktestReport> RunAsync(
        BacktestRequest request,
        ITradingStrategy strategy,
        CancellationToken cancellationToken);
}
