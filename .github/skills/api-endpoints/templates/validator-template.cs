// ==================================================
// VALIDATOR TEMPLATE (FluentValidation for Request Types)
// ==================================================
// Location: SlimBus.AppServices/Features/<FEATURE>/V1/Actions/<Action>.cs
//   (Same file as request type, as internal sealed class)
//
// INSTRUCTIONS:
// 1. Create one validator per request type
// 2. Validator class name should be {RequestName}Validator
// 3. Inherit from AbstractValidator<TRequest>
// 4. Name by convention: required for auto-discovery
// 5. Use RuleFor chains for fluent validation rules
// 6. Add clear .WithMessage() explanations for each rule
// 7. Validators are auto-discovered during startup

using FluentValidation;

namespace SlimBus.AppServices.Features.<FEATURE>.V1.Actions;

// ========== CREATE VALIDATOR ==========

/// <summary>
/// Validates Create<FEATURE>Request.
/// Ensures required fields are not empty and meet constraints.
/// Auto-discovered by FluentValidation assembly scan.
/// </summary>
internal sealed class Create<FEATURE>RequestValidator : 
    AbstractValidator<Create<FEATURE>Request>
{
    public Create<FEATURE>RequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Email format is invalid")
            .MaximumLength(256).WithMessage("Email cannot exceed 256 characters");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .Length(2, 150).WithMessage("Name must be between 2 and 150 characters");

        RuleFor(x => x.Phone)
            .MaximumLength(50).WithMessage("Phone cannot exceed 50 characters")
            .When(x => !string.IsNullOrEmpty(x.Phone));

        // Optional: cross-property validation
        RuleFor(x => x)
            .Custom((request, context) =>
            {
                // Example: check if email format matches company domain
                // if (!request.Email.EndsWith("@company.com"))
                //     context.AddFailure("Email must be from company domain");
            });
    }
}

// ========== UPDATE VALIDATOR ==========

/// <summary>
/// Validates Update<FEATURE>Request.
/// Ensures at least one field is provided (partial update).
/// </summary>
internal sealed class Update<FEATURE>RequestValidator : 
    AbstractValidator<Update<FEATURE>Request>
{
    public Update<FEATURE>RequestValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("ID is required");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Email format is invalid")
            .MaximumLength(256).WithMessage("Email cannot exceed 256 characters")
            .When(x => !string.IsNullOrEmpty(x.Email));

        RuleFor(x => x.Name)
            .Length(2, 150).WithMessage("Name must be between 2 and 150 characters")
            .When(x => !string.IsNullOrEmpty(x.Name));

        RuleFor(x => x.Phone)
            .MaximumLength(50).WithMessage("Phone cannot exceed 50 characters")
            .When(x => !string.IsNullOrEmpty(x.Phone));

        // ✓ IMPORTANT: At least one field must be updated (no empty updates)
        RuleFor(x => x)
            .Custom((request, context) =>
            {
                if (string.IsNullOrEmpty(request.Email) &&
                    string.IsNullOrEmpty(request.Name) &&
                    string.IsNullOrEmpty(request.Phone))
                {
                    context.AddFailure(
                        "At least one field (Email, Name, Phone) must be provided for update");
                }
            });
    }
}

// ========== DELETE VALIDATOR ==========

/// <summary>
/// Validates Delete<FEATURE>Request.
/// Simple validation - just ensure ID is not empty.
/// </summary>
internal sealed class Delete<FEATURE>RequestValidator : 
    AbstractValidator<Delete<FEATURE>Request>
{
    public Delete<FEATURE>RequestValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("ID is required for deletion");
    }
}

// ========== CUSTOM ACTION: APPROVE VALIDATOR ==========

/// <summary>
/// Validates Approve<FEATURE>Request.
/// Reason is optional but if provided should have reasonable length.
/// </summary>
internal sealed class Approve<FEATURE>RequestValidator : 
    AbstractValidator<Approve<FEATURE>Request>
{
    public Approve<FEATURE>RequestValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("ID is required");

        RuleFor(x => x.Reason)
            .MaximumLength(500).WithMessage("Approval reason cannot exceed 500 characters")
            .When(x => !string.IsNullOrEmpty(x.Reason));
    }
}

// ========== CUSTOM ACTION: REJECT VALIDATOR ==========

/// <summary>
/// Validates Reject<FEATURE>Request.
/// Reason is REQUIRED for audit trail.
/// </summary>
internal sealed class Reject<FEATURE>RequestValidator : 
    AbstractValidator<Reject<FEATURE>Request>
{
    public Reject<FEATURE>RequestValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("ID is required");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Rejection reason is required for audit trail")
            .MaximumLength(500).WithMessage("Rejection reason cannot exceed 500 characters");
    }
}

/*
============================================================
FLUENT VALIDATION BEST PRACTICES
============================================================

1. NAMING CONVENTION:
   Request class:   Create<FEATURE>Request
   Validator class: Create<FEATURE>RequestValidator
   ✓ Auto-discovered if validator is in same assembly

2. VALIDATION RULES CHAIN:
   RuleFor(x => x.PropertyName)
       .NotEmpty().WithMessage("...")
       .Length(min, max).WithMessage("...")
       .Matches(regex).WithMessage("...");

3. CONDITIONAL VALIDATION:
   RuleFor(x => x.Field)
       .SomeRule()
       .When(x => x.OtherField != null);  // Only validate if condition true

4. CROSS-FIELD VALIDATION:
   RuleFor(x => x)
       .Custom((obj, context) => {
           if (condition)
               context.AddFailure("Field", "Message");
       });

5. COMMON RULES:
   .NotEmpty()              // String/Guid/Collection not empty
   .NotNull()               // Must be non-null
   .EmailAddress()          // Valid email format
   .Length(min, max)        // String length constraints
   .MaximumLength(max)      // Max length only
   .PhoneNumber()           // Valid phone format
   .Matches(regex)          // Regex pattern
   .Equal(value)            // Exact match
   .GreaterThan(value)      // Numeric comparison
   .LessThanOrEqualTo(value) // Another numeric comparison
.
6. ERROR HANDLING:
   Always provide clear .WithMessage() - shown to API users
   Message should explain what's wrong and expected format

7. AUTO-DISCOVERY:
   ✓ Class must be public or internal (not private)
   ✓ Inherit from AbstractValidator<T>
   ✓ Must be in same assembly as request classes
   ✓ Registered during AddValidatorsFromAssembly() in Startup
*/
