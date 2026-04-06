#!/usr/bin/env bash
# ---------------------------------------------------------------
# Install graph/vector incremental update git hooks.
#
# Run once after cloning: ./graph/setup-hooks.sh
# ---------------------------------------------------------------
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
HOOKS_DIR="${REPO_ROOT}/.git/hooks"

if [[ ! -d "${REPO_ROOT}/.git" ]]; then
  echo "ERROR: Not a git repository: ${REPO_ROOT}" >&2
  exit 1
fi

mkdir -p "${HOOKS_DIR}"

# Install post-commit hook
SRC="${SCRIPT_DIR}/hooks/post-commit"
DST="${HOOKS_DIR}/post-commit"

if [[ -f "${DST}" ]]; then
  echo "WARNING: ${DST} already exists."
  echo "  Backing up to ${DST}.bak"
  cp "${DST}" "${DST}.bak"
fi

cp "${SRC}" "${DST}"
chmod +x "${DST}"
echo "Installed: ${DST}"
echo ""
echo "Graph/vector indexes will update incrementally after each commit."
echo "Logs: /tmp/monxa-graph-update.log"
