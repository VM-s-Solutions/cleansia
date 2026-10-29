import importlib.util
from pathlib import Path
import unittest

MODULE = Path(__file__).resolve().parents[1] / "check-live-activity-environment.py"
spec = importlib.util.spec_from_file_location("activity_environment", MODULE)
guard = importlib.util.module_from_spec(spec)
spec.loader.exec_module(guard)


class LiveActivityEnvironmentTests(unittest.TestCase):
    def test_development_marker_matches_xcode_signature(self):
        guard.validate_environment("development", "development")

    def test_production_marker_matches_testflight_signature(self):
        guard.validate_environment("production", "production", expected="production")

    def test_marker_must_match_actual_signed_entitlement(self):
        for marker, signed in [("development", "production"), ("production", "development")]:
            with self.subTest(marker=marker), self.assertRaisesRegex(ValueError, "does not match"):
                guard.validate_environment(marker, signed)

    def test_missing_unknown_or_unexpanded_configuration_fails_closed(self):
        for marker in [None, "", "$(CLEANSIA_APNS_ENVIRONMENT)", "sandbox"]:
            with self.subTest(marker=marker), self.assertRaisesRegex(ValueError, "marker"):
                guard.validate_environment(marker, "production")
        with self.assertRaisesRegex(ValueError, "entitlement"):
            guard.validate_environment("production", None)

    def test_distribution_lane_rejects_development_even_when_consistent(self):
        with self.assertRaisesRegex(ValueError, "requires production"):
            guard.validate_environment("development", "development", expected="production")


if __name__ == "__main__":
    unittest.main()
