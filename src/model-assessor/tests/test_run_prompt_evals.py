import time
import unittest

from scripts.next_phase.run_prompt_evals import CompletionTimeoutError, WallClockTimeout


@unittest.skipUnless(__import__("os").name == "posix", "wall-clock timeout guard uses POSIX signals")
class WallClockTimeoutTests(unittest.TestCase):
    def test_wall_clock_timeout_raises(self):
        with self.assertRaises(CompletionTimeoutError):
            with WallClockTimeout(1):
                time.sleep(2)

    def test_wall_clock_timeout_allows_fast_work(self):
        with WallClockTimeout(1):
            time.sleep(0.01)


if __name__ == "__main__":
    unittest.main()