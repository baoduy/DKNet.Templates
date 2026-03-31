#!/usr/bin/env bash
set -euo pipefail

# ──────────────────────────────────────────────────────────────────────────────
# publish-plugin.sh — Validate, version, tag, and release the DKNet Copilot plugin
#
# Usage:
#   ./publish-plugin.sh                  # Auto-bump patch version (0.0.8 → 0.0.9)
#   ./publish-plugin.sh --minor          # Bump minor version   (0.0.8 → 0.1.0)
#   ./publish-plugin.sh --major          # Bump major version   (0.0.8 → 1.0.0)
#   ./publish-plugin.sh --version 1.2.3  # Set explicit version
#   ./publish-plugin.sh --dry-run        # Validate only, no git/gh changes
# ──────────────────────────────────────────────────────────────────────────────

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# ── Defaults ──────────────────────────────────────────────────────────────────
BUMP="patch"
EXPLICIT_VERSION=""
DRY_RUN=false
PLUGIN_JSON="plugin.json"
MARKETPLACE_JSON=".github/plugin/marketplace.json"

# ── Parse args ────────────────────────────────────────────────────────────────
while [[ $# -gt 0 ]]; do
  case "$1" in
    --major)   BUMP="major"; shift ;;
    --minor)   BUMP="minor"; shift ;;
    --patch)   BUMP="patch"; shift ;;
    --version) EXPLICIT_VERSION="$2"; shift 2 ;;
    --dry-run) DRY_RUN=true; shift ;;
    -h|--help)
      sed -n '3,10p' "$0" | sed 's/^# //' | sed 's/^#//'
      exit 0 ;;
    *) echo "Unknown option: $1"; exit 1 ;;
  esac
done

# ── Helpers ───────────────────────────────────────────────────────────────────
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m'

info()  { echo -e "${CYAN}[INFO]${NC}  $*"; }
ok()    { echo -e "${GREEN}[OK]${NC}    $*"; }
warn()  { echo -e "${YELLOW}[WARN]${NC}  $*"; }
err()   { echo -e "${RED}[ERROR]${NC} $*" >&2; }
fail()  { err "$*"; exit 1; }

# ── Prerequisite checks ──────────────────────────────────────────────────────
info "Checking prerequisites..."

command -v git >/dev/null 2>&1 || fail "git is required"
command -v gh  >/dev/null 2>&1 || fail "gh CLI is required (brew install gh)"
command -v jq  >/dev/null 2>&1 || fail "jq is required (brew install jq)"

git rev-parse --is-inside-work-tree >/dev/null 2>&1 || fail "Not a git repository"

# Verify gh is authenticated
gh auth status >/dev/null 2>&1 || fail "Not authenticated with gh. Run: gh auth login"

# Verify remote is GitHub
REMOTE_URL=$(git remote get-url origin 2>/dev/null) || fail "No origin remote configured"
[[ "$REMOTE_URL" == *github.com* ]] || fail "Remote is not GitHub: $REMOTE_URL"

# Extract owner/repo
if [[ "$REMOTE_URL" =~ github\.com[:/]([^/]+)/([^/.]+) ]]; then
  OWNER="${BASH_REMATCH[1]}"
  REPO="${BASH_REMATCH[2]}"
else
  fail "Cannot parse owner/repo from: $REMOTE_URL"
fi

ok "Repository: $OWNER/$REPO"

# ── Validate plugin structure ─────────────────────────────────────────────────
info "Validating plugin structure..."

ERRORS=0

validate_file() {
  if [[ ! -f "$1" ]]; then
    err "Missing required file: $1"
    ((ERRORS++))
    return 1
  fi
  return 0
}

validate_dir() {
  if [[ ! -d "$1" ]]; then
    err "Missing required directory: $1"
    ((ERRORS++))
    return 1
  fi
  return 0
}

# Required files
validate_file "$PLUGIN_JSON"
validate_file "$MARKETPLACE_JSON"
validate_file "AGENTS.md"

# Required directories
validate_dir ".github/agents"
validate_dir ".github/skills"

