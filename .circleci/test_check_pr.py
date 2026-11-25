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
