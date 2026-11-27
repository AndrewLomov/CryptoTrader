# HTTP API CryptoTrader 0.1

Процесс не запускается, пока не заданы переменные окружения `Persistence__ConnectionString` и `Api__AdminApiKey`. В `appsettings.json` строка подключения и ключ намеренно пустые. Реальный секрет в файл не записывается.

Режим по умолчанию — Paper. `Execution:LiveTradingEnabled`, `Risk:AllowFuturesLive` и `Risk:AllowCrossMargin` равны false. Живые заявки не отправляются: `ITradingClient.SubmitAsync` из бумажного контура не вызывается. Публичные рыночные запросы Bybit разрешены.

## Аутентификация

Все маршруты под `/api/v1` требуют заголовок `X-Api-Key`. Значение сравнивается с `Api:AdminApiKey` за постоянное время. Нет ключа или ключ неверен — ответ `401` и тело `ApiErrorResponse`:

```json
{ "code": "Unauthorized", "message": "A valid X-Api-Key header is required." }
```

Без ключа доступны:

- `GET /health/live`
- `GET /health/ready`
- OpenAPI: `GET /openapi/v1.json`
- Prometheus, если `Observability:ExposePrometheus` равен true: `GET /metrics` (путь из `Observability:PrometheusPath`). Это внутренний сбор метрик, не публичный торговый API.

`GET /health/ready` проверяет только PostgreSQL. Недоступность Bybit не переводит готовность в ошибку. База недоступна — `503` и `databaseReady: false`.

## Маршруты

| Метод | Путь | Назначение |
|---|---|---|
| GET | `/health/live` | Процесс жив |
| GET | `/health/ready` | База доступна |
| GET | `/api/v1/exchanges` | Биржа BYBIT и режим исполнения. Соединение с биржей не опрашивается |
| GET | `/api/v1/instruments` | Инструменты. Фильтры `exchange`, `symbol` |
| GET | `/api/v1/market/candles` | Свечи из базы. Параметры `exchange`, `instrument`, `timeframe`, `fromUtc`, `toUtc` |
| POST | `/api/v1/market/candles/import` | Загрузка закрытых свечей без сети |
| POST | `/api/v1/market/candles/sync` | Загрузка истории с Bybit и сохранение |
| GET | `/api/v1/strategies` | Каталог стратегий |
| POST | `/api/v1/strategies/{id}/instances` | Создать экземпляр. Ответ `201` |
| POST | `/api/v1/strategy-instances/{id}/start` | Запустить экземпляр |
| POST | `/api/v1/strategy-instances/{id}/stop` | Остановить экземпляр. Открытая позиция не закрывается |
| GET | `/api/v1/strategy-instances` | Список экземпляров |
| POST | `/api/v1/backtests` | Прогон бэктеста по свечам из базы. Ответ `201` и `runId` |
| GET | `/api/v1/backtests/{id}` | Отчёт прогона |
| GET | `/api/v1/orders` | Заявки бумажного счёта. `tradingMode`: Backtest, Paper или Testnet |
| GET | `/api/v1/positions` | Позиции бумажного счёта |
| POST | `/api/v1/risk/emergency-stop` | Включить аварийную остановку. Новые входы и выходы блокируются |

Снятие аварийной остановки в API нет. Размер позиции и переходы состояния заявки считаются в риск-менеджере и журнале портфеля, не в обработчиках HTTP.

Если для бэктеста нет закрытых свечей рабочего или старшего таймфрейма, сервис возвращает ошибку и не достраивает ряд.

Базовый адрес локального HTTP: `http://localhost:56186`. В примерах `$API_KEY` — значение `Api__AdminApiKey`.

## Примеры

Проверка живости, без ключа:

```bash
curl http://localhost:56186/health/live
curl http://localhost:56186/health/ready
```

Импорт закрытых свечей:

```bash
curl -X POST http://localhost:56186/api/v1/market/candles/import \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: $API_KEY" \
  -d "{\"exchange\":\"BYBIT\",\"symbol\":\"BTCUSDT\",\"timeframe\":\"1h\",\"candles\":[{\"openTimeUtc\":\"2026-01-01T00:00:00Z\",\"closeTimeUtc\":\"2026-01-01T01:00:00Z\",\"open\":100,\"high\":110,\"low\":90,\"close\":105,\"volume\":12}]}"
```

Бэктест по уже загруженным свечам. Живой ордер при этом не создаётся:

```bash
curl -X POST http://localhost:56186/api/v1/backtests \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: $API_KEY" \
  -d "{\"strategyId\":\"trend-following\",\"strategyVersion\":\"0.1.0\",\"parametersJson\":\"{}\",\"exchange\":\"BYBIT\",\"instrument\":\"BTCUSDT\",\"fromUtc\":\"2026-01-01T00:00:00Z\",\"toUtc\":\"2026-02-01T00:00:00Z\",\"initialEquity\":10000,\"feeRate\":0.001,\"slippageBps\":5,\"exitOnTrendBreak\":true,\"risk\":{\"riskPerTradePercent\":0.5,\"maxDailyLossPercent\":1,\"maxOpenPositions\":2,\"maxExposurePercent\":100,\"feeRate\":0.001,\"slippageBps\":5,\"averagingDownEnabled\":false,\"maxDrawdownPercent\":10}}"
```

Аварийная остановка:

```bash
curl -X POST http://localhost:56186/api/v1/risk/emergency-stop \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: $API_KEY" \
  -d "{\"reason\":\"operator\"}"
```
