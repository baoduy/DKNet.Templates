#!/usr/bin/env bash
set -euo pipefail

# ──────────────────────────────────────────────────────────────────────────────
# publish-claude-plugin.sh — Validate, version, and publish the DKNet Claude
#                            Code plugin as a marketplace on GitHub
#
# Usage:
#   ./publish-claude-plugin.sh                  # Auto-bump patch version
#   ./publish-claude-plugin.sh --minor          # Bump minor version
#   ./publish-claude-plugin.sh --major          # Bump major version
#   ./publish-claude-plugin.sh --version 1.2.3  # Set explicit version
#   ./publish-claude-plugin.sh --dry-run        # Validate only, no changes
#   ./publish-claude-plugin.sh --validate       # Run claude plugin validate
# ──────────────────────────────────────────────────────────────────────────────

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PLUGIN_DIR="$SCRIPT_DIR/claude-code-plugin"

BUMP="patch"
EXPLICIT_VERSION=""
DRY_RUN=false
VALIDATE_ONLY=false

# ── Parse args ────────────────────────────────────────────────────────────────
while [[ $# -gt 0 ]]; do
  case "$1" in
    --major)    BUMP="major"; shift ;;
    --minor)    BUMP="minor"; shift ;;
    --patch)    BUMP="patch"; shift ;;
    --version)  EXPLICIT_VERSION="$2"; shift 2 ;;
    --dry-run)  DRY_RUN=true; shift ;;
    --validate) VALIDATE_ONLY=true; shift ;;
    -h|--help)
      sed -n '3,11p' "$0" | sed 's/^# //' | sed 's/^#//'
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
[[ -d "$PLUGIN_DIR" ]] || fail "Plugin directory not found: $PLUGIN_DIR"

PLUGIN_JSON="$PLUGIN_DIR/.claude-plugin/plugin.json"
MARKETPLACE_JSON="$PLUGIN_DIR/.claude-plugin/marketplace.json"

# ── Validate plugin structure ─────────────────────────────────────────────────
info "Validating Claude Code plugin structure..."

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
validate_file "$PLUGIN_DIR/README.md"

# Required directories
validate_dir "$PLUGIN_DIR/.claude-plugin"
validate_dir "$PLUGIN_DIR/commands"
validate_dir "$PLUGIN_DIR/agents"
validate_dir "$PLUGIN_DIR/skills"

# Validate plugin.json structure
if [[ -f "$PLUGIN_JSON" ]]; then
  jq -e '.name' "$PLUGIN_JSON" >/dev/null 2>&1 || { err "plugin.json: missing 'name' field"; ((ERRORS++)); }
  jq -e '.description' "$PLUGIN_JSON" >/dev/null 2>&1 || { err "plugin.json: missing 'description' field"; ((ERRORS++)); }
  jq -e '.version' "$PLUGIN_JSON" >/dev/null 2>&1 || { err "plugin.json: missing 'version' field"; ((ERRORS++)); }
fi

# Validate marketplace.json structure
if [[ -f "$MARKETPLACE_JSON" ]]; then
  jq -e '.name' "$MARKETPLACE_JSON" >/dev/null 2>&1 || { err "marketplace.json: missing 'name' field"; ((ERRORS++)); }
  jq -e '.owner' "$MARKETPLACE_JSON" >/dev/null 2>&1 || { err "marketplace.json: missing 'owner' field"; ((ERRORS++)); }
  jq -e '.plugins' "$MARKETPLACE_JSON" >/dev/null 2>&1 || { err "marketplace.json: missing 'plugins' array"; ((ERRORS++)); }
fi

