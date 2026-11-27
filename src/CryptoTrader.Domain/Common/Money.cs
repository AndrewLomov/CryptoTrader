namespace CryptoTrader.Domain.Common;

public readonly record struct Money
{
    public Money(decimal amount, string asset)
    {
        Amount = amount;
        Asset = Guard.Required(asset, nameof(asset));
    }

    public decimal Amount { get; }

    public string Asset { get; }

    public static Money operator +(Money left, Money right)
    {
        EnsureSameAsset(left, right);
        return new Money(left.Amount + right.Amount, left.Asset);
    }

    public static Money operator -(Money left, Money right)
    {
        EnsureSameAsset(left, right);
        return new Money(left.Amount - right.Amount, left.Asset);
    }

    public override string ToString() => $"{Amount} {Asset}";

    private static void EnsureSameAsset(Money left, Money right)
    {
        if (!string.Equals(left.Asset, right.Asset, StringComparison.Ordinal))
        {
            throw new DomainException("Money assets must match.");
        }
    }
}
