# AUTO-EXECUTE — DO NOT SUMMARIZE, EXECUTE IMMEDIATELY
TRIGGERS: /report, report, отчет, напиши отчет, сделай отчет
BEHAVIOR: When triggered, do not read, summarize, or explain this file. Execute the steps in this skill immediately.

# Report Skill

When the user runs `/report`, generate a report entry for the latest or given merged/open PR and append it to `Assets/Common/Docs/Reports/report_ivan_stage_2`.
Report Should be written in russian
## Rules

### Report Format

```
## [Short title — what was worked on]
PR: [URL to pull request]
- [What was added or improved, no technical details]
- [Another point]
- [Another point]
```

- Title = short human-readable name of the feature/area worked on (NOT the PR title verbatim, derive it from what was actually done)
- Bullet points = what was done from a product/feature perspective, no class names, no file names, no implementation details
- Reports are appended at the BOTTOM of the file, one after another

## Execution Steps

1. Find current git user:
   - `gh api user --jq '.login'` → get GitHub username

2. Find the latest PR made by this user (merged or open):
   - `gh pr list --author @me --state all --limit 5 --json number,title,url,mergedAt,createdAt` → pick the most recent one (by mergedAt or createdAt)

3. Get PR details to understand what was done:
   - `gh pr view [number] --json title,url,body,commits` → read the PR body and commits
   - Also run `gh pr view [number] --json files` or `gh pr diff [number] --name-only` to see changed files
   - Read 2-4 key source .cs files from the diff to understand the actual work done (skip .meta, .prefab, .unity, .asset)

4. Read the existing report file:
   - Read `Assets/Common/Docs/Reports/report_ivan_stage_2`
   - If the file does not exist, start with empty content

5. Generate the report entry:
   - Title: derive a short, clear feature name from the work (e.g. "Anchor Points for Sprite Frames", "Frame Selection and Outline")
   - PR link: full GitHub URL
   - Bullets: 3-6 points describing what was added/improved from a user/feature perspective
     - Good: "Added ability to place anchor points on individual animation frames"
     - Bad: "Added ObjectAnchorScheme class with serialization support"

6. Append the new report block at the END of the file (after existing content), separated by a blank line if file is not empty

7. Save the file with UTF-8 encoding

8. Report to user: "Report added for PR #[number]: [title]"

## Key Principles

- Language of report: RUSSIAN (the report file is in Russian)
- No technical jargon: no class names, no method names, no file paths in the bullets
- Focus on WHAT changed from the user's perspective: what feature appeared, what was improved, what was fixed
- If a PR body already exists and has a good description, use it as a starting point but rephrase into plain language
- Never overwrite existing report content — only APPEND

## Example Output

Given a PR about anchor points:

```
## Якорные точки для кадров анимации
PR: https://github.com/org/repo/pull/15
- Добавлена возможность задавать якорные точки для каждого кадра спрайтовой анимации
- Якорные точки можно перетаскивать прямо в редакторе на канвасе
- До 8 якорных точек на кадр с возможностью включения и выключения каждой
- Общая инфраструктура для перетаскиваемых элементов вынесена в базовые компоненты
```
