Feature: PostgreSQL Migration Verification
  As a developer
  I want to verify that the PostgreSQL migration produces the correct schema
  So that the application is fully migrated to PostgreSQL

  Background:
    Given the API is running with PostgreSQL configuration

  @migration-verification @sequence
  Scenario: SequenceService returns a value without error
    When the SequenceService generates the next MembershipNo
    Then a non-empty value should be returned

  @migration-verification @sequence
  Scenario: SequenceService generates unique values with InMemory fallback
    When the SequenceService generates 10 consecutive membership numbers
    Then all 10 values should be unique
    And each value should be a valid GUID

  @migration-verification @table
  Scenario: CustomerProfiles table has correct schema
    When I inspect the CustomerProfiles table model
    Then the table should be in schema "pro"
    And the table should have columns "Id, Email, MembershipNo, Name, Phone, Avatar, BirthDay"
    And the Email column should have a unique index
    And the MembershipNo column should have a unique index