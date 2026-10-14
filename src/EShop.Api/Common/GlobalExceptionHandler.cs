using EShop.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace EShop.Api.Common;

public sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            // Malformed request (bad query value, invalid JSON, body too large...): the framework gives the status code.
            BadHttpRequestException bad => (bad.StatusCode, "Invalid request"),
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            DomainException => (StatusCodes.Status422UnprocessableEntity, "Business rule violation"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };

        if (status >= StatusCodes.Status500InternalServerError)
        {
            LogUnhandledException(logger, exception);
        }
        else
        {
            LogRequestFailed(logger, exception.Message);
        }

        httpContext.Response.StatusCode = status;

        // Never expose internal error messages for 500s, and use a fixed text for malformed requests.
        var detail = exception switch
        {
            _ when status >= StatusCodes.Status500InternalServerError => null,
            BadHttpRequestException => "The request could not be read. Check the format of the parameters or the body.",
            _ => exception.Message
        };

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail
            }
        });
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception")]
    private static partial void LogUnhandledException(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Request failed: {ErrorMessage}")]
    private static partial void LogRequestFailed(ILogger logger, string errorMessage);
}