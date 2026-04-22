---
name: gitstat
description: "Collect and display git commit statistics for the current user. Shows commit count, total file/line changes, and .cs-specific changes in a compact table. Use this skill when the user asks for git statistics, commit stats, daily activity summary, line change counts, or wants to know what they (or someone) committed today or over a period."
---

# Git Statistics

Collect git commit statistics and display them in a compact table.

## How it works

1. Determine the **author** — use `git config user.email` to get the current user's email, unless the user specifies a different author.

2. Determine the **date range**:
   - Default: today (`--since="midnight"`)
   - If the user says "yesterday": `--since="yesterday midnight" --until="midnight"`
   - If the user gives a range like "last week" or "March 1-15": convert to `--since` / `--until`
   - If the user says "last N commits": use `-n N` instead of date filters

3. Run these git commands:

```bash
# Commit count
git log --author="<email>" --since="<since>" --until="<until>" --oneline | wc -l

# Per-file stats (all files)
git log --author="<email>" --since="<since>" --until="<until>" --pretty=format: --numstat

# Per-file stats (.cs only) — filter from the same numstat output
```

4. From the numstat output, compute three categories:
   - **All**: all files — count of unique files, sum of added lines, sum of removed lines, diff (added - removed)
   - **.cs**: files matching `*.cs`
   - **.md**: files matching `*.md` that are NOT inside `.claude/`
   - **.claude**: files with paths starting with `.claude/` (including .md files inside it)

5. Output the result using a Unicode box-drawing table. Right-align all numbers.

**Example** (replace values with actual data):

```
Commits: 13 (today)

┌─────────┬───────┬────────┬────────┬────────┐
│ Metric  │ Files │ +Lines │ -Lines │   Diff │
├─────────┼───────┼────────┼────────┼────────┤
│ .cs     │    34 │    895 │    438 │   +457 │
│ .md     │    12 │    340 │     85 │   +255 │
│ .claude │    82 │   9500 │   1200 │  +8300 │
├─────────┼───────┼────────┼────────┼────────┤
│ All     │   151 │  12778 │   2163 │ +10615 │
└─────────┴───────┴────────┴────────┴────────┘
```

Column widths must adapt to the longest value in each column. Right-align numeric cells, left-align the Metric column. The "Commits" line goes above the table with the date range in parentheses.

6. Below the table, list all commits in the range. Use `git log --author="<email>" --since="<since>" --until="<until>" --format="%h %s"` and display as a simple list:

```
Commits:
  e8e46e0 [Tests] Clean up unused imports and fix using statements
  6fd18af [Console] Apply Blazor conventions: injection, early returns
  ee463ca [Infra] Improve ServiceDiscovery with concurrency safety
  ...
```

No extra commentary after the commit list.

## Edge cases

- If there are 0 commits in the range, say "No commits found for <author> <range>." and stop.
- Binary files show up as `-\t-` in numstat — skip them.
- If the user asks for more detail (e.g., "show me the files"), then list the files with per-file +/- stats, grouped by extension.
