using System.Data;
using CryptoTrader.Application.Abstractions.MarketData;
using CryptoTrader.Application.Abstractions.Paper;
using CryptoTrader.Application.Abstractions.Persistence;
using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.Instruments;
using CryptoTrader.Domain.MarketData;
using CryptoTrader.Domain.Trading;
using CryptoTrader.Infrastructure.Persistence;
using CryptoTrader.Infrastructure.Persistence.Repositories;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shouldly;

namespace CryptoTrader.IntegrationTests;

[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class DatabaseTests
{
    private readonly PostgresContainerFixture _postgres;

    public DatabaseTests(PostgresContainerFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task Migration_applies_on_an_empty_database()
    {
        var connectionString = await CreateDatabaseAsync();
        await using var context = CreateContext(connectionString);

        var pending = await context.Database.GetPendingMigrationsAsync();
        pending.ShouldNotBeEmpty();

        await context.Database.MigrateAsync();

        (await context.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();
        (await context.Database.GetAppliedMigrationsAsync()).ShouldNotBeEmpty();
        (await TableExistsAsync(context, "candles")).ShouldBeTrue();
        (await TableExistsAsync(context, "orders")).ShouldBeTrue();
    }

    [Fact]
    public async Task Candle_upsert_keeps_one_row_and_updates_ohlc()
    {
        await using var context = await MigratedContextAsync();
        var instrument = SpotBtc();
        await Create<IInstrumentRepository>(context).UpsertAsync(instrument, CancellationToken.None);
        await context.SaveChangesAsync();

        var open = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
        var candles = Create<ICandleRepository>(context);
        await candles.UpsertRangeAsync([Hour(open, close: 100m)], CancellationToken.None);
        await context.SaveChangesAsync();
        await candles.UpsertRangeAsync([Hour(open, close: 110m)], CancellationToken.None);
        await context.SaveChangesAsync();

        var stored = await candles.GetRangeAsync(
            ExchangeId.Bybit,
            instrument.Id,
            Timeframe.OneHour,
            open,
            open.AddHours(2),
            CancellationToken.None);

        var candle = stored.ShouldHaveSingleItem();
        candle.OpenTimeUtc.ShouldBe(open);
        candle.Close.ShouldBe(110m);
        candle.Open.ShouldBe(100m);
    }

    [Fact]
    public async Task Duplicate_client_order_id_is_rejected()
    {
        await using var context = await MigratedContextAsync();
        var now = new DateTimeOffset(2026, 5, 2, 0, 0, 0, TimeSpan.Zero);
        var instrument = SpotBtc();
        var account = TradingAccount.Create(
            TradingAccountId.New(),
            ExchangeId.Bybit,
            "paper",
            TradingMode.Paper,
            now);
        await Create<IInstrumentRepository>(context).UpsertAsync(instrument, CancellationToken.None);
        await Create<ITradingAccountRepository>(context).UpsertAsync(account, CancellationToken.None);
        await context.SaveChangesAsync();

        var orders = Create<IOrderRepository>(context);
        await orders.AddAsync(MarketBuy(account.Id, instrument, "same-client", now), CancellationToken.None);
        await Should.ThrowAsync<DbUpdateException>(() =>
            orders.AddAsync(MarketBuy(account.Id, instrument, "same-client", now.AddMinutes(1)), CancellationToken.None));
    }

    [Fact]
    public async Task Latest_schema_isolates_exchanges_quotes_and_checkpoints()
    {
        await using var context = await MigratedContextAsync();
        (await ColumnExistsAsync(context, "paper_accounts", "applied_candle_keys")).ShouldBeTrue();
        (await ColumnExistsAsync(context, "candle_consumer_checkpoints", "skip_reason")).ShouldBeTrue();
        (await IndexExistsAsync(context, "ux_paper_accounts_exchange_quote")).ShouldBeTrue();

        var open = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var bybit = Hour(ExchangeId.Bybit, "BYBIT-SPOT-BTCUSDT", open, 100m);
        var okx = Hour(ExchangeId.Okx, "OKX-SPOT-BTC-USDT", open, 200m);
        var candles = Create<ICandleRepository>(context);
        await candles.UpsertRangeAsync([bybit, okx], CancellationToken.None);

        var bybitStored = await candles.GetRangeAsync(ExchangeId.Bybit, bybit.Instrument, Timeframe.OneHour, open, open, CancellationToken.None);
        var okxStored = await candles.GetRangeAsync(ExchangeId.Okx, okx.Instrument, Timeframe.OneHour, open, open, CancellationToken.None);
        bybitStored.Single().Close.ShouldBe(100m);
        okxStored.Single().Close.ShouldBe(200m);

        var checkpoint = Create<ICandleConsumerCheckpoint>(context);
        var now = open.AddHours(1);
        (await checkpoint.MarkPendingAsync(bybit, ICandleConsumerCheckpoint.PaperConsumer, now, CancellationToken.None)).ShouldBeTrue();
        await checkpoint.MarkSkippedAsync(bybit, ICandleConsumerCheckpoint.PaperConsumer, "Closed candle is older than the stale window.", now, CancellationToken.None);
        (await checkpoint.MarkPendingAsync(bybit, ICandleConsumerCheckpoint.PaperConsumer, now, CancellationToken.None)).ShouldBeFalse();
        (await checkpoint.MarkPendingAsync(okx, ICandleConsumerCheckpoint.PaperConsumer, now, CancellationToken.None)).ShouldBeTrue();

        var pending = await checkpoint.ListPendingCandlesAsync(ICandleConsumerCheckpoint.PaperConsumer, 10, CancellationToken.None);
        pending.Single().Exchange.ShouldBe(ExchangeId.Okx);

        var paper = Create<IPaperStateRepository>(context);
        var bybitAccount = TradingAccountId.New();
        var okxAccount = TradingAccountId.New();
        var usdcAccount = TradingAccountId.New();
        await paper.SaveAsync(Paper(bybitAccount, ExchangeId.Bybit, "USDT", 9_000m, "bybit-candle"), CancellationToken.None);
        await paper.SaveAsync(Paper(okxAccount, ExchangeId.Okx, "USDT", 8_000m, "okx-candle"), CancellationToken.None);
        await paper.SaveAsync(Paper(usdcAccount, ExchangeId.Bybit, "USDC", 7_000m, "usdc-candle"), CancellationToken.None);

        (await paper.LoadAsync(bybitAccount, CancellationToken.None))!.Cash.ShouldBe(9_000m);
        (await paper.LoadAsync(bybitAccount, CancellationToken.None))!.AppliedCandleKeys.ShouldBe(["bybit-candle"]);
        (await paper.LoadAsync(okxAccount, CancellationToken.None))!.Cash.ShouldBe(8_000m);
        (await paper.LoadAsync(usdcAccount, CancellationToken.None))!.QuoteAsset.ShouldBe("USDC");
        await Should.ThrowAsync<DbUpdateException>(() =>
            paper.SaveAsync(Paper(TradingAccountId.New(), ExchangeId.Bybit, "USDT", 1m, "duplicate"), CancellationToken.None));
    }

    [Fact]
    public async Task Upgrade_from_initial_keeps_existing_bybit_candles()
    {
        var connectionString = await CreateDatabaseAsync();
        var open = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
        await using (var initial = CreateContext(connectionString))
        {
            await initial.Database.MigrateAsync("20261127085645_Initial");
            await initial.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO candles (exchange_id, instrument_id, timeframe, open_time_utc, close_time_utc, open, high, low, close, volume, is_closed)
                VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10})
                """,
                "BYBIT",
                "BYBIT-SPOT-BTCUSDT",
                "1h",
                open,
                open.AddHours(1),
                100m,
                110m,
                90m,
                105m,
                2m,
                true);
        }

        await using var upgraded = CreateContext(connectionString);
        await upgraded.Database.MigrateAsync();
        (await upgraded.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();
        var stored = await Create<ICandleRepository>(upgraded).GetRangeAsync(
            ExchangeId.Bybit,
            new InstrumentId("BYBIT-SPOT-BTCUSDT"),
            Timeframe.OneHour,
            open,
            open,
            CancellationToken.None);

        var candle = stored.ShouldHaveSingleItem();
        candle.Close.ShouldBe(105m);
        candle.VolumeUnit.ShouldBe(VolumeUnit.BaseAsset);
        candle.Exchange.ShouldBe(ExchangeId.Bybit);
    }

    [Fact]
    public async Task Candle_and_checkpoint_roll_back_together_when_commit_does_not_happen()
    {
        var connectionString = await CreateDatabaseAsync();
        var open = new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero);
        var candle = Hour(ExchangeId.Bybit, "BYBIT-SPOT-BTCUSDT", open, 100m);
        await using (var context = CreateContext(connectionString))
        {
            await context.Database.MigrateAsync();
            var intake = new CandleIntake(context, new CandleRepository(context), new CandleConsumerCheckpoint(context))
            {
                CrashBeforeCommit = true,
            };

            var failure = await Should.ThrowAsync<InvalidOperationException>(() =>
                intake.PersistPendingAsync(candle, ICandleConsumerCheckpoint.PaperConsumer, open.AddHours(1), CancellationToken.None));
            failure.Message.ShouldBe(CandleIntake.CrashBeforeCommitMessage);
        }

        await using (var verify = CreateContext(connectionString))
        {
            (await verify.Candles.CountAsync()).ShouldBe(0);
            (await verify.CandleConsumerCheckpoints.CountAsync()).ShouldBe(0);
        }

        await using var recovered = CreateContext(connectionString);
        var stored = new CandleIntake(recovered, new CandleRepository(recovered), new CandleConsumerCheckpoint(recovered));
        (await stored.PersistPendingAsync(candle, ICandleConsumerCheckpoint.PaperConsumer, open.AddHours(1), CancellationToken.None)).ShouldBeTrue();
        var pending = await new CandleConsumerCheckpoint(recovered).ListPendingCandlesAsync(ICandleConsumerCheckpoint.PaperConsumer, 10, CancellationToken.None);
        pending.ShouldHaveSingleItem().Close.ShouldBe(100m);
    }

    [Fact]
    public async Task Global_emergency_stop_is_stored_without_a_paper_account()
    {
        await using var context = await MigratedContextAsync();
        (await TableExistsAsync(context, "paper_trading_control")).ShouldBeTrue();
        var control = new PaperTradingControlRepository(context);
        (await control.GetAsync(CancellationToken.None)).EmergencyStop.ShouldBeFalse();

        await control.EngageAsync("halt", new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero), CancellationToken.None);

        var engaged = await control.GetAsync(CancellationToken.None);
        engaged.EmergencyStop.ShouldBeTrue();
        engaged.Reason.ShouldBe("halt");
        (await context.PaperAccounts.CountAsync()).ShouldBe(0);

        await control.ReleaseAsync(new DateTimeOffset(2026, 6, 2, 1, 0, 0, TimeSpan.Zero), CancellationToken.None);
        (await control.GetAsync(CancellationToken.None)).EmergencyStop.ShouldBeFalse();
    }

    private async Task<CryptoTraderDbContext> MigratedContextAsync()
    {
        var context = CreateContext(await CreateDatabaseAsync());
        await context.Database.MigrateAsync();
        return context;
    }

    private static CryptoTraderDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<CryptoTraderDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new CryptoTraderDbContext(options);
    }

    private async Task<string> CreateDatabaseAsync()
    {
        var name = "ct_" + Guid.NewGuid().ToString("N");
        await using var connection = new NpgsqlConnection(_postgres.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"""CREATE DATABASE "{name}" """, connection);
        await command.ExecuteNonQueryAsync();
        var builder = new NpgsqlConnectionStringBuilder(_postgres.ConnectionString) { Database = name };
        return builder.ConnectionString;
    }

    private static PaperState Paper(TradingAccountId accountId, ExchangeId exchange, string quote, decimal cash, string candleKey) =>
        new(
            accountId,
            exchange,
            quote,
            cash,
            cash,
            cash,
            cash,
            new DateOnly(2026, 6, 1),
            false,
            [],
            [],
            new Dictionary<InstrumentId, decimal>(),
            new DateTimeOffset(2026, 6, 1, 1, 0, 0, TimeSpan.Zero),
            [candleKey]);

    private static Candle Hour(ExchangeId exchange, string instrumentId, DateTimeOffset open, decimal close) =>
        Candle.Create(
            exchange,
            new InstrumentId(instrumentId),
            Timeframe.OneHour,
            open,
            open.AddHours(1),
            close,
            close,
            close,
            close,
            1m,
            true);

    private static async Task<bool> ColumnExistsAsync(CryptoTraderDbContext context, string table, string column)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = @table AND column_name = @column)
            """;
        var tableParameter = command.CreateParameter();
        tableParameter.ParameterName = "table";
        tableParameter.Value = table;
        command.Parameters.Add(tableParameter);
        var columnParameter = command.CreateParameter();
        columnParameter.ParameterName = "column";
        columnParameter.Value = column;
        command.Parameters.Add(columnParameter);
        var result = await command.ExecuteScalarAsync();
        return result is true;
    }

    private static async Task<bool> IndexExistsAsync(CryptoTraderDbContext context, string index)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = 'public' AND indexname = @index)";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "index";
        parameter.Value = index;
        command.Parameters.Add(parameter);
        var result = await command.ExecuteScalarAsync();
        return result is true;
    }

    private static async Task<bool> TableExistsAsync(CryptoTraderDbContext context, string table)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = @name)
            """;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "name";
        parameter.Value = table;
        command.Parameters.Add(parameter);
        var result = await command.ExecuteScalarAsync();
        return result is true;
    }

    private static TRepository Create<TRepository>(CryptoTraderDbContext context)
        where TRepository : class
    {
        var implementation = RepositoryTypes()
            .Single(type => type is { IsClass: true, IsAbstract: false } && typeof(TRepository).IsAssignableFrom(type));
        var constructor = implementation
            .GetConstructors()
            .OrderBy(candidate => candidate.GetParameters().Length)
            .First(candidate => candidate.GetParameters().All(parameter => CanResolve(parameter.ParameterType)));
        var args = constructor.GetParameters().Select(parameter => Resolve(parameter.ParameterType, context)).ToArray();
        return (TRepository)constructor.Invoke(args);
    }

    private static IEnumerable<Type> RepositoryTypes()
    {
        try
        {
            return typeof(CryptoTraderDbContext).Assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.OfType<Type>();
        }
    }

    private static bool CanResolve(Type type) =>
        type.IsAssignableFrom(typeof(CryptoTraderDbContext)) || type == typeof(TimeProvider);

    private static object Resolve(Type type, CryptoTraderDbContext context)
    {
        if (type.IsAssignableFrom(typeof(CryptoTraderDbContext)))
        {
            return context;
        }

        if (type == typeof(TimeProvider))
        {
            return TimeProvider.System;
        }

        throw new InvalidOperationException($"Unsupported repository dependency {type}.");
    }

    private static Instrument SpotBtc() => Instrument.Create(
        new InstrumentId("BTCUSDT"),
        ExchangeId.Bybit,
        "BTCUSDT",
        "BTC",
        "USDT",
        InstrumentType.Spot,
        0.01m,
        0.001m,
        0.001m,
        5m,
        null,
        true);

    private static Candle Hour(DateTimeOffset open, decimal close) => Candle.Create(
        ExchangeId.Bybit,
        new InstrumentId("BTCUSDT"),
        Timeframe.OneHour,
        open,
        open.AddHours(1),
        open: 100m,
        high: Math.Max(100m, close),
        low: Math.Min(100m, close),
        close,
        1m,
        true);

    private static Order MarketBuy(TradingAccountId accountId, Instrument instrument, string clientOrderId, DateTimeOffset now) =>
        Order.Create(
            OrderId.New(),
            clientOrderId,
            accountId,
            ExchangeId.Bybit,
            instrument,
            OrderSide.Buy,
            OrderType.Market,
            new Quantity(0.01m),
            null,
            false,
            TradingMode.Paper,
            now);
}
