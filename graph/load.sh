#!/usr/bin/env bash
# ---------------------------------------------------------------
# Spin up Neo4j via docker compose and build graph index from source.
#
# Default behavior:
#   1) start Neo4j
#   2) optionally reset graph
#   3) run analyze.mjs (dynamic index based on current repo structure)
#
# Usage:
#   ./graph/load.sh
#   ./graph/load.sh --reset
#   ./graph/load.sh --no-analyze
# ---------------------------------------------------------------
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
REPO_NAME="$(basename "${REPO_ROOT}")"
REPO_SLUG="$(echo "${REPO_NAME}" | tr '[:upper:]' '[:lower:]' | sed -E 's/[^a-z0-9]+/-/g; s/^-+//; s/-+$//')"
ANALYZE=true
RESET_GRAPH=false

# Parse args
while [[ $# -gt 0 ]]; do
  case "$1" in
    --analyze) ANALYZE=true; shift ;;
    --no-analyze) ANALYZE=false; shift ;;
    --reset) RESET_GRAPH=true; shift ;;
    --*)
      echo "Unknown flag: $1" >&2
      exit 1
      ;;
    *)
      echo "Unknown argument: $1" >&2
      exit 1
      ;;
  esac
done

CONTAINER="${REPO_SLUG}-neo4j"
export NEO4J_CONTAINER_NAME="${CONTAINER}"
NEO4J_USER="neo4j"
NEO4J_PASS="codegraph123"
NEO4J_HTTP="http://localhost:7474"

# ── Colours ──────────────────────────────────────────────────────────────────
GREEN='\033[0;32m'; YELLOW='\033[1;33m'; CYAN='\033[0;36m'; RESET='\033[0m'
info()    { echo -e "${CYAN}▶ $*${RESET}"; }
success() { echo -e "${GREEN}✔ $*${RESET}"; }
warn()    { echo -e "${YELLOW}⚠ $*${RESET}"; }

# ── 1. Sanity checks ─────────────────────────────────────────────────────────
if ! command -v docker &>/dev/null; then
  echo "ERROR: docker is not installed or not in PATH." >&2; exit 1
fi
if [[ ! -f "${SCRIPT_DIR}/docker-compose.yml" ]]; then
  echo "ERROR: docker-compose.yml not found in ${SCRIPT_DIR}" >&2; exit 1
fi
if [[ "${ANALYZE}" == true ]] && ! command -v node &>/dev/null; then
  echo "ERROR: node not found. Install Node.js >= 18 for analyzer indexing." >&2; exit 1
fi

# ── 2. Start docker compose ──────────────────────────────────────────────────
cd "${SCRIPT_DIR}"

RUNNING=$(docker ps --format '{{.Names}}' | grep -c "^${CONTAINER}$" || true)
if [[ "${RUNNING}" -eq 0 ]]; then
  info "Starting docker compose (neo4j: ${CONTAINER})..."
  docker compose up -d
else
  info "Container ${CONTAINER} already running — skipping docker compose up."
fi

# ── 3. Wait for Neo4j to be ready ────────────────────────────────────────────
info "Waiting for Neo4j to be ready..."
MAX_WAIT=120   # seconds
ELAPSED=0
INTERVAL=5

until curl -sf -o /dev/null "${NEO4J_HTTP}"; do
  if [[ "${ELAPSED}" -ge "${MAX_WAIT}" ]]; then
    echo ""
    echo "ERROR: Neo4j did not become ready within ${MAX_WAIT}s." >&2
    echo "Check logs with: docker compose logs neo4j" >&2
    exit 1
  fi
  printf '.'
  sleep "${INTERVAL}"
  ELAPSED=$((ELAPSED + INTERVAL))
done
echo ""
success "Neo4j is ready (${ELAPSED}s)"

# ── 4. Optional reset + dynamic analyzer ─────────────────────────────────────
if [[ "${RESET_GRAPH}" == true ]]; then
  info "Resetting graph (MATCH (n) DETACH DELETE n)..."
  docker exec -i "${CONTAINER}" \
    cypher-shell \
      -u "${NEO4J_USER}" \
      -p "${NEO4J_PASS}" \
      --format plain \
      "MATCH (n) DETACH DELETE n;"
  success "Graph reset complete."
fi

if [[ "${ANALYZE}" == true ]]; then
  info "Running dynamic source analyzer..."
  node "${SCRIPT_DIR}/analyze.mjs"
  success "Dynamic indexing complete."
else
  warn "Analyzer skipped (--no-analyze)."
fi

# ── 5. Print summary ─────────────────────────────────────────────────────────
echo ""
echo -e "${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${RESET}"
success "All done!"
echo ""
echo -e "  ${CYAN}Neo4j Browser${RESET}  → ${NEO4J_HTTP}  (neo4j / ${NEO4J_PASS})"
echo ""
echo "  Rebuild index from live source any time:"
echo "    node graph/analyze.mjs"
echo ""
echo "  Reset then index:"
echo "    ./graph/load.sh --reset"
echo ""
echo "  Start DB only (skip analyzer):"
echo "    ./graph/load.sh --no-analyze"
echo -e "${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${RESET}"
