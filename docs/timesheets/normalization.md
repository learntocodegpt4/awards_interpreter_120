# Timesheet Normalization

Timesheet normalization converts raw workforce events into canonical payable segments before the Rule Engine calculates rates and allowances.

## Inputs

- clock in and clock out events,
- break events and auto-deduct break settings,
- schedule/roster information,
- leave entries,
- public holiday calendar,
- on-call and recall events,
- sleepover events,
- tags such as first aid, extreme heat, travel, leading hand, or higher duties,
- previous shift state for fatigue rules.

## Canonical Segment

Each normalized segment should include:

- `tenant_id`
- `employee_id`
- `timesheet_id`
- `segment_id`
- `start_utc`
- `end_utc`
- `local_start`
- `local_end`
- `timezone`
- `state_or_region`
- `day_type`
- `public_holiday_id`
- `is_part_day_public_holiday`
- `hour_type`
- `shift_type`
- `tags`
- `source_event_ids`

## Segment Splitting Rules

Split raw time ranges at:

- midnight boundaries,
- public holiday start/end boundaries,
- part-day public holiday boundaries,
- rostered span-of-hours boundaries,
- overtime band thresholds,
- unpaid break boundaries,
- leave overlay boundaries,
- recall/on-call event boundaries,
- fatigue recovery boundaries.

## Fatigue State

Some awards require premium rates when a required break between shifts is not received. The normalizer must expose:

- previous shift end time,
- next shift start time,
- break duration,
- required break duration,
- fatigue breach flag,
- fatigue recovery time.

## Manual QA Focus

QA should validate the segment timeline before pay is calculated. A correct Rule Engine cannot recover from incorrectly split time.

Required manual scenarios:

- Friday to Saturday cross-midnight shift,
- ordinary day into public holiday,
- part-day public holiday from 7:00 PM to midnight,
- unpaid break inside overtime period,
- on-call event that leads to recall,
- insufficient break between shifts.

