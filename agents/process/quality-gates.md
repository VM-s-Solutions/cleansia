# Quality Gates

A change does not reach `done` until every **applicable** gate passes. The Reviewer enforces gates
1–2 and 6–7 on every ticket; Security (gate 3), Architecture (gate 4), and Optimizer (gate 5) are
conditional. The PM will not merge until the gates that apply are green.

These gates exist because this platform is going to production and is already large. A bug or a
leak that ships now is expensive to undo later. The bar is "would I let this run unattended in
production handling real customers and real money" — not "does it compile."

---

## The gates

### Gate 0 — Evidence discipline (every reviewing/finding agent: reviewer, security, optimizer, qa, and any ad-hoc audit/exploration)

This is a **meta-gate**: it governs *how* every finding from every other gate is reported. It exists
because automated finders **systematically over-report** — they pattern-match to a scary scenario and
assert a defect without tracing the guard that already prevents it. This was observed on this very
codebase: agents reported "the tree won't build" on a transient mid-flight state, and security/review
passes flagged "bugs" that an existing `[Permission]` gate, idempotency key, or query filter already
prevented. **A finder that emits confident false findings is worse than no finder, because its output
gets trusted — and you may "fix" working code and introduce a real bug.**

Therefore, every reported finding MUST satisfy ALL of:

1. **REFUTED by default.** Treat your own hypothesis as false until you have *traced it through the
   actual code*. If you cannot complete the trace, report it as a **question** ("is X guarded?"), not
   a finding.
2. **File:line evidence.** Cite the exact location of the defect AND the location of the guard you
   confirmed is missing/insufficient. "Could happen if…" with no traced path is not a finding.
3. **Concrete trigger.** State the exact input/sequence/request that reaches the bug. If you can't
   describe the repro, you haven't confirmed it.
4. **Guard check (most "bugs" die here).** Before reporting, look for the guard that already prevents
   it. In this codebase the guard menu is: a `[Permission]`/`Policy.*` authz attribute; a deterministic
   **idempotency key** / `ProcessedMessage` / `DbIdempotencyGuard` claim; a **FluentValidation** rule
   (every `*Command` has a Validator, `Cascade.Stop`); an EF **query filter** (tenant scoping) or DB
   **constraint** (unique index, FK Restrict); a **rate-limit window** (`[EnableRateLimiting]`); the
   **UnitOfWork** commit-only-on-success pipeline; a domain **state-transition guard**
   (`CanTransitionTo`); or a config/options default. If a guard exists, the finding is **REFUTED** —
   say so and move on.
5. **Severity honesty.** A blocker = exploitable / money-losing / illegal-state in production *as
   written, reachable today* — not "in a hypothetical future topology." Downgrade or refute everything
   else. (A genuine *latent* multi-tenant/go-live blocker is real, but label it as such — dormant on
   the current single-tenant path, blocking before that capability ships — not as a live crash.)

When the orchestrator consumes finder output, the posture is **verify before acting** — never "fix" on
an unverified finding. A clean area reported honestly ("traced X/Y/Z, no defect, guard at file:line")
is a valid, valuable result; manufacturing findings to look thorough is the failure mode this gate
prevents. This is the report-side complement to the build-side **verify-not-trust** rule in Gate 8.

### Gate 1 — Conventions self-check (always)
The change conforms to [`../knowledge/conventions.md`](../knowledge/conventions.md) and the
relevant stack catalog (`patterns-backend.md` / `patterns-frontend.md` / `patterns-mobile.md`).
Concretely:
- **No hardcoded user-facing strings.** Backend errors use `BusinessErrorMessage` codes; frontend
  uses `TranslatePipe` keys; Android/iOS use string resources. Every new error key exists in all
  5 locales (en, cs, sk, uk, ru).
- **No `any` in TypeScript. No `dynamic` in C#.** Proper types and enums.
- **Architecture obeyed**: CQRS structure intact, facades delegate, no business logic in
  components/composables, no raw HTML form controls.
