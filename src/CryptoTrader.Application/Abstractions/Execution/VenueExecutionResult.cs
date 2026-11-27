using CryptoTrader.Domain.Trading;
using DomainExecution = CryptoTrader.Domain.Trading.Execution;

namespace CryptoTrader.Application.Abstractions.Execution;

public sealed record VenueExecutionResult(
    Order? Order,
    DomainExecution? Execution,
    string? RejectionCode);
