---
name: dknet-messaging-events
description: Explains how this template wires SlimMessageBus as its command/query/event backbone, how the three domain-event raise styles ([RaisesEvent], AddEvent<TEvent>(), AddEvent(instance)) reach a subscriber, and how to forward an event to an external Azure Service Bus topic. Use whenever adding a domain event, wiring an internal or external event consumer, or reasoning about how a command/query travels from an endpoint to its handler.
---

# DKNet messaging and events (SlimMessageBus)

Every command, query, and domain event in this template travels through `SlimMessageBus`'s
`IMessageBus`, not a MediatR `IMediator`. There is no `IMediator` anywhere in the solution.

## The MediatR mapping

If you know MediatR, the shapes map directly onto `DKNet.SlimBus.Extensions`' `Fluents` contracts:

| MediatR | This template | Example |
|---|---|---|
| `IRequest` (no response) | `Fluents.Requests.INoResponse` + `Fluents.Requests.IHandler<TRequest>` returning `Task<IResultBase>` | a delete/cancel command |
| `IRequest<TResponse>` | `Fluents.Requests.IWitResponse<TDto>` + `Fluents.Requests.IHandler<TRequest,TDto>` returning `Task<IResult<TDto>>` | `CreatePurchaseOrderRequest` |
| `IRequest<TResponse>` (read) | `Fluents.Queries.IWitResponse<TDto>` + `Fluents.Queries.IHandler<TRequest,TDto>` returning `Task<TDto?>` | `GetPurchaseOrderByIdQuery` |
| `IRequest<PagedList<T>>` | `Fluents.Queries.IWitPageResponse<TDto>` + `Fluents.Queries.IPageHandler<TRequest,TDto>` returning `Task<IPagedList<TDto>>` (`X.PagedList`) | `ListPurchaseOrdersQuery` |
| `INotification` | a plain `sealed record`, queued via `entity.AddEvent(...)` or composed by `[RaisesEvent]` | `PurchaseOrderCreatedEvent` |
| `INotificationHandler<T>` | `Fluents.EventsConsumers.IHandler<TEvent>` | `PurchaseOrderCreatedEventHandler` |

A handler's method is always `OnHandle`, never `Handle`. Endpoints never call a handler directly —
they resolve `IMessageBus` and call `bus.Send(request, cancellationToken: ct)` for commands/queries.
Only `EventPublisher` (below) calls `bus.Publish(...)`; application code never publishes an event by
hand.

Handlers never call `SaveChanges`. `AddSlimBusEfCoreInterceptor<CoreDbContext>()` (wired in
`AddServiceBus`, see below) saves automatically after a write handler's `OnHandle` returns
successfully.

**Auto-discovery, no per-message registration.** `AutoDeclareFrom(serviceAssembly)` scans the
`<YourApp>.AppServices` assembly and declares every request/handler pair it finds by convention;
`AddServicesFromAssembly(serviceAssembly)` registers the discovered handler classes in DI. Adding a
new `*Request` + `*Handler` pair needs no wiring beyond writing the two classes — see the
`dknet-crud` skill for how requests, validators, and handlers are shaped.

## Wiring: `<YourApp>.Infra/Extensions/ServiceBusSetup.cs`

```csharp
public static IServiceCollection AddServiceBus(
    this IServiceCollection service,
    IConfiguration configuration,
    Assembly serviceAssembly,
    FeatureOptions features)
{
    var busConnectionString = configuration.GetConnectionString(SharedConsts.AzureBusConnectionString)!;

    service.AddSlimBusEfCoreInterceptor<CoreDbContext>()
        .AddSlimMessageBus(mbb =>
        {
            mbb.AddJsonSerializer();          // global serializer for every child bus
            mbb.AddMemoryBus(serviceAssembly);

            if (features.EnableServiceBus && !string.IsNullOrWhiteSpace(busConnectionString))
                mbb.AddAzureBus(busConnectionString);
        });

    return service;
}
```

`AddJsonSerializer()` is a single, global setting shared by every child bus below it — it is not
per-child configuration.

