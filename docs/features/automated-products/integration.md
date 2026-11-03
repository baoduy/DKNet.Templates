# Automated Products — Integration

Part of [Automated Products](index.md).

## 📣 Events

`Product` declares three events by attribute — `[RaisesEvent]` only declares and raises; nothing in this feature calls `AddEvent` by hand. DKNet's EF Core save hook reads the declarations and raises each event after a successful `SaveChanges`, driven by change-tracker state.

| Event | Raised when | Payload | Transport | Consumers | Ordering | Duplicates | On failure |
|---|---|---|---|---|---|---|---|
| `ProductCreatedEvent` | A product is inserted — including the replacement `Discontinue()` creates | `Id`, `Name`, `Price` (narrowed by `Include = [...]` on the attribute) | In-memory bus (always) + Azure Service Bus topic `product-tp` (only when `EnableServiceBus` is true) | `ProductCreatedEventHandler` (in-memory, logs only) and, on the Azure child bus, `ProductCreatedNotificationHandler` (logs only) | None | None observed — both handlers only log; neither is idempotent-by-design because neither writes state | See below |
| `ProductPriceUpdatedEvent` | `ChangePrice` saves | Every audited property (`Id`, `Name`, `Price`, `IsDiscontinued`, `OwnedBy`, `CreatedBy`, `CreatedOn`, `UpdatedBy`, `UpdatedOn`, `SupplierCostPrice`, `SupplierReferenceCode`, `LastModifiedBy`, `LastModifiedOn`) — the attribute names only `Price` to compose the event's name, not to narrow its payload | In-memory bus only | None — no handler is registered for it in this repository | None | — | — |
| `ProductIsDiscontinuedUpdatedEvent` | `Discontinue()` saves | Same full payload shape as `ProductPriceUpdatedEvent` above | In-memory bus only | None — no handler is registered for it in this repository | None | — | — |

Event type names are composed by convention — `<Entity><NarrowingProps><Operation>Event` — and payload shapes are confirmed against the compiled assembly's XML documentation (`Minimal.Domains.xml`), since none has a hand-written source file.

**On failure.** `AddMemoryBus` sets `EnableBlockingPublish = false`; whether that specifically means a handler exception there is swallowed rather than surfaced to the caller's request is not confirmed from this repository alone — see ❓ Open questions on [index.md](index.md#-open-questions). The handler runs in-process either way. For the Azure path: `ServiceBusSetup.AddAzureBus` sets `TopologyProvisioning.Enabled = false`, so the `product-tp` topic and `product-sub` subscription must already exist — this code does not create them. The `MaxDeliveryCount: 10`, dead-lettering and 7-day TTL settings in `CreateSubscriptionOptions` only take effect on a subscription this code provisions, which, with provisioning off, it does not; whether the pre-existing subscription honors matching settings is outside this repository. An event raised while `EnableServiceBus` is `false` (Development's setting) is never produced to Azure at all — only the in-memory handler runs, and there is no retained or replayed copy for when the flag turns on later. Whether a database commit with no subsequent publish (a crash between the two, with no outbox pattern in this code) can drop an event entirely is tracked as an open question on [index.md](index.md#-open-questions).

## 🌐 Downstream systems

| System | Direction | How | What for | When it is down |
|---|---|---|---|---|
| Azure Service Bus (`product-tp` topic / `product-sub` subscription) | we call it (produce) + it calls us (consume) | `azb.Produce<ProductCreatedEvent>`/`azb.Consume<ProductCreatedEvent>` in `ServiceBusSetup.AddAzureBus`, wired only when `FeatureManagement:EnableServiceBus` is `true` **and** `ConnectionStrings:AzureBus` is non-empty | Notifies an external subscriber that a product was created | Registered, unused in Development: `EnableServiceBus` is `false` in `appsettings.Development.json`, so the Azure child bus is never added and `ProductCreatedNotificationHandler` never runs. In `appsettings.json` (the non-Development default) it is `true`; this repository sets no client-side retry or circuit breaker around the SDK call. Whether `POST /` still answers `201` when the topic is unreachable is open — see ❓ Open questions on [index.md](index.md#-open-questions). |

PostgreSQL (`sample.Products`) is this feature's only other dependency, and it is the request's own data store rather than a downstream integration — see [Data model](data-model.md).

The create-to-event path, including the Azure branch, is drawn in [index.md](index.md#-end-to-end-flow). That diagram cannot show `ProductPriceUpdatedEvent` or `ProductIsDiscontinuedUpdatedEvent`: both are raised the same way on their respective saves, but this repository registers no consumer for either, so there is no second hop to draw.
