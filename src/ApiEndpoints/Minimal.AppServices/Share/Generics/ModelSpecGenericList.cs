using System.Collections.Concurrent;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using DKNet.EfCore.Specifications;
using LinqKit;
using Minimal.Domains.Share;
using Minimal.Share;

namespace Minimal.AppServices.Share.Generics;

/// <summary>
/// Supported operators for dynamic field filters.
/// </summary>
public enum FilterOperator
{
    /// <summary>
    /// Matches rows where the field value equals the supplied value.
    /// </summary>
    Equals,
    /// <summary>
    /// Matches rows where the field value does not equal the supplied value.
    /// </summary>
    NotEquals,
    /// <summary>
    /// Matches rows where the field value equals any value in a comma-separated list.
    /// </summary>
    Any
}

/// <summary>
/// Represents one dynamic filter entry for generic list queries.
/// </summary>
/// <param name="FieldName">The target field name or nested path (for example, <c>GeneralInfo.CompanyType</c>).</param>
/// <param name="Operator">The comparison operator to apply.</param>
/// <param name="Value">The comparison value. For <see cref="FilterOperator.Any"/>, this can be a comma-separated list.</param>
public sealed record FilterInfo(string FieldName, FilterOperator Operator, string? Value);

/// <summary>
/// Request parameters for generic list endpoints, including paging, ordering, search, and filters.
/// </summary>
public record GenericListParameters : PageableQuery
{
    [JsonIgnore]
    internal ICollection<FilterInfo> Filters => string.IsNullOrEmpty(FiltersJson)
        ? []
        : JsonSerializer.Deserialize<ICollection<FilterInfo>>(FiltersJson, SharedConsts.JsonSerializerOptions) ?? [];

    /// <summary>
    ///     The additional filters as a JSON-serialized array of
    ///     ?filtersJson=[{"FieldName":"Name","Operator":"Equals","Value":"foo"}] <see cref="FilterInfo" /> objects.
    /// </summary>
    public string? FiltersJson { get; init; }

    /// <summary>
    ///     Start date for filtering (defaults to 30 days ago if not provided)
    /// </summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>
    ///     Optional explicit list of fields to search (must be string properties on the entity)
    ///     ?SearchFields=
    /// </summary>
    public string[] SearchFields { get; init; } = [];

    /// <summary>
    ///     End date for filtering (defaults to now if not provided)
    /// </summary>
    public DateTimeOffset? To { get; init; }

    /// <summary>
    ///     Gets the effective From date (30 days ago if not specified)
    /// </summary>
    [JsonIgnore]
    public DateTimeOffset FromValue => From ?? DateTimeOffset.Now.AddDays(-30);

    /// <summary>
    ///     Gets the effective To date (now if not specified)
    /// </summary>
    [JsonIgnore]
    public DateTimeOffset ToValue => To ?? DateTimeOffset.Now;
}

/// <summary>
/// FluentValidation validator for <see cref="GenericListParameters"/>.
/// </summary>
internal sealed class GenericListParametersValidator : AbstractValidator<GenericListParameters>
{
    #region Constructors

    /// <summary>
    /// Initializes validation rules for generic list parameters.
    /// </summary>
    public GenericListParametersValidator()
    {
        // Apply all GenericListParameters validation rules using shared extensions
        this.AddGenericListParametersValidation();
    }

    #endregion
}

