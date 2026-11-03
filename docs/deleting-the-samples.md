# Deleting the samples

A generated solution ships two worked samples — [Products](features/automated-products/index.md)
(generator-driven) and [Purchase Orders](features/manual-purchase-orders/index.md) (hand-written).
Neither depends on the other, and no other feature depends on either, so each can be removed on its
own once you have copied what you need from it.

## Products (`AutomatedSample`)

Remove `src/ApiEndpoints/**/AutomatedSample/**`, `src/ApiEndpoints/Minimal.Api/ApiEndpoints/AutomatedSample/`,
the two `Produce<ProductCreatedEvent>`/`Consume<ProductCreatedEvent>` lines in
`Minimal.Infra/Extensions/ServiceBusSetup.cs`, and the `docs/features/automated-products/` folder.
Then drop its EF Core mapping from the next migration. No other feature depends on it.

## Purchase Orders (`ManualSample`)

Remove `src/ApiEndpoints/**/ManualSample/**`, `src/ApiEndpoints/Minimal.Api/ApiEndpoints/ManualSample/`,
and the `docs/features/manual-purchase-orders/` folder. Then drop its EF Core mapping and seed classes
from the next migration. No other feature depends on it.
