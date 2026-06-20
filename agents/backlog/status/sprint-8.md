# Sprint 8 — Wave 6 plan (carried follow-ups: multi-tenant blocker, security fast-follows, hygiene, mobile ApiResult)

- **Date:** 2026-06-14
- **Goal:** Sequence and execute **Wave 6**: the genuinely-open carry-forward set left after Wave 5 closed.
  Front-load the **multi-tenant go-live blocker (T-0236)** and the small safe cleanups; run the security
  fast-follows and frontend/Android hygiene in shared-file lanes; sequence the **mobile `ApiResult<T>`
  migration (T-0197) ADR-first** (architect-owned, L) — recommend it as its own later mini-wave (Wave 6M)
  rather than folding the whole epic into this wave.
- **Status:** **✅ COMPLETE — closed 2026-06-15.** All in-scope tickets `done`, committed + pushed on
  `feature/wave-6` (`b8f89202`), orchestrator-verified green on a clean rebuild against real Postgres. PR to
  `master` is the owner's call. Q-W5-1 answered (path B) mid-wave → T-0242 unblocked + done. T-0197 stays
  deferred (ADR-first). **Close-out: §close-out below.**
- **Branch:** all Wave-6 work goes on one feature branch **`feature/wave-6`** cut from current `master`
  (**`7debef45`**, Wave 5 / PR #78), committed batch-by-batch. PM never merges; the PR to `master` is the
  owner's call.

---

## 0. Pre-flight reconciliation (verified against the repo, 2026-06-14)

- **Master tip = `7debef45`** ("Feature/wave 5 consistency bugs (#78)"). Waves 0–5 are all merged. Branch
  `feature/wave-6` created from it (no prior `feature/wave-6` existed).
- **Q-W5-1 is STILL UNANSWERED** — verified: the `Answer:` field in `questions/open.md` Q-W5-1 is empty
  (not moved to `answered.md`). **T-0242 stays `blocked` and is EXCLUDED from the executable Wave-6 set.**
- **Stale-status note (one-line cleanup, NOT this wave's work):** a `grep status: draft` returns **79**
  tickets, but ~66 of them are **historical Wave 0–3 tickets** (T-0100…T-0124, T-0142/0143, T-0160…T-0232)
  whose ticket-file frontmatter still reads `draft` even though INDEX.md already shows them `done ✅`
  (reconciled INDEX-side only at each wave close, per the standing convention — no history rewrite). They
  are **not open work.** The genuinely-open drafts are the 13-ticket carry-forward set in §1 + T-0242
  (blocked). **Recommend a future bulk status-reconciliation pass** to flip the shipped-but-stale ticket
  files to `done` so `grep status: draft` stops misleading — a mechanical bookkeeping job, not a Wave-6
  deliverable; flagged to the owner (§4.4).
- **What Wave 5 left for the owner (sprint-7 §7.8 — still standing):** nswag-regen admin client (T-0203) +
  customer client (T-0202, also clears the Wave-3 residual); apply the 4 T-0204 indexes `CONCURRENTLY` in
  PROD; answer Q-W5-1 (→ T-0242); confirm defer-T-0197 (this wave acts on the defer); open the Wave-5 PR.
  These are Wave-5 owner items carried as tracking — none gate Wave-6 batches except where noted.

---

## 1. Scope — the genuinely-open Wave-6 set (13 tickets + 1 deferred-epic + 1 excluded-blocked)

Every executable ticket below is **S or M** (no `L` runs un-split). T-0197 is the only `L` — it stays
`draft`, ADR-first, and is sequenced as a defer-candidate mini-wave (§3.7). T-0242 is excluded (blocked).

| ID | Title (short) | Size | Batch | Layers | depends_on (✓=done) | status | sec gate | manual_step |
|----|---------------|------|-------|--------|---------------------|--------|----------|-------------|
| **T-0236** ⚠️ MULTI-TENANT GO-LIVE BLOCKER | Token-revoke asymmetry: TenantId=null token writes vs tenant-filtered revoke reads | M | **6A** | backend | T-0188✓ | **ready** | **YES** | ef-migration* (only if AC1 backfill path chosen — decide at contract-lock) |
| **T-0262** | Remove dead `BusinessErrorMessage.EmailNotSentError` (zero consumers) | S | **6A** | backend | — | **ready** | no | — |
| **T-0240** | Android `.kotlin` build-artifact dir → `.gitignore` | S | **6A** | android | — | **ready** | no | — |
| **T-0260** | Funnel `HandleChargeback` dispute-terminal write through the T-0172 `CanTransitionTo` guard | S | **6B** | backend | T-0172✓, T-0247✓ | **ready** | **YES** | — |
| **T-0234** | Bound `ChangeOwnPassword` current-password guessing (authenticated surface) | S | **6B** | backend | T-0193✓ | **ready** | **YES** | ef-migration* (only if a dedicated counter is chosen; default = reuse lockout pair → none) |
| **T-0238** | Expose `PdfGenerationFailed`/`PdfGenerationError` on admin EmployeeInvoice DTOs (closes Q-W3-3) | S | **6B** | backend, frontend | T-0171✓ | **ready** | no | **nswag-regen (admin)** |
| **T-0261** | UserMembership partial index: cover the cancellation-reminder sweep arm | S | **6B** | db, backend | T-0204✓ | **ready** | no (optimizer) | **ef-migration** |
| **T-0240→** *(see 6A)* | | | | | | | | |
| **T-0241** | Admin-app eslint selector-prefix alignment + Nx generator default | S | **6C** | frontend | — | **ready** | no | — |
| **T-0259** | Frontend nx-lib test-infra scaffolding (loyalty-promo-codes + customer/partner forgot/login libs) | M | **6C** | frontend | — | **ready** | no | — |
| **T-0239** | Module-boundary sweep: customer features off `@cleansia/partner-services` + eslint boundary rule | M | **6C** | frontend | — | **ready** | no | — |
| **T-0237** | Catalog delete TOCTOU → FK Restrict + violation→`in_use` mapping + template JSON-id check | M | **6D** | backend, db | T-0191✓ | **ready** | **YES** | **ef-migration** |
| **T-0233** | Targeted-lockout DoS mitigation (trusted-device bypass / CAPTCHA) — **PANEL-FIRST** | M | **6E** | backend, frontend | T-0193✓ | **draft → panel** (then ready) | **YES** | ef-migration* (TBD by panel) |
| **T-0197** | Migrate customer-app repos to `ApiResult<T>` (mobile) | **L** | **6M** (defer) | architect, android, ios | — | **draft (ADR-first, split-required)** | no | — |
| **T-0242** | Cancellation-fee Plus free-window override direction | S | — | backend | T-0211✓ | **blocked (Q-W5-1) — EXCLUDED** | no (money-adv) | — |

**Counts:** **ready 11** (T-0236, T-0262, T-0240, T-0260, T-0234, T-0238, T-0261, T-0241, T-0259, T-0239,
T-0237); **panel-first then ready 1** (T-0233 — design decision needs the deliberation panel before it goes
`ready`); **deferred-epic 1** (T-0197, ADR-first); **excluded-blocked 1** (T-0242, Q-W5-1).

---

## 2. Why each ticket is where it is (executable-now vs not)

### 2.1 Executable now — deps `done`, no owner decision, S/M, contract clear
- **T-0236** (M, GO-LIVE BLOCKER) — dep T-0188✓. AC1 is an *architect call between two implementation
  options* (issuance-side stamping vs read-side `IgnoreQueryFilters`), **not an owner product decision** —
  the architect locks it at contract-lock; only AC4 (backfill of existing null-stamped rows) may add an
  ef-migration, flagged then. Goes first.
- **T-0262 / T-0240** — pure mechanical no-decision cleanups (dead constant; gitignore). Safe, early.
- **T-0260** — dep T-0172✓/T-0247✓. Defense-in-depth funnel-through-the-guard; security-touching but the
  contract is fixed (route through the existing guarded entry point). Characterization-test-first.
- **T-0234** — dep T-0193✓. Default path (reuse the existing lockout pair) needs no migration; security
  gate settles the counter choice at contract-lock.
- **T-0238** — dep T-0171✓. Closes Q-W3-3; the owner default already anticipated "yes" (it's contract
  completion of T-0171d AC4, not new design). Carries the `nswag-regen` hold-point.
- **T-0261** — dep T-0204✓. Pure DB index follow-up; ef-migration (owner, CONCURRENTLY).
- **T-0241 / T-0259 / T-0239** — frontend hygiene; deps none / done. Lanes in §3.3.
- **T-0237** — dep T-0191✓. DB-contract-first (FK Restrict) then violation-mapping; ef-migration (owner).

### 2.2 Needs the deliberation panel before `ready` (not an owner blocker, but a real design decision)
- **T-0233** (M, security) — the ticket body itself says *"convene the deliberation panel before this goes
  `ready`"*: trusted-device-cookie vs CAPTCHA vs both, cookie lifetime, scope across the 3 login surfaces,
  and whether Android needs an equivalent marker path is a **product/security design decision**. Per the
  charter, a story/decision is **panel-defended** before it becomes a ready ticket. **6E runs the panel
  first** (analyst author + 2–3 challengers + lead, security in the loop), the analyst finalizes the story,
  then the ticket goes `ready` and implements. Sequenced last because it is the only design-open item.

### 2.3 Needs an ADR first — defer-candidate epic
- **T-0197** (L, architect-owned) — the E5 judgment call (customer-app repos → `ApiResult<T>`, move the
  type into `:core`, fix the iOS Swift contract). **AC1 is an ADR**; the epic must be split (ADR → `:core`
  type move + partner import re-point → one **serial** child per customer-app repo + its ViewModels → iOS
  contract note). It is mobile-only (no nswag-regen, no ef-migration) with **no go-live/money pressure**.
  **Recommendation: run it as its own mini-wave (Wave 6M) AFTER the rest of Wave 6**, or keep it deferred
  to a dedicated mobile wave — see §3.7 and the owner question in §4.2. Do **not** fold the whole epic into
  the Wave-6 batches; at most run the **architect ADR step** in parallel (it edits no shared code) so the
  decision is banked for whenever the owner opens the mobile slice.

### 2.4 Excluded — blocked on an unanswered owner question
- **T-0242** — `blocked` on **Q-W5-1** (Plus free-cancellation-window direction), still unanswered in
  `questions/open.md`. It gates no other ticket. **Excluded from the executable wave**; unblocks the moment
  the owner answers Q-W5-1, then runs as an S money-adjacent ticket (adversarial review) editing
  `BookingPolicy.cs` + `CancellationFeeRateBoundaryTests.cs`. Carried — see §4.1.

---

## 3. Batches, parallelism, shared-file lanes

Reviewer-per-developer on **every** ticket (omitted per-row for brevity). Security gate on every
`security_touching: true` ticket (T-0236, T-0260, T-0234, T-0237, T-0233). "∥" = parallel, "→" = serial.

### Batch 6A — the multi-tenant blocker + the two safe mechanical cleanups. **FIRST. 3 tickets, parallel.**
Disjoint surfaces; the blocker leads.

| Ticket | Dev | Reviewer | Extra gates | Lane / files |
|---|---|---|---|---|
| **T-0236** (M, GO-LIVE BLOCKER) | backend | yes (concurrent) | **Security gate (mandatory)** + qa | `RefreshTokenService.cs` / `TokenService.cs` / refresh-token repo reads + revoke paths + a tenant-context test. Architect call at contract-lock on AC1 (issuance-stamp vs `IgnoreQueryFilters`). **Lane Auth-token** (no other 6A ticket touches it). Do not regress T-0149 (rotation re-checks). ef-migration ONLY if AC4 backfill is chosen → flag + hold then. |
| **T-0262** (S) | backend | yes | qa-light | `BusinessErrorMessage.cs` (remove constant) + the 5 locale JSONs (remove orphaned `errors.*` key **iff** it exists). **Lane BusinessErrorMessage + Lane locale-JSONs** — serialize vs T-0234 (6B), which adds a key in the same files. **Run T-0262 BEFORE T-0234** so the remove-then-add order is clean. |
| **T-0240** (S) | android | yes | — | `src/cleansia_android/.gitignore` only. Fully disjoint. |

### Batch 6B — backend follow-ups (security funnel + authn bound + DTO completion + index). Fan out, 2 serial lanes.
Runs after (or alongside) 6A. **Two intra-wave serialization lanes touch 6A/6B:**
- **Lane BusinessErrorMessage + locale-JSONs** — **T-0234** adds a new `auth.*` error key + ×5 locale
  entries; **T-0262** (6A) removes a dead key + its orphaned locale entries. Both edit
  `BusinessErrorMessage.cs` and the 5 locale JSONs. **Serialize T-0262 → T-0234** (T-0262 first, 6A).
- **Lane Auth-surface** — **T-0234** (`ChangeOwnPassword` counter) and **T-0233** (6E, login-surface
  lockout bypass) both touch the lockout/login authn surface (User counter columns, the login/change-pw
  handlers). They are in different batches (6B vs 6E) and 6E is last, so they don't race — but keep them in
  one conceptual lane and do not dispatch 6E's implementation child until 6B's T-0234 is `done`.

| Ticket | Dev | Reviewer | Extra gates | Parallel with / lane |
|---|---|---|---|---|
| **T-0260** (S) | backend | yes | **Security gate** + qa (characterization-first) | 6B. Lane Dispute-guard — sole editor of the `HandleChargeback` call site + the T-0247 allowlist entry. |
| **T-0234** (S) | backend | yes | **Security gate** + qa (red-first) | 6B. Lane BusinessErrorMessage+locale (after T-0262) + Lane Auth-surface (before T-0233 impl). Default = reuse lockout pair → no migration; security settles. |
| **T-0238** (S) | backend → (HOLD) frontend | yes (each half) | qa; **nswag-regen (admin) HOLD-POINT** | 6B. Backend DTO half lands → flag `nswag-regen (admin)` to owner → **HOLD the frontend half** until the owner regenerates the admin client. Disjoint files from the rest of 6B. |
| **T-0261** (S) | db (+ backend verify) | yes | **Optimizer** (query-plan evidence) + qa; **ef-migration HOLD-POINT** | 6B. Sole editor of the UserMembership index config; coexists with the T-0204 renewal index. ef-migration owner-applied CONCURRENTLY → held at the migration boundary. |

### Batch 6C — frontend hygiene. 3 tickets, one shared config lane.
All deps clear; can run alongside 6A/6B. **Lane FE-config** — these three all touch nx/eslint workspace
config (tags, selector-prefix rule, module-boundary rule, generator defaults). To avoid races on
`nx.json` / shared eslint config / `project.json` tags, **serialize the config-touching edits**:

- **T-0241** (S) — admin eslint **selector-prefix** + Nx generator default (`nx.json` generators block).
- **T-0259** (M) — adds `tags` + jest/eslint/tsconfig.spec **test targets** to under-scaffolded libs
  (`loyalty-promo-codes`, customer login/forgot, partner forgot). Touches per-lib `project.json` tags.
- **T-0239** (M) — adds the `@nx/enforce-module-boundaries` **tag scheme + boundary rule** and swaps 14
  customer files off `@cleansia/partner-services` onto `@cleansia/customer-services`.

**Serialization within Lane FE-config:** **T-0259 → T-0239.** T-0259 establishes the `scope:*`/`type:*`
**tags** on the under-scaffolded libs; T-0239's `enforce-module-boundaries` rule **depends on tags being
present** to constrain anything (an untagged lib is invisible to the rule). So tags-first (T-0259), then the
boundary rule + import swaps (T-0239). **T-0241** edits a *different* config concern (selector-prefix +
generator defaults) and the admin app — it can run **parallel** to the T-0259→T-0239 chain **as long as it
does not edit the same `nx.json` block**; if both need `nx.json` generators, serialize T-0241 after T-0259.
The 14-file import swap in T-0239 includes the **order-wizard cluster (4 files, money path)** — the dev does
it last with the existing Jest specs as the harness; no AUD-07 work is in flight (it shipped in Wave 5), so
no order-wizard contention.

### Batch 6D — catalog-delete TOCTOU hardening. 1 ticket, DB-contract-first.
- **T-0237** (M, security) — **DB layer first** (FKs `Cascade → Restrict` is the contract), then the
  SQLSTATE-23503 violation-mapping in the catalog-delete handlers (→ existing `service.in_use`/
  `package.in_use`), plus the `RecurringBookingTemplate` JSON-id in-use check (no FK can guard JSON refs).
  **ef-migration (owner)** carries the FK-behavior change → held at the migration boundary. Security gate
  (S7a TOCTOU). Real-database test (Testcontainers/Postgres for the violation mapping). Disjoint from all
  other batches → runs in parallel with 6A/6B/6C.

### Batch 6E — targeted-lockout DoS mitigation. **PANEL-FIRST, then implement. Last.**
- **T-0233** (M, security) — **convene the deliberation panel before `ready`** (analyst author + 2–3
  analyst challengers + lead, **security in the loop**): decide trusted-device-cookie vs CAPTCHA vs both,
  cookie lifetime + HMAC scope, coverage across `Login`/`AdminLogin`/`PartnerLogin`, and whether Android
  needs an equivalent marker path. The analyst finalizes the story (the marker must NOT become a session
  credential — S1–S4). **Only then** the PM flips T-0233 `ready` and routes backend (+ frontend, + android
  if cookies chosen) with a reviewer + the security gate. Sequenced **after 6B's T-0234** (shared
  Lane Auth-surface — same lockout/login authn surface). Implementation is red-first.

### Batch 6M — mobile `ApiResult<T>` (T-0197). **DEFER-CANDIDATE — own mini-wave, ADR-first.**
- **T-0197** (L) — **recommend running as its own mini-wave AFTER the Wave-6 batches above, or keeping it
  deferred to a dedicated mobile wave** (owner sequencing call, §4.2). If run: **architect ADR FIRST**
  (AC1 — canonicalize `ApiResult<T>`, move `ApiResult`/`ApiError`/`safeApiCall` into `:core`, fix the iOS
  Swift contract); then split into (2) `:core` type move + partner import re-point → (N) one **serial**
  child per customer-app repo + its ViewModels (never two repo children in parallel) → iOS contract note.
  Each child is S/M, characterization-test-first. Mobile-only → **no nswag-regen, no ef-migration.** The
  architect ADR step edits no shared production code and **may be run in parallel** with the rest of Wave 6
  to bank the decision even if the implementation children are deferred.

### Commit cadence
One commit per batch on `feature/wave-6` (6A's T-0236 may land as its own early commit / hotfix-PR option
given it is the multi-tenant go-live blocker). PM never merges; the PR to `master` is the owner's call.

---

## 4. Owner items

### 4.1 Blocking question carried (gates ONE excluded ticket; not the wave) — ✅ RESOLVED at close
- **Q-W5-1 (blocking: yes)** — Plus free-cancellation-window direction. **ANSWERED 2026-06-14 — path (B)**
  (Plus = wider free window). T-0242 unblocked, folded into 6E, and `done` (`b8f89202`). Moved to
  `questions/answered.md`. No longer carried.

### 4.2 Sequencing question (PM recommendation)
- **T-0197 (mobile `ApiResult<T>`, L, ADR-first):** recommend **either (a) run it as its own mini-wave
  6M after the Wave-6 batches**, or **(b) keep it deferred to a dedicated mobile wave**. It needs an
  architect ADR before any code and is a large serial cross-app refactor with no go-live/money pressure.
  **Owner: confirm (a) ADR-now-implement-after-6, or (b) keep fully deferred.** The PM can bank the ADR in
  parallel (it touches no shared code) either way if the owner wants the decision settled now.

### 4.3 Manual steps flagged this wave (owner-only — PM never runs)
- **T-0238:** `nswag-regen` (admin client) — backend DTO half adds `PdfGenerationFailed`/
  `PdfGenerationError`; the **frontend half is HELD** until the owner regenerates the admin client.
- **T-0261:** `ef-migration` — one additive partial index on `UserMembership` for the cancellation-reminder
  arm; owner builds + applies **CONCURRENTLY** on the populated table. Held at the migration boundary.
- **T-0237:** `ef-migration` — FK behavior change (catalog-referencing FKs `Cascade → Restrict`); owner
  builds + applies. Dependent violation-mapping verification is HELD until the migration is confirmed.
- **T-0236:** `ef-migration` **only if** AC4's backfill of existing null-stamped token rows is required by
  the chosen AC1 rule — the architect decides at contract-lock; the PM adds the flag + holds only then.
- **T-0234:** `ef-migration` **only if** a dedicated counter is chosen over reusing the lockout pair
  (default = reuse → none). Security settles at contract-lock.
- **T-0233:** `ef-migration` **TBD by the panel** (a trusted-device marker may need a column/store) — flag
  at contract-lock if the chosen mechanism requires it.

### 4.4 Stale-status reconciliation flag — ✅ EXECUTED at Wave-6 close (2026-06-15)
- The flagged bulk reconciliation was **performed at close** (see §close-out): **68 historical Wave 0–3
  ticket files** flipped `draft`/`in_progress`/`in-review` → `done` (T-0100…T-0124, T-0126…T-0128,
  T-0142/0143, T-0160…T-0168, T-0170…T-0195, T-0219…T-0222, T-0231/T-0232) — each cross-checked as `done ✅`
  in INDEX. Inline template comments preserved; `updated:` bumped to 2026-06-15. **NOT flipped:** T-0197
  (open deferred epic), T-0263/T-0264 (new follow-ups). `grep status: draft` no longer over-reports.

### 4.5 Standing carry-forwards (unchanged, owner-tracked — from Wave 5 §7.8)
nswag-regen admin client (T-0203) · nswag-regen customer client (T-0202, clears Wave-3 residual) · apply
the 4 T-0204 indexes `CONCURRENTLY` in PROD · answer Q-W5-1 (→ T-0242) · open the Wave-5 PR
`feature/wave-5-consistency-bugs` → `master` · T-0159 rotate-mapbox-token (live exposure) · IMP-1 Google
OAuth ClientId · CZ Stripe-fee figures · DE/AT/ES fiscal go-live gates (Q-REFUND-01/ADR-0009 D7) ·
Q-REFUND-03 per-bundle weights · Q-W3-2 partner-pay currency · Q-W3-4 dispute-resolve-on-refund-failure UX.

---

## 5. Definition of "Wave 6 done"

All in-scope tickets `done`: every AC has named-test / migration-metadata / log-event / reviewer evidence;
reviewer approved each (Security gate reconciled on T-0236/T-0260/T-0234/T-0237 and on T-0233-when-promoted;
optimizer on T-0261); QA recorded; `dotnet build` + `dotnet test` (Cleansia.Tests, IntegrationTests,
HostTests) green on the branch; the touched `nx` projects build/lint/test green; both Android apps build;
`check-consistency.mjs` reports no new violation for every touched area (T-0197's E5 area is excluded —
deferred); every owner manual-step is flagged-and-confirmed (no migration/regen run by an agent); INDEX.md +
this doc match reality. **T-0236 (the multi-tenant go-live blocker) is the headline must-land.** T-0233 is
`done` only via its panel-finalized story + implementation. T-0242 stays `blocked` until Q-W5-1. T-0197 is
`done`, run-as-6M, or kept-deferred per §4.2. PR to `master` is the owner's call.

---

## 6. Explicitly NOT in Wave 6

- **T-0242** — excluded; blocked on Q-W5-1 (owner product decision). Unblocks on answer.
- **The mobile `ApiResult<T>` implementation children** (T-0197's repo-migration children) — deferred to
  6M / a dedicated mobile wave; only the ADR may be banked in parallel.
- The bulk **stale-status reconciliation** of historical Wave 0–3 ticket files — a future bookkeeping pass.
- The owner manual steps themselves (migrations, regens) — flagged, never run.
- Any new feature behavior — Wave 6 is carried follow-ups: one multi-tenant correctness fix (T-0236), one
  defense-in-depth funnel (T-0260), two authn-hardening fixes (T-0234, T-0233), DTO completion (T-0238), DB
  index (T-0261), catalog-delete hardening (T-0237), and frontend/Android hygiene (T-0240/0241/0259/0239),
  plus dead-code removal (T-0262).

---

## close-out — Wave 6 COMPLETE (2026-06-15)

**Committed + pushed on `feature/wave-6` (`b8f89202`). PR to `master` is the owner's call (PM never merges).**

### What landed — 12 tickets DONE, orchestrator-verified green (clean rebuild, real Postgres)
Final suite counts: **Cleansia.Tests 1513/1513 · IntegrationTests 79/79 · HostTests 51/51 · all 3 web apps
build production · 15 locale files valid.**

- **6A** — **T-0236** (multi-tenant token-revoke asymmetry — **GO-LIVE BLOCKER, FIXED**; no ef-migration
  taken) · **T-0262** (dead `BusinessErrorMessage.EmailNotSentError` removed) · **T-0240** (`.kotlin`
  build-artifact dir gitignored).
- **6B** — **T-0260** (chargeback funneled through the T-0172 `CanTransitionTo` dispute guard) · **T-0234**
  (`ChangeOwnPassword` current-password guessing bounded; reused the lockout pair → no migration) ·
  **T-0238** (**BACKEND HALF ONLY** — admin `EmployeeInvoiceDto`/`EmployeeInvoiceDetailDto` gain
  `PdfGenerationFailed`/`PdfGenerationError` + red-first mapper tests; **frontend AC3/AC4 HELD on the admin
  nswag-regen → carried as T-0263**) · **T-0261** (UserMembership cancellation-reminder partial index).
- **6C** — **T-0259** (nx-lib test-infra: tags + jest/eslint/tsconfig.spec on the under-scaffolded libs) ·
  **T-0239** (module-boundary sweep — zero `@cleansia/partner-services` imports under customer features +
  `enforce-module-boundaries` rule) · **T-0241** (admin eslint selector-prefix + Nx generator default).
- **6D** — **T-0237** (catalog-delete TOCTOU → FK Cascade→Restrict + SQLSTATE-23503→`in_use` mapping +
  RecurringBookingTemplate JSON-id in-use check).
- **6E** — **T-0242** (cancellation-fee free-window — **Q-W5-1 answered path (B)** → unblocked + done) ·
  **T-0233** (targeted-lockout DoS mitigation — analyst-panel-decided trusted-device mitigation).

> Count note: "12 DONE this wave" counts T-0238's backend landing; end-to-end fully-closed = 11 (T-0238's
> frontend half is carried as T-0263). The held state is explicit in T-0238's status log so it is not lost.

### Q-W5-1 RESOLVED — path (B)
Owner answered **path (B)**: Plus members get a MORE generous (wider) free-cancellation window. T-0242
implemented + done; **moved to `questions/answered.md`**. Implementation note: under the existing
absolute-threshold caller/resolver wiring the owner intent is satisfied without any out-of-lane change (a
Plus plan seeded below the standard 24h is already more generous) — a literal `BookingPolicy`-only
inversion leaked the standard tier to all-free (reviewer caught it; security re-gate confirmed the revert).

### TWO regressions the real-Postgres gate caught + the orchestrator fixed during verification (AUDIT TRAIL)
Both were green on the unit suite AND passed their per-ticket reviewer — **caught ONLY by the real-DB
HostTests/IntegrationTests.** This reinforces the **verify-on-real-DB gate**.
1. **T-0237** — an explicit `.WithMany()` on `Service`'s read-only projection navs created a **duplicate
   shadow FK `ServiceId1`** that 500'd order-with-services queries. Fixed by a **string-named inverse nav**.
2. **T-0233** — its new integration test seeded a `RefreshToken` for an **unseeded foreign user → FK
   violation**. Fixed by **seeding the foreign user row**.

### Held T-0238 frontend half → follow-up T-0263
The admin failed-vs-pending render + `PdfGenerationError` text + i18n ×5 (T-0238 AC3/AC4) is **blocked on
the owner's admin nswag-regen** so the generated DTOs carry the two new fields. Carried as **T-0263**
(`blocked`). **Q-W3-3 stays OPEN** until T-0263 AC1 lands — NOT moved to `answered.md` yet.

### New Wave-6 close follow-ups filed
- **T-0263** — admin invoice failed-PDF render + i18n (carried frontend half of T-0238), **blocked** on the
  admin nswag-regen. Sprint 7 (Wave-7 candidate).
- **T-0264** — remove the **vestigial `api.email.sending_failed` locale keys** in admin.app + partner.app
  (×5 locales each = 10 entries) that T-0262's `errors.*`/backend scope did not reach (the `api.*`
  namespace). `ready`. Sprint 7 (Wave-7 candidate). No-decision mechanical cleanup.

