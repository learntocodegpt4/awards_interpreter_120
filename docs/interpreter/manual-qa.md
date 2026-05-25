# Clause Interpreter Manual QA

Manual QA validates that parser output is legally explainable and operationally usable. QA should compare the raw FWC fields, surrounding clause text, parsed output, confidence, and provenance.

## QA Evidence To Capture

For each scenario, record:

- award code,
- clause reference,
- source row ID or fixed ID,
- raw `allowance` or `penalty_description`,
- raw `payment_frequency` or `rate_unit`,
- parsed `condition_json`,
- compatibility fields,
- confidence,
- parse status,
- evidence text,
- QA result and notes.

## INT-MQA-001: First Aid Default Applicability

Given a wage allowance row with `allowance = First aid allowance` and `payment_frequency = per week`
When the Interpreter parses the row
Then `payment_trigger = first_aid`
And `payment_basis = per_week`
And day applicability is Monday-Sunday / everyday
And provenance for day applicability is `domain_default`
And parse status is `complete_defaulted`

## INT-MQA-002: First Aid With Restrictive Context

Given a first aid allowance row with parent or clause context that restricts employee group, location, or work type
When the Interpreter parses the row
Then the trigger remains `first_aid`
And the restriction is captured in `condition_json`
And unrestricted everyday applicability is not applied without preserving the restriction

## INT-MQA-003: On-Call Monday To Friday

Given a wage allowance row with `allowance = On-call allowance - Monday to Friday`
When the Interpreter parses the row
Then `payment_trigger = on_call`
And day applicability is Monday through Friday
And `public_holiday = false` unless the source text says otherwise
And day provenance is `explicit_text`

## INT-MQA-004: On-Call Monday To Saturday

Given a wage allowance row with `allowance = On-call allowance - Monday to Saturday`
When the Interpreter parses the row
Then day applicability is Monday through Saturday
And Sunday is excluded
And day provenance is `explicit_text`

## INT-MQA-005: On-Call Saturday

Given a wage allowance row with `allowance = On-call allowance - Saturday`
When the Interpreter parses the row
Then day applicability is Saturday only
And the parser does not expand Saturday into weekend

## INT-MQA-006: On-Call Sunday Or Public Holiday

Given a wage allowance row with `allowance = On-call allowance - Sunday or public holiday`
When the Interpreter parses the row
Then day applicability includes Sunday
And `public_holiday = true`
And the Rule Engine can later avoid double payment when a public holiday falls on Sunday

## INT-MQA-007: On-Call Per 24 Hour Period Or Part Thereof

Given a wage allowance row with `allowance = On-call allowance, per 24 hour period or part thereof - Sunday or public holiday`
When the Interpreter parses the row
Then `payment_basis = per_24h_or_part`
And period rounding metadata says an started 24-hour period counts
And day applicability includes Sunday and public holiday

## INT-MQA-008: Sleepover Additional To On-Call

Given a wage allowance row with `allowance = Sleepover allowance - additional to on-call allowance`
When the Interpreter parses the row
Then `payment_trigger = sleepover`
And the condition references the on-call allowance dependency
And stacking policy is additive or flagged for review if not explicit

## INT-MQA-009: Public Holiday Overlap With Sunday

Given an allowance or penalty that applies to `Sunday or public holiday`
When the Interpreter parses the row
Then Sunday and public holiday are represented as separate conditions
And parse notes identify the overlap risk
And downstream QA can verify no duplicate pay line is generated for the same entitlement

## INT-MQA-010: Ambiguous On-Call Without Day Text

Given a row with `allowance = On-call allowance` and no day wording in the row or clause context
When the Interpreter parses the row
Then the row is not defaulted to everyday
And parse status is `manual_review`
And review reason states that on-call day applicability is ambiguous

## INT-MQA-011: Source Field Provenance

Given a row where the condition appears in `clause_description` or clause statement text rather than the `allowance` field
When the Interpreter parses the row
Then the extracted condition records the correct source field
And QA can see the evidence text used for the decision

## INT-MQA-012: Penalty Compounding Signal

Given a penalty clause that says payment is based on the ordinary rate
When the Interpreter parses the row
Then `is_compounding = false`
And `base_rate_reference = ordinary_rate`
And ambiguous compounding wording is routed to manual review

## INT-MQA-013: Low Confidence Review Promotion

Given a parsed row has confidence below the active threshold
When a reviewer edits and approves the structured fields
Then the row moves to active publication status
And reviewer identity, timestamp, and reason are stored
And the reviewed example is eligible for the regression corpus
