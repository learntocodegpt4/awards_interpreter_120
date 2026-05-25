"""Deterministic allowance and penalty parsers for governed ETL output."""

from __future__ import annotations

import re
from typing import Any, Mapping

from .constants import DAYS, PARSER_VERSION
from .models import ConditionBuildState, ParsedSemanticRow, compact_dict
from .source_bundle import SourceBundle, build_source_bundle

DAY_INDEX = {day: index for index, day in enumerate(DAYS)}
DAY_PATTERNS = {
    "monday": r"\bmonday\b",
    "tuesday": r"\btuesday\b",
    "wednesday": r"\bwednesday\b",
    "thursday": r"\bthursday\b",
    "friday": r"\bfriday\b",
    "saturday": r"\bsaturday\b",
    "sunday": r"\bsunday\b",
}


def parse_award_row(row: Mapping[str, Any]) -> ParsedSemanticRow:
    """Parse one staging row into compatibility fields plus condition_json."""

    bundle = build_source_bundle(row)
    entity_type = _detect_entity_type(row, bundle)
    state = ConditionBuildState(entity_type=entity_type)

    if entity_type == "penalty":
        _parse_penalty(bundle, state)
    else:
        _parse_allowance(bundle, state)

    _parse_days(bundle, state)
    _parse_payment_basis(bundle, state)
    _apply_domain_defaults(bundle, state)
    _validate_and_score(bundle, state)

    compatibility = _compatibility_fields(state)
    award_reference = compact_dict(
        {
            "clause": bundle.clause_reference or "unknown",
            "title": row.get("clause_title") or row.get("clauseTitle") or "",
            "url": row.get("source_url") or row.get("sourceUrl") or "",
        }
    )

    return ParsedSemanticRow(
        row_id=bundle.row_id,
        award_code=bundle.award_code,
        award_reference=award_reference,
        source_text=bundle.source_text,
        source_key_hash=bundle.source_key_hash,
        compatibility_fields=compatibility,
        condition_json=state.to_condition_json(),
        parsed_confidence=round(state.confidence, 4),
        parse_status=state.parse_status,
        parse_notes=state.notes + state.review_reasons,
        review_status="candidate" if state.parse_status in {"complete", "complete_defaulted"} else "needs_review",
        parser_version=PARSER_VERSION,
    )


def parse_condition_json(row: Mapping[str, Any]) -> dict[str, Any]:
    return parse_award_row(row).condition_json


def _detect_entity_type(row: Mapping[str, Any], bundle: SourceBundle) -> str:
    explicit_type = str(row.get("entity_type") or row.get("entityType") or row.get("row_type") or row.get("rowType") or "").lower()
    if explicit_type in {"penalty", "loading"}:
        return "penalty"
    if bundle.first_match([r".+"], fields=("penalty_description",)):
        return "penalty"
    return "allowance"


def _parse_allowance(bundle: SourceBundle, state: ConditionBuildState) -> None:
    trigger_specs = (
        ("first_aid", "first_aid", r"\bfirst aid\b", "any"),
        ("sleepover", "sleepover", r"\bsleepover\b", "sleepover"),
        ("on_call", "on_call", r"\bon-call\b", "on_call"),
        ("recall", "recall", r"\brecall\b", "recall"),
        ("leading_hand", "leading_hand", r"\bleading hand\b", "any"),
        ("industry_disability", "industry_disability", r"\bindustry disability\b", "any"),
        ("meal", "meal", r"\bmeal\b", "any"),
        ("travel", "travel", r"\b(excess fares|travel)\b", "any"),
        ("laundry", "laundry", r"\blaundry\b", "any"),
        ("uniform", "uniform", r"\buniform\b", "any"),
        ("tool", "tool", r"\btool\b", "any"),
        ("vehicle", "vehicle", r"\b(vehicle|motorcycle|car allowance)\b", "any"),
        ("cold_temperature", "cold_temperature", r"\b(cold|freezer|refrigerated)\b", "any"),
        ("extreme_heat", "extreme_heat", r"\b(extreme heat|excessive heat|heat)\b", "any"),
    )

    for trigger, category, pattern, hour_type in trigger_specs:
        match = bundle.first_match([pattern])
        if match:
            state.trigger = trigger
            state.category = category
            state.hour_type = hour_type
            state.add_evidence("trigger", match.text, match.field_name, match.provenance)
            break

    if state.trigger == "leading_hand":
        _parse_leading_hand_threshold(bundle, state)
    elif state.trigger in {"cold_temperature", "extreme_heat"}:
        _parse_temperature_threshold(bundle, state)
    elif state.trigger == "sleepover":
        state.shift_type = "sleepover"
        dependency = bundle.first_match([r"\badditional to (?:the )?on-call allowance\b", r"\bin addition to (?:the )?on-call allowance\b"])
        if dependency:
            state.dependencies.append({"trigger": "on_call"})
            state.stacking_policy = "additional_to"
            state.add_evidence("stacking_policy", dependency.text, dependency.field_name, dependency.provenance)


