using CryptoTrader.Application.Abstractions.Persistence;
using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.Risk;
using CryptoTrader.Domain.Strategies;
using CryptoTrader.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CryptoTrader.Infrastructure.Persistence.Repositories;

internal sealed class TradeSignalRepository : ITradeSignalRepository
{
    private readonly CryptoTraderDbContext _db;

    public TradeSignalRepository(CryptoTraderDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(
        TradeSignal signal,
        StrategyInstanceId strategyInstanceId,
        RiskDecision decision,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(signal);
        ArgumentNullException.ThrowIfNull(decision);
        _db.TradeSignals.Add(new TradeSignalRow
        {
            SignalId = signal.SignalId,
            StrategyInstanceId = strategyInstanceId.Value,
            StrategyId = signal.StrategyId,
            StrategyVersion = signal.StrategyVersion,
            InstrumentId = signal.InstrumentId.Value,
            Action = (int)signal.Action,
            OccurredAtUtc = signal.OccurredAtUtc,
            ReferencePrice = signal.ReferencePrice.Value,
            StopLossPrice = signal.StopLossPrice?.Value,
            TakeProfitPrice = signal.TakeProfitPrice?.Value,
            Atr = signal.Atr,
            Explanation = signal.Explanation,
        });
        _db.RiskDecisions.Add(new RiskDecisionRow
        {
            SignalId = signal.SignalId,
            StrategyInstanceId = strategyInstanceId.Value,
            Approved = decision.Approved,
            ApprovedQuantity = decision.ApprovedQuantity?.Value,
            EstimatedMaxLoss = decision.EstimatedMaxLoss,
            AppliedConstraintsJson = PersistenceJson.Serialize(decision.AppliedConstraints.ToArray()),
            RejectionCode = decision.RejectionCode,
            Reason = decision.Reason,
            OccurredAtUtc = signal.OccurredAtUtc,
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> ExistsAsync(Guid signalId, CancellationToken cancellationToken) =>
        _db.TradeSignals.AsNoTracking().AnyAsync(row => row.SignalId == signalId, cancellationToken);
}
