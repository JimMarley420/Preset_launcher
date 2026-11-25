import hashlib
import io
import errno
import ssl
from email.message import Message
from datetime import datetime, timezone, timedelta
from email.utils import format_datetime
import json
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
from urllib.error import HTTPError, URLError

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
            "PresetLauncherWinUI3/favicon.ico",
            "PresetLauncherWinUI3/MainWindow.xaml.cs",
            "PresetLauncherWinUI3/Views/Pages/SettingsPage.xaml.cs",
            "PresetLauncherWinUI3/Services/LocalizationService.cs",
            "presetlauncherwinui3/views/pages/SETTINGSPAGE.XAML",
            "iNsTaLlEr/presetlauncher.iss",
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


    def test_renamed_sensitive_file_still_requires_full_lane(self):
        metadata = {"base": {"ref": "main"}}
        paths = "docs/archive.txt\0PresetLauncherWinUI3/Services/LocalizationService.cs\0"
        with patch.object(release, "git", side_effect=["", "merge-base", paths]) as git:
            self.assertTrue(release.validate_installer("feature", metadata, "head"))
        self.assertIn("--no-renames", git.call_args.args)


class GitHubRequestTests(unittest.TestCase):
    url = "https://api.github.com/repos/example/project/pulls/10"

    def error(self, code, **headers):
        message = Message()
        for name, value in headers.items():
            message[name] = value
        return HTTPError(self.url, code, "test error", message, io.BytesIO(b"{}"))

    def test_transient_http_errors_recover(self):
        for code in (429, 500, 502, 503, 504):
            with self.subTest(code=code), patch.object(release, "urlopen", side_effect=[
                    self.error(code), io.BytesIO(b'{"ok": true}')]) as call, \
                    patch.object(release.time, "sleep") as sleep, \
                    patch.object(release.random, "uniform", return_value=1):
                self.assertEqual(release.request(self.url), {"ok": True})
                self.assertEqual(call.call_count, 2)
                sleep.assert_called_once_with(1)
                self.assertEqual(call.call_args.kwargs["timeout"], 30)

    def test_network_errors_recover(self):
        for error in (TimeoutError(), ConnectionResetError(), URLError(TimeoutError()),
                      URLError(OSError(errno.ECONNREFUSED, "refused"))):
            with self.subTest(error=error), patch.object(release, "urlopen", side_effect=[
                    error, io.BytesIO(b'{"ok": true}')]) as call, patch.object(release.time, "sleep"):
                self.assertEqual(release.request(self.url), {"ok": True})
                self.assertEqual(call.call_count, 2)

    def test_retry_budget_exhausted(self):
        errors = [self.error(504) for _ in range(4)]
        with patch.object(release, "urlopen", side_effect=errors) as call, \
                patch.object(release.time, "sleep") as sleep, \
                patch.object(release.random, "uniform", side_effect=[1, 2, 4]) as jitter:
            with self.assertRaises(HTTPError) as raised:
                release.request(self.url)
            self.assertIs(raised.exception, errors[-1])
            self.assertEqual(call.call_count, 4)
            self.assertEqual([item.args for item in sleep.call_args_list], [(1,), (2,), (4,)])
            self.assertEqual([item.args for item in jitter.call_args_list], [(0, 2), (0, 4), (0, 8)])

    def test_permanent_errors_are_not_retried(self):
        for error in (self.error(401), self.error(403), self.error(404), self.error(422),
                      URLError(ssl.SSLCertVerificationError("bad certificate"))):
            with self.subTest(error=error), patch.object(release, "urlopen", side_effect=error) as call, \
                    patch.object(release.time, "sleep") as sleep:
                with self.assertRaises(type(error)):
                    release.request(self.url)
                call.assert_called_once()
                sleep.assert_not_called()

    def test_retry_after_and_rate_limit(self):
        for error, expected in ((self.error(429, **{"Retry-After": "7"}), 7),
                                (self.error(403, **{"Retry-After": "9"}), 9)):
            with self.subTest(expected=expected), patch.object(release, "urlopen", side_effect=[
                    error, io.BytesIO(b"{}")]), patch.object(release.time, "sleep") as sleep, \
                    patch.object(release.random, "uniform", return_value=1):
                self.assertEqual(release.request(self.url), {})
                sleep.assert_called_once_with(expected)

    def test_http_date_retry_after(self):
        future = datetime.now(timezone.utc) + timedelta(seconds=15)
        error = self.error(503, **{"Retry-After": format_datetime(future, usegmt=True)})
        with patch.object(release.random, "uniform", return_value=0):
            self.assertGreater(release.retry_delay(error, 0), 12)
            self.assertLessEqual(release.retry_delay(error, 0), 15)

    def test_long_or_invalid_rate_limit_wait_fails_without_early_retry(self):
        for error in (self.error(429, **{"Retry-After": "3600"}),
                      self.error(429, **{"Retry-After": "invalid"}),
                      self.error(403, **{"X-RateLimit-Remaining": "0"})):
            with self.subTest(error=error), patch.object(release, "urlopen", side_effect=error) as call, \
                    patch.object(release.time, "sleep") as sleep:
                with self.assertRaises(HTTPError):
                    release.request(self.url)
                call.assert_called_once()
                sleep.assert_not_called()

    def test_primary_rate_limit_reset_is_respected(self):
        reset = int(datetime.now(timezone.utc).timestamp()) + 15
        error = self.error(403, **{"X-RateLimit-Remaining": "0", "X-RateLimit-Reset": str(reset)})
        with patch.object(release.random, "uniform", return_value=0):
            self.assertGreater(release.retry_delay(error, 0), 12)
            self.assertLessEqual(release.retry_delay(error, 0), 15)

    def test_mutations_and_uploads_are_never_retried(self):
        with tempfile.TemporaryDirectory() as directory:
            asset = Path(directory) / "asset.exe"
            asset.write_bytes(b"test")
            for method, options in (("POST", {"payload": {"draft": True}}),
                                    ("PATCH", {"payload": {"draft": False}}),
                                    ("POST", {"file": asset})):
                with self.subTest(method=method, options=options), \
                        patch.object(release, "urlopen", side_effect=self.error(504)) as call, \
                        patch.object(release.time, "sleep") as sleep:
                    with self.assertRaises(HTTPError):
                        release.request(self.url, method, **options)
                    call.assert_called_once()
                    sleep.assert_not_called()

    def test_invalid_json_is_not_retried(self):
        with patch.object(release, "urlopen", return_value=io.BytesIO(b"invalid")) as call, \
                patch.object(release.time, "sleep") as sleep:
            with self.assertRaises(json.JSONDecodeError):
                release.request(self.url)
            call.assert_called_once()
            sleep.assert_not_called()


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
        self.signer = patch.object(release, "sign", return_value="test-signature")
        self.signer.start()
        self.addCleanup(self.signer.stop)
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
            self.assertIn("<!-- presetlauncher:signature-v1=test-signature -->", payload["body"])
            self.assertEqual(payload["target_commitish"], self.sha)
            return {"id": 42}
        if "/assets?" in url:
            digest = hashlib.sha256(file.read_bytes()).hexdigest()
            return {"name": file.name, "state": "uploaded", "digest": f"sha256:{digest}"}
        if url.endswith("/releases/42") and method == "PATCH":
            return {"draft": False, "tag_name": "v1.2.7", "html_url": "https://github.com/example"}
        self.fail(f"Unexpected API request: {method} {url}")

    def test_unsigned_previous_after_migration_is_rejected(self):
        with self.assertRaises(ValueError):
            release.verify_previous({"tag_name": "v1.3.1", "body": "<!-- presetlauncher:min-supported=1.3.0 -->"})

    def test_signed_previous_floor_is_verified(self):
        previous = {"tag_name": "v1.3.1", "body": "<!-- presetlauncher:min-supported=1.3.0 -->\n"
                    "<!-- presetlauncher:update-policy=optional -->\n<!-- presetlauncher:signature-v1=dGVzdA== -->"}
        with patch.object(release, "installer_asset", return_value={"size": 4, "sha256": "ab" * 32}), patch.object(release, "verify") as verify:
            release.verify_previous(previous)
            verify.assert_called_once_with({"version": "1.3.1", "minimum_supported_version": "1.3.0", "policy": "optional"}, 4, "ab" * 32, "dGVzdA==")

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

    def test_signing_failure_prevents_release_mutation(self):
        with patch.object(release, "sign", side_effect=ValueError("key mismatch")), patch.object(release, "request", side_effect=self.response):
            with self.assertRaises(ValueError):
                release.publish()
        self.assertTrue(all(method == "GET" for method, _, _ in self.calls))

    def test_publish_occurs_after_both_uploads(self):
        with patch.object(release, "request", side_effect=self.response):
            release.publish()
        self.assertEqual(sum("/assets?" in url for _, url, _ in self.calls), 2)
        self.assertEqual(self.calls[-1][0], "PATCH")
        self.assertFalse(self.calls[-1][2]["draft"])


if __name__ == "__main__":
    unittest.main()
