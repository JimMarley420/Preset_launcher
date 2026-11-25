"""Plan stable releases from Conventional Commits and publish tested artifacts."""
import hashlib
import json
import os
from pathlib import Path
import re
import random
import time
import errno
import http.client
from datetime import datetime, timezone
from email.utils import parsedate_to_datetime
import subprocess
import sys
from policy import LEVELS, pr_decision
from sign_update import sign, verify
from urllib.error import HTTPError, URLError
from urllib.parse import quote, urlencode, urlsplit
from urllib.request import Request, urlopen

REPO = "JimMarley420/Preset_launcher"
API = f"https://api.github.com/repos/{REPO}"
PLAN = Path("artifacts/release-plan.json")
OUTPUT = Path("artifacts/verified-installer")
VERSION = re.compile(r"v?(0|[1-9][0-9]{0,4})\.(0|[1-9][0-9]{0,4})\.(0|[1-9][0-9]{0,4})\Z")
POLICY = re.compile(r"^<!-- presetlauncher:min-supported=([0-9.]+) -->\r?$", re.M)
# Last main commit before mandatory PR metadata was introduced.
ROLLOUT_BASE = "8535d8e943a6503d4982ff9a5164ef66392ba3ea"


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


def make_plan(previous, decisions, sha):
    current = version(previous["tag_name"])
    floor = minimum(previous)
    rank = max((item["rank"] for item in decisions), default=0)
    policies = [item["policy"] for item in decisions if item["rank"]]
    selected = max(policies, key=LEVELS.index) if policies else "optional"
    major, minor, patch = current
    next_version = {0: current, 1: (major, minor, patch + 1),
                    2: (major, minor + 1, 0), 3: (major + 1, 0, 0)}[rank]
    value = format_version(next_version)
    version(value)
    return {"previous": previous["tag_name"], "version": value,
            "tag": f"v{value}", "sha": sha, "release": rank != 0,
            "policy": selected, "decisions": decisions,
            "minimum_supported_version": format_version(next_version if selected == "required" else floor)}


def merged_decisions(start, end):
    decisions = []
    for sha in git("rev-list", "--first-parent", "--reverse", f"{start}..{end}").splitlines():
        pulls = request(API + f"/commits/{sha}/pulls")
        matches = [pr for pr in pulls if pr.get("merge_commit_sha") == sha
                   and pr["base"]["ref"] == "main" and pr["base"]["repo"]["full_name"] == REPO
                   and pr.get("merged_at")]
        if len(matches) != 1:
            raise ValueError(f"Expected one merged main PR for {sha}; direct or ambiguous commits cannot publish")
        pr = request(API + f"/pulls/{matches[0]['number']}")
        decisions.append(pr_decision(pr))
    return decisions


def collect_decisions(previous, sha, branch, pr=None):
    tag = previous["tag_name"]
    tag_sha = git("rev-parse", f"refs/tags/{tag}^{{commit}}")
    # Only grandfather commits before the new system went live.
    start = ROLLOUT_BASE if tag == "1.2.6" else tag_sha
    if branch == "main":
        subprocess.run(["git", "merge-base", "--is-ancestor", start, sha], check=True)
        return merged_decisions(start, sha)
    git("fetch", f"https://github.com/{REPO}.git", "refs/heads/main")
    base = git("merge-base", sha, "FETCH_HEAD")
    subprocess.run(["git", "merge-base", "--is-ancestor", start, base], check=True)
    decisions = merged_decisions(start, base)
    return decisions + [branch_decision(pr or branch_pr(sha, branch))]


def open_prs(sha, branch):
    matches = []
    page = 1
    while True:
        query = urlencode({"state": "open", "per_page": 100, "page": page})
        pulls = request(API + "/pulls?" + query)
        matches.extend(pr for pr in pulls if pr["head"]["sha"] == sha
                       and pr["head"]["ref"] == branch
                       and pr["base"]["repo"]["full_name"] == REPO)
        if len(pulls) < 100:
            return matches
        page += 1


def branch_pr(sha, branch):
    matches = open_prs(sha, branch)
    if len(matches) != 1:
        raise ValueError("Expected one eligible open PR for this commit")
    return matches[0]


def needs_installer(paths):
    for path in paths:
        path = path.casefold()
        name = path.rsplit("/", 1)[-1]
        if (path.startswith((".circleci/", "installer/"))
                or "/publishprofiles/" in path
                or name.endswith((".csproj", ".props", ".targets", ".iss", ".pubxml", ".sln",
                                  ".slnx", ".vcxproj", ".manifest", ".ps1", ".pfx"))
                or name in ("global.json", "nuget.config", "packages.lock.json", "packages.config")
                or (name.endswith(".cs") and ("update" in name or name in ("app.xaml.cs", "appsettings.cs",
                                                                          "settingsservice.cs", "mainwindow.xaml.cs", "settingspage.xaml.cs",
                                                                          "localizationservice.cs")))
                or name in ("app.xaml", "mainwindow.xaml", "settingspage.xaml", "favicon.ico")):
            return True
    return False


