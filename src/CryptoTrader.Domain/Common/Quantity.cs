namespace CryptoTrader.Domain.Common;

public readonly record struct Quantity
{
    public Quantity(decimal value)
    {
        if (value < 0)
        {
            throw new DomainException("Quantity cannot be negative.");
        }

        Value = value;
    }

    public static Quantity Zero { get; } = new(0);

    public decimal Value { get; }

    public static Quantity operator +(Quantity left, Quantity right) => new(left.Value + right.Value);

    public static Quantity operator -(Quantity left, Quantity right) => new(left.Value - right.Value);

    public static bool operator >(Quantity left, Quantity right) => left.Value > right.Value;

    public static bool operator <(Quantity left, Quantity right) => left.Value < right.Value;

    public static bool operator >=(Quantity left, Quantity right) => left.Value >= right.Value;

    public static bool operator <=(Quantity left, Quantity right) => left.Value <= right.Value;

    public override string ToString() => Value.ToString();
}
