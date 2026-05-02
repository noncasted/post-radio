---
name: audit
description: "Audit and improve parts of the .claude configuration folder — agents, skills, rules, docs, CLAUDE.md. Researches best practices online for the specific domain being audited, then finds inconsistencies, errors, and improvement opportunities. Use this skill when the user says /audit, asks to review or improve their .claude setup, mentions 'audit agents', 'improve my rules', 'check my skill', or wants to optimize any part of the .claude folder."
---
# AUTO-EXECUTE — DO NOT SUMMARIZE, EXECUTE IMMEDIATELY
TRIGGERS: /audit, audit, аудит, проверь мои скиллы, проверь мои агенты
BEHAVIOR: When triggered, do not read, summarize, or explain this file. Execute the steps in this skill immediately.


# Audit Skill

Audits parts of the `.claude` folder by researching domain-specific best practices and then comparing the current state against them.

## Arguments

- `/audit agents` — audit agent definitions in `.claude/agents/`
- `/audit skills` — audit all skills in `.claude/skills/`
- `/audit skills/check` — audit a specific skill
- `/audit docs` — audit docs in `.claude/docs/`
- `/audit CLAUDE.md` — audit the main CLAUDE.md
- `/audit all` — full audit (takes a while)

If no argument — ask user what to audit.

## How It Works

Three phases must run in order.

### Phase 1: Understand the Domain

Read target files, figure out **what domain** you're actually auditing. This determines what to search for.

The same files can serve different purposes depending on context:

| Target | Domain | What to research |
|--------|--------|-----------------|
| `.claude/agents/` as files | AI agent prompt engineering | Effective agent system prompts, tool descriptions, few-shot examples |
| `.claude/agents/` as validators (from `/check`) | Static analysis / linting design | Effective code validators, lint rule quality, false positive rates |
| `.claude/skills/commit/` | Git commit workflow automation | Conventional commits, commit message best practices |
| `.claude/skills/check/` | Code review automation | Automated code review systems, checks that catch real bugs |
| `.claude/docs/COMMON_LIFETIMES.md` | Resource management documentation | Documenting ownership/lifetime patterns, reactive system pitfalls |
| `.claude/docs/COMMON_ORLEANS.md` | Distributed systems documentation | Orleans grain best practices, actor model anti-patterns |
| `.claude/docs/DEPLOY.md` | Deploy/DevOps documentation | Aspire + docker-compose patterns, Coolify best practices |
| `.claude/docs/BLAZOR.md` | Blazor component documentation | Blazor Server/WASM best practices, state management |
| `.claude/CLAUDE.md` | Claude Code configuration | CLAUDE.md structure, prompt hierarchy, what goes where |

Key insight: **don't audit the container, audit the content**. Agent files are prompts — audit them as prompts. A skill that automates commits — audit the commit workflow. Rules about Orleans — audit whether the Orleans guidance is correct and complete.

To identify the domain:
1. Read all target files.
2. For each, determine: what is this file trying to accomplish? What subject matter expertise is needed?
3. Group files by domain if auditing a folder.

Output a brief domain summary before Phase 2.

### Phase 2: Research Best Practices

Search the internet for best practices, common pitfalls, expert recommendations **specific to the identified domain**. This is what makes the audit valuable.

Use WebSearch:
1. **Best practices** — 2-3 searches with different angles.
2. **Common mistakes** people make in this domain.
3. **Expert recommendations** from authoritative sources.

Search strategy by domain:

**Prompt/agent engineering:**
- Recent prompt engineering guides, system prompt best practices.
- Agent design patterns, tool-use strategies.
- Common anti-patterns in LLM instructions.

**Code workflow automation (commits, PRs, checks):**
- Specific workflow best practices (e.g., "conventional commits best practices 2025").
- What similar tools do (how other linters organize rules).
- Metrics on what catches bugs vs noise.

**Technical documentation (rules, docs):**
- Documentation best practices for the technology (Orleans, Aspire, Blazor, reactive).
- Technology's own official best practices — verify rules are correct.
- Common misconceptions about the technology.

**CLAUDE.md / configuration:**
- Claude Code CLAUDE.md best practices, project instruction patterns.
- Prompt hierarchy and context management strategies.

Compile a best-practices checklist specific to the domain. Write it explicitly before Phase 3.

### Phase 3: Audit

Go through each target file against:
1. Best practices checklist from Phase 2.
2. Internal consistency — do files contradict each other? Reference things that don't exist?
3. Completeness — obvious gaps? Coverage matches project needs?
4. Accuracy — for technical rules, is guidance actually correct? Cross-reference with project code.
5. Effectiveness — based on research, will this achieve its goal?

Categorize each finding:
- **Error** — factually wrong, causes problems (e.g., rule references a class that doesn't exist).
- **Gap** — important thing missing.
- **Improvement** — works but could be better.
- **Style** — minor formatting/consistency issues.

## Output Format

```
## Audit: [target]
Domain: [identified domain]

### Research Summary
[2-3 key insights from best practices research most relevant]

### Findings

#### Errors
> `file.md` — [description]
> Evidence: [what's wrong and why]
> Fix: [specific fix]

#### Gaps
> [what's missing]
> Why it matters: [based on research]
> Suggestion: [how to add it]

#### Improvements
> `file.md` — [what could be better]
> Best practice: [what research says]
> Suggestion: [specific improvement]

#### Style
> [minor issues]

### Summary
Errors: N | Gaps: N | Improvements: N | Style: N

### Top 3 Recommendations
1. [highest impact]
2. [second]
3. [third]
```

## Rules

1. **Always research first** — audit value comes from external knowledge, not pattern matching.
2. **Domain-specific searches** — "best practices for X" where X is the actual domain.
3. **Cite sources** — "per Orleans documentation...", "common pattern in prompt engineering...".
4. **Cross-reference with project** — if a rule says "always do X", grep codebase. Rules not matching reality are findings.
5. **Russian prose** for report, English for code and technical terms.
6. **Don't break CLAUDE.md keyword lookup table** — it's load-bearing.
7. **Prioritize actionable findings** — "change line 42 from X to Y because Z" beats "this could be better".
8. **Repo context** — post-radio is backend-only (.NET 10 / Orleans / Aspire / Blazor / Coolify). Ignore references to Unity / VContainer / UniTask — those belong to another project.
