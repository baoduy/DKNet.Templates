# Quick Validation Checklist: CRUD Operations

## Request DTOs & Validators
- [ ] Request DTOs created as simple records (Create, Update, Delete)
- [ ] Validator class created for each request DTO type
- [ ] All validators inherit from AbstractValidator<T>
- [ ] Validators enforce all business rules (required fields, lengths, formats)
- [ ] Validators auto-discovered by FluentValidation assembly scan

## Query Specs Pattern
- [ ] Specs created in AppServices/Specs/ for each query pattern
- [ ] Each spec inherits from Specification<Entity>
- [ ] Specs for: GetById, GetByEmail, GetByUserId, etc.
- [ ] Specs include any Include() for related data
- [ ] Specs used via IRepository<T>.FirstOrDefaultAsync(spec)

## Write Repository Pattern
- [ ] Repository interface in AppServices layer (write-only)
- [ ] Repository implementation sealed and in Infra/Repos/ folder
- [ ] Only write methods: Add, Update, Delete, SaveChanges
- [ ] Read operations delegated to Specs + IRepository<T>

## Services/Business Logic
- [ ] Service class orchestrates IRepository<T> (reads via specs)
- [ ] Service injects IRepository<T> and ICustomerProfileRepository
- [ ] Business rules enforced at service layer
- [ ] Domain events published after mutations

## Code Quality
- [ ] All code compiles with zero warnings
- [ ] Namespace conventions followed (AppServices/Features/<Feature>/...)
- [ ] Unit tests pass for validators and business logic