# Validate plugin.json structure
if [[ -f "$PLUGIN_JSON" ]]; then
  jq -e '.name' "$PLUGIN_JSON" >/dev/null 2>&1 || { err "plugin.json: missing 'name' field"; ((ERRORS++)); }
  jq -e '.description' "$PLUGIN_JSON" >/dev/null 2>&1 || { err "plugin.json: missing 'description' field"; ((ERRORS++)); }
  jq -e '.version' "$PLUGIN_JSON" >/dev/null 2>&1 || { err "plugin.json: missing 'version' field"; ((ERRORS++)); }
  jq -e '.skills' "$PLUGIN_JSON" >/dev/null 2>&1 || { err "plugin.json: missing 'skills' field"; ((ERRORS++)); }
fi

# Validate marketplace.json structure
if [[ -f "$MARKETPLACE_JSON" ]]; then
  jq -e '.name' "$MARKETPLACE_JSON" >/dev/null 2>&1 || { err "marketplace.json: missing 'name' field"; ((ERRORS++)); }
  jq -e '.plugins' "$MARKETPLACE_JSON" >/dev/null 2>&1 || { err "marketplace.json: missing 'plugins' array"; ((ERRORS++)); }
fi

# Validate agents have frontmatter
AGENT_COUNT=0
for f in .github/agents/*.agent.md; do
  [[ -f "$f" ]] || continue
  ((AGENT_COUNT++))
  head -1 "$f" | grep -q '^---' || { warn "Agent missing YAML frontmatter: $f"; }
done

# Validate skills have skill.md
SKILL_COUNT=0
for d in .github/skills/*/; do
  [[ -d "$d" ]] || continue
  DIRNAME=$(basename "$d")
  [[ "$DIRNAME" == _* ]] && continue  # Skip _templates etc.
  ((SKILL_COUNT++))
  [[ -f "${d}skill.md" ]] || { err "Skill missing skill.md: $d"; ((ERRORS++)); }
done

if [[ $ERRORS -gt 0 ]]; then
  fail "Validation failed with $ERRORS error(s). Fix the issues above and retry."
fi

ok "Plugin structure valid: $AGENT_COUNT agents, $SKILL_COUNT skills"

# ── Version management ────────────────────────────────────────────────────────
CURRENT_VERSION=$(jq -r '.version' "$PLUGIN_JSON")
info "Current version: $CURRENT_VERSION"

if [[ -n "$EXPLICIT_VERSION" ]]; then
  NEW_VERSION="$EXPLICIT_VERSION"
else
  IFS='.' read -r MAJOR MINOR PATCH <<< "$CURRENT_VERSION"
  case "$BUMP" in
    major) NEW_VERSION="$((MAJOR + 1)).0.0" ;;
    minor) NEW_VERSION="${MAJOR}.$((MINOR + 1)).0" ;;
    patch) NEW_VERSION="${MAJOR}.${MINOR}.$((PATCH + 1))" ;;
  esac
fi

info "New version: $NEW_VERSION"

# ── Check for uncommitted changes ────────────────────────────────────────────
if [[ -n $(git status --porcelain) ]]; then
  warn "Working directory has uncommitted changes."
  if [[ "$DRY_RUN" == false ]]; then
    read -rp "Continue anyway? Version files will be staged and committed. [y/N] " REPLY
    [[ "$REPLY" =~ ^[Yy]$ ]] || { info "Aborted."; exit 0; }
  fi
fi

# ── Check if tag already exists ───────────────────────────────────────────────
TAG="v${NEW_VERSION}"
if git rev-parse "$TAG" >/dev/null 2>&1; then
  fail "Tag $TAG already exists. Choose a different version."
fi

# ── Dry run stops here ───────────────────────────────────────────────────────
if [[ "$DRY_RUN" == true ]]; then
  echo ""
  ok "Dry run complete. Summary:"
  echo "  Plugin:     $PLUGIN_JSON (valid)"
  echo "  Marketplace: $MARKETPLACE_JSON (valid)"
  echo "  Agents:     $AGENT_COUNT"
  echo "  Skills:     $SKILL_COUNT"
  echo "  Version:    $CURRENT_VERSION → $NEW_VERSION"
  echo "  Tag:        $TAG"
  echo "  Repository: $OWNER/$REPO"
  exit 0
fi

# ── Update version in plugin.json and marketplace.json ────────────────────────
info "Updating version to $NEW_VERSION..."

# Update plugin.json
jq --arg v "$NEW_VERSION" '.version = $v' "$PLUGIN_JSON" > "${PLUGIN_JSON}.tmp" \
  && mv "${PLUGIN_JSON}.tmp" "$PLUGIN_JSON"