# Validate commands have frontmatter
COMMAND_COUNT=0
for f in "$PLUGIN_DIR"/commands/*.md; do
  [[ -f "$f" ]] || continue
  ((COMMAND_COUNT++))
  if ! head -1 "$f" | grep -q '^---'; then
    warn "Command missing YAML frontmatter: $(basename "$f")"
  fi
done

# Validate agents have frontmatter
AGENT_COUNT=0
for f in "$PLUGIN_DIR"/agents/*.md; do
  [[ -f "$f" ]] || continue
  ((AGENT_COUNT++))
  if ! head -1 "$f" | grep -q '^---'; then
    warn "Agent missing YAML frontmatter: $(basename "$f")"
  fi
done

# Validate skills have SKILL.md
SKILL_COUNT=0
for d in "$PLUGIN_DIR"/skills/*/; do
  [[ -d "$d" ]] || continue
  DIRNAME=$(basename "$d")
  [[ "$DIRNAME" == _* ]] && continue
  ((SKILL_COUNT++))
  if [[ ! -f "${d}SKILL.md" ]]; then
    err "Skill missing SKILL.md: $d"
    ((ERRORS++))
  fi
done

if [[ $ERRORS -gt 0 ]]; then
  fail "Validation failed with $ERRORS error(s). Fix the issues above and retry."
fi

ok "Plugin structure valid: $COMMAND_COUNT commands, $AGENT_COUNT agents, $SKILL_COUNT skills"

# ── Optional: Run claude plugin validate ─────────────────────────────────────
if [[ "$VALIDATE_ONLY" == true ]]; then
  if command -v claude >/dev/null 2>&1; then
    info "Running claude plugin validate..."
    claude plugin validate "$PLUGIN_DIR" || warn "Validation returned warnings"
  else
    warn "claude CLI not found, skipping claude plugin validate"
  fi
  echo ""
  ok "Validation complete. Summary:"
  echo "  Commands:  $COMMAND_COUNT"
  echo "  Agents:    $AGENT_COUNT"
  echo "  Skills:    $SKILL_COUNT"
  exit 0
fi

# ── Version management ───────────────────────────────────────────────────────
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

# ── Extract repo info ────────────────────────────────────────────────────────
REMOTE_URL=$(git remote get-url origin 2>/dev/null) || fail "No origin remote configured"
[[ "$REMOTE_URL" == *github.com* ]] || fail "Remote is not GitHub: $REMOTE_URL"

if [[ "$REMOTE_URL" =~ github\.com[:/]([^/]+)/([^/.]+) ]]; then
  OWNER="${BASH_REMATCH[1]}"
  REPO="${BASH_REMATCH[2]}"
else
  fail "Cannot parse owner/repo from: $REMOTE_URL"
fi

ok "Repository: $OWNER/$REPO"

# ── Dry run stops here ───────────────────────────────────────────────────────
if [[ "$DRY_RUN" == true ]]; then
  echo ""
  ok "Dry run complete. Summary:"
  echo "  Plugin:       dknet-plugin"
  echo "  Version:      $CURRENT_VERSION → $NEW_VERSION"
  echo "  Commands:     $COMMAND_COUNT"
  echo "  Agents:       $AGENT_COUNT"
  echo "  Skills:       $SKILL_COUNT"
  echo "  Repository:   $OWNER/$REPO"
  echo "  Tag:          plugin-v${NEW_VERSION}"
  echo ""
  echo "  After publishing, users install with:"
  echo "    /plugin marketplace add $OWNER/$REPO"
  echo "    /plugin install dknet-plugin@dknet-plugins"
  exit 0
fi

# ── Update version in plugin.json and marketplace.json ───────────────────────
info "Updating version to $NEW_VERSION..."

jq --arg v "$NEW_VERSION" '.version = $v' "$PLUGIN_JSON" > "${PLUGIN_JSON}.tmp" \
  && mv "${PLUGIN_JSON}.tmp" "$PLUGIN_JSON"

jq --arg v "$NEW_VERSION" '
  .metadata.version = $v |
  .plugins[].version = $v
' "$MARKETPLACE_JSON" > "${MARKETPLACE_JSON}.tmp" \
  && mv "${MARKETPLACE_JSON}.tmp" "$MARKETPLACE_JSON"

ok "Version updated in plugin.json and marketplace.json"

