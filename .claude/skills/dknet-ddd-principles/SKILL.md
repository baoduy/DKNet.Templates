---
name: dknet-ddd-principles
description: DDD tactical judgment for this codebase — aggregate boundaries, entity vs. value object, invariant enforcement, when to use a domain event, avoiding anemic domain models. Use before dknet-domain-entity and dknet-appservices-actions whenever the aggregate shape or business-rule placement isn't obvious.
---

# Skill: DDD Tactical Principles

This codebase already gives you the class shapes (`AggregateRoot`, `DomainEntity`, `AddEvent`) — see `dknet-domain-entity`. This skill covers the judgment calls those shapes don't make for you: what belongs in one aggregate, what's an entity vs. a value object, where an invariant lives, and when a domain event is the right tool.

This template is a single microservice / single bounded context — these are tactical patterns, not strategic (bounded-context) design.

---

## When to Use

- Deciding what a new aggregate root should own vs. what belongs to a separate aggregate.
- Deciding whether a new type needs its own identity (entity) or is just a bag of values (owned/value object).
- Deciding whether a business rule belongs on the entity, in a domain service, or in the command handler.
- Deciding whether a mutation needs to publish a domain event or just needs a plain method call.

## Aggregate Boundaries

An aggregate is a transactional consistency boundary: everything inside it is saved together and its invariants are enforced together. The rule of thumb —

- If two pieces of data must always be consistent with each other *at the moment they're saved* (e.g. an order and its line items, where a total must match the sum of lines), they belong in the same aggregate.
- If two pieces of data can be consistent *eventually*, a moment apart, they belong in separate aggregates, coordinated through a domain event — not a direct object reference.
- Aggregates reference each other by ID (`Guid`), never by object reference. `PurchaseOrder` (`Minimal.Domains/Features/ManualSample/Entities/PurchaseOrder.cs`) does not hold a `Customer` object — it holds a plain `CustomerName` string, no navigation property back to another aggregate.

Keep aggregates small. A large aggregate means more contention (every mutation locks the whole thing) and usually signals a boundary was drawn around "things that seem related" rather than "things that must be consistent together."

## Entity vs. Value Object

- **Entity** (in this codebase: `AggregateRoot` for the root, `DomainEntity` for non-root entities): has identity and a lifecycle. Two entities with identical property values are still different entities if their `Id` differs. `PurchaseOrder` is an entity — two orders with the same customer name and amount are still two different orders.
- **Value object** (owned type, plain class with no `Id`): defined entirely by its values. Two value objects with identical properties are interchangeable. If a type never needs to be looked up or referenced independently of its parent entity, it's a value object — model it as a plain owned type (see `dknet-domain-entity` "Step 3: Create Owned Value Objects"), not as another `DomainEntity`.

Ask: "Do I ever need to fetch or reference this thing on its own, independent of its parent?" Yes → entity. No → value object.

## Invariant Enforcement

An invariant is a rule that must always hold true for an entity (e.g. "an already-cancelled order can't be cancelled again"). In this codebase, invariants are enforced by construction and by the entity's own mutation methods — never by a public setter:

- Properties are `{ get; private set; }`. Nothing outside the entity can put it into an invalid state directly.
- The constructor establishes the invariant for a new entity. Named mutation methods re-establish it for every change, and are the *only* path to changing mutable state — see `PurchaseOrder.ChangeAmount(...)` and `PurchaseOrder.Cancel(...)` in `dknet-domain-entity`.
- `PurchaseOrder.Cancel(string userId)` is the clearest example of an invariant guarded outside the constructor: the *handler* (`CancelPurchaseOrderCommandHandler`, in `Minimal.AppServices/ManualSample/V1/Actions/Cancel.cs`) checks `order.Status == PurchaseOrderStatus.Cancelled` and fails the request before calling `Cancel` — a business rule enforced at the aggregate boundary, one call into the entity method away from being violated by a second caller who skips that check.
- If a rule needs data external to the entity (e.g. "customer name must be unique across all orders"), that's not an entity invariant — it's a cross-entity business rule, and it belongs in the command handler as a duplicate-check `Specification` query (see `dknet-appservices-actions`), because the entity has no way to see other entities.

## When to Use a Domain Event

This codebase has two equally valid ways to raise the same kind of event — pick the one that matches how much control you need over the raise:

- **Hand-raised** (`PurchaseOrder`): the constructor calls `AddEvent(new PurchaseOrderCreatedEvent(Id, CustomerName, Amount))` directly, in application code you can step through in a debugger. Use this when the event's payload, timing, or "did this actually happen" condition needs logic more specific than "a tracked property changed."
- **Declared** (`Product`): the class carries `[RaisesEvent(EventOperations.Created, Include = [nameof(Id), nameof(Name), nameof(Price)])]` and `[RaisesEvent(EventOperations.Updated, nameof(Price))]` — no line of application code calls `AddEvent` anywhere in `AutomatedSample/`. DKNet's EF Core save hook reads these declarations and raises the composed event records (`ProductCreatedEvent`, `ProductPriceUpdatedEvent`) after a successful `SaveChanges`, driven by the change tracker. Use this when the event is a straightforward "this property changed" notification and you're already using `[CrudCreate]`/`[CrudUpdate]` for the entity.

Either way, publish a domain event (delivered via `Minimal.Infra/Services/EventPublisher.cs`) when **something outside this aggregate might care that this happened** — another aggregate needs to react, or an external system needs to be notified. `Product`'s declared `Created` event reaches an external Azure Service Bus subscriber (`ProductCreatedNotificationHandler`) exactly like a hand-raised one would.

Do NOT reach for an event when the effect is entirely local to this one request:
- Setting a computed field during the same handler → just do it in the handler or the entity method, no event needed.
- A validation failure → return `Result.Fail(...)`, don't publish an event.

If you can't name a concrete future subscriber (even a logging handler counts, but "just in case" doesn't), it's not an event yet — add it when a real consumer appears.

## Avoiding Anemic Domain Models

An anemic model is an entity that's just a property bag, with all the actual business logic living in command handlers. Symptoms to watch for:

- A handler reads several properties off an entity, computes something, then writes several properties back — that computation belongs in a method on the entity (e.g. `entity.Update(...)`, or a more specific method like `entity.Cancel(userId)`), not in the handler.
- The handler is the only place an invariant is checked — meaning the entity could be constructed or mutated elsewhere into an invalid state.

The handler's job is orchestration: fetch the entity (via `IRepositorySpec` + a `Specification`), call one or more methods on it, persist, map to a DTO. The entity's job is protecting its own consistency and encoding what a valid state transition looks like.

## Decision Checklist

- [ ] Can I name a concrete reason this needs to be consistent with the parent in the same transaction? If no, it's a separate aggregate.
- [ ] Does this type ever get looked up independently of its parent? If no, it's a value object, not an entity.
- [ ] Does this rule only need data already on the entity? If yes, enforce it in the entity's constructor/`Update` method, not the handler.
- [ ] Can I name a real, current subscriber for this event? If no, skip the event for now.
- [ ] Is the handler computing business logic, or just orchestrating fetch → mutate → persist → map? If it's computing, move the logic onto the entity.

---

## Next Steps

→ **dknet-domain-entity** — apply these decisions to actual entity code
→ **dknet-appservices-actions** — apply the handler-orchestration boundary to CQRS actions