### Stale-status reconciliation performed (the §4.4 flag, executed at close)
Flipped **68 historical Wave 0–3 ticket files** that read `status: draft`/`in_progress`/`in-review` in
frontmatter but are `done ✅` in INDEX (cross-checked against the INDEX Done markers): **T-0100…T-0124,
T-0126…T-0128, T-0142/0143 (split epics, all children done), T-0160…T-0168, T-0170…T-0195, T-0219…T-0222,
T-0231/T-0232.** Inline template comments preserved; `updated:` bumped to 2026-06-15. **Deliberately NOT
flipped:** T-0197 (genuinely-open deferred epic), T-0263 (new, blocked), T-0264 (new, ready). After the
pass the **only** non-`done` ticket files are exactly those three. `grep status: draft` no longer
over-reports closed work.

### Consolidated OWNER ACTION LIST — Wave 6 (PM never runs these)
**Wave-6 specific:**
1. **Open the PR** `feature/wave-6` → `master` (`b8f89202`). T-0236, the multi-tenant go-live blocker, is
   the headline — it should land before any multi-tenant onboarding (alongside the already-merged T-0245).
2. **nswag-regen — admin client** (T-0238 backend DTO fields `PdfGenerationFailed`/`PdfGenerationError`).
   **Unblocks the held frontend half T-0263.** The same shared DTOs also feed the partner + mobile-partner
   endpoints — additive/backward-compatible (no consumer break), regen those too for completeness.
