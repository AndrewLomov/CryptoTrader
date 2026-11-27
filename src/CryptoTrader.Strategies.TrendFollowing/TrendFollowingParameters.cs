using System.Text.Json;

namespace CryptoTrader.Strategies.TrendFollowing;

public sealed class TrendFollowingParameters
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public int EmaFastPeriod { get; init; } = 50;

    public int EmaSlowPeriod { get; init; } = 200;

    public int RsiPeriod { get; init; } = 14;

    public int AtrPeriod { get; init; } = 14;

    public decimal RsiCrossThreshold { get; init; } = 40;

    public decimal AtrStopMultiplier { get; init; } = 2;

    public decimal AtrTakeProfitMultiplier { get; init; } = 4;

    public static TrendFollowingParameters Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        TrendFollowingParameters? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<TrendFollowingParameters>(json, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("Strategy parameters JSON is invalid.", nameof(json), exception);
        }

        if (parsed is null)
        {
            throw new ArgumentException("Strategy parameters JSON must be an object.", nameof(json));
        }

        if (parsed.EmaFastPeriod < 1
            || parsed.EmaSlowPeriod < 1
            || parsed.RsiPeriod < 1
            || parsed.AtrPeriod < 1)
        {
            throw new ArgumentException("Indicator periods must be at least 1.", nameof(json));
        }

        if (parsed.EmaFastPeriod >= parsed.EmaSlowPeriod)
        {
            throw new ArgumentException("EMA fast period must be less than the slow period.", nameof(json));
        }

        return parsed;
    }
}
