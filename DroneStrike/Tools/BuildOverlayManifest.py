#!/usr/bin/env python3
"""Build the GitHub overlay manifest used by Install-DroneStrike-VisualAudioUpdate.py."""

import argparse
import hashlib
import json
import subprocess
from pathlib import Path


REPOSITORY = "folzy1092/Yandex-Game"
PROJECT_PREFIX = "DroneStrike/"
ALLOWED_TOP_LEVEL = ("Assets/", "Documentation/", "Tools/")


def git_lines(root, arguments):
    result = subprocess.run(["git"] + arguments, cwd=root, text=True,
                            stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=True)
    return [line for line in result.stdout.splitlines() if line]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--base", default="main", help="Branch or commit to compare against")
    parser.add_argument("--output", default="DroneStrike-Update-Manifest.json")
    args = parser.parse_args()

    root = Path(__file__).resolve().parents[2]
    changed = set(git_lines(root, ["diff", "--name-only", args.base]))
    changed.update(git_lines(root, ["ls-files", "--others", "--exclude-standard"]))

    files = []
    for repository_path in sorted(changed):
        if not repository_path.startswith(PROJECT_PREFIX):
            continue
        relative = repository_path[len(PROJECT_PREFIX):]
        if not relative.startswith(ALLOWED_TOP_LEVEL):
            continue
        if "/__pycache__/" in relative or relative.endswith(".pyc"):
            continue
        source = root / repository_path
        if not source.is_file():
            continue
        files.append({
            "path": relative.replace("\\", "/"),
            "sha256": hashlib.sha256(source.read_bytes()).hexdigest()
        })

    manifest = {
        "schema": 1,
        "repository": REPOSITORY,
        "project_subdir": "DroneStrike",
        "files": files
    }
    output = root / args.output
    output.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print("Wrote {} file entries to {}".format(len(files), output))


if __name__ == "__main__":
    main()
