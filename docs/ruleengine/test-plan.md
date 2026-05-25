# Rule Engine Automated Test Plan

## Objective

Prove that the Rule Engine calculates payroll outcomes exactly, repeatably, and with full traceability from normalized input facts and published rule snapshots.

## Test Levels

| Level | Purpose | Example |
|---|---|---|
| Unit | Validate one calculation component. | `per_24h_or_part` allowance rounds one started period. |
| Segment | Validate timesheet normalization and splitting. | Overnight shift splits across Saturday and Sunday. |
| Matrix | Validate combinations of employment, day, shift, public holiday, and allowance trigger. | Casual Sunday overtime with public holiday flag. |
| Property | Validate mathematical invariants. | Higher base rate cannot produce lower pay for the same rule context. |
| End to end | Validate full flow from rule snapshot to pay lines. | On-call Sunday/public holiday event produces one correct allowance pay line. |
| Regression | Preserve manually verified payroll examples. | A known award scenario remains stable after engine changes. |

## Required Assertions

Every calculation test should assert:

- input rule version,
- normalized segment boundaries,
- selected base rate,
- selected penalty and stacking policy,
- allowance trigger result,
- decimal precision and rounding,
- final pay line quantity, rate, and amount,
- calculation trace fields,
- compliance warnings or exceptions.

## Table-Driven Payroll Tests

Use CSV or JSON-driven tests for rule math. Each row should be understandable by payroll SMEs and executable by CI.

Minimum columns:

- employee type,
- award code,
- classification,
- date,
- clock in,
- clock out,
- break minutes,
- state/region,
- tags such as `first_aid`, `on_call`, `recall`, `extreme_heat`,
- expected ordinary hours,
- expected 1.5x hours,
- expected 2.0x hours,
- expected allowances,
- expected total.

The test runner should load each row, normalize segments, calculate pay lines, and compare exact decimal results.

## Time-Travel Tests

Every effective-dated table must be tested with historical calculation dates.

Required cases:

- timesheet date before current award version,
- timesheet date after a rate change,
- tenant override effective only for part of a pay period,
- recalculation using original rule version,
- recalculation explicitly using a newer rule version.

## Core Test Scenarios

| Scenario ID | Scenario | Expected Focus |
|---|---|---|
| RULE-AUTO-001 | First aid weekly allowance for eligible employee. | Trigger, basis, no day restriction misread. |
| RULE-AUTO-002 | On-call Monday to Friday. | Explicit weekday applicability. |
| RULE-AUTO-003 | On-call Monday to Saturday. | Saturday included, Sunday excluded. |
| RULE-AUTO-004 | On-call Sunday or public holiday. | Sunday and public holiday represented separately. |
| RULE-AUTO-005 | On-call per 24 hour period or part thereof. | Period rounding and quantity. |
| RULE-AUTO-006 | Sleepover additional to on-call. | Additive stacking. |
| RULE-AUTO-007 | Sunday that is also public holiday. | Highest-of or configured non-duplication policy. |
| RULE-AUTO-008 | Part-day public holiday. | Segment split at public holiday start/end. |
| RULE-AUTO-009 | Cross-midnight shift Friday to Saturday. | Day boundary split and rate change. |
| RULE-AUTO-010 | Tenant tries to reduce mandatory entitlement. | Override blocked and exception raised. |

## Decimal Precision

All monetary tests should use decimal types. Tests should fail if calculation paths convert payable amounts to floating point.

## Suggested Test File Layout

```text
RosteredAI_ETL/RuleEngine/RuleEngine.Tests/
  Compilation/
  Segmentation/
  Allowances/
  Penalties/
  Stacking/
  Compliance/
  EndToEnd/
```

## CI Gate

Rule Engine tests should be required before payroll exports are enabled for a build. A failed calculation test must block deployment to environments where payable outputs are generated.
