# ADR 011. Реестр бирж

## Статус

Принято

## Контекст

Один `IMarketDataClient` в DI не различает биржи. Ingestion и API должны находить адаптер по `ExchangeId`, а не по порядку регистрации.

## Решение

`IExchangeRegistry` и `ExchangeRegistry` лежат в `CryptoTrader.Exchanges.Abstractions`. Реестр строится из `IEnumerable<IExchangeAdapter>` и хранит адаптеры в словаре. Повторный `ExchangeId` и неизвестный идентификатор дают `InvalidOperationException`. `ExchangeId` адаптера — `new ExchangeId(ExchangeCode)`. Для Bybit это `BYBIT`, для OKX — `OKX`.

`OrderBook` и `FundingRates` по умолчанию `null`. `SupportsLinearPerpetual` — это публичные linear-данные. `SupportsFuturesAccounting` по умолчанию `false`. `SupportsLiveOrders` и `SupportsPrivateAccount` для публичного контура остаются `false`.

`GetCandleHistoryAsync` по умолчанию не доказывает полноту диапазона. Пустой список подписок допустим. Подписка допустима только на `BYBIT` или `OKX` и только если эта биржа включена.

## Последствия

Host собирает реестр из всех `IExchangeAdapter` после регистрации адаптеров. Проект OKX подключается отдельно и не получает приватную торговлю. Учёт фьючерсов этим решением не включается. Доменный таймфрейм `1d` — сутки UTC; на OKX это бар `1Dutc`, не `1D`.
