"""Independently validate PR titles and update labels."""
import argparse
import os
from policy import bump, update_decision
from release import REPO, git, open_prs


def validate(pr, mode):
    if mode == "title":
        rank = bump(pr["title"], pr.get("body"))
        if pr["base"]["ref"] != "main":
            head_repo = pr["head"]["repo"]
            if rank or not head_repo or head_repo["full_name"] != REPO:
                raise ValueError("Stacked validation requires a non-releasing PR from this repository")
        print(f"PR #{pr['number']}: valid title, bump={rank}")
        return
    if mode != "update-label":
        raise ValueError("Unknown validation mode")
    try:
        rank = bump(pr["title"], pr.get("body"))
    except ValueError:
        # The title check owns this failure; label requirements need a valid title.
        print(f"PR #{pr['number']}: update-label validation deferred to valid PR title")
        return
    decision = update_decision(pr, rank)
    print(f"PR #{pr['number']}: update policy={decision['policy']}")


def check(mode):
    sha = git("rev-parse", "HEAD")
    branch = os.environ.get("CIRCLE_BRANCH", "")
    if branch == "main":
        print("PR metadata check is not applicable to main.")
        return
    matches = open_prs(sha, branch)
    if not matches:
        raise ValueError("No eligible open PR for this commit. Open the PR, then rerun this job.")
    for pr in matches:
        validate(pr, mode)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("title", "update-label"))
    check(parser.parse_args().mode)
