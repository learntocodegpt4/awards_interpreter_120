# CI Gates

The governed rule engine CI prevents parser, compiler, and runtime regressions from
merging into the productionization branch.

## Local command

Run the complete CI gate locally from the repository root:

```bash
bash scripts/run-ci.sh
```

The script runs the same checks as GitHub Actions, in readable sections:

1. restore parser/compiler, runtime, and MA000120 projects;
2. parser unit tests;
3. parser golden corpus tests;
4. compiler validation tests;
5. runtime regression tests;
6. deterministic MA000120 acceptance suite;
7. parser golden fixture approval gate.

## Individual commands

Use these commands while iterating on a focused change:

```bash
# Parser unit, golden corpus, and compiler validation tests.
dotnet run --project award_interpretation_rules_engine.tests/AwardInterpretationRulesEngine.Tests.csproj --configuration Release

# Runtime tests for approved snapshots, traces, recalculation, stacking, and blocked exports.
dotnet test award_interpretation_rules_engine.runtime_tests/AwardInterpretationRulesEngine.Tests.csproj --configuration Release --logger "console;verbosity=normal"

# MA000120 end-to-end acceptance suite.
dotnet run --project award_interpretation_rules_engine/AwardInterpretationRulesEngine.csproj --configuration Release -- --acceptance

# Golden output approval check.
bash scripts/check-golden-fixture-approval.sh
```

## What blocks CI

CI fails when any of these conditions are detected:

- parser unit or golden corpus tests fail;
- compiler validation accepts an unapproved review state;
- compiler validation accepts raw Dynamic Expresso or script expressions from
  `condition_json`;
- compiler validation accepts unsupported conditions or invalid effective dates;
- runtime tests emit pay line events without rule traces;
- runtime tests execute draft/unapproved snapshots;
- the MA000120 acceptance suite misses review gates, traces, blocked-line behavior,
  TOIL movements, award references, or deterministic payroll totals;
- approved parser golden outputs change without fixture approval.

## Golden fixture approval

Approved parser golden outputs live under
`award_interpretation_rules_engine.tests/fixtures/golden-corpus/`.

If parser behavior intentionally changes, update the expected fixture and ask a
reviewer to apply the `fixture-approved` PR label. Without that label, pull
request CI fails with a list of changed golden output files.

For local rehearsal of an intentional fixture update, run:

```bash
FIXTURE_APPROVED=true bash scripts/run-ci.sh
```