- **No magic numbers/strings** — constants in the right home (Policy class, enum, theme).
- **Naming + file layout** match the canonical tables.

### Gate 2 — Acceptance criteria (always)
Every AC item in the ticket has **verifiable evidence**: an automated test, a screenshot from the
running app, a log line, or an explicit reviewer confirmation tied to a file:line. "Looks done" is
not evidence. An AC with no evidence fails the gate.

### Gate 3 — Security (mandatory iff `security_touching: true`)
The Security Reviewer walks [`../knowledge/security-rules.md`](../knowledge/security-rules.md)
(S1–S10) against the diff. A ticket is **security-touching** if it adds/changes any of: an
endpoint, auth/authorization, a resource-by-id operation, a response DTO, tenancy scoping, a
side-effecting command (payment, email, loyalty, referral, invoice), file upload, logging of user
data, or rate-limited routes. The verdict names the **specific risk**, not a category — e.g.
"customer A can read customer B's order because the handler doesn't check ownership at
`GetOrderById.cs:31`", not "missing authorization".

### Gate 4 — Architecture (mandatory iff a new pattern or extension point is touched)
The Architect confirms the change preserves the seams: no country branching in handlers (read
`CountryConfiguration`), no provider-specific code outside its adapter, no infra leaking into
`Core.Domain`/`Core.AppServices`, fiscal enforcement modes respected. If the change needed a new
pattern, an ADR exists and is cited.

### Gate 5 — Performance, cost & runtime readiness (for hot paths, external calls, jobs, heavy UI)
The Optimizer checks: no N+1 queries, `AsNoTracking()` on read paths, indexes for new
WHERE/ORDER/JOIN columns, no over-fetching DTOs, OnPush + trackBy on lists, no bundle bloat from a
heavy import, no needless re-renders/recompositions. **Plus runtime readiness** per
[`../knowledge/runtime-readiness.md`](../knowledge/runtime-readiness.md) when the change touches an
external service, a queue/Function, or a hot path: structured logging + correlation id, error
classification, **graceful degradation** (core action not blocked by a non-core dependency outage),
durable side effects, idempotency, and a visible dead-end for failures. Applied when the ticket
touches a list view, a paged query, a hot endpoint, an external integration, a background job, or
adds a dependency.

