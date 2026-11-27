# Preflight, 27 ноября 2026

Срез до правок этого прохода. Коммитов поверх проверенного SHA ещё не было.

| | |
|---|---|
| Ветка | `feature/okx-multi-exchange` |
| HEAD на старте | `79367b2181d443bf7c42e58d2cbb86bfaaf0caab` |
| `main` / `origin/main` | `8130a9e2d582fab944d0c9db0a0c8983e9f09cae` |
| Рабочее дерево | изменённые файлы в `.vs/`, неотслеживаемый prompt в `md/` |

База до правок, Release:

| Набор | Результат |
|---|---|
| `dotnet test --filter Category!=Integration` | 76 passed, 0 failed. Domain 28, Strategies 5, Application 6, Backtesting 5, Architecture 13, Contract 19 |
| `dotnet test --filter Category=Integration` | 3 passed, 0 failed |
| `dotnet format CryptoTrader.slnx --verify-no-changes` | код 2. UTF-8 BOM в `20261127085645_Initial.cs` и локальный CRLF |

`TreatWarningsAsErrors` в `Directory.Build.props` был `false` и остаётся `false`.

Ограничения прохода: без реальных заявок, без live, без приватного API, без ключей биржи. Демо OKX, учёт фьючерсов и новые стратегии не начинались. В `main` не коммитили, историю не переписывали, PR не сливали.
