#!/usr/bin/env bash
# ---------------------------------------------------------------
# Build graph and/or vector indexes from source.
#
# Services: FalkorDB (code graph), Qdrant (doc vectors).
# Embeddings via ONNX Runtime (all-MiniLM-L6-v2) — no external
# embedding service required.
#
# Usage:
#   ./graph/load.sh                    # run both graph + vector
#   ./graph/load.sh --graph            # graph only
#   ./graph/load.sh --vector           # vector only
#   ./graph/load.sh --purge            # purge all data + rebuild
#   ./graph/load.sh --purge --graph    # purge + rebuild graph only
#   ./graph/load.sh --purge --vector   # purge + rebuild vector only
#   ./graph/load.sh --no-dashboard     # stop containers after indexing
#   ./graph/load.sh --dry-run          # preview without writing
# ---------------------------------------------------------------
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

# Defaults: run both unless one is specified
RUN_GRAPH=false
RUN_VECTOR=false
EXPLICIT_MODE=false
PURGE=false
DRY_RUN=false
START_DASHBOARD=true

# FalkorDB config
FALKOR_HOST="localhost"
FALKOR_PORT="6379"
FALKOR_PASS="codegraph123"
GRAPH_NAME="codegraph"

# Qdrant config
QDRANT_HOST="localhost"
QDRANT_PORT="6334"
QDRANT_HTTP_PORT="6333"
COLLECTION="monxa-docs"

INTERVAL=2

# Parse args
while [[ $# -gt 0 ]]; do
  case "$1" in
    --graph)        RUN_GRAPH=true; EXPLICIT_MODE=true; shift ;;
    --vector)       RUN_VECTOR=true; EXPLICIT_MODE=true; shift ;;
    --purge|--reset) PURGE=true; shift ;;
    --dry-run)      DRY_RUN=true; shift ;;
    --no-dashboard) START_DASHBOARD=false; shift ;;
    --*)
      echo "Unknown flag: $1" >&2
      echo "Usage: ./graph/load.sh [--graph] [--vector] [--purge] [--dry-run] [--no-dashboard]" >&2
      exit 1
      ;;
    *)
      echo "Unknown argument: $1" >&2
      exit 1
      ;;
  esac
done

# If neither --graph nor --vector specified, run both
if [[ "${EXPLICIT_MODE}" == false ]]; then
  RUN_GRAPH=true
  RUN_VECTOR=true
fi

FALKOR_CONTAINER="${FALKORDB_CONTAINER_NAME:-codegraph-falkordb}"
QDRANT_CONTAINER="docsearch-qdrant"
export FALKORDB_CONTAINER_NAME="${FALKOR_CONTAINER}"

# ── Colours ──────────────────────────────────────────────────────────────────
GREEN='\033[0;32m'; YELLOW='\033[1;33m'; CYAN='\033[0;36m'; RED='\033[0;31m'; RESET='\033[0m'
info()    { echo -e "${CYAN}▶ $*${RESET}"; }
success() { echo -e "${GREEN}✔ $*${RESET}"; }
warn()    { echo -e "${YELLOW}⚠ $*${RESET}"; }
fail()    { echo -e "${RED}✘ $*${RESET}"; }

# ── 1. Sanity checks ────────────────────────────────────────────────────────
if ! command -v docker &>/dev/null; then
  echo "ERROR: docker is not installed or not in PATH." >&2; exit 1
fi
if ! command -v dotnet &>/dev/null; then
  echo "ERROR: dotnet not found. Install .NET SDK >= 10.0." >&2; exit 1
fi

# ── 2. Start Docker containers ──────────────────────────────────────────────
if [[ ! -f "${SCRIPT_DIR}/docker-compose.yml" ]]; then
  echo "ERROR: docker-compose.yml not found in ${SCRIPT_DIR}" >&2; exit 1
fi

cd "${SCRIPT_DIR}"

info "Starting Docker services..."
docker compose up -d

# ── 3. Wait for FalkorDB readiness ──────────────────────────────────────────
if [[ "${RUN_GRAPH}" == true ]]; then
  info "Waiting for FalkorDB to accept connections..."
  ATTEMPTS=0; MAX_ATTEMPTS=30
  until docker exec "${FALKOR_CONTAINER}" redis-cli -a "${FALKOR_PASS}" ping 2>/dev/null | grep -q PONG; do
    ATTEMPTS=$((ATTEMPTS + 1))
    if [[ "${ATTEMPTS}" -ge "${MAX_ATTEMPTS}" ]]; then
      fail "FalkorDB did not become ready after $((MAX_ATTEMPTS * INTERVAL))s"; exit 1
    fi
    sleep "${INTERVAL}"
  done
  success "FalkorDB is ready."
fi

