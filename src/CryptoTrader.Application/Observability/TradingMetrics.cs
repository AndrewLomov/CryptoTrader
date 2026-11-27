using System.Diagnostics.Metrics;

namespace CryptoTrader.Application.Observability;

public sealed class TradingMetrics : IDisposable
{
    public const string MeterName = "CryptoTrader.Trading";

    private readonly Meter _meter = new(MeterName);
    private readonly object _equityLock = new();
    private long _approved;
    private long _rejected;
    private decimal _equity;

    public TradingMetrics()
    {
        _meter.CreateObservableCounter(
            "cryptotrader.signals.approved",
            () => Interlocked.Read(ref _approved),
            description: "Signals approved by the risk manager.");
        _meter.CreateObservableCounter(
            "cryptotrader.signals.rejected",
            () => Interlocked.Read(ref _rejected),
            description: "Signals rejected by the risk manager.");
        _meter.CreateObservableGauge(
            "cryptotrader.paper.equity",
            ObserveEquity,
            description: "Latest paper equity in the quote asset.");
    }

    public void RecordDecision(bool approved)
    {
        if (approved)
        {
            Interlocked.Increment(ref _approved);
        }
        else
        {
            Interlocked.Increment(ref _rejected);
        }
    }

    public void RecordEquity(decimal equity)
    {
        lock (_equityLock)
        {
            _equity = equity;
        }
    }

    public void Dispose() => _meter.Dispose();

    private double ObserveEquity()
    {
        decimal equity;
        lock (_equityLock)
        {
            equity = _equity;
        }

        return (double)equity;
    }
}
