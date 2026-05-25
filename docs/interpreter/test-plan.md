# Clause Interpreter Automated Test Plan

## Objective

Validate that the Python Clause Interpreter converts FWC source text into deterministic, traceable, structured rule conditions without inventing payable logic.

## Test Levels

| Level | Purpose | Example |
|---|---|---|
| Unit | Validate one parser function or rule. | `Monday to Saturday` becomes six days with `explicit_text` provenance. |
| Golden corpus | Validate real FWC sample rows end to end. | `On-call allowance - Sunday or public holiday` maps to Sunday plus public holiday. |
| Mutation | Validate wording variations. | Dash, comma, casing, and spacing variants parse the same. |
| Contract | Validate JSON shape and enums. | `condition_json` conforms to schema and enum vocabulary. |
| Regression | Preserve manual fixes. | A previously corrected clause remains stable after parser changes. |

## Golden Corpus Requirements

Create a versioned test fixture containing raw input rows and expected parser output.

Minimum examples:

| Corpus ID | Source Example | Expected Focus |
|---|---|---|
| INT-GOLD-001 | `First aid allowance`, `per week` | `first_aid`, `per_week`, everyday by `domain_default`. |
| INT-GOLD-002 | `Industry disability allowance`, `per week` | `industry_disability`, weekly role/condition allowance. |
| INT-GOLD-003 | `Leading hand allowance - 3-5 employees` | `leading_hand`, employee threshold trigger. |
| INT-GOLD-004 | `On-call allowance - Monday to Friday` | Explicit weekday day set. |
| INT-GOLD-005 | `On-call allowance - Monday to Saturday` | Explicit Monday through Saturday day set. |
| INT-GOLD-006 | `On-call allowance - Saturday` | Saturday only. |
| INT-GOLD-007 | `On-call allowance - Sunday or public holiday` | Sunday plus public holiday. |
| INT-GOLD-008 | `On-call allowance, per 24 hour period or part thereof - Sunday or public holiday` | `per_24h_or_part` and period rounding. |
| INT-GOLD-009 | `Sleepover allowance - additional to on-call allowance` | Dependency on on-call rule and additive stacking hint. |
| INT-GOLD-010 | Cold or heat allowance wording | Environmental trigger and temperature threshold. |

## Required Assertions

Every parser test should assert:

- normalized trigger,
- payment basis,
- day set or day type where relevant,
- public holiday flag,
- provenance per interpreted field,
- confidence score,
- parse status,
- evidence text,
- absence of unexpected payable defaults.

## Ground Truth And Accuracy Metrics

Maintain a manually mapped ground-truth corpus of complex FWC clauses. The first production target is 1,000 mapped examples over time, with early increments starting smaller but covering high-risk wording.

Track at least:

- precision and recall for day applicability,
- precision and recall for payment basis,
- precision and recall for payment trigger,
- F1 score across critical enum fields,
- false-active rate for rows that should require review,
- regression count versus the previous parser version.

Deployment gate:

- no critical-field F1 regression,
- no increase in false-active rows for on-call, public holiday, compounding, or overtime clauses,
- every accepted manual correction has a regression fixture.

## Confidence Gates

Automated tests should enforce:

- explicit day parsing confidence is high,
- approved domain defaults are medium or high only when allowed by policy,
- unknown on-call applicability is review status,
- LLM-assisted output remains non-publishable until reviewed.

## Suggested Test File Layout

```text
RosteredAI_ETL/tests/interpreter/
  test_text_normalizer.py
  test_day_parser.py
  test_payment_basis_parser.py
  test_allowance_triggers.py
  test_condition_schema.py
  test_golden_corpus.py
```

## CI Gate

The interpreter test suite should be required before publishing award rules. A failed parser test must block rule snapshot compilation for the affected build.
