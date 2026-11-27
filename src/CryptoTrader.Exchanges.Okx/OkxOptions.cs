namespace CryptoTrader.Exchanges.Okx;

/// <summary>
/// Binds the <c>Okx</c> configuration section inside the adapter.
/// This project does not reference Application and does not read API credentials.
/// </summary>
public sealed class OkxOptions
{
    public const string SectionName = "Okx";

    public const string HttpClientName = "okx";

    /// <summary>
    /// Public market data stays off until configuration sets this to true.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Production REST host from the OKX V5 "Production Trading Services" section on 27 November 2026.
    /// </summary>
    public string RestBaseUrl { get; set; } = "https://openapi.okx.com";

    /// <summary>
    /// Public WebSocket on port 443. Port 8443 was retired for the public internet on 31 October 2026.
    /// </summary>
    public string PublicWebSocketUrl { get; set; } = "wss://ws.okx.com/ws/v5/public";

    /// <summary>
    /// Candle channels are documented on <c>/ws/v5/business</c>, not on the public socket.
    /// </summary>
    public string BusinessWebSocketUrl { get; set; } = "wss://ws.okx.com/ws/v5/business";

    public int RequestTimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// Recent <c>/api/v5/market/candles</c> page size. Official maximum is 300.
    /// </summary>
    public int MaxCandlesPerRequest { get; set; } = 300;

    /// <summary>
    /// <c>/api/v5/market/history-candles</c> page size. Official maximum on 27 November 2026 is 300.
    /// </summary>
    public int MaxHistoryCandlesPerRequest { get; set; } = 300;

    public int MaxPages { get; set; } = 500;

    public int ChannelCapacity { get; set; } = 256;

    public int AckTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Client text ping interval. Official rule: N must be below the 30 second idle disconnect.
    /// </summary>
    public int PingIntervalSeconds { get; set; } = 20;

    /// <summary>
    /// Requests allowed inside <see cref="RateLimitWindowSeconds"/>.
    /// Default 10 per 2 seconds is the official GET /api/v5/public/funding-rate limit
    /// (IP + instrument), which is stricter than history candles at 20 per 2 seconds.
    /// It is a window budget, not 10 requests per second.
    /// </summary>
    public int MaxRequestsPerWindow { get; set; } = 10;

    public int RateLimitWindowSeconds { get; set; } = 2;

    public string[] InstrumentTypes { get; set; } = ["SPOT", "SWAP"];
}
