#!/usr/bin/env bash
set -euo pipefail

base_ref="${BASE_REF:-}"
if [[ -z "$base_ref" ]]; then
  if [[ -n "${GITHUB_BASE_REF:-}" ]]; then
    base_ref="origin/${GITHUB_BASE_REF}"
  else
    base_ref="origin/dev/amreek_day1"
  fi
fi

if ! git rev-parse --verify "$base_ref" >/dev/null 2>&1; then
  echo "::warning::Base ref '$base_ref' was not available; skipping golden fixture approval diff."
  exit 0
fi

mapfile -t changed_fixtures < <(git diff --name-only "$base_ref"...HEAD -- \
  "award_interpretation_rules_engine.tests/fixtures/golden-corpus/**" \
  "award_interpretation_rules_engine.tests/fixtures/**/*.expected.json")

if [[ "${#changed_fixtures[@]}" -eq 0 ]]; then
  echo "No approved parser golden outputs changed."
  exit 0
fi

approval="${GOLDEN_FIXTURE_APPROVED:-${FIXTURE_APPROVED:-false}}"
if [[ "$approval" == "true" || "$approval" == "1" ]]; then
  echo "Golden fixture changes were approved:"
  printf '  - %s\n' "${changed_fixtures[@]}"
  exit 0
fi

echo "::error::Approved parser golden outputs changed without fixture approval."
echo "Changed fixture files:"
printf '  - %s\n' "${changed_fixtures[@]}"
echo
echo "A reviewer must confirm the parser output change is intentional and mark the PR with the 'fixture-approved' label."
echo "For local rehearsals of an intentional approved update, run with FIXTURE_APPROVED=true."
exit 1
