---
description: Add ApiFixture + IMessageBus integration tests for a DKNet feature (CRUD happy path, validation, duplicates, not-found, domain events).
argument-hint: <Feature> <Entity> [mode=manual|auto]
allowed-tools: Read, Grep, Glob, Edit, Write, Bash, Task
---

You are adding integration tests in `Minimal.App.Tests` that exercise the AppServices and Domains layers through the real DI container.

## Inputs

`$ARGUMENTS` — feature folder, entity, optional `mode=manual|auto`. If not supplied, detect it: a
`[CrudCreate]` on the entity means `auto`. The mode decides which of the cases below apply.

## Required reading

1. `.claude/skills/dknet-unit-test/SKILL.md`
2. `ApiEndpoints/Minimal.App.Tests/` — existing fixtures and test patterns (`Architecture/`, `Integration/`, `Unit/`).

## Steps

1. Use the `dknet-implementer` subagent (or follow the skill directly) to write tests covering.

   Both modes:
   - Happy-path Create / Update / Delete via `IMessageBus.Send(...)`.
   - Not-found in Update + Delete.
   - Domain event firing — assert the in-memory consumer ran. In `auto`, send to the **composed**
     event name (`<Entity><NarrowingProps><Operation>Event`, e.g. `ProductPriceUpdatedEvent`), which
     you verify against the compiled assembly, not by guessing.
   - Mapster smoke test (entity → DTO field-for-field).

   `mode=manual` only:
   - FluentValidation failures (empty / too-long / invalid format) on Create + Update.
   - Duplicate detection in Create handler.
   - Rejected state transitions (`Result.Fail`) on any business action.

   `mode=auto` only:
   - Entity mutation methods tested directly — that is where the behavior lives.
   - **Do NOT** write a test asserting a `400`/validation failure from a forwarded DataAnnotations
     attribute on a generated request. It is never enforced under this template's endpoint
     convention, so such a test either fails or, worse, gets "fixed" by relaxing it into asserting
     the gap is correct. Note the gap in the report instead.
2. Run only the affected tests:
   ```
   dotnet test ApiEndpoints/Minimal.App.Tests/Minimal.App.Tests.csproj --filter "FullyQualifiedName~<Entity>"
   ```
3. If any test fails, fix the test or product code (per skill guidance) — do not relax assertions.
4. Report: test file path, count, pass/fail, coverage areas hit.

## Constraints

- Tests use the real `ApiFixture` + DI container — no hand-rolled mocks for `IRepositorySpec` or `IMapper`.
- xUnit + Shouldly. Assertions: `result.IsSuccess.ShouldBeTrue()`, `result.Value.X.ShouldBe(...)`, `result.Errors.ShouldContain(e => ...)`.
- Reset DB state between tests (per the skill's fixture pattern). Don't leak state between cases.
