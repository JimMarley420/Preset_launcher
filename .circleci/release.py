"""Plan stable releases from Conventional Commits and publish tested artifacts."""
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
from urllib.error import HTTPError
from urllib.parse import quote, urlencode
from urllib.request import Request, urlopen

REPO = "JimMarley420/Preset_launcher"
API = f"https://api.github.com/repos/{REPO}"
PLAN = Path("artifacts/release-plan.json")
OUTPUT = Path("artifacts/verified-installer")
VERSION = re.compile(r"v?(0|[1-9][0-9]{0,4})\.(0|[1-9][0-9]{0,4})\.(0|[1-9][0-9]{0,4})\Z")
POLICY = re.compile(r"^<!-- presetlauncher:min-supported=([0-9.]+) -->$", re.M)
CONVENTIONAL = re.compile(r"^([a-z]+)(?:\([^()\r\n]+\))?(!)?: .+")


def version(value):
    match = VERSION.fullmatch(value)
    if not match:
        raise ValueError(f"Invalid stable version: {value}")
    result = tuple(map(int, match.groups()))
    if any(part > 65534 for part in result):
        raise ValueError("Windows file version component exceeds 65534")
    return result


def format_version(value):
    return ".".join(map(str, value))


def minimum(release):
    matches = POLICY.findall(release.get("body") or "")
    if not matches and release["tag_name"] == "1.2.6":
        return (0, 0, 0)
    if len(matches) != 1:
        raise ValueError("Missing or ambiguous release policy")
    floor = version(matches[0])
    if floor > version(release["tag_name"]):
        raise ValueError("Minimum supported version exceeds release version")
    return floor


def classify(messages):
    rank = 0
    required = False
    for message in messages:
        lines = message.strip().splitlines()
        if not lines:
            continue
        match = CONVENTIONAL.fullmatch(lines[0])
        if not match:
            continue
        kind, breaking = match.groups()
        if breaking or re.search(r"^BREAKING[ -]CHANGE: .+", message, re.M):
            bump = 3
        elif kind == "feat":
            bump = 2
        elif kind in ("fix", "perf"):
            bump = 1
        else:
            bump = 0
        if re.search(r"^Update-Required: true\s*$", message, re.M):
            if bump == 0:
                raise ValueError("Update-Required requires a releasing commit")
            required = True
        rank = max(rank, bump)
    return rank, required


def make_plan(previous, messages, sha):
    current = version(previous["tag_name"])
    floor = minimum(previous)
    rank, required = classify(messages)
    major, minor, patch = current
    next_version = {0: current, 1: (major, minor, patch + 1),
                    2: (major, minor + 1, 0), 3: (major + 1, 0, 0)}[rank]
    value = format_version(next_version)
    version(value)
    return {"previous": previous["tag_name"], "version": value,
            "tag": f"v{value}", "sha": sha, "release": rank != 0,
            "minimum_supported_version": format_version(next_version if required else floor)}


def request(url, method="GET", payload=None, token=None, file=None):
    headers = {"Accept": "application/vnd.github+json",
               "X-GitHub-Api-Version": "2022-11-28", "User-Agent": "PresetLauncher-CI"}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    if file is not None:
        headers["Content-Type"] = "application/octet-stream"
        headers["Content-Length"] = str(file.stat().st_size)
        with file.open("rb") as stream:
            with urlopen(Request(url, data=stream, headers=headers, method=method), timeout=300) as response:
                return json.load(response)
    data = None
    if payload is not None:
        headers["Content-Type"] = "application/json"
        data = json.dumps(payload).encode()
    with urlopen(Request(url, data=data, headers=headers, method=method), timeout=30) as response:
        return json.load(response)


def git(*args):
    return subprocess.check_output(["git", *args], text=True).strip()


def plan():
    previous = request(API + "/releases/latest")
    if previous["draft"] or previous["prerelease"]:
        raise ValueError("Expected a published stable release")
    if git("rev-parse", "--is-shallow-repository") == "true":
        git("fetch", "--unshallow", "origin")
    # Use the upstream release tag even when validating a contribution from a fork.
    tag = previous["tag_name"]
    version(tag)
    git("fetch", f"https://github.com/{REPO}.git", f"refs/tags/{tag}:refs/tags/{tag}")
    sha = git("rev-parse", "HEAD")
    tag_sha = git("rev-parse", f"refs/tags/{tag}^{{commit}}")
    subprocess.run(["git", "merge-base", "--is-ancestor", tag_sha, sha], check=True)
    messages = git("log", "--format=%B%x00", f"{tag_sha}..{sha}").split("\0")
    result = make_plan(previous, messages, sha)
    PLAN.parent.mkdir(parents=True, exist_ok=True)
    PLAN.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(result, indent=2))