def _parse_penalty(bundle: SourceBundle, state: ConditionBuildState) -> None:
    trigger_specs = (
        ("overtime", r"\bovertime\b", "overtime"),
        ("public_holiday", r"\bpublic holiday\b", "ordinary"),
        ("weekend", r"\b(saturday|sunday|weekend)\b", "ordinary"),
        ("shiftwork", r"\b(shiftwork|shift worker|night shift|afternoon shift|early morning)\b", "ordinary"),
        ("meal", r"\bmeal break\b", "ordinary"),
    )

    for trigger, pattern, hour_type in trigger_specs:
        match = bundle.first_match([pattern])
        if match:
            state.trigger = trigger
            state.category = trigger
            state.hour_type = hour_type
            state.add_evidence("trigger", match.text, match.field_name, match.provenance)
            break

    if first_two := bundle.first_match([r"\bfirst\s+2\s+hours\b", r"\bfirst\s+two\s+hours\b"]):
        state.time_condition = "first_2_hours"
        state.add_evidence("time_condition", first_two.text, first_two.field_name, first_two.provenance)
    elif after_two := bundle.first_match([r"\bafter\s+2\s+hours\b", r"\bafter\s+two\s+hours\b"]):
        state.time_condition = "after_2_hours"
        state.add_evidence("time_condition", after_two.text, after_two.field_name, after_two.provenance)

    if night := bundle.first_match([r"\bmidnight\s+to\s+7\s*am\b", r"\bafter\s+7\s*pm\b"]):
        state.time_window = {"label": night.text}
        state.add_evidence("time_window", night.text, night.field_name, night.provenance)

    _parse_penalty_compounding(bundle, state)
    _parse_penalty_multiplier(bundle, state)


def _parse_penalty_compounding(bundle: SourceBundle, state: ConditionBuildState) -> None:
    specs = (
        ("ordinary_rate", False, "replacement", r"\b(ordinary(?: hourly)? rate|ordinary time rate)\b"),
        ("minimum_rate", False, "replacement", r"\bminimum(?: hourly)? rate\b"),
        ("all_purpose_rate", False, "replacement", r"\ball-purpose rate\b"),
        ("base_pay_rate", False, "replacement", r"\bbase pay rate\b"),
        ("loaded_rate", True, "cumulative", r"\b(loaded rate|already loaded|enhanced rate)\b"),
    )
    for base_reference, is_compounding, stacking_policy, pattern in specs:
        match = bundle.first_match([pattern])
        if match:
            state.base_rate_reference = base_reference
            state.is_compounding = is_compounding
            state.stacking_policy = stacking_policy
            state.compounding_evidence.append(
                state.make_evidence("base_rate_reference", match.text, match.field_name, match.provenance)
            )
            state.add_evidence("base_rate_reference", match.text, match.field_name, match.provenance)
            break

    inclusive = bundle.first_match([r"\binclusive of casual loading\b", r"\bincludes casual loading\b"])
    if inclusive:
        state.is_compounding = False
        state.stacking_policy = "inclusive"
        state.compounding_evidence.append(
            state.make_evidence("stacking_policy", inclusive.text, inclusive.field_name, inclusive.provenance)
        )
        state.add_evidence("stacking_policy", inclusive.text, inclusive.field_name, inclusive.provenance)

    additional = bundle.first_match([r"\bin addition to\b", r"\badditional to\b", r"\bplus\b"])
    if additional and state.base_rate_reference == "loaded_rate":
        state.is_compounding = True
        state.stacking_policy = "cumulative"
        state.add_evidence("stacking_policy", additional.text, additional.field_name, additional.provenance)

    ambiguous = bundle.first_match([r"\bapplicable rate\b", r"\bincluding any loadings\b", r"\bplus applicable loadings\b"])
    if ambiguous and state.base_rate_reference is None:
        state.base_rate_reference = "manual_review_required"
        state.review_reasons.append("Penalty base-rate or compounding wording is ambiguous.")
        state.add_evidence("base_rate_reference", ambiguous.text, ambiguous.field_name, ambiguous.provenance)


