namespace Minimal.Api.Configs.GlobalExceptions;

[ExcludeFromCodeCoverage]
internal static class GlobalExceptionConfigs
{
    #region Methods

    /// <summary>
    ///     Configures the services to add global exception handling.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The service collection with the global exception handler configured.</returns>
    public static IServiceCollection AddGlobalException(this IServiceCollection services)
    {
        // Configure Problem Details middleware to customize the response for exceptions
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = ctx =>
            {
                // Set the instance to the request method and path
                ctx.ProblemDetails.Instance = $"{ctx.HttpContext.Request.Method} {ctx.HttpContext.Request.Path}";

                // Add the trace identifier to the problem details extensions
                ctx.ProblemDetails.Extensions.Add("trace-id", ctx.HttpContext.TraceIdentifier);

                // For exception-originated responses (ctx.Exception is set only by an IExceptionHandler),
                // ASP.NET Core's ProblemDetailsDefaults fills a null Type with a generic RFC status-code URI
                // before this callback runs. GlobalExceptionHandler deliberately leaves Type null outside
                // Development (SEC-005) — undo that default here so the response has no type member at all.
                // Scoped to exception responses only so unrelated problem+json responses (e.g. validation
                // failures) keep their usual Type.
                // This guard applies to any exception-originated problem+json response outside Development,
                // not only this template's own handler — that is exact today because the template registers
                // exactly one IExceptionHandler (GlobalExceptionHandler). Whoever adds a second IExceptionHandler
                // that sets a meaningful Type must revisit this guard, or that Type is silently dropped in Production.
                if (ctx.Exception is not null &&
                    !ctx.HttpContext.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment())
                {
                    ctx.ProblemDetails.Type = null;
                }
            };
        });

        // Add the global exception handler middleware
        services.AddExceptionHandler<GlobalExceptionHandler>();

        // Mark the configuration as added on this host
        services.MarkConfigAdded(nameof(GlobalExceptionConfigs));

        // Return the service collection
        return services;
    }

    /// <summary>
    ///     Applies the global exception handling middleware to the application.
    /// </summary>
    /// <param name="app">The web application to configure.</param>
    /// <returns>The web application with the global exception handler applied.</returns>
    public static WebApplication UseGlobalException(this WebApplication app)
    {
        // Check if the global exception configuration has been added on this host
        if (!app.Services.IsConfigAdded(nameof(GlobalExceptionConfigs)))
        {
            return app;
        }

        // Use the exception handler middleware
        app.UseExceptionHandler();

        // Log a message to indicate that the global exception handler is enabled
        Console.WriteLine("Global Exception enabled.");

        // Return the web application
        return app;
    }

    #endregion
}