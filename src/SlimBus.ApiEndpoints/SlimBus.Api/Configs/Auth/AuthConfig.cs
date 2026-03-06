using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace SlimBus.Api.Configs.Auth;

/// <summary>
///     Provides extension methods for configuring authentication and authorization in an ASP.NET Core application.
/// </summary>
[ExcludeFromCodeCoverage]
internal static class AuthConfig
{
    #region Properties

    /// <summary>
    ///     Indicates whether the authentication configuration has been added to the services.
    /// </summary>
    public static bool IsAuthConfigAdded { get; private set; }

    #endregion

    #region Methods

    /// <summary>
    ///     Adds authentication and authorization services to the specified <see cref="IServiceCollection" />.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add the authentication and authorization services to.</param>
    /// <returns>The updated <see cref="IServiceCollection" /> instance.</returns>
    /// <remarks>
    ///     This method configures the application to use JWT (JSON Web Token) Bearer authentication.
    ///     If the <see cref="ValidateMsGraphJwtToken" /> flag is set to true, it replaces the default token handler with a
    ///     custom <see cref="MsGraphJwtTokenHandler" />.
    /// </remarks>
    public static IServiceCollection AddAuthConfig(this IServiceCollection services)
    {
        IsAuthConfigAdded = true;

        services.AddAuthentication()
            .AddJwtBearer(c =>
            {
                if (ValidateMsGraphJwtToken)
                {
                    c.TokenHandlers.Clear();
                    c.TokenHandlers.Add(new MsGraphJwtTokenHandler());
                }
            });

        services.AddAuthorization(options =>
        {
            // TODO: Replace "sample-scope" with the actual scope value from your identity provider,
            //       then apply the policy to an endpoint with .RequireAuthorization(HasScopeRequirement.PolicyName).
            options.AddPolicy(
                HasScopeRequirement.PolicyName,
                policy => policy.Requirements.Add(new HasScopeRequirement("sample-scope")));
        });

        // Sample IClaimsTransformation: enriches the user principal after authentication.
        // TODO: Replace SampleClaimsTransformation with your real implementation or remove if not needed.
        services.AddScoped<IClaimsTransformation, SampleClaimsTransformation>();

        // Sample IAuthorizationHandler: evaluates HasScopeRequirement.
        // TODO: Replace HasScopeHandler with your real handler(s) or remove if not needed.
        services.AddScoped<IAuthorizationHandler, HasScopeHandler>();

        return services;
    }

    /// <summary>
    ///     Configures the specified <see cref="WebApplication" /> to use the added authentication and authorization services.
    /// </summary>
    /// <param name="app">The <see cref="WebApplication" /> to configure.</param>
    /// <returns>The updated <see cref="WebApplication" /> instance.</returns>
    /// <remarks>
    ///     This method enables authentication and authorization middleware only if the authentication configuration has been
    ///     added to the services.
    /// </remarks>
    public static WebApplication UseAuthConfig(this WebApplication app)
    {
        if (IsAuthConfigAdded)
        {
            app.UseAuthentication();
            app.UseAuthorization();
            Console.WriteLine("Authentication enabled.");
        }

        return app;
    }

    #endregion

    // NOTE: Switch this one off if we are not using MS Graph token
    private const bool ValidateMsGraphJwtToken = true;
}