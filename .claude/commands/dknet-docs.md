---
description: Generate or refresh feature documentation (README + architecture diagrams + API reference) under src/docs or docs/features for an implemented DKNet feature.
argument-hint: <Feature>
allowed-tools: Read, Grep, Glob, Edit, Write, Bash
---

You are producing authoritative feature documentation for a vertical slice that is already implemented and tested.

## Required reading

1. `.claude/skills/dknet-feature-documentation/SKILL.md`
2. `.claude/skills/dknet-feature-documentation/templates/` (README, architecture, data-model, events, api-reference templates).
3. `docs/samples/manual-vs-automated.md` and the two per-sample READMEs (`docs/samples/manual-purchase-orders/README.md`, `docs/samples/automated-products/README.md`) for shape/voice reference — these are the current worked examples of feature documentation in this repo.

## Steps

1. Inspect the implemented slice to harvest facts: entity properties, mapper indexes, request/response DTOs, validator rules, endpoint routes, event names, test coverage.
2. Render the four required artifacts under `docs/features/<feature>/` (or `src/docs/<feature>/` if the slice is template-internal):
   - `README.md` (feature overview + quick links)
   - `architecture.md` (Mermaid diagrams: layer flow, sequence for Create, ER snippet)
   - `data-model.md`
   - `api-reference.md`
3. Cross-link from `docs/features/README.md` (or whichever index file lists features).
4. Verify all referenced files exist and Mermaid blocks render (no stray fences).

## Constraints

- Do not invent fields, validators, events, or endpoints — only document what's in the code.
- Use the templates in the skill folder verbatim where they fit; deviations need a one-line note.
- No hand-wavey language ("flexible", "robust", "scalable") — describe what the code actually does.
