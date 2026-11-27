using CryptoTrader.Application.Configuration;
using CryptoTrader.Application.MarketData;
using CryptoTrader.Application.Paper;
using CryptoTrader.Application.Strategies;
using CryptoTrader.Application.Backtesting;
using CryptoTrader.Contracts.Requests;
using CryptoTrader.Contracts.Responses;
using CryptoTrader.Domain.Trading;
using Microsoft.Extensions.Options;

namespace CryptoTrader.Host.Api;

internal static class TradingApiExtensions
{
    public static WebApplication MapTradingApi(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var api = app.MapGroup("/api/v1");

        api.MapGet("/exchanges", (IOptions<ExecutionOptions> execution) =>
            Results.Ok(ContractMapper.Exchanges(execution.Value.Mode)));

        api.MapGet("/instruments", async (
            CandleAdminService candles,
            [AsParameters] GetInstrumentsQuery query,
            CancellationToken cancellationToken) =>
        {
            var items = await candles.ListInstrumentsAsync(query.Exchange, query.Symbol, cancellationToken);
            return Results.Ok(new InstrumentListResponse(items.Select(ContractMapper.ToResponse).ToArray()));
        });

        api.MapGet("/market/candles", async (
            CandleAdminService candles,
            [AsParameters] GetCandlesQuery query,
            CancellationToken cancellationToken) =>
        {
            var items = await candles.GetCandlesAsync(
                query.Exchange,
                query.Instrument,
                query.Timeframe,
                query.FromUtc,
                query.ToUtc,
                cancellationToken);
            var response = items
                .Select(candle => ContractMapper.ToResponse(candle, query.Exchange, query.Instrument, query.Timeframe))
                .ToArray();
            return Results.Ok(new CandleListResponse(response));
        });

        api.MapPost("/market/candles/import", async (
            CandleAdminService candles,
            ImportCandlesRequest request,
            CancellationToken cancellationToken) =>
        {
            var count = await candles.ImportAsync(
                request.Exchange,
                request.Symbol,
                request.Timeframe,
                request.Candles.Select(item => new ImportedCandle(
                    item.OpenTimeUtc,
                    item.CloseTimeUtc,
                    item.Open,
                    item.High,
                    item.Low,
                    item.Close,
                    item.Volume)).ToArray(),
                cancellationToken);
            return Results.Ok(new ImportCandlesResponse(count));
        });

        api.MapPost("/market/candles/sync", async (
            CandleAdminService candles,
            SyncCandlesRequest request,
            CancellationToken cancellationToken) =>
        {
            var count = await candles.SyncAsync(
                request.Exchange,
                request.Symbol,
                request.Timeframe,
                request.FromUtc,
                request.ToUtc,
                cancellationToken);
            return Results.Ok(new SyncCandlesResponse(count));
        });

        api.MapGet("/strategies", (StrategyAdminService strategies) =>
            Results.Ok(new StrategyListResponse(strategies.ListStrategies().Select(ContractMapper.ToResponse).ToArray())));

        api.MapPost("/strategies/{id}/instances", async (
            string id,
            CreateStrategyInstanceRequest request,
            StrategyAdminService strategies,
            CandleAdminService candles,
            CancellationToken cancellationToken) =>
        {
            var instance = await strategies.CreateInstanceAsync(id, request.Instrument, request.ParametersJson, cancellationToken);
            var symbols = await SymbolMapAsync(candles, cancellationToken);
            var response = ContractMapper.ToResponse(instance, symbols);
            return Results.Created($"/api/v1/strategy-instances/{response.Id}", response);
        });

        api.MapPost("/strategy-instances/{id:guid}/start", async (
            Guid id,
            StrategyAdminService strategies,
            CandleAdminService candles,
            CancellationToken cancellationToken) =>
        {
            var instance = await strategies.StartAsync(id, cancellationToken);
            var symbols = await SymbolMapAsync(candles, cancellationToken);
            return Results.Ok(ContractMapper.ToResponse(instance, symbols));
        });

        api.MapPost("/strategy-instances/{id:guid}/stop", async (
            Guid id,
            StrategyAdminService strategies,
            CandleAdminService candles,
            CancellationToken cancellationToken) =>
        {
            var instance = await strategies.StopAsync(id, cancellationToken);
            var symbols = await SymbolMapAsync(candles, cancellationToken);
            return Results.Ok(ContractMapper.ToResponse(instance, symbols));
        });

        api.MapGet("/strategy-instances", async (
            StrategyAdminService strategies,
            CandleAdminService candles,
            CancellationToken cancellationToken) =>
        {
            var instances = await strategies.ListInstancesAsync(cancellationToken);
            var symbols = await SymbolMapAsync(candles, cancellationToken);
            return Results.Ok(new StrategyInstanceListResponse(instances.Select(instance => ContractMapper.ToResponse(instance, symbols)).ToArray()));
        });

        api.MapPost("/backtests", async (
            CreateBacktestRequest request,
            BacktestAdminService backtests,
            CancellationToken cancellationToken) =>
        {
            var risk = request.Risk;
            var report = await backtests.RunAsync(
                request.StrategyId,
                request.StrategyVersion,
                request.ParametersJson,
                request.Exchange,
                request.Instrument,
                request.FromUtc,
                request.ToUtc,
                request.InitialEquity,
                request.FeeRate,
                request.SlippageBps,
                request.ExitOnTrendBreak,
                risk.RiskPerTradePercent,
                risk.MaxDailyLossPercent,
                risk.MaxOpenPositions,
                risk.MaxExposurePercent,
                risk.FeeRate,
                risk.SlippageBps,
                risk.AveragingDownEnabled,
                risk.MaxDrawdownPercent,
                cancellationToken);
            return Results.Created($"/api/v1/backtests/{report.RunId}", new CreateBacktestResponse(report.RunId));
        });

        api.MapGet("/backtests/{id:guid}", async (
            Guid id,
            BacktestAdminService backtests,
            CandleAdminService candles,
            CancellationToken cancellationToken) =>
        {
            var report = await backtests.GetAsync(id, cancellationToken);
            var symbols = ContractMapper.Symbols(await candles.ListInstrumentsAsync(null, null, cancellationToken));
            return Results.Ok(ContractMapper.ToResponse(report, symbols));
        });

        api.MapGet("/orders", async (
            PaperTradingCoordinator paper,
            CandleAdminService candles,
            [AsParameters] GetOrdersQuery query,
            CancellationToken cancellationToken) =>
        {
            var snapshot = await paper.GetSnapshotAsync(query.AccountId, ParseMode(query.TradingMode), cancellationToken);
            var symbols = ContractMapper.Symbols(await candles.ListInstrumentsAsync(null, null, cancellationToken));
            return Results.Ok(new OrderListResponse(snapshot.Orders.Select(order => ContractMapper.ToResponse(order, symbols)).ToArray()));
        });

        api.MapGet("/positions", async (
            PaperTradingCoordinator paper,
            CandleAdminService candles,
            [AsParameters] GetPositionsQuery query,
            CancellationToken cancellationToken) =>
        {
            var snapshot = await paper.GetSnapshotAsync(query.AccountId, null, cancellationToken);
            var symbols = ContractMapper.Symbols(await candles.ListInstrumentsAsync(null, null, cancellationToken));
            return Results.Ok(new PositionListResponse(snapshot.Positions.Select(position => ContractMapper.ToResponse(position, symbols)).ToArray()));
        });

        api.MapPost("/risk/emergency-stop", async (
            HttpRequest http,
            PaperTradingCoordinator paper,
            CancellationToken cancellationToken) =>
        {
            EmergencyStopRequest? request = null;
            if (http.ContentLength is > 0)
            {
                request = await http.ReadFromJsonAsync<EmergencyStopRequest>(cancellationToken);
            }

            var outcome = await paper.EngageEmergencyStopAsync(request?.Reason, cancellationToken);
            return Results.Ok(new EmergencyStopResponse(outcome.EmergencyStop, outcome.OccurredAtUtc));
        });

        return app;
    }

    private static async Task<Dictionary<string, string>> SymbolMapAsync(
        CandleAdminService candles,
        CancellationToken cancellationToken)
    {
        var instruments = await candles.ListInstrumentsAsync(null, null, cancellationToken);
        return ContractMapper.Symbols(instruments);
    }

    private static TradingMode? ParseMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!Enum.TryParse<TradingMode>(value, true, out var mode) || !Enum.IsDefined(mode) || mode == TradingMode.Live)
        {
            throw new InvalidOperationException("Trading mode must be Backtest, Paper, or Testnet.");
        }

        return mode;
    }
}