### The `"ImMemory"` child bus — always registered

```csharp
internal static MessageBusBuilder AddMemoryBus(this MessageBusBuilder builder, Assembly serviceAssembly)
{
    builder.AddChildBus("ImMemory", me =>
        me.WithProviderMemory(cf =>
            {
                cf.EnableMessageHeaders = false;
                cf.EnableMessageSerialization = false;
                cf.EnableBlockingPublish = false;
            })
            .AutoDeclareFrom(serviceAssembly)
            .AddServicesFromAssembly(serviceAssembly));
    return builder;
}
```

This is the MediatR-like dispatcher every command, query, and domain event in the solution runs
through, regardless of any feature flag. `EnableMessageHeaders = false` and
`EnableMessageSerialization = false` mean the message object is passed by reference in-process, no
header envelope or JSON round-trip. `EnableBlockingPublish = false` means `bus.Publish(...)` for a
domain event does not wait for every subscriber to finish before returning — a slow or hung internal
consumer does not block the HTTP response.

## Domain events: three raise styles, same publisher

All are covered in full in the `dknet-entity` and `dknet-ddd-principles` skills; here only what
matters for messaging. Prefer them in this order — `[RaisesEvent]`, then `AddEvent<TEvent>()`, then
`AddEvent(instance)`.

- **Manual, instance** — `PurchaseOrder`'s constructor calls
  `AddEvent(new PurchaseOrderCreatedEvent(...))` by hand; the record is a plain hand-written type
  next to the entity. Last resort: use it only when the payload is not a projection of the entity.
- **Manual, type-only** — `AddEvent<TEvent>()` queues the event *type*; the publisher maps the
  entity onto it via `IMapper` when the save succeeds, so there is no hand-written payload. It
  **requires an `IMapper` registration** — without one the publisher throws `EventException` instead
  of dropping the event. Use it when the decision to raise needs real logic but the payload does
  not.
- **Declared** — `Product` carries `[RaisesEvent(EventOperations.Created, Include = [...])]` and
  `[RaisesEvent(EventOperations.Updated, nameof(Price))]`; DKNet's EF Core save hook raises the event
  itself after a successful save. Composed names fold the narrowing property in:
  `[RaisesEvent(EventOperations.Updated, nameof(Price))]` on `Product` generates
  `ProductPriceUpdatedEvent`, not `ProductUpdatedEvent`. An `Updated` rule only fires when that
  property's value actually changed on that save — calling `ChangePrice` with the price it already
  holds raises nothing.

Either way, the entity only **queues** the event. `<YourApp>.Infra/Services/EventPublisher.cs` is what
actually calls the bus:

```csharp
internal sealed class EventPublisher(IMessageBus bus) : DefaultEventPublisher
{
    public override async Task PublishAsync(object eventObj, CancellationToken cancellationToken = default)
    {
        await bus.Publish(eventObj, cancellationToken: cancellationToken);
    }
}
```

`DefaultEventPublisher` drains the queued events only **after** `SaveChangesAsync` succeeds — a
subscriber never sees an event for a write that got rolled back. The reverse also holds: **a
subscriber failure does not roll back the write that raised it.** `SaveChanges` has already
committed by the time any consumer runs. If a rule must be able to fail the request, put that check
in the request validator or the domain method, never in an event handler.

Ordering follows registration/declaration order on the entity; nothing in this template depends on
a specific order across multiple handlers of the same event, and multiple consumers per event are
allowed (both an internal and an external consumer subscribe to the same `ProductCreatedEvent`, see
below).

**Consumers are always hand-written.** Neither `[RaisesEvent]` nor either `AddEvent` overload
generates a consumer — only the raise side is automatic for the declared style.

Internal consumers live in `<YourApp>.AppServices/<Feature>/V1/Events/`:

