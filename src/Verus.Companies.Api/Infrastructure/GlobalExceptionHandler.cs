using Microsoft.AspNetCore.Diagnostics;
using Verus.Companies.Application.Validation;

namespace Verus.Companies.Api.Infrastructure;

/// <summary>
/// The single place where exceptions become HTTP responses (RFC 7807 problem details).
/// Validation failures -> 400 with field errors; anything unexpected -> logged, generic 500 (no internals leaked).
/// </summary>
internal sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        switch (exception)
        {
            case RequestValidationException validation:
                logger.LogInformation("Validation failed for {Method} {Path}: {Message}",
                    httpContext.Request.Method, httpContext.Request.Path, validation.Message);
                await Results.ValidationProblem(
                        validation.Errors,
                        detail: validation.Message,
                        title: "Validation failed",
                        statusCode: StatusCodes.Status400BadRequest)
                    .ExecuteAsync(httpContext);
                return true;

            case OperationCanceledException when httpContext.RequestAborted.IsCancellationRequested:
                logger.LogDebug("Request {Method} {Path} was cancelled by the client", httpContext.Request.Method, httpContext.Request.Path);
                httpContext.Response.StatusCode = 499;
                return true;

            default:
                logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
                await Results.Problem(
                        title: "An unexpected error occurred.",
                        statusCode: StatusCodes.Status500InternalServerError)
                    .ExecuteAsync(httpContext);
                return true;
        }
    }
}
