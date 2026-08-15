import unittest

from scripts.next_phase.runner_lib import (
    OMLXHarness,
    build_instance_topology,
    ensure_vlm_mtp_assistant_configuration,
    match_topology_instance,
)


class FakeHarness(OMLXHarness):
    def __init__(self):
        self.calls = []

    def _json_request(self, method, path, body=None, timeout=120):
        self.calls.append((method, path, body, timeout))
        if path == "/admin/api/bench/active":
            return {"running": True, "bench_id": "bench-123"}
        if path == "/admin/api/bench/bench-123/cancel":
            return {"status": "cancelled", "bench_id": "bench-123"}
        raise AssertionError(f"Unexpected path: {path}")


class OMLXHarnessBenchmarkLockTests(unittest.TestCase):
    def test_cancel_active_benchmark_if_running(self):
        client = FakeHarness()

        result = client.cancel_active_benchmark_if_running(wait_for_clear=False)

        self.assertEqual(result, {"status": "cancelled", "bench_id": "bench-123"})
        self.assertEqual(client.calls[0], ("GET", "/admin/api/bench/active", None, 120))
        self.assertEqual(client.calls[1], ("POST", "/admin/api/bench/bench-123/cancel", None, 120))

    def test_cancel_active_benchmark_waits_until_clear(self):
        class ClearedHarness(FakeHarness):
            def __init__(self):
                super().__init__()
                self.active_states = [
                    {"running": True, "bench_id": "bench-123"},
                    {"running": True, "bench_id": "bench-123"},
                    {"running": False, "bench_id": None},
                ]

            def _json_request(self, method, path, body=None, timeout=120):
                self.calls.append((method, path, body, timeout))
                if path == "/admin/api/bench/active":
                    return self.active_states.pop(0)
                if path == "/admin/api/bench/bench-123/cancel":
                    return {"status": "cancelled", "bench_id": "bench-123"}
                raise AssertionError(f"Unexpected path: {path}")

        client = ClearedHarness()

        result = client.cancel_active_benchmark_if_running(wait_for_clear=True, timeout=2, poll_interval=0)

        self.assertEqual(result, {"status": "cancelled", "bench_id": "bench-123"})
        self.assertGreaterEqual(len(client.calls), 3)

    def test_cancel_active_benchmark_skips_when_not_running(self):
        class NotRunningHarness(FakeHarness):
            def _json_request(self, method, path, body=None, timeout=120):
                self.calls.append((method, path, body, timeout))
                if path == "/admin/api/bench/active":
                    return {"running": False, "bench_id": None}
                raise AssertionError(f"Unexpected path: {path}")

        client = NotRunningHarness()

        result = client.cancel_active_benchmark_if_running(wait_for_clear=False)

        self.assertEqual(result, {"running": False, "bench_id": None})
        self.assertEqual(len(client.calls), 1)


class RunnerTopologyHelpersTests(unittest.TestCase):
    def test_build_instance_topology_uses_seed_base_url(self):
        topology = build_instance_topology(
            [
                {
                    "workload": "short_coding",
                    "rank": 1,
                    "profile_id": "short_coding_mtp_on",
                    "mtp_recommended": True,
                    "assistant_model_id": "assistant-1",
                },
                {
                    "workload": "short_coding",
                    "rank": 2,
                    "profile_id": "short_coding_mtp_off",
                    "mtp_recommended": False,
                    "assistant_model_id": None,
                },
            ],
            base_url_seed="http://127.0.0.1:9100",
        )

        self.assertEqual(topology["instances"][0]["base_url"], "http://127.0.0.1:9100")
        self.assertEqual(topology["instances"][1]["base_url"], "http://127.0.0.1:9101")

    def test_match_topology_instance_prefers_exact_assistant_match(self):
        topology = {
            "instance_mode": "multi",
            "instance_count": 2,
            "instances": [
                {
                    "instance_id": "instance-1",
                    "workload": "short_coding",
                    "profile_id": "short_coding_mtp_on",
                    "mtp_enabled": True,
                    "assistant_model_id": None,
                },
                {
                    "instance_id": "instance-2",
                    "workload": "short_coding",
                    "profile_id": "short_coding_mtp_on",
                    "mtp_enabled": True,
                    "assistant_model_id": "assistant-1",
                },
            ],
            "workload_to_instance": {"short_coding": "instance-1"},
        }

        matched = match_topology_instance(
            topology,
            workload="short_coding",
            profile_id="short_coding_mtp_on",
            mtp_enabled=True,
            assistant_model_id="assistant-1",
        )

        self.assertIsNotNone(matched)
        self.assertEqual(matched["instance_id"], "instance-2")

    def test_ensure_vlm_mtp_assistant_configuration_requires_explicit_assistant(self):
        with self.assertRaisesRegex(ValueError, "requires --assistant-model-id"):
            ensure_vlm_mtp_assistant_configuration(
                profile_id="long_code_research_tools_mtp_on",
                settings_overrides={"vlm_mtp_enabled": True},
                current_settings={"vlm_mtp_enabled": False, "vlm_mtp_draft_model": None},
                profile_field_names=["vlm_mtp_enabled", "vlm_mtp_draft_model"],
                assistant_model_id=None,
            )

    def test_ensure_vlm_mtp_assistant_configuration_accepts_explicit_assistant(self):
        assistant_field = ensure_vlm_mtp_assistant_configuration(
            profile_id="long_code_research_tools_mtp_on",
            settings_overrides={"vlm_mtp_enabled": True},
            current_settings={"vlm_mtp_enabled": False, "vlm_mtp_draft_model": None},
            profile_field_names=["vlm_mtp_enabled", "vlm_mtp_draft_model"],
            assistant_model_id="gemma-4-12B-it-assistant-bf16",
        )

        self.assertEqual(assistant_field, "vlm_mtp_draft_model")


if __name__ == "__main__":
    unittest.main()
