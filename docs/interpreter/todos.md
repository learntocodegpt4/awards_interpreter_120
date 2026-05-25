# Clause Interpreter TODOs

Status values: `TODO`, `IN_PROGRESS`, `DONE`, `BLOCKED`, `DEFERRED`.

| ID | Status | Task | Deliverable | Automated Test | Manual QA |
|---|---|---|---|---|---|
| INT-001 | TODO | Define canonical enum vocabulary for day type, day set, hour type, time condition, shift type, trigger, basis, provenance, confidence, parse status. | Enum specification in `docs/data-model/semantic-schema.md` and code constants. | Schema validation test rejects unknown enum values. | QA confirms enum list covers sample first aid, leading hand, on-call, sleepover, and public holiday rows. |
| INT-002 | TODO | Document normalized semantic schema and `condition_json`. | Versioned condition schema with examples for allowances and penalties. | JSON schema test validates golden examples. | QA reviews `condition_json` for sample rows and confirms field meaning is understandable. |
| INT-003 | IN_PROGRESS | Refactor current `award_semantics.py` into parser modules: normalizer, deterministic patterns, confidence scorer, provenance writer. | Parser package with stable public API used by ETL transformers. | Existing parser tests still pass; new module-level unit tests added. | QA confirms no regression in existing ETL sample output. |
| INT-004 | IN_PROGRESS | Add deterministic parsing for first aid, on-call, leading hand, disability, meal, heat/cold, sleepover, travel, laundry. | Category and trigger extractors for supported allowance families. | Golden corpus tests for each allowance family. | QA validates expected trigger/category for representative FWC rows. |
| INT-005 | IN_PROGRESS | Add explicit day parsing: `Monday to Friday`, `Monday to Saturday`, `Saturday`, `Sunday or public holiday`, `weekday`, `weekend`. | Day-set extractor with provenance and public holiday flags. | Unit tests for each day wording pattern and conflict case. | QA validates on-call rows with weekday, Saturday, Sunday, and public holiday text. |
| INT-006 | IN_PROGRESS | Add payment basis parsing: `per week`, `per day`, `per hour`, `per shift`, `per 24 hour period or part thereof`. | Payment basis extractor and period rounding metadata. | Unit tests for each payment basis and casing variant. | QA confirms basis and rounding for on-call per-24-hour rows. |
| INT-007 | IN_PROGRESS | Add domain defaults with provenance, especially `First aid allowance -> everyday` only when no restrictive text exists. | Defaulting policy and implementation with `defaulted_fields`. | Tests prove defaults apply only to approved allowance families. | QA validates first aid default and verifies on-call without day text is not silently defaulted. |
| INT-008 | IN_PROGRESS | Add manual review routing for low confidence, ambiguous on-call, conflicting day/public holiday rules. | Review queue contract and parse status mapping. | Tests assert ambiguous rows produce review status. | QA confirms review records contain source evidence and reason. |
| INT-009 | TODO | Add optional LLM-assisted extraction as advisory only, gated by evidence spans and review status. | LLM candidate schema and quarantine path. | Tests prove LLM results cannot publish without review flag transition. | QA reviews LLM candidate evidence before acceptance. |
| INT-010 | TODO | Publish parser metrics and traceability: source row, clause, raw text, parser version, confidence, provenance. | Metrics and trace columns/events emitted per ETL run. | Tests assert parser output includes trace metadata. | QA verifies a parsed pay-related field traces back to source row and clause text. |
| INT-011 | TODO | Build source text bundling across allowance, penalty, clause statement, table header, payment frequency, and rate unit fields. | Source bundle object passed to all parser modules. | Tests prove field provenance is retained per extracted value. | QA verifies a parsed value shows the exact source field and text. |
| INT-012 | TODO | Add compounding and base-rate-reference extraction for penalty wording. | `is_compounding`, `base_rate_reference`, and stacking hints in condition output. | Tests for `ordinary rate`, `loaded rate`, `inclusive of casual loading`, and ambiguous wording. | QA reviews compounding decisions for sample penalty rows. |
| INT-013 | TODO | Define and build the human-in-the-loop review workflow. | Review queue, status transitions, reviewer metadata, and acceptance criteria. | Tests assert only reviewed rows can move from LLM/manual review to active. | QA performs accept, reject, and edit scenarios. |
| INT-014 | TODO | Build a manually mapped ground-truth corpus of complex FWC clauses. | Versioned corpus with at least 1,000 expert-mapped clauses over time. | F1/precision/recall test suite runs against the corpus. | QA reviews sample corpus mappings before they become deployment gates. |
| INT-015 | TODO | Add deployment gate for parser accuracy. | CI gate that blocks deployment when F1 or critical-field accuracy regresses. | Test command fails on accuracy regression. | QA signs off threshold and regression report. |
| INT-016 | TODO | Add penalty parser coverage equal to allowance parser coverage. | Penalty semantic extraction for day, time, compounding, stacking, and public holiday conditions. | Golden corpus tests for penalty rows. | QA validates penalty rows from awards with weekday, weekend, overtime, and public holiday penalties. |

## Implementation Notes

- Start with deterministic parsing for allowance rows already present in `data_sql/Stg_TblWageAllowances.csv`.
- Keep compatibility fields until downstream services fully consume `condition_json`.
- Do not remove raw FWC fields from staging or published tables.
- Every manual correction should become a golden parser test.

## Current Implementation Evidence

- Parser package started in `RosteredAI_ETL/src/transform/interpreter`.
- Compatibility wrapper retained in `RosteredAI_ETL/src/transform/award_semantics.py`.
- Focused automated tests added in `RosteredAI_ETL/tests/test_clause_interpreter.py`.
- Regression coverage executed against selected allowance hardening tests in `RosteredAI_ETL/tests/test_parallel_etl_hardening.py`.
- Current status remains `IN_PROGRESS` until Manual QA validates representative FWC sample rows.