### Gate 6 — Tests, written test-first (always, proportional to risk) — per [`../knowledge/testing.md`](../knowledge/testing.md)
- Development is **TDD by default**: the test is written **before** the implementation (red → green →
  refactor). For **pure logic** (pricing, pay calc + override precedence, validators, state machines,
  fiscal-mode selection, numbering, refunds) this is **strict and mandatory** — the Reviewer expects
  the test to predate the code (commit order / status-log "red→green"), and **rejects after-the-fact
  tests on pure logic** (they miss the branches the author didn't think of). Money math and state
  transitions are non-negotiable.
- Handlers: the unit test (mocked repos, asserting `IsSuccess` + each `Error.Code`) and the route
  integration test (incl. the auth/ownership rejection) are written against the intended contract
  first. UI: the **facade/ViewModel** test is written first; the view follows.
- Changing existing **untested** code: write a **characterization test** pinning current behavior
  first, then TDD the change.
- New **endpoints** have an integration test covering the happy path and the key failure
  (auth/ownership rejection — a real test, not just review).
- The change covers its slice of the **must-cover list** in `testing.md` (pay calc, order lifecycle,
  money/refunds, fiscal modes, authz boundaries, idempotency, every `BusinessErrorMessage` path).
- Cross-layer behavior has an integration test where the project's harness supports it.
- The QA test plan exists and was executed; results recorded.

### Gate 6.5 — Behavioral non-stub (when the AC assert behavior)
A green suite proves nothing if it would stay green with the feature deleted. This gate exists because
it happened: a "spine" ticket shipped with a green suite whose tests never exercised the real path — the
implementation could have been a no-op and nothing would have gone red. For any ticket whose AC assert
**behavior** — auth decisions, money math, state transitions, and anything named *spine / foundation /
middleware / skeleton* (tickets whose whole point is that the real path works) — at least one test must
**FAIL if the implementation body is replaced with the empty/default value** (return default, no-op,
empty collection). The reviewer **names that test** in the verdict (e.g. "Gate 6.5:
`RefundKey_DoubleSubmit_SingleStripeCall` goes red against a no-op seam"). If no such test can be named,
the suite is asserting the scaffolding, not the behavior — the gate fails, however green the run. The
cheap mental check: *delete the method body — does anything go red?* Routing flags these tickets up
front (`routing.md` §"Spine tickets gate harder") so the dev writes to this gate, not just past it.

### Gate 7 — Contract & docs parity (when the surface changed)
- If a backend DTO/endpoint changed, the ticket carries a `MANUAL_STEP: nswag-regen` flag for the
  owner. The agents do **not** regenerate clients.
- If a schema changed, the ticket carries a `MANUAL_STEP: ef-migration` flag. The agents do **not**
  run migrations.
- If shipped behavior changed, the Docs agent updates the relevant `docs/**` page and the changelog
  in the same ticket (or a linked docs ticket).

### Gate 8 — Mechanical checks pass (always; this is what makes the rules real)
Deterministic beats diligent. Before a ticket reaches `done`, the **mechanical** checks for the
touched stacks pass, and the Reviewer/PM record the result on the ticket as evidence. These are the
**same checks CI enforces** (so a green local check is a green PR), and CI is the structural safety
net — the gate must not depend on a human remembering to run a suite:
- **Backend touched:** `dotnet build` + **all three** test projects succeed — `Cleansia.Tests` (unit),
  **`Cleansia.IntegrationTests` and `Cleansia.HostTests` (real Postgres via Testcontainers)**. The
  integration/host suites are the ones that catch multi-tenant, FK, migration, and webhook bugs that
  SQLite/mocked unit tests cannot — run them, do not skip them. CI `backend-ci.yml` runs all three as
  explicit single-threaded steps.
- **Frontend touched:** the affected app(s) `nx build` (production) **and `nx affected -t test`** (Jest)
  succeed. `nx lint` runs but is currently **non-blocking** (pre-existing baseline debt) — make it
  blocking once that baseline is cleaned. CI `frontend-ci.yml` runs build (blocking) + test (blocking)
  + lint (informational).
- **Android touched:** `:core`/`:partner-app`/`:customer-app` `compileDebugKotlin` + `testDebugUnitTest`
  (JVM unit tests, no emulator) succeed. CI `android-ci.yml` runs these. **Kotlin source must stay
  ASCII/UTF-8 clean — no BOM, no mojibake** (a past mass-edit corrupted encodings that `-q` compiles
  tolerate; verify the diff is byte-clean).
- **iOS touched:** the touched targets `xcodebuild … build test` green on an iPhone simulator
  (`CleansiaCore` / `CleansiaPartner` / `CleansiaCustomer` hosted suites), and **SwiftFormat
  `--lint` + SwiftLint `--strict`** are clean at the **pinned** versions (0.60.1 / 0.65.0 — match
  them locally; CI asserts the pin). CI `ios-ci.yml` runs all of this on the runner's **newest**
  runtime. **For any `layers: [ios]` ticket that is only half the gate** — the latest-runtime run
  must be paired with the **Gate 8.5 iOS 16.4 floor smoke** below; a latest-only run is an
  **incomplete** gate.
- **Any stack:** `node agents/tools/check-consistency.mjs --paths=<changed dirs>` reports **no new**
  violation. A pre-existing violation the change merely sits near is noted, not blocking — unless the
  ticket *is* its canonicalization ticket. See [`enforcement.md`](./enforcement.md) for the tool, the
  ~187-item baseline, and the rollout to fully-automatic CI gating.

> **Verify-not-trust (the lesson that caught every shipped bug in the rework):** when work fans out
> across agents, the orchestrator re-runs the **combined-tree** suites itself before accepting — it does
> not trust per-agent "PASS" reports or per-lane isolation runs. Agents repeatedly reported "the tree
> won't build" on a transient mid-flight state, and reported PASS where a real-DB run failed. The
> authoritative gate is a clean rebuild of the merged tree, not the agent's word.
>
> This posture is a **required artifact, not a habit**: the ticket's `## Review` must contain the
> orchestrator's OWN combined-tree run — the command, its exit code, and the counts (tests passed /
> failed / skipped, build errors). A Gate 8 entry showing only a dev-reported PASS with no independent
> run recorded is **itself a FAIL** — the gate did not run, it was narrated.

**Absent toolchain ⇒ DEFERRED-TO-CI, never PASS.** If a touched stack's toolchain is absent from the
execution environment (Swift on a non-macOS box; `az`/Bicep where not installed; Docker down so the
Testcontainers suites cannot start), the mechanical check for that stack is **DEFERRED-TO-CI** and is
recorded on the ticket verbatim as **UNVERIFIED-LOCALLY** — naming the check that did not run and the
CI job that will run it. An agent may **NEVER** report PASS for a check it could not execute; "it
should pass" is a prediction, not evidence. The ticket may advance on the checks that DID run, but the
deferred check stays visibly open until CI goes green.

A ticket whose mechanical checks fail cannot be `done`, regardless of how good the review reads. If a
check is failing for a reason genuinely unrelated to the change (a flaky test, a pre-existing
baseline item), the Reviewer says so explicitly with evidence — it is never waved through silently.

### Gate 8.5 — iOS 16.4 floor smoke (every `layers: [ios]` ticket)

The project's declared support floor is **iOS 16** (ADR-0014 — the iPhone 8/X-class reach), and the
latest-runtime simulator **cannot represent it**. Before an iOS ticket reaches `done`, its evidence
must include an **iOS 16.4-simulator smoke of the touched surfaces**, in addition to the
latest-runtime suite run: **launch** the app, **navigate every push the diff introduces or
modifies**, and **render the changed screens**. Keep the smoke SHORT and surface-scoped — what the
diff touched, not a full manual regression; the point is the floor runtime, not more steps. A
latest-only run is an **incomplete gate** for any `layers: [ios]` ticket, and the smoke is recorded
on the ticket like any other mechanical check (e.g. "16.4 smoke: boot-install-launch, 0
NavigationAuthority/`comparisonTypeMismatch` hits; pushes navigated; changed screens rendered").

> **Why this leg exists — and must never be "optimized away" as redundant (phase/ios-fix1,
> 2026-07-02):** ALL of the crash/⚠️/island defects in the owner's first real-device shakeout were
> **invisible on the latest-runtime simulator**. iOS 17+ reworked the navigation authority — masking
> both the iOS-16 `comparisonTypeMismatch` **crash** and the yellow-⚠️ missing-destination
> placeholder pushes — and the modern system tab-bar styling masked the never-ported island bar. The
> floor was declared in ADR-0014, yet no gate ever exercised it. The leg **paid for itself within
> its first phase**: the **F-1 catch** — `BrandGradientTests` 18 failures on the 16.4 runtime ONLY
> (green on iPhone 17; `UIColor(Color)` flattens dynamic color providers pre-iOS-17) — is exactly
> the defect class only a floor run can see.

Mechanics and scope:

- The **iOS 16.4 runtime is installed locally** — devices are listed under `-- iOS 16.4 --` in
  `xcrun simctl list devices`. Boot one, install the built app, and walk the touched surfaces.
- Where the touched code is covered by tests, **running the suite against a 16.4 destination**
  counts as (and strengthens) the smoke — that is how F-1 was caught.
- `ios-ci.yml` tests only on the runner's newest runtime. Adding an iOS 16.4 CI destination is a
  **candidate follow-up** (a runtime-download cost decision on hosted runners), not part of this
  gate — until then the floor smoke is **local evidence recorded on the ticket**.
- Real-device CI is out of scope (macOS runners can't attach physical devices) — the leg is
  simulator-based; owner device passes remain ad-hoc acceptance, not a substitute for this gate.

---

## Owner-only steps (the agents never do these)

Per `CLAUDE.md`, two steps are **owner-only**. Agents detect the need, flag it as a `manual_steps`
entry on the ticket, and **block the dependent work** until the owner confirms it's done:

- **EF Core migrations** — `dotnet ef migrations add` / `database update`. Agents describe the
  schema delta; the owner creates & applies the migration.
- **NSwag client regeneration** — `npm run generate-*-client`. Agents flag it; the owner regenerates
  the TypeScript clients before dependent frontend/mobile work begins.

A ticket that needs either and hasn't had it confirmed cannot reach `done`.

### Batch the owner-only handoffs (don't interleave them mid-wave)
The owner-only rule is sound, but interleaving each `dotnet ef` / regen mid-wave is lossy: it leaves
the tree half-broken (a missing migration trips EF `PendingModelChangesWarning` on **every**
integration test; a stale client breaks the build) and forces a slow per-step round-trip. Instead, a
batch should produce **one MANUAL_STEPS bundle at the end** — "run these N migrations + these M
regens, then tell me" — so there is a single fat handoff, not many thin ones. The PM collects the
bundle from the batch's tickets; the orchestrator re-verifies once after the owner confirms the whole
bundle.

### After an NSwag regen, build **all three** apps before pushing
Regenerating one client (e.g. an added required DTO field) commonly breaks an **untouched** consumer
in another app. This drift surfaced repeatedly and was always the same shape (a stale `new
LoginCommand({...})` missing a now-required field). The owner-only regen guardrail stays, but the
follow-through is: **after any regen, run all three production builds** (`build:cleansia-{customer,
partner,admin}`) and fix the consumers before pushing. The blocking frontend prod-build CI catches
this too, but catching it locally avoids a red PR. (No dedicated client-drift CI job: the build gate
already fails on the drift symptom.)

### Match agent count to task risk (don't fan out mechanical work)
Multi-agent fan-out earns its overhead on **wide, parallel, or risky** work (a many-file migration, a
consistency sweep, anything needing independent verification). For **narrow, deterministic** work
(delete N lines, rename a symbol, a one-line consumer fix), a single direct edit + the mechanical
checks is faster and cheaper than dispatching a dev+reviewer — and avoids the rate-limit / collision
cost of pushing parallelism past what the task needs. Heuristic: **fan out for breadth or risk; act
directly for mechanical certainty.**

### Serialize shared-file lanes — and NEVER `git restore` a shared file in a parallel batch
When a batch fans out in parallel, tickets that touch the **same shared file** must be **serialized**
(one writer at a time), not run concurrently. The shared-file clusters that bite are:
`agents/knowledge/consistency.md`, `agents/backlog/INDEX.md`, the per-app **i18n bundles** (the 5
`{en,cs,sk,uk,ru}.json` per app), and the `Policy.cs` / `PolicyBuilder.cs` authz cluster (these two
must move together or `AssertComplete` fails boot). The PM sequences these into a single lane the same
way `:core` (T-0277/T-0278) and the Policy cluster (T-0285) were serialized — never two instances
editing one of them at once. The cluster list is maintained as **data** (verified paths, per-cluster
rationale) in [`shared-file-lanes.md`](./shared-file-lanes.md) — the PM validates every parallel
batch's lane assignments against that file before dispatch.

When true parallelism on adjacent (not identical) regions is unavoidable, each agent is told to **edit
only its own hunks** and is **forbidden from running `git restore` (or `git checkout --`, or a
wholesale revert) on a shared file** — even to "clean scope contamination." A blanket restore of a
shared file silently wipes a *sibling ticket's* committed deliverable.

> **Why this rule exists (a real incident, 2026-06-23 Wave-8 close-out batch):** T-0291 and T-0289 both
> edited `consistency.md` in one parallel batch; a third ticket's (**T-0292**) fix-agent ran
> `git restore consistency.md` to clean what it read as contamination — which **wiped T-0291's
> deliverable** (the disputes-archetype note). The orchestrator's combined-tree re-verify caught it and
> the note was restored by hand. The fix is structural: **serialize the shared-file lane, and ban
> shared-file `git restore` in parallel agents.** If an agent believes a shared file is contaminated, it
> **reports it to the PM** (leaves a note), it does **not** revert the file itself.

### A final-report (StructuredOutput) failure ≠ a work failure — gate the working tree by hand
A dev agent's **final StructuredOutput / report call can error** (retry cap exceeded) while its actual
work **completed on disk**. Observed 2026-06-23 (Wave-8 close-out, T-0290 FE half): the agent had already
written the new audit-entry component/facade/models + specs, all 5 i18n locales, with a clean prod-build
and 24/24 tests — but its final report call failed (likely an oversized / escaping-heavy `buildEvidence`
string tripping schema serialization). The work was fine; only the *report* failed. Rules: **(1)** a
StructuredOutput/final-report failure does **not** mean the work failed — the orchestrator **inspects the
working tree and gates the on-disk result by hand** (here it passed: build clean, tests green). **(2)**
keep `buildEvidence` **concise** to avoid the schema-serialization failure (don't pack the whole diff /
log into one giant escaped string).

> **Recurrence confirms the pattern (2026-06-23, Wave-11 Azure-infra, commit `38a10375`): same failure on
> 3 tickets across 2 runs.** The final-report tool failed on the **T-0319** (rewritten `deploy-dev.yml`)
> and **T-0330** (region connection-string resolver) dev agents — and earlier on **T-0290 FE** — while in
> every case the work landed on disk. The orchestrator gated T-0319 + T-0330 **by hand** (read the
> resolver + the CI; built `Cleansia.Config` 0 errors; secret-scanned; confirmed the tenancy filter
> untouched + all five hosts + OIDC/migration/provision gate) and both are verified-done **even though
> their in-workflow reviewer didn't run**. Three independent occurrences across two waves make this a
> standing operating rule, not a one-off: **a StructuredOutput retry-cap failure is NOT a work failure —
> always inspect the working tree and gate the on-disk result by hand.**
>
> **Mitigation (acts on the likely trigger): keep the schema's `buildEvidence` / `verifyEvidence` fields
> SHORT.** The common thread across all three failures is an **oversized, escaping-heavy evidence string**
> (a whole build log / diff packed into one field) tripping the schema serialization at the retry cap.
> Cap these fields to a terse summary (counts + a one-line verdict + the key file:line), never the raw
> log. The authoritative evidence lives in the ticket status log and the working tree anyway — the report
> field is a pointer, not the artifact.

---

## How a reviewer writes a verdict

In the ticket's `## Review` section, for each gate that applies:

```markdown
## Review — reviewer (2026-06-01)

- Gate 1 Conventions: PASS
- Gate 2 AC: FAIL — AC#3 ("admin sees override badge") has no evidence; the badge component
  isn't wired in `pay-config.component.html`. Add it + screenshot.
- Gate 6 Tests: FAIL — PayCalculationService override precedence has no unit test. Add one
  covering employee-override-wins and fallback-to-service-config.

Verdict: CHANGES REQUESTED. Re-invoke me after fixes.
```

Be specific (file:line + the fix expected), be kind (reject the code, not the author), and never
approve under time pressure. "It's a small change" is not a reason to skip a gate.