```csharp
// <YourApp>.AppServices/ManualSample/V1/Events/PurchaseOrderCreatedEventHandler.cs
internal sealed class PurchaseOrderCreatedEventHandler(ILogger<PurchaseOrderCreatedEventHandler> logger)
    : Fluents.EventsConsumers.IHandler<PurchaseOrderCreatedEvent>
{
    public Task OnHandle(PurchaseOrderCreatedEvent notification, CancellationToken cancellationToken)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "PurchaseOrderCreatedEvent received for purchase order {PurchaseOrderId} ({CustomerName}, {Amount}).",
                notification.Id, notification.CustomerName, notification.Amount);
        }
        return Task.CompletedTask;
    }
}
```

```csharp
// <YourApp>.AppServices/AutomatedSample/V1/Events/ProductEventHandlers.cs
internal sealed class ProductCreatedEventHandler(ILogger<ProductCreatedEventHandler> logger)
    : Fluents.EventsConsumers.IHandler<ProductCreatedEvent>
{
    public Task OnHandle(ProductCreatedEvent notification, CancellationToken cancellationToken)
    {
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("AutomatedSample product created: {ProductId}", notification.Id);
        return Task.CompletedTask;
    }
}
```

## External events — Azure Service Bus

A second child bus, `"AzureBus"`, is added only when **both** conditions hold:

| Condition | Where |
|---|---|
| `FeatureManagement:EnableServiceBus` is `true` | `<YourApp>.Share/Options/FeatureOptions.cs` |
| `ConnectionStrings:AzureBus` is a non-empty connection string | checked in `AddServiceBus` |

```csharp
private static MessageBusBuilder AddAzureBus(this MessageBusBuilder builder, string connectionString)
{
    builder.AddChildBus("AzureBus", azb =>
    {
        azb.AddServicesFromAssembly(typeof(InfraSetup).Assembly)
            .WithProviderServiceBus(st =>
            {
                st.ConnectionString = connectionString;
                st.ClientFactory = (_, settings) => new ServiceBusClient(
                    settings.ConnectionString,
                    new ServiceBusClientOptions { TransportType = ServiceBusTransportType.AmqpWebSockets });

                st.TopologyProvisioning = new ServiceBusTopologySettings
                {
                    Enabled = false,
                    CanProducerCreateTopic = true,
                    CanProducerCreateQueue = true,
                    CanConsumerCreateSubscription = true,
                    CanConsumerCreateQueue = true,
                    CreateSubscriptionOptions = op =>
                    {
                        op.EnableBatchedOperations = true;
                        op.MaxDeliveryCount = 10;
                        op.AutoDeleteOnIdle = TimeSpan.FromDays(60);
                        op.DeadLetteringOnMessageExpiration = true;
                        op.DefaultMessageTimeToLive = TimeSpan.FromDays(7);
                    }
                };
            });

        azb.Produce<ProductCreatedEvent>(o => o.DefaultTopic("product-tp"));
        azb.Consume<ProductCreatedEvent>(o => o.Path("product-tp")
            .SubscriptionName("product-sub")
            .WithConsumer<ProductCreatedNotificationHandler>());
    });
    return builder;
}
```

Connects over AMQP-over-WebSockets, which works through most corporate proxies that block raw AMQP.

`TopologyProvisioning.Enabled = false` means the template does **not** create the topic or
subscription for you — provision `product-tp` and `product-sub` yourself (Bicep, Pulumi, or the
portal) before running against a real namespace. The `Can*Create*` flags and
`CreateSubscriptionOptions` (including `MaxDeliveryCount = 10` and `DeadLetteringOnMessageExpiration`)
only apply when `Enabled` is flipped to `true`. With the shipped `Enabled = false`, those values are
documentation of intent, not enforced configuration — whoever provisions the real subscription must
set them to match by hand.

The same event type — `ProductCreatedEvent` — flows on both buses. There is no separate "external"
event record. The `Produce`/`Consume` declaration in `AddAzureBus` is what forwards an
already-declared internal event externally; nothing about the event itself changes.

External consumers live in `<YourApp>.Infra/Features/<Feature>/ExternalEvents/`, are `internal
sealed`, and are discovered by the same `AddServicesFromAssembly(typeof(InfraSetup).Assembly)` call
inside `AddAzureBus` — no separate registration:

