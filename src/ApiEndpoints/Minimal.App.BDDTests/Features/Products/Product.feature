@Product
Feature: Product CRUD lifecycle (automated sample)
  As a caller of the generator-driven AutomatedSample/Product feature
  I want to create, read, update and delete products over HTTP
  So that the fully generated CRUD slice ([CrudCreate]/[CrudUpdate]/[RaisesEvent]/[GenerateDto]) is proven end to end

  Background:
    Given the service is running with no Redis connection configured

  Scenario: Creating a product persists it and returns its details
    When I create a product named "Widget" with price 9.99
    Then the response status is 201
    And the product response has name "Widget" and price 9.99

  Scenario: Creating a product raises the internal ProductCreatedEvent
    When I create a product named "Widget" with price 9.99
    Then a log line reports the automated sample product was created

  Scenario: A negative price is still accepted — a known and accepted limitation of the generated path
    # docs/samples/manual-vs-automated.md's sharpest documented gap: [Range(0.01, double.MaxValue)] is
    # forwarded onto the generated request but never evaluated, because the .NET 10 minimal-API validation
    # source generator cannot see through DKNet.AspCore.Extensions' generic MapPost<TRequest,TResponse>.
    When I create a product named "Broken Widget" with price -1
    Then the response status is 201

  Scenario: Getting an unknown product returns 404
    When I get the product with id "99999999-9999-9999-9999-999999999999"
    Then the response status is 404

  Scenario: Listing products returns every created product
    Given a product exists named "Gizmo" with price 15.00
    When I list products
    Then the response status is 200
    And the response includes a product named "Gizmo"

  Scenario: Updating a product changes its price
    Given a product exists named "Gadget" with price 20.00
    When I change that product's price to 30.00
    Then the response status is 200
    And the product response has price 30.00

  Scenario: Deleting a product removes it
    Given a product exists named "Doohickey" with price 5.00
    When I delete that product
    # The generic MapDeleteById<TEntity,TKey>() library route returns 204 (No Content) — unlike the manual
    # sample's hand-written delete route, which returns 200 with a body (see PurchaseOrder.feature).
    Then the response status is 204
    When I get that product
    Then the response status is 404

  Scenario: Approving a product stamps the acting user and returns its details
    Given a product exists named "Approvable" with price 12.00
    When I approve that product as "alice"
    Then the response status is 200
    And the product response has name "Approvable" and price 12.00
    And the product response was approved by "alice"

  Scenario: Discontinuing a product marks it discontinued
    Given a product exists named "Retiring" with price 8.00
    When I discontinue that product
    Then the response status is 200
    And the product response is discontinued

  Scenario: Repeating discontinue is a 200 no-op, not a rejection
    # docs/samples/manual-vs-automated.md #4: a generated action has nowhere to hang a pre-condition, so
    # discontinuing an already-discontinued product succeeds again instead of failing.
    Given a product exists named "Retiring Twice" with price 8.00
    When I discontinue that product
    Then the response status is 200
    When I discontinue that product
    Then the response status is 200
    And the product response is discontinued