def publish():
    data = json.loads(PLAN.read_text(encoding="utf-8"))
    if os.environ.get("CIRCLE_BRANCH") != "main" or git("rev-parse", "HEAD") != data["sha"]:
        raise ValueError("Release publication requires the validated main commit")
    if not data["release"]:
        print("No releasing commits; nothing to publish.")
        return
    token = os.environ["GH_RELEASE_TOKEN"]
    previous = request(API + "/releases/latest", token=token)
    if previous["tag_name"] != data["previous"]:
        raise ValueError("Release plan is stale; run a fresh main pipeline")
    remote_head = request(API + "/git/ref/heads/main", token=token)["object"]["sha"]
    if remote_head != data["sha"]:
        raise ValueError("Main advanced; run the latest pipeline instead")
    value = data["version"]
    tag = data["tag"]
    if tag != f"v{format_version(version(value))}" or version(value) <= version(data["previous"]):
        raise ValueError("Release plan has an invalid version")
    floor = version(data["minimum_supported_version"])
    if not minimum(previous) <= floor <= version(value):
        raise ValueError("Invalid release policy")
    installer = OUTPUT / f"PresetLauncher_Setup_{value}.exe"
    sums = OUTPUT / "SHA256SUMS.txt"
    with installer.open("rb") as stream:
        digest = hashlib.file_digest(stream, "sha256").hexdigest()
    if sums.read_text(encoding="ascii").strip() != f"{digest}  {installer.name}":
        raise ValueError("Validated installer checksum mismatch")
    # Do not reuse an existing tag or silently overwrite a partial release.
    try:
        request(API + "/git/ref/tags/" + quote(tag, safe=""), token=token)
    except HTTPError as error:
        if error.code != 404:
            raise
    else:
        raise ValueError("Release tag already exists; inspect it before retrying")
    notes = request(API + "/releases/generate-notes", "POST",
                    {"tag_name": tag, "target_commitish": data["sha"],
                     "previous_tag_name": data["previous"]}, token)
    body = f"<!-- presetlauncher:min-supported={format_version(floor)} -->\n\n" + notes["body"]
    release = request(API + "/releases", "POST",
                      {"tag_name": tag, "target_commitish": data["sha"], "name": f"PresetLauncher {tag}",
                       "body": body, "draft": True, "prerelease": False}, token)
    release_id = release["id"]
    print(f"Draft created: {release_id}. If interrupted, inspect the draft before retrying.")
    for path in (installer, sums):
        upload_url = f"https://uploads.github.com/repos/{REPO}/releases/{release_id}/assets?{urlencode({'name': path.name})}"
        asset = request(upload_url, "POST", token=token, file=path)
        with path.open("rb") as stream:
            expected = hashlib.file_digest(stream, "sha256").hexdigest()
        if asset["name"] != path.name or asset["state"] != "uploaded" or asset["digest"] != f"sha256:{expected}":
            raise ValueError("GitHub asset verification failed; draft left unpublished")
    # Recheck main and latest before making the draft visible.
    if request(API + "/git/ref/heads/main", token=token)["object"]["sha"] != data["sha"]:
        raise ValueError("Main advanced during upload; draft left unpublished")
    if request(API + "/releases/latest", token=token)["tag_name"] != data["previous"]:
        raise ValueError("Latest release changed during upload; draft left unpublished")
    published = request(API + f"/releases/{release_id}", "PATCH",
                        {"draft": False, "make_latest": "true"}, token)
    if published["draft"] or published["tag_name"] != tag:
        raise ValueError("Unexpected publish result")
    print(published["html_url"])


if __name__ == "__main__":
    if len(sys.argv) != 2 or sys.argv[1] not in ("plan", "publish"):
        sys.exit("usage: release.py plan|publish")
    {"plan": plan, "publish": publish}[sys.argv[1]]()
