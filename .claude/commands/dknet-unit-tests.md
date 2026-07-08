---
description: Add ApiFixture + IMessageBus integration tests for a DKNet feature (CRUD happy path, validation, duplicates, not-found, domain events).
argument-hint: <Feature> <Entity>
allowed-tools: Read, Grep, Glob, Edit, Write, Bash, Task
---

You are adding integration tests in `Minimal.App.Tests` that exercise the AppServices and Domains layers through the real DI container.

## Required reading

1. `.claude/skills/dknet-unit-test/SKILL.md`
2. `src/ApiEndpoints/Minimal.App.Tests/` — existing fixtures and test patterns (`Architecture/`, `Integration/`, `Unit/`).

## Steps

1. Use the `dknet-implementer` subagent (or follow the skill directly) to write tests covering:
   - Happy-path Create / Update / Delete via `IMessageBus.Send(...)`.
   - FluentValidation failures (empty / too-long / invalid format) on Create + Update.
   - Duplicate detection in Create handler.
   - Not-found in Update + Delete.
   - Domain event firing — assert the in-memory consumer ran.
   - Mapster smoke test (entity → DTO field-for-field).
2. Run only the affected tests:
   ```
   dotnet test src/ApiEndpoints/Minimal.App.Tests/Minimal.App.Tests.csproj --filter "FullyQualifiedName~<Entity>"
   ```
3. If any test fails, fix the test or product code (per skill guidance) — do not relax assertions.
4. Report: test file path, count, pass/fail, coverage areas hit.

## Constraints

- Tests use the real `ApiFixture` + DI container — no hand-rolled mocks for `IRepositorySpec` or `IMapper`.
- xUnit + Shouldly. Assertions: `result.IsSuccess.ShouldBeTrue()`, `result.Value.X.ShouldBe(...)`, `result.Errors.ShouldContain(e => ...)`.
- Reset DB state between tests (per the skill's fixture pattern). Don't leak state between cases.
