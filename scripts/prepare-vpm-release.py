"""Select the commit to publish, incrementing first-party packages on a main push."""

import argparse
import json
import re
import subprocess
from pathlib import Path

MARKER = "VPM-Source: "


def git(*args):
    return subprocess.check_output(["git", *args], text=True).strip()


def bump_packages():
    changes = {}
    for path in sorted(Path("Packages").glob("*/package.json")):
        manifest = json.loads(path.read_text(encoding="utf-8"))
        if not manifest["name"].startswith("com.s4na."):
            continue
        version = re.fullmatch(r"(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)", manifest["version"])
        if not version:
            raise ValueError(f"{path}: expected a stable MAJOR.MINOR.PATCH version")
        major, minor, patch = version.groups()
        manifest["version"] = f"{major}.{minor}.{int(patch) + 1}"
        changes[path] = json.dumps(manifest, indent=2, ensure_ascii=False) + "\n"

    if not changes:
        raise ValueError("No first-party VPM packages found")
    for path, content in changes.items():
        path.write_text(content, encoding="utf-8", newline="\n")
    return changes


def publish_commit(source):
    if source and not re.fullmatch(r"[0-9a-f]{40}", source):
        raise ValueError("Expected a main push commit SHA")

    for attempt in range(5):
        try:
            return prepare(source)
        except MainAdvanced:
            if attempt == 4:
                raise


class MainAdvanced(Exception):
    pass


def prepare(source):
    tip = git("rev-parse", "HEAD")
    history = ("log", "--first-parent", "-1", "--format=%H")
    latest = git(*history, f"--grep=^{MARKER}[0-9a-f]{{40}}$", "--extended-regexp")
    if not source:
        return latest or tip

    git("merge-base", "--is-ancestor", source, tip)

    if git(*history, f"--grep=^{MARKER}{source}$"):
        # Do not roll Pages back if a later release is already in main.
        return latest

    changes = bump_packages()
    git("add", "--", *[str(path) for path in changes])
    git("-c", "user.name=github-actions[bot]", "-c",
        "user.email=41898282+github-actions[bot]@users.noreply.github.com",
        "commit", "-m", "chore: bump VPM package patch versions", "-m", MARKER + source)
    try:
        git("push", "origin", "HEAD:refs/heads/main")
    except subprocess.CalledProcessError as error:
        git("fetch", "--no-tags", "origin", "refs/heads/main")
        if git("rev-parse", "FETCH_HEAD") == tip:
            raise error
        # Another merge can update main while this job is preparing its commit.
        # Restart from the new tip without overwriting remote history.
        git("checkout", "--detach", "FETCH_HEAD")
        raise MainAdvanced("main advanced during release preparation") from error
    return git("rev-parse", "HEAD")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--source")
    print(publish_commit(parser.parse_args().source))
