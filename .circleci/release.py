"""Plan stable releases from Conventional Commits and publish tested artifacts."""
import base64
import hashlib
import json
import os
from pathlib import Path
import re
import random
import time
import errno
import http.client
import socket
import ssl
from datetime import datetime, timezone
from email.utils import parsedate_to_datetime
import subprocess
import sys
from policy import LEVELS, bump, pr_decision
from sign_update import sign, sign_message, verify, verify_message
from urllib.error import HTTPError, URLError
from urllib.parse import quote, urlencode, urlsplit
from urllib.request import Request, urlopen

REPO = "JimMarley420/Preset_launcher"
API = f"https://api.github.com/repos/{REPO}"
PLAN = Path("artifacts/release-plan.json")
OUTPUT = Path("Installer/Output")
EVIDENCE = Path("artifacts/windows-validation.json")
ARCHITECTURES = json.loads((Path(__file__).resolve().parent.parent / "Installer/architectures.json").read_text())
VERSION = re.compile(r"v?(0|[1-9][0-9]{0,4})\.(0|[1-9][0-9]{0,4})\.(0|[1-9][0-9]{0,4})\Z")
POLICY = re.compile(r"^<!-- presetlauncher:min-supported=([0-9.]+) -->\r?$", re.M)
APP_NOTES_BEGIN = "<!-- presetlauncher-app-notes:v1:begin -->"
APP_NOTES_END = "<!-- presetlauncher-app-notes:v1:end -->"
SUMMARY_BEGIN = "<!-- presetlauncher-summary:begin -->"
SUMMARY_END = "<!-- presetlauncher-summary:end -->"
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


def is_source(path):
    return path.casefold().endswith((".cs", ".xaml"))


def needs_installer(paths):
    for path in filter(None, paths):
        path = path.casefold()
        name = path.rsplit("/", 1)[-1]
        if name.endswith(".cs") and any(part in name for part in ("update", "interop", "native")):
            return True
        if (path.startswith((".circleci/", "installer/"))
                or "/publishprofiles/" in path
                or name.endswith((".csproj", ".props", ".targets", ".iss", ".pubxml", ".sln",
                                  ".slnx", ".vcxproj", ".manifest", ".ps1", ".pfx"))
                or name in ("global.json", "nuget.config", "packages.lock.json", "packages.config")
                or name in ("app.xaml.cs", "appsettings.cs", "settingsservice.cs", "mainwindow.xaml.cs",
                            "settingspage.xaml.cs", "localizationservice.cs")
                or name in ("app.xaml", "mainwindow.xaml", "settingspage.xaml", "favicon.ico",
                            "nativewindow.cs", "processselector.cs", "launcherservice.cs", "exportpath.cs",
                            "windowssystemdispatcherqueuehelper.cs", "themeservice.cs")):
            return True
        if path in ("readme.md", "readme.fr.md", "security.md", "contributing.md", "code_of_conduct.md") or (
                path.startswith("docs/") and path.endswith(".md")):
            continue
        if path.startswith(("presetlauncherwinui3/", "tests/presetlauncher.tests/")) and is_source(path):
            continue
        return True
    return False


