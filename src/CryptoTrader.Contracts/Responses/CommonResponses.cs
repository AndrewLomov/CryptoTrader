namespace CryptoTrader.Contracts.Responses;

public sealed record ApiErrorResponse(string Code, string Message);

public sealed record LiveHealthResponse(string Status);

public sealed record ReadyHealthResponse(string Status, bool DatabaseReady);
