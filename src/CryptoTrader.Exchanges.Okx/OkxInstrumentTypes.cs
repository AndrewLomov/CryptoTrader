namespace CryptoTrader.Exchanges.Okx;

/// <summary>
/// Canonical OKX instrument types. Callers trim and uppercase once, then reject blanks, unknowns, and duplicates.
/// <c>spot</c> and <c>SPOT</c> are the same value.
/// </summary>
public static class OkxInstrumentTypes
{
    public const string Spot = "SPOT";

    public const string Swap = "SWAP";

    public static string[] Normalize(IReadOnlyList<string>? types)
    {
        if (types is null || types.Count == 0)
        {
            throw new InvalidOperationException("Okx:InstrumentTypes requires at least one instrument type.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var normalized = new string[types.Count];
        for (var index = 0; index < types.Count; index++)
        {
            var type = types[index];
            if (string.IsNullOrWhiteSpace(type))
            {
                throw new InvalidOperationException("Okx:InstrumentTypes contains a blank value.");
            }

            var canonical = type.Trim().ToUpperInvariant();
            if (canonical is not (Spot or Swap))
            {
                throw new InvalidOperationException($"Okx:InstrumentTypes contains '{type.Trim()}', which is not SPOT or SWAP.");
            }

            if (!seen.Add(canonical))
            {
                throw new InvalidOperationException($"Okx:InstrumentTypes contains duplicate '{canonical}'.");
            }

            normalized[index] = canonical;
        }

        return normalized;
    }
}