3. **ef-migrations** — apply **T-0261** (UserMembership cancellation-reminder partial index) and **T-0237**
   (catalog-referencing FKs Cascade→Restrict). In **PROD apply the new indexes `CONCURRENTLY`** by hand
   (additive `CREATE INDEX CONCURRENTLY` outside the migration transaction).
4. ~~**Confirm T-0197 sequencing**~~ → **RESOLVED — T-0197 ran ADR-first and CLOSED 2026-06-17** on
   `feature/wave-6` (`dca897e1` + `7f391fdb`); ADR-0011 accepted. **The `feature/wave-6` PR → `master` now
   also carries ADR-0011 + the mobile `ApiResult<T>` migration.** Mobile-only → no nswag-regen, no
   ef-migration. See the T-0197 close-out section above.

**Still-carrying (standing owner items, unchanged from prior waves):**
5. **citext fix** — the Azure **Functions** host needs `NpgsqlDataSourceBuilder.EnableUnmappedTypes()` (the
   `DbContextBindingExtensions` fix) and a **Functions restart** to read citext columns without throwing.
6. **T-0159 rotate-mapbox-token** — the code fix shipped long ago (token off the URL) but the exposed token
   is still live until rotated in the Mapbox account.
7. ~~**T-0197 mobile `ApiResult<T>` mini-wave**~~ → **DONE (see item 4).**
8. Prior-wave carries still open: nswag-regen customer client (Wave-5 T-0202, clears the Wave-3 residual) +
   admin client (Wave-5 T-0203); the Wave-5 PR `feature/wave-5-consistency-bugs` → `master`; the 4 T-0204
   indexes `CONCURRENTLY` in PROD; IMP-1 Google OAuth ClientId; CZ Stripe-fee figures; DE/AT/ES fiscal
   go-live gates (Q-REFUND-01 / ADR-0009 D7); Q-REFUND-03 per-bundle weights; Q-W3-2 partner-pay currency;
   Q-W3-4 dispute-resolve-on-refund-failure UX.

