using System.Data;
using CryptoTrader.Application.Abstractions.Persistence;
using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.Instruments;
using CryptoTrader.Domain.MarketData;
using CryptoTrader.Domain.Trading;
using CryptoTrader.Infrastructure.Persistence;
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
