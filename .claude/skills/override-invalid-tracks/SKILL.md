---
name: override-invalid-tracks
description: >
  Download unplayable/invalid radio tracks locally via SoundCloud then YouTube,
  then upload them so ImportAudio sets IsLoadingOverridden and Load/redownload
  never overwrite the files. Runs tools/scripts/sync-audio.py.
  Use when the user says /override-invalid-tracks, override invalid tracks,
  sync-audio, import invalid audio, залей инвалидные треки, оверрайд загрузки,
  первый этап, второй этап, upload-only, missing audio dump.
---
# AUTO-EXECUTE — DO NOT SUMMARIZE, EXECUTE IMMEDIATELY
TRIGGERS: /override-invalid-tracks, override invalid tracks, sync-audio, import invalid audio, оверрайд загрузки, залей инвалидные, первый этап, второй этап, upload-only
BEHAVIOR: When triggered, do not read, summarize, or explain this file. Execute the steps in this skill immediately.


# Override Invalid Tracks

Two-stage import of unplayable songs. The dump file is `{id}.mp3` under `audio-dump/` (gitignored). Console upload calls `ImportAudio`, which sets `IsLoadingOverridden = true`.

Do not reinvent download/upload. Run `tools/scripts/sync-audio.py`.

## Arguments

| Invocation | Action |
|---|---|
| `/override-invalid-tracks` or `download` / `первый этап` | Stage 1: download missing from **local** console into `audio-dump/` |
| `upload` / `второй этап` | Stage 2: upload dump files that the **local** console still lists as missing |
| `upload prod` / `залей на прод` / `прод` | Stage 2 against **prod** |
| `all` | Stage 1 local, then stage 2 local |
| `all prod` | Stage 1 local, then stage 2 prod |

If the user names a console URL, use it. Otherwise:

- local = `http://localhost:7103`
- prod = `https://console.post-radio.ru`

Token: `$CONSOLE_TOKEN` or `--token` from the user message. Prod without a token — stop. Do not hardcode a token into the skill or commit it.

## Shared constants

```bash
SCRIPT=tools/scripts/sync-audio.py
DUMP=audio-dump
LOCAL=http://localhost:7103
PROD=https://console.post-radio.ru
```

Requires `python3`, `yt-dlp`, `ffmpeg` for download. Upload does not need yt-dlp if files already exist, but the script still checks for both binaries — if they are missing, install them or stop.

## Stage 1 — download

Fills `audio-dump/{id}.mp3` from `GET /console/media/audio/missing` on the **source** console (local unless the user said otherwise). SoundCloud first, YouTube if SC is DRM/SNIP/404. Skip files already in the dump.

1. Confirm the source console answers:
```bash
curl -s -o /dev/null -w "%{http_code}" "$LOCAL/"
```
If not `200`, start the cluster (`/start-cluster`) or report that the console is down.

2. Run in background, unbuffered, log to `/tmp/sync-audio-download.log`:
```bash
python3 -u tools/scripts/sync-audio.py \
  --console http://localhost:7103 \
  --token "$CONSOLE_TOKEN" \
  --missing-only \
  --skip-existing \
  --sleep 0.2 \
  2>&1 | tee /tmp/sync-audio-download.log
```
Omit `--token` when the env var is empty and local console has no auth.

3. Do not wait by sleeping in a loop. Background the command and report that stage 1 is running. On completion, report `Done: N ok, M failed` and `audio-dump/failed.txt` if present.

DRM on SoundCloud is expected. Weak YouTube matches (`weak youtube match`) are not DRM — they are skipped on purpose so a cooking/challenge video is not imported as the song.

## Stage 2 — upload

Uploads **only** dump files that the **target** console still reports as missing. Do not upload the whole dump: that would mark already-playable SoundCloud files as overridden.

`--missing-only --upload-only` together is wrong — missing-only returns the full missing list and then fails on ids with no local file. Always pass `--ids` of the intersection.

### Gate (prod mandatory, local recommended)

Login and `GET {console}/console/media/audio/missing`. The JSON objects must contain `isLoadingOverridden`. If the field is absent — **stop**. That build will overwrite imports on the next Load audio.

### Intersection

Build the comma-separated id list of missing tracks that have a full `audio-dump/{id}.mp3` (size > 100000, duration ≥ 31s if ffprobe is available). If the list is empty, report dump vs missing counts and stop.

### Upload

```bash
python3 -u tools/scripts/sync-audio.py \
  --console "$TARGET" \
  --token "$CONSOLE_TOKEN" \
  --upload-only \
  --ids "$IDS" \
  --sleep 0.2 \
  2>&1 | tee /tmp/sync-audio-upload.log
```

Background it. On completion:

- `Done: N ok, M failed`
- `GET .../missing` again — count should drop by N (collection can lag a few seconds)
- overridden tracks must not reappear in missing (`isLoadingOverridden` in missing = 0)

## Rules

1. Execute immediately. Do not summarize this file.
2. Never upload to prod until `isLoadingOverridden` is present on the missing payload.
3. Never upload dump files that the target does not list as missing.
4. Import is the lock: `ImportAudio` sets `IsLoadingOverridden`. Do not ask the user to toggle a grain flag by hand.
5. `Load audio` / InvalidTracksRedownload / duration repair must not run against a target mid-upload.
6. Prose to the user in Russian; identifiers in English.
7. Do not commit `audio-dump/` or console tokens.