def validate_installer(branch, pr, sha):
    if branch == "main":
        return True
    base = pr["base"]["ref"]
    git("fetch", "--no-tags", f"https://github.com/{REPO}.git", f"refs/heads/{base}")
    comparison = git("merge-base", "FETCH_HEAD", sha)
    # Report both sides of a rename as changed paths.
    changed = git("diff", "--name-only", "--no-renames", "-z", comparison, sha, "--")
    return needs_installer(changed.split("\0"))


def branch_decision(pr):
    decision = pr_decision(pr)
    if pr["base"]["ref"] != "main":
        head_repo = pr["head"]["repo"]
        if decision["rank"] or not head_repo or head_repo["full_name"] != REPO:
            raise ValueError("Stacked validation requires a non-releasing PR from this repository")
    return decision


def installer_asset(release):
    tag = release["tag_name"]
    name = f"PresetLauncher_Setup_{format_version(version(tag))}.exe"
    assets = [asset for asset in release["assets"] if asset["name"] == name]
    if len(assets) != 1:
        raise ValueError("Expected one previous release installer")
    asset = assets[0]
    url = f"https://github.com/{REPO}/releases/download/{tag}/{name}"
    if (asset["state"] != "uploaded" or asset["browser_download_url"] != url
            or not 0 < asset["size"] <= 1024**3
            or not re.fullmatch(r"sha256:[0-9a-fA-F]{64}", asset.get("digest") or "")):
        raise ValueError("Invalid previous installer metadata")
    return {"url": url, "size": asset["size"], "sha256": asset["digest"][7:].lower()}


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
    attempts = 4 if method == "GET" and data is None else 1
    for attempt in range(attempts):
        try:
            with urlopen(Request(url, data=data, headers=headers, method=method), timeout=30) as response:
                return json.load(response)
        except (HTTPError, URLError, TimeoutError, ConnectionError, http.client.IncompleteRead,
                http.client.RemoteDisconnected) as error:
            if attempt == attempts - 1 or not transient(error):
                raise
            delay = retry_delay(error, attempt)
            if delay is None:
                raise
            # Log only the path, not authorization headers, query parameters or payloads.
            print(f"GitHub GET {urlsplit(url).path}: {type(error).__name__}"
                  f"{' ' + str(error.code) if isinstance(error, HTTPError) else ''}; "
                  f"retry {attempt + 1}/{attempts - 1} in {delay:.1f}s", file=sys.stderr, flush=True)
            if isinstance(error, HTTPError):
                error.close()
            time.sleep(delay)


def transient(error):
    if isinstance(error, HTTPError):
        # A normal 403 is not retryable. GitHub also uses 403 for rate limits.
        return (error.code in (429, 500, 502, 503, 504)
                or (error.code == 403 and (error.headers is not None and
                    (error.headers.get("Retry-After") is not None
                     or error.headers.get("X-RateLimit-Remaining") == "0"))))
    reason = error.reason if isinstance(error, URLError) else error
    return (isinstance(reason, (TimeoutError, ConnectionError, http.client.IncompleteRead,
                                http.client.RemoteDisconnected))
            or isinstance(reason, OSError) and reason.errno in
            (errno.EAGAIN, errno.ETIMEDOUT, errno.ECONNRESET, errno.ECONNREFUSED,
             errno.ECONNABORTED, errno.EHOSTUNREACH, errno.ENETUNREACH))


def retry_delay(error, attempt):
    # Full jitter, with a 30-second ceiling on server-requested delays.
    delay = random.uniform(0, min(8, 2 ** (attempt + 1)))
    if not isinstance(error, HTTPError) or error.headers is None:
        return delay
    headers = error.headers
    now = datetime.now(timezone.utc)
    retry_after = headers.get("Retry-After")
    if retry_after is not None:
        try:
            if retry_after.isascii() and retry_after.isdecimal():
                requested = int(retry_after)
            else:
                target = parsedate_to_datetime(retry_after)
                if target.tzinfo is None:
                    target = target.replace(tzinfo=timezone.utc)
                requested = (target - now).total_seconds()
        except (ValueError, TypeError, OverflowError):
            return None
        delay = max(delay, requested)
    if headers.get("X-RateLimit-Remaining") == "0":
        try:
            delay = max(delay, int(headers["X-RateLimit-Reset"]) - now.timestamp())
        except (KeyError, ValueError, TypeError, OverflowError):
            return None
    # Never retry before the server's requested time. Let a later workflow retry.
    return delay if delay <= 30 else None


