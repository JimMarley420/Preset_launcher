import copy
import os
import unittest
from unittest.mock import patch

import publish_winget as winget


class ManifestTests(unittest.TestCase):
    def setUp(self):
        self.version = "1.5.1"
        self.notes = "https://github.com/example/repo/releases/tag/v1.5.1"
        self.assets = {arch: {"url": f"https://example.invalid/{arch}.exe",
                              "sha256": char * 64, "size": 100}
                       for arch, char in zip(("x64", "x86", "arm64"), "abc")}
        base = {"PackageIdentifier": winget.PACKAGE, "PackageVersion": self.version}
        self.docs = {
            winget.PACKAGE + ".yaml": base | {"ManifestType": "version", "DefaultLocale": "en-US"},
            winget.PACKAGE + ".locale.en-US.yaml": base | {
                "ManifestType": "defaultLocale", "PackageLocale": "en-US",
                "ReleaseNotesUrl": self.notes},
            winget.PACKAGE + ".installer.yaml": base | {
                "ManifestType": "installer", "InstallerType": "inno",
                "Scope": "user", "ElevationRequirement": "elevatesSelf",
                "AppsAndFeaturesEntries": [{"ProductCode": winget.PRODUCT_CODE,
                                            "DisplayVersion": self.version}],
                "Installers": [{"Architecture": arch, "InstallerUrl": asset["url"],
                                "InstallerSha256": asset["sha256"].upper()}
                               for arch, asset in self.assets.items()]},
        }

    def verify(self, docs):
        winget.verify_manifests(docs, self.version, self.assets, self.notes)

    def test_signed_assets_and_registration_match(self):
        self.verify(self.docs)

    def test_inno_override_scope_is_not_published(self):
        docs = copy.deepcopy(self.docs)
        manifest = docs[winget.PACKAGE + ".installer.yaml"]
        manifest["Installers"] += [entry | {"Scope": "machine"} for entry in manifest["Installers"]]
        docs[winget.PACKAGE + ".installer.yaml"] = winget.user_scope_only(manifest)
        self.assertEqual(len(docs[winget.PACKAGE + ".installer.yaml"]["Installers"]), 3)
        self.verify(docs)

    def test_product_code_can_be_optimized_to_common_field(self):
        docs = copy.deepcopy(self.docs)
        manifest = docs[winget.PACKAGE + ".installer.yaml"]
        manifest["ProductCode"] = winget.PRODUCT_CODE
        del manifest["AppsAndFeaturesEntries"]
        self.verify(docs)

    def test_wrong_hash_url_architecture_or_scope_fails(self):
        for key, value in (("InstallerSha256", "0" * 64), ("InstallerUrl", "https://evil.invalid"),
                           ("Architecture", "x86"), ("Scope", "machine"),
                           ("ElevationRequirement", "elevationRequired")):
            docs = copy.deepcopy(self.docs)
            docs[winget.PACKAGE + ".installer.yaml"]["Installers"][0][key] = value
            with self.subTest(key=key), self.assertRaises(ValueError):
                self.verify(docs)

    def test_missing_registration_and_missing_manifest_fail(self):
        docs = copy.deepcopy(self.docs)
        docs[winget.PACKAGE + ".installer.yaml"]["AppsAndFeaturesEntries"] = []
        with self.assertRaises(ValueError):
            self.verify(docs)
        docs = copy.deepcopy(self.docs)
        del docs[winget.PACKAGE + ".yaml"]
        with self.assertRaises(ValueError):
            self.verify(docs)

    def test_manifest_version_or_identity_mismatch_fails(self):
        docs = copy.deepcopy(self.docs)
        docs[winget.PACKAGE + ".yaml"]["PackageVersion"] = "1.5.0"
        with self.assertRaises(ValueError):
            self.verify(docs)


class PublicationTests(unittest.TestCase):
    def test_no_catalog_never_generates_or_submits(self):
        with patch.object(winget.release.PLAN.__class__, "read_text",
                          return_value='{"release": false}'), \
                patch.dict(os.environ, {"WINGET_GITHUB_TOKEN": "test"}), \
                patch.object(winget, "release_for_plan", return_value={"tag_name": "v1.5.1"}), \
                patch.object(winget, "catalog", return_value=[]), \
                patch.object(winget, "generate") as generate, \
                patch.object(winget, "submit") as submit:
            winget.main()
            generate.assert_not_called()
            submit.assert_not_called()

    def test_existing_version_and_pending_submission_are_noops(self):
        for versions, duplicate in [([(1, 5, 1)], None), ([(1, 5, 0)], "https://example.invalid/pr")]:
            with self.subTest(versions=versions), \
                    patch.object(winget.release.PLAN.__class__, "read_text",
                                 return_value='{"release": false}'), \
                    patch.dict(os.environ, {"WINGET_GITHUB_TOKEN": "test"}), \
                    patch.object(winget, "release_for_plan", return_value={"tag_name": "v1.5.1"}), \
                    patch.object(winget, "catalog", return_value=versions), \
                    patch.object(winget, "existing_submission", return_value=duplicate), \
                    patch.object(winget, "generate") as generate:
                winget.main()
                generate.assert_not_called()

    def test_closed_submission_requires_manual_review(self):
        with patch.object(winget, "read", return_value=[{
                "head": {"ref": winget.branch_name("1.5.1")},
                "state": "closed", "merged_at": None, "html_url": "https://example.invalid/pr"}]):
            with self.assertRaises(ValueError):
                winget.existing_submission("1.5.1", "token")

    def test_stale_release_plan_does_not_submit(self):
        plan = {"sha": "head", "release": True, "tag": "v1.5.1"}
        with patch.dict(os.environ, {"CIRCLE_BRANCH": "main", "CIRCLE_SHA1": "head"}), \
                patch.object(winget.release, "git", return_value="head"), \
                patch.object(winget, "read", return_value={"tag_name": "v1.5.2"}):
            self.assertIsNone(winget.release_for_plan(plan, "token"))

    def test_branch_cannot_submit(self):
        with patch.dict(os.environ, {"CIRCLE_BRANCH": "feature"}), self.assertRaises(ValueError):
            winget.release_for_plan({}, "token")


if __name__ == "__main__":
    unittest.main()
