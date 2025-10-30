namespace SlimBus.Api.Configs;

[ExcludeFromCodeCoverage]
internal static class ServiceConfigs
{
    #region Methods

    public static IServiceCollection AddAllAppServices(
        this IServiceCollection services,
        IConfiguration configuration,
        FeatureOptions features)
    {
        services
            .AddSingleton<IHttpContextAccessor, HttpContextAccessor>()
            .AddScoped<IPrincipalProvider, PrincipalProvider>()
            .AddScoped<IDataOwnerProvider>(p => p.GetRequiredService<IPrincipalProvider>());

        services
            .AddAppServices()
            .AddInfraServices()

            //Service Bus
            .AddServiceBus(configuration, typeof(AppSetup).Assembly);

        return services;
    }

    public static IServiceCollection AddOptions(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure core options for the application
        services.Configure<FeatureOptions>(configuration.GetSection(FeatureOptions.Name));

        services.ConfigureHttpJsonOptions(op =>
        {
            op.SerializerOptions.PropertyNamingPolicy = SharedConsts.JsonSerializerOptions.PropertyNamingPolicy;
            op.SerializerOptions.DefaultIgnoreCondition = SharedConsts.JsonSerializerOptions.DefaultIgnoreCondition;
            op.SerializerOptions.WriteIndented = SharedConsts.JsonSerializerOptions.WriteIndented;
            op.SerializerOptions.PropertyNameCaseInsensitive =
                SharedConsts.JsonSerializerOptions.PropertyNameCaseInsensitive;
            op.SerializerOptions.DictionaryKeyPolicy = SharedConsts.JsonSerializerOptions.DictionaryKeyPolicy;

            op.SerializerOptions.Converters.Clear();
            foreach (var converter in SharedConsts.JsonSerializerOptions.Converters)
            {
                op.SerializerOptions.Converters.Add(converter);
            }
        });

        return services;
    }

    #endregion
}