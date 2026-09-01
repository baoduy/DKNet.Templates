using Microsoft.AspNetCore.Diagnostics;
using Minimal.Infra.Contexts;

namespace Minimal.Api.Configs.GlobalExceptions;

internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    private const string GenericDetail = "An unexpected error occurred. Quote the trace-id when reporting this.";

    #region Methods

    public ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception on {Method} {Path}", httpContext.Request.Method,
            httpContext.Request.Path);

        var isDevelopment = environment.IsDevelopment();

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
                Detail = isDevelopment ? exception.Message : GenericDetail,
                Type = isDevelopment ? exception.GetType().Name : null
            };

        // ProblemDetails.Status only shapes the JSON body — the actual response line still needs it set.
        httpContext.Response.StatusCode = problem.Status!.Value;

        return problemDetailsService.TryWriteAsync(
            new ProblemDetailsContext
                { HttpContext = httpContext, ProblemDetails = problem, Exception = exception });
    }

    #endregion
}