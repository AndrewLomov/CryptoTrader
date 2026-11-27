using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.Instruments;

namespace CryptoTrader.Domain.Trading;

public sealed class Position : AggregateRoot
{
    private Position(
        TradingAccountId accountId,
        ExchangeId exchange,
        InstrumentId instrumentId,
        InstrumentType instrumentType,
        PositionSide side,
        Quantity quantity,
        decimal averagePrice,
        decimal realizedPnl,
        decimal fees,
        decimal funding,
        string? feeAsset,
        decimal leverage,
        decimal? margin,
        decimal? contractMultiplier,
        DateTimeOffset openedAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        AccountId = accountId;
        Exchange = exchange;
        InstrumentId = instrumentId;
        InstrumentType = instrumentType;
        Side = side;
        Quantity = quantity;
        AveragePrice = averagePrice;
        RealizedPnl = realizedPnl;
        Fees = fees;
        Funding = funding;
        FeeAsset = feeAsset;
        Leverage = leverage;
        Margin = margin;
        ContractMultiplier = contractMultiplier;
        OpenedAtUtc = openedAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public TradingAccountId AccountId { get; }

    public ExchangeId Exchange { get; }

    public InstrumentId InstrumentId { get; }

    public InstrumentType InstrumentType { get; }

    public PositionSide Side { get; private set; }

    public Quantity Quantity { get; private set; }

    public decimal AveragePrice { get; private set; }

    public decimal RealizedPnl { get; private set; }

    public decimal Fees { get; private set; }

    public decimal Funding { get; private set; }

    public string? FeeAsset { get; private set; }

    public decimal Leverage { get; }

    public decimal? Margin { get; }

    public decimal? ContractMultiplier { get; }

    public DateTimeOffset OpenedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static Position Create(
        TradingAccountId accountId,
        ExchangeId exchange,
        InstrumentId instrumentId,
        InstrumentType instrumentType,
        decimal? contractMultiplier,
        DateTimeOffset now)
    {
        now = Guard.Utc(now, nameof(now));
        ValidateContract(instrumentType, contractMultiplier, PositionSide.Flat, Quantity.Zero, 0);
        return new Position(
            accountId,
            exchange,
            instrumentId,
            instrumentType,
            PositionSide.Flat,
            Quantity.Zero,
            0,
            0,
            0,
            0,
            null,
            1,
            null,
            contractMultiplier,
            now,
            now);
    }

    public static Position Rehydrate(
        TradingAccountId accountId,
        ExchangeId exchange,
        InstrumentId instrumentId,
        InstrumentType instrumentType,
        PositionSide side,
        Quantity quantity,
        decimal averagePrice,
        decimal realizedPnl,
        decimal fees,
        decimal funding,
        string? feeAsset,
        decimal leverage,
        decimal? margin,
        decimal? contractMultiplier,
        DateTimeOffset openedAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        openedAtUtc = Guard.Utc(openedAtUtc, nameof(openedAtUtc));
        updatedAtUtc = Guard.Utc(updatedAtUtc, nameof(updatedAtUtc));
        if (updatedAtUtc < openedAtUtc)
        {
            throw new DomainException("Position update time cannot be earlier than open time.");
        }

        if (leverage != 1m)
        {
            throw new DomainException("Leverage above 1 is not supported in CryptoTrader 0.1.");
        }

        if (margin is < 0)
        {
            throw new DomainException("Margin cannot be negative.");
        }

        Guard.NotNegative(fees, nameof(fees));
        ValidateContract(instrumentType, contractMultiplier, side, quantity, averagePrice);
        if (fees > 0)
        {
            feeAsset = Guard.Required(feeAsset, nameof(feeAsset));
        }
        else if (string.IsNullOrWhiteSpace(feeAsset))
        {
            feeAsset = null;
        }
        else
        {
            feeAsset = feeAsset.Trim();
        }

        return new Position(
            accountId,
            exchange,
            instrumentId,
            instrumentType,
            side,
            quantity,
            averagePrice,
            realizedPnl,
            fees,
            funding,
            feeAsset,
            leverage,
            margin,
            contractMultiplier,
            openedAtUtc,
            updatedAtUtc);
    }

    public void ApplyFill(
        OrderSide side,
        Quantity quantity,
        Price price,
        decimal fee,
        string feeAsset,
        DateTimeOffset time,
        Guid? correlationId = null,
        Guid? causationId = null)
    {
        Guard.Defined(side, nameof(side));
        if (quantity.Value <= 0)
        {
            throw new DomainException("Fill quantity must be greater than zero.");
        }

        Guard.NotNegative(fee, nameof(fee));
        var asset = Guard.Required(feeAsset, nameof(feeAsset));
        time = Guard.Utc(time, nameof(time));
        if (InstrumentType == InstrumentType.Spot && side == OrderSide.Sell && (Side != PositionSide.Long || quantity.Value > Quantity.Value))
        {
            throw new DomainException("Spot position cannot go short.");
        }

        AssignFeeAsset(asset);

        if (Side == PositionSide.Flat)
        {
            Open(side, quantity, price, fee, time, correlationId, causationId);
            return;
        }

        var increasing = (Side == PositionSide.Long && side == OrderSide.Buy)
            || (Side == PositionSide.Short && side == OrderSide.Sell);
        if (increasing)
        {
            var newQuantity = Quantity.Value + quantity.Value;
            AveragePrice = ((AveragePrice * Quantity.Value) + (price.Value * quantity.Value)) / newQuantity;
            Quantity = new Quantity(newQuantity);
            Fees += fee;
            UpdatedAtUtc = time;
            Raise(new PositionChanged(AccountId, InstrumentId, Side, Quantity.Value, AveragePrice, RealizedPnl, time, correlationId, causationId));
            return;
        }

        if (quantity.Value > Quantity.Value)
        {
            throw new DomainException("Fill would reverse the position. Close and open as separate fills.");
        }

        var multiplier = ContractMultiplier ?? 1m;
        var pricePnl = Side == PositionSide.Long
            ? (price.Value - AveragePrice) * quantity.Value * multiplier
            : (AveragePrice - price.Value) * quantity.Value * multiplier;
        RealizedPnl += pricePnl - fee;
        Fees += fee;
        var remaining = Quantity.Value - quantity.Value;
        Quantity = new Quantity(remaining);
        UpdatedAtUtc = time;
        if (remaining == 0)
        {
            Side = PositionSide.Flat;
            AveragePrice = 0;
            Raise(new PositionClosed(AccountId, InstrumentId, RealizedPnl, Fees, time, correlationId, causationId));
            return;
        }

        Raise(new PositionChanged(AccountId, InstrumentId, Side, Quantity.Value, AveragePrice, RealizedPnl, time, correlationId, causationId));
    }

    public void ApplyFunding(
        decimal amount,
        DateTimeOffset time,
        Guid? correlationId = null,
        Guid? causationId = null)
    {
        if (Side == PositionSide.Flat || Quantity.Value == 0)
        {
            throw new DomainException("Funding cannot be applied to a flat position.");
        }

        time = Guard.Utc(time, nameof(time));
        Funding += amount;
        UpdatedAtUtc = time;
        Raise(new PositionChanged(AccountId, InstrumentId, Side, Quantity.Value, AveragePrice, RealizedPnl, time, correlationId, causationId));
    }

    public decimal UnrealizedPnl(Price markPrice)
    {
        if (Side == PositionSide.Flat || Quantity.Value == 0)
        {
            return 0;
        }

        var multiplier = ContractMultiplier ?? 1m;
        return Side == PositionSide.Long
            ? (markPrice.Value - AveragePrice) * Quantity.Value * multiplier
            : (AveragePrice - markPrice.Value) * Quantity.Value * multiplier;
    }

    private void Open(
        OrderSide side,
        Quantity quantity,
        Price price,
        decimal fee,
        DateTimeOffset time,
        Guid? correlationId,
        Guid? causationId)
    {
        Side = side == OrderSide.Buy ? PositionSide.Long : PositionSide.Short;
        Quantity = quantity;
        AveragePrice = price.Value;
        Fees += fee;
        OpenedAtUtc = time;
        UpdatedAtUtc = time;
        Raise(new PositionOpened(AccountId, InstrumentId, InstrumentType, Side, Quantity.Value, AveragePrice, time, correlationId, causationId));
    }

    private void AssignFeeAsset(string feeAsset)
    {
        if (FeeAsset is null)
        {
            FeeAsset = feeAsset;
            return;
        }

        if (!string.Equals(FeeAsset, feeAsset, StringComparison.Ordinal))
        {
            throw new DomainException("Position fee asset cannot change.");
        }
    }

    private static void ValidateContract(
        InstrumentType instrumentType,
        decimal? contractMultiplier,
        PositionSide side,
        Quantity quantity,
        decimal averagePrice)
    {
        Guard.Defined(instrumentType, nameof(instrumentType));
        Guard.Defined(side, nameof(side));
        switch (instrumentType)
        {
            case InstrumentType.Spot when contractMultiplier is not null:
                throw new DomainException("Spot positions cannot have a contract multiplier.");
            case InstrumentType.Spot when side == PositionSide.Short:
                throw new DomainException("Spot position cannot go short.");
            case InstrumentType.LinearPerpetual when contractMultiplier is null || contractMultiplier <= 0:
                throw new DomainException("Linear perpetual positions require a contract multiplier greater than zero.");
        }

        var isFlat = side == PositionSide.Flat;
        if (isFlat != (quantity.Value == 0) || (isFlat && averagePrice != 0) || (!isFlat && averagePrice <= 0))
        {
            throw new DomainException("Position side, quantity, and average price are inconsistent.");
        }
    }
}