def validate_installer(branch, pr, sha):
    if branch == "main":
        return True
    base = pr["base"]["ref"]
    git("fetch", "--no-tags", f"https://github.com/{REPO}.git", f"refs/heads/{base}")
    base_sha = pr["base"].get("sha")
    if not base_sha or not re.fullmatch(r"[0-9a-f]{40}", base_sha):
        return True
    if pr["head"]["sha"] != sha or git("rev-parse", "FETCH_HEAD") != base_sha:
        return True
    comparison = git("merge-base", base_sha, sha)
    # Report renames as deletion plus addition. A new source path has no
    # established classification, even if it resembles ordinary application code.
    changed = git("diff", "--name-status", "--no-renames", "-z", comparison, sha, "--")
    if not changed:
        return False
    fields = changed.removesuffix("\0").split("\0")
    if len(fields) % 2 or not changed.endswith("\0"):
        return True
    paths = []
    for status, path in zip(fields[::2], fields[1::2]):
        if not path or status not in ("A", "M", "D") or (status == "A" and is_source(path)):
            return True
        paths.append(path)
    if needs_installer(paths):
        return True
    app_sources = [path for path in paths if path.casefold().startswith("presetlauncherwinui3/") and is_source(path)]
    if not app_sources:
        return False
    # Also catch native API or architecture selection introduced in an
    # otherwise ordinary source file.
    platform_api = (r"\b(DllImport|LibraryImport|NativeLibrary|Marshal|IntPtr|UIntPtr|nint|nuint|"
                    r"RuntimeInformation|ProcessArchitecture|OSArchitecture|RegistryView|Microsoft\.Win32)\b")
    return bool(git("diff", "--name-only", "--no-renames", "-G", platform_api, comparison, sha, "--", *app_sources))


def branch_decision(pr):
    # Open PR builds use a provisional policy, independent of maintainer labels.
    # Keep the existing strict eligibility checks for stacked PRs.
    if pr["base"]["ref"] != "main":
        decision = pr_decision(pr)
        head_repo = pr["head"]["repo"]
        if decision["rank"] or not head_repo or head_repo["full_name"] != REPO:
            raise ValueError("Stacked validation requires a non-releasing PR from this repository")
        return decision
    rank = bump(pr["title"], pr.get("body"))
    return {"number": pr["number"], "rank": rank,
            "policy": "optional" if rank else None}


def installer_name(value, architecture):
    return f"PresetLauncher_Setup_{value}{ARCHITECTURES[architecture]['suffix']}.exe"


def installer_asset(release, architecture="x64"):
    tag = release["tag_name"]
    name = installer_name(format_version(version(tag)), architecture)
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
        try:
            with file.open("rb") as stream:
                with urlopen(Request(url, data=stream, headers=headers, method=method), timeout=300) as response:
                    return json.load(response)
        except HTTPError as error:
            error.close()
            raise
    data = None
    if payload is not None:
        headers["Content-Type"] = "application/json"
        data = json.dumps(payload).encode()
    attempts = 4 if method == "GET" and data is None else 1
    remaining_sleep = 30.0
    for attempt in range(attempts):
        try:
            with urlopen(Request(url, data=data, headers=headers, method=method), timeout=30) as response:
                return json.load(response)
        except (OSError, http.client.IncompleteRead) as error:
            try:
                if attempt == attempts - 1 or not transient(error):
                    raise
                delay = retry_delay(error, attempt)
                if delay is None or delay > remaining_sleep:
                    raise
            finally:
                if isinstance(error, HTTPError):
                    error.close()
            # Log only the path, not authorization headers, query parameters or payloads.
            print(f"GitHub GET {urlsplit(url).path}: {type(error).__name__}"
                  f"{' ' + str(error.code) if isinstance(error, HTTPError) else ''}; "
                  f"retry {attempt + 1}/{attempts - 1} in {delay:.1f}s", file=sys.stderr, flush=True)
            started = time.monotonic()
            time.sleep(delay)
            remaining_sleep -= max(delay, time.monotonic() - started)


def transient(error):
    if isinstance(error, HTTPError):
        # A normal 403 is not retryable. GitHub also uses 403 for rate limits.
        return (error.code in (429, 500, 502, 503, 504)
                or (error.code == 403 and (error.headers is not None and
                    (error.headers.get("Retry-After") is not None
                     or error.headers.get("X-RateLimit-Remaining") == "0"))))
    reason = error.reason if isinstance(error, URLError) else error
    if isinstance(reason, ssl.SSLError):
        return False
    if isinstance(reason, socket.gaierror):
        return reason.errno == socket.EAI_AGAIN
    return (isinstance(reason, (TimeoutError, ConnectionError, http.client.IncompleteRead,
                                http.client.RemoteDisconnected))
            or isinstance(reason, OSError) and reason.errno in
            (errno.EAGAIN, errno.ETIMEDOUT, errno.ECONNRESET, errno.ECONNREFUSED,
             errno.ECONNABORTED, errno.EHOSTUNREACH, errno.ENETUNREACH))


