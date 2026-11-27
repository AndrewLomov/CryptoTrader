using Microsoft.Extensions.Options;

namespace CryptoTrader.Exchanges.Bybit.Rest;

/// <summary>
/// Spaces public REST starts so the client never exceeds <see cref="BybitOptions.MaxRequestsPerSecond"/>.
/// </summary>
internal sealed class BybitRateGate
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly TimeSpan _interval;
    private readonly TimeProvider _time;
    private DateTimeOffset _nextRequestAt = DateTimeOffset.MinValue;

    public BybitRateGate(IOptions<BybitOptions> options, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        var perSecond = options.Value.MaxRequestsPerSecond;
        if (perSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Bybit:MaxRequestsPerSecond must be positive.");
        }

        var intervalMs = (int)decimal.Ceiling(1000m / perSecond);
        if (intervalMs < 1)
        {
            intervalMs = 1;
        }

        _interval = TimeSpan.FromMilliseconds(intervalMs);
        _time = timeProvider;
    }

    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            while (true)
            {
                var now = _time.GetUtcNow();
                var wait = _nextRequestAt - now;
                if (wait <= TimeSpan.Zero)
                {
                    var scheduled = now + _interval;
                    _nextRequestAt = scheduled < _nextRequestAt ? _nextRequestAt + _interval : scheduled;
                    return;
                }

                await Task.Delay(wait, _time, cancellationToken);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