def _parse_penalty_multiplier(bundle: SourceBundle, state: ConditionBuildState) -> None:
    multiplier_patterns = (
        (2.5, r"\b250\s*%|\bdouble time and a half\b"),
        (2.25, r"\b225\s*%"),
        (2.0, r"\b200\s*%|\bdouble time\b"),
        (1.75, r"\b175\s*%"),
        (1.5, r"\b150\s*%|\btime and a half\b"),
    )
    for multiplier, pattern in multiplier_patterns:
        match = bundle.first_match([pattern])
        if match:
            state.thresholds["multiplier"] = multiplier
            state.add_evidence("multiplier", match.text, match.field_name, match.provenance)
            return


def _parse_days(bundle: SourceBundle, state: ConditionBuildState) -> None:
    if non_public := bundle.first_match([r"\bother than (?:a )?public holiday\b", r"\bnon-public holiday\b"]):
        _add_day_type(state, "non_public_holiday")
        state.public_holiday = False
        state.add_evidence("public_holiday", non_public.text, non_public.field_name, non_public.provenance)
        state.review_reasons.append("Public holiday exclusion needs review before publication.")

    range_match = bundle.first_match([r"\b(monday|tuesday|wednesday|thursday|friday|saturday|sunday)\s+(?:to|-)\s+(monday|tuesday|wednesday|thursday|friday|saturday|sunday)\b"])
    if range_match:
        match = re.search(
            r"\b(monday|tuesday|wednesday|thursday|friday|saturday|sunday)\s+(?:to|-)\s+(monday|tuesday|wednesday|thursday|friday|saturday|sunday)\b",
            range_match.pattern_text,
            re.IGNORECASE,
        )
        if match:
            start, end = match.group(1).lower(), match.group(2).lower()
            state.days = _day_range(start, end)
            state.day_source = range_match.provenance
            state.add_evidence("days", range_match.text, range_match.field_name, range_match.provenance)
            _derive_day_types_from_days(state)

    if not state.days:
        matched_days: list[str] = []
        day_source = "explicit_text"
        for day, pattern in DAY_PATTERNS.items():
            match = bundle.first_match([pattern])
            if match:
                matched_days.append(day)
                day_source = match.provenance
                state.add_evidence("days", match.text, match.field_name, match.provenance)
        if matched_days:
            state.days = [day for day in DAYS if day in set(matched_days)]
            state.day_source = day_source
            _derive_day_types_from_days(state)

    if weekday := bundle.first_match([r"\bweekdays?\b", r"\bmonday\s+(?:to|-)\s+friday\b"]):
        if not state.days:
            state.days = list(DAYS[:5])
            state.day_source = weekday.provenance
            state.add_evidence("days", weekday.text, weekday.field_name, weekday.provenance)
        _add_day_type(state, "weekday")

    if weekend := bundle.first_match([r"\bweekends?\b"]):
        if not state.days:
            state.days = ["saturday", "sunday"]
            state.day_source = weekend.provenance
            state.add_evidence("days", weekend.text, weekend.field_name, weekend.provenance)
        _add_day_type(state, "weekend")

    if public_holiday := bundle.first_match([r"\bpublic holiday\b"]):
        state.public_holiday = True
        _add_day_type(state, "public_holiday")
        state.add_evidence("public_holiday", public_holiday.text, public_holiday.field_name, public_holiday.provenance)

    if state.public_holiday and "sunday" in state.days:
        state.notes.append("Sunday and public holiday were preserved separately to prevent duplicate downstream entitlements.")