/// <summary>
/// Builds a specification for generic list queries with date range, search text, filters, and ordering.
/// </summary>
/// <typeparam name="TEntity">The entity type being queried.</typeparam>
/// <typeparam name="TModel">The model type projected by the specification.</typeparam>
public class ModelSpecGenericList<TEntity, TModel> : ModelSpecification<TEntity, TModel>
    where TEntity : DomainEntity where TModel : class
{
    #region Constructors

    /// <summary>
    /// Initializes a new list specification from the supplied query parameters.
    /// </summary>
    /// <param name="parameters">Paging, search, filter, and ordering parameters.</param>
    public ModelSpecGenericList(GenericListParameters parameters)
    {
        var predicate = CreatePredicate();

        var datePredicate = CreatePredicate(x =>
            (x.CreatedOn >= parameters.FromValue && x.CreatedOn <= parameters.ToValue) ||
            (x.UpdatedOn >= parameters.FromValue && x.UpdatedOn <= parameters.ToValue));

        predicate = predicate.And(datePredicate);

        if (!string.IsNullOrWhiteSpace(parameters.SearchText))
            predicate = predicate.And(CreateSearchExpression(parameters));

        if (parameters.Filters is { Count: > 0 })
            predicate = predicate.And(CreateFilterPredicate(parameters));

        WithFilter(predicate);
        if (string.IsNullOrWhiteSpace(parameters.OrderBy))
            AddOrderByDescending(x => x.Id);
        else
        {
            // Handle nested property paths for OrderBy
            var orderByField = parameters.OrderBy;
            var direction = parameters.IsDescending == true ? ListSortDirection.Descending : ListSortDirection.Ascending;
            
            // Check if it's a nested property (contains dot)
            if (orderByField.Contains('.', StringComparison.Ordinal))
            {
                // Build dynamic LINQ expression for nested property
                var orderByExpr = DynamicExpressionParser.ParseLambda<TEntity, object>(
                    ParsingConfig.Default, false, orderByField);
                
                if (direction == ListSortDirection.Descending)
                    AddOrderByDescending(orderByExpr);
                else
                    AddOrderBy(orderByExpr);
            }
            else
            {
                // Simple property - use string-based method
                AddOrderBy(orderByField, direction);
            }
        }
    }

    #endregion

    #region Methods

    /// <summary>
    /// Creates a predicate from the parsed <see cref="GenericListParameters.FiltersJson"/> entries.
    /// </summary>
    /// <param name="parameters">The request parameters containing filter entries.</param>
    /// <returns>A combined filter predicate for the configured filter collection.</returns>
    protected Expression<Func<TEntity, bool>> CreateFilterPredicate(GenericListParameters parameters)
    {
        var predicate = CreatePredicate(_ => true);
        foreach (var filter in parameters.Filters)
        {
            if (string.IsNullOrWhiteSpace(filter.FieldName) || string.IsNullOrWhiteSpace(filter.Value)) continue;
            if (!EntityPropertyCache.TryGetNested<TEntity>(filter.FieldName, out var prop, out var propPath)) continue;

            var raw = filter.Value.Trim();
            var underlying = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

            if (filter.Operator == FilterOperator.Any)
            {
                var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length == 0) continue;
                var anyPredicate = CreatePredicate(_ => false);
                foreach (var part in parts)
                    if (underlying == typeof(string))
                        anyPredicate = anyPredicate.DynamicOr(
                            $"{propPath} != null && {propPath}.ToLower() == @0",
                            [part.ToLowerInvariant()]);
                    else
                        anyPredicate = anyPredicate.DynamicOr(
                            $"{propPath} == @0",
                            [part]);
                predicate = predicate.And(anyPredicate);
                continue;
            }

            if (underlying == typeof(string))
            {
                var valLower = raw.ToLowerInvariant();
                predicate = filter.Operator switch
                {
                    FilterOperator.Equals => predicate.DynamicAnd(
                        $"{propPath} != null && {propPath}.ToLower() == @0",
                        [valLower]),
                    FilterOperator.NotEquals => predicate.DynamicAnd(
                        $"{propPath} == null || {propPath}.ToLower() != @0",
                        [valLower]),
                    _ => predicate
                };
            }
            else
            {
                predicate = filter.Operator switch
                {
                    FilterOperator.Equals => predicate.DynamicAnd(
                        $"{propPath} == @0",
                        [raw]),
                    FilterOperator.NotEquals => predicate.DynamicAnd(
                        $"{propPath} != @0",
                        [raw]),
                    _ => predicate
                };
            }
        }

        return predicate;
    }

    /// <summary>
    /// Creates a text-search predicate using explicit search fields or inferred string properties.
    /// </summary>
    /// <param name="parameters">The request parameters containing search text and optional fields.</param>
    /// <returns>A search predicate when search text is present; otherwise <see langword="null"/>.</returns>
    protected Expression<Func<TEntity, bool>>? CreateSearchExpression(GenericListParameters parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters.SearchText))
            return null;

        var searchText = parameters.SearchText.Trim();
        var lowerSearch = searchText.ToLowerInvariant();
        var searchPredicate = CreatePredicate();

        if (parameters.SearchFields is { Length: > 0 })
        {
            // Explicit search fields provided - resolve each field (supporting nested paths)
            foreach (var fieldName in parameters.SearchFields)
            {
                if (!EntityPropertyCache.TryGetNested<TEntity>(fieldName, out var prop, out var propPath))
                    continue;

                // Only search string properties
                var underlying = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                if (underlying != typeof(string))
                    continue;

                searchPredicate = searchPredicate.DynamicOr(
                    $"{propPath} != null && {propPath}.ToLower().Contains(@0)",
                    [lowerSearch]);
            }
        }
        else
        {
            // No explicit fields - search all top-level string properties
            var candidateProps = ResolveSearchableProperties();
            foreach (var propInfo in candidateProps)
                searchPredicate = searchPredicate.DynamicOr(
                    $"{propInfo.Name} != null && {propInfo.Name}.ToLower().Contains(@0)",
                    [lowerSearch]);
        }

        return searchPredicate;
    }

    private static PropertyInfo[] ResolveSearchableProperties()
    {
        return EntityPropertyCache.GetSearchableStringProperties<TEntity>();
    }

    #endregion
}

