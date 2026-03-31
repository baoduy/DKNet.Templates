Feature: Create Customer Profile
  As an API consumer
  I want to create a new customer profile via the REST API
  So that the profile is persisted and can be retrieved later

  Background:
    Given the API has no customer profiles

  Scenario: Happy path — create a new profile with valid data
    When I send a create profile request with the following data:
      | Name             | Email                  | Phone       |
      | Integration User | bdd.create@example.com | +6598765432 |
    Then the response should be successful
    And the response body should contain the profile name "Integration User"

  Scenario: Duplicate email — create a profile with an already-registered email
    Given a customer profile with email "bdd.dup@example.com" already exists
    When I send a create profile request with the following data:
      | Name      | Email               | Phone       |
      | Duplicate | bdd.dup@example.com | +6500011122 |
    Then the response should be successful
    And the response body should contain an error message for duplicate email "bdd.dup@example.com"

  Scenario: Validation error — create a profile with a missing required field
    When I send a create profile request with the following data:
      | Name          | Email | Phone       |
      | Missing Email |       | +6511122233 |
    Then the response should indicate a validation error