```csharp
// <YourApp>.Infra/Features/AutomatedSample/ExternalEvents/ProductCreatedNotificationHandler.cs
internal sealed class ProductCreatedNotificationHandler(ILogger<ProductCreatedNotificationHandler> logger)
    : Fluents.EventsConsumers.IHandler<ProductCreatedEvent>
{
    public Task OnHandle(ProductCreatedEvent notification, CancellationToken cancellationToken)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "External broker received product-created event for {ProductId}", notification.Id);
        }
        return Task.CompletedTask;
    }
}
```

### Recipe: forward an internal domain event externally

1. In `AddAzureBus`, next to the `ProductCreatedEvent` lines, add:
   ```csharp
   azb.Produce<TEvent>(o => o.DefaultTopic("<topic-name>"));
   azb.Consume<TEvent>(o => o.Path("<topic-name>")
       .SubscriptionName("<subscription-name>")
       .WithConsumer<THandler>());
   ```
2. Write `THandler` as a `Fluents.EventsConsumers.IHandler<TEvent>` under
   `<YourApp>.Infra/Features/<Feature>/ExternalEvents/`. External-system concerns belong in `Infra`,
   never `AppServices`.
3. Nothing else — `azb.AddServicesFromAssembly(typeof(InfraSetup).Assembly)` already picks up the
   new handler by assembly scan.

### Recipe: consume an event another service publishes

Only the `Consume` half is needed — this service produces nothing for that event:

```csharp
azb.Consume<TExternalEvent>(o => o.Path("<their-topic-name>")
    .SubscriptionName("<your-subscription-name>")
    .WithConsumer<THandler>());
```

`THandler` still goes in `<YourApp>.Infra/Features/<Feature>/ExternalEvents/` and still needs no
manual DI registration. Do not add a matching `azb.Produce<TExternalEvent>(...)` — that would make
this service claim ownership of an event type it does not raise.

## What `EnableServiceBus` switches off

It gates the **Azure Service Bus child bus only**. The in-memory child bus is always registered
regardless of the flag — a service with `EnableServiceBus` off still handles every request and still
raises domain events in-process. Turning it off only stops this service producing to and consuming
from Azure Service Bus: `ProductCreatedEvent` is still published in-memory and handled by
`ProductCreatedEventHandler`, but it is never produced to `product-tp`, and
`ProductCreatedNotificationHandler` never fires.

## Local development

`<YourApp>.AppHost/AppHost.cs` (Aspire orchestration) wires only Redis and PostgreSQL today:

```csharp
var cache = builder.AddRedis("Redis");
var postgres = builder.AddPostgres("Postgres");
...
builder.AddProject("Api", "../<YourApp>.Api/<YourApp>.Api.csproj")
    .WithReference(cache, "Redis")
    .WithReference(apDb, "AppDb")
    //.WaitFor(bus)
    .WaitFor(cache)
    .WaitFor(apDb);
```

The `.WaitFor(bus)` line is commented out and no `bus` resource is added above it — no Azure Service
Bus emulator is wired into `AppHost.cs` as shipped. `<YourApp>.AppHost/Configs/busConfig.json` exists
and is copied to the build output, but nothing in `AppHost.cs` references it — it's a config file
waiting for an emulator resource, not something a consumer touches to run the app today.

DKNet also carries an `Aspire.Hosting.ServiceBus` project that runs the emulator locally, but it is
**not published to NuGet** — usable only via a project reference to a local DKNet clone, not
`dotnet add package`.

## Testing events

**BDD — log-capture pattern.** `<YourApp>.App.TestSupport/TestLogCapture.cs` is an `ILoggerProvider`
that queues every formatted log line into an in-memory collection, registered as an additional
provider alongside the host's normal logging. A scenario asserts on the resulting text instead of on
internal call order:

```gherkin
Scenario: Creating a purchase order raises PurchaseOrderCreatedEvent
  When I create a purchase order for customer "Acme Pte Ltd" with amount 250.00
  Then a log line reports the purchase order created event was received
```