---

## close-out — T-0197 mobile `ApiResult<T>` slice COMPLETE (2026-06-17)

The deferred Batch-6M epic (§3.7 / §4.2) was **executed and closed** after the Wave-6 batches, on the same
`feature/wave-6` branch. The owner's §4.2 sequencing question is **answered in the running**: option (a) —
ADR-first, then implement — was taken.

### What landed
- **Phase 1 — `dca897e1`:** **ADR-0011 authored + accepted** (`adr/0011-mobile-apiresult-contract.md`,
  living doc `architecture/decisions/mobile-result-contract.md`) — canonicalizes `ApiResult<T>` as the
  binding mobile repository contract (ratifies consistency rule **E5**, clarifies **E3**), and the **`:core`
  type move**: `ApiResult`/`ApiError`/`safeApiCall` hoisted from partner-app into
  `cz.cleansia.core.network`, with partner-app imports re-pointed (no partner behavior change). The iOS Swift
  equivalent (`Result<T, ApiError>`, VM surfaces the alert) is fixed in the ADR so iOS is born canonical.
- **Phase 2 — `7f391fdb`:** **all 15 customer-app repos migrated** to `ApiResult<T>`, with the snackbar
  moved **repo → ViewModel** (`onError { if (it !is ApiError.Network) snackbar.showError(...) }`); silent/
  background paths kept silent (Error → no-op), `ApiError.Network` mapped to VM no-op so the
  `NetworkErrorInterceptor` infra toast is not doubled. Behavior-preserving: same successes, same failures,
  same single snackbar per failure. No E1/E2 UiState change.

