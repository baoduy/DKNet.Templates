---
name: dknet-appservices-actions
description: Create commands (Create/Update/business transitions/Delete) at the AppServices layer for a DKNet.Minimal feature — request contracts, FluentValidation validators, and SlimMessageBus handlers, in both the hand-written and generator-driven (CrudCreate/CrudUpdate/CrudAction) shapes. Use after the domain entity and EF Core mapper exist. Queries and paged lists are the `dknet-queries-specs` skill; response DTOs and Mapster are `dknet-dto-mapping`.
---

# AppServices actions

Commands only: Create, Update, a business-rule transition, Delete, and the generator's
`[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]` equivalents. For query specs and paged lists, load
`dknet-queries-specs`. For DTO shape and Mapster wiring, load `dknet-dto-mapping`. For whether a
rule belongs on the entity or in a handler, load `dknet-ddd-principles` first.

## GlobalUsings already in Minimal.AppServices

`Minimal.AppServices/GlobalUsings.cs` gives every file in the project these without an explicit
`using`: `DKNet.AspCore.Extensions.ModelBinding` (`[FromClaim]`), `DKNet.SlimBus.Extensions`
(`Fluents.*`, `NotFoundError`), `System.ComponentModel.DataAnnotations`, `System.Security.Claims`
(`ClaimTypes`), `System.Text.Json.Serialization`, `FluentResults`, `Minimal.Domains.Services`,
`Microsoft.Extensions.DependencyInjection`, `FluentValidation`, `Mapster`, `MapsterMapper`,
`Minimal.AppServices.Extensions`, `DKNet.SlimBus.Extensions.LazyMapper`, `Minimal.AppServices.Share`
(`PreconditionCodes`, `IPrincipalProvider`). An action file typically adds only the entity's and its
own spec's namespace.

## Request and handler contracts

| Shape | Request | Handler | Return |
|---|---|---|---|
| Command returning a DTO | `Fluents.Requests.IWitResponse<TDto>` | `Fluents.Requests.IHandler<TReq, TDto>` | `Task<IResult<TDto>>` |
| Command with no response | `Fluents.Requests.INoResponse` | `Fluents.Requests.IHandler<TReq>` | `Task<IResultBase>` |

The handler method is `OnHandle(TRequest, CancellationToken)`, never `Handle`. Both are discovered
by assembly scan onto the in-memory bus — no per-message registration. Handlers never call
`SaveChanges`: the SlimBus EF Core interceptor auto-saves once, after the handler returns. A request
record is `public sealed record`; its validator and handler are `internal sealed class` — enforced
by `Minimal.App.Tests/Architecture/AppServiceTests.cs`.

File layout: one file per verb, request + validator + handler co-located —
`<Feature>/V1/Actions/<Verb>.cs` (`ManualSample/V1/Actions/Create.cs`, `Update.cs`, `Cancel.cs`,
`Delete.cs`).

## Acting user

`mode=manual`: a `[FromClaim(ClaimTypes.Name)] public string? ByUser { get; set; }` property.
`AddContextualRequestPopulation` (wired once in `Program.cs`) overwrites it from the caller's claim
before validation and before the handler runs — a value the caller put in the body is always
discarded, never trusted. The handler still guards it:

```csharp
if (string.IsNullOrEmpty(request.ByUser))
{
    return Result.Fail<PurchaseOrderDto>("The caller is not authenticated.");
}
```

The demo authentication provider (`FeatureManagement:EnableDemoAuthentication`) supplies this claim
locally and in tests, so the guard is reachable but rarely hit — an authenticated caller with a
missing name claim still has to fail it explicitly, there is no fallback to a system account.

`mode=auto`: a generated request never carries `[FromClaim]` — the generator forwards only
`System.ComponentModel.DataAnnotations` attributes, and `[FromClaim]` lives in
`DKNet.AspCore.Extensions.ModelBinding`. Instead `DataOwnerHook` stamps `OwnedBy` and the audit hook
stamps `CreatedBy`/`UpdatedBy`, both from `IPrincipalProvider`, wired once in
`ServiceConfigs.AddAllAppServices` (`.AddDataOwnerProvider<CoreDbContext, PrincipalProvider>()` /
`.AddCurrentUserProvider<CoreDbContext, PrincipalProvider>()`) and applying to every entity on
`CoreDbContext`, not just the one you're adding.

