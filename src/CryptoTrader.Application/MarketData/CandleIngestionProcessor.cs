using CryptoTrader.Application.Abstractions.MarketData;
using CryptoTrader.Application.Configuration;
using CryptoTrader.Application.Paper;
using CryptoTrader.Domain.MarketData;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CryptoTrader.Application.MarketData;

public readonly record struct PendingReplayBatch(int Count, bool LeftPending);

/// <summary>
/// Persists a closed candle with its checkpoint and applies or retries paper trading.
/// The hosted service owns subscriptions and the retry timer. This type owns one batch.
/// </summary>
public sealed class CandleIngestionProcessor(
    ICandleIntake intake,
    ICandleConsumerCheckpoint checkpoint,
    PaperTradingCoordinator coordinator,
    TimeProvider timeProvider,
    IOptions<MarketDataOptions> marketDataOptions,
    ILogger<CandleIngestionProcessor> logger)
{
    public const int PendingReplayLimit = 500;

    public async Task PersistAndTradeAsync(
        Candle candle,
        CandleProcessingDecision decision,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candle);
        if (!decision.CanPersistHistory)
        {
            return;
        }

        var pending = await intake.PersistPendingAsync(
            candle,
            ICandleConsumerCheckpoint.PaperConsumer,
            timeProvider.GetUtcNow(),
            cancellationToken);
        if (!pending)
        {
            return;
        }

        if (!decision.CanUpdatePaper)
        {
            await checkpoint.MarkSkippedAsync(
                candle,
                ICandleConsumerCheckpoint.PaperConsumer,
                decision.Reason ?? "Candle is not eligible for paper trading.",
                timeProvider.GetUtcNow(),
                cancellationToken);
            return;
        }

        var result = await coordinator.OnClosedCandleAsync(candle, cancellationToken);
        await CompleteAsync(candle, result, cancellationToken);
    }

    public async Task<PendingReplayBatch> ReplayPendingAsync(CancellationToken cancellationToken)
    {
        var pending = await checkpoint.ListPendingCandlesAsync(
            ICandleConsumerCheckpoint.PaperConsumer,
            PendingReplayLimit,
            cancellationToken);
        var leftPending = false;
        foreach (var candle in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (await ReplayOneAsync(candle, cancellationToken))
                {
                    leftPending = true;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                leftPending = true;
                logger.LogWarning(
                    exception,
                    "Pending candle for {InstrumentId} at {OpenTime} was left pending.",
                    candle.Instrument.Value,
                    candle.OpenTimeUtc);
            }
        }

        return new PendingReplayBatch(pending.Count, leftPending);
    }

    private async Task<bool> ReplayOneAsync(Candle candle, CancellationToken cancellationToken)
    {
        var decision = CandleProcessingDecision.Evaluate(
            candle.IsClosed,
            candle.CloseTimeUtc,
            receivedAtUtc: null,
            timeProvider.GetUtcNow(),
            CandleProcessingDecision.Window(marketDataOptions.Value.StaleAfterSeconds));
        if (!decision.CanUpdatePaper)
        {
            logger.LogInformation(
                "Pending {Exchange} {InstrumentId} candle closed at {CloseTime} is not eligible for paper trading: {Reason}.",
                candle.Exchange.Value,
                candle.Instrument.Value,
                candle.CloseTimeUtc,
                decision.Reason);
            await checkpoint.MarkSkippedAsync(
                candle,
                ICandleConsumerCheckpoint.PaperConsumer,
                decision.Reason ?? "Candle is not eligible for paper trading.",
                timeProvider.GetUtcNow(),
                cancellationToken);
            return false;
        }

        var result = await coordinator.OnClosedCandleAsync(candle, cancellationToken);
        return await CompleteAsync(candle, result, cancellationToken);
    }

    private async Task<bool> CompleteAsync(Candle candle, PaperCandleResult result, CancellationToken cancellationToken)
    {
        if (result.ShouldRetry)
        {
            return true;
        }

        var now = timeProvider.GetUtcNow();
        if (result.WasApplied)
        {
            await checkpoint.MarkProcessedAsync(candle, ICandleConsumerCheckpoint.PaperConsumer, now, cancellationToken);
            return false;
        }

        await checkpoint.MarkSkippedAsync(
            candle,
            ICandleConsumerCheckpoint.PaperConsumer,
            result.Reason ?? "Paper trading skipped the candle.",
            now,
            cancellationToken);
        return false;
    }
}