def retry_delay(error, attempt):
    delay = random.uniform(0, min(8, 2 ** (attempt + 1)))
    if not isinstance(error, HTTPError):
        return delay
    headers = error.headers or {}
    now = datetime.now(timezone.utc)
    retry_after = headers.get("Retry-After")
    primary = headers.get("X-RateLimit-Remaining") == "0"
    if retry_after is not None:
        try:
            if retry_after.isascii() and retry_after.isdecimal():
                requested = int(retry_after)
            else:
                target = parsedate_to_datetime(retry_after)
                if target.tzinfo is None:
                    return None
                requested = (target - now).total_seconds()
        except (ValueError, TypeError, OverflowError):
            return None
        delay = max(delay, requested)
    if primary:
        try:
            reset = headers.get("X-RateLimit-Reset")
            if not reset or not reset.isascii() or not reset.isdecimal():
                return None
            delay = max(delay, int(reset) - now.timestamp())
        except (KeyError, ValueError, TypeError, OverflowError):
            return None
    if error.code in (403, 429) and retry_after is None and not primary:
        # GitHub requires at least 60 seconds without explicit rate-limit timing.
        # The cumulative sleep budget is only 30 seconds.
        return None
    return delay


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
    manifests = re.findall(r"^<!-- presetlauncher:manifest-v2=([A-Za-z0-9+/=]+)\.([A-Za-z0-9+/=]+) -->\r?$", body, re.M)
    extra_installers = any(item["name"] == installer_name(data["version"], arch)
                           for item in release["assets"] for arch in ARCHITECTURES if arch != "x64")
    if not manifests and not extra_installers and "presetlauncher:manifest-v2" not in body:
        return
    if len(manifests) != 1:
        raise ValueError("Missing or ambiguous previous architecture manifest")
    encoded, signature = manifests[0]
    previous_assets = {arch: installer_asset(release, arch) for arch in ARCHITECTURES}
    message = base64.b64decode(encoded, validate=True)
    verify_message(message, signature)
    if message != manifest_message(data, previous_assets):
        raise ValueError("Previous architecture manifest does not match the release")
    return previous_assets


def manifest_message(data, installers):
    manifest = {key: data[key] for key in ("version", "minimum_supported_version", "policy")}
    manifest["installers"] = [
        {"architecture": arch, "name": installer_name(data["version"], arch),
         "size": installers[arch]["size"], "sha256": installers[arch]["sha256"]}
        for arch in ARCHITECTURES
    ]
    return b"presetlauncher-update-v2\n" + json.dumps(manifest, separators=(",", ":")).encode("ascii")


def signed_update_markers(data, installers):
    x64 = installers["x64"]
    signature = sign(data, x64["size"], x64["sha256"])
    message = manifest_message(data, installers)
    manifest = base64.b64encode(message).decode("ascii") + "." + sign_message(message)
    return (f"<!-- presetlauncher:min-supported={data['minimum_supported_version']} -->\n"
            f"<!-- presetlauncher:update-policy={data['policy']} -->\n"
            f"<!-- presetlauncher:signature-v1={signature} -->\n"
            f"<!-- presetlauncher:manifest-v2={manifest} -->")


