# Quant: volume semantics and spot backtest gate

Date: 2026-11-27. Agent 3. No commit. `dotnet build` and `dotnet test` were not run. Domain, exchange adapters, Host, Infrastructure, and position PnL were not changed. No exchange integration was added.

## Formula

Pending confirmation: `docs/okx-api-reference-notes.md` is not in the repository.

When `ctValCcy` is the base currency:

```text
baseNotionalPerContract = ctVal * ctMult
quoteNotionalPerContract = baseNotionalPerContract * price
```

OKX linear USDT example: `ctVal` 0.01 BTC, `ctMult` 1, `ctValCcy` BTC, `settleCcy` USDT. One contract is 0.01 BTC, and the quote notional is `0.01 * price`. The contract count is `Volume` with `VolumeUnit.Contract`. It is not base volume. A missing `ctVal` stays null and is not replaced with 1.

Full rule and status labels: `docs/market-data-semantics.md`.

## Test

`Linear_instrument_or_contract_volume_candle_does_not_run_as_a_spot_backtest` in `tests/CryptoTrader.Backtesting.Tests/BacktestEngineTests.cs`.

The linear instrument expects `InvalidOperationException`: `BacktestEngine 0.1 applies spot fills only.` The spot instrument with a `VolumeUnit.Contract` candle expects `InvalidOperationException`: `BacktestEngine 0.1 accepts base-asset volume only.`

## Gate

`BacktestEngine.RunAsync` rejects a non-spot instrument before the candle loop. It then rejects any working candle whose `VolumeUnit` is not `BaseAsset`. Working candles are the closed in-range series from `CandleWindow.Working`. Higher-timeframe candles are not volume-gated. Paper does not read `VolumeUnit`; a Bybit linear candle can still reach strategy evaluation before the spot fill is skipped.

## Compile dependency

`Candle.VolumeUnit` and the trailing `Candle.Create` argument `VolumeUnit` are referenced here and were not on `Candle` yet. Those members belong to the domain contract. This backtest project compiles after they exist.

## 27 ноября 2026

Текст выше оставлен как исторический. Paper теперь читает `VolumeUnit` до загрузки книги: контрактный объём и не-spot счёт не создают и капитал не меняют. Устаревшая свеча не двигает mark, equity, PnL и просадку. Формула spot fill и backtest не менялись. Прибыльность не оценивалась.