### Verified counts (orchestrator, real combined Android tree)
**`:core` + partner-app + customer-app all compile · customer-app 201/201 unit tests pass · `check-consistency
mobile` reports ZERO E5 violations for customer-app · all 64 changed files encoding-clean.** The E5 entry for
the customer-app repos is cleared in `audits/consistency-violations.md` (F16).

### Process note — rate-limit-resume recovery
The run hit a provider rate-limit mid-Phase-2 and was **resumed**; on resume the work was reconciled against
the **real tree** (compile + 201/201 tests + 0 E5 + encoding-clean) before close — confirming no
partial/abandoned migration was left behind. Lesson: a rate-limit interruption on a long serial epic is
recoverable, but **re-verify the whole touched tree on resume** (compile + suite + consistency + encoding),
never trust the pre-interruption green.

### Out of scope — STILL OPEN (their own future tickets, NOT closed by T-0197)
- **E1/E2** — sealed `*UiState` + shared `ActionState` (`audits/consistency-violations.md` F13/F14).
- **E6** — `collectAsStateWithLifecycle()`, **22 instances** remain across mobile screens (F15).
- **E7** — dir/naming inline-singular `features/<name>/` unification (F16).
- **T-0265** (NEW, filed 2026-06-17, S, `[android]`, sprint 7) — the partner/customer unit-test-env gap:
  `LoginViewModelTest` (×4) + `DashboardViewModelTest` fail on plain JVM because
  `android.util.Patterns.EMAIL_ADDRESS` returns `null` without Robolectric/an Android test runtime
  (keeps the partner suite permanently red; **proven pre-existing** — fails identically on clean `master`).

