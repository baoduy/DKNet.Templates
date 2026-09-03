# SlimMessageBus Messaging

This page covers how the template wires [SlimMessageBus](https://github.com/zarusz/SlimMessageBus) as
its command/query/event backbone, and how to forward a domain event to an external broker. For the
full `DKNet.SlimBus.Extensions` API surface — request/response contracts, `LazyMapper`, and more —
see DKNet's own `docs/Messaging/DKNet.SlimBus.Extensions.md`; this page only covers how the template
wires and uses it.

## The in-memory bus — the MediatR alternative

Every request, query, and domain event in this template travels through `IMessageBus`, not a MediatR
`IMediator`. Wiring lives in `Minimal.Infra/Extensions/ServiceBusSetup.cs`:

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
            mbb.AddJsonSerializer();
            mbb.AddMemoryBus(serviceAssembly);

            if (features.EnableServiceBus && !string.IsNullOrWhiteSpace(busConnectionString))
                mbb.AddAzureBus(busConnectionString);
        });
    return service;
}
```

`AddMemoryBus` always adds an `"ImMemory"` child bus:

```csharp
me.WithProviderMemory(cf => { cf.EnableMessageHeaders = false; cf.EnableMessageSerialization = false; cf.EnableBlockingPublish = false; })
  .AutoDeclareFrom(serviceAssembly)
  .AddServicesFromAssembly(serviceAssembly);
