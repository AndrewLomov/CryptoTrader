# Market data semantics

Date: 2026-11-27. Scope is public candle volume, the linear contract notional, funding, and what backtest 0.1 and paper 0.1 may consume. Position PnL formulas are unchanged and are not a volume conversion.

The OKX formula below follows `docs/okx-api-reference-notes.md` (checked 2026-11-27).

| Rule | Status |
|---|---|
| `Candle.Volume` is the exchange quantity in `VolumeUnit` | Implemented |
| Spot: base size in `Volume`, quote turnover in `QuoteVolume` when provided | Implemented |
| Linear SWAP: contract count in `Volume`, no substitute `ctVal` of 1 | Implemented |
| OKX linear notional `sz × ctVal × markPx` (no default `ctMult`) | Implemented |
| Funding is a public series and is not applied in backtest or paper | Implemented |
| Backtest and paper 0.1 accept spot base-asset volume only | Implemented |
| Closed candles only; historical gaps are not interpolated | Partially Implemented |
| A candle is usable at `CloseTimeUtc`; the forming bar is not closed | Implemented |

## Volume is the exchange quantity in its unit

**Status: Implemented.**

`Candle.Volume` is the quantity the exchange sent, measured in `Candle.VolumeUnit`. It is not implicitly base size, quote turnover, or a contract count. The unit decides the meaning:

| `VolumeUnit` | Meaning of `Volume` |
|---|---|
| `BaseAsset` (1) | Size of the base asset |
| `QuoteAsset` (2) | Turnover in the quote asset |
| `Contract` (3) | Number of contracts |

`QuoteVolume` and `BaseVolume` are extra fields. Each is null when the exchange did not send that quantity. A non-null extra volume is greater than or equal to zero. They do not replace `Volume`.

`Volume`, `QuoteVolume`, and `BaseVolume` are trailing arguments of `Candle.Create`. The default unit is `VolumeUnit.BaseAsset`. `BacktestEngine` rejects any other unit.

`BybitCandleMapper` copies the kline volume into `Candle.Volume` and does not pass a unit. With the default unit, that row is labeled base-asset size. That label is the spot case. A linear kline must not be stored as base volume by leaving the default in place.

## Spot

**Status: Implemented.**

For `InstrumentType.Spot`:

- `VolumeUnit` is `BaseAsset`.
- `Volume` is the base size (BTC on BTC-USDT).
- `QuoteVolume` is the quote turnover (USDT on BTC-USDT) when the exchange sends it. Otherwise it stays null.
- Contract value, contract-value currency, settlement currency, and contract kind stay null. `ContractMultiplier` stays null.

The OKX spot mapper writes base size to `Volume` and quote turnover to `QuoteVolume`. The Bybit kline mapper still stores the exchange volume in `Volume` with the default base-asset unit and does not fill `QuoteVolume`.

## Linear SWAP

**Status: Implemented.**

For a linear perpetual swap:

- `VolumeUnit` is `Contract`.
- `Volume` is the number of contracts.
- `BaseVolume` is the base size when the exchange provides that size.
- `QuoteVolume` is the quote turnover when the exchange provides that turnover.
- A missing contract value stays null. It is not replaced with 1. `ContractMultiplier` is `ctMult` only when the exchange sends `ctMult > 0`.

The contract count stays in `Volume`. It is not copied into `BaseVolume` and it is not stored as spot base size. Base size is known when the exchange sends it, or when both `ctVal` and `ctMult` are known and `ctValCcy` is the base currency, using the formula in the next section. A missing factor leaves the base size unknown.

`Instrument` stores `ContractMultiplier`, `ContractValue`, both currencies, `ContractKind`, and `HasCompleteLinearContractSpec`. The OKX SWAP mapper writes `VolumeUnit.Contract`. A Bybit linear kline is not given that unit by the current Bybit mapper.

## OKX linear USDT notional

**Status: Implemented** as metadata and as the documented identity. Backtest and paper do not apply it to fills.

Official linear notional, from the OKX place-order and position sections recorded in `docs/okx-api-reference-notes.md`:

```text
quoteNotional = sz × ctVal × markPx
```

`ctValCcy` is the currency of `ctVal`. For `BTC-USDT-SWAP` the published example is `ctVal = 0.01` BTC and `settleCcy = USDT`. `ctMult` is not in that formula. Published instrument examples leave `ctMult` as `""`. The adapter stores a positive `ctMult` when OKX sends one and leaves `ContractMultiplier` null when the field is empty. It does not substitute 1.

Example, `price` in USDT per BTC:

```text
quoteNotionalPerContract = 0.01 × price
baseSizePerContract = 0.01 BTC
```

For `N` contracts: `quoteNotional = N × 0.01 × price`. Futures accounting remains Not Implemented, so this identity is not a fill or PnL rule.

