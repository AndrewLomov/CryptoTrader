using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace CryptoTrader.Exchanges.Okx.Rest;

internal sealed class OkxPublicRest
{
    private const int MaxAttempts = 4;

    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly HttpClient? _httpClient;
    private readonly OkxRateLimiter _rateLimiter;
    private readonly TimeProvider _time;
    private readonly ILogger<OkxPublicRest> _logger;

    public OkxPublicRest(
        IHttpClientFactory httpClientFactory,
        OkxRateLimiter rateLimiter,
        TimeProvider time,
        ILogger<OkxPublicRest> logger)
        : this(httpClientFactory, httpClient: null, rateLimiter, time, logger)
    {
    }

    internal OkxPublicRest(
        HttpClient httpClient,
        OkxRateLimiter rateLimiter,
        TimeProvider time,
        ILogger<OkxPublicRest> logger)
        : this(httpClientFactory: null, httpClient, rateLimiter, time, logger)
    {
    }

    private OkxPublicRest(
        IHttpClientFactory? httpClientFactory,
        HttpClient? httpClient,
        OkxRateLimiter rateLimiter,
        TimeProvider time,
        ILogger<OkxPublicRest> logger)
    {
        if (httpClientFactory is null && httpClient is null)
        {
            throw new ArgumentException("An HTTP client factory or client is required.");
        }

        ArgumentNullException.ThrowIfNull(rateLimiter);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);
        _httpClientFactory = httpClientFactory;
        _httpClient = httpClient;
        _rateLimiter = rateLimiter;
        _time = time;
        _logger = logger;
    }

    public async Task<JsonDocument> GetAsync(string pathAndQuery, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await SendOnceAsync(pathAndQuery, cancellationToken);
            }
            catch (OkxRetryableException retry) when (attempt < MaxAttempts)
            {
                var delay = retry.RetryAfter ?? Backoff(attempt);
                _logger.LogWarning("OKX public GET will be retried ({Reason}).", retry.Reason);
                await Task.Delay(delay, _time, cancellationToken);
            }
            catch (OkxRetryableException retry)
            {
                throw new OkxApiException($"OKX public GET failed after retries ({retry.Reason}).");
            }
        }
    }

    private async Task<JsonDocument> SendOnceAsync(string pathAndQuery, CancellationToken cancellationToken)
    {
        await _rateLimiter.WaitAsync(cancellationToken);
        var client = _httpClient ?? _httpClientFactory!.CreateClient(OkxOptions.HttpClientName);
        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync(pathAndQuery, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning("OKX public request failed ({ExceptionType}).", exception.GetType().Name);
            throw new OkxRetryableException("network");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("OKX public request timed out.");
            throw new OkxRetryableException("timeout");
        }

        using (response)
        {
            var retryAfter = ReadRetryAfter(response);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (IsRetryStatus(response.StatusCode))
            {
                _logger.LogWarning("OKX public request returned HTTP {StatusCode}.", (int)response.StatusCode);
                throw new OkxRetryableException("http-" + ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture), retryAfter);
            }

            JsonDocument? document = null;
            try
            {
                try
                {
                    document = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "null" : body);
                }
                catch (JsonException exception)
                {
                    throw new OkxApiException("OKX response was not valid JSON.", exception);
                }

                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    throw new OkxApiException("OKX response was not a JSON object.", httpStatus: (int)response.StatusCode);
                }

                var code = OkxResponses.ReadCode(document.RootElement);
                if (code is null)
                {
                    throw new OkxApiException("OKX response did not include code.", httpStatus: (int)response.StatusCode);
                }

                if (!string.Equals(code, OkxResponses.SuccessCode, StringComparison.Ordinal))
                {
                    if (string.Equals(code, OkxResponses.RateLimitCode, StringComparison.Ordinal))
                    {
                        throw new OkxRetryableException("code-50011", retryAfter);
                    }

                    var message = OkxResponses.ReadMessage(document.RootElement);
                    throw new OkxApiException(
                        message ?? $"OKX API code {code}.",
                        code,
                        (int)response.StatusCode);
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new OkxApiException(
                        $"OKX HTTP {(int)response.StatusCode}.",
                        code,
                        (int)response.StatusCode);
                }

                if (!document.RootElement.TryGetProperty("data", out _))
                {
                    throw new OkxApiException("OKX response did not include data.", code, (int)response.StatusCode);
                }

                var success = document;
                document = null;
                return success;
            }
            catch (OkxRetryableException)
            {
                document?.Dispose();
                throw;
            }
            catch
            {
                document?.Dispose();
                throw;
            }
        }
    }

    private TimeSpan? ReadRetryAfter(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Retry-After", out var values))
        {
            return null;
        }

        var text = values.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
        {
            if (seconds < 0)
            {
                seconds = 0;
            }

            if (seconds > 30)
            {
                seconds = 30;
            }

            return TimeSpan.FromSeconds(seconds);
        }

        if (DateTimeOffset.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var when))
        {
            var delay = when - _time.GetUtcNow();
            if (delay < TimeSpan.Zero)
            {
                return TimeSpan.Zero;
            }

            return delay > TimeSpan.FromSeconds(30) ? TimeSpan.FromSeconds(30) : delay;
        }

        return null;
    }

    private static bool IsRetryStatus(HttpStatusCode status) =>
        status is HttpStatusCode.TooManyRequests
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;

    private static TimeSpan Backoff(int attempt)
    {
        var shift = Math.Min(Math.Max(attempt - 1, 0), 5);
        var millis = 200 * (1 << shift);
        if (millis > 5000)
        {
            millis = 5000;
        }

        return TimeSpan.FromMilliseconds(millis + Random.Shared.Next(0, 250));
    }

    private sealed class OkxRetryableException : Exception
    {
        public OkxRetryableException(string reason, TimeSpan? retryAfter = null)
        {
            Reason = reason;
            RetryAfter = retryAfter;
        }

        public string Reason { get; }

        public TimeSpan? RetryAfter { get; }
    }
}
