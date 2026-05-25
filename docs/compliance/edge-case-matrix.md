# Compliance Edge Case Matrix

This matrix captures known WFM interpretation traps and "unknown unknowns" that must be represented in parser, rule, and QA work.

| Edge Case | Example | Risk | Required Control |
|---|---|---|---|
| Implied everyday applicability | `First aid allowance` with no day text | Under- or over-restricting a role allowance. | Use approved domain default with `domain_default` provenance. |
| Ambiguous on-call applicability | `On-call allowance` with no day text | Incorrectly applying on-call allowance every day. | Route to manual review unless clause context supplies days. |
| Explicit day text | `Monday to Saturday` | Losing Saturday or including Sunday by accident. | Parse to day set, not free text. |
| Public holiday overlap | `Sunday or public holiday` | Double payment when Sunday is also public holiday. | Store Sunday and public holiday separately plus stacking policy. |
| Part-day public holiday | Christmas Eve public holiday from 7:00 PM | Paying whole shift at PH rate. | Split segment at exact local boundary. |
| Compounding ambiguity | Casual loading plus Sunday penalty | Wrong base for multiplier. | Extract `is_compounding` and `base_rate_reference`; review ambiguous wording. |
| Fatigue break | Less than 10-hour break between shifts | Missing double-time until break. | Persist previous shift state and fatigue recovery segment. |
| Leave loading higher-of | Annual leave on penalty day | Paying 17.5% when penalty is higher, or vice versa. | Compare leave loading against award-required alternative. |
| Clause table context | Condition appears in table header, not row text | Parser misses condition. | Bundle table headers/captions into source text with provenance. |
| Cross-clause dependency | Sleepover additional to on-call | Missing additive allowance. | Link dependent rule by clause reference and stacking policy. |
| Historical rate lookup | Back-pay for July 2024 | Using current rates for historical work. | SCD Type 2 effective-date lookup. |
| Tenant override reduction | Tenant config lowers mandatory entitlement | Underpayment. | Equal-or-better validation and compliance exception. |
| EBA/salary BOOT | Salary pays less than base award for a roster pattern | Legal non-compliance. | Run BOOT/base-award comparison and emit top-up. |

## Review Policy

If an edge case affects payable output and cannot be resolved from explicit text, reviewed domain defaults, or tenant configuration, it must produce a compliance exception or manual review item.

