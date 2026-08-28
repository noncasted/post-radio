#!/usr/bin/env python3
"""Download radio tracks locally and optionally upload them to the console.

SoundCloud progressive/HLS is tried first. Tracks that SoundCloud only serves
as a snipped preview or as Widevine-encrypted HLS are resolved on YouTube —
the same mirror downcloudme uses — then saved as {id}.mp3.
"""

from __future__ import annotations

import argparse
import json
import re
import shutil
import subprocess
import sys
import time
from dataclasses import dataclass
from difflib import SequenceMatcher
from pathlib import Path
from typing import Any
from urllib.error import URLError
from urllib.parse import urljoin, urlparse

import urllib.request
import ssl


REPO_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_SONGS = REPO_ROOT / "tools" / "metadata" / "songs.json"
DEFAULT_OUT = REPO_ROOT / "audio-dump"
MIN_PLAYABLE_MS = 31_000
DURATION_MATCH_S = 8.0


@dataclass
class Track:
    id: int
    author: str
    name: str
    url: str
    duration_ms: int | None = None


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--songs", type=Path, default=DEFAULT_SONGS, help="songs.json lookup")
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT, help="directory for {id}.mp3 files")
    parser.add_argument("--ids", help="comma-separated SoundCloud track ids")
    parser.add_argument("--limit", type=int, default=0, help="stop after N tracks (0 = no limit)")
    parser.add_argument("--console", help="console base URL, e.g. http://localhost:7103")
    parser.add_argument("--token", default="", help="CONSOLE_TOKEN when console auth is enabled")
    parser.add_argument("--insecure", action="store_true", help="skip TLS verification for local console")
    parser.add_argument("--upload", action="store_true", help="upload downloaded files to the console")
    parser.add_argument("--skip-existing", action="store_true",
                        help="do not re-download if {id}.mp3 already exists in --out")
    parser.add_argument("--missing-only", action="store_true",
                        help="take the work list from GET /console/media/audio/missing")
    parser.add_argument("--source", choices=("auto", "soundcloud", "youtube"), default="auto")
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--sleep", type=float, default=1.0, help="pause between tracks, seconds")
    args = parser.parse_args()

    require_bin("yt-dlp")
    require_bin("ffmpeg")

    try:
        session = ConsoleSession(args.console, args.token, args.insecure) if args.console else None
    except ConsoleError as exc:
        print(str(exc), file=sys.stderr)
        return 2

    if args.missing_only and session is None:
        print("--missing-only requires --console", file=sys.stderr)
        return 2

    try:
        tracks = load_tracks(args, session)
    except ConsoleError as exc:
        print(str(exc), file=sys.stderr)
        return 2
    if not tracks:
        print("No tracks to process.")
        return 0

    args.out.mkdir(parents=True, exist_ok=True)
    print(f"{len(tracks)} track(s) -> {args.out}")

    ok = 0
    failed: list[str] = []
    for index, track in enumerate(tracks, start=1):
        label = f"{track.author} - {track.name} #{track.id}"
        dest = args.out / f"{track.id}.mp3"
        print(f"[{index}/{len(tracks)}] {label}")

        try:
            if args.dry_run:
                preview_source(track, args.source)
            else:
                if args.skip_existing and dest.exists() and not is_preview(dest):
                    print(f"  skip download, using {dest}")
                else:
                    if dest.exists() and is_preview(dest):
                        print(f"  existing file is a short preview, re-downloading")
                        dest.unlink()
                    download_track(track, dest, args.source)
                if session is not None and args.upload:
                    session.upload(dest, track.id)
                    print(f"  uploaded {dest.name}")
            ok += 1
        except Exception as exc:
            failed.append(f"{label}: {exc}")
            print(f"  FAILED: {exc}")

        if args.sleep > 0 and index < len(tracks):
            time.sleep(args.sleep)

    print(f"Done: {ok} ok, {len(failed)} failed.")
    for line in failed:
        print(f"  - {line}")
    return 1 if failed else 0


