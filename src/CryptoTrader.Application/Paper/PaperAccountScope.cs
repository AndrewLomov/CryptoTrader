using System.Security.Cryptography;
using System.Text;
using CryptoTrader.Domain.Common;

namespace CryptoTrader.Application.Paper;

/// <summary>
/// One paper ledger per exchange and quote asset. USDT and USDC are never converted into each other.
/// The account id is derived from that scope so a restart finds the same row.
/// </summary>
public static class PaperAccountScope
{
    public static string NormalizeQuote(string quoteAsset)
    {
        var quote = quoteAsset?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(quote))
        {
            throw new ArgumentException("Quote asset is required.", nameof(quoteAsset));
        }

        return quote;
    }

    public static string NormalizeExchange(string exchange)
    {
        var code = exchange?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Exchange is required.", nameof(exchange));
        }

        return code;
    }

    public static TradingAccountId IdFor(ExchangeId exchange, string quoteAsset)
    {
        var name = $"cryptotrader.paper.v1|{NormalizeExchange(exchange.Value)}|{NormalizeQuote(quoteAsset)}";
        return new TradingAccountId(DeterministicGuid(name));
    }

    public static string DisplayName(ExchangeId exchange, string quoteAsset) =>
        $"Paper {NormalizeExchange(exchange.Value)} {NormalizeQuote(quoteAsset)}";

    public static bool SameExchange(ExchangeId left, ExchangeId right) =>
        string.Equals(left.Value, right.Value, StringComparison.OrdinalIgnoreCase);

    public static bool SameQuote(string left, string right) =>
        string.Equals(NormalizeQuote(left), NormalizeQuote(right), StringComparison.Ordinal);

    private static Guid DeterministicGuid(string name)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(name));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }
}
