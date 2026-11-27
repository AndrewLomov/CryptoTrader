namespace CryptoTrader.Contracts.Requests;

public sealed record CreateStrategyInstanceRequest(string Instrument, string ParametersJson);