/// <summary>
/// Caches reflected entity properties and lookup metadata used by dynamic filtering.
/// </summary>
internal static class EntityPropertyCache
{
    #region Fields

    private static readonly ConcurrentDictionary<Type, Entry> Cache = new();

    #endregion

    #region Methods

    /// <summary>
    /// Gets all public instance properties for the specified entity type.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <returns>All discovered public instance properties.</returns>
    public static PropertyInfo[] GetAll<TEntity>()
    {
        return GetEntry<TEntity>().All;
    }

    /// <summary>
    /// Gets string properties that are eligible for default search.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <returns>Searchable string properties for the entity.</returns>
    public static PropertyInfo[] GetSearchableStringProperties<TEntity>()
    {
        return GetEntry<TEntity>().Searchable;
    }

    /// <summary>
    /// Attempts to resolve a top-level property by name using case-insensitive matching.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="name">The property name to resolve.</param>
    /// <param name="prop">The resolved property info when found.</param>
    /// <returns><see langword="true"/> when the property is found; otherwise <see langword="false"/>.</returns>
    public static bool TryGet<TEntity>(string name, out PropertyInfo prop)
    {
        return GetEntry<TEntity>().Lookup.TryGetValue(name, out prop!);
    }

    /// <summary>
    ///     Attempts to resolve a nested property path (e.g., "GeneralInfo.CompanyType").
    ///     Returns the full property path and the final property info.
    /// </summary>
    /// <typeparam name="TEntity">The root entity type.</typeparam>
    /// <param name="fieldName">The field name or nested path to resolve.</param>
    /// <param name="finalProp">The final property in the resolved path.</param>
    /// <param name="propertyPath">The normalized property path with reflected casing.</param>
    /// <returns><see langword="true"/> when the path is resolved; otherwise <see langword="false"/>.</returns>
    public static bool TryGetNested<TEntity>(string fieldName, out PropertyInfo finalProp, out string propertyPath)
    {
        finalProp = null!;
        propertyPath = string.Empty;

        // Check for simple (non-nested) property first
        if (!fieldName.Contains('.', StringComparison.Ordinal))
        {
            if (TryGet<TEntity>(fieldName, out finalProp))
            {
                propertyPath = finalProp.Name;
                return true;
            }

            return false;
        }

        // Handle nested property path
        var parts = fieldName.Split('.');
        var currentType = typeof(TEntity);
        var pathParts = new List<string>();

        foreach (var part in parts)
        {
            var prop = currentType.GetProperty(part,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop is null)
                return false;

            pathParts.Add(prop.Name); // Use actual casing from reflection
            currentType = prop.PropertyType;
            finalProp = prop;
        }

        propertyPath = string.Join(".", pathParts);
        return true;
    }

    private static Entry Build(Type t)
    {
        var all = t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .DistinctBy(p => p.Name)
            .ToArray();
        var searchable = all.Where(p =>
                p.PropertyType == typeof(string) && p.GetCustomAttribute<NotMappedAttribute>() == null)
            .ToArray();
        var lookup = all.ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);
        return new Entry { All = all, Searchable = searchable, Lookup = lookup };
    }

    private static Entry GetEntry<TEntity>()
    {
        return Cache.GetOrAdd(typeof(TEntity), Build);
    }

    #endregion

    /// <summary>
    /// Cached metadata for a single entity type.
    /// </summary>
    private sealed class Entry
    {
        #region Properties

        /// <summary>
        /// All public instance properties discovered for the entity type.
        /// </summary>
        public required PropertyInfo[] All { get; init; }

        /// <summary>
        /// Case-insensitive lookup from property name to reflected property info.
        /// </summary>
        public required Dictionary<string, PropertyInfo> Lookup { get; init; }

        /// <summary>
        /// String properties that are eligible for default free-text search.
        /// </summary>
        public required PropertyInfo[] Searchable { get; init; }

        #endregion
    }
}