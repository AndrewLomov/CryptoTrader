using CryptoTrader.Domain.Common;

namespace CryptoTrader.Domain.Instruments;

public sealed record Instrument
{
    private Instrument(
        InstrumentId id,
        ExchangeId exchange,
        string exchangeSymbol,
        string baseAsset,
        string quoteAsset,
        InstrumentType type,
        decimal priceTick,
        decimal quantityStep,
        decimal minQuantity,
        decimal? minNotional,
        decimal? contractMultiplier,
        bool isTradingEnabled)
    {
        Id = id;
        Exchange = exchange;
        ExchangeSymbol = exchangeSymbol;
        BaseAsset = baseAsset;
        QuoteAsset = quoteAsset;
        Type = type;
        PriceTick = priceTick;
        QuantityStep = quantityStep;
        MinQuantity = minQuantity;
        MinNotional = minNotional;
        ContractMultiplier = contractMultiplier;
        IsTradingEnabled = isTradingEnabled;
    }

    public InstrumentId Id { get; }

    public ExchangeId Exchange { get; }

    public string ExchangeSymbol { get; }

    public string BaseAsset { get; }

    public string QuoteAsset { get; }

    public InstrumentType Type { get; }

    public decimal PriceTick { get; }

    public decimal QuantityStep { get; }

    public decimal MinQuantity { get; }

    public decimal? MinNotional { get; }

    public decimal? ContractMultiplier { get; }

    public bool IsTradingEnabled { get; }

    public static Instrument Create(
        InstrumentId id,
        ExchangeId exchange,
        string exchangeSymbol,
        string baseAsset,
        string quoteAsset,
        InstrumentType type,
        decimal priceTick,
        decimal quantityStep,
        decimal minQuantity,
        decimal? minNotional,
        decimal? contractMultiplier,
        bool isTradingEnabled)
    {
        Guard.Defined(type, nameof(type));
        exchangeSymbol = Guard.Required(exchangeSymbol, nameof(exchangeSymbol));
        baseAsset = Guard.Required(baseAsset, nameof(baseAsset));
        quoteAsset = Guard.Required(quoteAsset, nameof(quoteAsset));
        if (string.Equals(baseAsset, quoteAsset, StringComparison.Ordinal))
        {
            throw new DomainException("Base and quote assets must differ.");
        }

        Guard.Positive(priceTick, nameof(priceTick));
        Guard.Positive(quantityStep, nameof(quantityStep));
        Guard.Positive(minQuantity, nameof(minQuantity));
        if (minNotional is not null)
        {
            Guard.Positive(minNotional.Value, nameof(minNotional));
        }

        switch (type)
        {
            case InstrumentType.Spot when contractMultiplier is not null:
                throw new DomainException("Spot instruments cannot have a contract multiplier.");
            case InstrumentType.LinearPerpetual when contractMultiplier is null || contractMultiplier <= 0:
                throw new DomainException("Linear perpetual instruments require a contract multiplier greater than zero.");
        }

        return new Instrument(
            id,
            exchange,
            exchangeSymbol,
            baseAsset,
            quoteAsset,
            type,
            priceTick,
            quantityStep,
            minQuantity,
            minNotional,
            contractMultiplier,
            isTradingEnabled);
    }

    public decimal FloorQuantity(decimal quantity) => DecimalRounding.FloorToStep(quantity, QuantityStep);

    public decimal FloorPrice(decimal price) => DecimalRounding.FloorToStep(price, PriceTick);
}