def load_tracks(args: argparse.Namespace, session: ConsoleSession | None) -> list[Track]:
    selected_ids = parse_ids(args.ids)

    if args.missing_only:
        tracks = session.missing()  # type: ignore[union-attr]
        if selected_ids:
            tracks = [track for track in tracks if track.id in selected_ids]
        return tracks[: args.limit] if args.limit else tracks

    songs = json.loads(args.songs.read_text(encoding="utf-8"))
    tracks: list[Track] = []
    items = songs.values() if isinstance(songs, dict) else songs
    for entry in items:
        track_id = int(entry["Id"])
        if selected_ids and track_id not in selected_ids:
            continue
        tracks.append(Track(
            id=track_id,
            author=str(entry.get("Author") or "").strip() or "Unknown",
            name=str(entry.get("Name") or "").strip() or "Untitled",
            url=str(entry.get("Url") or "").strip(),
        ))

    if selected_ids:
        known = {track.id for track in tracks}
        missing = [track_id for track_id in selected_ids if track_id not in known]
        if missing:
            raise SystemExit(f"ids not in {args.songs}: {missing}")

    return tracks[: args.limit] if args.limit else tracks


def parse_ids(raw: str | None) -> list[int]:
    if not raw:
        return []
    return [int(part.strip()) for part in raw.split(",") if part.strip()]


def preview_source(track: Track, source: str) -> None:
    if source in ("auto", "soundcloud") and track.url:
        print(f"  soundcloud: {track.url}")
        if source == "soundcloud":
            return
    match = pick_youtube(track)
    print(f"  youtube: {match['title']} [{match['id']}] {match.get('duration')}s")


def download_track(track: Track, dest: Path, source: str) -> None:
    tmp_dir = dest.parent / f".tmp-{track.id}"
    if tmp_dir.exists():
        shutil.rmtree(tmp_dir)
    tmp_dir.mkdir(parents=True)

    try:
        downloaded: Path | None = None
        if source in ("auto", "soundcloud") and track.url:
            downloaded = try_soundcloud(track, tmp_dir)
            if downloaded is not None and is_preview(downloaded):
                print("  soundcloud: 30s preview, ignoring")
                downloaded = None

        if downloaded is None and source != "soundcloud":
            fill_expected_duration(track)
            match = pick_youtube(track)
            print(f"  youtube: {match['title']} [{match['id']}] {match.get('duration')}s")
            downloaded = download_url(f"https://www.youtube.com/watch?v={match['id']}", tmp_dir)

        if downloaded is None:
            raise RuntimeError("no downloadable source")

        if is_preview(downloaded):
            raise RuntimeError(f"downloaded preview only ({probe_duration_s(downloaded):.1f}s)")

        shutil.move(str(downloaded), dest)
        print(f"  saved {dest} ({dest.stat().st_size} bytes, {probe_duration_s(dest):.1f}s)")
    finally:
        shutil.rmtree(tmp_dir, ignore_errors=True)


def try_soundcloud(track: Track, tmp_dir: Path) -> Path | None:
    try:
        path = download_url(track.url, tmp_dir)
        print("  soundcloud: downloaded")
        return path
    except CommandError as exc:
        text = str(exc).lower()
        if "drm" in text or "format is not available" in text or "http error 404" in text:
            print("  soundcloud: unavailable, falling back to youtube")
            return None
        raise


