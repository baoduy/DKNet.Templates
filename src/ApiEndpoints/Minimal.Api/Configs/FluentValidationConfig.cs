using FluentValidation;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;

namespace Minimal.Api.Configs;

[ExcludeFromCodeCoverage]
internal static class FluentValidationConfig
{
    #region Methods

    public static WebApplicationBuilder AddFluentValidationConfig(this WebApplicationBuilder builder)
    {
        builder.Services.AddFluentValidationAutoValidation();
        builder.Services.AddValidatorsFromAssembly(typeof(AppSetup).Assembly, includeInternalTypes: true);

        return builder;
    }

    #endregion
}