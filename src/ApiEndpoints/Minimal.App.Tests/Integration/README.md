# Integration Tests

These tests validate the Profile V1 actions (`Create`, `Update`, `Delete`) from API DI downward.

## Design

- Bootstraps the API host with `WebApplicationFactory<SlimBus.Api.Program>`.
- Uses `ApiFixture` (`IAsyncLifetime`) to manage host lifecycle and DB reset.
- Replaces SQL Server with EF Core in-memory database in test host services.
- Replaces `IMembershipService` with a deterministic test implementation.
- Resolves action handlers from DI and executes them directly.

## Run

```zsh
dotnet test src/SlimBus.ApiEndpoints/SlimBus.App.Tests/SlimBus.App.Tests.csproj --filter "FullyQualifiedName~Integration.Profiles.V1"
```

