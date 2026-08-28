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

# Override Invalid Tracks

Two-stage import of unplayable songs. The dump file is `{id}.mp3` under `audio-dump/` (gitignored). Console upload calls `ImportAudio`, which sets `IsLoadingOverridden = true`.

When the user asks for this workflow, execute the matching stage immediately. Do not reinvent download/upload. Run `tools/scripts/sync-audio.py`.

Policy background: `.codex/docs/VOCABULARY.md` §Audio, `.codex/docs/CLAUDE_MISTAKES.md` lesson 6.

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

3. Do not wait by sleeping in a loop. Background the command. Immediately write the first status table (download format below), then start the 3-minute status schedule for this stage.

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

Background it. Immediately write the first status table (upload format below), then start the 3-minute status schedule for this stage.

When the run finishes (`Done:` in the log): also `GET .../missing` — count should drop by N (collection can lag a few seconds). Overridden tracks must not reappear in missing (`isLoadingOverridden` in missing = 0).

## Status reports (mandatory on both stages)

After starting a background download or upload, always report in this chat:

1. Write the first table immediately (do not wait 3 minutes).
2. Create a recurring scheduled task: interval `3m`, `foreground: true`, `fire_immediately: false`. Cancel any previous override-invalid-tracks status schedule first.
3. Each fire: pgrep the `sync-audio.py` process, parse the stage log, write one table. New FAILED since the last table — second table only.
4. When the log has `Done:` — final table, then `scheduler_delete` that task. If this was stage 1 of `all` / `all prod`, start stage 2 (with a new schedule). Do not keep reporting after Done.

Do not poll with sleep. Do not dump the log. Prose in Russian.

Download log: `/tmp/sync-audio-download.log`. Count `[N/TOTAL]`, `saved `, `FAILED:`, `skip download`, `soundcloud: downloaded`, lines starting `youtube:`.

Upload log: `/tmp/sync-audio-upload.log`. Count `[N/TOTAL]`, `  uploaded `, `FAILED:`, `upload attempt`.

Download table:

```
| Поле | Значение |
|---|---|
| Время | HH:MM |
| Статус | качается / готово / процесс умер |
| Прогресс | N/TOTAL |
| В дампе из списка | X |
| Ошибки | Y |
| SoundCloud / YouTube | A / B |
| Сейчас | author — name #id или Done |
```

Upload table:

```
| Поле | Значение |
|---|---|
| Время | HH:MM |
| Статус | заливается / готово / процесс умер |
| Прогресс | N/TOTAL |
| Залито | X |
| Ошибки | Y |
| Сейчас | author — name #id или Done |
```

The scheduled prompt must be self-contained: stage, log path, TOTAL, how to detect Done, instruction to `scheduler_delete` itself, and the table format. Never put `CONSOLE_TOKEN` in the prompt.

## Rules

1. Execute immediately. Do not summarize this file.
2. Never upload to prod until `isLoadingOverridden` is present on the missing payload.
3. Never upload dump files that the target does not list as missing.
4. Import is the lock: `ImportAudio` sets `IsLoadingOverridden`. Do not ask the user to toggle a grain flag by hand.
5. `Load audio` / InvalidTracksRedownload / duration repair must not run against a target mid-upload.
6. Prose to the user in Russian; identifiers in English.
7. Do not commit `audio-dump/` or console tokens.
8. Every long download or upload gets a 3-minute status table in this session until Done.
