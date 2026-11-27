using Microsoft.Extensions.Options;

namespace CryptoTrader.Exchanges.Okx.Rest;

/// <summary>
/// Sliding window for public REST. The default is 10 requests per 2 seconds.
/// </summary>
internal sealed class OkxRateLimiter
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Queue<DateTimeOffset> _hits = new();
    private readonly int _limit;
    private readonly TimeSpan _window;
    private readonly TimeProvider _time;

    public OkxRateLimiter(IOptions<OkxOptions> options, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        var limit = options.Value.MaxRequestsPerWindow;
        var windowSeconds = options.Value.RateLimitWindowSeconds;
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Okx:MaxRequestsPerWindow must be positive.");
        }

        if (windowSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Okx:RateLimitWindowSeconds must be positive.");
        }

        _limit = limit;
        _window = TimeSpan.FromSeconds(windowSeconds);
        _time = timeProvider;
    }

    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            while (true)
            {
                var now = _time.GetUtcNow();
                while (_hits.Count > 0 && now - _hits.Peek() >= _window)
                {
                    _hits.Dequeue();
                }

                if (_hits.Count < _limit)
                {
                    _hits.Enqueue(now);
                    return;
                }

                var wait = _window - (now - _hits.Peek());
                if (wait < TimeSpan.Zero)
                {
                    wait = TimeSpan.Zero;
                }

                await Task.Delay(wait, _time, cancellationToken);
            }
        }
        finally
        {
            _gate.Release();
        }
    }
}