**The `[CrudAction]` acting-user pitfall.** A method parameter literally named `byUser` is still
just a constructor/method parameter to the generator — it becomes a caller-settable, body-bound
property:

```csharp
[CrudAction("approval")]
public void Approve(string byUser) => SetUpdatedBy(byUser);
```

generates `public required string ByUser { get; init; }` on `ApproveProductRequest`, bound from the
JSON body (`{ "byUser": "alice" }`). Nothing stamps it from the caller's identity — whatever the
client sends is what `SetUpdatedBy` receives. Don't name a `[CrudCreate]`/`[CrudUpdate]`/
`[CrudAction]` parameter after an acting-user concept; if a method needs the real caller, stamp it
from `DataOwnerHook`/the audit hook instead, or hand-write the action.

## FluentValidation

A validator is `internal sealed class XValidator : AbstractValidator<TRequest>`, co-located with
its request. There is no per-route opt-in call: `Minimal.Api/Configs/FluentValidationConfig.cs`
registers every validator in the assembly —

```csharp
builder.Services.AddValidatorsFromAssembly(typeof(AppSetup).Assembly, includeInternalTypes: true);
```

— and `UseEndpointConfigs` attaches `AddFluentValidationAutoValidation()` to **every** endpoint
group (`Program.cs`), so it runs on generated routes exactly as it runs on hand-mapped ones. A
request failing validation never reaches the handler; it short-circuits to `400` (or `409`, see
below) with no handler code involved. Two hard constraints: the validator class must live in
`Minimal.AppServices` (one in `Minimal.Api` is never registered), and it must be `internal sealed`.

