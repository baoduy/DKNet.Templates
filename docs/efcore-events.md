# EF Core Domain Events

How this template wires `DKNet.EfCore.Events`. Events are collected on the entity during a unit of
work and dispatched only after `SaveChanges` succeeds, so a handler never runs against a write that
gets rolled back. For the package's own API surface — the `AddEvent`/`IEventPublisher` contracts,
the `RaisesEventAttribute` shape, the `EventOperations` enum — see `DKNet.EfCore.Events` in the main
`DKNet` repo. This page covers only how the template's own code uses it.

The interceptor that turns tracked entity changes into published events is registered once, for
every entity on `CoreDbContext`, in `AddServiceBus`:

```csharp
// Minimal.Infra/Extensions/ServiceBusSetup.cs
service.AddSlimBusEfCoreInterceptor<CoreDbContext>()
    .AddSlimMessageBus(mbb => { ... });
```

Both samples publish through the same interceptor and the same
`Minimal.Infra/Services/EventPublisher.cs`. The only difference between them is *how* the event
gets raised.

## Manual style — raise it yourself

`PurchaseOrder` (`Minimal.Domains/Features/ManualSample/Entities/PurchaseOrder.cs`) calls `AddEvent`
by hand, inside the constructor, right where the aggregate becomes valid:

```csharp
public PurchaseOrder(string customerName, decimal amount, string byUser) : base(byUser)
{
    CustomerName = customerName;
    Amount = amount;
    Status = PurchaseOrderStatus.Placed;

    AddEvent(new PurchaseOrderCreatedEvent(Id, CustomerName, Amount));
}
```

The event itself is a plain hand-written record next to the entity —
`Minimal.Domains/Features/ManualSample/Entities/PurchaseOrderCreatedEvent.cs`:

```csharp
public sealed record PurchaseOrderCreatedEvent(Guid Id, string CustomerName, decimal Amount);
```

The handler is hand-written too —
`Minimal.AppServices/ManualSample/V1/Events/PurchaseOrderCreatedEventHandler.cs`, an
`IHandler<PurchaseOrderCreatedEvent>` that logs at `Information`. Nothing about the manual style is
generated: you write the raise call, the payload shape, and the consumer.

## Attribute style — declare it

`Product` (`Minimal.Domains/Features/AutomatedSample/Entities/Product.cs`) never calls `AddEvent`.
Instead the class carries:

```csharp
[RaisesEvent(EventOperations.Created, Include = [nameof(Id), nameof(Name), nameof(Price)])]
[RaisesEvent(EventOperations.Updated, nameof(Price))]
public class Product : AggregateRoot
```

DKNet's EF Core save hook reads these declarations off the change tracker after a successful save
and raises the events itself. No code in `Product`, or anywhere in `AutomatedSample/`, calls
`AddEvent`. Each attribute composes its own payload record type at compile time; neither type has a
hand-written source file:

- `[RaisesEvent(EventOperations.Created, Include = [...])]` → `ProductCreatedEvent` with exactly the
  included properties (`Id`, `Name`, `Price`).
- `[RaisesEvent(EventOperations.Updated, nameof(Price))]` → `ProductPriceUpdatedEvent`. The naming
  convention folds the narrowing property into the name — it is **not** `ProductUpdatedEvent`.

**What you might expect:** calling `ChangePrice` raises `ProductPriceUpdatedEvent` on every call.

**What actually happens:** the `Updated` rule only fires when `Price` actually changed on that save.
Calling `ChangePrice` with the value the entity already holds does not raise the event.

**Why:** the save hook compares the property's current value against the change tracker's original
value, not whether the setter was called.

The handler is still hand-written —
`Minimal.AppServices/AutomatedSample/V1/Events/ProductEventHandlers.cs`'s
`ProductCreatedEventHandler`. The attribute generator's job stops at declaring and raising; it never
generates a consumer.

## Manual vs. attribute — which to reach for

| | Manual (`AddEvent`) | Attribute (`[RaisesEvent]`) |
|---|---|---|
| Payload shape | Whatever fields you put in the record | Fixed to `Include`'d / named properties; no extra field without switching to a `[GenerateDto]`-backed type name |
| Raise condition | Whatever your code checks before calling `AddEvent` | `Created` always; `Updated` only when the named property's value actually changed on that save |
| Debuggability | Step through the constructor/method — the `AddEvent` call is right there | Raised inside DKNet's save hook, not your code — you can't breakpoint the "decision to raise" |
| Pick it when | The event needs a condition beyond "this property changed", a payload field the entity doesn't expose as a property, or you want the raise call visible in the method you're already reading | The entity is a plain CRUD shape and the built-in `Created`/`Updated`(-on-property-change) semantics are exactly what you need |

## Ordering and transaction guarantee

Events dispatch **after** `SaveChanges` completes successfully, for both styles. A handler failure
does not roll back the write that raised it. If a handler needs to fail the request, that check
belongs in the request validator or the domain method instead of in the event handler.
</content>
</invoke>
