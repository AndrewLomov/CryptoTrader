# ADR 003. Live-торговля выключена в 0.1

## Статус

Принято

## Контекст

Реальное исполнение допускается только после отдельного допуска. Значение по умолчанию не должно случайно включать заявки.

## Решение

`ExecutionOptions.LiveTradingEnabled` по умолчанию `false`. `RiskOptions.AllowFuturesLive` и `AllowCrossMargin` по умолчанию `false`. `StartupConfigurationValidator` бросает `InvalidOperationException`, если любой из этих флагов истинный, если `Execution:Mode` равен `Live`, или если `Api:AdminApiKey` пустой. Доменные `Order` и `TradingAccount` не создаются и не восстанавливаются в режиме `Live`. `ITradingClient.SubmitAsync` при выключенном live не открывает сеть и возвращает `LiveTradingDisabled` с `UnknownState = false`.

## Последствия

Paper, backtest и testnet описываются доменом. Live Spot и Live Futures в 0.1 недостижимы ни конфигурацией, ни агрегатом.
