# {FeatureName} — Architecture

## Vertical Slice Overview

This feature follows the DKNet vertical slice architecture.
Each layer has a single, focused responsibility for this feature.

```mermaid
graph TD
    Client["Client / Browser"]

    subgraph API["Minimal.Api"]
        EP["{EntityName}V1Endpoint\n(IEndpointConfig)"]
    end

    subgraph AppServices["Minimal.AppServices"]
        REQ["Request Types\n(Create / Update / Delete\n+ custom actions)"]
        VAL["Validators\n(FluentValidation)"]
        HDL["Command Handlers\n(IHandler)"]
        SPEC["Query Specs\n(Ardalis.Specification)"]
        EVT["Domain Events\n({EntityName}CreatedEvent etc.)"]
    end

    subgraph Domains["Minimal.Domains"]
        ENT["{EntityName}\n(AggregateRoot)"]
    end

    subgraph Infra["Minimal.Infra"]
        MAP["{EntityName}Mapper.cs\n(EF Core Config)"]
        REPO["IRepositorySpec\n(EF Core + Spec)"]
        EVH["Event Handlers\n(Azure Bus / In-Memory)"]
    end

    DB[("SQL Server")]

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

## Create {EntityName} — Sequence Diagram

```mermaid
sequenceDiagram
    participant C as Client
    participant EP as {EntityName}Endpoints
    participant BUS as MessageBus
    participant VAL as Validator
    participant HDL as Create{EntityName}Handler
    participant SPEC as Spec{EntityName}GetByEmail
    participant REPO as IRepositorySpec
    participant EVT as EventPublisher

    C->>EP: POST /api/v1/{feature-route}
    EP->>BUS: bus.Send(Create{EntityName}Request)
    BUS->>VAL: Validate request
    VAL-->>BUS: Valid ✓

    BUS->>HDL: Handle(request)
    HDL->>SPEC: new Spec{EntityName}GetByEmail(email)
    HDL->>REPO: FirstOrDefaultAsync(spec)
    REPO-->>HDL: null (no duplicate)

    HDL->>HDL: new {EntityName}(...)
    HDL->>REPO: AddAsync(entity)
    HDL->>REPO: SaveChangesAsync()
    REPO-->>HDL: OK

    HDL->>EVT: PublishAsync({EntityName}CreatedEvent)
    EVT-->>HDL: OK

    HDL-->>BUS: Result<{EntityName}Dto>.Success(dto)
    BUS-->>EP: {EntityName}Dto
    EP-->>C: 201 Created + {EntityName}Dto
```

## Component Diagram

```mermaid
classDiagram
    class {EntityName}V1Endpoint {
        +int Version = 1
        +string GroupEndpoint = "/{feature-route}"
        +Map(RouteGroupBuilder group)
    }

    class Create{EntityName}Request {
        +string Field1
        +string Field2
    }

    class Create{EntityName}CommandHandler {
        -IMapper _mapper
        -IRepositorySpec _repo
        -IEventPublisher _eventPublisher
        +Handle(request) Result~{EntityName}Dto~
    }

    class {EntityName} {
        +Guid Id
        +string Field1
        +string Field2
        +string Status
        +Approve(reason)
        +Reject(reason)
        +Update(...)
    }

    class {EntityName}Mapper {
        +Configure(EntityTypeBuilder)
    }

    {EntityName}V1Endpoint ..> Create{EntityName}Request : maps request
    Create{EntityName}CommandHandler --> {EntityName} : creates
    Create{EntityName}CommandHandler --> IRepositorySpec : uses
    {EntityName}Mapper --> {EntityName} : configures
```

## Status State Machine

> Remove this section if the entity has no status/approval workflow.

```mermaid
stateDiagram-v2
    [*] --> Pending : {EntityName} Created

    Pending --> Approved : approve() action
    Pending --> Rejected : reject() action

    Approved --> [*] : (soft-deleted)
    Rejected --> [*] : (soft-deleted)

    note right of Pending : Default status on creation
    note right of Approved : Entity available for downstream use
    note right of Rejected : Reason stored for audit trail
```

## Event Flow

```mermaid
graph LR
    HDL["Create{EntityName}Handler"] -->|Publish| EVT["{EntityName}CreatedEvent"]

    EVT --> MEM["In-Memory Bus Handler"]
    EVT --> AZ["Azure Service Bus Handler\n(if AzureBus configured)"]

    MEM -->|Side effects| LOG["Audit Log / Internal"]
    AZ -->|Message to subscribers| EXT["External Systems\n(Notifications, Billing, etc.)"]
```

## Layer Responsibilities

| Layer | Responsibility in this feature |
|-------|-------------------------------|
| `Minimal.Api` | Route mapping only; zero business logic |
| `Minimal.AppServices` | Command handling, validation, event publishing |
| `Minimal.Domains` | Entity state, domain rules, invariants |
| `Minimal.Infra` | Persistence, EF Core config, message bus setup |
