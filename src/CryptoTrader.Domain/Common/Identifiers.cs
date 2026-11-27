namespace CryptoTrader.Domain.Common;

public readonly record struct ExchangeId
{
    public ExchangeId(string value)
    {
        Value = Guard.Required(value, nameof(ExchangeId));
    }

    public static ExchangeId Bybit { get; } = new("BYBIT");

    public static ExchangeId Okx { get; } = new("OKX");

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct InstrumentId
{
    public InstrumentId(string value)
    {
        Value = Guard.Required(value, nameof(InstrumentId));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct OrderId
{
    public OrderId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainException("OrderId cannot be empty.");
        }

        Value = value;
    }

    public Guid Value { get; }

    public static OrderId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}

public readonly record struct ExecutionId
{
    public ExecutionId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainException("ExecutionId cannot be empty.");
        }

        Value = value;
    }

    public Guid Value { get; }

    public static ExecutionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}

public readonly record struct StrategyInstanceId
{
    public StrategyInstanceId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainException("StrategyInstanceId cannot be empty.");
        }

        Value = value;
    }

    public Guid Value { get; }

    public static StrategyInstanceId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}

public readonly record struct TradingAccountId
{
    public TradingAccountId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainException("TradingAccountId cannot be empty.");
        }

        Value = value;
    }

    public Guid Value { get; }

    public static TradingAccountId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
