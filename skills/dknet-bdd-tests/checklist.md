# BDD Scenario Skill Checklist

Use this checklist before considering BDD scenario work complete.

## Context Coverage

- [ ] Confirmed the behavior is HTTP-shaped (request → status → body) or an event side effect via log
      capture — otherwise it belongs in the `dknet-unit-test` skill instead
- [ ] Reviewed the feature's AppServices request/handler code as the assertion source of truth (status
      codes, error codes, response fields)
- [ ] Checked `CommonSteps` for an existing step with the same meaning before writing a new one
- [ ] Captured at least one edge case (not-found, a guarded transition refused on retry, invalid input)

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
- [ ] For generated response DTO contracts, assertions include representative entity-derived fields
- [ ] Success assertions validate required `value` fields (not only success flag)
- [ ] Failure assertions validate `errors` array/object shape and expected messages/codes
- [ ] No assertion relies only on substring matching when structured contract fields exist

## Validation

- [ ] `dotnet build -c Release` succeeds
- [ ] `dotnet test ApiEndpoints/Minimal.App.BDDTests/Minimal.App.BDDTests.csproj` passes
- [ ] No undefined or pending Reqnroll steps
- [ ] Scenario names are readable in test output
