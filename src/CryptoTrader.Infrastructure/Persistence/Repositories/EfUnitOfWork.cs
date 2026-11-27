using CryptoTrader.Application.Abstractions.Persistence;

namespace CryptoTrader.Infrastructure.Persistence.Repositories;

internal sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly CryptoTraderDbContext _db;

    public EfUnitOfWork(CryptoTraderDbContext db)
    {
        _db = db;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => _db.SaveChangesAsync(cancellationToken);
}
