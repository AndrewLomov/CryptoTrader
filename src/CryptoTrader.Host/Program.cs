using CryptoTrader.Application.Configuration;
using CryptoTrader.Application.DependencyInjection;
using CryptoTrader.Application.Observability;
using CryptoTrader.Backtesting;
using CryptoTrader.Contracts.Responses;
using CryptoTrader.Exchanges.Abstractions;
using CryptoTrader.Exchanges.Bybit;
using CryptoTrader.Exchanges.Okx;
using CryptoTrader.Host.Api;
using CryptoTrader.Host.Health;
using CryptoTrader.Host.MarketData;
using CryptoTrader.Host.Observability;
using CryptoTrader.Host.Security;
using CryptoTrader.Infrastructure.Paper;
using CryptoTrader.Infrastructure.Persistence;
using CryptoTrader.Strategies.TrendFollowing;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, _, loggerConfiguration) =>
{
    var level = LogLevelParser.Parse(context.Configuration["Observability:MinimumLevel"]);
    loggerConfiguration
        .MinimumLevel.Is(level)
        .Enrich.FromLogContext()
        .WriteTo.Console(new RedactingJsonFormatter());
});

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddBybit(builder.Configuration);
builder.Services.AddOkx(builder.Configuration);
builder.Services.AddSingleton<IExchangeRegistry>(sp => new ExchangeRegistry(sp.GetServices<IExchangeAdapter>()));
builder.Services.AddSingleton<MarketDataFeedHealth>();
builder.Services.AddPaperExecution();
builder.Services.AddTrendFollowing();
builder.Services.AddBacktesting();
builder.Services.AddOpenApi();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<DatabaseReadyProbe>();
builder.Services.AddHostedService<CandleIngestionService>();

var observability = ApplicationOptionsReader.ReadObservability(builder.Configuration);
var persistence = ApplicationOptionsReader.ReadPersistence(builder.Configuration);
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(observability.ServiceName))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(metrics =>
    {
        metrics.AddMeter(TradingMetrics.MeterName);
        metrics.AddMeter(CandleIngestionService.MeterName);
        metrics.AddAspNetCoreInstrumentation();
        metrics.AddHttpClientInstrumentation();
        if (observability.ExposePrometheus)
        {
            metrics.AddPrometheusExporter();
        }
    });

var app = builder.Build();

if (persistence.ApplyMigrationsOnStartup)
{
    await CryptoTrader.Host.Persistence.DatabaseMigrator.ApplyAsync(app.Services, app.Lifetime.ApplicationStopping);
}

app.UseExceptionHandler();
app.UseAdminApiKey();
app.MapOpenApi();
if (observability.ExposePrometheus)
{
    app.MapPrometheusScrapingEndpoint(observability.PrometheusPath);
}

app.MapGet("/health/live", () => Results.Ok(new LiveHealthResponse("Healthy")));
app.MapGet("/health/feeds", (MarketDataFeedHealth feeds, IOptions<MarketDataOptions> marketData, TimeProvider time) =>
{
    var staleAfter = TimeSpan.FromSeconds(marketData.Value.StaleAfterSeconds);
    return Results.Ok(feeds.Snapshot(staleAfter, time.GetUtcNow()));
});
app.MapGet("/health/ready", async (DatabaseReadyProbe probe, CancellationToken cancellationToken) =>
{
    var ready = await probe.CheckAsync(cancellationToken);
    var response = new ReadyHealthResponse(ready ? "Healthy" : "Unhealthy", ready);
    return ready
        ? Results.Ok(response)
        : Results.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable);
});
app.MapTradingApi();
app.Run();

public partial class Program;