def git(*args):
    return subprocess.check_output(["git", *args], text=True).strip()


def verify_previous(release):
    tag = release["tag_name"]
    body = release.get("body") or ""
    signatures = re.findall(r"^<!-- presetlauncher:signature-v1=([A-Za-z0-9+/=]+) -->\r?$", body, re.M)
    # Only the two historical public releases predate signing.
    if not signatures and tag in ("1.2.6", "v1.3.0"):
        return
    levels = re.findall(r"^<!-- presetlauncher:update-policy=(optional|recommended|required) -->\r?$", body, re.M)
    if len(signatures) != 1 or len(levels) != 1:
        raise ValueError("Missing or ambiguous previous release signature or policy")
    floor = minimum(release)
    if levels[0] == "required" and floor != version(tag):
        raise ValueError("Invalid previous required release policy")
    asset = installer_asset(release)
    data = {"version": format_version(version(tag)),
            "minimum_supported_version": format_version(floor), "policy": levels[0]}
    verify(data, asset["size"], asset["sha256"], signatures[0])


def plan():
    previous = request(API + "/releases/latest")
    if previous["draft"] or previous["prerelease"]:
        raise ValueError("Expected a published stable release")
    if git("rev-parse", "--is-shallow-repository") == "true":
        git("fetch", "--unshallow", "origin")
    # Use the upstream release tag even when validating a contribution from a fork.
    tag = previous["tag_name"]
    version(tag)
    verify_previous(previous)
    git("fetch", f"https://github.com/{REPO}.git", f"refs/tags/{tag}:refs/tags/{tag}")
    sha = git("rev-parse", "HEAD")
    tag_sha = git("rev-parse", f"refs/tags/{tag}^{{commit}}")
    subprocess.run(["git", "merge-base", "--is-ancestor", tag_sha, sha], check=True)
    branch = os.environ.get("CIRCLE_BRANCH", "")
    pr = None if branch == "main" else branch_pr(sha, branch)
    result = make_plan(previous, collect_decisions(previous, sha, branch, pr), sha)
    result["validate_installer"] = validate_installer(branch, pr, sha)
    if result["validate_installer"] and (result["release"] or branch != "main"):
        result["previous_installer"] = installer_asset(previous)
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
    verify_previous(previous)
    remote_head = request(API + "/git/ref/heads/main", token=token)["object"]["sha"]
    if remote_head != data["sha"]:
        raise ValueError("Main advanced; run the latest pipeline instead")
    expected = make_plan(previous, collect_decisions(previous, data["sha"], "main"), data["sha"])
    if any(data.get(key) != value for key, value in expected.items()):
        raise ValueError("PR release metadata changed after validation; run a fresh main pipeline")
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
    signature = sign(data, installer.stat().st_size, digest)
    # Do not reuse an existing tag or silently overwrite a partial release.
    try:
        request(API + "/git/ref/tags/" + quote(tag, safe=""), token=token)
    except HTTPError as error:
        if error.code != 404:
            raise
    else:
        raise ValueError("Release tag already exists; inspect it before retrying")
    page = 1
    while True:
        releases = request(API + f"/releases?per_page=100&page={page}", token=token)
        if any(item["tag_name"] == tag for item in releases):
            raise ValueError("Release or draft already exists; inspect it before retrying")
        if len(releases) < 100:
            break
        page += 1
    notes = request(API + "/releases/generate-notes", "POST",
                    {"tag_name": tag, "target_commitish": data["sha"],
                     "previous_tag_name": data["previous"]}, token)
    if re.search(r"(?m)^<!-- presetlauncher:", notes["body"]):
        raise ValueError("Generated notes contain reserved update metadata")
    body = (f"<!-- presetlauncher:min-supported={format_version(floor)} -->\n"
            f"<!-- presetlauncher:update-policy={data['policy']} -->\n"
            f"<!-- presetlauncher:signature-v1={signature} -->\n\n" + notes["body"])
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
    final_plan = make_plan(previous, collect_decisions(previous, data["sha"], "main"), data["sha"])
    if any(data.get(key) != value for key, value in final_plan.items()):
        raise ValueError("PR metadata changed during upload; draft left unpublished")
    published = request(API + f"/releases/{release_id}", "PATCH",
                        {"draft": False, "make_latest": "true"}, token)
    if published["draft"] or published["tag_name"] != tag:
        raise ValueError("Unexpected publish result")
    print(published["html_url"])


if __name__ == "__main__":
    if len(sys.argv) != 2 or sys.argv[1] not in ("plan", "publish"):
        sys.exit("usage: release.py plan|publish")
    {"plan": plan, "publish": publish}[sys.argv[1]]()