def application_notes(decisions, token):
    def clean(text):
        text = re.sub(r"(?i)presetlauncher(?:-app-notes\s*:|\s*:|\s*-summary:).*", "", text)
        return re.sub(r"[<>\x00-\x1f\x7f]", " ", text).strip()

    lines = []
    seen = set()
    for decision in decisions:
        number = decision["number"]
        if decision["rank"] <= 0 or number in seen:
            continue
        seen.add(number)
        pull = request(API + f"/pulls/{number}", token=token)
        if pr_decision(pull) != decision:
            raise ValueError("PR release metadata changed while generating notes")

        body = (pull.get("body") or "").splitlines()
        summary = ""
        if body.count(SUMMARY_BEGIN) == body.count(SUMMARY_END) == 1:
            start, end = body.index(SUMMARY_BEGIN), body.index(SUMMARY_END)
            if end == start + 2:
                candidate = body[start + 1].strip()
                if len(candidate) <= 240 and "<" not in candidate and ">" not in candidate:
                    summary = clean(candidate)
        if not summary:
            summary = clean(pull["title"].split(": ", 1)[1])
        if summary:
            # Publisher summaries are factual text, not a second Markdown document.
            summary = re.sub(r"([\\`*_[\]#])", r"\\\1", summary[:240])
            lines.append(f"- {summary}")

    notes = "\n".join(lines)
    if len(notes.encode("utf-8")) > 48_000 or len(lines) > 500:
        raise ValueError("Application-facing release notes exceed the supported size")
    return f"{APP_NOTES_BEGIN}\n{notes}\n{APP_NOTES_END}"


def validated_installers(data, required_gate=None):
    evidence = json.loads(EVIDENCE.read_text(encoding="utf-8-sig"))
    full = data["validate_installer"]
    if type(full) is not bool or (os.environ["CIRCLE_BRANCH"] == "main" and not full):
        raise ValueError("Invalid Windows validation lane")
    if (data["sha"] != os.environ["CIRCLE_SHA1"] or evidence["sha"] != data["sha"]
            or evidence["version"] != data["version"] or evidence["full"] is not full):
        raise ValueError("Windows evidence does not match the validation plan")
    if evidence["audit"] is not True or evidence["test"] is not True:
        raise ValueError("Dependency audit or regression reports missing")
    targets = list(ARCHITECTURES) if full else ["x64"]
    expected_smoke = {"x64": "native", "x86": "wow64", "arm64": "incompatible-host-rejected"} if full else {}
    if evidence["built"] != targets or evidence["smoke"] != expected_smoke:
        raise ValueError("Required architecture validation missing")
    if not full or required_gate in ("dependency-audit", "test-windows", "build-windows", "smoke-installer"):
        return {}

    names = [installer_name(data["version"], arch) for arch in targets]
    if {path.name for path in OUTPUT.iterdir()} != {*names, "SHA256SUMS.txt"}:
        raise ValueError("Incorrect installer asset set")
    installers = {}
    for arch, name in zip(targets, names):
        path = OUTPUT / name
        size = path.stat().st_size
        if not 0 < size <= 1024**3:
            raise ValueError(f"Invalid installer size: {name}")
        with path.open("rb") as stream:
            installers[arch] = {"size": size, "sha256": hashlib.file_digest(stream, "sha256").hexdigest()}
    expected = [f"{installers[arch]['sha256']}  {name}" for arch, name in zip(targets, names)]
    if ((OUTPUT / "SHA256SUMS.txt").read_text(encoding="ascii").splitlines() != expected
            or evidence["installers"] != installers):
        raise ValueError("Validated installer checksum mismatch")
    return installers


