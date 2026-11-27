using CryptoTrader.Domain.Common;

namespace CryptoTrader.Domain.Risk;

public sealed record RiskDecision
{
    private RiskDecision(
        bool approved,
        Quantity? approvedQuantity,
        decimal? estimatedMaxLoss,
        IReadOnlyList<string> appliedConstraints,
        string? rejectionCode,
        string? reason)
    {
        Approved = approved;
        ApprovedQuantity = approvedQuantity;
        EstimatedMaxLoss = estimatedMaxLoss;
        AppliedConstraints = appliedConstraints;
        RejectionCode = rejectionCode;
        Reason = reason;
    }

    public bool Approved { get; }

    public Quantity? ApprovedQuantity { get; }

    public decimal? EstimatedMaxLoss { get; }

    public IReadOnlyList<string> AppliedConstraints { get; }

    public string? RejectionCode { get; }

    public string? Reason { get; }

    public static RiskDecision Approve(
        Quantity quantity,
        decimal estimatedMaxLoss,
        IReadOnlyList<string>? appliedConstraints = null)
    {
        if (quantity.Value <= 0)
        {
            throw new DomainException("Approved quantity must be greater than zero.");
        }

        return new RiskDecision(true, quantity, estimatedMaxLoss, appliedConstraints ?? [], null, null);
    }

    public static RiskDecision Reject(
        string rejectionCode,
        string reason,
        IReadOnlyList<string>? appliedConstraints = null)
    {
        return new RiskDecision(
            false,
            null,
            null,
            appliedConstraints ?? [],
            Guard.Required(rejectionCode, nameof(rejectionCode)),
            Guard.Required(reason, nameof(reason)));
    }
}
