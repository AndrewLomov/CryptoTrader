namespace CryptoTrader.Domain.Trading;

public enum OrderStatus
{
    PendingSubmit = 1,
    Submitted = 2,
    Accepted = 3,
    PartiallyFilled = 4,
    Filled = 5,
    CancelPending = 6,
    Canceled = 7,
    Rejected = 8,
    Unknown = 9,
}

public enum OrderSide
{
    Buy = 1,
    Sell = 2,
}

public enum OrderType
{
    Market = 1,
    Limit = 2,
}

public enum TradingMode
{
    Backtest = 1,
    Paper = 2,
    Testnet = 3,
    Live = 4,
}

public enum PositionSide
{
    Flat = 0,
    Long = 1,
    Short = 2,
}

public enum AccountConnectionStatus
{
    Connected = 1,
    Degraded = 2,
    Disconnected = 3,
    ConfigurationError = 4,
}
