# План разработки CryptoTrader 0.1

Сквозной сценарий версии: биржевые или заранее загруженные свечи → стратегия → TradeSignal → Risk Manager → Paper или Backtest execution → PortfolioLedger → PostgreSQL → API.

Live-ордера не отправляются.

| Этап | Владелец | Результат | Статус |
|---|---|---|---|
| 0. Команда и контракты | Lead | Роли, матрица, integration-contracts | In Progress |
| 1. Foundation | Architect, DevOps | Solution, Domain, порты, Docker-заготовка, запуск с PostgreSQL | Not Started |
| 2. Domain и Application | Architect, C# Developer, QA | Модель, use cases, Risk Manager, unit-тесты | Not Started |
| 3. Exchange | Trading Systems, QA | Публичные данные Bybit, reconnect, нормализация | Not Started |
| 4. Database | Database Engineer, QA | Схема, миграции, идемпотентные свечи | Not Started |
| 5. Strategy | Quant, QA | EMA, RSI, ATR, Trend Following | Not Started |
| 6. Risk | C# Developer, Quant, QA | Лимиты и position sizing | Not Started |
| 7. Backtesting | Quant, Trading Systems, QA | Воспроизводимый прогон без look-ahead | Not Started |
| 8. Paper | Trading Systems, C# Developer, QA | Симуляция без реальных заявок | Not Started |
| 9. API и monitoring | C# Developer, DevOps, QA | Управление, health, логи, метрики | Not Started |
| 10. Integration | Lead | Release build, тесты, отчёт | Not Started |

Вне версии 0.1: Live Spot, Live Futures, OKX-адаптер, скальпинг, арбитраж, ML, SaaS, Kubernetes, Kafka.