# ── 4. Wait for Qdrant readiness ────────────────────────────────────────────
if [[ "${RUN_VECTOR}" == true ]]; then
  info "Waiting for Qdrant to accept connections..."
  ATTEMPTS=0; MAX_ATTEMPTS=30
  until curl -sf "http://${QDRANT_HOST}:${QDRANT_HTTP_PORT}/" >/dev/null 2>&1; do
    ATTEMPTS=$((ATTEMPTS + 1))
    if [[ "${ATTEMPTS}" -ge "${MAX_ATTEMPTS}" ]]; then
      fail "Qdrant did not become ready after $((MAX_ATTEMPTS * INTERVAL))s"; exit 1
    fi
    sleep "${INTERVAL}"
  done
  success "Qdrant is ready."
fi

# ── 5. Optional purge ───────────────────────────────────────────────────────
if [[ "${PURGE}" == true ]]; then
  if [[ "${RUN_GRAPH}" == true ]]; then
    info "Purging graph (GRAPH.DELETE ${GRAPH_NAME})..."
    docker exec -i "${FALKOR_CONTAINER}" redis-cli -a "${FALKOR_PASS}" GRAPH.DELETE "${GRAPH_NAME}" 2>/dev/null || true
    success "Graph purge complete."
  fi
  if [[ "${RUN_VECTOR}" == true ]]; then
    info "Purging vector collection '${COLLECTION}'..."
    curl -sf -X DELETE "http://${QDRANT_HOST}:${QDRANT_HTTP_PORT}/collections/${COLLECTION}" >/dev/null 2>&1 || true
    success "Vector purge complete."
  fi
fi

# ── 6. Run graph indexer (Roslyn → FalkorDB) ────────────────────────────────
if [[ "${RUN_GRAPH}" == true ]]; then
  info "Running Roslyn source analyzer (graph.cs)..."
  DRY_FLAG=""
  [[ "${DRY_RUN}" == true ]] && DRY_FLAG="--dry-run"
  dotnet run "${SCRIPT_DIR}/graph.cs" -- \
    --host="${FALKOR_HOST}" \
    --port="${FALKOR_PORT}" \
    --password="${FALKOR_PASS}" \
    --graph="${GRAPH_NAME}" \
    --src="${REPO_ROOT}/src" \
    ${DRY_FLAG}
  success "Graph indexing complete."
else
  warn "Graph indexing skipped."
fi

# ── 7. Run vector indexer (Markdown → ONNX → Qdrant) ────────────────────────
if [[ "${RUN_VECTOR}" == true ]]; then
  info "Running markdown vector indexer (vector.cs)..."
  info "  Embeddings: ONNX all-MiniLM-L6-v2 (local, no external service)"
  EXTRA_FLAGS=""
  [[ "${DRY_RUN}" == true ]] && EXTRA_FLAGS="--dry-run"
  [[ "${PURGE}" == true ]] && EXTRA_FLAGS="${EXTRA_FLAGS} --purge"
  dotnet run "${SCRIPT_DIR}/vector.cs" -- \
    --host="${QDRANT_HOST}" \
    --port="${QDRANT_PORT}" \
    --collection="${COLLECTION}" \
    --src="${REPO_ROOT}" \
    ${EXTRA_FLAGS}
  success "Vector indexing complete."
else
  warn "Vector indexing skipped."
fi

# ── 8. Optionally stop containers ───────────────────────────────────────────
if [[ "${START_DASHBOARD}" == false ]]; then
  info "Stopping containers (--no-dashboard)..."
  docker compose down
fi

# ── 9. Print summary ────────────────────────────────────────────────────────
echo ""
echo -e "${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${RESET}"
success "All done!"
echo ""
if [[ "${RUN_GRAPH}" == true ]]; then
  echo -e "  ${CYAN}FalkorDB${RESET}      → ${FALKOR_HOST}:${FALKOR_PORT} (graph: ${GRAPH_NAME})"
  echo -e "  ${CYAN}Browser UI${RESET}    → http://localhost:3000"
fi
if [[ "${RUN_VECTOR}" == true ]]; then
  echo -e "  ${CYAN}Qdrant${RESET}        → ${QDRANT_HOST}:${QDRANT_HTTP_PORT} (collection: ${COLLECTION})"
  echo -e "  ${CYAN}Qdrant UI${RESET}     → http://localhost:${QDRANT_HTTP_PORT}/dashboard"
  echo -e "  ${CYAN}Embeddings${RESET}    → ONNX all-MiniLM-L6-v2 (384d, local)"
fi
echo ""
echo "  Examples:"
echo "    ./graph/load.sh                    # rebuild both indexes"
echo "    ./graph/load.sh --graph            # graph only"
echo "    ./graph/load.sh --vector           # vector only"
echo "    ./graph/load.sh --purge            # purge + full rebuild"
echo "    ./graph/load.sh --purge --vector   # purge + rebuild vector only"
echo "    ./graph/load.sh --no-dashboard     # stop containers after indexing"
echo "    ./graph/load.sh --dry-run          # preview without writing"
echo -e "${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${RESET}"
