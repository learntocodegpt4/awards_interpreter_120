# Clause Interpreter

The Clause Interpreter is the Python ETL component responsible for converting unstructured FWC allowance and penalty wording into structured, machine-readable rule conditions. It enriches staging data after raw FWC ingestion and before publication into canonical award/rule tables.

## Design Position

The interpreter is not a free-form text classifier. It is a governed rule compiler with evidence, confidence, and provenance.

Primary decision:

- Deterministic parsing is authoritative.
- Domain defaults are allowed only when documented and traceable.
- LLM-assisted extraction is advisory only and must never auto-publish payable rules without review gates.

## Inputs

Typical inputs include:

- FWC API fields such as `allowance`, `parent_allowance`, `penalty_description`, `clause_description`, `payment_frequency`, `rate_unit`, `clauses`, `clause_fixed_id`.
- Document-view clause text from raw snapshots.
- Parsed clause tables and row/header context where applicable.
- Current ETL metadata such as award code, fixed ID, published year, effective dates, source hash, and source URL.

## Outputs

The interpreter writes both flat compatibility fields and normalized condition data.

Compatibility fields:

- `allowance_category`
- `applies_to`
- `applies_day_type`
- `applies_hour_type`
- `applies_time_condition`
- `applies_shift_type`
- `payment_trigger`
- `payment_timing`
- `payment_basis`
- `parsed_confidence`
- `parse_status`
- `parse_notes`

Normalized rule output:

- `condition_json`
- source evidence spans
- confidence score
- provenance per field
- parser version
- review state

## Parser Layers

1. Text normalization
   - Normalize dash variants, punctuation, whitespace, case, time formats, day names, and public holiday wording.
2. Token and pattern extraction
   - Match high-precision constructs such as `Monday to Friday`, `Sunday or public holiday`, `first 2 hours`, `per 24 hour period or part thereof`.
3. Deterministic rule tree
   - Classify categories, triggers, day applicability, basis, hour type, shift type, and stacking hints.
4. Domain defaults
   - Apply conservative defaults only for known allowance families where legal meaning is stable enough to encode.
5. Sanity validation
   - Detect conflicts such as `weekday` plus `Sunday`, missing trigger on event-driven allowances, or public holiday double-count risks.
6. Advisory semantic extraction
   - Optional LLM output may fill review candidates only. It must include evidence text and remain gated until accepted.

## Hybrid Extraction Strategy

Use a three-pass extraction model:

1. Regex and heuristic pass
   - Extract high-confidence entities such as rates, percentages, explicit day names, times, `first 2 hours`, `per week`, and `per 24 hour period or part thereof`.
2. Deterministic rule tree
   - Apply domain-specific rules for allowance families, penalty wording, day applicability, compounding hints, and payment basis.
3. NLP-assisted semantic classification
   - Use spaCy-style classifiers or Azure OpenAI with a strict JSON schema only when deterministic extraction is incomplete.

LLM or NLP output should be treated as `llm_suggestion` provenance until reviewed. This is a compliance control, not an implementation detail.

## Confidence Gate

Recommended publication behavior:

| Score | Parser Status | Publication Status | Required Action |
|---|---|---|---|
| >= 0.95 | `complete` or `complete_defaulted` | `active` | Publish if sanity checks pass. |
| 0.70-0.9499 | `partial` | `pending_review` | Human review before payroll use. |
| < 0.70 | `manual_review` or `failed` | `pending_review` | Human mapping required. |
| Any LLM-only value | `llm_suggested` | `pending_review` | Review evidence and accept/reject. |

If the database only has `parse_status`, use parser-level statuses in ETL and expose `active`/`pending_review` as a publish/review status in the Admin UI.

## Source Text Composition

The parser must not inspect only the `allowance` field. Build a source text bundle per row:

- `allowance`
- `parent_allowance`
- `penalty_description`
- `clause_description`
- `clauses`
- clause statement text from document-view snapshots
- table headers and captions where the FWC page supplies conditions outside the row
- payment frequency and rate unit fields

Every extracted value must identify which source field or clause fragment supplied the evidence.

## Provenance Rules

Each interpreted field should state where its value came from.

| Provenance | Meaning |
|---|---|
| explicit_text | Directly found in the FWC row or clause text. |
| clause_context | Derived from surrounding clause header, table header, caption, or parent clause. |
| domain_default | Inferred from a documented domain default. |
| tenant_config | Supplied by tenant configuration, not FWC source. |
| manual_override | Human-reviewed correction. |
| llm_suggestion | Suggested by an LLM and not authoritative until reviewed. |

## Confidence Bands

| Band | Range | Expected Handling |
|---|---|---|
| High | 0.90-1.00 | Publish automatically if no sanity warnings exist. |
| Medium | 0.70-0.89 | Publish only if deterministic evidence exists and no payable ambiguity remains. |
| Low | 0.40-0.69 | Route to review before use in payable rules. |
| None | 0.00-0.39 | Manual review or quarantine. |

## Critical Applicability Rule

Do not map `applies_day_type` directly from the `allowance` field as a single loose label.

Examples:

- `First aid allowance`, `per week`: day applicability may be `everyday`, but the source is a `domain_default`.
- `On-call allowance - Monday to Saturday`: day applicability is Monday through Saturday, source `explicit_text`.
- `On-call allowance, per 24 hour period or part thereof - Sunday or public holiday`: day applicability is Sunday and public holiday, payment basis is `per_24h_or_part`, source `explicit_text`.

The Rule Engine must know the difference between explicit legal text and inferred logical defaults.

## Review Routing

Send a parsed row to review when:

- confidence is below the publish threshold,
- an on-call or recall allowance has no day applicability,
- day type conflicts with public holiday semantics,
- wording says `other than public holiday`,
- the parser used an LLM suggestion,
- a source row changed but the semantic output stayed unexpectedly identical,
- a payable field was inferred without evidence and no approved default exists.

## Human-In-The-Loop Review

The HITL Admin UI should let a payroll compliance reviewer:

- see raw FWC text and parsed condition side by side,
- edit enum values and condition JSON,
- accept or reject LLM suggestions,
- record review reason and reviewer identity,
- promote a row from `pending_review` to `active`,
- create a regression fixture from the reviewed row.
