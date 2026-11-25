"""Validate the actual PR title and maintainer-selected update label."""
import os
from release import branch_decision, git, open_prs

def check():
    sha = git("rev-parse", "HEAD")
    branch = os.environ.get("CIRCLE_BRANCH", "")
    if branch == "main":
        print("PR metadata check is not applicable to main.")
        return
    matches = open_prs(sha, branch)
    if not matches:
        raise ValueError("No eligible open PR for this commit. Open the PR, then rerun this job.")
    for pr in matches:
        decision = branch_decision(pr)
        print(f"PR #{pr['number']}: bump={decision['rank']}, policy={decision['policy']}")


if __name__ == "__main__":
    check()
