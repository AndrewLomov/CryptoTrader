using CryptoTrader.Application.Abstractions.Paper;
using CryptoTrader.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CryptoTrader.Infrastructure.Persistence.Repositories;

internal sealed class PaperTradingControlRepository : IPaperTradingControl
{
    public const int RowId = 1;

    private readonly CryptoTraderDbContext _db;

    public PaperTradingControlRepository(CryptoTraderDbContext db)
    {
        _db = db;
    }

    public async Task<PaperTradingControlState> GetAsync(CancellationToken cancellationToken)
    {
        var row = await _db.PaperTradingControls.AsNoTracking().FirstOrDefaultAsync(item => item.Id == RowId, cancellationToken);
        return row is null
            ? new PaperTradingControlState(false, null, null)
            : new PaperTradingControlState(row.EmergencyStop, row.Reason, row.UpdatedAtUtc);
    }

    public Task EngageAsync(string reason, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return SaveAsync(true, Trim(reason), updatedAtUtc, cancellationToken);
    }

    public Task ReleaseAsync(DateTimeOffset updatedAtUtc, CancellationToken cancellationToken) =>
        SaveAsync(false, null, updatedAtUtc, cancellationToken);

    private async Task SaveAsync(bool emergencyStop, string? reason, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken)
    {
        var row = await _db.PaperTradingControls.FirstOrDefaultAsync(item => item.Id == RowId, cancellationToken);
        if (row is null)
        {
            _db.PaperTradingControls.Add(new PaperTradingControlRow
            {
                Id = RowId,
                EmergencyStop = emergencyStop,
                Reason = reason,
                UpdatedAtUtc = updatedAtUtc,
            });
        }
        else
        {
            row.EmergencyStop = emergencyStop;
            row.Reason = reason;
            row.UpdatedAtUtc = updatedAtUtc;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string Trim(string reason)
    {
        var text = reason.Trim();
        return text.Length <= 512 ? text : text[..512];
    }
}