def pick_youtube(track: Track) -> dict[str, Any]:
    query = f"{track.author} {track.name}".strip()
    payload = yt_dlp_json(["ytsearch8:" + query, "--flat-playlist", "-J"])
    entries = payload.get("entries") or []
    if not entries:
        raise RuntimeError(f"youtube search returned nothing for: {query}")

    expected_s = None
    if track.duration_ms is not None and track.duration_ms >= MIN_PLAYABLE_MS:
        expected_s = track.duration_ms / 1000.0

    ranked: list[tuple[float, dict[str, Any]]] = []
    needle = normalize(f"{track.author} {track.name}")
    title_needle = normalize(track.name)
    for entry in entries:
        title = str(entry.get("title") or "")
        channel = str(entry.get("channel") or entry.get("uploader") or "")
        duration = entry.get("duration")
        score = SequenceMatcher(None, needle, normalize(f"{channel} {title}")).ratio()
        score = max(score, SequenceMatcher(None, title_needle, normalize(title)).ratio())
        if channel.lower().endswith(" - topic") or channel.lower().endswith("topic"):
            score += 0.12
        if expected_s is not None and isinstance(duration, (int, float)):
            delta = abs(float(duration) - expected_s)
            if delta <= DURATION_MATCH_S:
                score += 0.35
            elif delta <= 20:
                score += 0.05
            else:
                score -= 0.8
        ranked.append((score, entry))

    if expected_s is not None:
        close = [
            item for item in ranked
            if isinstance(item[1].get("duration"), (int, float))
            and abs(float(item[1]["duration"]) - expected_s) <= 20
        ]
        if close:
            ranked = close

    ranked.sort(key=lambda item: item[0], reverse=True)
    best_score, best = ranked[0]
    if best_score < 0.42:
        raise RuntimeError(
            f"weak youtube match ({best_score:.2f}): {best.get('title')} [{best.get('id')}]"
        )
    if expected_s is not None and isinstance(best.get("duration"), (int, float)):
        delta = abs(float(best["duration"]) - expected_s)
        if delta > 20:
            raise RuntimeError(
                f"youtube duration {best.get('duration')}s != soundcloud {expected_s:.1f}s "
                f"({best.get('title')} [{best.get('id')}])"
            )
    return best


def fill_expected_duration(track: Track) -> None:
    if track.duration_ms is not None and track.duration_ms >= MIN_PLAYABLE_MS:
        return
    if not track.url:
        return
    try:
        completed = run_checked([
            "yt-dlp",
            "--ignore-no-formats-error",
            "--no-warnings",
            "--print", "%(duration)s",
            track.url,
        ])
        seconds = float((completed.stdout or "").strip())
    except (CommandError, ValueError):
        return
    if seconds * 1000 >= MIN_PLAYABLE_MS:
        track.duration_ms = int(round(seconds * 1000))
        print(f"  soundcloud duration {seconds:.1f}s")


def download_url(url: str, tmp_dir: Path) -> Path:
    output = tmp_dir / "%(id)s.%(ext)s"
    run_checked([
        "yt-dlp",
        "--no-playlist",
        "--no-warnings",
        "-f", "bestaudio/best",
        "-x",
        "--audio-format", "mp3",
        "--audio-quality", "5",
        "-o", str(output),
        url,
    ])
    files = list(tmp_dir.glob("*.mp3"))
    if not files:
        raise RuntimeError(f"yt-dlp produced no mp3 for {url}")
    return files[0]


def yt_dlp_json(args: list[str]) -> dict[str, Any]:
    completed = run_checked(["yt-dlp", "--no-warnings", *args])
    return json.loads(completed.stdout)


def run_checked(command: list[str]) -> subprocess.CompletedProcess[str]:
    completed = subprocess.run(command, capture_output=True, text=True)
    if completed.returncode != 0:
        detail = (completed.stderr or completed.stdout or "").strip()
        raise CommandError(detail or f"command failed: {' '.join(command)}")
    return completed


class CommandError(RuntimeError):
    pass


class ConsoleError(RuntimeError):
    pass


def require_bin(name: str) -> None:
    if shutil.which(name) is None:
        raise SystemExit(f"missing dependency: {name}")


