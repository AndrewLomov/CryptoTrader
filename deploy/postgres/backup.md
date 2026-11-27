# Резервная копия PostgreSQL

Скрипты, расписание, срок хранения и проверка восстановления ведёт DevOps. Этот файл только фиксирует границу.

Копируется база приложения целиком, включая данные и служебную таблицу `__EFMigrationsHistory`. Список таблиц — в [docs/database-schema.md](../../docs/database-schema.md).

Строка подключения берётся из секретного хранилища или из переменной окружения, не из репозитория и не из этого файла. В копию не входят API-секреты биржи: в `api_credentials_metadata` их нет.

Ориентир для локальной проверки, без пароля в команде:

```text
pg_dump --format=custom --file cryptotrader.dump --dbname "$CRYPTO_TRADER_CONNECTION"
pg_restore --dbname "$CRYPTO_TRADER_CONNECTION" --clean --if-exists cryptotrader.dump
```

Команды не являются расписанием продакшена. DevOps подтверждает, что восстановленная база поднимает приложение и health check `database`.
