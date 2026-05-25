# Payroll Output And STP Phase 2

This document defines the downstream output roadmap after rules are calculated.

## Output Boundary

Payroll integrations consume approved pay lines, not raw FWC clauses and not low-confidence parser output.

Each pay line should include:

- tenant ID,
- employee ID,
- pay period,
- earning code,
- quantity,
- rate,
- amount,
- award code,
- classification,
- rule version,
- source clause,
- calculation trace,
- STP Phase 2 category candidate,
- payroll system mapping status.

## STP Phase 2 Mapping Areas

Map calculated outputs into payroll categories such as:

- gross ordinary earnings,
- overtime,
- paid leave,
- allowances,
- bonuses/commissions where applicable,
- deductions where applicable,
- salary sacrifice where applicable,
- termination-related categories where applicable.

Final STP mapping must be reviewed against ATO/payroll-provider requirements before production.

## Integration Targets

Initial integration patterns:

- CSV export for QA and payroll parallel runs,
- API adapter for payroll systems,
- Xero/MYOB mapping layer when selected,
- ABA file generation only for payment workflows that require it,
- audit report export for compliance review.

## Validation Gates

Before payroll export:

- all pay lines must have a rule version,
- all pay lines must have source trace,
- no pay line can depend on `pending_review` parser output,
- payroll category mapping must be complete or explicitly marked review-required,
- totals must reconcile against calculation trace.

