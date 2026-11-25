"""Fingerprint tracked inputs that can affect restore, independently of checkout line endings."""
import json
from pathlib import Path
import subprocess


def main():
    index = subprocess.check_output(["git", "ls-files", "--stage", "-z"], text=True, encoding="utf-8")
    inputs = {}
    for entry in index.split("\0"):
        if not entry:
            continue
        metadata, path = entry.split("\t", 1)
        name = path.rsplit("/", 1)[-1].casefold()
        if name.endswith((".csproj", ".fsproj", ".vbproj", ".proj", ".props", ".targets",
                          ".sln", ".slnx", ".pubxml", ".lock.json", ".nupkg")) or name in (
            "global.json", "nuget.config", "packages.config", "config.yml", "dotnet-sdk.sha512"
        ) or path == "Installer/architectures.json":
            inputs[path] = metadata.split()[1]
    if not inputs:
        raise ValueError("No tracked restore inputs found")
    target = Path("artifacts/nuget-inputs.json")
    target.parent.mkdir(exist_ok=True)
    target.write_bytes(json.dumps(inputs, sort_keys=True).encode("utf-8"))


if __name__ == "__main__":
    main()
