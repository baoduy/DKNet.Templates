using Microsoft.AspNetCore.Diagnostics;

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

        var problem = new ProblemDetails
        {
            Status = (int)HttpStatusCode.InternalServerError,
            Title = "Something went wrong!.",
            Detail = isDevelopment ? exception.Message : GenericDetail,
            Type = isDevelopment ? exception.GetType().Name : null
        };

        return problemDetailsService.TryWriteAsync(
            new ProblemDetailsContext
                { HttpContext = httpContext, ProblemDetails = problem, Exception = exception });
    }

    #endregion
}