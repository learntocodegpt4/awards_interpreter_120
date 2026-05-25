"""Compatibility wrapper for legacy ETL award semantic enrichment."""

from __future__ import annotations

from typing import Any, Mapping

from .interpreter import build_source_bundle, parse_award_row, parse_condition_json


def interpret_award_row(row: Mapping[str, Any]) -> dict[str, Any]:
    """Return a serialized semantic row with flat fields and conditionJson."""

    return parse_award_row(row).to_dict()


def enrich_row(row: Mapping[str, Any]) -> dict[str, Any]:
    """Merge parser output into an existing staging row dictionary."""

    enriched = dict(row)
    parsed = interpret_award_row(row)
    enriched.update(parsed)
    return enriched


__all__ = ["build_source_bundle", "enrich_row", "interpret_award_row", "parse_condition_json"]
