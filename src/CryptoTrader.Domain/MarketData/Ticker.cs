using CryptoTrader.Domain.Common;

namespace CryptoTrader.Domain.MarketData;

public sealed record Ticker
{
    private Ticker(
        ExchangeId exchange,
        InstrumentId instrument,
        decimal? bid,
        decimal? ask,
        decimal last,
        DateTimeOffset exchangeTimeUtc,
        DateTimeOffset receivedAtUtc)
    {
        Exchange = exchange;
        Instrument = instrument;
        Bid = bid;
        Ask = ask;
        Last = last;
        ExchangeTimeUtc = exchangeTimeUtc;
        ReceivedAtUtc = receivedAtUtc;
    }

    public ExchangeId Exchange { get; }

    public InstrumentId Instrument { get; }

    public decimal? Bid { get; }

    public decimal? Ask { get; }

    public decimal Last { get; }

    public DateTimeOffset ExchangeTimeUtc { get; }

    public DateTimeOffset ReceivedAtUtc { get; }

    public static Ticker Create(
        ExchangeId exchange,
        InstrumentId instrument,
        decimal? bid,
        decimal? ask,
        decimal last,
        DateTimeOffset exchangeTimeUtc,
        DateTimeOffset receivedAtUtc)
    {
        EnsureOptionalPrice(bid, nameof(bid));
        EnsureOptionalPrice(ask, nameof(ask));
        if (last <= 0)
        {
            throw new DomainException("Ticker last price must be greater than zero.");
        }

        return new Ticker(
            exchange,
            instrument,
            bid,
            ask,
            last,
            Guard.Utc(exchangeTimeUtc, nameof(exchangeTimeUtc)),
            Guard.Utc(receivedAtUtc, nameof(receivedAtUtc)));
    }

    private static void EnsureOptionalPrice(decimal? value, string name)
    {
        if (value is <= 0)
        {
            throw new DomainException($"Ticker {name} must be greater than zero when present.");
        }
    }
}
