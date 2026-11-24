import unittest
from check_pr import validate


class PullRequestTitleTests(unittest.TestCase):
    def test_valid_titles(self):
        for title in ("feat: add presets", "fix(updater): verify downloads", "feat!: change format", "docs: update README", "ci: optimize builds"):
            with self.subTest(title=title):
                validate(title, "")

    def test_invalid_titles(self):
        for title in ("Update stuff", "feature: add presets", "fix:", "Fix: repair", "fix:  ", "feat: a\nfix: b"):
            with self.subTest(title=title), self.assertRaises(ValueError):
                validate(title, "")

    def test_required_footer(self):
        validate("fix: repair", "Update-Required: true")
        validate("docs: update", "<!-- Update-Required: true -->")
        for title, body in (("docs: update", "Update-Required: true"), ("fix: repair", "Update-Required: yes"), ("fix: repair", "Update-Required: true\nUpdate-Required: true")):
            with self.subTest(title=title, body=body), self.assertRaises(ValueError):
                validate(title, body)
