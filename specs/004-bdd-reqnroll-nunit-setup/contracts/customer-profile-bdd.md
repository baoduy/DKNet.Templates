# BDD Feature Contract: Create Customer Profile

**Purpose**: Canonical Gherkin template for the Customer Profile creation BDD scenarios.  
This file lives in `specs/` as the spec-level contract. The implementation copy lives at
`src/ApiEndpoints/Minimal.App.BDDTests/Features/CustomerProfiles/CreateCustomerProfile.feature`.

> **2026-08-25 note:** This document is a historical record from when the template's demo
> features were `CustomerProfile`/`LoyaltyMembership`. Those were removed; current worked
> examples are the `PurchaseOrder` (hand-written) and `Product` (generator-driven) samples —
> see `docs/samples/manual-vs-automated.md`.

---

```gherkin
Feature: Create Customer Profile
  As an API consumer
  I want to create a new customer profile via the REST API
  So that the profile is persisted and can be retrieved later

  Background:
    Given the API has no customer profiles

  Scenario: Happy path — create a new profile with valid data
    When I send a create profile request with the following data:
      | Name             | Email                       | Phone        |
      | Integration User | bdd.create@example.com      | +6598765432  |
    Then the response should be successful
    And the response body should contain the profile name "Integration User"

  Scenario: Duplicate email — create a profile with an already-registered email
    Given a customer profile with email "bdd.dup@example.com" already exists
    When I send a create profile request with the following data:
      | Name       | Email                  | Phone        |
      | Duplicate  | bdd.dup@example.com    | +6500011122  |
    Then the response should be successful
    And the response body should contain an error message for duplicate email "bdd.dup@example.com"

  Scenario: Validation error — create a profile with a missing required field
    When I send a create profile request with the following data:
      | Name          | Email | Phone        |
      | Missing Email |       | +6511122233  |
    Then the response should indicate a validation error
```

---

## API Contract: POST /api/v1/customer-profiles

**Method**: `POST`  
**Route**: `/api/v1/customer-profiles`  
**Required Header**: `X-Idempotency-Key: <GUID>` (enforced by idempotency filter)  
**Content-Type**: `application/json`

### Request Body

```json
{
  "name":  "string (required, non-empty)",
  "email": "string (required, valid email format)",
  "phone": "string (required, non-empty)"
}
```

> `byUser` is injected automatically by `SetUserIdPropertyFilter`; do not include in request body from step definitions.

### Success Response — HTTP 200

```json
{
  "isSuccess": true,
  "value": {
    "id": "guid",
    "name": "string",
    "email": "string",
    "phone": "string",
    "membershipNo": "TEST-MEM-000001"
  }
}
```

### Business-Rule Failure Response — HTTP 200 (IsFailed)

```json
{
  "isSuccess": false,
  "errors": [
    { "message": "Email bdd.dup@example.com is already existed." }
  ]
}
```

### Validation Failure Response — HTTP 400

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Email": ["'Email' must not be empty."]
  }
}
```

---

## Step Definition Binding Contract

| Gherkin Step Pattern | Method Name | Notes |
|---|---|---|
| `Given the API has no customer profiles` | `GivenTheApiHasNoCustomerProfiles` | Calls `BddApiFactory.ResetDatabaseAsync()` |
| `Given a customer profile with email {string} already exists` | `GivenAProfileWithEmailAlreadyExists` | Seeds via direct bus `Send(CreateProfileRequest)` |
| `When I send a create profile request with the following data:` | `WhenISendACreateProfileRequest` | Uses `DataTable` parameter; sets `X-Idempotency-Key` |
| `Then the response should be successful` | `ThenTheResponseShouldBeSuccessful` | Asserts `IsSuccess == true` in body |
| `Then the response body should contain the profile name {string}` | `ThenTheResponseBodyShouldContainProfileName` | Asserts `value.name` in JSON |
| `Then the response body should contain an error message for duplicate email {string}` | `ThenTheResponseBodyShouldContainDuplicateError` | Asserts `errors[0].message` contains email |
| `Then the response should indicate a validation error` | `ThenTheResponseShouldIndicateValidationError` | Asserts HTTP 400 status code |
