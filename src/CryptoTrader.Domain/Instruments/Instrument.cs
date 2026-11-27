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
        bool isTradingEnabled,
        decimal? contractValue,
        string? contractValueCurrency,
        string? settlementCurrency,
        string? contractKind)
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
        ContractValue = contractValue;
        ContractValueCurrency = contractValueCurrency;
        SettlementCurrency = settlementCurrency;
        ContractKind = contractKind;
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

    public decimal? ContractValue { get; }

    public string? ContractValueCurrency { get; }

    public string? SettlementCurrency { get; }

    public string? ContractKind { get; }

    public bool HasCompleteLinearContractSpec =>
        Type == InstrumentType.LinearPerpetual
        && ContractValue is > 0
        && !string.IsNullOrWhiteSpace(ContractValueCurrency)
        && !string.IsNullOrWhiteSpace(SettlementCurrency)
        && string.Equals(ContractKind, "linear", StringComparison.Ordinal)
        && ContractMultiplier is not <= 0;

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
        bool isTradingEnabled,
        decimal? contractValue = null,
        string? contractValueCurrency = null,
        string? settlementCurrency = null,
        string? contractKind = null)
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

        if (type == InstrumentType.Spot)
        {
            if (contractMultiplier is not null)
            {
                throw new DomainException("Spot instruments cannot have a contract multiplier.");
            }

            if (contractValue is not null
                || contractValueCurrency is not null
                || settlementCurrency is not null
                || contractKind is not null)
            {
                throw new DomainException("Spot instruments cannot have contract value, currencies, or contract kind.");
            }
        }
        else if (type == InstrumentType.LinearPerpetual)
        {
            if (contractMultiplier is <= 0)
            {
                throw new DomainException("Linear perpetual contract multiplier must be greater than zero when it is set.");
            }

            var faceValue = contractValue is > 0
                && !string.IsNullOrWhiteSpace(contractValueCurrency)
                && !string.IsNullOrWhiteSpace(settlementCurrency)
                && contractKind is not null
                && string.Equals(contractKind.Trim(), "linear", StringComparison.OrdinalIgnoreCase);
            if (contractMultiplier is null && !faceValue)
            {
                throw new DomainException("Linear perpetual instruments require a contract multiplier greater than zero or a complete linear contract value.");
            }
        }

        if (contractValue is not null)
        {
            Guard.Positive(contractValue.Value, nameof(contractValue));
        }

        var storedContractValueCurrency = contractValueCurrency is null
            ? null
            : Guard.Required(contractValueCurrency, nameof(contractValueCurrency));
        var storedSettlementCurrency = settlementCurrency is null
            ? null
            : Guard.Required(settlementCurrency, nameof(settlementCurrency));
        string? storedContractKind = null;
        if (contractKind is not null)
        {
            var kind = Guard.Required(contractKind, nameof(contractKind));
            if (!string.Equals(kind, "linear", StringComparison.OrdinalIgnoreCase))
            {
                throw new DomainException("Contract kind must be linear.");
            }

            storedContractKind = "linear";
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
            isTradingEnabled,
            contractValue,
            storedContractValueCurrency,
            storedSettlementCurrency,
            storedContractKind);
    }

    public decimal FloorQuantity(decimal quantity) => DecimalRounding.FloorToStep(quantity, QuantityStep);

    public decimal FloorPrice(decimal price) => DecimalRounding.FloorToStep(price, PriceTick);
}
