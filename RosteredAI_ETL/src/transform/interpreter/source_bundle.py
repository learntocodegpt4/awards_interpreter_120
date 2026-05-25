"""Source bundle and provenance model for noisy FWC semantic rows."""

from __future__ import annotations

from dataclasses import dataclass
import hashlib
import re
from typing import Any, Iterable, Mapping

from .normalizer import normalize_for_match, normalize_text


CLAUSE_CONTEXT_FIELDS = {
    "parent_allowance",
    "penalty_description",
    "clause_description",
    "clauses",
    "clause_text",
    "clause_statement",
    "table_header",
    "table_caption",
}

SOURCE_FIELD_ORDER = (
    "allowance",
    "parent_allowance",
    "penalty_description",
    "clause_description",
    "clauses",
    "clause_text",
    "clause_statement",
    "table_header",
    "table_caption",
    "payment_frequency",
    "rate_unit",
)

ROW_ID_KEYS = ("row_id", "rowId", "id", "source_row_id", "fixed_id", "fixedId")
CLAUSE_KEYS = ("clause", "clause_reference", "clauseReference", "clause_id", "clauseId")


@dataclass(frozen=True)
class SourceField:
    name: str
    text: str
    match_text: str
    provenance: str


@dataclass(frozen=True)
class SourceMatch:
    field_name: str
    text: str
    pattern_text: str
    provenance: str


@dataclass(frozen=True)
class SourceBundle:
    row_id: str
    award_code: str
    clause_reference: str
    fields: tuple[SourceField, ...]
    source_key_hash: str

    @property
    def source_text(self) -> str:
        return " | ".join(f"{field.name}: {field.text}" for field in self.fields)

    @property
    def combined_match_text(self) -> str:
        return " | ".join(field.match_text for field in self.fields)

    def first_match(self, patterns: Iterable[str], fields: Iterable[str] | None = None) -> SourceMatch | None:
        allowed_fields = set(fields) if fields is not None else None
        for pattern in patterns:
            compiled = re.compile(pattern, re.IGNORECASE)
            for field in self.fields:
                if allowed_fields is not None and field.name not in allowed_fields:
                    continue
                match = compiled.search(field.match_text)
                if match:
                    return SourceMatch(
                        field_name=field.name,
                        text=normalize_text(match.group(0)),
                        pattern_text=match.group(0),
                        provenance=field.provenance,
                    )
        return None

    def has(self, pattern: str) -> bool:
        return re.search(pattern, self.combined_match_text, re.IGNORECASE) is not None


def build_source_bundle(row: Mapping[str, Any]) -> SourceBundle:
    fields: list[SourceField] = []
    for key in SOURCE_FIELD_ORDER:
        value = row.get(key)
        text = normalize_text(value)
        if not text:
            continue
        fields.append(
            SourceField(
                name=key,
                text=text,
                match_text=normalize_for_match(text),
                provenance="clause_context" if key in CLAUSE_CONTEXT_FIELDS else "explicit_text",
            )
        )

    award_code = str(row.get("award_code") or row.get("awardCode") or "")
    row_id = _first_raw_value(row, ROW_ID_KEYS) or _stable_row_id(award_code, fields)
    clause_reference = _first_value(row, CLAUSE_KEYS) or ""
    source_key_hash = _hash_fields(award_code, row_id, fields)

    return SourceBundle(
        row_id=row_id,
        award_code=award_code,
        clause_reference=clause_reference,
        fields=tuple(fields),
        source_key_hash=source_key_hash,
    )


def _first_value(row: Mapping[str, Any], keys: Iterable[str]) -> str:
    for key in keys:
        value = normalize_text(row.get(key))
        if value:
            return value
    return ""


def _first_raw_value(row: Mapping[str, Any], keys: Iterable[str]) -> str:
    for key in keys:
        value = row.get(key)
        if value is None:
            continue
        text = str(value).strip()
        if text:
            return text
    return ""


def _stable_row_id(award_code: str, fields: Iterable[SourceField]) -> str:
    digest = hashlib.sha256()
    digest.update(award_code.encode("utf-8"))
    for field in fields:
        digest.update(field.name.encode("utf-8"))
        digest.update(field.text.encode("utf-8"))
    return digest.hexdigest()[:16]


def _hash_fields(award_code: str, row_id: str, fields: Iterable[SourceField]) -> str:
    digest = hashlib.sha256()
    digest.update(award_code.encode("utf-8"))
    digest.update(row_id.encode("utf-8"))
    for field in fields:
        digest.update(field.name.encode("utf-8"))
        digest.update(b"\0")
        digest.update(field.text.encode("utf-8"))
        digest.update(b"\0")
    return digest.hexdigest()
