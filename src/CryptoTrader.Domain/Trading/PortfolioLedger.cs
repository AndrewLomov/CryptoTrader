using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.Instruments;
using CryptoTrader.Domain.Risk;

namespace CryptoTrader.Domain.Trading;

public sealed class PortfolioLedger : AggregateRoot
{
    private readonly List<Position> _positions = [];
    private readonly Dictionary<InstrumentId, decimal> _marks = [];
    private readonly HashSet<string> _appliedTradeIds = new(StringComparer.Ordinal);
    private decimal _cash;
    private decimal _peakEquity;
    private decimal _dayStartEquity;
    private decimal _lastEquity;
    private DateOnly _dayUtc;
    private bool _emergencyStop;

    private PortfolioLedger(
        TradingAccountId accountId,
        ExchangeId exchange,
        string quoteAsset,
        decimal cash,
        decimal peakEquity,
        decimal dayStartEquity,
        decimal lastEquity,
        DateOnly dayUtc,
        bool emergencyStop,
        DateTimeOffset updatedAtUtc)
    {
        AccountId = accountId;
        Exchange = exchange;
        QuoteAsset = quoteAsset;
        _cash = cash;
        _peakEquity = peakEquity;
        _dayStartEquity = dayStartEquity;
        _lastEquity = lastEquity;
        _dayUtc = dayUtc;
        _emergencyStop = emergencyStop;
        UpdatedAtUtc = updatedAtUtc;
    }

    public TradingAccountId AccountId { get; }

    public ExchangeId Exchange { get; }

    public string QuoteAsset { get; }

    public decimal Cash => _cash;

    public decimal PeakEquity => _peakEquity;

    public decimal DayStartEquity => _dayStartEquity;

    public decimal LastEquity => _lastEquity;

    public DateOnly DayUtc => _dayUtc;

    public bool EmergencyStop => _emergencyStop;

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public decimal DailyPnl => _lastEquity - _dayStartEquity;

    public decimal DrawdownFraction =>
        _peakEquity <= 0 ? 0 : Math.Max(0, (_peakEquity - _lastEquity) / _peakEquity);

    public IReadOnlyList<Position> Positions => _positions;

    public IReadOnlyDictionary<InstrumentId, decimal> Marks => _marks;

    public IReadOnlyCollection<string> AppliedExchangeTradeIds => _appliedTradeIds;

    public static PortfolioLedger Create(
        decimal cash,
        string quoteAsset,
        TradingAccountId accountId,
        ExchangeId exchange,
        DateTimeOffset now)
    {
        Guard.NotNegative(cash, nameof(cash));
        now = Guard.Utc(now, nameof(now));
        return new PortfolioLedger(
            accountId,
            exchange,
            Guard.Required(quoteAsset, nameof(quoteAsset)),
            cash,
            cash,
            cash,
            cash,
            DateOnly.FromDateTime(now.UtcDateTime),
            false,
            now);
    }

    public static PortfolioLedger Rehydrate(
        TradingAccountId accountId,
        ExchangeId exchange,
        string quoteAsset,
        decimal cash,
        decimal peakEquity,
        decimal dayStartEquity,
        decimal lastEquity,
        DateOnly dayUtc,
        bool emergencyStop,
        DateTimeOffset updatedAtUtc,
        IReadOnlyList<Position> positions,
        IReadOnlyDictionary<InstrumentId, decimal> marks,
        IReadOnlyCollection<string> appliedExchangeTradeIds)
    {
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(marks);
        ArgumentNullException.ThrowIfNull(appliedExchangeTradeIds);
        Guard.NotNegative(cash, nameof(cash));
        Guard.NotNegative(peakEquity, nameof(peakEquity));
        Guard.NotNegative(dayStartEquity, nameof(dayStartEquity));
        Guard.NotNegative(lastEquity, nameof(lastEquity));
        if (peakEquity < lastEquity)
        {
            throw new DomainException("Peak equity cannot be below the last equity.");
        }

        updatedAtUtc = Guard.Utc(updatedAtUtc, nameof(updatedAtUtc));
        var ledger = new PortfolioLedger(
            accountId,
            exchange,
            Guard.Required(quoteAsset, nameof(quoteAsset)),
            cash,
            peakEquity,
            dayStartEquity,
            lastEquity,
            dayUtc,
            emergencyStop,
            updatedAtUtc);

        var seen = new HashSet<InstrumentId>();
        foreach (var position in positions)
        {
            ArgumentNullException.ThrowIfNull(position);
            if (position.AccountId != accountId || position.Exchange != exchange)
            {
                throw new DomainException("Ledger position belongs to another account or exchange.");
            }

            if (position.InstrumentType != InstrumentType.Spot)
            {
                throw new DomainException("PortfolioLedger in 0.1 tracks spot positions only.");
            }

            if (!seen.Add(position.InstrumentId))
            {
                throw new DomainException($"Duplicate position for {position.InstrumentId}.");
            }

            ledger._positions.Add(position);
        }

        foreach (var (instrumentId, mark) in marks)
        {
            if (mark <= 0)
            {
                throw new DomainException("Mark price must be greater than zero.");
            }

            ledger._marks[instrumentId] = mark;
        }

        foreach (var tradeId in appliedExchangeTradeIds)
        {
            var normalized = Guard.Required(tradeId, nameof(appliedExchangeTradeIds));
            if (!ledger._appliedTradeIds.Add(normalized))
            {
                throw new DomainException($"Duplicate exchange trade id '{normalized}'.");
            }
        }

        return ledger;
    }

    public Position? FindPosition(InstrumentId instrumentId) =>
        _positions.FirstOrDefault(position => position.InstrumentId == instrumentId);

