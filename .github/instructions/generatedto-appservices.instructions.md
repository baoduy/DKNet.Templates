---
description: "Use when creating or updating request/response DTO records in AppServices. Enforces GenerateDto-first policy with explicit exceptions and writable-field audit guidance."
applyTo: "src/ApiEndpoints/Minimal.AppServices/**/*.cs"
---

# GenerateDto-First for AppServices

Apply this policy for all DTO/request/response record changes under AppServices.

## Default Rule

- Use `[GenerateDto(typeof(Entity), Exclude = [...])]` for response DTOs by default.
- Keep `[MapsFrom(typeof(Entity))]` on generated response records.

## Request Record Rules

- Prefer entity-aligned shapes to avoid request/response drift.
- Use manual request records only when there is an explicit contract reason:
  - server-generated or audit fields must be hidden from client input
  - operation is partial/mutation-specific (not full entity projection)
  - request field names/types intentionally diverge from entity

## Required Audit for Manual Request Records

When adding/changing `Create*Request` or `Update*Request`, include a short writable-field audit comment block in the file:

- Writable fields: fields client is allowed to set
- Server-managed fields: Id/audit/sequence/by-user fields not client-writable
- Divergence notes: fields intentionally different from entity and why

## Quality Checks

- Avoid duplicating manual `*Dto` records that mirror entity shape when GenerateDto can be used.
- If manual DTO is kept, document why GenerateDto is not appropriate.
- Keep naming/type consistency with entity for all non-divergent fields.
