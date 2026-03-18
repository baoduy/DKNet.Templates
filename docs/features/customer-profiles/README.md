# Customer Profiles

> Manages customer identity, contact information, and membership accounts.

## What Is This?

The Customer Profiles feature provides a complete lifecycle for customer records — creation,
updates, soft-deletion, and an approval workflow. Each profile is linked to a user account
and carries a unique auto-generated membership number issued at creation time.

## Why Does It Exist?

Customer profiles are the central entity in the system. All downstream features
(orders, invoices, communications) reference a profile. This feature enables:

- Customer onboarding via REST API
- Membership number auto-generation (via `IMembershipService`)
- Profile approval workflow for KYC (Know Your Customer) compliance
- Audit trail of all changes via `CreatedBy` / `UpdatedBy` fields

## Quick Start

### Create a Profile

```http
POST /api/v1/customer-profiles
Content-Type: application/json
Authorization: Bearer {token}

{
  "name": "Jane Smith",
  "email": "jane.smith@example.com",
  "phone": "+61412345678"
}
```

### Get a Profile

```http
GET /api/v1/customer-profiles/3fa85f64-5717-4562-b3fc-2c963f66afa6
Authorization: Bearer {token}
```

### Approve a Pending Profile

```http
PATCH /api/v1/customer-profiles/3fa85f64-5717-4562-b3fc-2c963f66afa6/approve
Content-Type: application/json
Authorization: Bearer {admin-token}

{
  "reason": "KYC documents verified"
}
```

## Key Concepts

| Concept | Description |
|---------|-------------|
| **MembershipNo** | Unique identifier auto-generated on create by `IMembershipService`. Read-only after assignment. |
| **Status** | Approval workflow state: `Pending → Approved` or `Pending → Rejected` |
| **ByUser** | The authenticated user ID who created or last modified the record (from Bearer token) |
| **Soft Delete** | Profiles are never hard-deleted; `IsDeleted = true` hides them from all queries via EF Global Query Filter |
| **Idempotency** | POST requests use `X-Idempotency-Key` header to prevent duplicate creation |

## Feature Map

| Layer | Path |
|-------|------|
| Domain Entity | `Minimal.Domains/Features/Profiles/Entities/CustomerProfile.cs` |
| EF Core Mapper | `Minimal.Infra/Features/Profiles/Mappers/ProfileMapper.cs` |
| Create Handler | `Minimal.AppServices/CustomerProfiles/V1/Actions/Create.cs` |
| Update Handler | `Minimal.AppServices/CustomerProfiles/V1/Actions/Update.cs` |
| Delete Handler | `Minimal.AppServices/CustomerProfiles/V1/Actions/Delete.cs` |
| Domain Events | `Minimal.AppServices/CustomerProfiles/V1/Events/` |
| Query Specs | `Minimal.AppServices/CustomerProfiles/V1/Specs/` |
| API Endpoints | `Minimal.Api/ApiEndpoints/ProfileEndpoints.cs` |

## Related Documentation

- [Architecture](./architecture.md)
- [API Reference](./api-reference.md)
- [Data Model](./data-model.md)
- [Domain Events](./events.md)