    public decimal Equity(Price mark)
    {
        var open = _positions.Where(position => position.Side == PositionSide.Long && position.Quantity.Value > 0).ToList();
        if (open.Count > 1)
        {
            throw new DomainException("Equity(mark) supports one open long. Pass a mark for each instrument.");
        }

        return open.Count == 0 ? _cash : _cash + open[0].Quantity.Value * mark.Value;
    }

    public decimal Equity(IReadOnlyDictionary<InstrumentId, Price> marks)
    {
        ArgumentNullException.ThrowIfNull(marks);
        var equity = _cash;
        foreach (var position in _positions)
        {
            if (position.Side != PositionSide.Long || position.Quantity.Value == 0)
            {
                continue;
            }

            if (!marks.TryGetValue(position.InstrumentId, out var mark))
            {
                throw new DomainException($"Mark is missing for {position.InstrumentId}.");
            }

            equity += position.Quantity.Value * mark.Value;
        }

        return equity;
    }

    public decimal GrossExposure(IReadOnlyDictionary<InstrumentId, Price> marks) => Equity(marks) - _cash;

    public Position? ApplySpotFill(
        Instrument instrument,
        OrderSide side,
        Quantity quantity,
        Price price,
        decimal fee,
        string feeAsset,
        DateTimeOffset time,
        string exchangeTradeId,
        Guid? correlationId = null,
        Guid? causationId = null)
    {
        ArgumentNullException.ThrowIfNull(instrument);
        Guard.Defined(side, nameof(side));
        if (instrument.Type != InstrumentType.Spot)
        {
            throw new DomainException("ApplySpotFill accepts spot instruments only.");
        }

        if (instrument.Exchange != Exchange)
        {
            throw new DomainException("Instrument exchange does not match the ledger.");
        }

        if (!string.Equals(instrument.QuoteAsset, QuoteAsset, StringComparison.Ordinal))
        {
            throw new DomainException("Instrument quote asset does not match the ledger.");
        }

        if (quantity.Value <= 0)
        {
            throw new DomainException("Fill quantity must be greater than zero.");
        }

        Guard.NotNegative(fee, nameof(fee));
        var asset = Guard.Required(feeAsset, nameof(feeAsset));
        if (!string.Equals(asset, QuoteAsset, StringComparison.Ordinal))
        {
            throw new DomainException("Spot fee must be charged in the quote asset.");
        }

        time = Guard.Utc(time, nameof(time));
        var tradeId = Guard.Required(exchangeTradeId, nameof(exchangeTradeId));
        if (_appliedTradeIds.Contains(tradeId))
        {
            return null;
        }

        var existing = FindPosition(instrument.Id);
        var held = existing is { Side: PositionSide.Long } ? existing.Quantity.Value : 0m;
        if (side == OrderSide.Sell && quantity.Value > held)
        {
            throw new DomainException("Spot position cannot go short.");
        }

        var notional = price.Value * quantity.Value;
        var delta = side == OrderSide.Buy ? -(notional + fee) : notional - fee;
        if (_cash + delta < 0)
        {
            throw new DomainException("Insufficient cash.");
        }

        EnsureDay(time);

        var position = existing ?? Position.Create(AccountId, Exchange, instrument.Id, instrument.Type, null, time);
        position.ApplyFill(side, quantity, price, fee, asset, time, correlationId, causationId);
        if (existing is null)
        {
            _positions.Add(position);
        }

        _cash += delta;
        _appliedTradeIds.Add(tradeId);
        _marks[instrument.Id] = price.Value;
        foreach (var domainEvent in position.DomainEvents)
        {
            Raise(domainEvent);
        }

        position.ClearDomainEvents();
        Recalculate(time);
        return position;
    }

    public void ObserveMark(InstrumentId instrumentId, Price mark, DateTimeOffset now)
    {
        now = Guard.Utc(now, nameof(now));
        EnsureDay(now);
        _marks[instrumentId] = mark.Value;
        Recalculate(now);
    }

    public void EnsureDay(DateTimeOffset now)
    {
        now = Guard.Utc(now, nameof(now));
        var day = DateOnly.FromDateTime(now.UtcDateTime);
        if (day == _dayUtc)
        {
            return;
        }

        _dayUtc = day;
        _dayStartEquity = _lastEquity;
        UpdatedAtUtc = now;
    }

    public void EngageEmergencyStop(
        DateTimeOffset now,
        string reason,
        Guid? correlationId = null,
        Guid? causationId = null)
    {
        now = Guard.Utc(now, nameof(now));
        var normalized = Guard.Required(reason, nameof(reason));
        if (_emergencyStop)
        {
            return;
        }

        _emergencyStop = true;
        UpdatedAtUtc = now;
        Raise(new RiskLimitTriggered(AccountId, RiskRejectionCodes.EmergencyStop, normalized, now, correlationId, causationId));
    }

    public void ReleaseEmergencyStop(DateTimeOffset now)
    {
        now = Guard.Utc(now, nameof(now));
        if (!_emergencyStop)
        {
            return;
        }

        _emergencyStop = false;
        UpdatedAtUtc = now;
    }

    private void Recalculate(DateTimeOffset now)
    {
        var equity = _cash;
        foreach (var position in _positions)
        {
            if (position.Side != PositionSide.Long || position.Quantity.Value == 0)
            {
                continue;
            }

            var mark = _marks.TryGetValue(position.InstrumentId, out var stored) ? stored : position.AveragePrice;
            equity += position.Quantity.Value * mark;
        }

        _lastEquity = equity;
        if (equity > _peakEquity)
        {
            _peakEquity = equity;
        }

        UpdatedAtUtc = now;
    }
}
