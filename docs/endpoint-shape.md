# The shape your endpoints should take

**Composite-first.** One endpoint class per aggregate: declare the group's authorization scopes on the
class, map the generated CRUD composite at the top, then hand-write below it only the routes that
carry real orchestration. Generated and hand-written routes live in the same group — this is not a
choice between two styles of endpoint.

`ProductV1Endpoint` (`Minimal.Api/ApiEndpoints/AutomatedSample/ProductV1Endpoint.cs`) is that shape,
trimmed here to its structure:

```csharp
[EndpointGroupScope(ProductScopes.Read, EndpointHttpMethods.Get)]
[EndpointGroupScope(ProductScopes.Write, EndpointHttpMethods.Post, EndpointHttpMethods.Put,
    EndpointHttpMethods.Delete)]
internal sealed class ProductV1Endpoint : IEndpointConfig
{
    public void Map(RouteGroupBuilder group)
    {
        group.MapProductCrud(o =>
        {
            o.Exclude("Discontinue");                                        // hand-written below

            // a PUT like Update, so the per-method declaration cannot separate them — holding
            // products.write must not be enough to assign a supplier reference
            if (requireAuthorization)
                o.Configure("AssignSupplierReference", rb => rb.RequireAuthorization(ProductScopes.Supplier));
        });

        // writes two aggregates in one transaction — outside what the generator can express
        group.MapPut("{id:guid}/discontinue", /* ... */).Produces<ProductDto>();

        // a response shape the generator has none for; the group's GET declaration already covers it
        group.MapGet("summary", /* ... */).Produces<ProductPriceSummaryDto>();
    }
}
```

Seven of the nine routes come from `MapProductCrud`. `[EndpointGroupScope]` names the scope per HTTP
method once and covers generated and hand-mapped routes alike; a per-route `Configure`/
`RequireAuthorization` is needed only where two routes share an HTTP method but not a scope. Scopes
are applied only when `FeatureManagement:RequireAuthorization` is on, so a stock Development run
enforces none of them — the attribute checks that flag itself, which is why the per-route overrides
are the only calls that need an explicit guard.

## When an operation needs a hand-written route

The criterion is stated once, in
[Manual vs. Automated — At a glance](samples/manual-vs-automated.md#at-a-glance-which-one-should-i-copy),
and this page does not carry a second version of it. Read it before you hand-write a handler: the
list is shorter than most readers expect, and two of the cases that look like they belong on it do
not.

- **A rule that refuses an operation after reading stored data does not force a hand-written route.**
  A FluentValidation validator written against the *generated* request runs on the generated route —
  the group-level `AddFluentValidationAutoValidation()` filter applies to every route in the group.
  `CreateProductRequestValidator` refuses a name already taken and `DeleteProductRequestValidator`
  refuses a product still for sale, both with `409`, and neither operation has a hand-written route,
  request or handler.
- **A response value the generator's naming convention cannot produce does not force one either.**
  `ProductDto.GrossMargin` (`Price` minus `SupplierCostPrice`) is derived from two columns, so the
  convention cannot emit it — one property on the generated `partial record` plus a Mapster
  `IRegister` puts it on the response, with no route, request or handler written for it. See
  [a response value the convention cannot produce](features/automated-products/endpoints.md#a-response-value-the-convention-cannot-produce).

Both samples ship in full: [Products](features/automated-products/index.md)
(generator-driven `Product`) and
[Purchase Orders](features/manual-purchase-orders/index.md) (hand-written
`PurchaseOrder`). The end-to-end steps for a feature of your own are in
the [DDD implementation guide](ddd-implementation-guide.md).