```

`AutoDeclareFrom` scans `serviceAssembly` — the `Minimal.AppServices` assembly — and declares every
request/handler pair it finds by convention. No per-message `.Produce<T>()`/`.Consume<T>()`
registration is needed for internal traffic. `AddServicesFromAssembly` registers the discovered
handler classes in DI.

Endpoints never call a handler directly. They resolve `IMessageBus` and call `bus.Send(...)`, as in
`Minimal.Api/ApiEndpoints/ManualSample/PurchaseOrderV1Endpoint.cs`:

```csharp
group.MapGet("{id:guid}", async (Guid id, IMessageBus bus, CancellationToken ct) =>
{
    var dto = await bus.Send(new GetPurchaseOrderByIdQuery { Id = id }, cancellationToken: ct);
    return dto is null ? Results.NotFound() : Results.Ok(dto);
});
```

### Mapping MediatR concepts onto DKNet's `Fluents` contracts

If you're coming from MediatR, the shapes map directly:

| MediatR | This template (`DKNet.SlimBus.Extensions`) | Example |
|---|---|---|
| `IRequest<TResponse>` | `Fluents.Queries.IWitResponse<TResponse>` | `GetPurchaseOrderByIdQuery` |
| `IRequest<PagedList<T>>` | `Fluents.Queries.IWitPageResponse<TDto>` | `ListPurchaseOrdersQuery` |
| `IRequestHandler<TRequest, TResponse>` | `Fluents.Queries.IHandler<TRequest, TResponse>` | `GetPurchaseOrderByIdQueryHandler` |
| `IRequestHandler<TRequest, PagedList<T>>` | `Fluents.Queries.IPageHandler<TRequest, TDto>` | `ListPurchaseOrdersQueryHandler` |
| `INotification` | plain `sealed record`, queued via `entity.AddEvent(...)` | `PurchaseOrderCreatedEvent` |
| `INotificationHandler<TNotification>` | `Fluents.EventsConsumers.IHandler<TEvent>` | `PurchaseOrderCreatedEventHandler` |

A handler's method is `OnHandle(TRequest request, CancellationToken cancellationToken)`, not
`Handle`. It returns the response type, or `Task` for event handlers. Write and command requests
instead return a `Result` — see `docs/ddd-implementation-guide.md` §4 — which the endpoint turns into
an HTTP response via `result.Response()`.

Domain events queued with `entity.AddEvent(...)` are not published by the handler.
`Minimal.Infra/Services/EventPublisher.cs` drains the queue and calls `IMessageBus.Publish(...)` after
`SaveChangesAsync` succeeds, so a subscriber only ever sees an event for a change that was actually
persisted.

### Request validation

Requests such as `ListPurchaseOrdersQuery` carry a co-located `AbstractValidator<TRequest>` —
`ListPurchaseOrdersQueryValidator`, in the same file. These validators are registered in DI via
`builder.Services.AddValidatorsFromAssembly(typeof(AppSetup).Assembly, includeInternalTypes: true)` in
`Minimal.Api/Configs/FluentValidationConfig.cs`, and enforced by SharpGrip's
`AddFluentValidationAutoValidation()` at the ASP.NET Core endpoint layer. Validation runs on the bound
request **before** it ever reaches `bus.Send(...)`; it is not a SlimMessageBus interceptor.
`Minimal.Infra` references the `SlimMessageBus.Host.FluentValidation` package, but nothing in
`ServiceBusSetup.cs` wires it into the bus pipeline. Don't assume a validator runs a second time on
the bus side.

### `LazyMapper` projection

Mapping an entity to its DTO after a write goes through `DKNet.SlimBus.Extensions.LazyMapper`'s
`ResultOf<TDto>(entity)`. This is already covered in `docs/ddd-implementation-guide.md` §7 — see that
section for when to use it over a plain `mapper.Map<TDto>(entity)`.

## External forwarding — Azure Service Bus

A second child bus, `"AzureBus"`, is added only when **both** `FeatureManagement:EnableServiceBus`
is `true` **and** `ConnectionStrings:AzureBus` is a non-empty connection string. `AddServiceBus`
checks both in the same condition, shown above; either one missing and no `AzureBus` child bus is
registered:

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

This connects over AMQP-over-WebSockets, which is firewall-friendly and works through most corporate
proxies that block raw AMQP. `TopologyProvisioning.Enabled = false` means the template does **not**
create the topic or subscription for you. Provision `product-tp` and `product-sub` yourself —
Bicep, Pulumi, or the portal — before running against a real namespace. The other `Can*Create*` flags
and `CreateSubscriptionOptions` only take effect if you flip `Enabled` to `true`.

### The recipe: forward an internal domain event externally

`ProductCreatedEvent` is raised internally via `[RaisesEvent(EventOperations.Created, ...)]` on
`Product` — see `docs/ddd-implementation-guide.md` §8. It is consumed on the `AzureBus` child bus by
`Minimal.Infra/Features/AutomatedSample/ExternalEvents/ProductCreatedNotificationHandler.cs`:

```csharp
internal sealed class ProductCreatedNotificationHandler(ILogger<ProductCreatedNotificationHandler> logger)
    : Fluents.EventsConsumers.IHandler<ProductCreatedEvent>
{
    public Task OnHandle(ProductCreatedEvent notification, CancellationToken cancellationToken)
    {
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("External broker received product-created event for {ProductId}", notification.Id);
        return Task.CompletedTask;
    }
}
```

The same event type is both queued internally by `EventPublisher` and produced and consumed on
`AzureBus`. There is no separate "external" event record; the topology declaration in
`ServiceBusSetup.cs` is what actually forwards it. To forward a second event:

1. Add `azb.Produce<TEvent>(o => o.DefaultTopic("<topic-name>"))` and
   `azb.Consume<TEvent>(o => o.Path("<topic-name>").SubscriptionName("<subscription-name>").WithConsumer<THandler>())`
   next to the `ProductCreatedEvent` lines in `AddAzureBus`.
2. Write `THandler` as a `Fluents.EventsConsumers.IHandler<TEvent>` under
   `Minimal.Infra/Features/<Feature>/ExternalEvents/`. External-system concerns belong in `Infra`, not
   `AppServices` — see `docs/ddd-implementation-guide.md` §8.
3. No DI registration needed — `azb.AddServicesFromAssembly(typeof(InfraSetup).Assembly)` picks up the
   new handler by assembly scan.

## What `FeatureManagement:EnableServiceBus` switches off

`EnableServiceBus` gates the **Azure Service Bus child bus only**. The in-memory child bus — the
MediatR-like dispatcher every command, query, and domain event in the solution runs through — is
always registered by `ServiceBusSetup.AddServiceBus`, regardless of the flag. Turning
`EnableServiceBus` off does not stop internal dispatch; a service with the flag off still handles
requests and still raises domain events in-process.

The Azure child bus is added only when both conditions hold:

| Condition | Where |
|---|---|
| `FeatureManagement:EnableServiceBus` is `true` | `Minimal.Share/Options/FeatureOptions.cs` |
| `ConnectionStrings:AzureBus` is non-empty | `Minimal.Infra/Extensions/ServiceBusSetup.cs` |

Either one missing and no `AzureBus` child bus is registered: `ProductCreatedEvent` is still
published on the in-memory bus and handled by `ProductCreatedEventHandler`, but it is never produced
to the `product-tp` topic and `ProductCreatedNotificationHandler` never fires.

So the flag is a real kill switch for external messaging — flip it to `false` to stop a service
producing to and consuming from Azure Service Bus while leaving the rest of the application working.
It is not a switch for internal message handling.
