using CryptoTrader.Domain.Common;

namespace CryptoTrader.Domain.Strategies;

public sealed record PortfolioSnapshot
{
    public PortfolioSnapshot(
        decimal equity,
        decimal availableCash,
        string quoteAsset,
        decimal dayPnl,
        decimal drawdownFraction,
        int openCount,
        decimal grossExposure,
        bool hasPosition,
        bool positionIsLosing,
        decimal? openQuantity,
        bool emergencyStop,
        IReadOnlyCollection<Guid> approvedSignalIds)
    {
        Guard.NotNegative(equity, nameof(equity));
        Guard.NotNegative(availableCash, nameof(availableCash));
        Guard.NotNegative(drawdownFraction, nameof(drawdownFraction));
        Guard.NotNegative(grossExposure, nameof(grossExposure));
        if (openCount < 0)
        {
            throw new DomainException("Open count cannot be negative.");
        }

        if (!hasPosition && positionIsLosing)
        {
            throw new DomainException("A missing position cannot be losing.");
        }

        if (hasPosition && openQuantity is not > 0)
        {
            throw new DomainException("An open position requires a positive quantity.");
        }

        if (!hasPosition && openQuantity is not null)
        {
            throw new DomainException("A missing position cannot have a quantity.");
        }

        ArgumentNullException.ThrowIfNull(approvedSignalIds);
        if (approvedSignalIds.Any(signalId => signalId == Guid.Empty))
        {
            throw new DomainException("Approved signal ids cannot be empty.");
        }

        Equity = equity;
        AvailableCash = availableCash;
        QuoteAsset = Guard.Required(quoteAsset, nameof(quoteAsset));
        DayPnl = dayPnl;
        DrawdownFraction = drawdownFraction;
        OpenCount = openCount;
        GrossExposure = grossExposure;
        HasPosition = hasPosition;
        PositionIsLosing = positionIsLosing;
        OpenQuantity = openQuantity;
        EmergencyStop = emergencyStop;
        ApprovedSignalIds = approvedSignalIds;
    }

    public decimal Equity { get; }

    public decimal AvailableCash { get; }

    public string QuoteAsset { get; }

    public decimal DayPnl { get; }

    public decimal DrawdownFraction { get; }

    public int OpenCount { get; }

    public decimal GrossExposure { get; }

    public bool HasPosition { get; }

    public bool PositionIsLosing { get; }

    public decimal? OpenQuantity { get; }

    public bool EmergencyStop { get; }

    public IReadOnlyCollection<Guid> ApprovedSignalIds { get; }
}

public sealed record MarketSnapshot
{
    public MarketSnapshot(
        DateTimeOffset asOfUtc,
        DateTimeOffset? lastEventUtc,
        bool healthy,
        bool stale,
        decimal? bid,
        decimal? ask,
        decimal? last)
    {
        asOfUtc = Guard.Utc(asOfUtc, nameof(asOfUtc));
        if (lastEventUtc is not null)
        {
            lastEventUtc = Guard.Utc(lastEventUtc.Value, nameof(lastEventUtc));
        }

        if (stale && healthy)
        {
            throw new DomainException("Stale market data cannot be marked healthy.");
        }

        EnsureOptionalPrice(bid, nameof(bid));
        EnsureOptionalPrice(ask, nameof(ask));
        EnsureOptionalPrice(last, nameof(last));
        AsOfUtc = asOfUtc;
        LastEventUtc = lastEventUtc;
        Healthy = healthy;
        Stale = stale;
        Bid = bid;
        Ask = ask;
        Last = last;
    }

    public DateTimeOffset AsOfUtc { get; }

    public DateTimeOffset? LastEventUtc { get; }

    public bool Healthy { get; }

    public bool Stale { get; }

    public decimal? Bid { get; }

    public decimal? Ask { get; }

    public decimal? Last { get; }

    private static void EnsureOptionalPrice(decimal? value, string name)
    {
        if (value is <= 0)
        {
            throw new DomainException($"Market {name} must be greater than zero when present.");
        }
    }
}
