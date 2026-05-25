# Rule Engine TODOs

Status values: `TODO`, `IN_PROGRESS`, `DONE`, `BLOCKED`, `DEFERRED`.

| ID | Status | Task | Deliverable | Automated Test | Manual QA |
|---|---|---|---|---|---|
| RULE-001 | TODO | Define immutable `RuleSetVersion` snapshot contract. | Snapshot schema and version selection rules. | Contract tests for required fields and immutability. | QA confirms calculation request references one rule version. |
| RULE-002 | DONE | Compile published award semantics into rule conditions consumed by the backend engine. | MA000120 compiler baseline fixture compiles approved semantic rows into `RuleSetVersion.rules_json`. | `dotnet run --project award_interpretation_rules_engine.tests/AwardInterpretationRulesEngine.Tests.csproj` covers the repeatable baseline snapshot and POC 2 scenario parity. | QA can trace compiled rules to fixture row IDs, source hashes, and clause references. |
| RULE-003 | TODO | Normalize timesheet inputs into payable segments before calculation. | Segment model and normalization pipeline. | Unit tests for clock, break, leave, schedule, and event inputs. | QA validates segment timeline against sample timesheet. |
| RULE-004 | TODO | Implement day/public holiday segment splitting, including overnight and part-day public holidays. | Boundary splitter for calendar and public holiday transitions. | Matrix tests for overnight and part-day public holiday cases. | QA validates segment split on cross-midnight and part-day scenarios. |
| RULE-005 | TODO | Implement allowance trigger evaluation for first aid, on-call, recall, meal, sleepover, leading hand. | Trigger evaluator and allowance basis handlers. | Tests for each trigger and payment basis. | QA validates allowance pay lines against manually calculated examples. |
| RULE-006 | TODO | Implement penalty stacking policy: cumulative, non-cumulative, highest-of, replacement, additional-to. | Stacking policy engine. | Tests for each stacking policy and conflict case. | QA confirms no double counting for Sunday/public holiday overlap. |
| RULE-007 | TODO | Emit calculation trace per pay line: formula, rule version, clause reference, source hash, input segment IDs. | Trace model persisted with pay lines. | Tests assert trace fields exist for every pay line. | QA follows a pay line back to source clause and input segment. |
| RULE-008 | TODO | Add tenant override validation: allow equal-or-better changes, block silent reduction of mandatory entitlements. | Override validator and compliance exceptions. | Tests for allowed and blocked overrides. | QA validates tenant override approval workflow. |
| RULE-009 | TODO | Add compliance exceptions for ambiguity, missing applicability, and possible underpayment. | Exception model and event emission. | Tests assert ambiguous inputs block or warn correctly. | QA verifies exception reason and remediation path. |
| RULE-010 | TODO | Add recalculation flow for award source updates and affected pay periods. | Recalculation request and impact analysis workflow. | Tests for old version retention and explicit new-version recalculation. | QA confirms affected employees/pay periods are listed before recalculation. |
| RULE-011 | TODO | Implement SCD Type 2 temporal rule lookup for back-pay and historical calculations. | Effective-date query contract for rates, allowances, penalties, overrides, and snapshots. | Time-travel tests for historical and current rates. | QA validates July 2024 work uses July 2024 rule values. |
| RULE-012 | TODO | Implement event-sourced timesheet state for prior-shift and rolling-window rules. | Event model for timesheet lifecycle and prior shift state. | Event replay tests and rolling window tests. | QA verifies previous shift end time affects fatigue calculation. |
| RULE-013 | TODO | Implement fatigue break rules such as double time until a required break is received. | Fatigue rule evaluator using previous shift state. | Tests for insufficient break and break recovery cases. | QA validates less-than-10-hour-break scenario. |
| RULE-014 | TODO | Implement leave accrual and leave loading, including higher-of 17.5% or weekend/public holiday penalty where applicable. | Leave overlay and leave loading calculator. | Tests for annual leave, personal leave, unpaid leave, and higher-of loading. | QA validates leave pay line breakdown. |
| RULE-015 | TODO | Implement BOOT/base award comparison for EBA or salary arrangements. | Comparison engine and top-up output. | Tests compare EBA/salary result to base award result. | QA validates top-up amount and trace. |

## Implementation Notes

- The engine should initially integrate with the existing .NET CQRS shape in `RosteredAI_ETL/RuleEngine`.
- Rule calculations should be deterministic and replayable from persisted input facts plus rule version.
- The engine should reject incomplete rule snapshots instead of falling back to raw FWC text.
