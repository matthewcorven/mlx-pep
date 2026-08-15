import unittest

from scripts.next_phase.generate_recommendation_report import (
    build_quality_summary,
    build_recommendation_manifest,
    build_summary_markdown,
    validate_report_evidence,
)


class RecommendationManifestTopologyTests(unittest.TestCase):
    def _candidate(self, *, workload="short_coding", profile_id="short_coding_mtp_off", benchmark_available=True, evaluation_available=True):
        return {
            "candidate_id": f"{workload}-{profile_id}",
            "profile_id": profile_id,
            "workload": workload,
            "mtp_enabled": False,
            "assistant_model_id": None,
            "source_paths": [],
            "benchmark": {"available": benchmark_available},
            "evaluation": {"available": evaluation_available},
        }

    def test_validate_report_evidence_accepts_full_evidence(self):
        candidates = [
            self._candidate(workload="short_coding", profile_id="short_coding_mtp_off"),
            self._candidate(workload="short_coding", profile_id="short_coding_mtp_on", benchmark_available=True, evaluation_available=True),
        ]

        self.assertIsNone(validate_report_evidence(candidates))

    def test_validate_report_evidence_requires_benchmark_evidence(self):
        candidates = [self._candidate(benchmark_available=False)]

        with self.assertRaises(ValueError) as ctx:
            validate_report_evidence(candidates)

        self.assertIn("benchmark evidence", str(ctx.exception))

    def test_validate_report_evidence_requires_prompt_quality_evidence(self):
        candidates = [self._candidate(evaluation_available=False)]

        with self.assertRaises(ValueError) as ctx:
            validate_report_evidence(candidates)

        self.assertIn("prompt-quality", str(ctx.exception))

    def test_validate_report_evidence_mentions_required_workflow_sequence(self):
        candidates = [self._candidate(evaluation_available=False)]

        with self.assertRaises(ValueError) as ctx:
            validate_report_evidence(candidates)

        self.assertIn("run benchmark/probe collection first", str(ctx.exception))
        self.assertIn("then prompt-quality evaluation", str(ctx.exception))
        self.assertIn("then generate the recommendation report", str(ctx.exception))

    def test_build_quality_summary_reports_missing_prompt_quality_evidence(self):
        candidate = {"evaluation": {"available": False}}

        self.assertIn("No prompt-quality evaluation evidence available.", build_quality_summary(candidate))

    def test_build_summary_markdown_lists_missing_evidence(self):
        recommendation_manifest = {
            "created_at": "2026-06-11T00:00:00+00:00",
            "model_id": "demo-model",
            "assistant_model_id": None,
            "recommendation_id": "rec-123",
            "source_run_ids": [],
            "source_evaluation_run_ids": [],
            "missing_evidence": ["workload 'short_coding' is missing prompt-quality evaluation evidence"],
            "recommendations": [
                {
                    "workload": "short_coding",
                    "rank": 1,
                    "profile_id": "short_coding_mtp_off",
                    "mtp_recommended": False,
                    "assistant_model_id": None,
                    "confidence": "low",
                    "speed_summary": "No benchmark evidence available.",
                    "quality_summary": "No prompt-quality evaluation evidence available.",
                    "tradeoffs": [],
                    "caveats": [],
                    "source_paths": [],
                }
            ],
        }
        normalized_manifest = {
            "normalization_id": "norm-123",
            "candidates": [
                {
                    "candidate_id": "short-coding-mtp-off",
                    "profile_id": "short_coding_mtp_off",
                    "assistant_model_id": None,
                    "workload": "short_coding",
                    "mtp_enabled": False,
                    "assistant_summary": {"observation_count": 0},
                }
            ],
        }

        summary = build_summary_markdown(recommendation_manifest, normalized_manifest, [
            {
                "workload": "short_coding",
                "candidate_ids": ["short-coding-mtp-off"],
                "source_paths": [],
                "missing_evidence": ["workload 'short_coding' is missing prompt-quality evaluation evidence"],
            }
        ])

        self.assertIn("## Missing Evidence", summary)
        self.assertIn("prompt-quality evaluation evidence", summary)

    def test_build_recommendation_manifest_includes_instance_topology(self):
        candidates = [
            {
                "candidate_id": "short-coding-mtp-off",
                "profile_id": "short_coding_mtp_off",
                "workload": "short_coding",
                "mtp_enabled": False,
                "assistant_model_id": None,
                "source_paths": [],
                "benchmark": {"available": True},
                "evaluation": {"available": True},
            },
            {
                "candidate_id": "short-coding-mtp-on",
                "profile_id": "short_coding_mtp_on",
                "workload": "short_coding",
                "mtp_enabled": True,
                "assistant_model_id": None,
                "source_paths": [],
                "benchmark": {"available": True},
                "evaluation": {"available": True},
            },
        ]

        manifest = build_recommendation_manifest(
            recommendation_id="rec-123",
            created_at="2026-06-11T00:00:00+00:00",
            model_id="demo-model",
            assistant_model_id=None,
            candidates=candidates,
            comparison_groups=[
                {
                    "workload": "short_coding",
                    "primary_evidence_type": "benchmark",
                    "secondary_evidence_type": "evaluation",
                    "candidate_ids": ["short-coding-mtp-off", "short-coding-mtp-on"],
                    "source_paths": [],
                    "missing_evidence": [],
                }
            ],
            source_run_ids=[],
            source_evaluation_run_ids=[],
            source_paths=[],
            missing_evidence=[],
        )

        self.assertIn("instance_topology", manifest)
        self.assertEqual(manifest["instance_topology"]["instance_mode"], "multi")
        self.assertGreaterEqual(manifest["instance_topology"]["instance_count"], 2)


if __name__ == "__main__":
    unittest.main()
