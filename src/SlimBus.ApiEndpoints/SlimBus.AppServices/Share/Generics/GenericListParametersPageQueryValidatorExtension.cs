using System.Text.RegularExpressions;

namespace SlimBus.AppServices.Share.Generics;

/// <summary>
///     Extension methods for applying common validation rules to validators that extend GenericListParameters.
/// </summary>
public static partial class GenericListParametersPageQueryValidatorExtension
{
    #region Fields

    // Allow alphanumeric, underscores, and dots for nested field names (e.g., GeneralInfo.CompanyType)
    private static readonly Regex FieldNameRegex = FieldNameValidationRegex();

    #endregion

    #region Methods

    [GeneratedRegex(
        @"^[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.ExplicitCapture,
        50)]
    /// <summary>
    /// Creates the regex used to validate <c>OrderBy</c> and <c>SearchFields</c> field paths.
    /// </summary>
    /// <returns>A compiled regex for validating field names and nested property paths.</returns>
    private static partial Regex FieldNameValidationRegex();

    #endregion

    /// <summary>
    /// Defines validator extensions for a type derived from <see cref="GenericListParameters"/>.
    /// </summary>
    /// <param name="validator">The validator instance to extend.</param>
    /// <typeparam name="T">The request type being validated.</typeparam>
    extension<T>(AbstractValidator<T> validator) where T : GenericListParameters
    {
        /// <summary>
        /// Adds date range, paging, ordering, and search-text validation rules.
        /// </summary>
        /// <returns>The same validator instance to support method chaining.</returns>
        public AbstractValidator<T> AddPageQueryValidation()
        {
            // Date range validation - only validate when both are provided
            validator.RuleFor(x => x.To)
                .GreaterThanOrEqualTo(x => x.From!.Value)
                .When(x => x.From.HasValue && x.To.HasValue)
                .WithMessage("To must be greater than or equal to From.");

            // Max span 1 year (365 days)
            validator.RuleFor(x => x)
                .Must(r => r.To - r.From <= TimeSpan.FromDays(365))
                .When(x => x.From.HasValue && x.To.HasValue)
                .WithMessage("Date range cannot exceed 1 year.");

            // Paging validation (optional properties)
            validator.RuleFor(x => x.PageNumber)
                .GreaterThan(0)
                .When(x => x.PageNumber.HasValue)
                .WithMessage("PageNumber must be greater than 0.");

            validator.RuleFor(x => x.PageSize)
                .GreaterThan(0)
                .LessThanOrEqualTo(100)
                .When(x => x.PageSize.HasValue)
                .WithMessage("PageSize must be between 1 and 100.");

            // OrderBy validation (single property name pattern)
            validator.RuleFor(x => x.OrderBy)
                .MaximumLength(50)
                .Matches(FieldNameRegex)
                .When(x => !string.IsNullOrWhiteSpace(x.OrderBy));

            // Search text length
            validator.RuleFor(x => x.SearchText)
                .MaximumLength(100)
                .When(x => !string.IsNullOrWhiteSpace(x.SearchText));

            return validator;
        }

        /// <summary>
        /// Adds validation rules for <see cref="GenericListParameters.SearchFields"/>.
        /// </summary>
        /// <returns>The same validator instance to support method chaining.</returns>
        public AbstractValidator<T> AddSearchFieldsValidation()
        {
            // SearchFields collection constraints
            validator.RuleFor(x => x.SearchFields)
                .Must(arr => arr!.Length <= 20)
                .When(x => x.SearchFields is { Length: > 0 })
                .WithMessage("SearchFields exceeds maximum of 20.");

            validator.RuleForEach(x => x.SearchFields)
                .NotEmpty()
                .MaximumLength(128)
                .Matches(FieldNameRegex)
                .When(x => x.SearchFields is { Length: > 0 });

            return validator;
        }

        /// <summary>
        /// Adds all common <see cref="GenericListParameters"/> validation rules.
        /// </summary>
        /// <returns>The same validator instance to support method chaining.</returns>
        public AbstractValidator<T> AddGenericListParametersValidation()
        {
            return validator
                .AddPageQueryValidation()
                .AddSearchFieldsValidation()
                .AddFiltersJsonValidation();
        }
    }
}