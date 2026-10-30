using FileForge.Application.Errors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace FileForge.Api.Middleware;

/// <summary>
/// Turns an AppException into a ProblemDetails response with our error code.
/// Any other exception is left to the default handler, which returns a generic 500 without details.
/// </summary>
internal sealed class AppExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<AppExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not AppException appException)
        {
            return false;
        }

        logger.LogInformation("Request rejected with {ErrorCode}", appException.Code);

        var status = GetStatusCode(appException.Code);
        httpContext.Response.StatusCode = status;

        var problem = new ProblemDetails
        {
            Status = status,
            Detail = appException.Message,
        };
        problem.Extensions["code"] = appException.Code;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
        });
    }

    private static int GetStatusCode(string code) => code switch
    {
        ErrorCodes.FileTooLarge => StatusCodes.Status413PayloadTooLarge,
        ErrorCodes.UnsupportedFormat => StatusCodes.Status415UnsupportedMediaType,
        ErrorCodes.InvalidFileContent or ErrorCodes.ConversionNotSupported => StatusCodes.Status422UnprocessableEntity,
        ErrorCodes.AccessDenied => StatusCodes.Status403Forbidden,
        ErrorCodes.JobExpired => StatusCodes.Status410Gone,
        ErrorCodes.StorageLimitExceeded => StatusCodes.Status503ServiceUnavailable,
        ErrorCodes.JobNotCompleted => StatusCodes.Status409Conflict,
        ErrorCodes.JobInProgress => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status400BadRequest,
    };
}
