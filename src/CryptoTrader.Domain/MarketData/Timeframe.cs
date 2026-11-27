using CryptoTrader.Domain.Common;

namespace CryptoTrader.Domain.MarketData;

public readonly record struct Timeframe
{
    private Timeframe(string code, TimeSpan duration)
    {
        Code = code;
        Duration = duration;
    }

    public static Timeframe OneMinute { get; } = new("1m", TimeSpan.FromMinutes(1));

    public static Timeframe FiveMinutes { get; } = new("5m", TimeSpan.FromMinutes(5));

    public static Timeframe FifteenMinutes { get; } = new("15m", TimeSpan.FromMinutes(15));

    public static Timeframe OneHour { get; } = new("1h", TimeSpan.FromHours(1));

    public static Timeframe FourHours { get; } = new("4h", TimeSpan.FromHours(4));

    public static Timeframe OneDay { get; } = new("1d", TimeSpan.FromDays(1));

    public string Code { get; }

    public TimeSpan Duration { get; }

    public static Timeframe Parse(string code)
    {
        var normalized = Guard.Required(code, nameof(code)).ToLowerInvariant();
        return normalized switch
        {
            "1m" => OneMinute,
            "5m" => FiveMinutes,
            "15m" => FifteenMinutes,
            "1h" => OneHour,
            "4h" => FourHours,
            "1d" => OneDay,
            _ => throw new DomainException($"Timeframe '{code}' is not supported."),
        };
    }

    public override string ToString() => Code;
}
