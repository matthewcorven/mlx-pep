import pathlib
import unittest

from scripts.next_phase.run_assessment import build_profile_execution_plan, summarize_benchmark_guard_findings


class RunAssessmentGuardTests(unittest.TestCase):
    def test_no_findings_for_completed_rows_without_skips(self):
        errors, warnings = summarize_benchmark_guard_findings(
            "short_coding_mtp_off",
            {
                "status": "completed",
                "results": [{"test_type": "single", "ttft_ms": 1.0}],
                "upload_state": {"skipped_features": []},
            },
        )

        self.assertEqual(errors, [])
        self.assertEqual(warnings, [])

    def test_findings_for_cancelled_and_empty_results(self):
        errors, warnings = summarize_benchmark_guard_findings(
            "deep_research_mtp_on",
            {
                "status": "cancelled",
                "results": [],
                "upload_state": {"skipped_features": []},
            },
        )

        self.assertIn("deep_research_mtp_on: benchmark status cancelled", errors)
        self.assertIn("deep_research_mtp_on: benchmark returned no comparable result rows", errors)
        self.assertEqual(warnings, [])

    def test_findings_for_skipped_features(self):
        errors, warnings = summarize_benchmark_guard_findings(
            "short_code_research_tools_mtp_on",
            {
                "status": "completed",
                "results": [{"test_type": "single", "ttft_ms": 1.0}],
                "upload_state": {
                    "skipped_features": ["vlm_mtp"],
                    "skipped_reason": "experimental_features",
                },
            },
        )

        self.assertEqual(errors, [])
        self.assertIn(
            "short_code_research_tools_mtp_on: upload skipped features vlm_mtp (experimental_features)",
            warnings,
        )


class RunAssessmentTopologyPlanTests(unittest.TestCase):
    def test_build_profile_execution_plan_requires_assistant_for_vlm_mtp(self):
        profiles = [
            {"id": "long_code_research_tools_mtp_on", "workload": "long_code_research_tools", "settings": {"vlm_mtp_enabled": True}}
        ]
        selected_model = {"settings": {"vlm_mtp_enabled": False, "vlm_mtp_draft_model": None}, "mtp_compatible": False}

        with self.assertRaisesRegex(ValueError, "requires --assistant-model-id"):
            build_profile_execution_plan(
                profiles=profiles,
                mtp_mode="profile",
                base_url_seed="http://127.0.0.1:8000",
                requested_assistant_model_id=None,
                selected_model=selected_model,
                topology=None,
            )

    def test_build_profile_execution_plan_uses_supplied_instance_topology(self):
        profiles = [
            {"id": "short_coding_mtp_on", "workload": "short_coding", "settings": {"vlm_mtp_enabled": True}},
            {
                "id": "short_code_research_tools_mtp_off",
                "workload": "short_code_research_tools",
                "settings": {"vlm_mtp_enabled": False},
            },
        ]
        topology = {
            "instance_mode": "multi",
            "instance_count": 2,
            "instances": [
                {
                    "instance_id": "instance-1",
                    "base_url": "http://127.0.0.1:8000",
                    "port": 8000,
                    "workload": "short_code_research_tools",
                    "profile_id": "short_code_research_tools_mtp_off",
                    "mtp_enabled": False,
                    "assistant_model_id": None,
                },
                {
                    "instance_id": "instance-2",
                    "base_url": "http://127.0.0.1:8001",
                    "port": 8001,
                    "workload": "short_coding",
                    "profile_id": "short_coding_mtp_on",
                    "mtp_enabled": True,
                    "assistant_model_id": "assistant-1",
                },
            ],
            "workload_to_instance": {
                "short_code_research_tools": "instance-1",
                "short_coding": "instance-2",
            },
            "instance_topology_summary": "Multi-instance topology required.",
        }

        resolved_topology, execution_plan = build_profile_execution_plan(
            profiles=profiles,
            mtp_mode="profile",
            base_url_seed="http://127.0.0.1:8000",
            requested_assistant_model_id="assistant-1",
            topology=topology,
        )

        self.assertEqual(resolved_topology, topology)
        self.assertEqual(execution_plan[0]["instance_id"], "instance-2")
        self.assertEqual(execution_plan[0]["base_url"], "http://127.0.0.1:8001")
        self.assertTrue(execution_plan[0]["mtp_enabled"])
        self.assertEqual(execution_plan[0]["assistant_model_id"], "assistant-1")
        self.assertEqual(execution_plan[1]["instance_id"], "instance-1")
        self.assertEqual(execution_plan[1]["base_url"], "http://127.0.0.1:8000")
        self.assertFalse(execution_plan[1]["mtp_enabled"])
        self.assertIsNone(execution_plan[1]["assistant_model_id"])

    def test_smoke_wrapper_stages_benchmark_evaluation_and_report(self):
        script_path = pathlib.Path(__file__).resolve().parents[1] / "scripts" / "run_smoke_suite.sh"
        text = script_path.read_text(encoding="utf-8")

        self.assertIn('profile_id = item.get("id")', text)
        self.assertIn('run_assessment.py', text)
        self.assertIn('run_prompt_evals.py', text)
        self.assertIn('generate_recommendation_report.py', text)
        self.assertLess(text.index('run_assessment.py'), text.index('run_prompt_evals.py'))
        self.assertLess(text.index('run_prompt_evals.py'), text.index('generate_recommendation_report.py'))


if __name__ == "__main__":
    unittest.main()