def probe_duration_s(path: Path) -> float:
    completed = subprocess.run(
        [
            "ffprobe",
            "-v", "error",
            "-show_entries", "format=duration",
            "-of", "default=noprint_wrappers=1:nokey=1",
            str(path),
        ],
        capture_output=True,
        text=True,
    )
    try:
        return float((completed.stdout or "").strip())
    except ValueError:
        return 0.0


def is_preview(path: Path) -> bool:
    return probe_duration_s(path) < MIN_PLAYABLE_MS / 1000.0


def normalize(value: str) -> str:
    value = value.casefold()
    value = re.sub(r"[^\w\s]+", " ", value, flags=re.UNICODE)
    return re.sub(r"\s+", " ", value).strip()


class ConsoleSession:
    def __init__(self, base_url: str, token: str, insecure: bool):
        host = urlparse(base_url).hostname or ""
        if host in {"console_host", "console-host"} or host.upper() == "CONSOLE_HOST":
            raise ConsoleError(
                f"'{base_url}' is a placeholder. Local console is http://localhost:7103"
            )

        self.base_url = base_url.rstrip("/") + "/"
        self.token = token
        self.insecure = insecure
        self.opener = self._opener()
        if token:
            self._login()

    def missing(self) -> list[Track]:
        payload = self._json("GET", "console/media/audio/missing")
        tracks: list[Track] = []
        for entry in payload:
            tracks.append(Track(
                id=int(entry["id"]),
                author=str(entry.get("author") or "").strip() or "Unknown",
                name=str(entry.get("name") or "").strip() or "Untitled",
                url=str(entry.get("url") or "").strip(),
                duration_ms=entry.get("durationMs"),
            ))
        return tracks

    def upload(self, path: Path, song_id: int) -> None:
        boundary = "----PostRadioAudioBoundary"
        body = bytearray()
        body.extend(
            f"--{boundary}\r\n"
            f'Content-Disposition: form-data; name="id"\r\n\r\n'
            f"{song_id}\r\n".encode("utf-8")
        )
        body.extend(
            f"--{boundary}\r\n"
            f'Content-Disposition: form-data; name="file"; filename="{path.name}"\r\n'
            f"Content-Type: audio/mpeg\r\n\r\n".encode("utf-8")
        )
        body.extend(path.read_bytes())
        body.extend(f"\r\n--{boundary}--\r\n".encode("utf-8"))

        request = urllib.request.Request(
            urljoin(self.base_url, "console/media/audio/upload"),
            data=bytes(body),
            method="POST",
            headers={"Content-Type": f"multipart/form-data; boundary={boundary}"},
        )
        with self._open(request) as response:
            raw = response.read()
            if response.status >= 300:
                raise ConsoleError(f"upload HTTP {response.status}: {raw[:300]!r}")

    def _login(self) -> None:
        request = urllib.request.Request(
            urljoin(self.base_url, f"login?token={self.token}"),
            method="GET",
        )
        with self._open(request) as response:
            response.read()

    def _json(self, method: str, path: str) -> Any:
        request = urllib.request.Request(urljoin(self.base_url, path), method=method)
        with self._open(request) as response:
            raw = response.read()
            if response.status >= 300:
                raise ConsoleError(f"{method} {path} HTTP {response.status}: {raw[:300]!r}")
            return json.loads(raw.decode("utf-8"))

    def _open(self, request: urllib.request.Request):
        try:
            return self.opener.open(request, timeout=60)
        except URLError as exc:
            raise ConsoleError(
                f"cannot reach console at {self.base_url.rstrip('/')}: {exc.reason}"
            ) from exc

    def _opener(self) -> urllib.request.OpenerDirector:
        handlers: list[urllib.request.BaseHandler] = [urllib.request.HTTPCookieProcessor()]
        if self.insecure:
            ctx = ssl._create_unverified_context()
            handlers.append(urllib.request.HTTPSHandler(context=ctx))
        return urllib.request.build_opener(*handlers)


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except KeyboardInterrupt:
        raise SystemExit(130)
