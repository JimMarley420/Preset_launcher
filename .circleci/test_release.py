import hashlib
import json
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
from urllib.error import HTTPError

import release
from release import make_plan, version
from policy import pr_decision


def decision(message, policy="optional", number=3):
    title, _, body = message.partition("\n")
    from policy import bump
    labels = [{"name": f"update: {policy}"}] if bump(title, body) else []
    return pr_decision({"number": number, "title": title, "body": body, "labels": labels})


class ReleasePlanTests(unittest.TestCase):
    def setUp(self):
        self.previous = {"tag_name": "1.2.6", "body": "Legacy release"}

    def plan(self, *messages):
        return make_plan(self.previous, [decision(message, number=i) for i, message in enumerate(messages)], "abc")

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
        self.assertEqual(make_plan(self.previous, [decision("fix: security", "required")], "abc")["minimum_supported_version"], "1.2.7")
        self.assertEqual(make_plan(self.previous, [decision("feat: a", "recommended"), decision("fix: b", "optional")], "abc")["policy"], "recommended")
        self.assertEqual(self.plan("feat!: redesign")["minimum_supported_version"], "0.0.0")

    def test_floor_survives_later_optional_release(self):
        self.previous = {"tag_name": "v1.3.0", "body": "<!-- presetlauncher:min-supported=1.2.7 -->"}
        self.assertEqual(self.plan("fix: typo")["minimum_supported_version"], "1.2.7")

    def test_invalid_policy_and_versions_fail(self):
        for value in ("v1.2", "1.02.3", "1.2.3-beta", "1.2.65535"):
            with self.assertRaises(ValueError):
                version(value)
        self.previous = {"tag_name": "v1.3.0", "body": "missing policy"}
        with self.assertRaises(ValueError):
            self.plan("fix: a")


class InstallerLaneTests(unittest.TestCase):
    def test_regular_changes_use_fast_lane(self):
        self.assertFalse(release.needs_installer([
            "README.md", "tests/PresetLauncher.Tests/ServiceTests.cs",
            "PresetLauncherWinUI3/Services/LauncherService.cs",
        ]))

    def test_installer_updater_and_publish_paths_require_full_lane(self):
        for path in (
            ".circleci/config.yml", "Installer/PresetLauncher.iss", "global.json",
            "PresetLauncherWinUI3/Services/UpdateService.cs", "PresetLauncherWinUI3/App.xaml.cs",
            "PresetLauncherWinUI3/Models/AppSettings.cs", "Directory.Build.props",
            "PresetLauncherWinUI3/Properties/PublishProfiles/win-x64.pubxml", "NuGet.Config",
        ):
            with self.subTest(path=path):
                self.assertTrue(release.needs_installer([path]))

    def test_main_always_uses_full_lane(self):
        with patch.object(release, "git") as git:
            self.assertTrue(release.validate_installer("main", None, "head"))
            git.assert_not_called()

    def test_stacked_pr_uses_its_actual_base(self):
        metadata = {"base": {"ref": "feat/github-release-updater"}}
        with patch.object(release, "git", side_effect=["", "merge-base", "README.md\0"]) as git:
            self.assertFalse(release.validate_installer("feat/dotnet-10-migration", metadata, "head"))
        self.assertEqual(git.call_args_list[0].args[-1], "refs/heads/feat/github-release-updater")
        self.assertEqual(git.call_args_list[-1].args, (
            "diff", "--name-only", "--no-renames", "-z", "merge-base", "head", "--",
        ))


class PublishTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        root = Path(temporary.name)
        self.output = root / "verified"
        self.output.mkdir()
        self.installer = self.output / "PresetLauncher_Setup_1.2.7.exe"
        self.installer.write_bytes(b"test installer")
        digest = hashlib.sha256(self.installer.read_bytes()).hexdigest()
        self.sums = self.output / "SHA256SUMS.txt"
        self.sums.write_text(f"{digest}  {self.installer.name}\n", encoding="ascii")
        self.previous = {"tag_name": "1.2.6", "body": ""}
        self.sha = "a" * 40
        self.plan = root / "plan.json"
        self.plan.write_text(json.dumps(make_plan(self.previous, [decision("fix: repair")], self.sha)), encoding="utf-8")
        self.calls = []
        for patcher in (
            patch.object(release, "PLAN", self.plan),
            patch.object(release, "OUTPUT", self.output),
            patch.object(release, "git", return_value=self.sha),
            patch.object(release, "collect_decisions", return_value=[decision("fix: repair")]),
            patch.dict(os.environ, {"CIRCLE_BRANCH": "main", "GH_RELEASE_TOKEN": "test-token"}),
        ):
            patcher.start()
            self.addCleanup(patcher.stop)

    def response(self, url, method="GET", payload=None, token=None, file=None):
        self.calls.append((method, url, payload))
        if url.endswith("/releases/latest"):
            return self.previous
        if url.endswith("/git/ref/heads/main"):
            return {"object": {"sha": self.sha}}
        if "/git/ref/tags/" in url:
            raise HTTPError(url, 404, "Not found", None, None)
        if "/releases?per_page=" in url:
            return []
        if url.endswith("/releases/generate-notes"):
            return {"body": "Changes"}
        if url.endswith("/releases") and method == "POST":
            self.assertTrue(payload["draft"])
            self.assertEqual(payload["target_commitish"], self.sha)
            return {"id": 42}
        if "/assets?" in url:
            digest = hashlib.sha256(file.read_bytes()).hexdigest()
            return {"name": file.name, "state": "uploaded", "digest": f"sha256:{digest}"}
        if url.endswith("/releases/42") and method == "PATCH":
            return {"draft": False, "tag_name": "v1.2.7", "html_url": "https://github.com/example"}
        self.fail(f"Unexpected API request: {method} {url}")

    def test_no_release_never_contacts_api(self):
        self.plan.write_text(json.dumps(make_plan(self.previous, [decision("docs: typo")], self.sha)), encoding="utf-8")
        with patch.object(release, "request") as request:
            release.publish()
            request.assert_not_called()

    def test_branch_cannot_publish(self):
        with patch.dict(os.environ, {"CIRCLE_BRANCH": "feature"}), patch.object(release, "request") as request:
            with self.assertRaises(ValueError):
                release.publish()
            request.assert_not_called()

    def test_stale_plan_fails_before_mutation(self):
        self.previous = {"tag_name": "v1.2.7", "body": "<!-- presetlauncher:min-supported=0.0.0 -->"}
        with patch.object(release, "request", side_effect=self.response):
            with self.assertRaises(ValueError):
                release.publish()
        self.assertTrue(all(method == "GET" for method, _, _ in self.calls))

    def test_changed_labels_block_publication(self):
        with patch.object(release, "collect_decisions", return_value=[decision("fix: repair", "required")]), patch.object(release, "request", side_effect=self.response):
            with self.assertRaises(ValueError):
                release.publish()
        self.assertTrue(all(method == "GET" for method, _, _ in self.calls))

    def test_tampered_installer_fails_before_mutation(self):
        self.installer.write_bytes(b"modified")
        with patch.object(release, "request", side_effect=self.response):
            with self.assertRaises(ValueError):
                release.publish()
        self.assertTrue(all(method == "GET" for method, _, _ in self.calls))

    def test_upload_digest_mismatch_leaves_draft_unpublished(self):
        def wrong_digest(url, method="GET", payload=None, token=None, file=None):
            result = self.response(url, method, payload, token, file)
            if "/assets?" in url:
                result["digest"] = "sha256:" + "0" * 64
            return result
        with patch.object(release, "request", side_effect=wrong_digest):
            with self.assertRaises(ValueError):
                release.publish()
        self.assertFalse(any(method == "PATCH" for method, _, _ in self.calls))

    def test_publish_occurs_after_both_uploads(self):
        with patch.object(release, "request", side_effect=self.response):
            release.publish()
        self.assertEqual(sum("/assets?" in url for _, url, _ in self.calls), 2)
        self.assertEqual(self.calls[-1][0], "PATCH")
        self.assertFalse(self.calls[-1][2]["draft"])


if __name__ == "__main__":
    unittest.main()
