using CryptoTrader.Domain.Trading;

namespace CryptoTrader.Application.Paper;

public sealed class PaperTradingGate
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public Task WaitAsync(CancellationToken cancellationToken) => _semaphore.WaitAsync(cancellationToken);

    public void Release() => _semaphore.Release();
}

public sealed record PaperTradingSnapshot(
    TradingAccount? Account,
    IReadOnlyList<Order> Orders,
    IReadOnlyList<Position> Positions)
{
    public static PaperTradingSnapshot Empty { get; } = new(null, [], []);
}

public sealed record EmergencyStopOutcome(bool EmergencyStop, DateTimeOffset OccurredAtUtc);

public enum PaperCandleDisposition
{
    Applied = 1,
    Skipped = 2,
}

/// <summary>
/// Result of one paper attempt. <see cref="PaperCandleDisposition.Skipped"/> is terminal:
/// the candle stays in history and must not be retried until it becomes eligible again.
/// </summary>
public readonly record struct PaperCandleResult(PaperCandleDisposition Disposition, string? Reason)
{
    public bool WasApplied => Disposition == PaperCandleDisposition.Applied;

    public static PaperCandleResult Applied(string? reason = null) => new(PaperCandleDisposition.Applied, reason);

    public static PaperCandleResult Skipped(string? reason) =>
        new(PaperCandleDisposition.Skipped, string.IsNullOrWhiteSpace(reason) ? "Paper trading skipped the candle." : reason.Trim());
}

public sealed record PaperAccountSummary(
    Guid AccountId,
    string Exchange,
    string QuoteAsset,
    decimal Cash,
    decimal Equity,
    decimal DailyPnl,
    decimal DrawdownFraction,
    bool EmergencyStop,
    int OpenPositions);
