# Traceability Matrix

This matrix maps requirements to implementation tasks, automated tests, and Manual QA scenarios. Update it whenever task status changes.

| Requirement | Task ID | Automated Evidence | Manual QA Evidence | Status |
|---|---|---|---|---|
| Canonical parser enums exist for semantic fields. | INT-001 | Schema validation test rejects unknown values. | QA confirms enum coverage on sample rows. | TODO |
| `condition_json` is documented and versioned. | INT-002 | JSON schema test validates golden examples. | QA reviews condition examples. | TODO |
| Existing parser is refactored into maintainable modules. | INT-003 | `python -m pytest tests\test_clause_interpreter.py tests\test_parallel_etl_hardening.py -k "allowance or clause_interpreter" -q` passed. | QA confirms no ETL sample regression. | IN_PROGRESS |
| Common allowance families parse deterministically. | INT-004 | `tests/test_clause_interpreter.py` covers first aid, on-call, and leading hand; broader family corpus pending. | INT-MQA-001, INT-MQA-002, INT-MQA-008. | IN_PROGRESS |
| Explicit day wording is parsed into day sets. | INT-005 | `tests/test_clause_interpreter.py` covers weekday, Monday-Saturday, and Sunday/public holiday; remaining matrix pending. | INT-MQA-003, INT-MQA-004, INT-MQA-005, INT-MQA-006. | IN_PROGRESS |
| Payment basis and period rounding are parsed. | INT-006 | `tests/test_clause_interpreter.py` covers per-day, per-week, and per-24-hour-or-part basis; remaining matrix pending. | INT-MQA-007. | IN_PROGRESS |
| First aid default uses provenance and does not over-default on-call. | INT-007 | `tests/test_clause_interpreter.py` covers first aid everyday default and ambiguous on-call manual review. | INT-MQA-001, INT-MQA-010. | IN_PROGRESS |
| Ambiguous and low-confidence rows route to review. | INT-008 | `tests/test_clause_interpreter.py` covers ambiguous on-call manual review. | INT-MQA-010. | IN_PROGRESS |
| LLM extraction is advisory and gated. | INT-009 | LLM candidate cannot publish without review. | QA reviews candidate evidence. | TODO |
| Parser output is traceable to source row and clause. | INT-010 | Trace metadata tests. | QA follows parsed field to source evidence. | TODO |
| Parser uses allowance, penalty, clause statement, and table context as source input. | INT-011 | Source bundle provenance tests. | INT-MQA-011. | TODO |
| Penalty compounding and base-rate reference are extracted or reviewed. | INT-012 | Compounding wording tests. | INT-MQA-012. | TODO |
| Low-confidence extraction has a HITL review path. | INT-013 | Review transition tests. | INT-MQA-013. | TODO |
| Parser accuracy is measured against a ground-truth corpus. | INT-014, INT-015 | F1/precision/recall deployment gate. | QA reviews corpus and regression report. | TODO |
| Penalty parser coverage matches allowance parser coverage. | INT-016 | Penalty golden corpus tests. | QA validates penalty source rows. | TODO |
| Rule snapshots are immutable and versioned. | RULE-001 | Contract and immutability tests. | QA confirms calculation references one version. | TODO |
| Published semantics compile to engine rules. | RULE-002 | Compiler tests from golden interpreter outputs. | QA confirms compiled rule matches clause. | TODO |
| Timesheet inputs normalize before calculation. | RULE-003 | Segment normalization tests. | QA validates segment timeline. | TODO |
| Day and public holiday boundaries split correctly. | RULE-004 | Boundary matrix tests. | RULE-MQA-008, RULE-MQA-009. | TODO |
| Allowance triggers calculate correctly. | RULE-005 | Trigger and basis tests. | RULE-MQA-001 through RULE-MQA-006. | TODO |
| Penalty and allowance stacking is deterministic. | RULE-006 | Stacking policy tests. | RULE-MQA-006, RULE-MQA-007. | TODO |
| Every pay line has a calculation trace. | RULE-007 | Trace field tests. | QA traces pay line to source clause and segment. | TODO |
| Tenant overrides cannot reduce mandatory entitlements silently. | RULE-008 | Override validation tests. | RULE-MQA-010. | TODO |
| Compliance exceptions catch ambiguity and underpayment risk. | RULE-009 | Exception tests. | QA validates exception reason and remediation. | TODO |
| Award updates support explicit recalculation. | RULE-010 | Version retention and recalculation tests. | QA reviews impacted employees and pay periods. | TODO |
| Historical calculations use effective-dated rules. | RULE-011 | Time-travel tests. | QA validates historical rate lookup. | TODO |
| Prior-shift state supports rolling-window rules. | RULE-012 | Event replay tests. | QA reviews prior shift state. | TODO |
| Fatigue break rules calculate premium time until recovery. | RULE-013 | Fatigue break tests. | RULE-MQA-011. | TODO |
| Leave loading and leave overlays are calculated correctly. | RULE-014 | Leave loading tests. | RULE-MQA-012. | TODO |
| BOOT/base-award comparison produces top-ups. | RULE-015 | BOOT comparison tests. | RULE-MQA-013. | TODO |

## Review Cadence

Use this matrix during implementation checkpoints:

1. Before coding a task, confirm its automated and Manual QA evidence are clear.
2. During implementation, update task status in the relevant `todos.md`.
3. After tests pass, add the test file or test run reference to this matrix.
4. After Manual QA, add the QA evidence location or notes.
5. Mark task `DONE` only after both implementation and validation evidence exist.
