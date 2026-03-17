# Quick Validation Checklist: Feature Documentation

## Folder Structure
- [ ] Feature docs folder created at `docs/features/<feature-name>/` (kebab-case)
- [ ] All 5 required documents are present

## README.md (Overview)
- [ ] Explains **what** the feature does (1-2 sentences)
- [ ] Explains **why** it exists (business value, problem solved)
- [ ] Contains quick-start code example (at minimum one HTTP example)
- [ ] Lists all key concepts in a table
- [ ] Contains a **Feature Map** table linking to source files across layers
- [ ] Links to all other docs (architecture, api-reference, data-model, events)

## architecture.md (Diagrams)
- [ ] **Vertical Slice Diagram** (`graph TD`) shows all layers (Api → AppServices → Domains → Infra)
- [ ] **Sequence Diagram** shows the full request flow for at least one write operation (e.g., Create)
- [ ] **Component Diagram** (`classDiagram`) shows key classes and their relationships
- [ ] **State Diagram** (`stateDiagram-v2`) present if entity has status/state (Pending/Approved/Rejected etc.)
- [ ] **Event Flow Diagram** shows publishers and subscribers
- [ ] All diagrams use Mermaid (render in GitHub natively)
- [ ] Layer responsibilities table lists each layer's role in this specific feature

## api-reference.md (Endpoint Reference)
- [ ] Summary table lists ALL endpoints (Method / Path / Description / Auth)
- [ ] Each endpoint has: description, request body or params, response body example (JSON)
- [ ] Request field tables show: field name, type, required flag, validation rules
- [ ] Each endpoint has at least one `curl` example that can be copy-pasted
- [ ] Error response table lists all possible error status codes and reasons
- [ ] Common `ProblemDetails` error format documented at end
- [ ] Custom action endpoints (approve, reject, etc.) documented
- [ ] GET list endpoint documents all query parameters (pagination, sorting, filtering)

## data-model.md (Data Model)
- [ ] `erDiagram` (Mermaid ER diagram) shows all columns with types and constraints
- [ ] Properties table lists every field with C# type, DB column name, and constraints
- [ ] Unique indexes documented
- [ ] EF Core mapping notes (table name, schema, global query filters)
- [ ] Validation rules table (field → rule → enforcement mechanism)
- [ ] Related entities shown if any foreign key relationships exist

## events.md (Domain Events)
- [ ] Each published event documented with: name, publisher, payload (record definition)
- [ ] Payload property table (name, type, description)
- [ ] Known subscribers table (handler class name, bus type, action description)
- [ ] Code example showing how to subscribe to the event
- [ ] "Events Consumed" section present (or explicitly states "none")
- [ ] Event bus configuration documented (In-Memory vs Azure Service Bus conditions)
- [ ] Mermaid event flow diagram showing publish and subscribe flow

## Quality Standards
- [ ] All file names are lowercase with hyphens (`api-reference.md`, not `ApiReference.md`)
- [ ] Folder name uses kebab-case (`customer-profiles`, not `CustomerProfiles`)
- [ ] All internal links work (relative links between docs)
- [ ] No broken Mermaid diagrams (test by opening in VS Code Preview or GitHub)
- [ ] JSON examples are valid (check with a formatter)
- [ ] All source file paths in the Feature Map table are correct and files exist
- [ ] No placeholder text left (e.g., `{todo}`, `replace this`, etc.)
