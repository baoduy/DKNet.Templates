#!/usr/bin/env bash
set -uo pipefail

# ──────────────────────────────────────────────────────────────────────────────
# validate-plugin.sh — Content validation for the DKNet plugin (DRK-185 §8)
#
# This is a content/config-only plugin (no compiled code), so the usual
# line-coverage / dotnet-pack bar does not apply. These 5 checks are the
# coverage-equivalent gate and must all pass on a clean tree:
#
#   1. manifest-consistency   — name+version agree everywhere; every path a
#                                manifest declares actually exists
#   2. no-foreign-reference   — no "Monxa"/"Mx.Pgw" in the DKNet plugin's core
#                                surface (the generic/third-party skill packs
#                                carved out of scope by DRK-196 §9 are excluded)
#   3. shared-guidance        — every core .claude/skills/<x> vs
#                                .github/skills/<x> pair is byte-identical,
#                                including file names
#   4. core-guidance-parity   — the core dknet-* skill set is present under
#                                both ecosystems
#   5. install-doc-complete   — every install channel the README advertises
#                                has a non-empty instruction
#
# Usage: ./validate-plugin.sh
# ──────────────────────────────────────────────────────────────────────────────

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

FAILURES=0
fail() { echo "  FAIL: $1"; FAILURES=$((FAILURES + 1)); }
pass() { echo "  ok: $1"; }

# The core DKNet skills shared between Claude Code and GitHub Copilot.
CORE_SKILLS=(
  dknet-appservices-actions
  dknet-bdd-tests
  dknet-ddd-principles
  dknet-domain-entity
  dknet-efcore-config
  dknet-endpoint-config
  dknet-feature-documentation
  dknet-package-adoption
  dknet-project-structure
  dknet-unit-test
)

# Generic/third-party guidance carved out of scope by DRK-196 §9 (architecture-kit,
# spec-kit, vector-search, graph-DB, general C# and Aspire skills, etc.) — not part
# of the DKNet plugin's core identity, so excluded from the no-foreign-reference scan.
OUT_OF_SCOPE_REGEX='^\./\.github/skills/(arckit-|csharp-|aspire-|qdrant-docsearch|falkordb-codegraph|database-performance|serialization|testcontainers|skills-index-snippets|_templates|feature-documentation/)|^\./\.github/skills/ARCHITECTURE-SKILLS-INDEX\.md:'

echo "=== 1. manifest-consistency ==="
NAMES=$(jq -r '.name' plugin.json .claude-plugin/plugin.json | sort -u)
if [[ "$(echo "$NAMES" | wc -l)" -eq 1 ]]; then pass "name agrees: $NAMES"; else fail "plugin name diverges: $NAMES"; fi

VERSIONS=$(
  {
    jq -r '.version' plugin.json .claude-plugin/plugin.json
    jq -r '.metadata.version' .claude-plugin/marketplace.json .github/plugin/marketplace.json
    jq -r '.plugins[0].version' .claude-plugin/marketplace.json .github/plugin/marketplace.json
    jq -r '.version' package.json
  } | sort -u
)
if [[ "$(echo "$VERSIONS" | wc -l)" -eq 1 ]]; then pass "version agrees: $VERSIONS"; else fail "version diverges across manifests: $VERSIONS"; fi

MARKETPLACE_NAMES=$(jq -r '.plugins[0].name' .claude-plugin/marketplace.json .github/plugin/marketplace.json | sort -u)
if [[ "$(echo "$MARKETPLACE_NAMES" | wc -l)" -eq 1 ]]; then pass "marketplace entries agree: $MARKETPLACE_NAMES"; else fail "marketplace plugin name diverges: $MARKETPLACE_NAMES"; fi

