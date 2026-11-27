namespace CryptoTrader.Application.Common;

internal static class UtcTime
{
    public static DateTimeOffset Normalize(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return utc.Offset == TimeSpan.Zero
            ? utc
            : new DateTimeOffset(utc.UtcDateTime, TimeSpan.Zero);
    }
}
