"""Validate the squash-merge title and mandatory-update footer of open PRs."""
import os
import re
from urllib.parse import urlencode
from release import API, git, request, classify

TITLE = re.compile(r"(feat|fix|perf|docs|ci|test|refactor|chore|build|style|revert)(?:\([a-zA-Z0-9][a-zA-Z0-9._/-]*\))?(!)?: \S[^\r\n]*")
FOOTER = re.compile(r"^\s*Update-Required\s*:.*$", re.M | re.I)


def validate(title, body):
    if not TITLE.fullmatch(title) or title != title.strip():
        raise ValueError("PR title must use Conventional Commits, e.g. feat: add presets or fix(updater): verify downloads")
    body = re.sub(r"<!--.*?-->", "", body or "", flags=re.S)
    markers = FOOTER.findall(body)
    if markers and (len(markers) != 1 or markers[0].strip() != "Update-Required: true"):
        raise ValueError("Use exactly one Update-Required: true footer, or omit it")
    classify([title + "\n\n" + body])


def check():
    sha = git("rev-parse", "HEAD")
    branch = os.environ.get("CIRCLE_BRANCH", "")
    if branch == "main":
        print("PR metadata check is not applicable to main.")
        return
    page = 1
    matches = []
    while True:
        query = urlencode({"state": "open", "base": "main", "per_page": 100, "page": page})
        pulls = request(API + "/pulls?" + query)
        matches.extend(pr for pr in pulls if pr["head"]["sha"] == sha and pr["head"]["ref"] == branch)
        if len(pulls) < 100:
            break
        page += 1
    if not matches:
        raise ValueError("No open PR targeting main for this commit. Open the PR, then rerun this job.")
    for pr in matches:
        validate(pr["title"], pr.get("body"))
        print(f"PR #{pr['number']}: valid Conventional Commit title")


if __name__ == "__main__":
    check()
