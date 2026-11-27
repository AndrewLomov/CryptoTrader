namespace CryptoTrader.Domain.Risk;

public static class RiskRejectionCodes
{
    public const string EmergencyStop = "EmergencyStop";

    public const string ExchangeUnhealthy = "ExchangeUnhealthy";

    public const string StaleMarketData = "StaleMarketData";

    public const string FuturesLiveForbidden = "FuturesLiveForbidden";

    public const string CrossMarginForbidden = "CrossMarginForbidden";

    public const string DailyLossLimit = "DailyLossLimit";

    public const string MaxDrawdown = "MaxDrawdown";

    public const string MaxOpenPositions = "MaxOpenPositions";

    public const string MaxExposure = "MaxExposure";

    public const string DuplicateSignal = "DuplicateSignal";

    public const string InsufficientBalance = "InsufficientBalance";

    public const string MinQuantity = "MinQuantity";

    public const string MinNotional = "MinNotional";

    public const string InvalidStop = "InvalidStop";

    public const string AveragingDownForbidden = "AveragingDownForbidden";

    public const string InstrumentDisabled = "InstrumentDisabled";

    public const string LiveTradingDisabled = "LiveTradingDisabled";

    public const string NoOpenPosition = "NoOpenPosition";
}
