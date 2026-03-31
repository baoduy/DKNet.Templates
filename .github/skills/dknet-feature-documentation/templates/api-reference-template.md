# {FeatureName} — API Reference

**Base Path**: `/api/v1/{feature-route}`
**Auth**: Bearer token required on all endpoints
**Content-Type**: `application/json`

---

## Endpoints Summary

| Method | Path | Description | Auth |
|--------|------|-------------|------|
| `GET` | `/` | List {entities} (paginated) | ✓ |
| `GET` | `/{id}` | Get {entity} by ID | ✓ |
| `POST` | `/` | Create new {entity} | ✓ |
| `PUT` | `/{id}` | Update {entity} | ✓ |
| `DELETE` | `/{id}` | Soft-delete {entity} | ✓ |
| `PATCH` | `/{id}/approve` | Approve pending {entity} | ✓ Admin |
| `PATCH` | `/{id}/reject` | Reject pending {entity} | ✓ Admin |

> Remove rows that don't apply. Add custom actions as needed.

---

## GET /api/v1/{feature-route}

Returns a paginated list of {entities}.

**Query Parameters**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `pageNumber` | int | 1 | Page number (1-based) |
| `pageSize` | int | 20 | Items per page (max 100) |
| `search` | string | — | Filter by name or key fields |
| `sortBy` | string | `CreatedAt` | Field to sort by |
| `sortDirection` | string | `desc` | `asc` or `desc` |

**Response** `200 OK`

```json
{
  "items": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "field1": "value",
      "field2": "value",
      "status": "Pending",
      "createdAt": "2025-01-15T10:30:00Z",
      "updatedAt": "2025-01-20T14:00:00Z"
    }
  ],
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 100,
  "totalPages": 5
}
```

**curl Example**

```bash
curl -X GET "https://api.example.com/api/v1/{feature-route}?pageSize=10" \
  -H "Authorization: Bearer {token}"
```

---

## GET /api/v1/{feature-route}/{id}

Returns a single {entity} by ID.

**Route Parameters**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | `Guid` | The {entity} unique identifier |

**Response** `200 OK`

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "field1": "value",
  "field2": "value",
  "status": "Pending",
  "createdAt": "2025-01-15T10:30:00Z",
  "updatedAt": "2025-01-20T14:00:00Z"
}
```

**Error Responses**

| Status | Reason |
|--------|--------|
| `404 Not Found` | No {entity} with this ID |

---

## POST /api/v1/{feature-route}

Creates a new {entity}.

**Request Body**

```json
{
  "field1": "value",
  "field2": "value"
}
```

| Field | Type | Required | Rules |
|-------|------|----------|-------|
| `field1` | string | ✓ | {constraints, e.g., 2–150 characters} |
| `field2` | string | ✓ | {constraints, e.g., valid email, unique} |
| `field3` | string | — | {optional field rules} |

**Response** `201 Created`

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "field1": "value",
  "field2": "value",
  "status": "Pending",
  "createdAt": "2025-01-15T10:30:00Z",
  "updatedAt": "2025-01-15T10:30:00Z"
}
```

**Error Responses**

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Validation failure |
| `409 Conflict` | Duplicate value in unique field |

**curl Example**

```bash
curl -X POST "https://api.example.com/api/v1/{feature-route}" \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{"field1":"value","field2":"value"}'
```

---

## PUT /api/v1/{feature-route}/{id}

Updates an existing {entity}. All fields are optional — only non-null fields are updated.

**Request Body**

```json
{
  "field1": "updated value"
}
```

| Field | Type | Required | Rules |
|-------|------|----------|-------|
| `field1` | string | — | {constraints}; if null, current value preserved |
| `field2` | string | — | {constraints}; if null, current value preserved |

**Response** `200 OK` — Returns updated `{EntityName}Dto`.

**Error Responses**

| Status | Reason |
|--------|--------|
| `400 Bad Request` | All fields null (nothing to update) |
| `404 Not Found` | {Entity} not found |

---

## DELETE /api/v1/{feature-route}/{id}

Soft-deletes the {entity}. The record is preserved with `IsDeleted = true`.

**Response** `204 No Content`

**Error Responses**

| Status | Reason |
|--------|--------|
| `404 Not Found` | {Entity} not found |

---

## PATCH /api/v1/{feature-route}/{id}/approve

Approves a pending {entity}. Updates status to `Approved`.

> Remove this section if there is no approval workflow.

**Request Body**

```json
{
  "reason": "Manually verified"
}
```

| Field | Type | Required | Rules |
|-------|------|----------|-------|
| `reason` | string | — | Optional audit comment; max 500 chars |

**Response** `200 OK` — Returns updated `{EntityName}Dto`.

**Error Responses**

| Status | Reason |
|--------|--------|
| `400 Bad Request` | {Entity} is not in Pending state |
| `404 Not Found` | {Entity} not found |
| `403 Forbidden` | User does not have Admin role |

---

## PATCH /api/v1/{feature-route}/{id}/reject

Rejects a pending {entity}. Reason is **required** for audit trail.

> Remove this section if there is no rejection workflow.

**Request Body**

```json
{
  "reason": "Identity documents not provided"
}
```

| Field | Type | Required | Rules |
|-------|------|----------|-------|
| `reason` | string | ✓ | Required; 1–500 characters |

**Response** `200 OK` — Returns updated `{EntityName}Dto`.

---

## Common Error Response Format

All errors return `ProblemDetails`:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Validation Error",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "errors": {
    "field1": ["Field1 is required"],
    "field2": ["Field2 format is invalid"]
  }
}
```

| Status | Meaning |
|--------|---------|
| `400` | Validation or business rule failure |
| `401` | Missing or invalid Bearer token |
| `403` | Insufficient permissions (role missing) |
| `404` | Resource not found |
| `409` | Conflict (duplicate unique field) |
| `429` | Rate limit exceeded |
| `500` | Internal server error |
