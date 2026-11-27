using CryptoTrader.Application.Abstractions.Persistence;
using CryptoTrader.Domain.Common;
using CryptoTrader.Infrastructure.Persistence.Entities;

namespace CryptoTrader.Infrastructure.Persistence.Repositories;

internal sealed class RiskEventRepository : IRiskEventRepository
{
    private readonly CryptoTraderDbContext _db;

    public RiskEventRepository(CryptoTraderDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(DomainEvent riskEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(riskEvent);
        var eventType = riskEvent.GetType();
        _db.RiskEvents.Add(new RiskEventRow
        {
            Id = riskEvent.EventId,
            EventType = eventType.FullName ?? eventType.Name,
            OccurredAtUtc = riskEvent.OccurredAtUtc,
            CorrelationId = riskEvent.CorrelationId,
            CausationId = riskEvent.CausationId,
            PayloadJson = PersistenceJson.SerializeRuntime(riskEvent),
        });
        await _db.SaveChangesAsync(cancellationToken);
    }
}
