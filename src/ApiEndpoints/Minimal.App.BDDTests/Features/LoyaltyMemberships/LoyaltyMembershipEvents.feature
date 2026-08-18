Feature: Loyalty Membership Declared Events
  As a developer scaffolding a service from the DKNet template
  I want the loyalty membership aggregate's events to be declared, never hand-raised
  So that adding a new mutation path later cannot silently drop its events

  @integration
  Scenario: Enrolling a member publishes the enrolment event
    Given the service is running with no Redis connection configured
    When Alice Nguyen is enrolled in the loyalty programme at tier Silver with 0 points
    Then the enrolment is stored
    And the application log contains one line reporting Alice Nguyen's enrolment

  @integration
  Scenario: Changing the tier publishes the tier-changed event
    Given Alice Nguyen holds a Silver loyalty membership
    When her membership is changed to tier Gold
    Then the application log contains one line reporting her tier change to Gold

  @integration
  Scenario: Changing only the points balance does not publish the tier-changed event
    Given Alice Nguyen holds a Gold loyalty membership with 120 points
    When her points balance is changed to 300 and her tier is left at Gold
    Then the application log contains no line reporting a tier change

  @integration
  Scenario: Withdrawing a membership publishes the withdrawal event with the values it last held
    Given Alice Nguyen holds a Gold loyalty membership with 300 points
    When her loyalty membership is withdrawn
    Then the application log contains one line reporting the withdrawal at tier Gold with 300 points

  @integration
  Scenario: A rejected enrolment publishes nothing
    Given Alice Nguyen already holds a loyalty membership
    When a second loyalty membership is requested for Alice Nguyen
    Then the request is rejected
    And the application log contains no line reporting an enrolment
