# Benchmarks Docs Agent

Update benchmark documentation after new benchmarks are added.

## Input

You receive: list of newly created benchmark file paths.

## Process

### Step 1 — Read the new benchmark files

For each new file, extract:
- Title (from `public override string Title =>`)
- Group (from `public override string Group =>`)
- MetricName (from `public override string MetricName =>`)
- Payload fields with defaults
- Whether it has a Node class (distributed)
- What Run() does (1-2 sentences)

### Step 2 — Update the group doc

Open the matching doc in `backend/Benchmarks/docs/`:
- State → `STATE.md`
- Messaging → `MESSAGING.md`
- Game → `GAME.md`
- Meta → `META.md`
- Infrastructure → `INFRASTRUCTURE.md`

Add entries following this exact format (match existing entries):

```markdown
### [title from code]
- **File**: `backend/Benchmarks/[Group]/[FileName].cs`
- **Payload**: field1=default1, field2=default2 (or "EmptyPayload")
- **What it measures**: 1-2 sentences describing the operation
- **Distributed**: Yes | No
```

Place new entries in the correct section:
- For STATE.md: under "Throughput Benchmarks (ops/s)" or "Correctness Benchmarks (ms)" depending on MetricName
- For other docs: append after existing benchmarks

### Step 3 — Update INDEX.md

Open `backend/Benchmarks/docs/INDEX.md` and update the benchmark count in the group table:

```markdown
| [Group](GROUP.md) | metric | **new count** | description |
```

### Step 4 — Update feature.md (if exists)

If `backend/Benchmarks/feature.md` exists, add a note about the new benchmarks added.

## Output

Report which doc files were updated.
