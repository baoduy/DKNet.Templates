# Purchase Orders — Integration

Part of [Purchase Orders](index.md).

## 📣 Events

| Event | Raised when | Payload | Transport | Consumers | Ordering | Duplicates | On failure |
|---|---|---|---|---|---|---|---|
| `PurchaseOrderCreatedEvent` | a `PurchaseOrder` is constructed through `POST /` (never on the internal seeding constructor) | `Id`, `CustomerName`, `Amount` | in-memory only — the `ImMemory` SlimMessageBus child bus | `PurchaseOrderCreatedEventHandler` (logs receipt) | none needed: one in-process consumer, delivered after the row commits | none: the handler only logs, so redelivery has no observable side effect | not retried: the handler never throws, so this sample has no dead-letter or replay path to document |

`PurchaseOrder`'s constructor calls `AddEvent(new PurchaseOrderCreatedEvent(...))` itself — this
sample raises its event by hand, unlike [Automated Products](../automated-products/index.md)'s
`[RaisesEvent]`-declared events. `EventPublisher` publishes whatever the aggregate queued only after
`SaveChanges` succeeds, so a rolled-back create never reaches the consumer.

This feature carries no external broker publish or subscribe — its event stays on the in-memory bus
only, a deliberate scope split from the automated sample, which also wires the same kind of event to
an Azure Service Bus topic. There is accordingly no 🌐 Downstream systems section here: nothing in
`ManualSample` calls another system, is called by one, or exchanges events with one.
