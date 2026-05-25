"""Canonical semantic vocabulary for the ETL clause interpreter."""

PARSER_VERSION = "etl-clause-interpreter/1.0.0"
SCHEMA_VERSION = "1.0"

DAYS = (
    "monday",
    "tuesday",
    "wednesday",
    "thursday",
    "friday",
    "saturday",
    "sunday",
)

DAY_TYPES = {
    "weekday",
    "weekend",
    "saturday",
    "sunday",
    "public_holiday",
    "everyday",
    "non_public_holiday",
    "off_duty_day",
}

HOUR_TYPES = {"ordinary", "overtime", "on_call", "recall", "sleepover", "any"}

SHIFT_TYPES = {
    "standard",
    "night",
    "afternoon",
    "early_morning",
    "broken",
    "split",
    "sleepover",
}

PAYMENT_TRIGGERS = {
    "first_aid",
    "on_call",
    "recall",
    "leading_hand",
    "industry_disability",
    "meal",
    "travel",
    "laundry",
    "uniform",
    "tool",
    "vehicle",
    "cold_temperature",
    "extreme_heat",
    "sleepover",
    "overtime",
    "public_holiday",
    "weekend",
    "shiftwork",
    "manual_review_required",
}

PAYMENT_BASIS = {
    "per_hour",
    "per_day",
    "per_week",
    "per_shift",
    "per_event",
    "per_meal",
    "per_occasion",
    "per_24h_or_part",
    "per_annum",
    "percentage_of_base",
}

BASE_RATE_REFERENCES = {
    "ordinary_rate",
    "minimum_rate",
    "loaded_rate",
    "all_purpose_rate",
    "base_pay_rate",
    "manual_review_required",
}

PROVENANCE = {
    "explicit_text",
    "clause_context",
    "domain_default",
    "tenant_config",
    "manual_override",
    "llm_suggestion",
}

PARSE_STATUSES = {
    "complete",
    "complete_defaulted",
    "partial",
    "manual_review",
    "llm_suggested",
    "conflict",
    "failed",
    "quarantined",
}
