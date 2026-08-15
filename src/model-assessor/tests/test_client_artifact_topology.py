import unittest

from scripts.next_phase.generate_client_config_artifacts import (
    build_harness_reference_rows,
    build_instance_topology,
)


class ClientArtifactTopologyTests(unittest.TestCase):
    def test_build_instance_topology_marks_multi_instance_when_settings_diverge(self):
        recommendations = [
            {
                "workload": "short_coding",
                "rank": 1,
                "profile_id": "short_coding_mtp_off",
                "mtp_recommended": False,
                "assistant_model_id": None,
            },
            {
                "workload": "short_coding",
                "rank": 2,
                "profile_id": "short_coding_mtp_on",
                "mtp_recommended": True,
                "assistant_model_id": None,
            },
        ]

        topology = build_instance_topology(recommendations)

        self.assertEqual(topology["instance_mode"], "multi")
        self.assertGreaterEqual(topology["instance_count"], 2)
        self.assertIn("multi-instance", topology["instance_topology_summary"].lower())
        self.assertEqual(topology["workload_to_instance"]["short_coding"], "instance-1")

    def test_build_harness_reference_rows_emit_per_workload_harness_rows(self):
        recommendation_manifest = {
            "recommendation_id": "rec-123",
            "created_at": "2026-06-12T00:00:00+00:00",
            "model_id": "demo-model",
            "instance_topology": {
                "instance_mode": "multi",
                "instance_count": 2,
                "instances": [
                    {
                        "instance_id": "instance-1",
                        "port": 8000,
                        "workloads": ["short_coding"],
                    }
                ],
                "workload_to_instance": {"short_coding": "instance-1"},
            },
            "recommendations": [],
        }
        workload_entries = [
            {
                "workload": "short_coding",
                "ranked_recommendations": [
                    {
                        "workload": "short_coding",
                        "rank": 1,
                        "profile_id": "short_coding_mtp_off",
                        "mtp_enabled": False,
                        "assistant_model_id": None,
                        "recommended_server_settings": {
                            "max_context_window": 8192,
                            "max_tokens": 1024,
                            "mtp_enabled": False,
                        },
                    }
                ],
            }
        ]

        rows = build_harness_reference_rows(recommendation_manifest, workload_entries)

        self.assertEqual(len(rows), 5)
        vscode_insiders = next(row for row in rows if row["harness_id"] == "vscode_insiders")
        self.assertEqual(vscode_insiders["workload"], "short_coding")
        self.assertEqual(vscode_insiders["instance_id"], "instance-1")
        self.assertEqual(vscode_insiders["inference_api_base_url"], "http://127.0.0.1:8000/v1")
        self.assertIn("models[].id", [item["term"] for item in vscode_insiders["recommended_values"]])
        self.assertFalse(vscode_insiders["recommended_server_settings"]["mtp_enabled"])


if __name__ == "__main__":
    unittest.main()
