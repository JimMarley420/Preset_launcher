"""Shared release classification and PR label validation."""
import re

TITLE = re.compile(r"(feat|fix|perf|docs|ci|test|refactor|chore|build|style|revert)(?:\([a-zA-Z0-9][a-zA-Z0-9._/-]*\))?(!)?: \S[^\r\n]*")
LEVELS = ("optional", "recommended", "required")


def bump(title, body=""):
    if not TITLE.fullmatch(title) or title != title.strip():
        raise ValueError("Use a Conventional Commit title, e.g. feat: add presets or fix(updater): verify downloads")
    kind, breaking = TITLE.fullmatch(title).groups()
    body = re.sub(r"<!--.*?-->", "", body or "", flags=re.S)
    if breaking or re.search(r"^BREAKING[ -]CHANGE: \S", body, re.M):
        return 3
    return {"feat": 2, "fix": 1, "perf": 1}.get(kind, 0)


def update_decision(pr, rank):
    labels = [label["name"] for label in pr["labels"] if label["name"].startswith("update:")]
    if not rank:
        if labels:
            raise ValueError("Non-releasing PRs must not have update labels")
        return {"number": pr["number"], "rank": 0, "policy": None}
    if len(labels) != 1 or labels[0] not in [f"update: {level}" for level in LEVELS]:
        raise ValueError("Select exactly one label: update: optional, update: recommended, or update: required")
    return {"number": pr["number"], "rank": rank, "policy": labels[0].split(": ")[1]}


def pr_decision(pr):
    return update_decision(pr, bump(pr["title"], pr.get("body")))
