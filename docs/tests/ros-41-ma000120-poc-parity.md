# ROS-41 MA000120 POC parity report

## Scope

Validate the production MA000120 pipeline output against the POC CLI artifact
contract for the sample pay run.

Compared artifacts:

- `MA000120.interpretation.json`
- `MA000120.governed-library.json`
- `MA000120.calculation.json`

POC CLI fixtures are committed at:

- `award_interpretation_rules_engine/fixtures/poc-cli/MA000120.interpretation.json`
- `award_interpretation_rules_engine/fixtures/poc-cli/MA000120.governed-library.json`
- `award_interpretation_rules_engine/fixtures/poc-cli/MA000120.calculation.json`

The deterministic fixture configuration is:

- `award_interpretation_rules_engine/fixtures/MA000120.parity-appsettings.json`

It disables the subscription API, reads the local MA000120 acceptance HTML, and
pins retrieval/calculation timestamps to `2026-03-01T00:00:00Z`.

## Commands

Fixture generation command, run from `award_interpretation_rules_engine`:

```bash
dotnet run -- --award MA000120 --input samples/sample-payrun-ma000120.json --config fixtures/MA000120.parity-appsettings.json --out fixtures/poc-cli
```

Automated parity command:

```bash
dotnet test award_interpretation_rules_engine.runtime_tests/AwardInterpretationRulesEngine.Tests.csproj
```

Acceptance baseline command:

```bash
dotnet run --project award_interpretation_rules_engine/AwardInterpretationRulesEngine.csproj -- --acceptance
```

## Result

The production pipeline regenerated all three JSON artifacts from the sample
MA000120 pay run and matched the committed POC CLI fixtures byte-for-byte.

| Artifact | Fixture SHA-256 | Classification |
| --- | --- | --- |
| `MA000120.interpretation.json` | `9d0ebf63a6d51e1f95a15127d706217086dea226f650d042330388e97daa652a` | No difference |
| `MA000120.governed-library.json` | `85c12ee146c716560f9b7c8c86651395ccb4be752acdbced17cf10d303000a17` | No difference |
| `MA000120.calculation.json` | `d2676aa5386155acf5bffc7b63ca1d3e57b9f3318bb9956fdd8924151fa05511` | No difference |

Validation evidence:

- `dotnet test award_interpretation_rules_engine.runtime_tests/AwardInterpretationRulesEngine.Tests.csproj` passed: 6/6 tests.
- `dotnet run --project award_interpretation_rules_engine/AwardInterpretationRulesEngine.csproj -- --acceptance` passed the MA000120 acceptance suite.
- `dotnet run --project award_interpretation_rules_engine.tests/AwardInterpretationRulesEngine.Tests.csproj` passed: 6/6 compiler tests.

## Difference classification

No production-vs-POC differences were observed.

| Classification | Items |
| --- | --- |
| Expected production change | None |
| POC bug | None |
| Regression | None |

## Fixture governance

This change establishes the first committed deterministic POC CLI fixture set
for ROS-41. Future intentional changes to any of the three fixture artifacts
must include reviewer sign-off in the PR or linked issue, with the report table
above updated to classify the resulting diff.
