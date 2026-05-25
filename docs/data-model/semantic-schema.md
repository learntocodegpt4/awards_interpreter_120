# Semantic Data Model

This document defines the structured semantic contract produced by the Clause Interpreter and consumed by rule compilation.

## Compatibility Fields

Existing flat fields remain useful for reporting and transitional consumers:

| Field | Meaning |
|---|---|
| `allowance_category` | Normalized allowance family such as `first_aid`, `on_call`, `meal`, `leading_hand`. |
| `applies_to` | Human-readable target or role restriction when available. |
| `applies_day_type` | Compatibility day label such as `weekday`, `saturday`, `sunday`, `public_holiday`, `everyday`. |
| `applies_hour_type` | `ordinary`, `overtime`, `on_call`, `recall`, or `any`. |
| `applies_time_condition` | Condition such as `first_2_hours`, `after_7pm`, `midnight_to_7am`. |
| `applies_shift_type` | Shift condition such as `night`, `afternoon`, `broken`, `sleepover`. |
| `payment_trigger` | Event or entitlement trigger such as `first_aid`, `on_call`, `extreme_heat`. |
| `payment_timing` | Timing label such as `first_meal`, `second_or_subsequent_meal`. |
| `payment_basis` | Basis such as `per_week`, `per_day`, `per_hour`, `per_shift`, `per_24h_or_part`. |
| `parsed_confidence` | Numeric parser confidence from 0.0000 to 1.0000. |
| `parse_status` | Review/publication state. |
| `parse_notes` | Human-readable parser explanation or warning. |

## Canonical Enums

### Day Values

- `monday`
- `tuesday`
- `wednesday`
- `thursday`
- `friday`
- `saturday`
- `sunday`

### Day Types

- `weekday`
- `weekend`
- `saturday`
- `sunday`
- `public_holiday`
- `everyday`
- `non_public_holiday`
- `off_duty_day`

### Hour Types

- `ordinary`
- `overtime`
- `on_call`
- `recall`
- `sleepover`
- `any`

### Shift Types

- `standard`
- `night`
- `afternoon`
- `early_morning`
- `broken`
- `split`
- `sleepover`

### Payment Triggers

- `first_aid`
- `on_call`
- `recall`
- `leading_hand`
- `industry_disability`
- `meal`
- `travel`
- `laundry`
- `uniform`
- `tool`
- `vehicle`
- `cold_temperature`
- `extreme_heat`
- `sleepover`
- `manual_review_required`

### Payment Basis

- `per_hour`
- `per_day`
- `per_week`
- `per_shift`
- `per_event`
- `per_meal`
- `per_occasion`
- `per_24h_or_part`
- `per_annum`
- `percentage_of_base`

### Base Rate Reference

- `ordinary_rate`
- `minimum_rate`
- `loaded_rate`
- `all_purpose_rate`
- `base_pay_rate`
- `manual_review_required`

### Provenance

- `explicit_text`
- `clause_context`
- `domain_default`
- `tenant_config`
- `manual_override`
- `llm_suggestion`

### Parse Status

- `complete`
- `complete_defaulted`
- `partial`
- `manual_review`
- `llm_suggested`
- `conflict`
- `failed`
- `quarantined`

## Condition JSON

The normalized `condition_json` should carry structured rule semantics plus evidence.

```json
{
  "schema_version": "1.0",
  "entity_type": "allowance",
  "days": {
    "mode": "include",
    "values": ["monday", "tuesday", "wednesday", "thursday", "friday"],
    "source": "explicit_text"
  },
  "day_types": ["weekday"],
  "public_holiday": false,
  "hour_type": "on_call",
  "time_window": null,
  "shift_type": null,
  "trigger": "on_call",
  "basis": "per_day",
  "is_compounding": false,
  "base_rate_reference": "ordinary_rate",
  "period_rounding": null,
  "stacking_policy": "exclusive",
  "defaulted_fields": [],
  "evidence": [
    {
      "field": "days",
      "text": "Monday to Friday",
      "source": "allowance"
    }
  ]
}
```

## Domain Default Example

```json
{
  "schema_version": "1.0",
  "entity_type": "allowance",
  "days": {
    "mode": "include",
    "values": ["monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday"],
    "source": "domain_default"
  },
  "day_types": ["everyday"],
  "public_holiday": true,
  "trigger": "first_aid",
  "basis": "per_week",
  "defaulted_fields": ["days", "day_types"],
  "evidence": [
    {
      "field": "trigger",
      "text": "First aid allowance",
      "source": "allowance"
    },
    {
      "field": "basis",
      "text": "per week",
      "source": "payment_frequency"
    }
  ]
}
```

## Publication Rules

- `complete` rows may publish if all required payable fields exist.
- `complete_defaulted` rows may publish only if the default is approved and documented.
- `partial`, `manual_review`, `llm_suggested`, `conflict`, `failed`, and `quarantined` rows must not become payable rules without review.
- Any manual override must preserve original source evidence and record reviewer identity, timestamp, and reason.

## Temporal Model

Awards, rates, allowances, penalties, tenant overrides, and compiled rule snapshots must use SCD Type 2 style effective dating.

Required temporal fields:

- `operative_from`
- `operative_to`
- `version_number`
- `published_year`
- `source_key_hash`
- `created_at`
- `updated_at`

Rule lookup must use the work date, not the current system date:

```sql
WHERE operative_from <= @work_date
  AND (operative_to IS NULL OR operative_to > @work_date)
```

## Compounding Fields

Penalty and loading rules should carry explicit compounding metadata:

| Field | Meaning |
|---|---|
| `is_compounding` | Whether the rule multiplies against an already loaded/enhanced rate. |
| `base_rate_reference` | The base to which the rule applies, such as `ordinary_rate` or `loaded_rate`. |
| `stacking_policy` | Whether rules are cumulative, replacement, highest-of, exclusive, or additional-to. |
| `compounding_evidence` | Source wording that justified the decision. |

Ambiguous compounding wording must route to review.
