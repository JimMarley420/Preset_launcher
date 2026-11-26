"""Submit a verified stable release to the WinGet community catalog."""
import base64
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tarfile
import tempfile
from urllib.error import HTTPError
from urllib.parse import quote, urlencode

import release

PACKAGE = "JimmyMarley.PresetLauncher"
UPSTREAM = "https://api.github.com/repos/microsoft/winget-pkgs"
FORK = "https://api.github.com/repos/JimMarley420/winget-pkgs"
OWNER = "JimMarley420"
ROOT = "manifests/j/JimmyMarley/PresetLauncher"
KOMAC_VERSION = "2.17.2"
KOMAC_SHA256 = "b03b020eec98acfb0b53e284a6e21815b884be33de6785b97e7a120f0e748966"
PRODUCT_CODE = "{C4A7B8D2-E3F5-4901-A2B3-C4D5E6F70819}_is1"


def read(url, token):
    return release.request(url, token=token)


def optional(url, token):
    try:
        return read(url, token)
    except HTTPError as error:
        if error.code != 404:
            raise
        return None


def catalog(token):
    rows = optional(f"{UPSTREAM}/contents/{ROOT}?ref=master", token)
    if rows is None:
        return []
    if not isinstance(rows, list):
        raise ValueError("Unexpected WinGet package directory")
    return sorted(release.version(row["name"]) for row in rows if row["type"] == "dir")


def branch_name(value):
    return f"presetlauncher-winget-{value}"


def existing_submission(value, token):
    head = f"{OWNER}:{branch_name(value)}"
    query = urlencode({"head": head, "base": "master", "state": "all", "per_page": 100})
    own = read(f"{UPSTREAM}/pulls?{query}", token)
    if own:
        if len(own) != 1 or own[0]["head"]["ref"] != branch_name(value):
            raise ValueError("Ambiguous previous WinGet submission")
        if own[0]["state"] == "closed" and not own[0].get("merged_at"):
            raise ValueError(f"Previous submission closed; review {own[0]['html_url']}")
        return own[0]["html_url"]
    # Other contributors may submit the same version. Search is best-effort;
    # deterministic branch identity above is the retry safety boundary.
    q = f'repo:microsoft/winget-pkgs is:pr is:open in:title "{PACKAGE}"'
    result = read("https://api.github.com/search/issues?" + urlencode({"q": q, "per_page": 100}), token)
    if result["incomplete_results"] or result["total_count"] > 100:
        raise ValueError("Incomplete WinGet duplicate search")
    for item in result["items"]:
        if PACKAGE.casefold() in item["title"].casefold() and re.search(
                rf"(?<![\w.]){re.escape(value)}(?![\w.])", item["title"]):
            return item["html_url"]
    return None


def release_for_plan(plan, token):
    if (os.environ.get("CIRCLE_BRANCH") != "main"
            or plan["sha"] != os.environ.get("CIRCLE_SHA1")
            or release.git("rev-parse", "HEAD") != plan["sha"]
            or plan.get("validation_only") or type(plan["release"]) is not bool):
        raise ValueError("WinGet publication requires a main release plan")
    current = read(release.API + "/releases/latest", token)
    expected = plan["tag"] if plan["release"] else plan["previous"]
    if current["tag_name"] != expected:
        print("Release plan superseded; a newer main pipeline handles the catalog.")
        return None
    if current["draft"] or current["prerelease"]:
        raise ValueError("WinGet requires a published stable release")
    if plan["release"] and current["target_commitish"] != plan["sha"]:
        raise ValueError("Published release does not target the planned commit")
    return current


def prepare_komac(directory):
    name = f"komac-{KOMAC_VERSION}-x86_64-unknown-linux-gnu.tar.gz"
    archive = directory / name
    subprocess.run(["curl", "--fail", "--silent", "--show-error", "--location",
                    "--max-time", "120", "--output", str(archive),
                    f"https://github.com/russellbanks/Komac/releases/download/v{KOMAC_VERSION}/{name}"],
                   check=True)
    with archive.open("rb") as stream:
        if hashlib.file_digest(stream, "sha256").hexdigest() != KOMAC_SHA256:
            raise ValueError("Komac archive SHA-256 mismatch")
    binary = directory / "komac"
    with tarfile.open(archive, "r:gz") as tar:
        members = [item for item in tar.getmembers()
                   if item.isfile() and Path(item.name).name == "komac"]
        if len(members) != 1:
            raise ValueError("Unexpected Komac archive layout")
        with tar.extractfile(members[0]) as stream, binary.open("wb") as out:
            out.write(stream.read())
    binary.chmod(0o700)
    return binary