# Update marketplace.json (top-level metadata and each plugin entry)
jq --arg v "$NEW_VERSION" '
  .metadata.version = $v |
  .plugins[].version = $v
' "$MARKETPLACE_JSON" > "${MARKETPLACE_JSON}.tmp" \
  && mv "${MARKETPLACE_JSON}.tmp" "$MARKETPLACE_JSON"

ok "Version updated in plugin.json and marketplace.json"

# ── Build release notes ───────────────────────────────────────────────────────
info "Building release notes..."

# Get commits since last tag (or all commits if no prior tag)
LAST_TAG=$(git describe --tags --abbrev=0 2>/dev/null || echo "")
if [[ -n "$LAST_TAG" ]]; then
  COMMITS=$(git log "${LAST_TAG}..HEAD" --oneline --no-merges 2>/dev/null || echo "Initial release")
else
  COMMITS=$(git log --oneline --no-merges -20 2>/dev/null || echo "Initial release")
fi

# List agents and skills for release notes
AGENT_LIST=""
for f in .github/agents/*.agent.md; do
  [[ -f "$f" ]] || continue
  NAME=$(grep -m1 '^name:' "$f" 2>/dev/null | sed 's/name: *//' | tr -d '"' || basename "$f" .agent.md)
  DESC=$(grep -m1 '^description:' "$f" 2>/dev/null | sed 's/description: *//' | tr -d '"' | cut -c1-80 || echo "")
  AGENT_LIST+="- **${NAME}** — ${DESC}"$'\n'
done

SKILL_LIST=""
for d in .github/skills/*/; do
  [[ -d "$d" ]] || continue
  DIRNAME=$(basename "$d")
  [[ "$DIRNAME" == _* ]] && continue
  DESC=""
  if [[ -f "${d}skill.md" ]]; then
    DESC=$(grep -m1 '^description:' "${d}skill.md" 2>/dev/null | sed 's/description: *//' | tr -d '"' | cut -c1-80 || echo "")
  fi
  SKILL_LIST+="- **${DIRNAME}** — ${DESC}"$'\n'
done

RELEASE_NOTES=$(cat <<EOF
## DKNet Copilot Plugin v${NEW_VERSION}

GitHub Copilot skills and agents for scaffolding production-ready .NET 10 microservices using DKNet.Minimal.Template.

### Agents ($AGENT_COUNT)

${AGENT_LIST}
### Skills ($SKILL_COUNT)

${SKILL_LIST}
### Installation

Clone this repository — Copilot automatically discovers agents in \`.github/agents/\` and skills in \`.github/skills/\`.

For Claude Code, use the slash commands in \`.claude/commands/\` (e.g., \`/project:dknet-developer\`).

### Changes since ${LAST_TAG:-initial}

\`\`\`
${COMMITS}
\`\`\`
EOF
)

# ── Commit, tag, push, release ────────────────────────────────────────────────
info "Committing version bump..."
git add "$PLUGIN_JSON" "$MARKETPLACE_JSON"
git commit -m "chore: bump plugin version to ${NEW_VERSION}" || info "Nothing to commit (version files unchanged)"

info "Creating tag $TAG..."
git tag -a "$TAG" -m "Release ${NEW_VERSION}"

info "Pushing to origin..."
git push origin HEAD
git push origin "$TAG"

ok "Pushed to GitHub"

info "Creating GitHub release..."
gh release create "$TAG" \
  --repo "$OWNER/$REPO" \
  --title "v${NEW_VERSION}" \
  --notes "$RELEASE_NOTES"

ok "GitHub release created: v${NEW_VERSION}"

# ── Summary ───────────────────────────────────────────────────────────────────
echo ""
echo "══════════════════════════════════════════════════════════"
echo -e " ${GREEN}Plugin published successfully!${NC}"
echo "══════════════════════════════════════════════════════════"
echo ""
echo "  Version:  $NEW_VERSION"
echo "  Tag:      $TAG"
echo "  Agents:   $AGENT_COUNT"
echo "  Skills:   $SKILL_COUNT"
echo "  Release:  https://github.com/$OWNER/$REPO/releases/tag/$TAG"
echo ""
echo "  Users can access your plugin by cloning/forking:"
echo "    git clone https://github.com/$OWNER/$REPO.git"
echo ""
