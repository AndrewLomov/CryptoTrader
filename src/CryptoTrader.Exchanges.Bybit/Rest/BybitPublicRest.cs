using System.Net;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace CryptoTrader.Exchanges.Bybit.Rest;

internal sealed class BybitPublicRest
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly BybitRateGate _rateGate;
    private readonly ILogger<BybitPublicRest> _logger;

    public BybitPublicRest(
        IHttpClientFactory httpClientFactory,
        BybitRateGate rateGate,
        ILogger<BybitPublicRest> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(rateGate);
        ArgumentNullException.ThrowIfNull(logger);
        _httpClientFactory = httpClientFactory;
        _rateGate = rateGate;
        _logger = logger;
    }

    public async Task<JsonDocument> GetAsync(string pathAndQuery, CancellationToken cancellationToken)
    {
        await _rateGate.WaitAsync(cancellationToken);
        var client = _httpClientFactory.CreateClient(BybitOptions.HttpClientName);
        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync(pathAndQuery, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning("Bybit public request failed ({ExceptionType}).", exception.GetType().Name);
            throw new InvalidOperationException("Bybit public request failed.", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Bybit public request timed out.");
            throw new InvalidOperationException("Bybit public request timed out.", exception);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("Bybit public request was rejected with HTTP 429.");
                throw new InvalidOperationException(BybitResponses.ReadMessage(body) ?? "Bybit rate limit (HTTP 429).");
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "null" : body);
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException("Bybit response was not valid JSON.", exception);
            }

            try
            {
                _ = BybitResponses.RequireResult(document, _logger);
            }
            catch
            {
                document.Dispose();
                throw;
            }

            if (!response.IsSuccessStatusCode)
            {
                document.Dispose();
                _logger.LogWarning("Bybit public request failed with HTTP {StatusCode}.", (int)response.StatusCode);
                throw new InvalidOperationException($"Bybit HTTP {(int)response.StatusCode}.");
            }

            return document;
        }
    }
}