### Owner note
The **`feature/wave-6` PR → `master` now also carries ADR-0011 + the mobile `ApiResult<T>` migration** on top
of the Wave-6 batches. T-0197 is **mobile-only → no nswag-regen, no ef-migration.** PR to `master` is the
owner's call (PM never merges).

---

## Status log
- 2026-06-17 — **T-0197 mobile `ApiResult<T>` slice CLOSED (PM).** The deferred Batch-6M epic executed
  ADR-first on `feature/wave-6`: Phase 1 `dca897e1` (ADR-0011 authored+accepted + `:core` type move +
  partner import re-point) + Phase 2 `7f391fdb` (all 15 customer-app repos → `ApiResult<T>`, snackbar
  repo→VM). Orchestrator-verified on the real combined tree: 3 modules compile · customer-app 201/201 ·
  0 E5 violations (customer-app) · 64 changed files encoding-clean. Cleared the E5 entry in
  `audits/consistency-violations.md` (F16); **E1/E2/E6/E7 kept OPEN as separate rules.** Filed **T-0265**
  (android email-validation unit-test-env gap, pre-existing, sprint 7). Updated INDEX (MOBILE SLICE banner +
  T-0197 row → done + T-0265 follow-up row) + this doc. The owner §4.2 T-0197 sequencing question is answered
  in the running (option (a), ADR-first-then-implement). Backlog bookkeeping only — no code/commits by the PM.
