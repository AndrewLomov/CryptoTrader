namespace CryptoTrader.Contracts.Requests;

public sealed record GetOrdersQuery(Guid? AccountId, string? TradingMode);

public sealed record GetPositionsQuery(Guid? AccountId);

public sealed record EmergencyStopRequest(string? Reason);
