---
id: T-0502
title: Dev cold start — Always On is off, the existing warm loop does not point at dev, and every deploy restarts 7 sites twice
status: in_review
size: M
owner: architect
created: 2026-08-02
updated: 2026-08-05
depends_on: []
blocks: []
stories: []
adrs: [0015]
layers: [architect, backend]
security_touching: false
manual_steps: [azure-deploy]
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
- 2026-08-05 — **in_review (backend, on the architect's ticket).** **AC1, AC3 and AC4 were already shipped**
  by `2012b014` and are re-verified at file:line. Added this pass: **AC5's refusal recorded in the repo**
  (`AZURE-PROD-POSTURE.md` §1, argued on SKU arithmetic rather than a price that goes stale) and
  `DeployWarmProbeCoverageTests` — which converts AC4's unguarded copy-paste hazard into a test
  (per-host coverage, the SSR-`/` subtlety, dev-vs-slot path agreement, and `alwaysOn` never conditional),
  mutation-proved twice with both artifacts restored byte-identical. **A premise correction that matters for
  sizing: dev is B2 (2 vCPU / 3.5 GB); the S1 / 1.75 GB figure is the authored PROD plan, never deployed.**
  **AC6 holds open** — TTFB needs a deploy and no numbers were invented; note the "before" is likely no
  longer obtainable, since the first post-merge deploy is what applied Always On. **AC2's second half —
  whether 7 always-on processes fit 3.5 GB — is unfalsified, not answered.**
- 2026-08-02 — **draft (created by pm from the cold-start / deploy investigation).** **Four of the
  five findings PM-verified first-hand** at file:line — the `alwaysOn: env == 'prod'` conditional in
  two places, the module default, the B-series slot rejection, and the existing warm-probe machinery
  with its two documented subtleties. **The double restart is RELAYED** and AC3 re-establishes it.
  **AC5 records a deliberate refusal** — slots on dev cost €35–45/month for half the RAM, so the
  expensive option is also the worse one, and that finding is worth more than the fix.

## Review

### Gate 0 — AC1, AC3 and AC4 were ALREADY DONE before this ticket was picked up

`2012b014` (2026-08-02 17:31, on `master`) shipped all three. Re-verified at `c15e295e`:

- **AC1.** `main.bicep:574` (the five-API loop) and `:677` (SSR) both read **`alwaysOn: true`**,
  unconditional. The `env == 'prod'` fence the ticket quotes is gone, and the replacement comment carries
  the €0 sentence the AC asked for (*"It costs nothing on a plan that is already paid for by the hour"*).
- **AC3.** The double restart reproduced and is closed by a **fingerprint gate**, not a path filter
  (`deploy-azure.yml:346-390`). The gate is right for the reason the commit gives: dev is dispatch-only, so
  a path diff means nothing on a button press days after the merge. The fingerprint covers
  `deploy/bicep/**` **plus the three secret-presence flags**, which closes the hole a path filter leaves —
  setting a secret for the first time changes no file but must still re-wire app settings. It fails **open**
  (missing tag / unreadable RG → provision) and stamps the tag only after Bicep succeeds (`:437-441`), so a
  half-applied provision is not recorded as done.
- **AC4.** The warm probe points at dev: `if: inputs.env != 'prod'`, one per web host, 30 attempts × 10 s,
  failing the job if the site never answers. Targets verified at `:703`, `:773`, `:840`, `:907`, `:974`
  (`/health`) and `:1102` (SSR, `/`).

### AC4 — one honest deviation, and what I did about it instead of pretending

The AC said *"reuse the code path; do not write a second warm loop"*. What shipped is a **copy**: six jobs ×
two loops = **12 near-identical shell blocks**. The substance the AC was protecting *was* carried correctly —
the SSR probe hits `/` and the five API probes hit `/health` — but the drift risk the AC named is real and
unguarded.

I did **not** refactor it into a composite action. A GitHub Actions refactor cannot be exercised locally,
the deploy pipeline is the one path where a mistake is expensive, and there is no runtime signal to catch a
regression. Instead I converted the hazard into a **test**, which is the part that can be verified here:

`src/Cleansia.Tests/Configuration/DeployWarmProbeCoverageTests.cs` parses `deploy-azure.yml` and pins

- every one of the six web hosts has a dev warm probe (the set is asserted by name — five-of-six is a hole);
- the SSR probes `/` and every API probes `/health`, with the *reason* in the failure message;
- **the dev loop and the prod slot loop agree on the path for every host** — the exact drift the AC feared;
- (AC1) `main.bicep` sets `alwaysOn` only to `true`, never to a conditional.

**Mutation-proved twice**, each edit reverted immediately and the files confirmed byte-identical to HEAD
afterwards (`git status` clean on `.github/` and `deploy/`):

- `alwaysOn: true` → `alwaysOn: env == 'prod'` → red (*Expected "true", Actual "env == 'prod'"*).
- one API dev probe `/health` → `/` → **two** tests red, including the dev-vs-slot disagreement.

### AC2 — the capacity arithmetic, and a correction to a premise that was handed to me

**The dev plan is B2 — 2 vCPU / 3.5 GB — not S1/1.75 GB.** `main.bicep:33-34` (`appServicePlanSku = 'B2'`,
*"Dev = B2 (ADR-0015 D2 owner override); prod = S1"*) and `weu.dev.bicepparam` (`param appServicePlanSku =
'B2'`). **S1 / 1 vCPU / 1.75 GB is the authored PROD SKU and prod has never been deployed.** Anyone sizing
the only live environment against 1.75 GB is working with half the real memory; `AZURE-PROD-POSTURE.md:43-49`
is where the 1.75 GB figure legitimately lives, and it is explicitly about the prod plan.

So: **7 resident processes on 3.5 GB** (5 APIs + SSR + Functions; no slots on B-series). The arithmetic and
the watch item are recorded at `AZURE-PROD-POSTURE.md:20-24`.

**Not verified, and it cannot be from here: the per-site resident footprint.** That needs a live memory
reading, and there is no telemetry path to me. What I can say is that the change made the plan *more* likely
to be memory-bound, not less, and the observable symptom to watch for is named in the posture doc
(memory-driven recycling → 502/503 + worker restarts). **This is the one place where the AC's warning — a
blanket flag that pushes the plan into paging makes cold start worse — remains unfalsified rather than
answered.** T-0503's EF warm-up pushes marginally the same way (~2 s of model graph resident per host,
already resident once the host serves a request).

