Feature: Customer Profile Create Idempotency
  As an API consumer
  I want a retried create request to be deduplicated by its idempotency key
  So that a network retry never creates a second profile

  @integration
  Scenario: A retried create request with the same idempotency key does not create a second profile
    Given the service is running with no Redis connection configured
    And a customer profile has been created for Bao Duy with idempotency key "6f1c2a90-0001-4a3b-9d2e-1f0b7c5ad001"
    When the same create request is sent again with idempotency key "6f1c2a90-0001-4a3b-9d2e-1f0b7c5ad001"
    Then the first request's result is returned
    And only one customer profile exists for Bao Duy

  @integration
  Scenario: A create request carrying no idempotency key is rejected
    Given the service is running with no Redis connection configured
    When a customer profile is requested for Bao Duy without an idempotency key
    Then the request is rejected
    And no customer profile exists for Bao Duy
