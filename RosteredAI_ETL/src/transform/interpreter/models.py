"""Serializable models produced by the ETL clause interpreter."""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any, Mapping

from .constants import PARSER_VERSION, SCHEMA_VERSION


@dataclass(frozen=True)
class Evidence:
    field: str
    text: str
    source: str
    provenance: str = "explicit_text"

    def to_dict(self) -> dict[str, str]:
        return {
            "field": self.field,
            "text": self.text,
            "source": self.source,
            "provenance": self.provenance,
        }


@dataclass
class ConditionBuildState:
    entity_type: str
    trigger: str | None = None
    category: str | None = None
    basis: str | None = None
    days: list[str] = field(default_factory=list)
    day_source: str | None = None
    day_types: list[str] = field(default_factory=list)
    public_holiday: bool = False
    hour_type: str | None = None
    time_window: dict[str, Any] | None = None
    time_condition: str | None = None
    shift_type: str | None = None
    period_rounding: str | None = None
    is_compounding: bool | None = None
    base_rate_reference: str | None = None
    stacking_policy: str = "exclusive"
    dependencies: list[dict[str, str]] = field(default_factory=list)
    thresholds: dict[str, Any] = field(default_factory=dict)
    defaulted_fields: list[str] = field(default_factory=list)
    evidence: list[Evidence] = field(default_factory=list)
    compounding_evidence: list[Evidence] = field(default_factory=list)
    notes: list[str] = field(default_factory=list)
    review_reasons: list[str] = field(default_factory=list)
    confidence: float = 0.0
    parse_status: str = "manual_review"

    def add_evidence(self, field: str, text: str, source: str, provenance: str = "explicit_text") -> None:
        if not text:
            return
        candidate = Evidence(field=field, text=text, source=source, provenance=provenance)
        if candidate not in self.evidence:
            self.evidence.append(candidate)

    def make_evidence(self, field: str, text: str, source: str, provenance: str = "explicit_text") -> Evidence:
        return Evidence(field=field, text=text, source=source, provenance=provenance)

    def to_condition_json(self) -> dict[str, Any]:
        condition: dict[str, Any] = {
            "schema_version": SCHEMA_VERSION,
            "entity_type": self.entity_type,
            "trigger": self.trigger or "manual_review_required",
            "category": self.category or self.trigger or "manual_review_required",
            "basis": self.basis,
            "days": None,
            "day_types": self.day_types,
            "public_holiday": self.public_holiday,
            "hour_type": self.hour_type,
            "time_window": self.time_window,
            "time_condition": self.time_condition,
            "shift_type": self.shift_type,
            "is_compounding": bool(self.is_compounding) if self.is_compounding is not None else False,
            "base_rate_reference": self.base_rate_reference or "manual_review_required",
            "period_rounding": self.period_rounding,
            "stacking_policy": self.stacking_policy,
            "dependencies": self.dependencies,
            "thresholds": self.thresholds,
            "defaulted_fields": self.defaulted_fields,
            "evidence": [item.to_dict() for item in self.evidence],
            "compounding_evidence": [item.to_dict() for item in self.compounding_evidence],
            "confidence": round(self.confidence, 4),
            "parse_status": self.parse_status,
            "parser_version": PARSER_VERSION,
        }

        if self.days:
            condition["days"] = {
                "mode": "include",
                "values": self.days,
                "source": self.day_source or "explicit_text",
            }

        return condition


@dataclass(frozen=True)
class ParsedSemanticRow:
    row_id: str
    award_code: str
    award_reference: dict[str, str]
    source_text: str
    source_key_hash: str
    compatibility_fields: dict[str, Any]
    condition_json: dict[str, Any]
    parsed_confidence: float
    parse_status: str
    parse_notes: list[str]
    review_status: str
    parser_version: str = PARSER_VERSION

    def to_dict(self) -> dict[str, Any]:
        return {
            "rowId": self.row_id,
            "awardCode": self.award_code,
            "awardReference": self.award_reference,
            "sourceText": self.source_text,
            "sourceKeyHash": self.source_key_hash,
            "parserVersion": self.parser_version,
            "reviewStatus": self.review_status,
            "conditionJson": self.condition_json,
            "interpretationNotes": self.parse_notes,
            **self.compatibility_fields,
        }


def compact_dict(values: Mapping[str, Any]) -> dict[str, Any]:
    """Drop empty values while keeping false booleans and zeroes."""

    return {key: value for key, value in values.items() if value not in (None, "", [], {})}
