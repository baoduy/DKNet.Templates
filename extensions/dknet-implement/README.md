# DKNet Implementation Validator Extension

A Spec Kit extension that validates feature implementations against DKNet.Templates DDD vertical slice conventions and guides developers through the correct implementation order.

## Commands

### `/speckit.dknet-implement.validate`

Scans all features across the 4 DDD layers and validates:

- **Domain Layer**: Entity inheritance, property encapsulation, constructor pattern, mutation methods
- **Infrastructure Layer**: Mapper inheritance, auto-discovery rules, property config, schema mapping
- **AppServices Layer**: Request/handler patterns, validators, specs, events, lazy mapping
- **API Layer**: Endpoint config, fluent helpers, OpenAPI descriptions

Produces a detailed validation report with pass/fail per feature per layer.

### `/speckit.dknet-implement.implement`

Guides implementation through all 4 layers in the correct order using project-specific Claude Code skills:

1. Domain Entity → `dknet-domain-entity` skill
2. EF Core Config → `dknet-efcore-config` skill
3. AppServices Actions → `dknet-appservices-actions` skill
4. Endpoint Config → `dknet-endpoint-config` skill

Includes build checkpoints after each layer and automatic validation at completion.

## Hooks

- **after_implement** (mandatory): Automatically runs validation after `/speckit.implement` completes
- **after_tasks** (optional): Prompts to validate existing code after `/speckit.tasks` completes

## Installation

```bash
specify extension add --dev extensions/dknet-implement
```

## Related Skills

The Claude Code skills this extension orchestrates are in `.claude/skills/`:

- `dknet-domain-entity` — Domain entity creation
- `dknet-efcore-config` — EF Core mapper configuration
- `dknet-appservices-actions` — CRUD actions + business logic
- `dknet-endpoint-config` — REST API endpoint configuration

## License

MIT
