# Quick Validation Checklist: API Endpoints

## DTOs & Validators
- [ ] Response DTOs use [GenerateDto] attribute (auto-generated from entity)
- [ ] Request DTOs created as manual records (Create/Update requests)
- [ ] Validator class created for each request DTO
- [ ] Validators inherit from AbstractValidator<T>
- [ ] Validators auto-discovered by FluentValidation assembly scan
- [ ] All validators registered in AppSetup.cs

## Endpoint Configuration
- [ ] All CRUD endpoints mapped (GET, POST, PUT, DELETE)
- [ ] Correct HTTP methods and route patterns
- [ ] Status codes properly documented (201, 204, 400, 404, etc.)
- [ ] Error responses use ProblemDetails format

## OpenAPI & Documentation
- [ ] OpenAPI/Swagger documentation generated automatically
- [ ] Endpoints have proper summaries and descriptions
- [ ] Response types documented with Produces<T>()
- [ ] Request types documented with Accepts<T>()

## Code Quality
- [ ] All code compiles with zero warnings
- [ ] Namespace conventions followed (Api/ApiEndpoints/...)
- [ ] Integration tests pass for all endpoints

