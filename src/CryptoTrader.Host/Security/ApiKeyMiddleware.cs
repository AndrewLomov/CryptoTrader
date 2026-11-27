using System.Security.Cryptography;
using System.Text;
using CryptoTrader.Application.Configuration;
using CryptoTrader.Contracts.Responses;
using Microsoft.Extensions.Options;

namespace CryptoTrader.Host.Security;

internal static class ApiKeyMiddleware
{
    public static WebApplication UseAdminApiKey(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.Use(async (context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments("/api"))
            {
                await next(context);
                return;
            }

            var expected = context.RequestServices.GetRequiredService<IOptions<ApiOptions>>().Value.AdminApiKey;
            if (!context.Request.Headers.TryGetValue("X-Api-Key", out var provided)
                || provided.Count != 1
                || !ApiKeyComparer.Matches(provided.ToString(), expected))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(
                    new ApiErrorResponse("Unauthorized", "A valid X-Api-Key header is required."),
                    context.RequestAborted);
                return;
            }

            await next(context);
        });
        return app;
    }
}

internal static class ApiKeyComparer
{
    public static bool Matches(string? provided, string? expected)
    {
        var providedText = provided ?? string.Empty;
        var expectedText = expected ?? string.Empty;
        var providedBytes = Encoding.UTF8.GetBytes(providedText);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedText);
        if (providedBytes.Length > 256 || expectedBytes.Length > 256)
        {
            var providedHash = SHA256.HashData(providedBytes);
            var expectedHash = SHA256.HashData(expectedBytes);
            return CryptographicOperations.FixedTimeEquals(providedHash, expectedHash);
        }

        Span<byte> providedBuffer = stackalloc byte[256];
        Span<byte> expectedBuffer = stackalloc byte[256];
        providedBytes.CopyTo(providedBuffer);
        expectedBytes.CopyTo(expectedBuffer);
        var sameLength = providedBytes.Length == expectedBytes.Length;
        return CryptographicOperations.FixedTimeEquals(providedBuffer, expectedBuffer) & sameLength;
    }
}
