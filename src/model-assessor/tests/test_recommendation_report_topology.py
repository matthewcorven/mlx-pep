import unittest

from scripts.next_phase.generate_recommendation_report import build_recommendation_manifest


class RecommendationManifestTopologyTests(unittest.TestCase):
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
