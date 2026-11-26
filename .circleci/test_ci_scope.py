import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

import ci_scope


class DiffTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.git("init", "-q")
        self.git("config", "user.name", "CI test")
        self.git("config", "user.email", "ci@example.invalid")
        self.write("README.md", "before")
        self.write("PresetLauncherWinUI3/App.xaml.cs", "code")
        self.git("add", ".")
        self.git("commit", "-qm", "baseline")

    def git(self, *args):
        return subprocess.check_output(["git", "-C", str(self.root), *args])

    def write(self, path, value):
        target = self.root / path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(value, encoding="utf-8")

    def diff(self):
        self.git("add", "-A")
        return self.git("diff", "--cached", "--raw", "--no-renames", "-z", "HEAD", "--")

    def test_docs_website_worker_and_templates_are_eligible(self):
        self.write("README.md", "after")
        self.write("docs/USAGE.fr.md", "doc")
        self.write("website/assets/screenshots/demo.png", "image")
        self.write("website/fr/index.html", "html")
        self.write(".github/ISSUE_TEMPLATE/bug.yml", "template")
        self.write("tools/auto-labeler/worker.mjs", "worker")
        self.assertTrue(ci_scope.non_application_diff(self.diff()))

    def test_mixed_change_cannot_skip_windows(self):
        self.write("README.md", "after")
        self.write("PresetLauncherWinUI3/App.xaml.cs", "changed code")
        self.assertFalse(ci_scope.non_application_diff(self.diff()))

    def test_rename_out_of_application_cannot_skip(self):
        self.git("mv", "PresetLauncherWinUI3/App.xaml.cs", "docs/moved.md")
        self.assertFalse(ci_scope.non_application_diff(self.diff()))

    def test_ci_and_unknown_paths_require_windows(self):
        for path in (".circleci/config.yml", ".github/workflows/build.yml", "global.json",
                     "Installer/PresetLauncher.iss", "docs/build.ps1", "unknown.txt"):
            with self.subTest(path=path):
                self.assertFalse(ci_scope.non_application_path(path))

    def test_empty_malformed_and_nonregular_entries_require_windows(self):
        for raw in (b"", b"M\0README.md\0", b":100644 120000 0 0 T\0README.md\0",
                    b":000000 120000 0 0 A\0docs/link.md\0",
                    b":000000 160000 0 0 A\0docs/module.md\0"):
            with self.subTest(raw=raw):
                self.assertFalse(ci_scope.non_application_diff(raw))


class SelectionTests(unittest.TestCase):
    def test_full_lane_does_not_plan_twice(self):
        with patch.object(ci_scope, "eligible", return_value=False), \
                patch.object(ci_scope.release, "plan") as plan:
            self.assertEqual(ci_scope.select_executor(), "windows-x64")
            plan.assert_not_called()

    def test_unknown_scope_falls_back(self):
        with patch.object(ci_scope, "eligible", side_effect=ValueError("uncertain")):
            self.assertEqual(ci_scope.select_executor(), "windows-x64")

    def test_pending_release_never_selects_linux(self):
        with patch.object(ci_scope, "eligible", return_value=True), \
                patch.object(ci_scope.release, "plan"), \
                patch.object(ci_scope, "check_plan", side_effect=ValueError("release")):
            self.assertEqual(ci_scope.select_executor(), "windows-x64")

    def test_proven_non_application_selects_linux(self):
        with patch.object(ci_scope, "eligible", return_value=True), \
                patch.object(ci_scope.release, "plan"), \
                patch.object(ci_scope, "check_plan"):
            self.assertEqual(ci_scope.select_executor(), "linux-small")

    def test_plan_identity_and_release_are_required(self):
        with tempfile.TemporaryDirectory() as temp:
            plan = Path(temp) / "plan.json"
            with patch.object(ci_scope.release, "PLAN", plan), \
                    patch.object(ci_scope, "identity", return_value=("main", "head")):
                for data in ({"sha": "head", "release": True},
                             {"sha": "other", "release": False},
                             {"sha": "head", "release": False, "validation_only": True}):
                    plan.write_text(json.dumps(data), encoding="utf-8")
                    with self.subTest(data=data), self.assertRaises(ValueError):
                        ci_scope.check_plan()
                plan.write_text(json.dumps({"sha": "head", "release": False}), encoding="utf-8")
                ci_scope.check_plan()

    def test_late_scope_change_blocks_light_job(self):
        with patch.object(ci_scope, "check_plan"), \
                patch.object(ci_scope, "eligible", return_value=False):
            with self.assertRaises(ValueError):
                ci_scope.verify()


if __name__ == "__main__":
    unittest.main()
