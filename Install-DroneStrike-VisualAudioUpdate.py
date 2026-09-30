#!/usr/bin/env python3
"""Safe overlay installer for the DroneStrike visual and audio update.

It downloads the versioned manifest and files directly from GitHub, verifies
every SHA-256 hash before changing the Unity project, and backs up any file it
will replace. Python 3.9+ is the only requirement.
"""

import argparse
import hashlib
import json
import os
from concurrent.futures import ThreadPoolExecutor, as_completed
from datetime import datetime, timezone
from pathlib import Path, PurePosixPath
from shutil import copy2
from urllib.error import HTTPError, URLError
from urllib.parse import quote
from urllib.request import Request, urlopen


DEFAULT_TARGET = Path(r"C:\Users\Folzy\Desktop\DroneStrike")
DEFAULT_REPOSITORY = "folzy1092/Yandex-Game"
DEFAULT_REF = "main"
DEFAULT_MANIFEST = "DroneStrike-Update-Manifest.json"
ALLOWED_TOP_LEVEL = {"Assets", "Documentation", "Tools"}


def get_bytes(url):
    request = Request(url, headers={"User-Agent": "DroneStrike-Update-Installer/1.0"})
    try:
        with urlopen(request, timeout=60) as response:
            return response.read()
    except (HTTPError, URLError, TimeoutError) as error:
        raise RuntimeError("Could not download " + url + ": " + str(error)) from error


def raw_url(repository, ref, path):
    safe_ref = quote(ref, safe="/")
    safe_path = "/".join(quote(part) for part in PurePosixPath(path).parts)
    return "https://raw.githubusercontent.com/{}/{}/{}".format(repository, safe_ref, safe_path)


def checked_relative(path):
    value = PurePosixPath(path)
    if value.is_absolute() or ".." in value.parts or not value.parts:
        raise RuntimeError("Unsafe manifest path: " + path)
    if value.parts[0] not in ALLOWED_TOP_LEVEL:
        raise RuntimeError("Manifest path is outside the overlay scope: " + path)
    return Path(*value.parts)


def load_manifest(repository, ref, manifest_path):
    data = get_bytes(raw_url(repository, ref, manifest_path))
    try:
        manifest = json.loads(data.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as error:
        raise RuntimeError("The downloaded manifest is not valid JSON.") from error

    if manifest.get("schema") != 1:
        raise RuntimeError("Unsupported manifest schema.")
    if manifest.get("repository") != repository:
        raise RuntimeError("Manifest repository does not match --repo.")
    if manifest.get("project_subdir") != "DroneStrike":
        raise RuntimeError("Unexpected Unity project folder in manifest.")
    if not isinstance(manifest.get("files"), list) or not manifest["files"]:
        raise RuntimeError("Manifest contains no files.")
    return manifest


def download_and_verify(repository, ref, files):
    def one(entry):
        if not isinstance(entry, dict):
            raise RuntimeError("Invalid manifest entry.")
        relative = checked_relative(entry.get("path", ""))
        expected_hash = entry.get("sha256", "")
        if len(expected_hash) != 64 or any(c not in "0123456789abcdef" for c in expected_hash):
            raise RuntimeError("Invalid SHA-256 for " + relative.as_posix())

        content = get_bytes(raw_url(repository, ref, "DroneStrike/" + relative.as_posix()))
        actual_hash = hashlib.sha256(content).hexdigest()
        if actual_hash != expected_hash:
            raise RuntimeError(
                "Hash mismatch for {}. Nothing has been installed.".format(relative.as_posix())
            )
        return relative, content

    prepared = []
    with ThreadPoolExecutor(max_workers=6) as executor:
        futures = {executor.submit(one, entry): index
                   for index, entry in enumerate(files, 1)}
        completed = 0
        for future in as_completed(futures):
            prepared.append(future.result())
            completed += 1
            print("Verified {}/{} files".format(completed, len(files)))

    return sorted(prepared, key=lambda item: item[0].as_posix())


def check_target(target):
    if not target.is_dir():
        raise RuntimeError("Unity project folder not found: " + str(target))
    if not (target / "Assets").is_dir() or not (target / "ProjectSettings").is_dir():
        raise RuntimeError(
            "This does not look like a Unity project. Expected Assets and ProjectSettings in "
            + str(target)
        )


def install(target, prepared, create_backup):
    changed = [(relative, content) for relative, content in prepared
               if not (target / relative).is_file()
               or (target / relative).read_bytes() != content]
    if not changed:
        print("Already up to date. No files changed.")
        return

    backup_root = None
    if create_backup:
        timestamp = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%SZ")
        backup_root = target / "_DroneStrike_Update_Backups" / timestamp
        for relative, _ in changed:
            destination = target / relative
            if destination.is_file():
                backup_file = backup_root / relative
                backup_file.parent.mkdir(parents=True, exist_ok=True)
                copy2(destination, backup_file)

    for relative, content in changed:
        destination = target / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        temporary = destination.with_name(destination.name + ".drone-update.tmp")
        temporary.write_bytes(content)
        os.replace(temporary, destination)

    print("Installed {} file(s).".format(len(changed)))
    if backup_root is not None:
        print("Backups: " + str(backup_root))


def main():
    parser = argparse.ArgumentParser(description="Install DroneStrike visual and audio update.")
    parser.add_argument("--target", type=Path, default=DEFAULT_TARGET,
                        help="Unity project directory (default: %(default)s)")
    parser.add_argument("--repo", default=DEFAULT_REPOSITORY,
                        help="GitHub repository in owner/name format")
    parser.add_argument("--ref", default=DEFAULT_REF,
                        help="Git branch, tag, or commit to install from")
    parser.add_argument("--manifest", default=DEFAULT_MANIFEST,
                        help="Manifest path at repository root")
    parser.add_argument("--dry-run", action="store_true",
                        help="Download and verify everything without changing files")
    parser.add_argument("--no-backup", action="store_true",
                        help="Do not save copies of files that are being replaced")
    args = parser.parse_args()

    target = args.target.expanduser().resolve()
    check_target(target)
    manifest = load_manifest(args.repo, args.ref, args.manifest)
    prepared = download_and_verify(args.repo, args.ref, manifest["files"])

    if args.dry_run:
        print("Dry run successful: {} verified file(s), no files changed.".format(len(prepared)))
        return

    install(target, prepared, not args.no_backup)
    print("Open the project in Unity, wait for import, then run:")
    print("Tools > Drone Strike > 1 - Generate Materials")
    print("Tools > Drone Strike > 2 - Build All Missions")


if __name__ == "__main__":
    try:
        main()
    except RuntimeError as error:
        print("UPDATE FAILED: " + str(error))
        raise SystemExit(1)
