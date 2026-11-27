using CryptoTrader.Domain.Common;

namespace CryptoTrader.Domain.Risk;

public sealed record RiskCheckApproved : DomainEvent
{
    public RiskCheckApproved(
        Guid signalId,
        decimal approvedQuantity,
        decimal estimatedMaxLoss,
        DateTimeOffset occurredAtUtc,
        Guid? correlationId = null,
        Guid? causationId = null)
        : base(occurredAtUtc, correlationId, causationId)
    {
        if (signalId == Guid.Empty)
        {
            throw new DomainException("Signal id cannot be empty.");
        }

        if (approvedQuantity <= 0)
        {
            throw new DomainException("Approved quantity must be greater than zero.");
        }

        SignalId = signalId;
        ApprovedQuantity = approvedQuantity;
        EstimatedMaxLoss = estimatedMaxLoss;
    }

    public Guid SignalId { get; init; }

    public decimal ApprovedQuantity { get; init; }

    public decimal EstimatedMaxLoss { get; init; }
}

public sealed record RiskCheckRejected : DomainEvent
{
    public RiskCheckRejected(
        Guid signalId,
        string rejectionCode,
        string reason,
        DateTimeOffset occurredAtUtc,
        Guid? correlationId = null,
        Guid? causationId = null)
        : base(occurredAtUtc, correlationId, causationId)
    {
        if (signalId == Guid.Empty)
        {
            throw new DomainException("Signal id cannot be empty.");
        }

        SignalId = signalId;
        RejectionCode = Guard.Required(rejectionCode, nameof(rejectionCode));
        Reason = Guard.Required(reason, nameof(reason));
    }

    public Guid SignalId { get; init; }

    public string RejectionCode { get; init; }

    public string Reason { get; init; }
}

public sealed record RiskLimitTriggered : DomainEvent
{
    public RiskLimitTriggered(
        TradingAccountId accountId,
        string code,
        string reason,
        DateTimeOffset occurredAtUtc,
        Guid? correlationId = null,
        Guid? causationId = null)
        : base(occurredAtUtc, correlationId, causationId)
    {
        AccountId = accountId;
        Code = Guard.Required(code, nameof(code));
        Reason = Guard.Required(reason, nameof(reason));
    }

    public TradingAccountId AccountId { get; init; }

    public string Code { get; init; }

    public string Reason { get; init; }
}
