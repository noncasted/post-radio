#!/usr/bin/env python3
"""Merge author/name from _migration playlist metadata into tools/metadata/songs.json by URL."""

import json
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
SOURCE_DIR = REPO_ROOT / "_migration" / "Console" / "Migrations" / "Playlists"
TARGET = REPO_ROOT / "tools" / "metadata" / "songs.json"


def load_lookup() -> dict[str, tuple[str, str]]:
    lookup: dict[str, tuple[str, str]] = {}
    for file in sorted(SOURCE_DIR.glob("*.json")):
        data = json.loads(file.read_text(encoding="utf-8"))
        for url, entry in data.items():
            author = (entry.get("Author") or "").strip()
            name = (entry.get("Name") or "").strip()
            if url and (author or name):
                lookup[url] = (author, name)
    return lookup


def main() -> int:
    if not TARGET.exists():
        print(f"Target not found: {TARGET}", file=sys.stderr)
        return 1

    lookup = load_lookup()
    print(f"Loaded {len(lookup)} entries from {SOURCE_DIR}")

    songs = json.loads(TARGET.read_text(encoding="utf-8"))

    updated = 0
    missed = 0
    unchanged = 0

    for song_id, info in songs.items():
        url = info.get("Url") or ""
        match = lookup.get(url)
        if not match:
            missed += 1
            continue
        author, name = match
        if info.get("Author") == author and info.get("Name") == name:
            unchanged += 1
            continue
        info["Author"] = author
        info["Name"] = name
        updated += 1

    TARGET.write_text(
        json.dumps(songs, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )

    print(f"Updated: {updated}, unchanged: {unchanged}, no match: {missed}, total: {len(songs)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
