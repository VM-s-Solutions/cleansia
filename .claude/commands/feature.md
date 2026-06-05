# /feature — Full-stack feature (alias for /team)

Implement a feature across backend / db / frontend / mobile. This is now an **alias for `/team`** —
the PM coordinates the full cross-stack flow with a reviewer running in parallel with each developer,
which is strictly better than the old single-pass phase script.

## Usage
```
/feature <feature description>
```

## What it does
Behaves exactly like [`/team`](./team.md): invoke the **PM** (`subagent_type: "pm"`), which creates
tickets, locks the contract (`architect` → `db` → `backend`), fans out consumers
(`frontend`/`android`/`ios`), runs reviewer-in-parallel + the `security`/`optimizer`/`qa` gates, flags
owner-only `manual_steps`, and updates the sprint status. See `agents/WAY-OF-WORKING.md` §3.

## Rules
- Cross-stack work is ticketed and traceable in `agents/backlog/` — nothing happens "verbally".
- Do not run owner-only steps; flag them. Do not commit/push unless the owner asks.

## Example
```
/feature Add employee time tracking with start/stop
```
