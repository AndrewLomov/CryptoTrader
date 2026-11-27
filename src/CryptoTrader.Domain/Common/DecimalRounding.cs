namespace CryptoTrader.Domain.Common;

public static class DecimalRounding
{
    public static decimal FloorToStep(decimal value, decimal step)
    {
        if (step <= 0)
        {
            throw new DomainException("Step must be greater than zero.");
        }

        if (value < 0)
        {
            throw new DomainException("Value cannot be negative.");
        }

        return decimal.Floor(value / step) * step;
    }
}
