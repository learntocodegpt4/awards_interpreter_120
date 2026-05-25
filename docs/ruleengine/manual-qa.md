# Rule Engine Manual QA

Manual QA validates that calculated outputs match expected award logic and are traceable enough for payroll and compliance review.

## QA Evidence To Capture

For each scenario, record:

- tenant,
- employee,
- award code,
- classification,
- employment type,
- timesheet or event input,
- rule version,
- normalized segments,
- calculated pay lines,
- source clause references,
- formula trace,
- expected amount,
- actual amount,
- QA result and notes.

## RULE-MQA-001: First Aid Weekly Allowance

Given an employee is eligible for first aid allowance
And the active rule snapshot includes `payment_trigger = first_aid` and `payment_basis = per_week`
When the pay period is calculated
Then one weekly first aid allowance pay line is produced according to the rule basis
And the pay line traces to the source clause and rule version

## RULE-MQA-002: On-Call Monday To Friday

Given an employee is on call on a Wednesday
And the applicable rule is `On-call allowance - Monday to Friday`
When the event is calculated
Then the on-call allowance applies
And no Saturday, Sunday, or public holiday rule is selected

## RULE-MQA-003: On-Call Monday To Saturday

Given an employee is on call on Saturday
And the applicable rule is `Monday to Saturday`
When the event is calculated
Then the allowance applies
And Sunday-only rules do not apply

## RULE-MQA-004: On-Call Sunday Or Public Holiday

Given an employee is on call on Sunday
When the event is calculated
Then the Sunday/public holiday on-call allowance applies
And the trace shows whether the selected condition was Sunday or public holiday

## RULE-MQA-005: On-Call Per 24 Hour Period Or Part Thereof

Given an employee is on call for any part of a 24-hour period
When the event is calculated
Then the period quantity is rounded according to `per_24h_or_part`
And the pay line quantity matches the started period count

## RULE-MQA-006: Sleepover Additional To On-Call

Given an employee has both sleepover and on-call conditions for the same period
When the event is calculated
Then sleepover is added only if the rule snapshot marks it as additional to on-call
And the trace shows both rule references

## RULE-MQA-007: Sunday Public Holiday Overlap

Given Sunday is also a public holiday
And an employee works or is on call during that day
When the calculation runs
Then Sunday and public holiday entitlements are not double counted unless the rule explicitly allows compounding
And the selected stacking policy is visible in the trace

## RULE-MQA-008: Part-Day Public Holiday

Given a public holiday applies only for part of a day
And an employee works before, during, and after the public holiday period
When the timesheet is normalized
Then the segment timeline splits at the public holiday boundaries
And only the public holiday segment receives public holiday treatment

## RULE-MQA-009: Cross-Midnight Shift

Given an employee works from Friday night into Saturday morning
When the timesheet is normalized
Then the shift is split at midnight
And Friday and Saturday portions are calculated with the correct day conditions

## RULE-MQA-010: Blocked Tenant Reduction

Given a tenant override attempts to reduce a mandatory award entitlement
When the override is validated
Then the override is blocked
And a compliance exception explains the reduction risk

## RULE-MQA-011: Fatigue Break Double Time

Given an employee finishes work at midnight
And starts the next shift at 6:00 AM
And the applicable award requires a 10-hour break
When the next shift is calculated
Then fatigue penalty rules apply until the employee receives the required break
And the trace references previous shift end time

## RULE-MQA-012: Leave Loading Higher Of Rule

Given an employee takes annual leave on a day that would otherwise attract a penalty
When leave loading is calculated
Then the engine compares 17.5% leave loading against the applicable penalty outcome where the award requires it
And pays the higher entitlement

## RULE-MQA-013: BOOT Top-Up

Given an employee is paid under an EBA or salary arrangement
When the BOOT tester compares the arrangement against the base FWC award
Then any shortfall is emitted as a top-up
And the report explains the base award comparison and top-up formula