def verify_manifests(documents, value, assets, notes_url):
    types = {}
    for name, data in documents.items():
        if data["PackageIdentifier"] != PACKAGE or str(data["PackageVersion"]) != value:
            raise ValueError("Manifest package or version mismatch")
        kind = data["ManifestType"]
        if kind == "locale":
            expected_name = f"{PACKAGE}.locale.{data['PackageLocale']}.yaml"
        elif kind == "defaultLocale":
            expected_name = f"{PACKAGE}.locale.{data['PackageLocale']}.yaml"
        elif kind == "version":
            expected_name = f"{PACKAGE}.yaml"
        elif kind == "installer":
            expected_name = f"{PACKAGE}.installer.yaml"
        else:
            raise ValueError("Unexpected manifest type")
        if name != expected_name:
            raise ValueError("Unexpected manifest filename")
        if kind != "locale":
            if kind in types:
                raise ValueError("Duplicate manifest type")
            types[kind] = data
    if set(types) != {"version", "installer", "defaultLocale"}:
        raise ValueError("Incomplete WinGet manifest set")
    locale = types["defaultLocale"]
    if (types["version"]["DefaultLocale"] != locale["PackageLocale"]
            or locale.get("ReleaseNotesUrl") != notes_url):
        raise ValueError("Manifest locale or release notes URL mismatch")
    common = types["installer"]
    installers = common["Installers"]
    if len(installers) != len(assets) or {i["Architecture"] for i in installers} != set(assets):
        raise ValueError("Manifest architecture set mismatch")
    for installer in installers:
        arch = installer["Architecture"]
        effective = common | installer
        expected = assets[arch]
        if (installer["InstallerUrl"] != expected["url"]
                or installer["InstallerSha256"].lower() != expected["sha256"]
                or effective.get("InstallerType") != "inno"
                or effective.get("Scope") != "user"
                or effective.get("ElevationRequirement") != "elevatesSelf"):
            raise ValueError(f"Installer identity or metadata mismatch: {arch}")
        entries = effective.get("AppsAndFeaturesEntries", [])
        codes = {entry["ProductCode"] for entry in entries if entry.get("ProductCode")}
        if effective.get("ProductCode"):
            codes.add(effective["ProductCode"])
        if (codes != {PRODUCT_CODE}
                or any(str(entry.get("DisplayVersion", value)) != value
                       or entry.get("DisplayName", "PresetLauncher") != "PresetLauncher"
                       or entry.get("Publisher", "JimmyMarley") != "JimmyMarley"
                       for entry in entries)):
            raise ValueError(f"Installer registration mismatch: {arch}")


def user_scope_only(manifest):
    # Inno Setup exposes an optional elevation dialog. Komac detects both
    # scopes, but the catalog package deliberately supports only user installs.
    selected = []
    for entry in manifest["Installers"]:
        scope = entry.get("Scope", manifest.get("Scope"))
        if scope not in ("user", "machine"):
            raise ValueError("Unknown generated installer scope")
        if scope == "user":
            selected.append({key: value for key, value in entry.items() if key != "Scope"})
    return manifest | {"Scope": "user", "Installers": selected}


def generate(directory, value, assets, notes_url, token):
    # Pin both generator and YAML parser; no tooling is downloaded for no-op runs.
    subprocess.run([sys.executable, "-m", "pip", "install", "--disable-pip-version-check",
                    "--require-hashes", "--only-binary=:all:", "--no-deps",
                    "-r", ".circleci/winget-requirements.txt"], check=True)
    import yaml
    binary = prepare_komac(directory)
    output = directory / "generated"
    env = dict(os.environ, GITHUB_TOKEN=token)
    # Do not pass release, website, or signing credentials to the generator.
    for key in ("GH_RELEASE_TOKEN", "GH_PAGES_TOKEN", "UPDATE_SIGNING_KEY_B64", "WINGET_GITHUB_TOKEN"):
        env.pop(key, None)
    subprocess.run([str(binary), "update", PACKAGE, "--version", value, "--urls",
                    *(f"{asset['url']}|{arch}" for arch, asset in assets.items()),
                    "--release-notes-url", notes_url, "--exit", "--output", str(output),
                    "--concurrent-downloads", "3"], env=env, stdin=subprocess.DEVNULL,
                   timeout=600, check=True)
    root = output / ROOT / value
    files = {p.name: p.read_text(encoding="utf-8") for p in root.glob("*.yaml")}
    documents = {name: yaml.safe_load(data) for name, data in files.items()}
    installer_name = f"{PACKAGE}.installer.yaml"
    documents[installer_name] = user_scope_only(documents[installer_name])
    verify_manifests(documents, value, assets, notes_url)
    # Keep the schema/tool headers while serializing the filtered installer.
    original = files[installer_name]
    header = "\n".join(line for line in original.splitlines() if line.startswith("#"))
    files[installer_name] = header + "\n\n" + yaml.safe_dump(
        documents[installer_name], sort_keys=False, allow_unicode=True)
    return files


