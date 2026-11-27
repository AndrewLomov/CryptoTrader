using CryptoTrader.Domain.Common;

namespace CryptoTrader.Domain.Trading;

public sealed class TradingAccount : AggregateRoot
{
    private TradingAccount(
        TradingAccountId id,
        ExchangeId exchange,
        string displayName,
        TradingMode mode,
        AccountConnectionStatus status,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        Id = id;
        Exchange = exchange;
        DisplayName = displayName;
        Mode = mode;
        Status = status;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public TradingAccountId Id { get; }

    public ExchangeId Exchange { get; }

    public string DisplayName { get; }

    public TradingMode Mode { get; private set; }

    public AccountConnectionStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static TradingAccount Create(
        TradingAccountId id,
        ExchangeId exchange,
        string displayName,
        TradingMode mode,
        DateTimeOffset now)
    {
        now = Guard.Utc(now, nameof(now));
        RejectLive(mode);
        return new TradingAccount(
            id,
            exchange,
            Guard.Required(displayName, nameof(displayName)),
            mode,
            AccountConnectionStatus.Disconnected,
            now,
            now);
    }

    public static TradingAccount Rehydrate(
        TradingAccountId id,
        ExchangeId exchange,
        string displayName,
        TradingMode mode,
        AccountConnectionStatus status,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        createdAtUtc = Guard.Utc(createdAtUtc, nameof(createdAtUtc));
        updatedAtUtc = Guard.Utc(updatedAtUtc, nameof(updatedAtUtc));
        if (updatedAtUtc < createdAtUtc)
        {
            throw new DomainException("Account update time cannot be earlier than creation time.");
        }

        RejectLive(mode);
        Guard.Defined(status, nameof(status));
        return new TradingAccount(
            id,
            exchange,
            Guard.Required(displayName, nameof(displayName)),
            mode,
            status,
            createdAtUtc,
            updatedAtUtc);
    }

    public void ChangeMode(TradingMode mode, DateTimeOffset now)
    {
        now = Guard.Utc(now, nameof(now));
        RejectLive(mode);
        if (mode == Mode)
        {
            return;
        }

        Mode = mode;
        UpdatedAtUtc = now;
    }

    public void UpdateStatus(AccountConnectionStatus status, DateTimeOffset now)
    {
        now = Guard.Utc(now, nameof(now));
        Guard.Defined(status, nameof(status));
        Status = status;
        UpdatedAtUtc = now;
    }

    private static void RejectLive(TradingMode mode)
    {
        Guard.Defined(mode, nameof(mode));
        if (mode == TradingMode.Live)
        {
            throw new DomainException("Live trading is disabled in CryptoTrader 0.1.");
        }
    }
}
