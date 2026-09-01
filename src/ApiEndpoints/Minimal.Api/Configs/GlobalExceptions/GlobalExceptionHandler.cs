using Microsoft.AspNetCore.Diagnostics;
using Minimal.Infra.Contexts;

namespace Minimal.Api.Configs.GlobalExceptions;

internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    #region Methods

    public ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, exception.Message);

        if (exception.InnerException is not null)
        {
            exception = exception.InnerException;
        }

        // A refused write is a controlled 403, not an internal error — never leak EF Core detail for it.
        var problem = exception is OwnershipRequiredException
            ? new ProblemDetails
            {
                Status = (int)HttpStatusCode.Forbidden,
                Title = "Request refused.",
                Detail = exception.Message,
                Type = exception.GetType().Name
            }
            : new ProblemDetails
            {
                Status = (int)HttpStatusCode.InternalServerError,
                Title = "Something went wrong!.",
                Detail = exception.Message,
                Type = exception.GetType().Name
            };

        // ProblemDetails.Status only shapes the JSON body — the actual response line still needs it set.
        httpContext.Response.StatusCode = problem.Status!.Value;

        return problemDetailsService.TryWriteAsync(
            new ProblemDetailsContext
                { HttpContext = httpContext, ProblemDetails = problem, Exception = exception });
    }

    #endregion
}