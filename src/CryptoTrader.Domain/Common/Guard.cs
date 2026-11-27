namespace CryptoTrader.Domain.Common;

internal static class Guard
{
    public static string Required(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{name} cannot be empty.");
        }

        return value.Trim();
    }

    public static DateTimeOffset Utc(DateTimeOffset value, string name)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new DomainException($"{name} must be UTC.");
        }

        return value;
    }

    public static void Defined<TEnum>(TEnum value, string name)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new DomainException($"{name} is not a supported value.");
        }
    }

    public static void Positive(decimal value, string name)
    {
        if (value <= 0)
        {
            throw new DomainException($"{name} must be greater than zero.");
        }
    }

    public static void NotNegative(decimal value, string name)
    {
        if (value < 0)
        {
            throw new DomainException($"{name} cannot be negative.");
        }
    }

    public static void Percent(decimal value, string name)
    {
        if (value <= 0 || value > 100)
        {
            throw new DomainException($"{name} must be greater than 0 and less than or equal to 100.");
        }
    }
}
