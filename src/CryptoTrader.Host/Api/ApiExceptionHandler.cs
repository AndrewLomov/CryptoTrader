using System.Text.Json;
using CryptoTrader.Application.Common;
using CryptoTrader.Contracts.Responses;
using CryptoTrader.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;

namespace CryptoTrader.Host.Api;

internal sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException)
        {
            return false;
        }

        var (status, code, message) = Map(exception);
        if (status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "API request failed.");
        }

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(new ApiErrorResponse(code, message), cancellationToken);
        return true;
    }

    private static (int Status, string Code, string Message) Map(Exception exception) => exception switch
    {
        NotFoundException notFound => (StatusCodes.Status404NotFound, "NotFound", notFound.Message),
        DomainException domain => (StatusCodes.Status400BadRequest, "DomainRule", domain.Message),
        JsonException => (StatusCodes.Status400BadRequest, "InvalidJson", "Request JSON is invalid."),
        InvalidOperationException invalid when invalid.Message.Contains("Unable to resolve service", StringComparison.Ordinal) =>
            (StatusCodes.Status500InternalServerError, "UnexpectedError", "The request failed."),
        InvalidOperationException invalid => (StatusCodes.Status400BadRequest, "InvalidOperation", invalid.Message),
        ArgumentException argument => (StatusCodes.Status400BadRequest, "InvalidArgument", argument.Message),
        _ => (StatusCodes.Status500InternalServerError, "UnexpectedError", "The request failed."),
    };
}
