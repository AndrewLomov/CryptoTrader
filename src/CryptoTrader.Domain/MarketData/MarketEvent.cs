using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.Trading;

namespace CryptoTrader.Domain.MarketData;

public abstract record MarketEvent
{
    protected MarketEvent(
        ExchangeId exchange,
        InstrumentId instrument,
        DateTimeOffset exchangeTimeUtc,
        DateTimeOffset receivedAtUtc)
    {
        Exchange = exchange;
        Instrument = instrument;
        ExchangeTimeUtc = Guard.Utc(exchangeTimeUtc, nameof(exchangeTimeUtc));
        ReceivedAtUtc = Guard.Utc(receivedAtUtc, nameof(receivedAtUtc));
    }

    public ExchangeId Exchange { get; }

    public InstrumentId Instrument { get; }

    public DateTimeOffset ExchangeTimeUtc { get; }

    public DateTimeOffset ReceivedAtUtc { get; }
}

public sealed record CandleObserved : MarketEvent
{
    public CandleObserved(
        ExchangeId exchange,
        InstrumentId instrument,
        DateTimeOffset exchangeTimeUtc,
        DateTimeOffset receivedAtUtc,
        Candle candle)
        : base(exchange, instrument, exchangeTimeUtc, receivedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(candle);
        if (candle.Exchange != exchange || candle.Instrument != instrument)
        {
            throw new DomainException("Candle event does not match the instrument.");
        }

        Candle = candle;
    }

    public Candle Candle { get; }
}

public sealed record TickerUpdated : MarketEvent
{
    public TickerUpdated(
        ExchangeId exchange,
        InstrumentId instrument,
        DateTimeOffset exchangeTimeUtc,
        DateTimeOffset receivedAtUtc,
        Ticker ticker)
        : base(exchange, instrument, exchangeTimeUtc, receivedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(ticker);
        if (ticker.Exchange != exchange || ticker.Instrument != instrument)
        {
            throw new DomainException("Ticker event does not match the instrument.");
        }

        Ticker = ticker;
    }

    public Ticker Ticker { get; }
}

public sealed record PublicTradeObserved : MarketEvent
{
    public PublicTradeObserved(
        ExchangeId exchange,
        InstrumentId instrument,
        DateTimeOffset exchangeTimeUtc,
        DateTimeOffset receivedAtUtc,
        Price price,
        Quantity quantity,
        OrderSide? side)
        : base(exchange, instrument, exchangeTimeUtc, receivedAtUtc)
    {
        if (quantity.Value <= 0)
        {
            throw new DomainException("Trade quantity must be greater than zero.");
        }

        if (side is not null)
        {
            Guard.Defined(side.Value, nameof(side));
        }

        Price = price;
        Quantity = quantity;
        Side = side;
    }

    public Price Price { get; }

    public Quantity Quantity { get; }

    public OrderSide? Side { get; }
}

public sealed record OrderBookUpdated : MarketEvent
{
    public OrderBookUpdated(
        ExchangeId exchange,
        InstrumentId instrument,
        DateTimeOffset exchangeTimeUtc,
        DateTimeOffset receivedAtUtc,
        OrderBookSnapshot orderBook)
        : base(exchange, instrument, exchangeTimeUtc, receivedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(orderBook);
        if (orderBook.Exchange != exchange || orderBook.Instrument != instrument)
        {
            throw new DomainException("Order book event does not match the instrument.");
        }

        OrderBook = orderBook;
    }

    public OrderBookSnapshot OrderBook { get; }
}

public sealed record FundingRateUpdated : MarketEvent
{
    public FundingRateUpdated(
        ExchangeId exchange,
        InstrumentId instrument,
        DateTimeOffset exchangeTimeUtc,
        DateTimeOffset receivedAtUtc,
        FundingRate fundingRate)
        : base(exchange, instrument, exchangeTimeUtc, receivedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(fundingRate);
        if (fundingRate.Exchange != exchange || fundingRate.Instrument != instrument)
        {
            throw new DomainException("Funding event does not match the instrument.");
        }

        FundingRate = fundingRate;
    }

    public FundingRate FundingRate { get; }
}
