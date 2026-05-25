"""Text normalization helpers shared by deterministic parser modules."""

from __future__ import annotations

import re
import unicodedata


_DASHES = {
    "\u2010": "-",
    "\u2011": "-",
    "\u2012": "-",
    "\u2013": "-",
    "\u2014": "-",
    "\u2212": "-",
}


def normalize_text(value: object | None) -> str:
    """Normalize noisy FWC text without erasing legal wording."""

    if value is None:
        return ""

    text = str(value)
    text = unicodedata.normalize("NFKC", text)
    for source, target in _DASHES.items():
        text = text.replace(source, target)

    text = text.replace("\u00a0", " ")
    text = re.sub(r"[ \t\r\f\v]+", " ", text)
    text = re.sub(r"\s*,\s*", ", ", text)
    text = re.sub(r"\s+", " ", text)
    return text.strip()


def normalize_for_match(value: object | None) -> str:
    """Return a lowercase comparison string for pattern matching."""

    text = normalize_text(value).lower()
    text = re.sub(r"\bon\s*-?\s*call\b", "on-call", text)
    text = re.sub(r"\bpublic holidays\b", "public holiday", text)
    text = re.sub(r"\bhrs\b", "hours", text)
    return text