def plan():
    previous = request(API + "/releases/latest")
    if previous["draft"] or previous["prerelease"]:
        raise ValueError("Expected a published stable release")
    if git("rev-parse", "--is-shallow-repository") == "true":
        git("fetch", "--unshallow", "origin")
    # Use the upstream release tag even when validating a contribution from a fork.
    tag = previous["tag_name"]
    version(tag)
    previous_assets = verify_previous(previous)
    git("fetch", f"https://github.com/{REPO}.git", f"refs/tags/{tag}:refs/tags/{tag}")
    sha = git("rev-parse", "HEAD")
    tag_sha = git("rev-parse", f"refs/tags/{tag}^{{commit}}")
    subprocess.run(["git", "merge-base", "--is-ancestor", tag_sha, sha], check=True)
    branch = os.environ.get("CIRCLE_BRANCH", "")
    pr = None if branch == "main" else branch_pr(sha, branch)
    result = make_plan(previous, collect_decisions(previous, sha, branch, pr), sha)
    if branch != "main":
        result["validation_only"] = True
    result["validate_installer"] = validate_installer(branch, pr, sha)
    if result["validate_installer"] and (result["release"] or branch != "main"):
        result["previous_installers"] = previous_assets or {"x64": installer_asset(previous)}
    PLAN.parent.mkdir(parents=True, exist_ok=True)
    PLAN.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(result, indent=2))


def publish():
    data = json.loads(PLAN.read_text(encoding="utf-8"))
    if os.environ.get("CIRCLE_BRANCH") != "main" or git("rev-parse", "HEAD") != data["sha"]:
        raise ValueError("Release publication requires the validated main commit")
    if data.get("validation_only"):
        raise ValueError("PR validation plans cannot be published")
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
    installers = validated_installers(data)
    paths = [OUTPUT / installer_name(value, arch) for arch in ARCHITECTURES]
    paths.append(OUTPUT / "SHA256SUMS.txt")
    markers = signed_update_markers(data, installers)
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
    full_notes = re.sub(r"(?i)presetlauncher-app-notes(?=\s*:)", "presetlauncher&#45;app-notes", notes["body"])
    body = markers + "\n\n" + full_notes + "\n\n" + application_notes(expected["decisions"], token)
    release = request(API + "/releases", "POST",
                      {"tag_name": tag, "target_commitish": data["sha"], "name": f"PresetLauncher {tag}",
                       "body": body, "draft": True, "prerelease": False}, token)
    release_id = release["id"]
    print(f"Draft created: {release_id}. If interrupted, inspect the draft before retrying.")
    for path in paths:
        upload_url = f"https://uploads.github.com/repos/{REPO}/releases/{release_id}/assets?{urlencode({'name': path.name})}"
        asset = request(upload_url, "POST", token=token, file=path)
        with path.open("rb") as stream:
            expected = hashlib.file_digest(stream, "sha256").hexdigest()
        if (asset["name"] != path.name or asset["state"] != "uploaded"
                or asset["size"] != path.stat().st_size or asset["digest"] != f"sha256:{expected}"):
            raise ValueError("GitHub asset verification failed; draft left unpublished")
    # Recheck main and latest before making the draft visible.
    if request(API + "/releases/latest", token=token)["tag_name"] != data["previous"]:
        raise ValueError("Latest release changed during upload; draft left unpublished")
    final_plan = make_plan(previous, collect_decisions(previous, data["sha"], "main"), data["sha"])
    if any(data.get(key) != value for key, value in final_plan.items()):
        raise ValueError("PR metadata changed during upload; draft left unpublished")
    if request(API + "/git/ref/heads/main", token=token)["object"]["sha"] != data["sha"]:
        raise ValueError("Main advanced during upload; draft left unpublished")
    published = request(API + f"/releases/{release_id}", "PATCH",
                        {"draft": False, "make_latest": "true"}, token)
    if published["draft"] or published["tag_name"] != tag:
        raise ValueError("Unexpected publish result")
    print(published["html_url"])


if __name__ == "__main__":
    if len(sys.argv) == 3 and sys.argv[1] == "gate":
        validated_installers(json.loads(PLAN.read_text(encoding="utf-8")), sys.argv[2])
        sys.exit(0)
    if len(sys.argv) != 2 or sys.argv[1] not in ("plan", "publish"):
        sys.exit("usage: release.py plan|publish")
    {"plan": plan, "publish": publish}[sys.argv[1]]()
