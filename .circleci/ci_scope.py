"""Select a Linux-only lane for proven non-application changes."""
import json
import os
from pathlib import Path, PurePosixPath
import subprocess
import sys
from urllib.request import Request, urlopen

import release

ROOT_DOCS = {
    "README.md", "README.fr.md", "CONTRIBUTING.md", "CODE_OF_CONDUCT.md",
    "SECURITY.md", ".github/pull_request_template.md",
}
IMAGES = {".png", ".jpg", ".jpeg", ".webp", ".gif", ".svg", ".ico"}
WORKER = {
    "tools/auto-labeler/worker.mjs", "tools/auto-labeler/worker.test.mjs",
}


def non_application_path(path):
    suffix = PurePosixPath(path).suffix
    return (path in ROOT_DOCS or path in WORKER
            or (path.startswith("docs/") and suffix in IMAGES | {".md"})
            or (path.startswith(".github/ISSUE_TEMPLATE/") and suffix in {".md", ".yml", ".yaml"})
            or (path.startswith("website/") and suffix in IMAGES | {".html", ".css", ".js", ".txt"}))


def non_application_diff(raw):
    # Raw diff includes file modes; symlinks, submodules and type changes are
    # never eligible. No rename detection means both old and new paths count.
    if not raw or not raw.endswith(b"\0"):
        return False
    fields = raw[:-1].split(b"\0")
    if len(fields) % 2:
        return False
    for meta, path in zip(fields[::2], fields[1::2]):
        parts = meta.split()
        if len(parts) != 5 or not meta.startswith(b":"):
            return False
        old, new, _, _, status = parts
        if (old[1:], new, status) not in {
            (b"100644", b"100644", b"M"),
            (b"000000", b"100644", b"A"),
            (b"100644", b"000000", b"D"),
        }:
            return False
        if not non_application_path(os.fsdecode(path)):
            return False
    return True


def identity():
    sha = release.git("rev-parse", "HEAD")
    branch = os.environ["CIRCLE_BRANCH"]
    if sha != os.environ["CIRCLE_SHA1"] or not branch:
        raise ValueError("Checkout does not match pipeline")
    return branch, sha


def eligible():
    branch, sha = identity()
    if release.git("rev-parse", "--is-shallow-repository") == "true":
        release.git("fetch", "--unshallow", "origin")
    if branch == "main":
        parents = release.git("rev-list", "--parents", "-n", "1", sha).split()
        if len(parents) != 2:
            return False
        base = parents[1]
    else:
        pr = release.branch_pr(sha, branch)
        if pr["base"]["ref"] != "main" or release.branch_decision(pr)["rank"]:
            return False
        release.git("fetch", "--no-tags", f"https://github.com/{release.REPO}.git", "refs/heads/main")
        base = release.git("rev-parse", "FETCH_HEAD")
        # Stacked or stale PRs take the existing Windows lane.
        if base != pr["base"]["sha"] or release.git("merge-base", base, sha) != base:
            return False
    raw = subprocess.check_output([
        "git", "diff", "--raw", "--no-renames", "-z", base, sha, "--",
    ])
    return non_application_diff(raw)


def check_plan():
    branch, sha = identity()
    data = json.loads(release.PLAN.read_text(encoding="utf-8"))
    if (data["sha"] != sha or data["release"] is not False
            or data.get("validation_only", False) is not (branch != "main")):
        raise ValueError("Linux-only validation requires a non-releasing plan for this checkout")
    print("Windows gate not applicable: non-releasing, non-application change.")


def verify():
    check_plan()
    if not eligible():
        raise ValueError("Change scope is no longer eligible; trigger a fresh pipeline")


def select_executor():
    try:
        if eligible():
            # A docs merge can still carry a pending release from an earlier
            # merge. The canonical release planner must rule that out.
            release.plan()
            check_plan()
            return "linux-small"
    except (OSError, ValueError, KeyError, TypeError, subprocess.CalledProcessError) as error:
        print(f"Could not prove lightweight eligibility ({type(error).__name__}); using Windows.")
    return "windows-x64"


def continue_pipeline():
    runner = select_executor()
    print(f"Selected validation executor: {runner}")
    body = json.dumps({
        "continuation-key": os.environ["CIRCLE_CONTINUATION_KEY"],
        "configuration": Path(".circleci/continue.yml").read_text(encoding="utf-8"),
        "parameters": {"validation-executor": runner},
    }).encode("utf-8")
    request = Request("https://circleci.com/api/v2/pipeline/continue", data=body,
                      headers={"Content-Type": "application/json"}, method="POST")
    # Continuation is a single-use mutation. Do not retry or print its key.
    with urlopen(request, timeout=30) as response:
        print(f"Continuation accepted (HTTP {response.status})")


if __name__ == "__main__":
    commands = {"continue": continue_pipeline, "verify": verify, "check-plan": check_plan}
    if len(sys.argv) != 2 or sys.argv[1] not in commands:
        sys.exit("usage: ci_scope.py continue|verify|check-plan")
    commands[sys.argv[1]]()
