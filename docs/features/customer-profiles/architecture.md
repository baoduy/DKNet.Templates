# Customer Profiles — Architecture

## Vertical Slice Overview

Customer Profiles follows the DKNet vertical slice architecture.
Each layer has a single, focused responsibility for this feature.

```mermaid
graph TD
    Client["Client / Browser"]

    subgraph API["Minimal.Api"]
        EP["CustomerProfileV1Endpoint\n(IEndpointConfig)"]
    end

    subgraph AppServices["Minimal.AppServices/CustomerProfiles"]
        REQ["Request Types\n(Create/Update/Delete)"]
        VAL["Validators\n(FluentValidation)"]
        HDL["Command Handlers\n(IHandler)"]
        SPEC["Query Specs\nSpecGetCustomerProfiles\nSpecGetProfileByEmail"]
        EVT["Domain Events\nProfileCreatedEvent"]
    end

    subgraph Domains["Minimal.Domains"]
        ENT["CustomerProfile\n(AggregateRoot)\nId, Name, Email,\nMembershipNo, Phone, Avatar, BirthDay"]
    end

    subgraph Infra["Minimal.Infra"]
        MAP["CustomerProfileConfigs.cs\n(EF Core Config)"]
        REPO["IRepositorySpec\n(EF Core + Spec)"]
        EVH["ProfileCreatedEventHandlers\n(In-Memory / Azure Bus)"]
    end

    DB[("PostgreSQL — schema 'pro'\nCustomerProfiles table")]

    Client -->|HTTP| EP
    EP -->|Message Bus| REQ
    REQ --> VAL
    VAL --> HDL
    HDL -->|Query via Spec| SPEC
    SPEC -->|Reads| REPO
    HDL -->|Mutations| REPO
    HDL -->|Publish| EVT
    REPO --> MAP
    MAP --> DB
    EVT --> EVH
```

## Create Profile — Sequence Diagram

```mermaid
sequenceDiagram
    participant C as Client
    participant EP as ProfileEndpoints
    participant BUS as MessageBus
    participant VAL as CreateProfileCommandValidator
    participant HDL as CreateProfileCommandHandler
    participant SPEC as SpecGetProfileByEmail
    participant REPO as IRepositorySpec
    participant MEM as IMembershipService
    participant EVT as EventPublisher

    C->>EP: POST /v1/customer-profiles
    EP->>BUS: bus.Send(CreateProfileRequest)
    BUS->>VAL: Validate(request)
    VAL-->>BUS: Valid ✓

    BUS->>HDL: Handle(request)
    HDL->>SPEC: new SpecGetProfileByEmail(email)
    HDL->>REPO: FirstOrDefaultAsync(spec)
    REPO-->>HDL: null (no duplicate)

    HDL->>MEM: NextValueAsync()
    MEM-->>HDL: "MEM-2025-00001"

    HDL->>HDL: new CustomerProfile(name, membershipNo, email, phone, byUser)
    HDL->>REPO: AddAsync(profile)
    HDL->>REPO: SaveChangesAsync()
    REPO-->>HDL: OK

    HDL->>EVT: PublishAsync(ProfileCreatedEvent)
    EVT-->>HDL: OK

    HDL-->>BUS: Result<CustomerProfileDto>.Success(dto)
    BUS-->>EP: CustomerProfileDto
    EP-->>C: 201 Created + CustomerProfileDto
```

## Component Diagram

```mermaid
classDiagram
    class CustomerProfileV1Endpoint {
        +int Version = 1
        +string GroupEndpoint = "/customer-profiles"
        +Map(RouteGroupBuilder group)
    }

    class CreateProfileRequest {
        +string Email
        +string Name
        +string Phone
        +string MembershipNo
    }

    class CreateProfileCommandHandler {
        -IRepositorySpec repository
        -IMembershipService membershipProvider
        -IMapper mapper
        +OnHandle(request, cancellationToken) IResult~CustomerProfileDto~
    }

    class CustomerProfile {
        +Guid Id
        +string Name
        +string Email
        +string MembershipNo
        +string? Phone
        +string? Avatar
        +DateTime? BirthDay
        +CustomerProfile(name, membershipNo, email, phone, byUser)
        +Update(avatar, name, phoneNumber, birthday, userId)
    }

    class CustomerProfileConfigs {
        +Configure(EntityTypeBuilder~CustomerProfile~)
    }

    class CustomerProfileDto {
        +Guid Id
        +string Name
        +string Email
        +string MembershipNo
        +string? Phone
        +DateTime CreatedOn
    }

    CustomerProfileV1Endpoint ..> CreateProfileRequest : dispatches
    CreateProfileCommandHandler --> CustomerProfile : creates
    CreateProfileCommandHandler --> IRepositorySpec : queries via Spec
    CustomerProfileConfigs --> CustomerProfile : configures EF mapping
    CreateProfileCommandHandler ..> CustomerProfileDto : maps to
```

## Event Flow

```mermaid
graph LR
    HDL["CreateProfileCommandHandler"] -->|PublishAsync| EVT["ProfileCreatedEvent\n(Id, Name)"]

    EVT --> MEM["In-Memory Bus"]
    EVT --> AZ["Azure Service Bus\n(if AzureBus configured)"]

    MEM --> INTL["ProfileCreatedEventFromMemoryHandler\n(internal audit/testing)"]
    AZ --> EXT["CustomerProfileCreatedEmailNotificationHandler\n(topic 'profile-tp' / subscription 'profile-sub')"]
```

## Layer Responsibilities

| Layer | Responsibility in Customer Profiles |
|-------|--------------------------------------|
| `Minimal.Api` | Route mapping only; no business logic — dispatches to message bus |
| `Minimal.AppServices` | Command handling, FluentValidation, membership number generation, event publishing |
| `Minimal.Domains` | `CustomerProfile` entity with constructors and `Update()` method |
| `Minimal.Infra` | EF Core `CustomerProfileConfigs`, `IRepositorySpec` implementation, event handler wiring |

> There is no approval workflow or soft-delete on `CustomerProfile` — it has no `Status` or
> `IsDeleted` field. `DELETE` removes the row. If your feature needs a workflow like this, add the
> field/state machine yourself; don't assume one exists here.
