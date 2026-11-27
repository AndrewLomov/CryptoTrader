using CryptoTrader.Domain.Common;

namespace CryptoTrader.Domain.MarketData;

public sealed record FundingRate
{
    private FundingRate(
        ExchangeId exchange,
        InstrumentId instrument,
        decimal rate,
        DateTimeOffset fundingTimeUtc,
        DateTimeOffset receivedAtUtc)
    {
        Exchange = exchange;
        Instrument = instrument;
        Rate = rate;
        FundingTimeUtc = fundingTimeUtc;
        ReceivedAtUtc = receivedAtUtc;
    }

    public ExchangeId Exchange { get; }

    public InstrumentId Instrument { get; }

    public decimal Rate { get; }

    public DateTimeOffset FundingTimeUtc { get; }

    public DateTimeOffset ReceivedAtUtc { get; }

    public static FundingRate Create(
        ExchangeId exchange,
        InstrumentId instrument,
        decimal rate,
        DateTimeOffset fundingTimeUtc,
        DateTimeOffset receivedAtUtc)
    {
        return new FundingRate(
            exchange,
            instrument,
            rate,
            Guard.Utc(fundingTimeUtc, nameof(fundingTimeUtc)),
            Guard.Utc(receivedAtUtc, nameof(receivedAtUtc)));
    }
}
