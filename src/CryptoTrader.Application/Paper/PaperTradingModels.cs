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
