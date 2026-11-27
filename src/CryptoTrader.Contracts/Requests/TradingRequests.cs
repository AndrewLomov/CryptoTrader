namespace CryptoTrader.Contracts.Requests;

public sealed record GetOrdersQuery(Guid? AccountId, string? TradingMode, string? Exchange = null, string? QuoteAsset = null);

public sealed record GetPositionsQuery(Guid? AccountId, string? Exchange = null, string? QuoteAsset = null);

public sealed record EmergencyStopRequest(string? Reason, string? Exchange = null, string? QuoteAsset = null);