- 2026-06-15 — **Wave 6 CLOSED (PM).** All 12 in-scope tickets `done` on `feature/wave-6` (`b8f89202`),
  orchestrator-verified green (Cleansia.Tests 1513/1513 · IntegrationTests 79/79 · HostTests 51/51 · 3 web
  apps prod-build · 15 locales valid). **T-0236 multi-tenant go-live blocker FIXED.** Q-W5-1 answered
  path (B) → T-0242 unblocked + done, moved to `answered.md`. Recorded the 2 real-Postgres-caught
  regressions (T-0237 `ServiceId1` shadow FK; T-0233 seed FK violation) for the audit trail — both missed
  by unit suite + reviewer. T-0238 done backend-half only; frontend AC3/AC4 HELD on admin nswag-regen →
  carried as **T-0263** (blocked); Q-W3-3 stays open. Filed **T-0264** (vestigial `api.email.sending_failed`
  locale residual, ready). **Stale-status reconciliation executed: 68 historical Wave 0–3 ticket files
  flipped draft→done** (INDEX-confirmed); only T-0197/T-0263/T-0264 remain non-done. Updated INDEX (Wave-6
  COMPLETE banner + done roster + follow-up table) + this doc (§close-out). Owner action list consolidated.
  Backlog bookkeeping only — no code/commits.
