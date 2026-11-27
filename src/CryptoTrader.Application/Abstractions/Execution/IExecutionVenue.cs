using CryptoTrader.Domain.Instruments;
using CryptoTrader.Domain.Strategies;
using CryptoTrader.Domain.Trading;

namespace CryptoTrader.Application.Abstractions.Execution;

public interface IExecutionVenue
{
    TradingMode Mode { get; }

    Task<VenueExecutionResult> ExecuteAsync(
        ApprovedOrder order,
        Instrument instrument,
        MarketSnapshot market,
        CancellationToken cancellationToken);

    /// <summary>
    /// Paper reconciliation compares the ledger with saved paper positions and does not call an exchange.
    /// A network call must not run inside an open database transaction.
    /// </summary>
    Task ReconcileAsync(CancellationToken cancellationToken);
}
