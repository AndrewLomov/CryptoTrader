using CryptoTrader.Domain.Common;

namespace CryptoTrader.Domain.MarketData;

public sealed record Candle
{
    private Candle(
        ExchangeId exchange,
        InstrumentId instrument,
        Timeframe timeframe,
        DateTimeOffset openTimeUtc,
        DateTimeOffset closeTimeUtc,
        decimal open,
        decimal high,
        decimal low,
        decimal close,
        decimal volume,
        bool isClosed)
    {
        Exchange = exchange;
        Instrument = instrument;
        Timeframe = timeframe;
        OpenTimeUtc = openTimeUtc;
        CloseTimeUtc = closeTimeUtc;
        Open = open;
        High = high;
        Low = low;
        Close = close;
        Volume = volume;
        IsClosed = isClosed;
    }

    public ExchangeId Exchange { get; }

    public InstrumentId Instrument { get; }

    public Timeframe Timeframe { get; }

    public DateTimeOffset OpenTimeUtc { get; }

    public DateTimeOffset CloseTimeUtc { get; }

    public decimal Open { get; }

    public decimal High { get; }

    public decimal Low { get; }

    public decimal Close { get; }

    public decimal Volume { get; }

    public bool IsClosed { get; }

    public static Candle Create(
        ExchangeId exchange,
        InstrumentId instrument,
        Timeframe timeframe,
        DateTimeOffset openTimeUtc,
        DateTimeOffset closeTimeUtc,
        decimal open,
        decimal high,
        decimal low,
        decimal close,
        decimal volume,
        bool isClosed)
    {
        var canonical = Timeframe.Parse(timeframe.Code);
        if (canonical.Duration != timeframe.Duration)
        {
            throw new DomainException("Timeframe duration does not match its code.");
        }

        openTimeUtc = Guard.Utc(openTimeUtc, nameof(openTimeUtc));
        closeTimeUtc = Guard.Utc(closeTimeUtc, nameof(closeTimeUtc));
        if (openTimeUtc >= closeTimeUtc)
        {
            throw new DomainException("Candle open time must be earlier than close time.");
        }

        EnsurePrice(open, nameof(open));
        EnsurePrice(high, nameof(high));
        EnsurePrice(low, nameof(low));
        EnsurePrice(close, nameof(close));
        Guard.NotNegative(volume, nameof(volume));
        if (high < open || high < close || high < low)
        {
            throw new DomainException("Candle high must be greater than or equal to open, close, and low.");
        }

        if (low > open || low > close || low > high)
        {
            throw new DomainException("Candle low must be less than or equal to open, close, and high.");
        }

        return new Candle(
            exchange,
            instrument,
            canonical,
            openTimeUtc,
            closeTimeUtc,
            open,
            high,
            low,
            close,
            volume,
            isClosed);
    }

    public void EnsureClosed()
    {
        if (!IsClosed)
        {
            throw new DomainException("Candle is not closed.");
        }
    }

    private static void EnsurePrice(decimal value, string name)
    {
        if (value <= 0)
        {
            throw new DomainException($"Candle {name} must be greater than zero.");
        }
    }
}
