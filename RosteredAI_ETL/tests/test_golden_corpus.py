from __future__ import annotations

import json
import sys
import unittest
from pathlib import Path
from typing import Any


PROJECT_ROOT = Path(__file__).resolve().parents[1]
SRC_ROOT = PROJECT_ROOT / "src"
sys.path.insert(0, str(SRC_ROOT))

from transform.interpreter.parser import parse_award_row


FIXTURE_PATH = PROJECT_ROOT / "tests" / "fixtures" / "interpreter" / "ma000120_golden_rows.json"


class GoldenCorpusTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.cases = json.loads(FIXTURE_PATH.read_text(encoding="utf-8"))

    def test_golden_corpus_has_required_acceptance_coverage(self) -> None:
        case_ids = {case["id"] for case in self.cases}
        for expected_id in {
            "INT-GOLD-001",
            "INT-GOLD-002",
            "INT-GOLD-003",
            "INT-GOLD-004",
            "INT-GOLD-005",
            "INT-GOLD-006",
            "INT-GOLD-007",
            "INT-GOLD-008",
            "INT-GOLD-009",
            "INT-GOLD-010",
            "INT-EDGE-011",
            "INT-EDGE-012",
            "INT-PEN-001",
            "INT-PEN-002",
            "INT-PEN-003",
        }:
            self.assertIn(expected_id, case_ids)

    def test_golden_corpus_expected_outputs(self) -> None:
        for case in self.cases:
            with self.subTest(case=case["id"]):
                parsed = parse_award_row(case["input"])
                condition = parsed.condition_json
                expected = case["expected"]

                self._assert_expected_subset(parsed.parse_status, condition, expected)

                self.assertGreaterEqual(parsed.parsed_confidence, 0.0)
                self.assertLessEqual(parsed.parsed_confidence, 1.0)
                self.assertTrue(parsed.source_key_hash)
                self.assertTrue(condition["evidence"])

                if expected.get("review_note_contains"):
                    self.assertIn(expected["review_note_contains"], " ".join(parsed.parse_notes))

    def _assert_expected_subset(self, parse_status: str, condition: dict[str, Any], expected: dict[str, Any]) -> None:
        simple_fields = (
            "entity_type",
            "trigger",
            "basis",
            "public_holiday",
            "period_rounding",
            "shift_type",
            "stacking_policy",
            "base_rate_reference",
            "is_compounding",
            "time_condition",
        )
        for field in simple_fields:
            if field in expected:
                self.assertEqual(condition[field], expected[field], field)

        if "parse_status" in expected:
            self.assertEqual(parse_status, expected["parse_status"])
            self.assertEqual(condition["parse_status"], expected["parse_status"])

        if "days" in expected:
            self.assertIsNotNone(condition["days"])
            self.assertEqual(condition["days"]["values"], expected["days"])

        if "day_source" in expected:
            self.assertIsNotNone(condition["days"])
            self.assertEqual(condition["days"]["source"], expected["day_source"])

        if "day_types" in expected:
            self.assertEqual(condition["day_types"], expected["day_types"])

        if "employee_threshold" in expected:
            self.assertEqual(condition["thresholds"]["employees"], expected["employee_threshold"])

        if "temperature_celsius" in expected:
            self.assertEqual(condition["thresholds"]["temperature_celsius"], expected["temperature_celsius"])

        if "multiplier" in expected:
            self.assertEqual(condition["thresholds"]["multiplier"], expected["multiplier"])

        if "dependency_trigger" in expected:
            dependency_triggers = {dependency["trigger"] for dependency in condition["dependencies"]}
            self.assertIn(expected["dependency_trigger"], dependency_triggers)

        if "days_evidence_source" in expected:
            day_evidence = [item for item in condition["evidence"] if item["field"] == "days"]
            self.assertTrue(day_evidence)
            self.assertEqual(day_evidence[0]["source"], expected["days_evidence_source"])


if __name__ == "__main__":
    unittest.main()