# Every path a manifest declares must exist.
declare -a DECLARED_PATHS=()
while IFS= read -r p; do DECLARED_PATHS+=("$p"); done < <(jq -r '.agents, .commands, .skills[]?' plugin.json)
while IFS= read -r p; do DECLARED_PATHS+=("$p"); done < <(jq -r '.agents, .commands, .skills' .claude-plugin/plugin.json)
while IFS= read -r p; do DECLARED_PATHS+=("$p"); done < <(jq -r '.plugins[0].skills[]?' .github/plugin/marketplace.json)
for p in "${DECLARED_PATHS[@]}"; do
  [[ "$p" == "null" ]] && continue
  if [[ -e "$p" ]]; then pass "manifest path exists: $p"; else fail "manifest declares missing path: $p"; fi
done

echo "=== 2. no-foreign-reference ==="
HITS=$(grep -rniE 'monxa|mx\.pgw' --exclude-dir=.git --exclude=validate-plugin.sh . 2>/dev/null | grep -vE "$OUT_OF_SCOPE_REGEX")
if [[ -z "$HITS" ]]; then
  pass "no Monxa/Mx.Pgw references in the core plugin surface"
else
  fail "foreign project reference(s) found in core plugin surface:"
  echo "$HITS" | sed 's/^/    /'
fi

echo "=== 3. shared-guidance consistency ==="
for s in "${CORE_SKILLS[@]}"; do
  d1=".claude/skills/$s"
  d2=".github/skills/$s"
  if [[ ! -d "$d1" || ! -d "$d2" ]]; then
    fail "$s: directory missing on one side (checked separately by core-guidance-parity)"
    continue
  fi
  DIFF=$(diff -rq "$d1" "$d2" 2>&1)
  if [[ -z "$DIFF" ]]; then
    pass "$s: byte-identical"
  else
    fail "$s: pair diverges"
    echo "$DIFF" | sed 's/^/    /'
  fi
done

echo "=== 4. core-guidance parity ==="
for s in "${CORE_SKILLS[@]}"; do
  in_claude=false; in_github=false
  [[ -d ".claude/skills/$s" ]] && in_claude=true
  [[ -d ".github/skills/$s" ]] && in_github=true
  if $in_claude && $in_github; then
    pass "$s present under both ecosystems"
  else
    fail "$s missing — claude=$in_claude github=$in_github"
  fi
done

echo "=== 5. install-doc completeness ==="
# Parse the "## AI Plugin" section of README.md: each **Channel** heading must be
# followed by a non-empty fenced code block.
AI_SECTION=$(awk '/^## AI Plugin/{flag=1; next} /^## /{flag=0} flag' README.md)
CHANNEL=""
IN_FENCE=false
FENCE_BODY=""
declare -A CHANNEL_BODY=()
while IFS= read -r line; do
  if [[ "$line" =~ ^\*\*(.+)\*\*$ ]]; then
    CHANNEL="${BASH_REMATCH[1]}"
    CHANNEL_BODY["$CHANNEL"]=""
  elif [[ "$line" == '```'* ]]; then
    if $IN_FENCE; then IN_FENCE=false; else IN_FENCE=true; fi
  elif $IN_FENCE && [[ -n "$CHANNEL" ]]; then
    CHANNEL_BODY["$CHANNEL"]+="$line"$'\n'
  fi
done <<< "$AI_SECTION"

if [[ ${#CHANNEL_BODY[@]} -eq 0 ]]; then
  fail "no install channels found under '## AI Plugin' in README.md"
fi
for ch in "${!CHANNEL_BODY[@]}"; do
  body="$(echo -n "${CHANNEL_BODY[$ch]}" | tr -d '[:space:]')"
  if [[ -n "$body" ]]; then
    pass "$ch: instruction present"
  else
    fail "$ch: advertised but has no install instruction (empty code fence) in README.md"
  fi
done

echo
if [[ $FAILURES -eq 0 ]]; then
  echo "ALL CHECKS PASSED"
  exit 0
else
  echo "$FAILURES CHECK(S) FAILED"
  exit 1
fi
