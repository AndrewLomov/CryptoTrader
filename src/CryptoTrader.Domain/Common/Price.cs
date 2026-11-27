namespace CryptoTrader.Domain.Common;

public readonly record struct Price
{
    public Price(decimal value)
    {
        if (value <= 0)
        {
            throw new DomainException("Price must be greater than zero.");
        }

        Value = value;
    }

    public decimal Value { get; }

    public static bool operator >(Price left, Price right) => left.Value > right.Value;

    public static bool operator <(Price left, Price right) => left.Value < right.Value;

    public static bool operator >=(Price left, Price right) => left.Value >= right.Value;

    public static bool operator <=(Price left, Price right) => left.Value <= right.Value;

    public override string ToString() => Value.ToString();
}