def _parse_payment_basis(bundle: SourceBundle, state: ConditionBuildState) -> None:
    specs = (
        ("per_24h_or_part", "started_24_hour_period_counts", r"\bper\s+24\s+hour\s+period\s+or\s+part\s+thereof\b"),
        ("per_hour", "started_hour_counts", r"\bper\s+hour\s+or\s+part\s+thereof\b"),
        ("per_week", None, r"\bper\s+week\b|\bweekly\b"),
        ("per_day", None, r"\bper\s+day\b|\bdaily\b"),
        ("per_hour", None, r"\bper\s+hour\b|\bhourly\b"),
        ("per_shift", None, r"\bper\s+shift\b"),
        ("per_meal", None, r"\bper\s+meal\b"),
        ("per_occasion", None, r"\bper\s+occasion\b"),
        ("per_annum", None, r"\bper\s+annum\b|\bannual\b"),
        ("per_event", None, r"\bper\s+(?:event|call|screen test)\b"),
    )
    for basis, rounding, pattern in specs:
        match = bundle.first_match([pattern], fields=("payment_frequency", "rate_unit", "allowance", "penalty_description", "clause_description", "clause_text", "table_header"))
        if match:
            state.basis = basis
            state.period_rounding = rounding
            state.add_evidence("basis", match.text, match.field_name, match.provenance)
            if rounding:
                state.add_evidence("period_rounding", match.text, match.field_name, match.provenance)
            return

    if percent := bundle.first_match([r"\bpercent\b|%"], fields=("rate_unit",)):
        state.basis = "percentage_of_base"
        state.add_evidence("basis", percent.text, percent.field_name, percent.provenance)


def _apply_domain_defaults(bundle: SourceBundle, state: ConditionBuildState) -> None:
    if state.days or state.public_holiday or state.trigger not in {"first_aid", "industry_disability", "leading_hand"}:
        return

    if restrictive := bundle.first_match([r"\bonly\b", r"\bwhere\b", r"\bwhen\b", r"\bif\b", r"\bother than\b"]):
        state.review_reasons.append(f"Domain default suppressed because restrictive wording was found in {restrictive.field_name}.")
        state.add_evidence("default_suppressed", restrictive.text, restrictive.field_name, restrictive.provenance)
        return

    state.days = list(DAYS)
    state.day_source = "domain_default"
    state.day_types = ["everyday"]
    state.public_holiday = True
    for field in ("days", "day_types", "public_holiday"):
        if field not in state.defaulted_fields:
            state.defaulted_fields.append(field)
    state.add_evidence(
        "days",
        f"{state.trigger} allowance approved everyday domain default",
        "domain_default",
        "domain_default",
    )


def _validate_and_score(bundle: SourceBundle, state: ConditionBuildState) -> None:
    if not state.trigger:
        state.trigger = "manual_review_required"
        state.category = "manual_review_required"
        state.review_reasons.append("No deterministic trigger/category matched the source bundle.")

    if state.entity_type == "allowance" and state.trigger == "on_call" and not (state.days or state.public_holiday):
        state.review_reasons.append("On-call day applicability is ambiguous without explicit day or public holiday wording.")

    if state.entity_type == "penalty":
        if state.base_rate_reference is None:
            state.base_rate_reference = "manual_review_required"
            state.review_reasons.append("Penalty base-rate reference was not found in source text.")
        if state.is_compounding is None:
            state.is_compounding = False

    score = 0.25
    if state.trigger != "manual_review_required":
        score += 0.2
    if state.basis:
        score += 0.15
    if state.days or state.public_holiday:
        score += 0.15
    if state.defaulted_fields:
        score -= 0.03
    if state.entity_type == "penalty" and state.base_rate_reference != "manual_review_required":
        score += 0.2
    if state.evidence:
        score += 0.1
    if state.thresholds:
        score += 0.05
    if state.dependencies or state.shift_type:
        score += 0.05
    if state.review_reasons:
        score = min(score, 0.69)

    state.confidence = max(0.0, min(score, 0.99))
    if state.review_reasons:
        state.parse_status = "manual_review" if state.confidence < 0.7 else "partial"
    elif state.defaulted_fields:
        state.parse_status = "complete_defaulted"
    elif state.confidence >= 0.9 or (state.entity_type == "allowance" and state.confidence >= 0.75):
        state.parse_status = "complete"
    else:
        state.parse_status = "partial"