def submit(value, files, token):
    # The token must belong to the owner of the existing catalog fork.
    user = read("https://api.github.com/user", token)
    fork = read(FORK, token)
    if user["login"] != OWNER or fork["parent"]["full_name"] != "microsoft/winget-pkgs":
        raise ValueError("WinGet token or fork does not match the publisher")
    ref = branch_name(value)
    ref_url = f"{FORK}/git/ref/heads/{quote(ref, safe='/')}"
    existing = optional(ref_url, token)
    if existing is None:
        parent = read(UPSTREAM + "/git/ref/heads/master", token)["object"]["sha"]
        tree = read(UPSTREAM + "/git/commits/" + parent, token)["tree"]["sha"]
        entries = [{"path": f"{ROOT}/{value}/{name}", "mode": "100644",
                    "type": "blob", "content": data} for name, data in sorted(files.items())]
        tree = release.request(FORK + "/git/trees", "POST",
                               {"base_tree": tree, "tree": entries}, token)["sha"]
        commit = release.request(FORK + "/git/commits", "POST",
                                 {"message": f"Update: {PACKAGE} to {value}",
                                  "tree": tree, "parents": [parent]}, token)["sha"]
        release.request(FORK + "/git/refs", "POST",
                        {"ref": f"refs/heads/{ref}", "sha": commit}, token)
    # An interrupted submission may have created the branch. Never overwrite it.
    # Verify its complete diff and file bytes before opening the PR.
    comparison = read(f"{UPSTREAM}/compare/master...{OWNER}:{quote(ref, safe='/')}", token)
    expected = {f"{ROOT}/{value}/{name}" for name in files}
    changed = comparison["files"]
    if ({item["filename"] for item in changed} != expected
            or any(item["status"] != "added" for item in changed)
            or comparison["ahead_by"] != 1):
        raise ValueError("Existing WinGet branch contains unexpected changes")
    for name, data in files.items():
        url = f"{FORK}/contents/{ROOT}/{value}/{name}?" + urlencode({"ref": ref})
        blob = read(url, token)
        if blob["encoding"] != "base64" or base64.b64decode(blob["content"]) != data.encode("utf-8"):
            raise ValueError("Existing WinGet branch content mismatch")
    duplicate = existing_submission(value, token)
    if duplicate:
        return duplicate
    if any(v >= release.version(value) for v in catalog(token)):
        print("Catalog already contains this version or a newer one.")
        return None
    pr = release.request(UPSTREAM + "/pulls", "POST",
                         {"title": f"Update: {PACKAGE} to {value}",
                          "head": f"{OWNER}:{ref}", "base": "master",
                          "body": f"Update {PACKAGE} to {value} using the published PresetLauncher release. "
                                  "The manifest installer URLs and SHA-256 digests are checked against signed release metadata.",
                          "maintainer_can_modify": True}, token)
    return pr["html_url"]


def main():
    plan = json.loads(release.PLAN.read_text(encoding="utf-8"))
    token = os.environ.get("WINGET_GITHUB_TOKEN", "").strip()
    if not token:
        raise ValueError("WINGET_GITHUB_TOKEN is required in the restricted WinGet context")
    current = release_for_plan(plan, token)
    if current is None:
        return
    value = release.format_version(release.version(current["tag_name"]))
    versions = catalog(token)
    if not versions:
        print("Initial package is not in the WinGet catalog; no update submitted.")
        return
    if max(versions) >= release.version(value):
        print("WinGet already contains this version or a newer version.")
        return
    duplicate = existing_submission(value, token)
    if duplicate:
        print(f"Existing WinGet submission: {duplicate}")
        return
    assets = release.verify_previous(current)
    if not assets or set(assets) != set(release.ARCHITECTURES):
        raise ValueError("WinGet publication requires all three signed installer assets")
    notes_url = f"https://github.com/{release.REPO}/releases/tag/{current['tag_name']}"
    with tempfile.TemporaryDirectory() as temp:
        files = generate(Path(temp), value, assets, notes_url, token)
        # Check again after downloading and inspecting the binaries.
        if release_for_plan(plan, token) is None:
            return
        result = submit(value, files, token)
        if result:
            print(f"WinGet pull request: {result}")


if __name__ == "__main__":
    main()
