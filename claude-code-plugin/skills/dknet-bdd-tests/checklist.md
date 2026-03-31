# BDD Scenario Skill Checklist

Use this checklist before considering BDD scenario work complete.

## Context Coverage

- [ ] Reviewed `docs/features/<feature>/README.md`
- [ ] Reviewed `docs/features/<feature>/api-reference.md`
- [ ] Reviewed `specs/<feature>/spec.md`
- [ ] Reviewed `specs/<feature>/contracts/*`
- [ ] Captured at least one edge case from spec
- [ ] Confirmed `specs/<feature>/contracts/*` is treated as assertion source of truth

## Scenario Quality

- [ ] `.feature` file has clear business title and purpose
- [ ] Includes happy path scenario
- [ ] Includes business-rule failure scenario
- [ ] Includes validation failure scenario
- [ ] Uses stable domain language (no implementation jargon)

## Binding Quality

- [ ] Every step has exactly one matching `[Given]/[When]/[Then]` binding
- [ ] Constructor injection uses scenario-registered dependencies
- [ ] Request serialization uses `SharedConsts.JsonSerializerOptions`
- [ ] Required headers (for example idempotency) are present
- [ ] Assertions verify status code, contract-defined JSON structure, and key data fields
- [ ] Success assertions validate required `value` fields (not only success flag)
- [ ] Failure assertions validate `errors` array/object shape and expected messages/codes
- [ ] No assertion relies only on substring matching when structured contract fields exist

## Validation

- [ ] `dotnet build src/DKNet.Templates.sln -c Release` succeeds
- [ ] `dotnet test src/ApiEndpoints/Minimal.App.BDDTests` passes
- [ ] No undefined or pending Reqnroll steps
- [ ] Scenario names are readable in test output