def _compatibility_fields(state: ConditionBuildState) -> dict[str, Any]:
    day_type = ""
    if "public_holiday" in state.day_types:
        day_type = "public_holiday"
    elif state.day_types:
        day_type = state.day_types[0]
    elif state.days:
        day_type = state.days[0] if len(state.days) == 1 else "day_set"

    return {
        "allowance_category": state.category if state.entity_type == "allowance" else None,
        "penalty_category": state.category if state.entity_type == "penalty" else None,
        "applies_to": None,
        "applies_day_type": day_type or None,
        "applies_hour_type": state.hour_type,
        "applies_time_condition": state.time_condition,
        "applies_shift_type": state.shift_type,
        "payment_trigger": state.trigger,
        "payment_timing": state.time_condition,
        "payment_basis": state.basis,
        "parsed_confidence": round(state.confidence, 4),
        "parse_status": state.parse_status,
        "parse_notes": "; ".join(state.notes + state.review_reasons),
    }


def _parse_leading_hand_threshold(bundle: SourceBundle, state: ConditionBuildState) -> None:
    range_match = bundle.first_match([r"\b(\d+)\s*-\s*(\d+)\s+employees?\b", r"\b(\d+)\s+to\s+(\d+)\s+employees?\b"])
    if range_match:
        numbers = re.findall(r"\d+", range_match.pattern_text)
        if len(numbers) >= 2:
            state.thresholds["employees"] = {"minimum": int(numbers[0]), "maximum": int(numbers[1])}
            state.add_evidence("thresholds.employees", range_match.text, range_match.field_name, range_match.provenance)
        return

    more_than = bundle.first_match([r"\bmore than\s+(\d+)\s+employees?\b"])
    if more_than:
        numbers = re.findall(r"\d+", more_than.pattern_text)
        if numbers:
            state.thresholds["employees"] = {"minimum": int(numbers[0]) + 1}
            state.add_evidence("thresholds.employees", more_than.text, more_than.field_name, more_than.provenance)


def _parse_temperature_threshold(bundle: SourceBundle, state: ConditionBuildState) -> None:
    threshold = bundle.first_match([r"\b(?:below|under|less than|exceeds?|over|above)\s+(-?\d+(?:\.\d+)?)\s*(?:degrees?|c|celsius)\b"])
    if threshold:
        number = re.search(r"-?\d+(?:\.\d+)?", threshold.pattern_text)
        if number:
            state.thresholds["temperature_celsius"] = float(number.group(0))
            state.add_evidence("thresholds.temperature_celsius", threshold.text, threshold.field_name, threshold.provenance)


def _derive_day_types_from_days(state: ConditionBuildState) -> None:
    day_set = set(state.days)
    if day_set == set(DAYS):
        _add_day_type(state, "everyday")
    elif day_set == set(DAYS[:5]):
        _add_day_type(state, "weekday")
    elif day_set == {"saturday", "sunday"}:
        _add_day_type(state, "weekend")
    else:
        if "saturday" in day_set:
            _add_day_type(state, "saturday")
        if "sunday" in day_set:
            _add_day_type(state, "sunday")


def _add_day_type(state: ConditionBuildState, day_type: str) -> None:
    if day_type not in state.day_types:
        state.day_types.append(day_type)


def _day_range(start: str, end: str) -> list[str]:
    start_index = DAY_INDEX[start]
    end_index = DAY_INDEX[end]
    if start_index <= end_index:
        return list(DAYS[start_index : end_index + 1])
    return list(DAYS[start_index:]) + list(DAYS[: end_index + 1])
