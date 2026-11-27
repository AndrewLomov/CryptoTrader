namespace CryptoTrader.Exchanges.Bybit;

/// <summary>
/// Binds the <c>Bybit</c> configuration section inside the adapter.
/// The shape matches application settings. This project does not reference Application.
/// </summary>
public sealed class BybitOptions
{
    public const string SectionName = "Bybit";

    public const string HttpClientName = "bybit";

    public string RestBaseUrl { get; set; } = "https://api.bybit.com";

    public string SpotWebSocketUrl { get; set; } = "wss://stream.bybit.com/v5/public/spot";

    public string LinearWebSocketUrl { get; set; } = "wss://stream.bybit.com/v5/public/linear";

    public string[] Symbols { get; set; } = ["BTCUSDT", "ETHUSDT"];

    public int TimeoutSeconds { get; set; } = 15;

    public int MaxRequestsPerSecond { get; set; } = 10;
}
