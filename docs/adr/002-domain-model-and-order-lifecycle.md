# ADR 002. Доменная модель и жизненный цикл заявки

## Статус

Принято

## Контекст

Контракт фиксирует состояния заявки и инварианты позиции, но не назначает числовые значения всем перечислениям и не раскладывает типы по вложенным пространствам имён.

## Решение

Типы живут во вложенных пространствах `CryptoTrader.Domain.Common`, `Instruments`, `MarketData`, `Trading`, `Risk`, `Strategies`. `ExchangeId` и `InstrumentId` — непустые строки. `OrderId`, `ExecutionId`, `StrategyInstanceId`, `TradingAccountId` — непустой `Guid`.

Явные значения перечислений: `OrderStatus` 1–9 в порядке контракта, `OrderSide` Buy=1/Sell=2, `OrderType` Market=1/Limit=2, `TradingMode` Backtest=1/Paper=2/Testnet=3/Live=4, `PositionSide` Flat=0/Long=1/Short=2, `SignalAction` EnterLong=1/ExitLong=2, `StrategyInstanceStatus` Created=1/Running=2/Stopped=3.

`Rehydrate` восстанавливает агрегат без новых событий. Повторный `ApplyFill` с тем же `ExchangeTradeId` не меняет количество. Переход `Filled -> Submitted` запрещён таблицей состояний. Spot не открывает short. Одно исполнение не разворачивает позицию через ноль. Плечо в 0.1 только 1. Комиссия покупки остаётся в `Fees` и не вычитается из `RealizedPnl`; комиссия продажи вычитается из `RealizedPnl` и тоже входит в `Fees`. Источник истины по деньгам счёта — cash и equity книги, а не `RealizedPnl`.

Для выхода без позиции добавлен код `NoOpenPosition`: в утверждённом списке отдельного кода не было.

## Последствия

База и API хранят те же целые значения. `default` у record struct не является допустимым идентификатором: идентификатор создаётся конструктором.
