# /review — Review the current change

Run the Reviewer (and, when relevant, Security & Optimizer) over the current diff.

## Usage
```
/review                 # review the working changes / current branch diff
/review security        # focus the S1–S10 security gate
/review perf            # focus the performance/cost gate
/review <path>          # review a specific file/area
```

## What it does
Act as the **Reviewer** (`.claude/agents/reviewer.md`). Walk the applicable quality gates
(`agents/process/quality-gates.md`) against the diff, checking the stack catalog
(`agents/knowledge/patterns-*.md`), `conventions.md`, and `security-rules.md`. For a `security` focus,
run the **Security Reviewer** (`.claude/agents/security.md`); for `perf`, the **Optimizer**
(`.claude/agents/optimizer.md`).

Produce a verdict — `APPROVED` or `CHANGES REQUESTED` — with each finding as **file:line + the exact
fix**. Do not write the fixes; request them.

## Rules
- Quote the rule being enforced; be specific and kind.
- Never approve under pressure or to unblock a schedule.

## Example
```
/review src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs
```