Storage for that series:

```text
Volume = N
VolumeUnit = Contract
```

`N` is the contract count. `BaseVolume` holds base size only when the exchange provides it. `QuoteVolume` holds quote turnover only when the exchange provides it. If `ctVal` or `ctMult` is missing, leave the missing factor null. The contract count is not multiplied by a substitute `ctVal` of 1.

## Funding

**Status: Partially Implemented.**

The funding rate is a public market series. It is not an input to backtest fills or paper fills.

- Backtest 0.1 records `FundingModeled = false`. `BacktestEngine` does not call `Position.ApplyFunding`. Cash, fees, and trade PnL ignore funding.
- Paper 0.1 does not call `Position.ApplyFunding`. A stored `Funding` amount can be rehydrated with a position; the paper loop does not add a funding payment from a rate.
- Bybit can read a current public funding rate. That read is not wired into the backtest or paper equity path. An OKX funding client is not part of this increment.

`Position.ApplyFunding` remains on the domain type for a later accounting increment. Calling it is outside version 0.1.

## Backtest and paper 0.1

**Status: Partially Implemented.**

Version 0.1 accepts spot base-asset volume only. SWAP candles, contract counts, and quote-asset volume do not enter the spot fill calculation.

Backtest, in `BacktestEngine.RunAsync`:

- An instrument whose type is not `Spot` throws `InvalidOperationException` with the message `BacktestEngine 0.1 applies spot fills only.` This happens before any fill.
- After `CandleWindow.Working`, any surviving candle with `VolumeUnit` other than `BaseAsset` throws `InvalidOperationException` with the message `BacktestEngine 0.1 accepts base-asset volume only.`
- The working set is closed candles with `OpenTimeUtc >= FromUtc` and `CloseTimeUtc <= ToUtc`, after stable de-duplication by open time. Candles dropped by that filter are not strategy input and are not volume-gated.
- Higher-timeframe candles are not volume-gated in this increment. Callers still pass only base-asset candles there. A linear instrument never reaches that path, because the type check has already rejected it.
- Fill size stays the risk-approved base quantity. The engine does not read `Candle.Volume` to size a fill, and it does not convert contracts with `ctVal`.

Paper 0.1:

- `CandleProcessingDecision` admits a closed candle only when its close age and the queue lag are not strictly greater than `MarketData:StaleAfterSeconds`. Equality is still eligible. A rejected candle can stay in `candles` and is marked Skipped. It does not change cash, equity, marks, signals, orders, or positions.
- Spot and `VolumeUnit.BaseAsset` are required before the ledger is loaded. A futures instrument or contract volume is rejected first. Bybit and OKX each have their own paper account per quote asset.
- An approved signal on a quote asset that does not match the ledger does not call the spot fill.
- `PaperExecutionVenue` rejects a linear perpetual before creating a paper order.
- `PortfolioLedger.ApplySpotFill` accepts spot instruments only.
- A candle key is stored on the ledger only after the candle is handled. Replaying that key does not fill again.

## Closed candles and gaps

**Status: Partially Implemented.**

Strategy input is a closed candle. On OKX, closed means `confirm = 1`. `confirm = 0` is the forming bar. It is not closed, and it is not strategy input. Mapping `confirm` onto `Candle.IsClosed` is adapter work and is not in this increment.

What version 0.1 does with the domain flag:

- `CandleWindow.Working` and `CandleWindow.Higher` drop `IsClosed = false`.
- Paper `HandleCandleAsync` returns immediately when the candle is not closed.
- `TrendFollowingStrategy` skips a candle that is not closed.

Historical gaps stay gaps. The backtest walks the closed candles it was given, ordered by open time, and does not insert a bar for a missing open time. Indicators see that shorter series. No component in the backtest path interpolates prices or volume across a hole.

Bybit historical kline mapping currently marks every candle it keeps as closed. That is separate from the OKX `confirm` rule. An unclosed Bybit update is not forwarded as a candle event.

## Look-ahead

**Status: Implemented.**

A candle is usable at its `CloseTimeUtc`. The decision time of a working bar is that bar's `CloseTimeUtc`. Another candle is visible when its own `CloseTimeUtc` is less than or equal to the decision time. The forming bar fails that test: it is not closed, and its close time has not been reached.

In the backtest loop the strategy receives the closed working prefix through the current bar, and higher-timeframe candles with `CloseTimeUtc <=` the current bar's `CloseTimeUtc` (`CandleWindow.CountClosedBy` / `ClosedBy`). The signal price is the decision bar's close. The entry fill, when the signal is approved and a later working bar exists, is the next bar's open plus adverse slippage. The next bar's close is not the signal price.

Paper builds the same closed prefix with `CloseTimeUtc <= asOf`. The trend-following strategy applies the same closed-and-not-after-`asOf` filter again.
