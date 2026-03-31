# DKNet Plugin for Claude Code

Skills and agents for scaffolding production-ready .NET 10 microservices using DKNet.Minimal.Template with vertical slice DDD/CQRS, EF Core, .NET Aspire, SlimMessageBus, and FluentValidation.

## Installation

### Option 1: Marketplace (recommended)

Claude Code plugins are distributed through **git-based marketplaces**, not file uploads.

```shell
# Step 1: Add this repo as a marketplace
/plugin marketplace add baoduy/DKNet.Templates

# Step 2: Install the plugin
/plugin install dknet-plugin@dknet-plugins

# Step 3: Reload
/reload-plugins
```

### Option 2: Local testing (development)

```bash
claude --plugin-dir ./claude-code-plugin
```

### Option 3: Submit to official Claude AI marketplace

Submit via the official form (no file upload — just provide the GitHub repo URL):
- **Claude.ai**: [claude.ai/settings/plugins/submit](https://claude.ai/settings/plugins/submit)
- **Console**: [platform.claude.com/plugins/submit](https://platform.claude.com/plugins/submit)

### Team configuration

Add to your project's `.claude/settings.json` so teammates get it automatically:

```json
{
  "extraKnownMarketplaces": {
    "dknet-plugins": {
      "source": {
        "source": "github",
        "repo": "baoduy/DKNet.Templates"
      }
    }
  },
  "enabledPlugins": {
    "dknet-plugin@dknet-plugins": true
  }
}
```

## Commands (12)

| Command | Description |
|---------|-------------|
| `/dknet-developer` | Full 11-phase workflow orchestrator: specify -> clarify -> plan -> architecture -> checklist -> tasks -> analyze -> implement -> BDD -> unit tests -> docs |
| `/dknet-bdd-test` | Create/update BDD scenarios with contract-first assertions (Reqnroll + NUnit) |
| `/speckit-specify` | Create feature specification from natural language description |
| `/speckit-clarify` | Identify spec ambiguities with up to 5 targeted questions |
| `/speckit-plan` | Generate design artifacts: research.md, data-model.md, contracts/, quickstart.md |
| `/speckit-architecture` | Create 14-section .NET architecture documentation |
| `/speckit-checklist` | Generate requirement quality checklists ("unit tests for English") |
| `/speckit-tasks` | Generate dependency-ordered tasks.md organized by user stories |
| `/speckit-analyze` | Non-destructive consistency analysis across spec/plan/tasks |
| `/speckit-implement` | Execute implementation plan phase by phase from tasks.md |
| `/speckit-constitution` | Create/update project constitution with template sync |
| `/speckit-taskstoissues` | Convert tasks to GitHub issues |

## Agents (12)

| Agent | Description |
|-------|-------------|
| `dknet-developer` | Workflow orchestrator delegating to specialized agents |
| `dknet-bdd-test` | BDD test engineer with Reqnroll + NUnit expertise |
| `speckit-specify` | Feature specification generator |
| `speckit-clarify` | Spec ambiguity detector and resolver |
| `speckit-plan` | Implementation planning and design artifact generator |
| `speckit-architecture` | .NET architecture documentation specialist |
| `speckit-checklist` | Requirement quality checklist generator |
| `speckit-tasks` | Task generation with dependency ordering |
| `speckit-analyze` | Cross-artifact consistency analyzer |
| `speckit-implement` | Task executor with progress tracking |
| `speckit-constitution` | Project constitution manager |
| `speckit-taskstoissues` | GitHub issue creator from tasks |

## Skills (7)

| Skill | Description |
|-------|-------------|
| `dknet-domain-entity` | Create DDD entities with AggregateRoot/DomainEntity patterns |
| `dknet-efcore-config` | EF Core mapper configurations, seed data, and domain services |
| `dknet-appservices-actions` | CRUD actions with SlimMessageBus + FluentResults + Mapster |
| `dknet-endpoint-config` | Minimal API endpoints via IEndpointConfig with fluent helpers |
| `dknet-bdd-tests` | Reqnroll + NUnit BDD scenario development |
| `dknet-unit-test` | Integration tests with ApiFixture + IMessageBus |
| `dknet-feature-documentation` | Structured technical documentation with Mermaid diagrams |

## Architecture

```
Minimal.Api          -> entry point, endpoints, auth, OpenAPI
  |
Minimal.AppServices  -> CQRS handlers, validators, DTOs
  |
Minimal.Domains      -> entities, aggregate roots, repo interfaces
  ^
Minimal.Infra        -> EF Core, repos, event publisher, service bus

Minimal.Share        -> shared constants/options/base types
Minimal.AppHost      -> Aspire orchestration only
```

## Publishing

Run the publish script to validate, version, tag, and create a GitHub release:

```bash
# Validate only
./publish-claude-plugin.sh --dry-run

# Validate with claude CLI
./publish-claude-plugin.sh --validate

# Publish (auto-bump patch version, push, create GitHub release)
./publish-claude-plugin.sh
```

## License

MIT
