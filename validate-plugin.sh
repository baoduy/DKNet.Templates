#!/usr/bin/env bash
set -uo pipefail

# ──────────────────────────────────────────────────────────────────────────────
# validate-plugin.sh — Content validation for the dknet-minimal plugin
#
# The repository root IS the plugin: `.claude-plugin/plugin.json` + `skills/` +
# `agents/` (Claude Code), `plugin.json` (GitHub Copilot), `package.json` (npm,
# `npx skills add`). This is content only (no compiled code), so these checks are
# the coverage-equivalent gate and must all pass on a clean tree:
#
#   1. manifest-consistency  — name agrees across manifests; every declared path
#                               exists; versions are the 0.0.0 placeholder the
#                               release pipeline stamps; `claude plugin validate`
#                               passes in strict mode when the CLI is installed
#   2. no-foreign-reference  — no "Monxa"/"Mx.Pgw" anywhere in the plugin surface
#   3. install-doc-complete  — every install channel the README advertises has a
#                               non-empty instruction
#   4. skill-portability     — every skills/<x>/SKILL.md is valid for Claude Code,
#                               GitHub Copilot and `npx skills add`: frontmatter
#                               name matches its folder, single-line description
#                               <= 1024 chars, only Agent Skills spec frontmatter
#                               keys, <= 500 lines, `agentskills validate` (when uvx
#                               is installed), and
#                               no path that is wrong in a generated consumer
#                               solution (src/ prefix, .claude/ or .github/ file
#                               refs, links into this repo's docs/, *.sh scripts,
#                               the solution file name)
#
# Usage: ./validate-plugin.sh
# ──────────────────────────────────────────────────────────────────────────────

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

FAILURES=0
fail() { echo "  FAIL: $1"; FAILURES=$((FAILURES + 1)); }
pass() { echo "  ok: $1"; }

