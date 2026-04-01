#!/usr/bin/env bash
# ---------------------------------------------------------------
# Spin up Neo4j via docker compose, load seed.cypher,
# then optionally run the live code analyzer.
#
# Usage:
#   ./graph/load.sh                   # load graph/seed.cypher
#   ./graph/load.sh graph/my.cypher   # load a custom Cypher file
#   ./graph/load.sh --analyze         # seed + re-analyze live source
# ---------------------------------------------------------------
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
CYPHER_FILE="${REPO_ROOT}/graph/seed.cypher"
ANALYZE=false

# Parse args
for arg in "$@"; do
  case "$arg" in
    --analyze) ANALYZE=true ;;
    --*)       echo "Unknown flag: $arg"; exit 1 ;;
    *)         CYPHER_FILE="$arg" ;;
  esac
done

CONTAINER="dknet-neo4j"
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
if [[ ! -f "${REPO_ROOT}/docker-compose.yml" ]]; then
  echo "ERROR: docker-compose.yml not found in ${REPO_ROOT}" >&2; exit 1
fi
if [[ ! -f "${CYPHER_FILE}" ]]; then
  echo "ERROR: Cypher file not found: ${CYPHER_FILE}" >&2; exit 1
fi

# ── 2. Start docker compose ──────────────────────────────────────────────────
cd "${SCRIPT_DIR}"

RUNNING=$(docker ps --format '{{.Names}}' | grep -c "^${CONTAINER}$" || true)
if [[ "${RUNNING}" -eq 0 ]]; then
  info "Starting docker compose (neo4j)..."
  docker compose up -d
else
  info "Containers already running — skipping docker compose up."
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

# ── 4. Load the Cypher seed ───────────────────────────────────────────────────
info "Loading ${CYPHER_FILE}..."
docker exec -i "${CONTAINER}" \
  cypher-shell \
    -u "${NEO4J_USER}" \
    -p "${NEO4J_PASS}" \
    --format plain \
  < "${CYPHER_FILE}"
success "Seed loaded."

# ── 5. Optionally run the live analyzer ──────────────────────────────────────
if [[ "${ANALYZE}" == true ]]; then
  if ! command -v node &>/dev/null; then
    warn "node not found — skipping live analysis. Install Node.js >= 18 to use --analyze."
  else
    info "Running live source analyzer..."
    node "${SCRIPT_DIR}/analyze.mjs"
  fi
fi

# ── 6. Print summary ─────────────────────────────────────────────────────────
echo ""
echo -e "${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${RESET}"
success "All done!"
echo ""
echo -e "  ${CYAN}Neo4j Browser${RESET}  → ${NEO4J_HTTP}  (neo4j / ${NEO4J_PASS})"
echo ""
echo "  Re-analyze live source any time:"
echo "    node graph/analyze.mjs"
echo ""
echo "  Load a custom Cypher file:"
echo "    ./graph/load.sh path/to/file.cypher"
echo ""
echo "  Seed + analyze in one step:"
echo "    ./graph/load.sh --analyze"
echo -e "${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${RESET}"
