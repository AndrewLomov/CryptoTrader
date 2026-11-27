# ADR 005. Одна таблица заявок

## Статус

Принято

## Контекст

Отдельный агрегат paper-заявок повторил бы тот же жизненный цикл и разошёлся бы с backtest.

## Решение

Есть один агрегат `Order` и один порт `IOrderRepository`. Режим хранится в `Order.TradingMode` и колонке `orders.trading_mode`. Отдельных `PaperOrder` и `paper_orders` нет. Уникальность клиентского идентификатора — пара счёт и `ClientOrderId`, поэтому `FindByClientOrderIdAsync` принимает `TradingAccountId`.

## Последствия

Paper и backtest пишут в одну таблицу. Фильтр API по режиму читает ту же модель. Аудиторская история исполнений живёт в `Execution`, не внутри строки заявки.
