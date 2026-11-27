using CryptoTrader.Application.Abstractions.Persistence;
using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.Trading;
using CryptoTrader.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CryptoTrader.Infrastructure.Persistence.Repositories;

internal sealed class TradingAccountRepository : ITradingAccountRepository
{
    private readonly CryptoTraderDbContext _db;

    public TradingAccountRepository(CryptoTraderDbContext db)
    {
        _db = db;
    }

    public async Task<TradingAccount?> FindAsync(TradingAccountId id, CancellationToken cancellationToken)
    {
        var key = id.Value;
        var row = await _db.TradingAccounts.AsNoTracking().FirstOrDefaultAsync(item => item.Id == key, cancellationToken);
        return row is null ? null : ToAccount(row);
    }

    public async Task UpsertAsync(TradingAccount account, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        await ExchangeCatalog.EnsureAsync(_db, account.Exchange.Value, cancellationToken);
        var key = account.Id.Value;
        var row = await _db.TradingAccounts.FirstOrDefaultAsync(item => item.Id == key, cancellationToken);
        if (row is null)
        {
            _db.TradingAccounts.Add(ToRow(account));
        }
        else
        {
            Copy(account, row);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TradingAccount>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await _db.TradingAccounts
            .AsNoTracking()
            .OrderBy(item => item.DisplayName)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        return rows.Select(ToAccount).ToArray();
    }

    private static TradingAccountRow ToRow(TradingAccount account) => new()
    {
        Id = account.Id.Value,
        ExchangeId = account.Exchange.Value,
        DisplayName = account.DisplayName,
        Mode = (int)account.Mode,
        Status = (int)account.Status,
        CreatedAtUtc = account.CreatedAtUtc,
        UpdatedAtUtc = account.UpdatedAtUtc,
    };

    private static void Copy(TradingAccount account, TradingAccountRow row)
    {
        row.ExchangeId = account.Exchange.Value;
        row.DisplayName = account.DisplayName;
        row.Mode = (int)account.Mode;
        row.Status = (int)account.Status;
        row.CreatedAtUtc = account.CreatedAtUtc;
        row.UpdatedAtUtc = account.UpdatedAtUtc;
    }

    private static TradingAccount ToAccount(TradingAccountRow row) => TradingAccount.Rehydrate(
        new TradingAccountId(row.Id),
        new ExchangeId(row.ExchangeId),
        row.DisplayName,
        StoredEnum.FromInt32<TradingMode>(row.Mode, "mode"),
        StoredEnum.FromInt32<AccountConnectionStatus>(row.Status, "status"),
        row.CreatedAtUtc,
        row.UpdatedAtUtc);
}