### AC5 — slots are explicitly NOT adopted on dev, recorded in the repo

`deploy/AZURE-PROD-POSTURE.md` §1 previously carried only the *technical* reason (*"B-series rejects slot
creation, which is why dev stays false"*), which reads as a limitation to be worked around. The refusal is
now recorded as a refusal, with the arithmetic that makes it decisive rather than a price quote that goes
stale: Standard is the lowest tier that accepts a slot, and **B2 (2 vCPU / 3.5 GB) → S1 (1 vCPU / 1.75 GB)
halves both RAM and CPU** on the plan that already holds 7 always-on processes — paying more for less memory
to fix a memory-sensitive symptom.

**I deliberately did not quote the €35–45/month figure.** I cannot verify a price from the repo, and the SKU
arithmetic is both checkable and non-perishable. That is stated in the doc so the omission reads as a choice.

### AC6 — NOT MEASURED, and it cannot be from here

Time-to-first-byte before/after needs a deploy against the live dev environment. **`manual_steps:
azure-deploy`.** This AC **holds open**. I am flagging rather than fabricating: six invented numbers would be
worse than none, and a cold-start ticket that ships with fake timings is exactly the unfalsifiable outcome
the AC exists to prevent.

Note the sequencing: the first `Deploy to DEV` run after `2012b014` merged is the one that applied Always
On, because the fingerprint tag was absent then. So the "after" state may already be live — the missing
measurement is the **before**, which is no longer obtainable. Realistically AC6 is best discharged as a
*current* cold/warm TTFB reading plus the statement that the host no longer unloads.

### AC7 — no portal change

Everything is in Bicep or the workflow. My own edits: **no net workflow change** (both mutations reverted and
verified), one line in `deploy/bicep/modules/storage.bicep` (a missing queue — see T-0499), and the AC5
paragraph in `AZURE-PROD-POSTURE.md`.

### AC8 — repo vs live, and what could not be verified without a deploy

**Verified in the repo, at file:line:** the `alwaysOn: true` on both web-host module invocations; the module
default and the slot's hardcoded `alwaysOn: false`; the B2 dev SKU in both `main.bicep` and the dev param
file; the fingerprint gate and its fail-open/stamp-after-success behaviour; all six dev warm probes and
their paths; the six prod slot probes and their paths. All now under test.

**NOT verified — needs the live environment:**

- that Always On is actually **applied** in Azure (it needs the provision run; the repo can only assert intent);
- the real per-site memory footprint, hence whether 7 always-on processes fit 3.5 GB (AC2's open half);
- cold/warm TTFB per host (AC6);
- the observed restart count per deploy before/after (AC3's *"before/after restart count from a real deploy"*
  clause — the *mechanism* is verified in the workflow, the *count* is not).

### Gate 0.5 — suites

Baselines re-measured locally at `c15e295e` before any edit: **3129 / 144 / 135**, all exit 0 (the ticket
header's 2295 / 108 / 75 are stale). After: **3146 / 144 / 135**.