SKILLS=()
for d in skills/*/; do
  [[ -d "$d" ]] && SKILLS+=("$(basename "$d")")
done

echo "=== 1. manifest-consistency ==="
NAMES=$(jq -r '.name' plugin.json .claude-plugin/plugin.json | sort -u)
if [[ "$(echo "$NAMES" | wc -l)" -eq 1 ]]; then pass "plugin name agrees: $NAMES"; else fail "plugin name diverges: $NAMES"; fi
MP_NAME=$(jq -r '.plugins[0].name' .claude-plugin/marketplace.json)
if [[ "$MP_NAME" == "$NAMES" ]]; then pass "marketplace entry names the plugin"; else fail "marketplace plugin name '$MP_NAME' != '$NAMES'"; fi

VERSIONS=$(jq -r '.version' plugin.json .claude-plugin/plugin.json package.json | sort -u)
if [[ "$(echo "$VERSIONS" | wc -l)" -eq 1 ]]; then
  pass "version agrees across plugin.json, .claude-plugin/plugin.json, package.json: $VERSIONS (0.0.0 in git; the publish pipeline stamps the release)"
else
  fail "version diverges across manifests (run: npm version <x.y.z> --no-git-tag-version): $(echo "$VERSIONS" | tr '\n' ' ')"
fi

declare -a DECLARED_PATHS=()
while IFS= read -r p; do DECLARED_PATHS+=("$p"); done < <(jq -r '.agents, .skills[]?' plugin.json)
while IFS= read -r p; do DECLARED_PATHS+=("$p"); done < <(jq -r '(.agents // empty | if type == "array" then .[] else . end), (.skills // empty | if type == "array" then .[] else . end)' .claude-plugin/plugin.json)
while IFS= read -r p; do DECLARED_PATHS+=("$p"); done < <(jq -r '.files[]' package.json)
for p in "${DECLARED_PATHS[@]}"; do
  [[ -z "$p" || "$p" == "null" ]] && continue
  if [[ -e "$p" ]]; then pass "declared path exists: $p"; else fail "manifest declares missing path: $p"; fi
done
[[ -d skills ]] || fail "skills/ directory missing (Claude Code default skills dir)"
[[ -d agents ]] || fail "agents/ directory missing"

if command -v claude >/dev/null 2>&1; then
  for target in . skills agents; do
    if OUT=$(claude plugin validate "$target" --strict 2>&1); then
      pass "claude plugin validate $target --strict"
    else
      fail "claude plugin validate $target --strict reported problems:"
      echo "$OUT" | sed 's/^/    /'
    fi
  done
fi

echo "=== 2. no-foreign-reference ==="
HITS=$(grep -rniE 'monxa|mx\.pgw' --exclude-dir=.git --exclude=validate-plugin.sh . 2>/dev/null)
if [[ -z "$HITS" ]]; then
  pass "no Monxa/Mx.Pgw references"
else
  fail "foreign project reference(s) found:"
  echo "$HITS" | sed 's/^/    /'
fi

echo "=== 3. install-doc completeness ==="
# Parse the "## AI Plugin" section of README.md: each **Channel** heading must be
# followed by a non-empty fenced code block.
AI_SECTION=$(awk '/^## AI Plugin/{flag=1; next} /^## /{flag=0} flag' README.md)
CHANNEL=""
HAS_BODY=false
IN_FENCE=false
CHANNEL_COUNT=0

report_channel() {
  [[ -z "$1" ]] && return
  if [[ "$2" == "true" ]]; then
    pass "$1: instruction present"
  else
    fail "$1: advertised but has no install instruction (empty code fence) in README.md"
  fi
}

while IFS= read -r line; do
  if [[ "$line" =~ ^\*\*(.+)\*\*$ ]]; then
    report_channel "$CHANNEL" "$HAS_BODY"
    CHANNEL="${BASH_REMATCH[1]}"
    HAS_BODY=false
    IN_FENCE=false
    CHANNEL_COUNT=$((CHANNEL_COUNT + 1))
  elif [[ "$line" == '```'* ]]; then
    if $IN_FENCE; then IN_FENCE=false; else IN_FENCE=true; fi
  elif $IN_FENCE && [[ -n "$CHANNEL" ]] && [[ -n "$(echo "$line" | tr -d '[:space:]')" ]]; then
    HAS_BODY=true
  fi
done <<< "$AI_SECTION"
report_channel "$CHANNEL" "$HAS_BODY"
[[ $CHANNEL_COUNT -eq 0 ]] && fail "no install channels found under '## AI Plugin' in README.md"

echo "=== 4. skill-portability ==="
# A skill is copied verbatim into other repositories (plugin cache, `npx skills add`
# targets, node_modules), so its frontmatter must be self-describing and its body
# must only carry paths that exist in a generated consumer solution.
[[ ${#SKILLS[@]} -eq 0 ]] && fail "no skills found under skills/"
for s in "${SKILLS[@]}"; do
  f="skills/$s/SKILL.md"
  if [[ ! -f "$f" ]]; then fail "$s: SKILL.md missing"; continue; fi
  if [[ "$(head -1 "$f")" != "---" ]]; then fail "$s: SKILL.md must start with a --- frontmatter block"; continue; fi
  FM=$(awk 'NR==1{next} /^---$/{exit} {print}' "$f")
  NAME=$(echo "$FM" | sed -n 's/^name:[[:space:]]*//p' | head -1 | tr -d '"'"'"'')
  DESC=$(echo "$FM" | sed -n 's/^description:[[:space:]]*//p' | head -1)
  if [[ "$NAME" != "$s" ]]; then fail "$s: frontmatter name '$NAME' must equal the folder name"; fi
  if [[ -z "$DESC" || "$DESC" == ">-" || "$DESC" == "|" ]]; then
    fail "$s: description must be a single-line string (block scalars break some agents)"
  elif [[ ${#DESC} -gt 1024 ]]; then
    fail "$s: description is ${#DESC} chars (max 1024)"
  fi
  UNKNOWN=$(echo "$FM" | grep -E '^[a-zA-Z_-]+:' | sed 's/:.*//' | grep -vE '^(name|description|allowed-tools|license|compatibility|metadata)$' || true)
  [[ -n "$UNKNOWN" ]] && fail "$s: unexpected frontmatter key(s): $(echo "$UNKNOWN" | tr '\n' ' ')"

  LINES=$(wc -l < "$f")
  [[ "$LINES" -le 500 ]] || fail "$s: SKILL.md has $LINES lines (max 500 — the release pipeline enforces this)"
  if command -v uvx >/dev/null 2>&1; then
    OUT=$(uvx --from skills-ref agentskills validate "skills/$s" 2>&1) || fail "$s: agentskills validate failed: $(echo "$OUT" | tail -3 | tr '\n' ' ')"
  fi

  BAD=$(grep -rnE 'src/ApiEndpoints|`src/|\(src/|\.claude/skills/|\.github/(skills|agents|prompts)/|docs/(samples|[a-z-]+\.md)|add-migration\.sh|remove-migration\.sh|DKNet\.Templates\.sln' "skills/$s" --include=*.md 2>/dev/null || true)
  if [[ -z "$BAD" ]]; then
    pass "$s: portable (frontmatter + paths)"
  else
    fail "$s: body carries paths that do not exist in a generated solution:"
    echo "$BAD" | head -10 | sed 's/^/    /'
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
