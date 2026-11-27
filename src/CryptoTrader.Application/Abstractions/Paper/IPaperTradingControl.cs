namespace CryptoTrader.Application.Abstractions.Paper;

/// <summary>
/// Process-wide paper trading latch. It is not a field of one exchange account.
/// A new ledger created while the latch is engaged starts stopped.
/// </summary>
public interface IPaperTradingControl
{
    Task<PaperTradingControlState> GetAsync(CancellationToken cancellationToken);

    Task EngageAsync(string reason, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken);

    Task ReleaseAsync(DateTimeOffset updatedAtUtc, CancellationToken cancellationToken);
}

public sealed record PaperTradingControlState(bool EmergencyStop, string? Reason, DateTimeOffset? UpdatedAtUtc);