- 2026-06-14 — **Wave-6 plan drafted + promoted (PM).** Verified master `7debef45` (Wave 5 / PR #78); cut
  `feature/wave-6` from it. **Confirmed Q-W5-1 STILL UNANSWERED** → T-0242 stays `blocked` and is excluded.
  Reconciled the candidate set against current master: the open Wave-6 set is **13 tickets** (T-0236, T-0260,
  T-0262, T-0240, T-0234, T-0238, T-0241, T-0259, T-0239, T-0237, T-0261, T-0233 + the deferred-epic T-0197);
  T-0242 excluded. **Front-loaded T-0236** (multi-tenant GO-LIVE BLOCKER, security-gated) + the two safe
  mechanical cleanups (T-0262, T-0240) as **Batch 6A**. Sequenced 6B (backend follow-ups: T-0260 funnel-
  guard, T-0234 authn bound, T-0238 DTO+nswag-hold, T-0261 index+ef-migration), 6C (frontend hygiene lane:
  **T-0259 tags → T-0239 boundary-rule**; T-0241 selector-prefix parallel), 6D (T-0237 catalog TOCTOU,
  DB-contract-first + ef-migration), 6E (**T-0233 PANEL-FIRST** then implement, after T-0234), and **6M**
  (T-0197 mobile `ApiResult<T>` — ADR-first defer-candidate, recommend own mini-wave; ADR may bank in
  parallel). **Promoted 11 `ready`**; **T-0233 held `draft` for the deliberation panel** (its body mandates
  it); **T-0197 stays `draft` (ADR-first, split-required)**; **T-0242 stays `blocked` (Q-W5-1)**. Shared-file
  lanes called out: Auth-token (T-0236), BusinessErrorMessage+locale-JSONs (T-0262→T-0234), Auth-surface
  (T-0234→T-0233), Dispute-guard (T-0260), FE-config (T-0259→T-0239; T-0241 parallel). Owner items: Q-W5-1
  (carried, gates T-0242 only), the T-0197 sequencing question (§4.2), 6 conditional manual-step flags
  (§4.3), and the **stale-status-reconciliation flag** (§4.4). No code/commits beyond the branch cut (backlog
  bookkeeping only).
