namespace CryptoTrader.Application.MarketData;

/// <summary>
/// Decides whether a closed candle may be stored as history and whether it may enter paper trading.
/// History and execution are separate: a stale candle is still a valid historical bar.
/// </summary>
public enum CandleAdmission
{
    Eligible = 0,
    NotClosed = 1,
    StaleClose = 2,
    ConsumerBehind = 3,
    FutureTimestamp = 4,
}

/// <summary>
/// Shared admission result for ingestion, startup replay, and the paper coordinator.
/// A candle is eligible when its close age is less than or equal to the stale window.
/// An age strictly greater than the window is stale. Queue lag uses the same boundary.
/// </summary>
public readonly record struct CandleProcessingDecision(
    bool CanPersistHistory,
    bool CanUpdatePaper,
    CandleAdmission Admission,
    string? Reason)
{
    /// <summary>
    /// Close or receipt times this far ahead of the local clock are still eligible.
    /// A larger lead is a future timestamp and must not enter paper trading.
    /// </summary>
    public static readonly TimeSpan FutureClockSkew = TimeSpan.FromSeconds(5);

    public static TimeSpan Window(int staleAfterSeconds)
    {
        if (staleAfterSeconds <= 0)
        {
            throw new InvalidOperationException("MarketData:StaleAfterSeconds must be greater than zero.");
        }

        return TimeSpan.FromSeconds(staleAfterSeconds);
    }

    /// <summary>
    /// Receipt time is optional. Live ingestion passes it so a backed-up channel cannot trade.
    /// Replay passes null: the durable rule is the candle close age, because receipt time is not stored.
    /// A short outage still admits a candle whose close is inside the window. A long outage skips it.
    /// </summary>
    public static CandleProcessingDecision Evaluate(
        bool isClosed,
        DateTimeOffset closeTimeUtc,
        DateTimeOffset? receivedAtUtc,
        DateTimeOffset utcNow,
        TimeSpan staleAfter) =>
        Evaluate(isClosed, closeTimeUtc, receivedAtUtc, utcNow, staleAfter, FutureClockSkew);

    public static CandleProcessingDecision Evaluate(
        bool isClosed,
        DateTimeOffset closeTimeUtc,
        DateTimeOffset? receivedAtUtc,
        DateTimeOffset utcNow,
        TimeSpan staleAfter,
        TimeSpan futureSkew)
    {
        if (staleAfter <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(staleAfter), "The stale window must be greater than zero.");
        }

        if (futureSkew < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(futureSkew), "The future clock skew cannot be negative.");
        }

        if (!isClosed)
        {
            return new CandleProcessingDecision(false, false, CandleAdmission.NotClosed, "Candle is not closed.");
        }

        if (receivedAtUtc is DateTimeOffset received && received - utcNow > futureSkew)
        {
            return Future("Candle receipt time is in the future.");
        }

        if (closeTimeUtc - utcNow > futureSkew)
        {
            return Future("Candle close time is in the future.");
        }

        if (receivedAtUtc is DateTimeOffset receivedAt && utcNow - receivedAt > staleAfter)
        {
            return new CandleProcessingDecision(
                true,
                false,
                CandleAdmission.ConsumerBehind,
                "Channel consumer is behind.");
        }

        if (utcNow - closeTimeUtc > staleAfter)
        {
            return new CandleProcessingDecision(
                true,
                false,
                CandleAdmission.StaleClose,
                "Closed candle is older than the stale window.");
        }

        return new CandleProcessingDecision(true, true, CandleAdmission.Eligible, null);
    }

    private static CandleProcessingDecision Future(string reason) =>
        new(true, false, CandleAdmission.FutureTimestamp, reason);
}
