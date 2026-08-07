#!/usr/bin/env bash
set -euo pipefail

if ! command -v git >/dev/null 2>&1; then
  exit 0
fi

repo_root="$(git rev-parse --show-toplevel 2>/dev/null || true)"
if [[ -z "${repo_root}" ]]; then
  exit 0
fi

cd "${repo_root}"

changed_files="$(git diff --name-only --diff-filter=ACMR -- src/ApiEndpoints/Minimal.AppServices | grep -E '\\.cs$' || true)"
if [[ -z "${changed_files}" ]]; then
  exit 0
fi

warnings=()
while IFS= read -r file; do
  [[ -z "${file}" ]] && continue

  if grep -Eq '^\+.*record[[:space:]]+[A-Za-z0-9_]+Dto\b' <(git diff -U0 -- "${file}" 2>/dev/null || true); then
    if ! grep -Eq '\[GenerateDto\(' "${file}"; then
      warnings+=("${file}")
    fi
  fi
done <<< "${changed_files}"

if (( ${#warnings[@]} > 0 )); then
  echo "[dto-warning] Manual DTO record detected without [GenerateDto]." >&2
  echo "[dto-warning] Review GenerateDto-first policy and document exception rationale." >&2
  for file in "${warnings[@]}"; do
    echo "[dto-warning] - ${file}" >&2
  done
fi

exit 0
