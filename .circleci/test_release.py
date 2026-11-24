import unittest
from release import make_plan, version


class ReleasePlanTests(unittest.TestCase):
    def setUp(self):
        self.previous = {"tag_name": "1.2.6", "body": "Legacy release"}

    def plan(self, *messages):
        return make_plan(self.previous, messages, "abc")

    def test_patch_minor_major_and_no_release(self):
        for message, expected, release in [
            ("fix: repair launcher", "1.2.7", True),
            ("perf: improve startup", "1.2.7", True),
            ("feat(ui): add a view", "1.3.0", True),
            ("feat!: change format", "2.0.0", True),
            ("fix: change format\n\nBREAKING CHANGE: incompatible", "2.0.0", True),
            ("docs: update README", "1.2.6", False),
            ("ci: update workflow", "1.2.6", False),
        ]:
            with self.subTest(message=message):
                result = self.plan(message)
                self.assertEqual(result["version"], expected)
                self.assertEqual(result["release"], release)

    def test_highest_bump_wins(self):
        self.assertEqual(self.plan("fix: a", "feat: b", "fix: c")["tag"], "v1.3.0")

    def test_required_policy_independent_of_version(self):
        self.assertEqual(self.plan("fix: security\n\nUpdate-Required: true")["minimum_supported_version"], "1.2.7")
        self.assertEqual(self.plan("feat!: redesign")["minimum_supported_version"], "0.0.0")

    def test_floor_survives_later_optional_release(self):
        self.previous = {"tag_name": "v1.3.0", "body": "<!-- presetlauncher:min-supported=1.2.7 -->"}
        self.assertEqual(self.plan("fix: typo")["minimum_supported_version"], "1.2.7")

    def test_invalid_policy_and_versions_fail(self):
        for value in ("v1.2", "1.02.3", "1.2.3-beta", "1.2.65535"):
            with self.assertRaises(ValueError):
                version(value)
        with self.assertRaises(ValueError):
            self.plan("docs: typo\n\nUpdate-Required: true")
        self.previous = {"tag_name": "v1.3.0", "body": "missing policy"}
        with self.assertRaises(ValueError):
            self.plan("fix: a")


if __name__ == "__main__":
    unittest.main()
