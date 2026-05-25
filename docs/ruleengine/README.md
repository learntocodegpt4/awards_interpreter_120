# Rule Engine

The Rule Engine calculates payable outcomes from structured award rules and normalized workforce events. It must not parse FWC legal text at runtime. Its input is a published, immutable rule snapshot generated from the ETL and Clause Interpreter pipeline.

## Responsibilities

- Load active rule snapshot by tenant, award, classification, employee type, and as-at date.
- Evaluate day, time, shift, trigger, allowance, penalty, and leave conditions.
- Split timesheet input into payable segments before calculation.
- Apply stacking, replacement, highest-of, and additional-to rules deterministically.
- Emit pay lines with full calculation trace.
- Raise compliance exceptions when an input cannot be safely calculated.

## Non-Responsibilities

- Extracting meaning from unstructured FWC legal text.
- Making tenant policy decisions without tenant configuration.
- Silently fixing ambiguous source data.
- Producing payroll outputs without traceable source clauses and rule versions.

## Calculation Inputs

The engine consumes normalized facts, not raw UI or clock data:

- employee and tenant identity,
- award and classification,
- employment type,
- normalized work segments,
- public holiday calendar and part-day boundaries,
- leave overlays,
- on-call/recall/sleepover events,
- approved tenant overrides,
- rule snapshot version.

## Calculation Outputs

Each pay line should include:

- earning code,
- quantity,
- rate,
- amount,
- rule ID and rule version,
- source clause reference,
- source row hash,
- calculation formula,
- segment IDs,
- triggered allowances,
- stacking and override decisions,
- compliance warnings or exceptions.

## Rule Snapshot Contract

A `RuleSetVersion` should be immutable once published.

Minimum fields:

- `rule_set_version_id`
- `award_code`
- `published_year`
- `effective_from`
- `effective_to`
- `source_snapshot_hash`
- `parser_version`
- `compiler_version`
- `rules_json`
- `status`
- `published_at`

The engine should calculate using one explicit rule version per request. Recalculation with a newer rule version must be an intentional workflow.

## Calculation Flow

1. Validate request and tenant scope.
2. Resolve employee, award, classification, and applicable rule version.
3. Normalize timesheet data into segments.
4. Split segments by day boundary, public holiday boundary, overtime band, leave overlay, and shift condition.
5. Evaluate base rate and employment loading.
6. Evaluate penalties by priority and stacking policy.
7. Evaluate allowances by trigger and basis.
8. Apply tenant overrides and compliance guards.
9. Emit pay lines, calculation trace, and exceptions.
10. Persist audit and publish calculation events.

## Event Flow

Recommended events:

- `TimesheetSubmitted`
- `TimesheetNormalized`
- `RuleCalculationRequested`
- `PayLineCalculated`
- `ComplianceExceptionRaised`
- `CalculationApproved`
- `PayrollExportRequested`
- `PayrollExportCompleted`

## CQRS And Event-Sourced Timesheet State

The write side receives clock, schedule, break, leave, on-call, and recall facts. It validates tenant scope and appends state-changing events. The read side serves normalized segments, calculated pay lines, payroll summaries, and compliance reports.

Timesheet state should be replayable from events such as:

- `ClockEventImported`
- `BreakRecorded`
- `LeaveApplied`
- `OnCallEventRecorded`
- `TimesheetSubmitted`
- `TimesheetApproved`
- `TimesheetNormalized`
- `PayCalculated`

Fatigue and rolling-window rules require previous-shift state. The engine must know the prior shift end time and whether a required break, such as a 10-hour break, was received.

## Temporal Rule Lookup

Every award rule, rate, allowance, penalty, tenant override, and compiled rule snapshot must be effective-dated.

Calculation lookup pattern:

```sql
WHERE operative_from <= @work_date
  AND (operative_to IS NULL OR operative_to > @work_date)
```

This supports back-pay and historical recalculation. A timesheet from July 2024 must not use a 2026 active rate unless the recalculation explicitly requests that version.

## Special WFM Calculators

The Rule Engine roadmap includes:

- daily and weekly overtime bands,
- span-of-hours and unsocial hours,
- casual loading and base rate interactions,
- compounding versus non-compounding penalty handling,
- fatigue break rules,
- public holiday and part-day public holiday splitting,
- leave loading higher-of calculations,
- BOOT and salary/EBA top-up checks,
- allowance trigger evaluation from timesheet tags and events.

## Precision Rule

Use decimal math for all payable calculations. Floating point arithmetic is not acceptable for payroll amounts.
