// ==================================================
// REQUEST VALIDATOR TEMPLATE
// ==================================================
// Location: SlimBus.AppServices/Features/<FEATURE>/V1/<REQUEST>Validator.cs
//
// INSTRUCTIONS:
// 1. Replace <FEATURE> and <REQUEST> with your names
// 2. Copy all RuleFor(...) examples and customize for your DTOs
// 3. Add this validator to your AppServices project
// 4. Auto-discovered by FluentValidation assembly scan in AppSetup.cs
// 5. Change error messages to match your business rules

using FluentValidation;
using System;

namespace SlimBus.AppServices.Features.<FEATURE>.V1;

public sealed class Create<FEATURE>RequestValidator 
    : AbstractValidator<Create<FEATURE>Request>
{
    public Create<FEATURE>RequestValidator()
    {
        // TODO: Add validation rules for each property
        // Examples:
        
        // RuleFor(x => x.FullName)
        //     .NotEmpty().WithMessage("Full name is required")
        //     .MaximumLength(200).WithMessage("Full name cannot exceed 200 characters");

        // RuleFor(x => x.Email)
        //     .NotEmpty().WithMessage("Email is required")
        //     .EmailAddress().WithMessage("Email format is invalid")
        //     .MaximumLength(256).WithMessage("Email cannot exceed 256 characters");

        // RuleFor(x => x.DateOfBirth)
        //     .LessThanOrEqualTo(DateTime.Today)
        //     .When(x => x.DateOfBirth.HasValue)
        //     .WithMessage("Date of birth must be in the past");
    }
}

public sealed class Update<FEATURE>RequestValidator 
    : AbstractValidator<Update<FEATURE>Request>
{
    public Update<FEATURE>RequestValidator()
    {
        // TODO: Add validation rules for update DTO
        // (Often same as Create, but may differ)
    }
}
