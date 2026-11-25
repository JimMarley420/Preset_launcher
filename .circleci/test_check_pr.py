import unittest
from policy import pr_decision
from release import REPO, branch_decision


def pr(title, labels=(), body=""):
    return {"number": 3, "title": title, "body": body,
            "labels": [{"name": label} for label in labels]}


class PullRequestPolicyTests(unittest.TestCase):
    def test_valid_titles_and_labels(self):
        for title, rank in (("feat: add presets", 2), ("fix(updater): verify downloads", 1),
                            ("feat!: change format", 3), ("perf: faster startup", 1)):
            for policy in ("optional", "recommended", "required"):
                with self.subTest(title=title, policy=policy):
                    self.assertEqual(pr_decision(pr(title, [f"update: {policy}"])),
                                     {"number": 3, "rank": rank, "policy": policy})

    def test_invalid_titles(self):
        for title in ("Update stuff", "feature: add presets", "fix:", "Fix: repair", "fix:  ", "feat: a\nfix: b"):
            with self.subTest(title=title), self.assertRaises(ValueError):
                pr_decision(pr(title, ["update: optional"]))

    def test_missing_conflicting_and_unknown_labels(self):
        for labels in ([], ["update: optional", "update: required"], ["update: typo"]):
            with self.subTest(labels=labels), self.assertRaises(ValueError):
                pr_decision(pr("fix: repair", labels))

    def test_non_releasing_pr(self):
        self.assertEqual(pr_decision(pr("docs: update"))["rank"], 0)
        with self.assertRaises(ValueError):
            pr_decision(pr("docs: update", ["update: optional"]))

    def test_breaking_footer(self):
        self.assertEqual(pr_decision(pr("fix: change format", ["update: recommended"],
                                       "BREAKING CHANGE: incompatible"))["rank"], 3)


    def test_stacked_pr_metadata(self):
        metadata = pr("build: migrate runtime")
        metadata["base"] = {"ref": "feat/github-release-updater"}
        metadata["head"] = {"repo": {"full_name": REPO}}
        self.assertEqual(branch_decision(metadata)["rank"], 0)
        with self.assertRaises(ValueError):
            branch_decision({**metadata, **pr("feat: change behavior", ["update: optional"])})
        with self.assertRaises(ValueError):
            branch_decision({**metadata, **pr("build: migrate runtime", ["update: optional"])})
        with self.assertRaises(ValueError):
            branch_decision({**metadata, "head": {"repo": {"full_name": "example/fork"}}})


class IndependentChecksTests(unittest.TestCase):
    def metadata(self, title, labels=(), body=""):
        return {**pr(title, labels, body), "base": {"ref": "main"},
                "head": {"repo": {"full_name": REPO}}}

    def test_missing_label_does_not_fail_title(self):
        from check_pr import validate
        item = self.metadata("feat: add presets")
        validate(item, "title")
        with self.assertRaisesRegex(ValueError, "Select exactly one label"):
            validate(item, "update-label")

    def test_bad_title_owned_by_title_check(self):
        from check_pr import validate
        item = self.metadata("Invalid title")
        with self.assertRaisesRegex(ValueError, "Conventional Commit"):
            validate(item, "title")
        validate(item, "update-label")

    def test_conflicting_or_unexpected_update_label(self):
        from check_pr import validate
        for title, labels in (("fix: repair", ["update: optional", "update: required"]),
                              ("ci: optimize", ["update: optional"]),
                              ("fix: repair", ["update: typo"])):
            with self.subTest(title=title, labels=labels):
                item = self.metadata(title, labels)
                validate(item, "title")
                with self.assertRaises(ValueError):
                    validate(item, "update-label")

    def test_breaking_footer_requires_policy(self):
        from check_pr import validate
        item = self.metadata("refactor: change format", body="BREAKING CHANGE: incompatible")
        validate(item, "title")
        with self.assertRaisesRegex(ValueError, "Select exactly one label"):
            validate(item, "update-label")
        validate(self.metadata("refactor: change format", ["update: required"],
                               "BREAKING CHANGE: incompatible"), "update-label")

    def test_non_releasing_and_valid_releasing_pr(self):
        from check_pr import validate
        for item in (self.metadata("ci: optimize"), self.metadata("fix: repair", ["update: optional"])):
            validate(item, "title")
            validate(item, "update-label")

    def test_stacked_releasing_pr_remains_invalid(self):
        from check_pr import validate
        item = self.metadata("feat: add presets", ["update: optional"])
        item["base"]["ref"] = "other-branch"
        with self.assertRaisesRegex(ValueError, "Stacked validation"):
            validate(item, "title")
