namespace CryptoTrader.Contracts.Responses;

public sealed record StrategyResponse(string Id, string Version, string DisplayName);

public sealed record StrategyListResponse(IReadOnlyList<StrategyResponse> Items);

public sealed record StrategyInstanceResponse(
    Guid Id,
    string StrategyId,
    string StrategyVersion,
    string Instrument,
    string ParametersJson,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record StrategyInstanceListResponse(IReadOnlyList<StrategyInstanceResponse> Items);
