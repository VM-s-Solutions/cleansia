---
id: T-0501
title: The infrastructure docs claim all five APIs send telemetry; none of them do
status: draft
size: S
owner: docs
created: 2026-08-02
updated: 2026-08-02
depends_on: [T-0500]
blocks: []
stories: []
adrs: [0015]
layers: [docs]
security_touching: false
manual_steps: []
sprint: 15
---

## Context

**Source: the Azure cost investigation (2026-08-02).** A side finding: *"the infrastructure docs claim
all APIs send telemetry (false)."*

### Ground truth — PM-verified first-hand at `master` `0e4ede1b`

**There is no Application Insights exporter in any of the five API hosts.** A grep for
`ApplicationInsights`, `AddAzureMonitor` and `UseAzureMonitor` across `Cleansia.Config` and every
`Cleansia.Web*` project returns **zero hits**. The connection string is provisioned by Bicep, injected
as an app setting, and **read by nothing**.

**Why a docs ticket is worth filing rather than shrugging at:** this is the same failure class that
cost this backlog real time twice in sprint-14 alone —

- `CLAUDE.md:84-91` documented **six** Nx commands, **none of which run** (T-0462). The sprint doc's
  own note on it: *"a developer who does not stop to investigate could reasonably conclude the build
  is broken rather than the docs — and then start 'fixing' a build that was never broken."*
- Three `*-client-formatter.sh` scripts lacked `set -e` and **always exited 0**, so a green run proved
  nothing (fixed in `d6969fef`).

**This instance is worse than a stale command, because it is load-bearing for an incident.** An
engineer during an outage reads "all APIs send telemetry to Application Insights", goes to the
workspace, finds nothing, and concludes **the APIs are down** — when in fact they were never
instrumented. The document does not just fail to help; it points at a wrong diagnosis at the exact
moment that costs the most.

**And it interacts with the money:** T-0499 is reducing App Insights spend on the Functions host.
Someone reading these docs would reasonably believe the five APIs are also contributing to that bill
and go looking for savings that are not there.

## Acceptance criteria

- [ ] **AC1 — every false telemetry claim is FOUND, not just the one that was reported.** Grep the
      documentation tree (`docs/`, `deploy/*.md`, `agents/architecture/`) for claims about
      Application Insights, telemetry, monitoring and observability on the API hosts. Produce the
      list with file:line **before** editing anything. Evidence: the list.
- [ ] **AC2 — each claim is corrected to the verified state, and the correction is specific.** Not
      "telemetry may be limited" — the true statement is: *the five API hosts have no Application
      Insights exporter; the connection string is provisioned and unread; the Functions host is the
      only App Insights producer.* Evidence: the diff.
- [ ] **AC3 — the Bicep's inert connection string is ANNOTATED where it is provisioned**, so the next
      reader of `main.bicep` does not re-derive this. One comment line, at the setting. **This is the
      same annotation T-0500 AC4 requires — coordinate so it is written once, not twice.** Evidence:
      the diff, or a note that T-0500 already did it.
- [ ] **AC4 — the doc states what error tracking DOES exist**, per **T-0500**'s ruling. If the answer
      is "none on dev", the document says so plainly rather than omitting it. **A document that is
      merely no longer wrong is a wasted edit; it should be right.** Evidence: the paragraph.
- [ ] **AC5 — `CLAUDE.md` is NOT edited by this ticket.** It is owner-gated and a shared-file-lane
      entry (`process/shared-file-lanes.md`). **If a correction is needed there, it is proposed as
      text in `## Review`** for the owner to apply — exactly as T-0462 does for its three
      corrections. Evidence: the proposed text, or "no `CLAUDE.md` change needed".
- [ ] **AC6 — no claim in the corrected text is asserted without having been checked.** Every
      statement about what a host does or does not do carries a file:line or a command. **A docs
      ticket correcting an unverified claim with another unverified claim is the same defect.**
      Evidence: the citations inline or in `## Review`.
- [ ] **AC7 (Gate 0.5 leg 3)** — state which documents were **not** reviewed and why.

## Out of scope

- **Adding an exporter.** That is **T-0500** AC2 option (b). This ticket documents reality; it does
  not change it. **Hence `depends_on: [T-0500]`** — writing "the APIs send no telemetry" the week
  before a ticket makes them send telemetry produces a second wrong document.
- **`CLAUDE.md`** — AC5.
- **The Functions host's telemetry configuration** — **T-0499**.
- **`docs/` build/deploy.** If the VitePress site needs rebuilding, that is `manual_steps: docs-build`
  and it is flagged, not run.

## Implementation notes

**No panel — one-line "no-decision" note:** correcting a factually false statement to a verified one
introduces no behaviour and no decision. **The only decision (does dev get error tracking) belongs to
T-0500**, and this ticket consumes its answer.

**Sequencing:** strictly after T-0500's ruling. It is cheap and it should ride in the same PR as
T-0500 if the timing allows — one reader, one correction, one commit.

**Read first:** `deploy/AZURE-DEV-RUNBOOK.md`, `deploy/AZURE-PROD-POSTURE.md`,
`docs/architecture/*`, `agents/architecture/decisions/azure-deployment.md`, **ADR-0015**.

## Status log
- 2026-08-02 — **draft (created by pm from the Azure cost investigation).** **PM-verified
  first-hand:** zero `ApplicationInsights` / `AddAzureMonitor` / `UseAzureMonitor` references across
  `Cleansia.Config` and all five `Cleansia.Web*` projects. Filed as a real ticket rather than a
  shrug because this backlog has already paid twice for documentation that described a system that
  did not exist (T-0462's six non-running Nx commands; the always-exit-0 formatter scripts), and this
  instance points an incident responder at a wrong diagnosis. **`depends_on: [T-0500]`** so the
  correction is written once against the final posture.

## Review