```csharp
[Then("a log line reports the purchase order created event was received")]
public void ThenALogLineReportsThePurchaseOrderCreatedEventWasReceived() =>
    factory.LogCapture.Messages.ShouldContain(m => m.Contains("PurchaseOrderCreatedEvent received", StringComparison.Ordinal));
```

The automated sample proves the same shape for the declared-event style:

```gherkin
Scenario: Creating a product raises the internal ProductCreatedEvent
  When I create a product named "Widget" with price 9.99
  Then a log line reports the automated sample product was created
```

**xUnit — a unit test for an external handler.** Because the test hosts never set
`ConnectionStrings:AzureBus`, nothing in either suite ever routes a message onto the `AzureBus` child
bus, so `ProductCreatedNotificationHandler` is invoked directly instead:

```csharp
[Fact]
public async Task OnHandle_ShouldLogTheExternalBrokerReceipt()
{
    var logCapture = new TestLogCapture();
    using var loggerFactory = LoggerFactory.Create(b => b.AddProvider(logCapture));
    var handler = new ProductCreatedNotificationHandler(loggerFactory.CreateLogger<ProductCreatedNotificationHandler>());
    var productId = Guid.NewGuid();
    var notification = new ProductCreatedEvent { Id = productId, Name = "Widget", Price = 9.99m };

    await handler.OnHandle(notification, CancellationToken.None);

    logCapture.Messages.ShouldContain(m => m.Contains(productId.ToString(), StringComparison.Ordinal));
}
```

**The honest gap.** The full `Produce → topic → Consume` path against a real or emulated Azure
Service Bus namespace is not exercised by either shipped suite. `ProductCreatedNotificationHandlerTests`
proves the handler's own behavior; it does not prove a message actually crosses the broker. Treat
that path as untested until you add integration coverage against a real namespace or an emulator.

## Handler failure and retry — only what the code shows

- **In-memory bus:** no retry policy anywhere in `ServiceBusSetup.cs`. An exception from an internal
  `OnHandle` is not retried; it propagates like any other in-process exception.
- **Azure bus:** `MaxDeliveryCount = 10` and `DeadLetteringOnMessageExpiration = true` are the values
  `CreateSubscriptionOptions` sets, but (as above) they only take effect through SlimMessageBus's own
  provisioning, or if you set them by hand when provisioning `product-sub` yourself.

## Common mistakes

- **What you might expect:** publishing a domain event directly from a command handler.
  **What actually happens:** only `EventPublisher`, called by DKNet's save-hook after a successful
  `SaveChanges`, ever calls `bus.Publish(...)`. A handler that calls `bus.Publish` itself bypasses the
  "only after a committed write" guarantee.
- **What you might expect:** an `[RaisesEvent(EventOperations.Updated, ...)]` fires on every call to
  the method that touches that property. **What actually happens:** it only fires when the value
  actually changed on that save — see `dknet-entity` for the mechanics.
- **What you might expect:** placing a new event consumer in `<YourApp>.Api` gets it discovered like
  the others. **What actually happens:** discovery only scans the `<YourApp>.AppServices` assembly
  (internal) and the `<YourApp>.Infra` assembly (external, inside `AddAzureBus`). A consumer in
  `<YourApp>.Api` is never registered.
- **What you might expect:** setting `EnableServiceBus: true` is enough to start producing to Azure.
  **What actually happens:** `ConnectionStrings:AzureBus` must also be a non-empty string. Either one
  missing and the `AzureBus` child bus, and everything registered only on it, silently does not exist
  — no error, no log, just no external traffic.
- **What you might expect:** an external consumer belongs next to the internal one, in
  `<YourApp>.AppServices/<Feature>/V1/Events/`. **What actually happens:** external-system consumers
  belong in `<YourApp>.Infra/Features/<Feature>/ExternalEvents/` — that is the assembly `AddAzureBus`
  scans, and it keeps the external-system dependency out of `AppServices`.
