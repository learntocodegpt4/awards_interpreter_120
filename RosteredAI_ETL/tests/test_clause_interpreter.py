from __future__ import annotations

import sys
import unittest
from pathlib import Path


PROJECT_ROOT = Path(__file__).resolve().parents[1]
SRC_ROOT = PROJECT_ROOT / "src"
sys.path.insert(0, str(SRC_ROOT))

from transform.award_semantics import enrich_row, interpret_award_row
from transform.interpreter.constants import (
    BASE_RATE_REFERENCES,
    PARSE_STATUSES,
    PAYMENT_BASIS,
    PAYMENT_TRIGGERS,
    PROVENANCE,
)
from transform.interpreter.parser import parse_award_row
from transform.interpreter.source_bundle import build_source_bundle


class ClauseInterpreterTests(unittest.TestCase):
    def test_source_bundle_keeps_field_provenance_and_hash(self) -> None:
        bundle = build_source_bundle(
            {
                "row_id": "bundle-1",
                "award_code": "MA000120",
                "allowance": "On-call allowance",
                "clause_description": "Applies Monday to Saturday",
                "payment_frequency": "per day",
            }
        )

        self.assertEqual(bundle.row_id, "bundle-1")
        self.assertEqual(len(bundle.source_key_hash), 64)
        self.assertIn("allowance: On-call allowance", bundle.source_text)
        self.assertIn("clause_description: Applies Monday to Saturday", bundle.source_text)

        match = bundle.first_match([r"monday\s+to\s+saturday"])
        self.assertIsNotNone(match)
        assert match is not None
        self.assertEqual(match.field_name, "clause_description")
        self.assertEqual(match.provenance, "clause_context")

    def test_first_aid_uses_documented_domain_default(self) -> None:
        parsed = parse_award_row(
            {
                "row_id": "first-aid",
                "award_code": "MA000120",
                "clause": "15.5",
                "allowance": "First aid allowance",
                "payment_frequency": "per week",
            }
        )

        condition = parsed.condition_json
        self.assertEqual(condition["trigger"], "first_aid")
        self.assertEqual(condition["basis"], "per_week")
        self.assertEqual(condition["days"]["source"], "domain_default")
        self.assertEqual(condition["day_types"], ["everyday"])
        self.assertIn("days", condition["defaulted_fields"])
        self.assertEqual(parsed.parse_status, "complete_defaulted")

    def test_on_call_without_day_text_routes_to_review(self) -> None:
        parsed = parse_award_row(
            {
                "row_id": "on-call-missing-days",
                "award_code": "MA000120",
                "clause": "15.4",
                "allowance": "On-call allowance",
                "payment_frequency": "per day",
            }
        )

        self.assertEqual(parsed.condition_json["trigger"], "on_call")
        self.assertIsNone(parsed.condition_json["days"])
        self.assertEqual(parsed.parse_status, "manual_review")
        self.assertEqual(parsed.review_status, "needs_review")
        self.assertIn("On-call day applicability is ambiguous", " ".join(parsed.parse_notes))

    def test_clause_context_can_supply_day_evidence(self) -> None:
        parsed = parse_award_row(
            {
                "row_id": "on-call-clause-context",
                "award_code": "MA000120",
                "clause": "15.4",
                "allowance": "On-call allowance",
                "clause_description": "This rate applies Monday to Saturday.",
                "payment_frequency": "per day",
            }
        )

        condition = parsed.condition_json
        self.assertEqual(
            condition["days"]["values"],
            ["monday", "tuesday", "wednesday", "thursday", "friday", "saturday"],
        )
        self.assertEqual(condition["days"]["source"], "clause_context")
        day_evidence = [item for item in condition["evidence"] if item["field"] == "days"]
        self.assertEqual(day_evidence[0]["source"], "clause_description")
        self.assertEqual(day_evidence[0]["provenance"], "clause_context")
        self.assertEqual(parsed.parse_status, "complete")

    def test_penalty_compounding_ordinary_rate_is_non_compounding(self) -> None:
        parsed = parse_award_row(
            {
                "row_id": "ordinary-rate-penalty",
                "award_code": "MA000120",
                "clause": "23.2",
                "penalty_description": "Overtime first 2 hours paid at 150% of the ordinary rate",
                "payment_frequency": "per hour",
            }
        )

        condition = parsed.condition_json
        self.assertEqual(condition["entity_type"], "penalty")
        self.assertEqual(condition["trigger"], "overtime")
        self.assertEqual(condition["time_condition"], "first_2_hours")
        self.assertEqual(condition["base_rate_reference"], "ordinary_rate")
        self.assertFalse(condition["is_compounding"])
        self.assertEqual(condition["thresholds"]["multiplier"], 1.5)
        self.assertEqual(parsed.parse_status, "complete")

    def test_legacy_wrapper_returns_flat_and_structured_fields(self) -> None:
        row = {
            "row_id": "wrapper-row",
            "award_code": "MA000120",
            "clause": "15.4",
            "allowance": "On-call allowance - Sunday or public holiday",
            "payment_frequency": "per day",
        }

        interpreted = interpret_award_row(row)
        enriched = enrich_row(row)

        self.assertEqual(interpreted["payment_trigger"], "on_call")
        self.assertEqual(interpreted["payment_basis"], "per_day")
        self.assertTrue(interpreted["conditionJson"]["public_holiday"])
        self.assertEqual(enriched["conditionJson"]["days"]["values"], ["sunday"])

    def test_condition_json_uses_canonical_contract_values(self) -> None:
        parsed = parse_award_row(
            {
                "row_id": "schema-row",
                "award_code": "MA000120",
                "clause": "23.5",
                "penalty_description": "Sunday penalty paid at 200% of the loaded rate plus applicable loadings",
                "payment_frequency": "per hour",
            }
        )
        condition = parsed.condition_json

        self.assertIn(condition["trigger"], PAYMENT_TRIGGERS)
        self.assertIn(condition["basis"], PAYMENT_BASIS)
        self.assertIn(condition["base_rate_reference"], BASE_RATE_REFERENCES)
        self.assertIn(condition["parse_status"], PARSE_STATUSES)
        for item in condition["evidence"]:
            self.assertIn(item["provenance"], PROVENANCE)


if __name__ == "__main__":
    unittest.main()
