# SlimMessageBus Messaging

How this template wires [SlimMessageBus](https://github.com/zarusz/SlimMessageBus) as its command/query/event
backbone, and how to forward a domain event to an external broker. For the full `DKNet.SlimBus.Extensions`
API surface (request/response contracts, `LazyMapper`, etc.), see DKNet's own
`docs/Messaging/DKNet.SlimBus.Extensions.md` — this page only covers how the template wires and uses it.

## The in-memory bus — the MediatR alternative

Every request, query, and domain event in this template travels through `IMessageBus`, not a MediatR
`IMediator`. Wiring lives in `Minimal.Infra/Extensions/ServiceBusSetup.cs`:

```csharp
public static IServiceCollection AddServiceBus(
    this IServiceCollection service, IConfiguration configuration, Assembly serviceAssembly)
{
    service.AddSlimBusEfCoreInterceptor<CoreDbContext>()
        .AddSlimMessageBus(mbb =>
        {
            mbb.AddJsonSerializer();
            mbb.AddMemoryBus(serviceAssembly);
            if (!string.IsNullOrWhiteSpace(busConnectionString))
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

`AutoDeclareFrom` scans `serviceAssembly` (the `Minimal.AppServices` assembly) and declares every request/handler
pair it finds by convention — no per-message `.Produce<T>()`/`.Consume<T>()` registration needed for internal
traffic. `AddServicesFromAssembly` registers the discovered handler classes in DI.

Endpoints never call a handler directly — they resolve `IMessageBus` and call `bus.Send(...)`
(see `Minimal.Api/ApiEndpoints/ManualSample/PurchaseOrderV1Endpoint.cs`):

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

A handler's method is `OnHandle(TRequest request, CancellationToken cancellationToken)`, returning the
response type (or `Task` for event handlers) — not `Handle`. Write/command requests instead return a `Result`
(see `docs/ddd-implementation-guide.md` §4), which the endpoint turns into an HTTP response via
`result.Response()`.

Domain events queued with `entity.AddEvent(...)` are not published by the handler — `Minimal.Infra/Services/EventPublisher.cs`
drains the queue and calls `IMessageBus.Publish(...)` after `SaveChangesAsync` succeeds, so a subscriber only
ever sees an event for a change that was actually persisted.

### Request validation

Requests such as `ListPurchaseOrdersQuery` carry a co-located `AbstractValidator<TRequest>`
(`ListPurchaseOrdersQueryValidator` in the same file). These are registered in DI via
`builder.Services.AddValidatorsFromAssembly(typeof(AppSetup).Assembly, includeInternalTypes: true)`
(`Minimal.Api/Configs/FluentValidationConfig.cs`) and enforced by SharpGrip's
`AddFluentValidationAutoValidation()` at the ASP.NET Core endpoint layer — validation runs on the bound
request **before** it ever reaches `bus.Send(...)`, not as a SlimMessageBus interceptor. `Minimal.Infra`
references the `SlimMessageBus.Host.FluentValidation` package, but nothing in `ServiceBusSetup.cs` wires it
into the bus pipeline — don't assume a validator runs a second time on the bus side.

### `LazyMapper` projection

Mapping an entity to its DTO after a write goes through `DKNet.SlimBus.Extensions.LazyMapper`'s
`ResultOf<TDto>(entity)`, already covered in `docs/ddd-implementation-guide.md` §7 — see that section for
when to use it over a plain `mapper.Map<TDto>(entity)`.

## External forwarding — Azure Service Bus

A second child bus, `"AzureBus"`, is added only when `ConnectionStrings:AzureBus` is a non-empty connection
string (`AddServiceBus` checks this — the `FeatureManagement:EnableServiceBus` flag is not consulted here):

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

Connects over AMQP-over-WebSockets (firewall-friendly, works through most corporate proxies that block raw
AMQP). `TopologyProvisioning.Enabled = false` means the template does **not** create the topic/subscription
for you — provision `product-tp` / `product-sub` yourself (Bicep/Pulumi/portal) before running against a real
namespace; the other `Can*Create*` flags and `CreateSubscriptionOptions` only take effect if you flip
`Enabled` to `true`.

### The recipe: forward an internal domain event externally

`ProductCreatedEvent` (raised internally via `[RaisesEvent(EventOperations.Created, ...)]` on `Product`, see
`docs/ddd-implementation-guide.md` §8) is consumed on the `AzureBus` child bus by
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

The same event type is both queued internally by `EventPublisher` and produced/consumed on `AzureBus` — there
is no separate "external" event record; the topology declaration in `ServiceBusSetup.cs` is what actually
forwards it. To forward a second event:

1. Add `azb.Produce<TEvent>(o => o.DefaultTopic("<topic-name>"))` and
   `azb.Consume<TEvent>(o => o.Path("<topic-name>").SubscriptionName("<subscription-name>").WithConsumer<THandler>())`
   next to the `ProductCreatedEvent` lines in `AddAzureBus`.
2. Write `THandler` as an `Fluents.EventsConsumers.IHandler<TEvent>` under
   `Minimal.Infra/Features/<Feature>/ExternalEvents/` — external-system concerns belong in `Infra`, not
   `AppServices` (see `docs/ddd-implementation-guide.md` §8).
3. No DI registration needed — `azb.AddServicesFromAssembly(typeof(InfraSetup).Assembly)` picks up the new
   handler by assembly scan.

## Config trap: `EnableServiceBus` vs `EnableServiceBusProcess`

As already flagged in `docs/template-features.md`, the generated `appsettings.json` sets
`FeatureManagement:EnableServiceBusProcess`, but `Minimal.Share/Options/FeatureOptions.cs` defines the flag as
`EnableServiceBus`. The drifted key silently no-ops. In practice this rarely matters for the Azure child bus
specifically, since `AddServiceBus` gates it on `ConnectionStrings:AzureBus` being non-empty rather than on
this flag at all — but don't rely on flipping `EnableServiceBusProcess` in `appsettings.json` to mean anything;
verify any service-bus-related flag against `FeatureOptions.cs`.
