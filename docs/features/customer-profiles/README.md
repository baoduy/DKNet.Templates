# Customer Profiles

> Manages customer identity, contact information, and membership accounts.

## What Is This?

The Customer Profiles feature provides create/read/update/delete for customer records. Each
profile is linked to a user account and carries a unique auto-generated membership number issued
at creation time.

## Why Does It Exist?

Customer profiles are the central entity in the system. All downstream features
(orders, invoices, communications) reference a profile. This feature enables:

- Customer onboarding via REST API
- Membership number auto-generation (via `IMembershipService`, backed by a PostgreSQL sequence)
- Audit trail of all changes via `CreatedBy` / `UpdatedBy` fields

## Quick Start

### Create a Profile

```http
POST /v1/customer-profiles
Content-Type: application/json
Authorization: Bearer {token}
X-Idempotency-Key: {Guid}

{
  "name": "Jane Smith",
  "email": "jane.smith@example.com",
  "phone": "+61412345678"
}
```

### Get a Profile

```http
GET /v1/customer-profiles/3fa85f64-5717-4562-b3fc-2c963f66afa6
Authorization: Bearer {token}
```

## Key Concepts

| Concept | Description |
|---------|-------------|
| **MembershipNo** | Unique identifier auto-generated on create by `IMembershipService`. Read-only after assignment. |
| **ByUser** | The authenticated user ID who created or last modified the record (from Bearer token) |
| **Idempotency** | POST requests use `X-Idempotency-Key` header to prevent duplicate creation |

> No approval workflow and no soft-delete: the entity has no `Status` or `IsDeleted` field, and
> `DELETE` removes the row.

## Feature Map

| Layer | Path |
|-------|------|
| Domain Entity | `Minimal.Domains/Features/Profiles/Entities/CustomerProfile.cs` |
| EF Core Mapper | `Minimal.Infra/Features/Profiles/Mappers/CustomerProfileConfigs.cs` |
| Create Handler | `Minimal.AppServices/CustomerProfiles/V1/Actions/Create.cs` |
| Update Handler | `Minimal.AppServices/CustomerProfiles/V1/Actions/Update.cs` |
| Delete Handler | `Minimal.AppServices/CustomerProfiles/V1/Actions/Delete.cs` |
| Domain Events | `Minimal.AppServices/CustomerProfiles/V1/Events/` |
| Query Specs | `Minimal.AppServices/CustomerProfiles/V1/Specs/` |
| API Endpoint | `Minimal.Api/ApiEndpoints/CustomerProfileV1Endpoint.cs` |

## Related Documentation

- [Architecture](./architecture.md)
- [API Reference](./api-reference.md)
- [Data Model](./data-model.md)
- [Domain Events](./events.md)
