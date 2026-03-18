using System.Text.Json;
using System.Text.RegularExpressions;

namespace SlimBus.AppServices.Share.Generics;

/// <summary>
/// Provides filter JSON validation extensions for validators that target <see cref="GenericListParameters"/>.
/// </summary>
public static partial class GenericListParametersValidatorExtension
{
    // Allow alphanumeric, underscores, and dots for nested field names (e.g., GeneralInfo.CompanyType)
    private static readonly Regex FieldNameRegex = FieldNameValidationRegex();

    [GeneratedRegex(
        @"^[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.ExplicitCapture,
        50)]
    /// <summary>
    /// Builds the regex used to validate top-level and nested filter field names.
    /// </summary>
    /// <returns>A compiled regular expression for filter field names.</returns>
    private static partial Regex FieldNameValidationRegex();

    /// <summary>
    /// Adds validation rules for <see cref="GenericListParameters.FiltersJson"/>.
    /// </summary>
    /// <typeparam name="T">The type being validated, must extend GenericListParameters.</typeparam>
    /// <param name="validator">The validator to add rules to.</param>
    /// <returns>The same validator instance to support method chaining.</returns>
    public static AbstractValidator<T> AddFiltersJsonValidation<T>(this AbstractValidator<T> validator)
        where T : GenericListParameters
    {
        // Raw FiltersJson size guard
        validator.RuleFor(x => x.FiltersJson)
            .MaximumLength(4000)
            .When(x => !string.IsNullOrEmpty(x.FiltersJson));

        // Deep validation of parsed filters
        validator.RuleFor(x => x)
            .Custom((request, context) =>
            {
                if (string.IsNullOrEmpty(request.FiltersJson)) return;

                ICollection<FilterInfo> filters;
                try
                {
                    filters = request.Filters;
                }
                catch (JsonException jex)
                {
                    context.AddFailure("FiltersJson", "Invalid filtersJson: " + jex.Message);
                    return;
                }
                catch (NotSupportedException nex)
                {
                    context.AddFailure("FiltersJson", "Unsupported filtersJson: " + nex.Message);
                    return;
                }

                if (filters.Count > 25)
                    context.AddFailure("FiltersJson", "Too many filters (max 25).");

                foreach (var f in filters)
                {
                    if (string.IsNullOrWhiteSpace(f.FieldName))
                    {
                        context.AddFailure("FiltersJson", "Filter FieldName is required.");
                        continue;
                    }

                    if (!FieldNameRegex.IsMatch(f.FieldName))
                        context.AddFailure("FiltersJson", $"Invalid field name '{f.FieldName}'.");

                    // Operator validity (enum range) using generic overload
                    if (!Enum.IsDefined(f.Operator))
                        context.AddFailure("FiltersJson", $"Invalid operator '{f.Operator}'.");

                    // Value requirements: for Equals/NotEquals require a value; Any allows comma-separated list
                    if (f.Operator != FilterOperator.Any && string.IsNullOrWhiteSpace(f.Value))
                        context.AddFailure("FiltersJson", $"Filter value required for operator {f.Operator}.");

                    if (!string.IsNullOrEmpty(f.Value) && f.Value.Length > 512)
                        context.AddFailure("FiltersJson", "Filter value too long (max 512).");
                }
            });

        return validator;
    }
}
