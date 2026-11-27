using CryptoTrader.Domain.Common;

namespace CryptoTrader.Application.Common;

internal static class ExchangeCode
{
    public static ExchangeId Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Exchange is required.", nameof(value));
        }

        return new ExchangeId(value.Trim().ToUpperInvariant());
    }
}
