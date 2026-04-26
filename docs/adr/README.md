# Architectural Decision Records (ADR)

Records the **significant decisions** made while building Invest Mate v2. Goal: six months from now, anyone can still answer **"why did we choose A over B?"** — not "what did we change" (that's what `git log` is for).

## When to write an ADR

Write an ADR when the decision meets **at least one** of the following:

1. **Affects ≥ 2 layers** (e.g. a contract change between Domain ↔ Application, or a backend contract change that forces the frontend to follow).
2. **Hard to reverse** — data migration, DB schema change, public API shape change, or a project-wide convention change.
3. **Has a clear trade-off between options** — "A is faster but B is easier to maintain" → record why we picked one.
4. **Goes against the existing default/convention** — e.g. using camelCase somewhere the project is using PascalCase.
5. **The decision will be questioned later** — "why didn't we use library X?", "why is this field nullable?".

## When you don't need an ADR

- Routine bug fixes — the commit message is enough.
- Adding a field/endpoint without changing the existing contract.
- Styling, copy, formatting — usually covered by other docs.
- Trivial decisions (variable naming, picking a small utility lib).

Rule of thumb: *"Six months from now, will anyone ask why we did this?"* Yes → write it. No → skip.

## Format & conventions

- **File name:** `NNNN-kebab-case-title.md` — `NNNN` is a 4-digit zero-padded incrementing number starting from `0001`. Example: `0001-mongodb-pascalcase-fields.md`.
- **Never delete an old ADR** — if a later decision overrides an earlier one:
  - Old ADR → set `Status: Superseded by ADR-NNNN`, but keep the content.
  - New ADR → in `Context`, write `Supersedes ADR-NNNN` and explain why.
- **Language:** English. Keep technical terms as-is.
- **Target length:** about one page (~50–150 lines). Longer than that → split into a plan under `docs/plans/`.
- **Template:** see [template.md](template.md).

## Status field

| Status | Meaning |
|---|---|
| `Proposed` | Under discussion, not yet decided |
| `Accepted` | Decided and implemented |
| `Superseded by ADR-NNNN` | Replaced by a later ADR |
| `Deprecated` | No longer applies, with no replacement (e.g. the feature was removed) |

## Relationship to other artifacts

| Artifact | Purpose | When to use |
|---|---|---|
| `docs/plans/*.md` | **PRD + TDD hybrid** — what/why a feature, how to implement | Each new feature |
| `docs/adr/NNNN-*.md` | **ADR** — why we chose X over Y | When a significant decision is made (see triggers above) |
| `docs/architecture.md` | **Current snapshot** — codebase map | Update whenever the structure changes |
| `git log` + commit message | **What changed** | Every commit |

A single plan can produce **0, 1, or many ADRs**. Plans describe features; ADRs describe decisions.

## Workflow inside the `/ship` skill

The `/ship` skill **prompts automatically** when the plan looks like it has an ADR trigger (schema change, contract change, "choose X over Y" wording, etc.). See `.claude/commands/ship/SKILL.md` for details.
