namespace Minimal.Api.Configs.AzureAppConfig;

/// <summary>
///     Configuration options for Azure App Configuration integration
/// </summary>
internal sealed class AzureAppConfigOptions
{
    #region Fields

    private const int DefaultCacheExpirationInSeconds = 300;

    #endregion

    #region Properties

    /// <summary>
    ///     Whether to load feature flags from Azure App Configuration
    /// </summary>
    public bool LoadFeatureFlags { get; set; } = true;

    /// <summary>
    ///     Connection string for Azure App Configuration
    /// </summary>
    public string ConnectionStringName { get; set; } = "AzureAppConfig";

    /// <summary>
    ///     Feature flag prefix to filter feature flags (optional)
    /// </summary>
    public string FeatureFlagPrefix { get; set; } = string.Empty;

    /// <summary>
    ///     Configuration section name
    /// </summary>
    public static string Name => "AzureAppConfiguration";

    /// <summary>
    ///     Label to filter configuration values (optional)
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    ///     Refresh interval for configuration values, in seconds
    /// </summary>
    public int CacheExpirationInSeconds { get; set; } = DefaultCacheExpirationInSeconds;

    /// <summary>
    ///     The label the setup selects keys with: <see cref="Label" />, or the service name when it is
    ///     null, empty or whitespace
    /// </summary>
    public string ResolvedLabel => string.IsNullOrWhiteSpace(Label) ? SharedConsts.ApiName : Label;

    /// <summary>
    ///     The refresh interval the setup registers: <see cref="CacheExpirationInSeconds" />, or 300 seconds when
    ///     it is zero or negative (the SDK rejects an interval under one second)
    /// </summary>
    public TimeSpan RefreshInterval => TimeSpan.FromSeconds(
        CacheExpirationInSeconds > 0 ? CacheExpirationInSeconds : DefaultCacheExpirationInSeconds);

    /// <summary>
    ///     The feature flag name filter the setup selects with: flags starting with <see cref="FeatureFlagPrefix" />,
    ///     its reserved characters (<c>\</c>, <c>*</c>, <c>,</c>) escaped; <see langword="null" /> selects all flags
    /// </summary>
    public string? FeatureFlagFilter => string.IsNullOrWhiteSpace(FeatureFlagPrefix)
        ? null
        : FeatureFlagPrefix
              .Replace(@"\", @"\\", StringComparison.Ordinal)
              .Replace("*", @"\*", StringComparison.Ordinal)
              .Replace(",", @"\,", StringComparison.Ordinal) + "*";

    #endregion
}