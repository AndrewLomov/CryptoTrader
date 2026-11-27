namespace CryptoTrader.Contracts.Responses;

public sealed record FeedHealthResponse(string Status, IReadOnlyList<FeedHealthItemResponse> Items);

public sealed record FeedHealthItemResponse(
    string Exchange,
    string Instrument,
    string Timeframe,
    string Status,
    string? Detail,
    DateTimeOffset? LastEventUtc);
