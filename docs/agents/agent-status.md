# Статус агентов

| Agent | Status | Current Task | Completed | Blockers |
|---|---|---|---|---|
| Senior .NET Architect | Review | Каркас сдан, Lead проверяет интеграцию | Solution, Domain, порты, ADR 001–009, Release build | — |
| Senior C# Developer | Review | Host собран в Release | Risk Manager, API, Program.cs | Процесс не стартует без строки подключения и Api__AdminApiKey |
| Quantitative Developer | Review | Стратегия и backtest сданы | EMA, RSI, ATR, Trend Following 0.1.0, BacktestEngine | Прибыльность не заявлена; прогрев 1h должен попадать в окно теста |
| Algorithmic Trading Systems Engineer | Review | Bybit public V5 и Paper venue сданы | SubmitAsync без сети, reconnect, PaperExecutionVenue | Приватная торговля не реализована |
| Database Engineer | Review | Схема сдана, Lead убрал reflection из репозитория заявок | Миграция 20261127085645_Initial, 24 таблицы | Исполнения пока не пишутся отдельной таблицей executions |
| QA Automation Engineer | Review | Прогон Lead после сборки Host | 43 теста Release, включая 3 integration | Нет API smoke через WebApplicationFactory |
| DevOps Engineer | Review | Ожидает точку входа Host для реального образа | Compose, Dockerfile, CI, backup/restore, runbook | HTTP в контейнере появится после Program.cs |

Статус Completed ставит только Lead после проверки кода и сборки.
