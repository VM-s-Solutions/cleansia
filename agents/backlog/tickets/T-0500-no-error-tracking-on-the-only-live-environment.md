---
id: T-0500
title: The only live environment has no error tracking at all — Sentry's DSN is empty and there is no App Insights exporter
status: draft
size: S
owner: architect
created: 2026-08-02
updated: 2026-08-02
depends_on: []
blocks: []
stories: []
adrs: [0015]
layers: [architect, backend]
security_touching: false
manual_steps: []
sprint: 15
---

## Context

**Source: the Azure cost investigation (2026-08-02),** which flagged *"Sentry may be silently
disabled, which would mean the APIs have no error tracking whatsoever."*

### Ground truth — PM-verified first-hand at `master` `0e4ede1b`. Both halves are TRUE, and the
### investigation's framing needs one correction.

**Half 1 — there is no Application Insights exporter on any of the five APIs. Confirmed.**
A grep for `ApplicationInsights` / `AddAzureMonitor` / `UseAzureMonitor` across `Cleansia.Config` and
every `Cleansia.Web*` project returns **zero hits**. The connection string is plumbed through Bicep
and is **inert** — nothing reads it. The investigation is right.

**Half 2 — Sentry is disabled, and it is NOT silent. This is the correction.**

- All five hosts call it: `Cleansia.Web.Partner/Program.cs:16`,
  `Cleansia.Web.Admin/Program.cs:16`, `Cleansia.Web.Customer/Program.cs:16`,
  `Cleansia.Web.Mobile.Customer/Program.cs:16`, `Cleansia.Web.Mobile.Partner/Program.cs:16`.
- The implementation (`Cleansia.ServiceDefaults/Extensions.cs:85-113`) is **correct and deliberate**:
  an absent or blank DSN leaves Sentry uninitialized, with a doc comment explaining that the SDK
  rejects an empty DSN and would otherwise **fail startup**. That is a guard, not an accident.
- **Every committed `appsettings*.json` carries `"Dsn": ""`** — 10 files, all empty.
- The DSN is supplied at deploy from a GitHub secret: `deploy-azure.yml:433` `SENTRY_DSN:
  ${{ secrets.SENTRY_DSN }}` → `:481` `set_secret "Sentry--Dsn" "$SENTRY_DSN"` → Key Vault →
  `main.bicep:467` / `:730` `Sentry__Dsn: kvRef(...)`.
- **And `deploy/AZURE-DEV-RUNBOOK.md:239` says it outright:** *"leave EMPTY for dev (Sentry off); real
  DSN in prod."* `:520` repeats it.

**So Sentry is off on dev BY DESIGN, documented, in a runbook.** Not silent, not a misconfiguration.

### Why this is still a real ticket, and arguably an urgent one

**Because of a fact from the same investigation: prod has never been deployed.** So the "real DSN in
prod" that the design defers to **does not exist yet**, and **DEV is the only live environment** — the
one the owner's iPhone runs against, the one that will be demoed.

Put the two halves together and the honest statement is:

> **The only running instance of this platform has no error tracking of any kind.** No App Insights
> exporter (nothing reads the connection string). No Sentry (DSN empty by documented design). An
> unhandled 500 on DEV right now is visible to nobody unless a human is watching a log stream.

That is not a cost problem and not a bug. It is a **posture that was correct when dev was a scratch
environment and is no longer correct now that dev is the demo environment.** The decision to revisit
it is an architect + owner call, which is why this is `architect`-owned and why AC1 is a question
rather than a fix.

**And it sharpens T-0499:** that ticket lowers Functions log levels to save money. **Lowering
observability on a platform that has none is a worse trade than it looks**, which is why T-0499 AC3
keeps `Exception` excluded from sampling and AC4 demands a stated visibility floor. Recorded on both.

## Acceptance criteria

- [ ] **AC1 — the state is CONFIRMED against the running DEV environment, not just the repo.** Check
      the deployed app settings: is `Sentry__Dsn` empty on the live dev sites? **And is
      `secrets.SENTRY_DSN` populated in GitHub at all?** *(The second is owner-only — it is on the
      owner-decision list.)* Evidence: the app-setting value (redacted to present/absent) plus the
      owner's answer on the secret.
