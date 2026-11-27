using CryptoTrader.Domain.Common;

namespace CryptoTrader.Domain.Risk;

public sealed record PositionSizeResult(
    bool IsApproved,
    Quantity? Quantity,
    decimal? EstimatedMaxLoss,
    string? RejectionCode,
    string? Reason)
{
    public static PositionSizeResult Success(Quantity quantity, decimal estimatedMaxLoss) =>
        new(true, quantity, estimatedMaxLoss, null, null);

    public static PositionSizeResult Reject(string rejectionCode, string reason) =>
        new(false, null, null, rejectionCode, reason);
}

public static class PositionSizer
{
    public static PositionSizeResult TrySize(
        Price entry,
        Price stop,
        decimal equity,
        decimal availableCash,
        RiskProfile profile,
        decimal quantityStep,
        decimal minQuantity,
        decimal? minNotional)
    {
        ArgumentNullException.ThrowIfNull(profile);
        Guard.Positive(quantityStep, nameof(quantityStep));
        Guard.Positive(minQuantity, nameof(minQuantity));
        if (minNotional is not null)
        {
            Guard.Positive(minNotional.Value, nameof(minNotional));
        }

        var slippedEntry = entry.Value * (1m + profile.SlippageBps / 10000m);
        var unitRisk = slippedEntry - stop.Value;
        if (unitRisk <= 0)
        {
            return PositionSizeResult.Reject(RiskRejectionCodes.InvalidStop, "Stop is not below the slipped entry.");
        }

        if (equity <= 0)
        {
            return PositionSizeResult.Reject(RiskRejectionCodes.InsufficientBalance, "Equity is not positive.");
        }

        var riskAmount = equity * profile.RiskPerTradePercent / 100m;
        var rawQuantity = riskAmount / unitRisk;
        var quantity = DecimalRounding.FloorToStep(rawQuantity, quantityStep);
        if (quantity <= 0 || quantity < minQuantity)
        {
            return PositionSizeResult.Reject(RiskRejectionCodes.MinQuantity, "Sized quantity is below the minimum.");
        }

        var notional = quantity * slippedEntry;
        if (minNotional is decimal requiredNotional && notional < requiredNotional)
        {
            return PositionSizeResult.Reject(RiskRejectionCodes.MinNotional, "Sized notional is below the minimum.");
        }

        var entryFee = notional * profile.FeeRate;
        var exitFee = quantity * stop.Value * profile.FeeRate;
        var cashRequired = quantity * slippedEntry * (1m + profile.FeeRate) + quantity * stop.Value * profile.FeeRate;
        if (availableCash < cashRequired)
        {
            return PositionSizeResult.Reject(RiskRejectionCodes.InsufficientBalance, "Available cash does not cover notional and fees.");
        }

        var estimatedMaxLoss = quantity * unitRisk + entryFee + exitFee;
        return PositionSizeResult.Success(new Quantity(quantity), estimatedMaxLoss);
    }
}
