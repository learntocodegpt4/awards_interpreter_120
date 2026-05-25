#!/usr/bin/env bash
set -euo pipefail

section() {
  local title="$1"
  if [[ -n "${GITHUB_ACTIONS:-}" ]]; then
    echo "::group::$title"
  else
    echo
    echo "==> $title"
  fi
}

end_section() {
  if [[ -n "${GITHUB_ACTIONS:-}" ]]; then
    echo "::endgroup::"
  fi
}

run_section() {
  local title="$1"
  shift
  section "$title"
  "$@"
  end_section
}

run_section "Restore parser/compiler test project" \
  dotnet restore "award_interpretation_rules_engine.tests/AwardInterpretationRulesEngine.Tests.csproj"

run_section "Restore runtime test project" \
  dotnet restore "award_interpretation_rules_engine.runtime_tests/AwardInterpretationRulesEngine.Tests.csproj"

run_section "Restore MA000120 legacy rule engine" \
  dotnet restore "MA000120RuleEngine/MA000120RuleEngine.csproj"

run_section "Parser unit, golden corpus, and compiler validation tests" \
  dotnet run --project "award_interpretation_rules_engine.tests/AwardInterpretationRulesEngine.Tests.csproj" --configuration Release

run_section "Runtime regression tests" \
  dotnet test "award_interpretation_rules_engine.runtime_tests/AwardInterpretationRulesEngine.Tests.csproj" --configuration Release --logger "console;verbosity=normal"

run_section "MA000120 acceptance suite" \
  dotnet run --project "award_interpretation_rules_engine/AwardInterpretationRulesEngine.csproj" --configuration Release -- --acceptance

run_section "Golden fixture approval gate" \
  bash "scripts/check-golden-fixture-approval.sh"

echo
echo "CI gate completed successfully."