- [ ] **AC2 — the DECISION is made and recorded: does DEV get error tracking?** Three options, each
      priced: **(a)** populate `SENTRY_DSN` and turn Sentry on for dev (Sentry has a free tier; the
      code path already exists and is tested-by-construction — **this is a secret, not a build**);
      **(b)** add a real App Insights exporter to the five APIs — but note **T-0499 exists because
      App Insights telemetry is what is costing money**, so this option *increases* the bill;
      **(c)** accept no error tracking on dev until prod exists, and write that down as a decision
      with a date. Evidence: the ruling with the why-nots.
- [ ] **AC3 — if (a): the DSN's blast radius is stated.** With `TracesSampleRate = 0.2` and
      `AutoSessionTracking = true` (`Extensions.cs:105-107`), turning Sentry on has its own volume and
      its own free-tier ceiling. **And `SendDefaultPii = false` is already set (`:103`) — confirm it
      stays**, because sprint-14's **T-0457** established that this platform writes caller email,
      name, phone and birth date to Information-level logs on all five hosts, and an error tracker
      that ships log context would carry that to a third party. **T-0457 should land first if (a) is
      chosen.** Evidence: the stated volume plus the PII check.
- [ ] **AC4 — the inert App Insights connection string is resolved either way.** If the ruling is not
      (b), then the connection string plumbed through Bicep into five apps is **dead config** that
      reads as working instrumentation to anyone who looks. Either remove it or annotate it in
      `main.bicep`. **This is the exact defect T-0501 documents from the other direction.** Evidence:
      the diff or the annotation.
- [ ] **AC5 — the runbook's dev/prod sentence is updated to match the ruling.**
      `AZURE-DEV-RUNBOOK.md:239` and `:520` currently encode option (c) implicitly. Whatever is
      decided, those two lines say it explicitly, with the reason. Evidence: the diff.
- [ ] **AC6 — no secret value is ever written into the repo, a ticket, or a log.** The owner sets
      GitHub secrets; no agent handles a DSN. Evidence: `git diff` contains no DSN-shaped string.
- [ ] **AC7 (Gate 0.5 leg 3)** — state plainly what was checked in the **repo** versus what was
      checked against the **live environment**, and which claims are the owner's rather than measured.

## Out of scope

- **The Functions host's telemetry cost** — **T-0499**. Related and sequenced against this
  (see AC2 option (b)), but a different file.
- **Setting any GitHub secret or Azure app setting.** Owner-only.
- **Building a new observability stack.** The three options are: use what exists, add the exporter
  the Bicep already assumes, or accept the gap. Nothing new is designed here.
- **The docs correction about telemetry** — **T-0501**, which fixes the *documentation* claiming the
  APIs send telemetry. This ticket decides whether they should.

## Implementation notes

**Architect panel, short.** AC2 is a genuine three-way trade-off and one of the options (b) makes the
bill T-0499 is fixing **worse** — so the two tickets should be ruled together or in a stated order.
The `architect` owns it; the **owner ratifies**, because "we run a demo with no error tracking" is a
risk acceptance, not an engineering default.

**This ticket should run near the front of the sprint regardless of its size.** Not because it is
expensive — it is `S` and option (a) is a secret paste — but because **every other ticket in this
sprint that ships to DEV ships into an environment where its failures are invisible.** It changes what
"green on DEV" means.

**Read first:** `Cleansia.ServiceDefaults/Extensions.cs:80-115`, `deploy/AZURE-DEV-RUNBOOK.md:230-300`
and `:510-530`, `deploy/bicep/main.bicep:426-470`, `.github/workflows/deploy-azure.yml:425-490`, and
sprint-14's **T-0457**.

## Status log
- 2026-08-02 — **draft (created by pm from the Azure cost investigation).** **All of it PM-verified
  first-hand, and the investigation's framing corrected on one point:** Sentry is *not* "silently
  disabled" — the empty-DSN guard is deliberate, documented at `Extensions.cs:87-90`, and
  `AZURE-DEV-RUNBOOK.md:239` explicitly says *"leave EMPTY for dev (Sentry off); real DSN in prod."*
  **The conclusion survives the correction and gets worse:** prod has never been deployed, so the
  "prod" that was supposed to have Sentry does not exist, and **DEV — the demo environment — has no
  error tracking from either source.** Filed `architect`-owned because the fix is a posture decision,
  not a defect.

## Review
