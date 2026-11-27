# Матрица ответственности

| Область | Ответственный | Проверяет |
|---|---|---|
| Архитектура Solution | .NET Architect | Lead Agent |
| Domain | .NET Architect | C# Developer, QA |
| Application | C# Developer | .NET Architect |
| Exchange Abstractions | Trading Systems Engineer | .NET Architect |
| Bybit Connector | Trading Systems Engineer | QA |
| Market Data | Trading Systems Engineer | C# Developer, QA |
| Индикаторы | Quantitative Developer | C# Developer, QA |
| Стратегии | Quantitative Developer | Trading Systems Engineer, QA |
| Risk Manager | C# Developer | Quant, Trading Systems, QA |
| Order Execution | Trading Systems Engineer | C# Developer, QA |
| Backtesting | Quantitative Developer | Trading Systems Engineer, QA |
| PostgreSQL | Database Engineer | C# Developer, QA |
| EF Core | Database Engineer | .NET Architect |
| API | C# Developer | QA, .NET Architect |
| Тестирование | QA Automation Engineer | Все агенты |
| Docker | DevOps Engineer | C# Developer |
| CI/CD | DevOps Engineer | QA |
| Monitoring | DevOps Engineer | Trading Systems Engineer |
| Финальная интеграция | Lead Agent | Все агенты |

Правило единственного владельца: файл из таблицы владения в `team-structure.md` меняет только указанный агент. Изменение общего контракта возвращается Lead и .NET Architect, а не вносится в чужой проект.
