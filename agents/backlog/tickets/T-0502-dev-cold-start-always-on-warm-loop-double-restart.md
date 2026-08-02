---
id: T-0502
title: Dev cold start — Always On is off, the existing warm loop does not point at dev, and every deploy restarts 7 sites twice
status: draft
size: M
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

**Source: the cold-start / deploy investigation (2026-08-02).** Its headline is the useful part:
**the blue-green mechanism the owner described already exists, is written, is tested, and is fenced
behind `if: env == 'prod'`.** Nothing needs to be built. Everything worth doing is **€0**.

### Ground truth — PM-verified first-hand at `master` `0e4ede1b`

**1. Always On is off on dev, by an env conditional.**
`deploy/bicep/main.bicep:571-572`:
```bicep
// Prod (S1) keeps the hosts warm; dev (B2) keeps the cost posture — an idle host may unload.
alwaysOn: env == 'prod'
```
Repeated at `:673`. The module default is `alwaysOn: false`
(`deploy/bicep/modules/appService.bicep:33-34`, doc'd *"off (default) suits dev cost on B2"*).

**That is the "sometimes, without a deploy" case, exactly.** An idle B2 site unloads after ~20
minutes; the next request pays a full cold start. The owner's phone hits DEV; DEV is idle most of the
day.

**2. Always On is FREE on B2.** It is a per-site boolean on a plan the owner is already paying for.
The comment calls it *"the cost posture"* — that is true of the **plan tier**, not of the flag.
Turning it on costs €0 and consumes plan capacity that is already provisioned and idle.

**3. Slots are the expensive option AND the worse one.** `main.bicep:163` — slots need Standard+;
*"B-series rejects slot creation, so dev stays false."* The investigation prices Standard at
**+€35–45/month for half the RAM**. **This ticket does not schedule slots on dev**, and says so
explicitly so the idea does not come back.

**4. The blue-green machinery exists and is documented in detail.** `deploy/AZURE-PROD-POSTURE.md:20-62`
describes deploy-to-slot → warm → swap, including two things a reader would otherwise get wrong:
slots are deliberately **not** Always On (`appService.bicep:106-110` — Always On is slot-sticky, so a
warm slot buys nothing; the workflow warms it explicitly), and the **SSR warm probe hits `/` rather
than `/health` on purpose**. **There is a working warm-probe implementation in this repo already.**

**5. The double restart. RELAYED, not PM-verified.** The investigation: deploys restart **7 sites in
parallel on one 2-core B2, twice**, because the infra step rewrites app settings on every run and an
app-setting write restarts the site. AC3 re-establishes it.

## Acceptance criteria

- [ ] **AC1 — Always On is enabled on the dev web hosts, and the cost claim is stated.** Change the
      conditional at `main.bicep:571-572` and `:673`. The verdict states in one sentence why this is
      €0 (a per-site flag on an already-paid B2 plan) so the comment it replaces — which reads as if
      the flag itself costs money — does not get reinstated by the next reader. Evidence: the diff
      plus the sentence.
- [ ] **AC2 — the plan's capacity is checked before, not after.** 7 sites kept warm on one B2 is a
      memory question. State the B2's memory, the per-site footprint, and whether 7 always-on sites
      fit. **If they do not, Always On goes on a named SUBSET (the hosts the demo actually uses) and
      the subset is justified.** A blanket flag that pushes a 2-core B2 into paging makes cold start
      worse, not better. Evidence: the capacity arithmetic.
- [ ] **AC3 — the double restart is RE-ESTABLISHED, then removed.** Trace the deploy workflow and
      state which step writes app settings, whether it writes them **unconditionally**, and whether
      each write triggers a restart. Then make the write conditional or idempotent. **If the finding
      does not reproduce, say so and close this AC** — a "we could not reproduce it" is a result.
      Evidence: the trace at file:line plus the before/after restart count from a real deploy.
- [ ] **AC4 — the existing warm probe is pointed at dev, reusing the prod implementation.** The
      workflow already warms a slot before swapping (`AZURE-PROD-POSTURE.md:50-62`). Dev has no slot,
      so the probe targets the site directly after deploy. **Reuse the code path; do not write a
      second warm loop.** Two warm implementations drift, and the SSR-hits-`/` subtlety is exactly the
      kind of thing a second implementation gets wrong. Evidence: the diff showing reuse.
- [ ] **AC5 — SLOTS ARE EXPLICITLY NOT ADOPTED ON DEV, with the reasoning recorded in the repo.**
      +€35–45/month for **half the RAM** on the hosts that are cold-starting. Write it into
      `AZURE-PROD-POSTURE.md` or the ADR so the next person costing this out finds the answer instead
      of re-deriving it. **This AC exists because "add slots" is the intuitive fix and it is wrong.**
      Evidence: the recorded reasoning.
- [ ] **AC6 — the improvement is MEASURED.** Time-to-first-byte on a cold dev site before and after,
      three samples each, stated per host. **A cold-start ticket that ships without a timing is
      unfalsifiable.** Evidence: the six numbers.
- [ ] **AC7 — no Azure portal change, no manual `az` command as the fix.** Everything lands in Bicep
      or the workflow, so the next `deploy` does not undo it. Any owner-run step is flagged as
      `manual_steps`, not performed. Evidence: the diff.
- [ ] **AC8 (Gate 0.5 leg 3)** — state which claims were verified in the **repo** and which against
      the **live environment**, and name every one that could not be verified without a deploy.

## Out of scope

- **App-side boot cost** — **T-0503** (the blocking DB call in DI registration, the EF model warm-up).
  Same investigation, different layer, different lane. They compose: this makes the host stay up,
  that makes it come up faster. **Neither substitutes for the other.**
- **Slots on dev.** AC5 records the refusal.
- **Prod.** It has never been deployed. The prod path already has the mechanism; nothing here changes
  it.
- **Changing the plan SKU.**
- **Telemetry cost** — T-0499. Independent.

## Implementation notes

**Architect panel, short — author + 2 challengers + lead.** AC2 is the reason: enabling Always On on
7 sites on a 2-core B2 is a capacity decision that can make the reported symptom **worse**, and that
is the challenge the panel must survive. AC3's restart fix is mechanical once traced.

**`manual_steps` watch:** a Bicep change needs a deployment to take effect. **The PM does not run
deploys.** Whether the owner runs it or CI does on merge must be stated at dispatch, and dependent
measurement (AC6) **holds** until it has run.

**Read first:** `deploy/bicep/main.bicep:560-680`, `deploy/bicep/modules/appService.bicep`,
`deploy/AZURE-PROD-POSTURE.md` in full, `.github/workflows/deploy-azure.yml`, **ADR-0015**.

## Status log
- 2026-08-02 — **draft (created by pm from the cold-start / deploy investigation).** **Four of the
  five findings PM-verified first-hand** at file:line — the `alwaysOn: env == 'prod'` conditional in
  two places, the module default, the B-series slot rejection, and the existing warm-probe machinery
  with its two documented subtleties. **The double restart is RELAYED** and AC3 re-establishes it.
  **AC5 records a deliberate refusal** — slots on dev cost €35–45/month for half the RAM, so the
  expensive option is also the worse one, and that finding is worth more than the fix.

## Review
