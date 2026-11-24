"""Validate the actual PR title and maintainer-selected update label."""
import os
from urllib.parse import urlencode
from release import API, git, request
from policy import pr_decision


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
        decision = pr_decision(pr)
        print(f"PR #{pr['number']}: bump={decision['rank']}, policy={decision['policy']}")


if __name__ == "__main__":
    check()