# ── Check for uncommitted changes ────────────────────────────────────────────
if [[ -n $(git status --porcelain) ]]; then
  warn "Working directory has uncommitted changes."
  read -rp "Continue? Version files will be staged and committed. [y/N] " REPLY
  [[ "$REPLY" =~ ^[Yy]$ ]] || { info "Aborted."; exit 0; }
fi

# ── Check if tag already exists ──────────────────────────────────────────────
TAG="plugin-v${NEW_VERSION}"
if git rev-parse "$TAG" >/dev/null 2>&1; then
  fail "Tag $TAG already exists. Choose a different version."
fi

# ── Build release notes ──────────────────────────────────────────────────────
info "Building release notes..."

LAST_TAG=$(git describe --tags --abbrev=0 --match "plugin-v*" 2>/dev/null || echo "")
if [[ -n "$LAST_TAG" ]]; then
  COMMITS=$(git log "${LAST_TAG}..HEAD" --oneline --no-merges -- claude-code-plugin/ 2>/dev/null || echo "Initial release")
else
  COMMITS=$(git log --oneline --no-merges -20 -- claude-code-plugin/ 2>/dev/null || echo "Initial release")
fi

RELEASE_NOTES=$(cat <<EOF
## DKNet Claude Code Plugin v${NEW_VERSION}

Skills and agents for scaffolding production-ready .NET 10 microservices using DKNet.Minimal.Template.

### Contents

- **Commands**: $COMMAND_COUNT slash commands
- **Agents**: $AGENT_COUNT specialized agents
- **Skills**: $SKILL_COUNT development skills

### Installation

\`\`\`
# Add the marketplace
/plugin marketplace add $OWNER/$REPO --path claude-code-plugin

# Install the plugin
/plugin install dknet-plugin@dknet-plugins

# Or test locally
claude --plugin-dir ./claude-code-plugin
\`\`\`

### Submit to Official Marketplace

To make this plugin available on the official Claude AI marketplace:
1. Go to https://claude.ai/settings/plugins/submit
2. Submit the GitHub repository: $OWNER/$REPO

### Changes since ${LAST_TAG:-initial}

\`\`\`
${COMMITS}
\`\`\`
EOF
)

# ── Commit, tag, push, release ───────────────────────────────────────────────
info "Committing version bump..."
git add "$PLUGIN_JSON" "$MARKETPLACE_JSON"
git commit -m "chore: bump claude plugin version to ${NEW_VERSION}" || info "Nothing to commit"

info "Creating tag $TAG..."
git tag -a "$TAG" -m "Claude Code Plugin Release ${NEW_VERSION}"

info "Pushing to origin..."
git push origin HEAD
git push origin "$TAG"

ok "Pushed to GitHub"

info "Creating GitHub release..."
gh release create "$TAG" \
  --repo "$OWNER/$REPO" \
  --title "Claude Plugin v${NEW_VERSION}" \
  --notes "$RELEASE_NOTES"

ok "GitHub release created: $TAG"

# ── Summary ──────────────────────────────────────────────────────────────────
echo ""
echo "══════════════════════════════════════════════════════════"
echo -e " ${GREEN}Claude Code plugin published successfully!${NC}"
echo "══════════════════════════════════════════════════════════"
echo ""
echo "  Plugin:    dknet-plugin"
echo "  Version:   $NEW_VERSION"
echo "  Tag:       $TAG"
echo "  Commands:  $COMMAND_COUNT"
echo "  Agents:    $AGENT_COUNT"
echo "  Skills:    $SKILL_COUNT"
echo "  Release:   https://github.com/$OWNER/$REPO/releases/tag/$TAG"
echo ""
echo "  ── How users install ──────────────────────────────────"
echo ""
echo "  Option 1: Marketplace (recommended)"
echo "    /plugin marketplace add $OWNER/$REPO"
echo "    /plugin install dknet-plugin@dknet-plugins"
echo ""
echo "  Option 2: Local testing"
echo "    claude --plugin-dir ./claude-code-plugin"
echo ""
echo "  Option 3: Submit to official Claude AI marketplace"
echo "    https://claude.ai/settings/plugins/submit"
echo "    https://platform.claude.com/plugins/submit"
echo ""
