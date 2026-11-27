# Матрица ответственности — OKX и multi-exchange

| Область | Владелец | Проверяют |
|---|---|---|
| Multi-exchange architecture | Agent 1 | Lead, Agent 2, Agent 4, Agent 6 |
| Exchange registry | Agent 1 | Agent 2, Agent 4, Agent 6 |
| Host / DI / ingestion | Agent 2 | Agent 1, Agent 4, Agent 6 |
| OKX API specification | Agent 8 | Agent 4, Agent 6 |
| OKX adapter | Agent 4 | Agent 8, Agent 2, Agent 6 |
| Bybit regression | Agent 4 | Agent 6 |
| Instrument identity / capabilities | Agent 1 | Agent 3, Agent 4, Agent 5, Agent 6 |
| Volume and contract semantics | Agent 3 | Agent 1, Agent 4, Agent 5, Agent 8 |
| PostgreSQL schema / migrations | Agent 5 | Agent 1, Agent 2, Agent 6 |
| Checkpoint / replay | Agent 5 | Agent 2, Agent 4, Agent 6 |
| Automated tests | Agent 6 | Все агенты передают сценарии |
| CI / Docker / operations | Agent 7 | Agent 2, Agent 5, Agent 6 |
| Final integration | Lead | Все агенты |

Один файл — один владелец из `team-structure.md`. Reviewer не правит чужой файл. Общий контракт меняет только Lead в `docs/agents/multi-exchange-contract.md`.