**Ordinary input rules** stay inline (`RuleFor(a => a.Amount).GreaterThan(0)`). Keep `Id` out of an
update validator — an unknown/empty id is a `404` from the handler's spec lookup, not a `400` from
validation (`UpdatePurchaseOrderCommandValidator`'s comment says this explicitly).

**Precondition validators** read stored data through `IRepositorySpec` and refuse with a code:

```csharp
internal sealed class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator(IRepositorySpec repository)
    {
        RuleFor(r => r.Name)
            .MustAsync(async (name, ct) => !await repository.AnyAsync(new SpecProductByName(name), ct))
            .WithErrorCode(PreconditionCodes.ProductNameTaken)
            .WithMessage(r => $"The product name '{r.Name}' is already taken.");
    }
}
```

Any code prefixed `PreconditionCodes.Prefix` (`"precondition."`) makes
`FluentValidationConfig`'s `AddErrorResponses` answer `409` instead of `400`, with that code echoed
in the response body's `code` extension. This is a check, not a guarantee — the database's unique
index is what actually enforces it under a race.

**Keep 404 out of validators.** `DeleteProductRequestValidator` deliberately *passes* an unknown id:

```csharp
RuleFor(r => r.Id)
    .MustAsync(async (id, ct) =>
    {
        var product = await repository.FirstOrDefaultAsync(new SpecGetProduct(id), ct);
        return product is null || product.IsDiscontinued;
    })
    .WithErrorCode(PreconditionCodes.ProductDeleteWhileForSale)
    .WithMessage("The product is still for sale and cannot be deleted.");
```

If it refused a missing id too, the route's own `404` would be hidden behind a `409`.

**Validators on generated requests are the supported way to add a business rule to a generated
route.** `CreateProductRequestValidator` and `DeleteProductRequestValidator` both target generated
request types (`Minimal.AppServices.Crud.CreateProductRequest` / `DeleteProductRequest`) — no
literal `Map*` call is needed for FluentValidation to reach them, only the `using
Minimal.AppServices.Crud;` and a constructor parameter naming the request type. This is the fix for
the one thing `[DataAnnotations]` cannot do on a generated route: `[Range]`/`[Required]`/etc.
forwarded onto a generated request property is **not evaluated**, because .NET's validation source
generator only recognizes literal `Map*(string, Delegate)` calls in the compiling project's own
source — true for `PurchaseOrderV1Endpoint`'s hand-mapped routes, false for anything routed through
`DKNet.AspCore.Extensions`'s generic `Map*<TRequest,TDto>` wrapper, which is every generated CRUD
route. `POST /v1/products` with `price: -1` returns `201`, not `400`, despite
`CreateProductRequest.Price` carrying `[Range(0.01, double.MaxValue)]`. Don't assume a
DataAnnotations attribute on a generated request is enforced; write a validator for any rule you
actually need.

## Handler patterns

**Create** (`ManualSample/V1/Actions/Create.cs`) — construct the aggregate through its own
constructor, add it, return a lazily-mapped result so the response carries the DB-generated `Id`:

```csharp
var order = new PurchaseOrder(request.CustomerName, request.Amount, request.ByUser);
await repository.AddAsync(order, cancellationToken);
return mapper.ResultOf<PurchaseOrderDto>(order);
```

**Update / business transition** (`Update.cs`) — fetch via spec, `404` if missing, mutate through
a domain method, map eagerly since nothing on the DTO needs a value only `SaveChanges` produces:

```csharp
var order = await repository.FirstOrDefaultAsync(new SpecGetPurchaseOrder(request.Id), cancellationToken);
if (order is null)
    return Result.Fail<PurchaseOrderDto>(new NotFoundError($"The purchase order {request.Id} was not found."));
order.ChangeAmount(request.Amount, request.ByUser);
return Result.Ok(mapper.Map<PurchaseOrderDto>(order));
```

**Rejected transition** (`Cancel.cs`) — a domain-specific `409`, distinct from the generic
`NotFoundError` `404`:

```csharp
if (order.Status == PurchaseOrderStatus.Cancelled)
    return Result.Fail<PurchaseOrderDto>(
        new Error($"The purchase order {request.Id} is already cancelled.")
            .WithMetadata("Code", PreconditionCodes.PurchaseOrderAlreadyCancelled));
```

`.WithMetadata("Code", ...)` is the handler-side equivalent of `.WithErrorCode(...)` in a
validator — same `PreconditionCodes.Prefix` check drives the same `409`.

**Delete** (`Delete.cs`, `INoResponse`) — fetch, `404` if missing, `repository.Delete(order)`,
`Result.Ok()`. No DTO, no mapper.

**Multi-aggregate command** (`AutomatedSample/V1/Actions/Discontinue.cs`) — one handler, two writes,
one `SaveChanges`:

```csharp
public sealed record DiscontinueProductCommand : Fluents.Requests.IWitResponse<ProductDto>
{
    public Guid Id { get; init; }              // not `required` — bound via `req with { Id = id }`
    [Required, StringLength(150)] public string ReplacementName { get; init; } = null!;
    [Range(0.01, double.MaxValue)] public decimal ReplacementPrice { get; init; }
}
```

```csharp
product.Discontinue();
var replacement = new Product(request.ReplacementName, request.ReplacementPrice);
await repository.AddAsync(replacement, cancellationToken);
return mapper.ResultOf<ProductDto>(product);
```

Named `...Command`, not `...Request`: `Product.Discontinue()` keeps its own `[CrudAction]`
declaration (so the generator still has a route to drop by name), which reserves the generated
`DiscontinueProductRequest`/`DiscontinueProductHandler` names — a second, hand-written type by
either name would collide. `Id` is deliberately not `required`: the request body from a client never
carries `id` (it comes from the route), and `required` would make System.Text.Json reject that body
outright; the endpoint supplies it with `req with { Id = id }`. This kind of command cannot be
generated at all — an operation writing more than one aggregate in one transaction is outside what
`[CrudUpdate]`/`[CrudAction]` can express — so it stays fully hand-written even though the entity is
otherwise generator-driven.

## Generated requests and handlers (`mode=auto`)

Naming, mechanical from the entity's own signatures: `Create<Entity>Request` (from `[CrudCreate]`),
`Change<Member><Entity>Request` (from a `[CrudUpdate]` method named `Change<Member>`),
`<Method><Entity>Request` (from `[CrudAction]`), `Delete<Entity>Request` (always). Handlers mirror
the name with `Handler` instead of `Request` — `CreateProductHandler`, `ChangePriceProductHandler`,
`ApproveProductHandler`, `DiscontinueProductHandler`, `AssignSupplierReferenceProductHandler` — all
in namespace `Minimal.AppServices.Crud`, emitted under
`obj/Generated/DKNet.SlimBus.Generators/.../<Entity>CrudRequests.g.cs` and `...Handlers.g.cs`. They
are compiler output, not files in the repo — inspect them after a build, not by searching source.

A generated update handler, verbatim (`ProductCrudHandlers.g.cs`):

```csharp
internal sealed class ChangePriceProductHandler(IRepositorySpec repository, IMapper mapper)
    : Fluents.Requests.IHandler<ChangePriceProductRequest, ProductDto>
{
    public async Task<IResult<ProductDto>> OnHandle(ChangePriceProductRequest request, CancellationToken cancellationToken)
    {
        var entity = await repository.FirstOrDefaultAsync(new ProductByIdCrudSpec(request.Id), cancellationToken);
        if (entity is null)
            return Result.Fail<ProductDto>(new NotFoundError($"Product '{request.Id}' was not found."));
        entity.ChangePrice(request.Price);
        return mapper.ResultOf<ProductDto>(entity);
    }
}
```

Fetch by a private, generated `ProductByIdCrudSpec`, `404` if missing, call the one domain method,
`ResultOf`. This is exactly the shape of a hand-written Update handler above, generated for you.

**Replacing a generated handler.** The generated file's own doc comment says how: "Write a class
implementing the same IHandler to replace it." Add your own `internal sealed class` implementing
`Fluents.Requests.IHandler<ChangePriceProductRequest, ProductDto>` in `Minimal.AppServices` — DI
registration is by interface via assembly scan, so yours and the generated one cannot coexist; keep
only yours (there is no attribute to suppress generation of the handler alone — drop the whole
route with `CrudMapOptions.Exclude` if you also need to change the request shape).

**Adding a rule without leaving the generated route**: write a validator, as above.

**The generated request records are `partial`.** You may declare a member in your own partial
`record CreateProductRequest { ... }`, but the generated handler never reads it — it only
constructs the entity from the parameters the generator saw on `[CrudCreate]`. A partial addition is
for a validator or a Mapster customization to read, not a way to pass extra data into the entity
constructor.

## Error → HTTP mapping

| Outcome | Status | Body |
|---|---|---|
| `Result.Ok(dto)` | `200`, or `201` on a create route with `isCreated: true` | the DTO |
| `Result.Fail(new NotFoundError(...))` | `404` | `errors: [{ message }]` |
| `Result.Fail(...)`/validator error with a `precondition.`-prefixed code | `409` | `errors: [...]`, `code` = that code |
| Any other `Result.Fail(...)` or FluentValidation failure | `400` | `errors: [{ message, code?, field? }]` |
| Unhandled `OwnershipRequiredException` | `403` | `errors: [{ message }]` |
| Any other unhandled exception | `500` | generic message outside `Development`, `traceId` always present |

Every body shares `title: "Error"`, `status`, `type` (the status's name, e.g. `"Conflict"`), and
`traceId` (the current `Activity` id, falling back to `TraceIdentifier`). One registration answers
all three failure kinds — `Minimal.Api/Configs/FluentValidationConfig.cs`'s single
`AddErrorResponses(...)` call — there is no second place to configure this.

## Architecture rules enforced by tests

`Minimal.App.Tests/Architecture/AppServiceTests.cs` and `RecordArchitectureTests.cs` pin, exactly:

- Every class implementing `IRequestHandler<>`/`IRequestHandler<,>`/`IConsumer<>` must be
  non-public and `sealed` (`AllHandlerClassesShouldBeInternalAndSealed`).
- Every non-abstract class inheriting `AbstractValidator<>` must be non-public and `sealed`
  (`AllValidatorClassesShouldBeInternalAndSealed` — two hand-authored customer/order sample
  validators are exempted by name; a new feature's validators are not).
- Any interface named `*Repo`/`*Repository` must inherit `IRepository<TEntity>`
  (`AllRepoInterfaces_ShouldInheritFromIRepository`).
- A `[GenerateDto]` type must expose no property whose type (or generic argument) inherits
  `DomainEntity` (`DtosWithGenerateDtoAttribute_ShouldNotHaveProperties_ThatAreDomainEntities`) —
  the DTO boundary must not leak an entity.
- No record type in `Minimal.Domains` or `Minimal.AppServices` may declare a public property with a
  **private** setter (`RecordTypes_ShouldNotContain_PrivateSetters_OnPublicProperties`) — AutoMapper
  (Mapster's `MapToConstructor`/property assignment) cannot fill it.

## Step-by-step

**mode=manual** (mirror `ManualSample/PurchaseOrder`):

1. Add `<Feature>/V1/Actions/Create.cs`: request implementing `IWitResponse<TDto>` with
   `[FromClaim(ClaimTypes.Name)] ByUser`, a co-located validator, a handler constructing the
   aggregate and returning `mapper.ResultOf<TDto>(entity)`.
2. Add `Update.cs` / a named business-transition file: request with `Id` (route-bound) and the
   mutable fields, a validator that skips `Id`, a handler that fetches via spec, 404s, calls a
   domain method, returns `Result.Ok(mapper.Map<TDto>(entity))`.
3. Add `Delete.cs`: `INoResponse` request, handler fetches, 404s, `repository.Delete(entity)`,
   `Result.Ok()`.
4. Wire each into `<Feature>V1Endpoint.cs` with a literal `Map*` call (`dknet-endpoint-config`) so
   DataAnnotations validation, if any, is actually enforced.

**mode=auto** (mirror `AutomatedSample/Product`):

1. Confirm the entity already carries `[CrudCreate]` on its constructor and `[CrudUpdate]`/
   `[CrudAction]` on its mutation methods (`dknet-domain-entity`, `dknet-crud`).
2. Build once; read the generated request/handler/endpoint files to know the exact contract.
3. Add any business rule as a validator against the generated request type — never a hand-mapped
   route just to enforce a `[Range]`/`[Required]` attribute.
4. For an operation that must write more than one aggregate, or must reject a repeat call as a
   domain failure rather than a `200`, write a `...Command` (not `...Request`) and drop the
   generated route for that member with `CrudMapOptions.Exclude("MethodName")` in the endpoint.

## Validation checklist

- Request is `public sealed record`; validator and handler are `internal sealed class`.
- `[FromClaim(ClaimTypes.Name)]` only on a hand-written request — never named onto a
  `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]` parameter.
- Handler guards `IsNullOrEmpty(request.ByUser)` (manual mode) before doing anything else.
- No `SaveChanges` call anywhere in the handler.
- A precondition rule uses `.WithErrorCode(PreconditionCodes.X)` (validator) or
  `.WithMetadata("Code", PreconditionCodes.X)` (handler `Error`), not a bare `Result.Fail(string)`.
- A validator that must let an unknown id through to the route's own `404` does so explicitly.
- `mapper.ResultOf<TDto>(entity)` after a write whose DTO needs a DB-generated value;
  `mapper.Map<TDto>(entity)` otherwise.
- A rule for a generated request lives in a validator, not a DataAnnotations attribute you're
  hoping gets enforced.

## Common mistakes

- **What you might expect**: adding `[Range(0.01, double.MaxValue)]` to a `[CrudUpdate]` parameter
  enforces it, since the generated request clearly carries the attribute.
  **What actually happens**: `PUT` with a negative value still returns `200`.
  **Why**: the generated route is mapped through a generic library wrapper the .NET validation
  source generator cannot see into; only a validator runs on that route.

- **What you might expect**: a `[CrudAction]` method parameter called `byUser` is populated the
  same way `[FromClaim]` populates a hand-written request.
  **What actually happens**: the generated request exposes `ByUser` as an ordinary
  caller-supplied, required body field.
  **Why**: the generator forwards only `DataAnnotations` attributes; `[FromClaim]` is a different
  namespace it never looks at.

- **What you might expect**: a validator refusing an unknown id in a delete/update route is the
  safer default.
  **What actually happens**: the route now answers `409` for a target that doesn't exist, instead
  of the expected `404`.
  **Why**: FluentValidation runs before the handler; a `MustAsync` that fails closed on "not found"
  hides the handler's own 404 behind a validation-layer 409.

- **What you might expect**: replacing a generated handler means adding an attribute to suppress
  generation.
  **What actually happens**: there is no such attribute — both classes would be registered, and DI
  resolution becomes ambiguous.
  **Why**: the generator has no per-handler opt-out; write your own class implementing the same
  `IHandler<TRequest, TDto>` and remove nothing from the entity, or drop the whole route with
  `CrudMapOptions.Exclude` if the request shape itself must change.
