# Backlog Index

The manifest of every ticket. The **PM owns this file** and updates it on every state transition.
One row per ticket. Source of truth for "what's the team doing right now".

## Legend
- **Status:** draft · ready · in_progress · in_review · qa · done · blocked
- **Size:** S · M · L
- **Layers:** analyst, architect, db, backend, frontend, android, ios, docs

## Active

> ## 🏁 ALL WAVES (0–7) COMPLETE — the entire audit-driven program backlog is DONE (2026-06-21)
> **Every ticketed wave is closed.** Waves 0–6 + the T-0197 mobile slice + T-0264/T-0265 are merged to
> `master` (tip `b9e91cd8`, PR #81). **Wave 7 (Android consistency debt) is now COMPLETE** — 4 tickets
> done, committed + pushed (`9c1989e4`) on `feature/wave7-android-consistency`; PR to `master` is the
> owner's call (PM never merges). The **consistency audit (`audits/consistency-violations.md`) is
> essentially fully resolved** — all backend (F1–F8), frontend (F10–F12), and Android §E rules
> (E1/E2/E5/E6/E7) closed.
>
> **What remains across the WHOLE program — exactly ONE open engineering follow-up + standing owner items:**
> - **Engineering follow-up (the ONLY open ticket):** **T-0270** (E2 residual — 3 post-Wave-5
>   one-shot-action VMs onto `ActionState`; S, `[android]`, draft, sprint 8; behavior-preserving,
>   non-blocking). **Every other follow-up is `done`:** T-0263 (admin failed-PDF render — the owner's
>   admin nswag-regen WAS confirmed and the frontend half shipped: 34/34 + 12/12 green; Q-W3-3 now
>   reconciled-closed), T-0264 (vestigial locale keys), T-0265 (Android email-validation test-env gap —
>   why the partner suite is green on plain JVM) are all **`done`** on `master`/the Wave-7 branch.
> - **Standing OWNER items (PM never runs these):** the two ops tasks — **Mapbox key rotation** +
>   **Functions app restart**; the queued **owner manual steps** still pending merge/apply (the Wave-6
>   ef-migrations: T-0261 UserMembership index + T-0237 catalog FK → in PROD apply the new indexes
>   `CONCURRENTLY` by hand; and the two open PRs to `master`); and the **optional product / external-config
>   items** (IMP-1 Google OAuth needs a Google Cloud project; BUG-22 email-badge CSS). Full consolidated
>   owner list: `status/sprint-9.md` §close-out.
>
> --- (Wave-7 close detail below; mobile-slice + Wave-6 history kept for traceability) ---
>
> ## ✅ WAVE 7 COMPLETE — Android consistency debt (deferred E1/E2/E6/E7) (closed 2026-06-21)
> **Wave 7 is COMPLETE — all work committed + pushed on `feature/wave7-android-consistency` (`9c1989e4`).**
> PR to `master` is the owner's call (PM never merges). It cleared the **last** engineering debt: the
> deferred Android consistency-sweep rules **E1/E2/E6/E7** filed STILL-OPEN in
> `audits/consistency-violations.md` (F13/F14/F15/F16). T-0197 had closed **E5/ApiResult** only. All four
> were **Android-only, mobile-only, behavior-preserving** — no go-live / money / correctness impact. **No
> new ADR** (E5/E7 ratified by ADR-0011; E1/E2/E6 are §E rules). **No deliberation panel** (each a
> mechanical canonicalization against a ratified rule → one-line no-decision note). Plan + execution
> lanes + the E6 real-vs-raw count: **`status/sprint-9.md`**.
>
> **Orchestrator-verified on the real Android tree:** `:core` + partner-app + customer-app **all compile**;
> **partner-app 37/37** (was 26 — T-0267 added 11 E1 characterization tests), **customer-app 201/201**,
> **`:core` 13/13**; **92 changed files encoding-clean**; the **E6 re-grep confirms only the scoped
> exclusions remain** (Singleton-repo flows, the 2 NavHosts, `:core` `GlobalSnackbarHost`).
>
> **DONE (4):** **T-0266** (E7 — partner dir/naming collapsed to inline-singular `features/<name>/`; pure
> move + package/import rewrite, 0 body diffs; `Details`→`Detail` singular rename) · **T-0267** (E1 —
> residual partner page-state flag-bags `InvoiceDetailsViewModel` + `OrderPhotosViewModel` → sealed
> `*UiState`; +11 characterization tests) · **T-0268** (E2 — **verify-and-close, NO production edits**;
> the audit-named F14 set confirmed canonical on the shared `ActionState`, F14 cleared — **surfaced 3
> genuine post-Wave-5 E2 residuals → carried as T-0270**) · **T-0269** (E6 —
> `collectAsStateWithLifecycle()` sweep over the filtered ≈56 screen/VM-flow collections across both apps).
>
> **Audit closed:** `audits/consistency-violations.md` — **F13 (E1), F14 (E2), F15 (E6), F16-E7
> RESOLVED**; F14 carries the **small T-0270 residual**. The consistency sweep is essentially complete.
>
> **NEW follow-up filed:** **T-0270** (S, `[android]`, draft, sprint 8) — convert the 3 one-shot-action
> VMs that postdate T-0252 (`CreateRecurringViewModel`, `DisputeDetailViewModel`, `DeleteAccountViewModel`)
> off loose `_submitting`/`_loading` booleans onto the shared `ActionState` + `SharedFlow` pattern.
> Behavior-preserving. The per-row/per-button in-flight discriminators
> (`OrderDetailsViewModel._inFlightAction`, `OrdersListViewModel.inFlightActionOrderId`,
> `RecurringBookingsViewModel._mutating`) are **recorded NON-violations** (a single `ActionState` can't
> express which-row/which-button) — **NOT** in T-0270's scope.
>
> | ID | Rule | Title | Size | Status | depends_on | Layers | sec | manual_step |
> |----|------|-------|------|--------|-----------|--------|-----|-------------|
> | **T-0266** | **E7** | Unify partner-app dir/naming → inline-singular `features/<name>/` (structural move, no logic) | M | **done ✅** `9c1989e4` | — | android | no | — |
> | **T-0267** | **E1** | Convert residual partner flag-bag `*UiState` → sealed (`InvoiceDetails`+`OrderPhotos`; T-0252 did the rest) | M | **done ✅** `9c1989e4` | T-0266✓ | android | no | — |
> | **T-0268** | **E2** | Verify-and-close shared `ActionState` coverage (done by T-0252) — no production edits; surfaced T-0270 | S | **done ✅** `9c1989e4` (verify-close) | — | android | no | — |
> | **T-0269** | **E6** | `collectAsStateWithLifecycle()` sweep — filtered ≈56 screen/VM-flow violations (both apps) | M | **done ✅** `9c1989e4` | T-0266✓, T-0267✓ | android | no | — |
>
> **Wave-7 close follow-up (filed 2026-06-21):**
>
> | ID | Title | Size | Status | depends_on | Layers | sec | manual_step | Source |
> |----|-------|------|--------|-----------|--------|-----|-------------|--------|
> | **T-0270** | Convert 3 post-Wave-5 one-shot-action VMs (`CreateRecurring`/`DisputeDetail`/`DeleteAccount`) off loose `_submitting`/`_loading` booleans → shared `ActionState` + `SharedFlow` | S | **draft** (sprint 8) | — | android | no | — | T-0268 E2 verify-close AC4 residual |
>
> --- (mobile-slice + Wave-6 history below, kept for traceability) ---
>
> ## ✅ MOBILE SLICE — T-0197 `ApiResult<T>` migration COMPLETE (closed 2026-06-17, on `feature/wave-6`)
> **T-0197 (mobile `ApiResult<T>`, the deferred ADR-first L epic) is DONE** — committed + pushed on
> `feature/wave-6` in two phases: **Phase 1 = `dca897e1`** (ADR-0011 authored+accepted + the `:core` type
> move: `ApiResult`/`ApiError`/`safeApiCall` hoisted into `cz.cleansia.core.network`, partner-app imports
> re-pointed) · **Phase 2 = `7f391fdb`** (all **15 customer-app repos** migrated to `ApiResult<T>`, snackbar
> moved repo → VM). PR to `master` is the owner's call (the `feature/wave-6` PR now also carries ADR-0011 +
> this mobile migration on top of the Wave-6 batches). **PM never merges.**
>
> **ADR-0011 (`adr/0011-mobile-apiresult-contract.md`) is `accepted`** (2026-06-15) — it ratifies
> consistency rule **E5** as the binding mobile repo contract, fixes the type's `:core` home, and fixes the
> born-canonical iOS Swift equivalent. Living doc: `architecture/decisions/mobile-result-contract.md`.
>
> **Orchestrator-verified** on the real combined Android tree: `:core` + partner-app + customer-app **all
> compile**; **customer-app 201/201 unit tests pass**; **ZERO E5 consistency violations for customer-app**
> (`check-consistency mobile`); all **64 changed files encoding-clean**. The E5 entry for the customer-app
> repos is **cleared** in `audits/consistency-violations.md` (F16).
>
> **Process note (rate-limit-resume recovery):** the run hit a provider rate-limit mid-Phase-2 and was
> resumed; the resume was reconciled against the real tree (compile + 201/201 tests + 0 E5 + encoding) before
> close — no partial/abandoned migration left behind.
>
> **STILL OPEN — separate out-of-scope mobile-consistency rules (their OWN future tickets, NOT closed by
> T-0197):** **E1/E2** (sealed `*UiState` + shared `ActionState` — F13/F14) · **E6**
> (`collectAsStateWithLifecycle()`, **22 instances** — F15) · **E7** (dir/naming inline-singular — F16).
>
> **NEW follow-up filed:** **T-0265** (S, `[android]`, draft, sprint 7) — the partner-app + customer-app
> unit-test-env gap: `LoginViewModelTest` (×4) + `DashboardViewModelTest` fail on plain JVM because
> `android.util.Patterns.EMAIL_ADDRESS` returns `null` without Robolectric/an Android test runtime (keeps the
> partner suite permanently red; **proven pre-existing** — fails identically on clean `master`, independent
> of T-0197). Scope: add Robolectric **or** extract email validation off `android.util.Patterns`. Row in the
> follow-up table below the Wave-6 roster.
>
> ⚠️ **OWNER:** the `feature/wave-6` PR → `master` now carries **ADR-0011 + the mobile `ApiResult<T>`
> migration** in addition to the Wave-6 batches. Mobile-only refactor → **no nswag-regen, no ef-migration**
> for T-0197. Full consolidated owner list: `status/sprint-8.md` §close-out.
>
> --- (Wave-6 close banner below, kept for traceability) ---
>
> ## ✅ WAVE 6 COMPLETE — carried follow-ups (multi-tenant blocker, security fast-follows, hygiene) (closed 2026-06-15)
> **Wave 6 is COMPLETE — all work committed + pushed on `feature/wave-6` (`b8f89202`).** PR to `master`
> is the owner's call (PM never merges). **12 tickets DONE this wave**, **orchestrator-verified green** on a
> clean rebuild against real Postgres: **Cleansia.Tests 1513/1513 · IntegrationTests 79/79 · HostTests
> 51/51 · all 3 web apps build production · 15 locale files valid.** The headline: **T-0236, the
> MULTI-TENANT TOKEN-REVOKE GO-LIVE BLOCKER, is FIXED.** Close-out detail (per-batch landings, the two
> regressions the real-DB gate caught, the held T-0238 frontend half, owner manual-step queue, follow-ups
> filed): **`status/sprint-8.md` §close-out**.
>
> **DONE (12):** 6A — **T-0236** (multi-tenant token-revoke asymmetry — GO-LIVE BLOCKER, FIXED) · **T-0262**
> (dead const removed) · **T-0240** (.kotlin gitignore). 6B — **T-0260** (chargeback funneled through the
> dispute guard) · **T-0234** (ChangeOwnPassword guess bound) · **T-0238** (invoice PDF-failure DTO —
> **BACKEND HALF ONLY**; frontend AC3 HELD on the admin nswag-regen → carried as **T-0263**) · **T-0261**
> (UserMembership cancellation-reminder partial index). 6C — **T-0259** (nx-lib test-infra) · **T-0239**
> (module-boundary sweep — zero `@cleansia/partner-services` imports under customer features + eslint rule)
> · **T-0241** (admin eslint selector-prefix). 6D — **T-0237** (catalog-delete TOCTOU → FK Restrict). 6E —
> **T-0242** (cancellation-fee per **Q-W5-1 path (B)** — unblocked + done) · **T-0233** (lockout-DoS —
> analyst-panel-decided trusted-device mitigation). *(T-0238 is `done` for its backend half; its frontend
> half is the new follow-up T-0263 — count of fully-closed-end-to-end = 11; "12 DONE this wave" counts
> T-0238's backend landing.)*
>
> **Q-W5-1 RESOLVED:** owner answered **path (B)** — Plus members get a wider free-cancellation window;
> T-0242 implemented + done, Q-W5-1 moved to `answered.md`.
>
> **TWO regressions the real-Postgres gate caught + the orchestrator fixed during verification (audit trail
> — unit tests + reviewer PASS MISSED both):** (a) **T-0237** — an explicit `.WithMany()` on Service's
> read-only projection navs created a duplicate shadow FK `ServiceId1` that 500'd order-with-services
> queries; fixed by a string-named inverse nav. (b) **T-0233** — its new integration test seeded a
> `RefreshToken` for an unseeded foreign user (FK violation); fixed by seeding the foreign user row. **Both
> were caught ONLY by HostTests/IntegrationTests against real Postgres** — reinforces the verify-on-real-DB
> gate (the unit suite + the per-ticket reviewer were both green and blind to them).
>
> **STILL OPEN / carried out of Wave 6:** ~~**T-0197** (mobile `ApiResult<T>`, L, ADR-first) — stays
> deferred~~ → **DONE 2026-06-17** as the mobile slice on `feature/wave-6` (`dca897e1`+`7f391fdb`); ADR-0011
> accepted. See the MOBILE SLICE banner at the top of Active. Its out-of-scope siblings (E1/E2, E6, E7) and
> the new test-env follow-up **T-0265** carry forward.
>
> **NEW Wave-6 close follow-ups filed (T-0263…T-0264):** **T-0263** (admin invoice failed-PDF render + i18n
> — the carried frontend half of T-0238, `blocked` on the admin nswag-regen) · **T-0264** (remove the
> vestigial `api.email.sending_failed` locale keys in admin.app + partner.app, ×5 locales each, that
> T-0262's `errors.*`/backend scope did not reach, `ready`). Detail rows in the Wave-6 close follow-up table
> below the Wave-6 roster. Both are Wave-7 candidates.
>
> ⚠️ **OWNER ACTION QUEUE for Wave 6** (PM never runs these): **(1)** open the **PR `feature/wave-6` →
> `master`** · **(2) nswag-regen — admin client** (T-0238 backend DTO fields `PdfGenerationFailed`/
> `PdfGenerationError`; unblocks the held frontend half **T-0263**; the same shared DTOs also feed
> partner + mobile-partner — additive/backward-compatible) · **(3)** apply the **T-0261 + T-0237
> ef-migrations**, and in PROD apply the **new indexes `CONCURRENTLY`** by hand · **(4)** confirm the
> **T-0197** sequencing (6M now or stay deferred). Full consolidated list: `status/sprint-8.md` §close-out.
>
> --- (Wave-6 planning/progress history below, kept for traceability) ---
>
> ## 🟢 WAVE 6 (planning + progress) — carried follow-ups (multi-tenant blocker, security fast-follows, hygiene, mobile ApiResult) (promoted 2026-06-14) *(superseded by the WAVE 6 COMPLETE banner above)*
> **Wave 5 merged to master: PR #78 (`7debef45`).** Owner gave the GO on **Wave 6** — the genuinely-open
> carry-forward set after the Wave-5 close. **Branch: `feature/wave-6`** (cut from `7debef45`), committed
> batch-by-batch. PM never merges; the PR to `master` is the owner's call. Full sequenced plan + per-ticket
> lanes/gates/manual-steps + the owner items: **`status/sprint-8.md`**.
>
> **Scope = 13 genuinely-open tickets** (the recent follow-ups + deferred items), NOT the historical
> Wave 0–3 ticket files that still read `draft` but are `done ✅` here (the **stale-status reconciliation**
> was performed at Wave-6 close — see the close-out banner; 68 stale historical ticket files flipped to
> `done`). **Front-loaded T-0236** (the MULTI-TENANT GO-LIVE BLOCKER, security-gated) + two safe
> mechanical cleanups (T-0262, T-0240) as **Batch 6A**.
>
> **Promoted 11 `ready`:** T-0236, T-0262, T-0240, T-0260, T-0234, T-0238, T-0261, T-0241, T-0259, T-0239,
> T-0237. **Held 1 `draft` for the deliberation PANEL** (its body mandates it): **T-0233** (lockout-DoS
> mitigation — trusted-device vs CAPTCHA design decision). **Deferred-epic 1:** **T-0197** (mobile
> `ApiResult<T>`, L, ADR-first) — runs as its own mini-wave **6M** or stays deferred (owner call, sprint-8
> §4.2); the ADR may bank in parallel. **Excluded-blocked 1:** **T-0242** — was **BLOCKED on Q-W5-1**
> (now answered path (B) → unblocked + done this wave).
>
> **Reviewer-per-developer on every ticket. Security gate** on T-0236, T-0260, T-0234, T-0237, T-0233.
> **Optimizer** on T-0261.
>
> | ID | Title | Size | Status | Batch | Layers | sec | manual_step |
> |----|-------|------|--------|-------|--------|-----|-------------|
> | **T-0236** ⚠️ MULTI-TENANT GO-LIVE BLOCKER | Token-revoke asymmetry: TenantId=null writes vs tenant-filtered revoke reads | M | **done ✅** `b8f89202` | 6A | backend | **yes** | ef-migration* (not taken) |
> | **T-0262** | Remove dead `BusinessErrorMessage.EmailNotSentError` (zero consumers) | S | **done ✅** `b8f89202` | 6A | backend | no | — |
> | **T-0240** | Android `.kotlin` build-artifact dir → `.gitignore` | S | **done ✅** `b8f89202` | 6A | android | no | — |
> | **T-0260** | Funnel `HandleChargeback` through the T-0172 `CanTransitionTo` guard (defense-in-depth) | S | **done ✅** `b8f89202` | 6B | backend | **yes** | — |
> | **T-0234** | Bound `ChangeOwnPassword` current-password guessing | S | **done ✅** `b8f89202` | 6B | backend | **yes** | ef-migration* (not taken — reused lockout pair) |
> | **T-0238** | Expose PdfGenerationFailed/Error on admin EmployeeInvoice DTOs (closes Q-W3-3) | S | **done ✅ (BACKEND HALF)** `b8f89202` — frontend AC3 HELD → **T-0263** | 6B | backend, frontend | no | **nswag-regen (admin) — owner** |
> | **T-0261** | UserMembership partial index: cover the cancellation-reminder sweep arm | S | **done ✅** `b8f89202` | 6B | db, backend | no (optimizer) | **ef-migration (owner; PROD = CONCURRENTLY)** |
> | **T-0241** | Admin-app eslint selector-prefix alignment + Nx generator default | S | **done ✅** `b8f89202` | 6C | frontend | no | — |
> | **T-0259** | Frontend nx-lib test-infra scaffolding (tags + jest/eslint/tsconfig.spec) | M | **done ✅** `b8f89202` | 6C | frontend | no | — |
> | **T-0239** | Module-boundary sweep: customer features off `@cleansia/partner-services` + eslint rule | M | **done ✅** `b8f89202` | 6C | frontend | no | — |
> | **T-0237** | Catalog delete TOCTOU → FK Restrict + violation→`in_use` + template JSON check | M | **done ✅** `b8f89202` (⚠️ caught the `ServiceId1` shadow-FK regression — see close-out) | 6D | backend, db | **yes** | **ef-migration (owner)** |
> | **T-0242** | Cancellation-fee Plus free-window override direction (Q-W5-1 path **B**) | S | **done ✅** `b8f89202` | 6E | backend | no (money-adv) | — |
> | **T-0233** | Targeted-lockout DoS mitigation (trusted-device, panel-decided) | M | **done ✅** `b8f89202` (⚠️ caught the seed FK-violation regression — see close-out) | 6E | backend, frontend | **yes** | (panel marker; no migration taken) |
> | **T-0197** | Migrate customer-app repos to `ApiResult<T>` (mobile) | **L** (epic, ran as 15 serial children) | **done ✅** `dca897e1`+`7f391fdb` (mobile slice, closed 2026-06-17; ADR-0011 accepted; 0 E5; 201/201) | 6M | architect, android, ios | no | — |
>
> \* `nswag-regen`/`ef-migration` flagged conditionally fire only when the diff actually changes a
> generated-client surface or schema. **Owner manual steps this wave:** T-0238 nswag-regen (admin);
> T-0261 ef-migration (UserMembership index, CONCURRENTLY in PROD); T-0237 ef-migration (catalog FK
> Cascade→Restrict). Full detail: sprint-8 §close-out / §4.3. **Q-W5-1 RESOLVED (path B).** Dispatch was
> {6A, 6B, 6C, 6D} concurrent → 6E (T-0233 panel + T-0242 once Q-W5-1 answered).

**Wave-6 close follow-ups (filed 2026-06-15) — the held T-0238 frontend half + the T-0262 locale residual. Both Wave-7 candidates.**

| ID | Title | Size | Status | depends_on | Layers | sec | manual_step | Source |
|----|-------|------|--------|-----------|--------|-----|-------------|--------|
| **T-0263** | Admin invoice failed-PDF render (failed-vs-pending indicator + `PdfGenerationError` text) + i18n ×5 — carried frontend half of T-0238 | S | **blocked** (admin nswag-regen) | T-0238✓ (backend) | frontend | no | **nswag-regen (admin)** | T-0238 AC3/AC4 held at Wave-6 close |
| **T-0264** | Remove vestigial `api.email.sending_failed` locale keys (admin.app + partner.app, ×5 locales each = 10 entries) | S | **ready** | T-0262✓ | frontend | no | — | T-0262 residual (its `errors.*`/backend scope did not reach the `api.*` namespace) |
| **T-0265** | Make email-validating VMs unit-testable off `android.util.Patterns` (Robolectric or extract) — `LoginViewModelTest`×4 + `DashboardViewModelTest` red on plain JVM | S | **draft** (sprint 7) | — | android | no | — | T-0197 Phase-2 verification (pre-existing test-env gap, proven on clean `master`) |

> **T-0263** carries the **frontend half of T-0238** (the admin failed-vs-pending render + error text +
> i18n). T-0238 shipped its backend DTO fields in Wave 6; the frontend AC is **blocked on the owner's
> admin nswag-regen** and unblocks to `ready` the moment that lands. **Q-W3-3 stays OPEN** until T-0263's
> AC1 lands (it is NOT moved to `answered.md` yet). **T-0264** is the i18n residual T-0262 left because its
> scope was the backend constant + the `errors.*` namespace, not the `api.*` namespace where the frontend
> mirror lives (10 orphaned entries; the sibling `api.email.invalid_format`/`invalid_email` stay).
>
> --- (Wave-5 history below, kept for traceability) ---
>
> ## ✅ WAVE 5 COMPLETE — priority bugs + consistency/quality sweep (closed 2026-06-14)
> **Wave 5 is functionally COMPLETE — all work committed + pushed on `feature/wave-5-consistency-bugs`**
> (commits **`3df53ab2`** [5A bugs], **`79b0153c`**, **`226bc928`**, **`9be1f8ee`**). PR to `master` is the
> owner's call (PM never merges). **21 tickets DONE** this wave, **orchestrator-verified green** on a clean
> rebuild against real Postgres: **Cleansia.Tests 1472/1472 · IntegrationTests 66/66 · HostTests 51/51 ·
> frontend order-wizard 119/119 + customer-disputes 41/41 Jest · all 3 web apps build production · S6
> logging 9/9.** **T-0212 CreateOrder characterization gate held 20/20 unchanged** through the AUD-06
> decomposition. Close-out detail (per-batch landings, AUD-06/AUD-07 decomposition outcomes, owner manual-step
> queue, real bugs fixed, follow-ups filed): **`status/sprint-7.md` §close-out**.
>
> **DONE (21):** 5A — **T-0245** (multi-tenant webhook GO-LIVE BLOCKER — FIXED) · **T-0246** (StartOrder NRE
> — FIXED). 5B — **T-0243 · T-0203 · T-0244 · T-0205 · T-0206**. 5C (T-0196 epic) — **T-0248 · T-0249 ·
> T-0250 · T-0251 · T-0252**. 5D (T-0199/AUD-06 epic) — **T-0253 · T-0254 · T-0255** (CreateOrder god-handler
> decomposed). 5E — **T-0201 · T-0198** (fixed real bugs: weak admin password, swallowed login/forgot errors).
> 5F (T-0200/AUD-07 epic) — **T-0256 · T-0257 · T-0258** (order-wizard decomposed) · **T-0202** (disputes
> own-client). 5G — **T-0204** (perf cluster + GDPR paging correctness fix + 4 indexes) · **T-0247**
> (consistency-rule tooling). **The 3 parent epics T-0196 / T-0199 / T-0200 are now `done`** (all children done).
>
> **STILL OPEN (carried out of Wave 5):** **T-0242** (cancellation-fee Plus free-window direction) — **BLOCKED
> on Q-W5-1** (owner product decision, still unanswered); carried to whenever the owner answers. **T-0197**
> (mobile `ApiResult<T>` L-migration) — **DEFERRED to Wave 6** per sprint-7 §4.2 (stays `draft`, ADR-first).
>
> **NEW Wave-5 close follow-ups filed (T-0259…T-0262, all `draft`, Wave-6 candidates):** **T-0259** frontend
> nx-lib test-infra scaffolding (T-0203 + T-0198 findings) · **T-0260** funnel HandleChargeback through the
> T-0172 dispute guard (T-0247 finding, `sec`) · **T-0261** UserMembership partial-index cancellation-reminder
> arm (T-0204 finding, ef-migration) · **T-0262** remove dead `BusinessErrorMessage.EmailNotSentError` (T-0205
> finding). Detail rows in the Wave-5 close follow-up table below.
>
> ⚠️ **OWNER ACTION QUEUE for Wave 5** (PM never runs these): **(1) nswag-regen — admin client** + **customer
> client** (T-0203 / T-0202 surfaces; the customer regen also clears the residual Wave-3 `DisputeReason.Chargeback`
> + device-endpoints item) · **(2) the T-0204 ef-migration WAS applied; for PROD apply the 4 indexes
> `CONCURRENTLY` by hand** (additive `CREATE INDEX CONCURRENTLY` outside the migration transaction) ·
> **(3) answer Q-W5-1** to unblock T-0242 · **(4) confirm defer-T-0197-to-Wave-6** · **(5) the PR to `master`.**
> Full consolidated list: `status/sprint-7.md` §close-out.
>
> --- (Wave-5 planning/progress history below, kept for traceability) ---
>
> ## 🟢 WAVE 5 (planning + progress) — priority bugs + consistency/quality sweep (promoted 2026-06-13)
> **Wave 4 merged to master: PR #77 (`ee95a57f`).** Owner gave GO on Wave 5 and **folded the two
> confirmed production bugs T-0245 + T-0246 to the FRONT** (fix first). Scope = the 2 bugs + the
> consistency/quality sweep **T-0196…T-0206** + the 3 Wave-4 follow-ups **T-0242/T-0243/T-0244**. Full
> sequenced plan + per-ticket stale-text deltas + lane/serialization notes: **`status/sprint-7.md`**.
> **Branch:** all work on `feature/wave-5-consistency-bugs` (cut from `ee95a57f`), committed batch-by-batch.
>
> **Intake actions:** (1) **fixed an id collision** — two files claimed `id: T-0200`; the dispute-guard
> `check-consistency` follow-up (`T-0200-da-2-followup.md`) was **re-id'd `T-0200 → T-0247`**; the AUD-07
> order-wizard file keeps canonical `T-0200`. (2) sprint frontmatter re-tagged `3→5` on the swept tickets.
> (3) **L-epics are NOT promoted `ready`** — they were split at dispatch. (4) Opened **Q-W5-1 (blocking)** —
> Plus free-cancellation-window direction — **gates T-0242 ONLY**; the rest of the wave proceeds.
>
> **WAVE-5 PROGRESS (2026-06-13):** **Batch 5A DONE / committed `3df53ab2`** (T-0245 webhook tenant-scope +
> T-0246 StartOrder NRE). Owner approved driving the rest autonomously. **The three L-epics are now SPLIT**
> into **11 child tickets T-0248…T-0258** (T-0196→T-0248..T-0252; T-0199→T-0253..T-0255; T-0200→T-0256..T-0258);
> the epics are `in_progress` [SPLIT/EPIC] trackers (`done` only when their children are). T-0197 (5H) stays
> `draft`, defer-candidate. **Dependency-ordered dispatch plan: sprint-7 §2.2** — {5B,5C,5D,5E} concurrent →
> {5F,5G} after T-0249/T-0251 land → 5H deferred. **5C must complete before 5F/5G.** T-0242 BLOCKED on Q-W5-1.
>
> **Critical sequencing:** **Batch 5A = T-0245 ∥ T-0246 FIRST** (disjoint files; T-0245 is the
> **multi-tenant GO-LIVE BLOCKER**, `security_touching`, with a non-null-tenant integration test extending
> the T-0210 webhook suite; T-0246 = null-guard + regression). **Batch 5D = T-0199/AUD-06 runs ALONE on
> the `CreateOrder.cs` cluster** — its acceptance gate is **T-0212's Wave-4 characterization suite staying
> green unchanged**; nothing else touching `CreateOrder.cs` parallelizes with it. **T-0196 (5C) is the
> base** the frontend rebuilds (T-0200, T-0202) and the perf cluster (T-0204) depend on. **Reviewer-per-
> developer on every ticket; Security gate on T-0245** (advisory on T-0198/T-0206/T-0247); adversarial
> money review on T-0244 (and T-0242 when unblocked); optimizer on T-0204.
>
> | Batch | Tickets | Parallelism / lanes |
> |---|---|---|
> | **5A — priority bugs (FIRST) — DONE ✅ `3df53ab2`** | **T-0245** (webhook tenant-scope, M, sec gate, GO-LIVE BLOCKER) ∥ **T-0246** (StartOrder NRE→500, S) | Parallel — disjoint files. Both verified + committed. |
> | **5B — backend micro-fixes + long tail** | **T-0243** (XS) → **T-0203** (M) *(Lane M-Membership, serial — both edit `CreateMembershipCheckoutSession.cs`)* · **T-0244** (S, money-adv) · **T-0205** (S, backend∥mobile) · **T-0206** (S, S6 sec-advisory) · **T-0242** (S, **BLOCKED Q-W5-1**, Lane BookingPolicy) | Fan out; 2 serial lanes (M-Membership, BookingPolicy). |
> | **5C — consistency sweep base (T-0196 SPLIT → T-0248..T-0252)** | **T-0248** A* ∥ **T-0249** B1 ∥ **T-0250** B3 ∥ **T-0251** C* *(excl. `disputes.facade.ts`)* ∥ **T-0252** E1/E2 | 5 children concurrent; serialize only on same-file. **Base dep for 5F/5G (T-0249→T-0202/T-0204; T-0251→T-0200/T-0204).** |
> | **5D — AUD-06 (T-0199 SPLIT → T-0253..T-0255) ALONE** | **T-0253**→**T-0254**→**T-0255** (serial a/b/c under the T-0212 net; T-0255 preserves the outbox seam) | **LANE-ISOLATED + SERIAL on `CreateOrder.cs`.** No other CreateOrder writer concurrent. Gate: T-0212 stays green+unmodified. |
> | **5E — de-triplication + AddSavedAddress** | **T-0198** (M, auth/dispute/saved-address controllers + login/forgot facades, sec-advisory) · **T-0201** (M, AddSavedAddress + B9 mapper) | Separate lanes; SavedAddress controllers (T-0198) vs handlers/mappers (T-0201) vs T-0249 DeleteSavedAddress command disjoint but same area — one lane. |
> | **5F — frontend rebuilds (after 5C)** | **[T-0256→T-0257→T-0258]** (AUD-07 order-wizard, SPLIT, serial) ∥ **T-0202** (disputes archetype, M, **regen-verify**) | Disjoint feature folders. AUD-07 chain downstream of T-0251; T-0202 downstream of T-0249 + regen-verify. |
> | **5G — perf cluster + tooling (after 5C)** | **T-0204** (M, **ef-migration**, optimizer, BLOCKED on T-0249/T-0251) ∥ **T-0247** (S, check-consistency rule, sec) | Parallel. T-0204 internal fan-out one dev/reviewer per repo group; rebases PERF-D2 on T-0249 B1. |
> | **5H — mobile ApiResult<T> (T-0197, L→split) — DEFER-CANDIDATE** | **T-0197** (architect ADR-first; one serial child per customer-app repo) | **Recommend defer to Wave 6** (owner call, sprint-7 §4.2). |
>
> | ID | Title | Size | Status | Batch | Layers | sec | manual_step |
> |----|-------|------|--------|-------|--------|-----|-------------|
> | T-0245 ⚠️ GO-LIVE BLOCKER | Multi-tenant webhook validator/handler tenant-scope mismatch | M | **done ✅** `3df53ab2` | 5A | backend | **yes** | — |
> | T-0246 | StartOrder handler NRE→500 on load divergence | S | **done ✅** `3df53ab2` | 5A | backend | no | — |
> | T-0243 | CheckoutSession `nameof(Command)`→`nameof(userId)` B5 | XS | **done ✅** | 5B | backend | no | — |
> | T-0244 | `GenerateVariableSymbol` deterministic stable hash | S | **done ✅** | 5B | backend | no (money-adv) | ef-migration* (not taken — stable-hash path) |
> | T-0205 | Remove dead/unsafe code (Handlebars/SendGrid/FCM/scrap) | S | **done ✅** | 5B | backend, mobile | no | — |
> | T-0206 | S6 logging hygiene (no PII/secrets in logs) | S | **done ✅** | 5B | backend, functions | no (advisory) | — |
> | T-0203 | LG/DA/IA long tail (B5/B1/CQRS/magic-strings/swallowed catch) | M | **done ✅** | 5B | backend, frontend | no | **nswag-regen (admin — owner)** |
> | T-0242 | Cancellation-fee Plus free-window override direction | S | **blocked (Q-W5-1) — CARRIED** | 5B | backend | no (money-adv) | — |
> | T-0196 | Mechanical consistency canonicalization sweep (A*/B1/B3/C*/E1/E2) | **L** | **done ✅ (epic — T-0248..T-0252 all done)** | 5C | backend, frontend, android | no | nswag-regen* |
> | T-0199 | AUD-06: decompose CreateOrder god-handler | **L** | **done ✅ (epic — T-0253..T-0255 all done)** | 5D | backend | no | — |
> | T-0198 | De-triplicate Dispute/SavedAddress/Auth controllers + login/forgot facades | M | **done ✅** (fixed real bugs: weak admin password + swallowed login/forgot errors) | 5E | backend, frontend | no (advisory) | — |
> | T-0201 | Decompose AddSavedAddress god-method + B9 mapper | M | **done ✅** | 5E | backend | no | — |
> | T-0200 | AUD-07: split order-wizard god-facade + C3 pipe | **L** | **done ✅ (epic — T-0256..T-0258 all done)** | 5F | frontend | no | — |
> | T-0202 | Customer disputes → own client + cleansia-table/form/error | M | **done ✅** | 5F | frontend | no | **nswag-regen (customer — owner)** |
> | T-0204 | PERF cluster: indexes, tracked reads, eager Includes, projection-before-order | M | **done ✅** (+ GDPR paging correctness fix + 4 indexes) | 5G | backend, db | no (optimizer) | **ef-migration (done; PROD = apply 4 indexes CONCURRENTLY by hand)** |
> | T-0247 | check-consistency rule: Dispute state-write allowlist *(re-id'd from T-0200; lives in T-0200-da-2-followup.md)* | S | **done ✅** | 5G | backend, tooling | yes | — |
> | T-0197 | Migrate customer-app repos to `ApiResult<T>` (mobile) | **L** | **DEFERRED to Wave 6** (draft, ADR-first) | 5H (defer) | architect, android, ios | no | — |
>
> **L-epic split children (created 2026-06-13, ids T-0248…T-0258) — the three L-epics above are now
> `in_progress` [SPLIT/EPIC] tracking tickets; each is `done` only when all its children are `done`:**
>
> | ID | Title | Size | Status | Batch | Parent | depends_on / blocks | Layers | manual_step |
> |----|-------|------|--------|-------|--------|---------------------|--------|-------------|
> | T-0248 | 5C.A A* canonical paged-query (PromoCodes/Referrals/PayConfigs/Services) | M | **done ✅** | 5C | T-0196 | — | backend | — |
> | T-0249 | 5C.B B1 Response-wrap (CreateDispute/UpdateDisputeStatus/DeleteSavedAddress) | S | **done ✅** | 5C | T-0196 | blocks T-0202, T-0204 | backend | nswag-regen* (conditional) |
> | T-0250 | 5C.C B3 validator-base composition (PayConfig/PayPeriod/Employee/CurrentUser) | S | **done ✅** | 5C | T-0196 | — | backend | — |
> | T-0251 | 5C.D C* customer/partner/admin facades (**EXCL `disputes.facade.ts`**) | M | **done ✅** | 5C | T-0196 | blocks T-0200, T-0204 | frontend | — |
> | T-0252 | 5C.E E1/E2 sealed Android UiState + shared ActionState | M | **done ✅** | 5C | T-0196 | — | android | — |
> | T-0253 | AUD-06a address-resolution + serviced-area collaborator | M | **done ✅** | 5D | T-0199 | dep T-0118✓/T-0212✓; blocks T-0254 | backend | — |
> | T-0254 | AUD-06b promo preview/apply collaborator | M | **done ✅** | 5D | T-0199 | blocks T-0255 | backend | — |
> | T-0255 | AUD-06c payment-dispatcher + late-referral + slim handler (preserves outbox seam) | M | **done ✅** | 5D | T-0199 | closes T-0199 | backend | — |
> | T-0256 | AUD-07a quote/pricing collaborator + C3-migrate stream | M | **done ✅** | 5F | T-0200 | blocks T-0257 | frontend | — |
> | T-0257 | AUD-07b promo+referral + city-serviced collaborators + drop `firstValueFrom` | M | **done ✅** | 5F | T-0200 | blocks T-0258 | frontend | — |
> | T-0258 | AUD-07c saved-address + slim facade (step-nav + submit) + C1/C3 submit branches | M | **done ✅** | 5F | T-0200 | closes T-0200 | frontend | — |
>
> \* `nswag-regen`/`ef-migration` fire **only if** the diff actually changes a generated-client surface or
> schema (**T-0249** B1 / T-0203 SendSitewidePromo+device-error / T-0202 customer-client / T-0244 persist-path)
> — the dev confirms at review; the PM adds the flag + holds consumers only then. **Owner manual steps this
> wave:** T-0204 ef-migration (4 indexes, CONCURRENTLY); see sprint-7 §4.3. **Dependency-ordered dispatch
> plan (post-split): sprint-7 §2.2** — {5B,5C,5D,5E} fan out concurrently → {5F,5G} after T-0249/T-0251 land →
> 5H deferred; **5C must complete before 5F/5G**; T-0242 stays BLOCKED on Q-W5-1.

**Wave-5 close follow-ups (filed 2026-06-14, all `draft`, Wave-6 candidates) — non-blocking findings the wave surfaced but (correctly) did NOT fold into the in-flight tickets. Sources in the rightmost column.**

| ID | Title | Size | Status | depends_on | Layers | sec | manual_step | Source |
|----|-------|------|--------|-----------|--------|-----|-------------|--------|
| **T-0259** | Frontend nx-lib test-infra scaffolding: tags + jest/eslint/tsconfig.spec targets for loyalty-promo-codes + customer login/forgot + partner-forgot libs | M | draft | — | frontend | no | — | T-0203 (nx config drift) + T-0198 (missing test targets) |
| **T-0260** | Funnel `HandleChargeback` dispute-terminal write through the T-0172 `CanTransitionTo` guard (not direct `Escalate`) — defense-in-depth | S | draft | T-0172✓, T-0247✓ | backend | **yes** | — | T-0247 finding (safe today: Pending→Escalated is legal) |
| **T-0261** | LG-PERF-06: UserMembership `(Status,CurrentPeriodEnd)` partial index `WHERE RenewalReminderSentAt IS NULL` doesn't cover the cancellation-reminder sweep arm | S | draft | T-0204✓ | db, backend | no | **ef-migration** (CONCURRENTLY, owner) | T-0204 finding |
| **T-0262** | Remove dead `BusinessErrorMessage.EmailNotSentError` constant (zero consumers) | S | draft | — | backend | no | — | T-0205 finding (no-decision mechanical cleanup) |

>
> ## ✅ WAVE 4 COMPLETE — tests + accessibility (11 of 11 done 2026-06-13)
> **Wave 3 merged to master: PR #76 (`05bf567a`).** Owner gave the go signal; Wave 4 = the test+a11y
> block **T-0210…T-0218** + carried **T-0179** (LG-07, not built in Wave 3) + **T-0235** (the T-0194
> AC6 runtime-429 deviation). Full plan + per-ticket stale-text deltas + the 4C close-out:
> **`status/sprint-6.md`** (§7 = 4A+4B, §8 = 4C).
> **Branch:** all work on `feature/wave-4-tests-a11y` (cut from `05bf567a`), committed batch-by-batch.
> **DONE: 11 of 11.** **Batch 4A** (T-0212/T-0211/T-0213/T-0214/T-0216/T-0179) + **Batch 4B**
> (T-0218/T-0217) landed orchestrator-verified green (**Cleansia.Tests 1311/1311**, frontend Jest green,
> customer prod build clean), committed **`6706d8d1`** + pushed. **Batch 4C** = **T-0210 / T-0215 /
> T-0235** (integration + host-runtime tests) **DONE 2026-06-13**, orchestrator-verified green against
> real Postgres (**HostTests 51/51, IntegrationTests 60/60, RateLimiting 65/65**). 4C surfaced **2
> confirmed production bugs** (test-only wave, correctly NOT fixed) → new tickets **T-0245** (multi-tenant
> webhook tenant-scope mismatch — **GO-LIVE BLOCKER**) + **T-0246** (StartOrder handler NRE→500). The 5
> Wave-4 carried follow-ups are **T-0242…T-0246**. Close-out: `status/sprint-6.md` §7 (4A+4B) + §8 (4C).
> **All `security_touching: false`** (tests/i18n/a11y/doc against existing behavior); adversarial/
> security-advisory review on T-0211 (money), T-0210 (signature lock), T-0215 (tenant boundary).
> Reviewer-per-developer on every ticket; QA = suite-green + AC↔test mapping (+ keyboard walkthrough
> on T-0218). **Resizes on verified dedup evidence: T-0213 L→M, T-0214 L→M** (Waves 0–3 TDD already
> shipped the bulk — both are now audit+gap-fill nets; if either regrows past M the dev stops and the
> PM splits). **Zero open dependencies; no intra-wave edges** — batching is shared-file lanes only.
> The consistency sweep **T-0196…T-0206 is NOT in this wave** (Wave-5 candidate, owner to confirm).
>
> | Batch | Tickets | Parallelism / lanes |
> |---|---|---|
> | **4A — backend unit nets** (`Cleansia.Tests`) | **T-0212** (CreateOrder characterization, M) ∥ **T-0211** (refund/dispute money-math gap-fill, M, adversarial review) ∥ **T-0213** (invoice/pay-period gap-fill, M) ∥ **T-0214** (per-Function coverage audit+gap-fill, M) ∥ **T-0216** (fiscal-mode matrix, M) ∥ **T-0179** (carried; doc+B5 rename+lock test, S) | All 6 parallel. Lane U1: edits to the same existing `Cleansia.TestUtilities` builder file serialize (Order builders: T-0211/T-0212). Lane U2: `Cleansia.Tests.csproj` already refs Functions(.Core) — no edit expected. |
> | **4B — frontend (customer app)** — runs ∥ 4A | **T-0218** (a11y: cleansia-* + order wizard, M) **→ then T-0217** (error-contract parity `api.*` ×5 locales + parity guard, M) | **STRICTLY SERIAL** — both edit the 5 customer locale JSONs. T-0218 is sole editor of `libs/shared/components/**` + `order-wizard/**` this wave. |
> | **4C — integration/host runtime** | **T-0210** (webhook integration + signature-stays-on, M) ∥ **T-0215** (cross-tenant/cross-user write-path integration, M) ∥ **T-0235** (runtime 429 flood harness, S, `Cleansia.HostTests`) | T-0210 ∥ T-0215 with Lane I1: any edit to `PostgresContainerFixture`/`BaseIntegrationTest`/`PostgresCollection` serializes. T-0235 parallel (separate project; touches no guard-test/policy/startup file). |
>
> **Gates/owner confirms (sprint-6 §4 — none blocks 4A/4B):** (1) confirm `Cleansia.IntegrationTests`
> green on master — the Users-lockout migration is verified **in-repo** (`20260612134125_Initial`),
> so 4C is not hard-blocked; the confirm formally closes **T-0193 AC4**; (2) customer nswag-regen
> still outstanding (no Wave-4 ticket consumes it); (3) confirm T-0196…T-0206 → Wave 5.
>
> | ID | Title | Size | Status | Batch | Layers | sec | manual_step |
> |----|-------|------|--------|-------|--------|-----|-------------|
> | T-0212 | TC-4: CreateOrder characterization tests | M | **done ✅** `6706d8d1` | 4A | backend | no | — |
> | T-0211 | TC-7: refund/dispute money-math gap-fill | M | **done ✅** `6706d8d1` | 4A | backend | no (adversarial) | — |
> | T-0213 | TC-6: invoice/numbering/pay-period tests (resized L→M) | M | **done ✅** `6706d8d1` | 4A | backend | no | — |
> | T-0214 | TC-8: per-Function coverage audit + gap-fill (resized L→M; 26 fns) | M | **done ✅** `6706d8d1` | 4A | backend | no | — |
> | T-0216 | TC-10: fiscal-mode selection characterization | M | **done ✅** `6706d8d1` | 4A | backend | no | — |
> | T-0179 | LG-07 (carried): unify membership subscribe path | S | **done ✅** `6706d8d1` (no regen) | 4A | backend, frontend | no | nswag-regen* (none needed) |
> | T-0218 | A11Y-1: a11y pass — cleansia-* + order wizard | M | **done ✅** `6706d8d1` | 4B (1st) | frontend | no | — |
> | T-0217 | EP-1/2/DA-7: error-contract parity ×5 locales | M | **done ✅** `6706d8d1` | 4B (2nd, after T-0218) | frontend | no | — |
> | T-0210 | TC-2/3: Stripe webhook integration + signature lock | M | **done ✅** | 4C | backend | no (advisory) | — |
> | T-0215 | TC-9: authz/cross-tenant write-path integration | M | **done ✅** | 4C | backend | no (advisory) | — |
> | T-0235 | Runtime 429 flood harness (T-0194 AC6) | S | **done ✅** | 4C | backend | no | — |
>
> **Batch 4C orchestrator-verified green** (real Postgres): **HostTests 51/51, IntegrationTests 60/60,
> RateLimiting 65/65**. (T-0235's AC3 named `Cleansia.HostTests` as the home, but the runtime limiter is
> only exercisable in `Cleansia.Tests/RateLimiting` — the existing harness home; AC3 intent satisfied,
> deviation D1 accepted.)
>
> **Wave-4 carried production findings → new tickets (all `draft`, Wave-5 candidates):**
> **T-0242** (cancellation-fee free-window override direction, from T-0211) · **T-0243**
> (CreateMembershipCheckoutSession `nameof` B5 consistency, from T-0179) · **T-0244**
> (EmployeeInvoice.GenerateVariableSymbol cross-process stable hash, from T-0213) · **T-0245**
> (multi-tenant Stripe webhook validator/handler tenant-scope mismatch — **GO-LIVE BLOCKER**, from T-0210) ·
> **T-0246** (StartOrder handler NRE→500 on validator/handler load divergence, from T-0215). Detail rows
> in the follow-up table below the Wave-3 roster.
>
> ## ✅ WAVE 3 CLOSED — admin-feature block T-0170…T-0195 (2026-06-12 reconciliation)
> **Wave 3** (26 tickets, 6 batches 3A–3F) is functionally complete on
> `feature/wave-3a-admin-order-dispute-ops` across four commits: **`8aa7bcc1`** (Batch 3A — admin order
> ops, dispute management, chargeback linkage + the citext runtime fix), **`5d631f8c`** (Batches
> 3B/3D/3C/3E backend — payroll lifecycle, Functions resilience, durable idempotency, membership/referral/
> device/profile/catalog admin ops), **`8ddfef9d`** (frontend mega-batch — payroll/membership/referral/
> GDPR/profile/catalog admin UIs, customer self-service, partner read-only pay, Android device management),
> **`66cc823d`** (Batch 3F — account lockout, S5 rate-limit closure, client Retry-After back-off).
> **25 of 26 reconciled `done ✅`** in the table below (the ticket files still read `draft`/`in-review`;
> PM reconciled status here, INDEX-side only, per the Wave-2 convention — no history rewrite).
> **EXCEPTION: T-0179 was NOT built** — verified: `CreateMembershipSubscription.cs` untouched since Wave 1,
> ticket file untouched since creation; it stays `draft` and **carries forward to Wave 4** (its T-0194 edge
> was satisfied-in-substance: the Subscribe endpoints got their rate-limit windows regardless; T-0179 is
> doc + B5-rename only). **ADR-0010 (durable consumer idempotency) was produced mid-wave** (the
> T-0181/T-0182 consumer-idempotency line) and is in force. **Deviations on the record:** T-0194 AC6 —
> runtime 429 flood harness deferred to the Wave-4 test slice (→ **T-0235**); T-0188 — optional AC6 admin
> device panel deferred (backend + Android shipped); T-0193 — AC4 verification **closes only after** the
> owner applies the Users lockout ef-migration and `Cleansia.IntegrationTests` runs green.
> **Owner steps PENDING:** ef-migration (4 additive `Users` lockout columns) + nswag-regen (customer
> client: `DisputeReason.Chargeback` + device endpoints) — detail in `status/sprint-5.md` §8.
> **Review-generated follow-ups filed (all `draft`): T-0233…T-0241** — see the follow-up table below the
> Wave-3 roster. Q-W3-1 answered (path b — no `Language.IsDefault`); T-0191 sub-(d) shipped against it.
>
> ## ✅ WAVE 2 CLOSED — merged to master (2026-06-09 reconciliation)
> **Wave 2** (the refund money-path epic + per-included-service package-pricing + fiscal go-live gates +
> fast-follows) = merged in **`8ff35d49` (PR #75).** The 12 Wave-2 ticket files still read `status: draft`
> in their frontmatter (the plan was never marked executed); the PM reconciled them to **`done ✅`** here,
> status-reconciliation only (no history rewrite). **Shipped & now `done`:** **T-0160** (Refund entity +
> enums), **T-0161** (IRefundService seam + key param), **T-0163** (loyalty partial-refund clawback),
> **T-0164** (CancelOrder/ResolveDispute migrated onto the seam), **T-0167** (admin partial-refund cmd +
> allocator + RefundPolicy + per-country Stripe-fee config), **T-0168** (admin refund UX incl. bundled-
> service selection), **T-0231** (PackageService.PriceWeight + the T-0231b extension exposing PriceWeight +
> serviceWeights on the package DTO), **T-0232** (admin package-weight UX), **T-0219** (anon-catalog →
> platform config), **T-0220** (FiscalCounter gapless allocator), **T-0221** (IFiscalService register
> idempotency key), **T-0222** (pay-split rounding). Plus two runtime fixes folded into the PR
> (OutboxMessageRepository non-composable FromSqlRaw; AppHost pinned Postgres password) and the new backend
> DTO field `PackageDetails.IncludedServiceItems [{Id,Name}]`. Split epics **T-0162**/**T-0165** remain
> `[SPLIT]` tracking epics — all four children (T-0167/T-0168/T-0231/T-0232) `done`. **Q-REFUND-03**
> (per-bundle weights) stays open/non-blocking — owner sets weights via T-0232 or confirms even-split.
>
> ## 🟡 WAVE 3 PLANNED — admin-feature block T-0170…T-0195 *(superseded by the WAVE 3 CLOSED banner above; kept for traceability)*
> Full sequenced plan: **`status/sprint-5.md`**. **No new ADR gates Wave 3** — ADR-0001 (authz, frozen
> map), ADR-0002 (outbox/dispatch), ADR-0006/0009 (refund seam + policy) are all `accepted` and freeze
> every decision the 26 tickets consume; Wave 3 is pure BUILD against accepted contracts.
>
> **Scope (26 tickets, 6 batches).** **Batch 3A — refund-seam consumers (the spine):** **T-0170** (admin
> order ops, `L`→split), **T-0172** (dispute transition-guard), **T-0174** (chargeback linkage), then
> **T-0173** (admin dispute mgmt + issue refund, `L`→split). **Batch 3B — payroll lifecycle:** **T-0171**
> (`L`→split) then **T-0180** (GenerateInvoiceFunction). **Batch 3C — loyalty/membership/referral:**
> **T-0175** (`L`→split), **T-0176**, **T-0177**, **T-0178**, **T-0179**. **Batch 3D — Functions resilience
> fast-follows:** **T-0181**, **T-0182**, **T-0183**, **T-0184**, **T-0185**. **Batch 3E —
> identity/GDPR/device/catalog:** **T-0186** (`L`→split), **T-0187**, **T-0188**, **T-0189**, **T-0190**,
> **T-0191** (`L`→split), **T-0192**. **Batch 3F — rate-limit fast-follows:** **T-0193**, **T-0194**,
> **T-0195**.
>
> **L-splits authorized this pass (5):** **T-0170** → 170a generalized-cancel+CancelledBy enum (folds
> AUD-15) / 170b status-override / 170c reassign / 170d refund-only; **T-0173** → 173a backend (Admin
> DisputeController + Partner-endpoint removal + refund/guard) / 173b admin disputes-management frontend;
> **T-0171** → 171a invoice adjust+dispute/reject / 171b period MarkPaid+Reopen / 171c AUD-04 partner-
> surface reconciliation / 171d admin UI / 171e partner web+Android read-only; **T-0175** → 175a backend /
> 175b admin frontend; **T-0186** → 186a admin Data-Protection / 186b partner GDPR self-service. **T-0191**
> stays one ticket with internal split-(a/b/c/d) sub-sequencing (CC-06 sub-(d) held on Q-W3-1).
>
> **Corrected/verified edges (post Wave-2):** **T-0170** `depends_on T-0161✓, T-0164✓` (refund seam +
> migration — both now `done`, so T-0170 is **unblocked**); **T-0173** `depends_on T-0161✓, T-0164✓, T-0172,
> T-0171` (so 3A's dispute spine + 3B's payroll spine gate it). **All other Wave-3 deps verified `done`:**
> T-0100, T-0111, T-0112, T-0115, T-0141, T-0142(epic children), T-0143(epic children), T-0145, T-0148.
>
> **Open question:** **Q-W3-1** (blocking) — default-language policy for catalog translations (gates ONLY
> T-0191 CC-06 sub-(d); the rest of T-0191 and all of Wave 3 proceed). Plus **carry-forward owner items**
> (not Wave-3 tickets) tracked in sprint-5 §3: **T-0159 rotate-mapbox-token** (still outstanding),
> outstanding Wave-0 nswag-regens (T-0102/0104/0111/0112 — confirm), IMP-1 Google OAuth ClientId, CZ
> Stripe-fee figure, fiscal go-live gates DE/AT/ES.
>
> --- (Wave-1 history below, kept for traceability) ---
>
> ## ✅ WAVE 1 CLOSED — merged to master (2026-06-07 reconciliation)
> **Wave 0** = PR #72 (`9a774435`); **Wave 1 Batch 1A** (4 ADRs) + **Batch 1B** (T-0144…T-0159) = merged in
> `a4f14094` ("Wave-1 Batch 1B — integration resilience, outbox durability, soft-delete, loyalty/membership
> hardening"). **Local master == origin/master == a4f14094.** The PM reconciled the 14 Batch-1B tickets that
> still read `ready`/`draft` to **`done`** (status-log line on each); T-0166 hotfix already `done`. All four
> Wave-1 ADRs (0005/0006+0009/0007/0008) `accepted`.
>
> ## 🟡 WAVE 2 PLANNED — refund epic + fiscal go-live gates (proposed; awaiting owner sign-off)
> Full sequenced plan: **`status/sprint-4.md`**. **No new ADR gates Wave 2** — ADR-0006 (seam) + ADR-0009
> (policy) are `accepted` and freeze every refund decision; the Wave-2 refund tickets are pure BUILD.
>
> **Scope (12 tickets, refund foundation = the spine):** **T-0160** entity+enums → **T-0161** seam, **T-0163**
> loyalty revoke, **T-0231** package PriceWeight (all parallel-ish) → **T-0164** migrate cancel/dispute,
> **T-0167** admin refund cmd (depends on **T-0231** — AUD-02p→AUD-01c cross-edge) → **T-0168** admin UX,
> **T-0232** weight UX; plus the independent **T-0220/T-0221** fiscal go-live gates (DE/AT/ES), **T-0219**
> anon-catalog, **T-0222** pay-split rounding.
>
> **L-splits (this pass):** **T-0162** (AUD-01c) → **T-0167** (backend) + **T-0168** (frontend); **T-0165**
> (AUD-02p) → **T-0231** (db+backend) + **T-0232** (frontend). Parents T-0162/T-0165 are `[SPLIT]` tracking
> epics. The old `T-0162 depends_on T-0165` edge is now **T-0167 depends_on T-0231**.
>
> **Corrected edges:** T-0170 (admin order ops) + T-0173 (admin dispute mgmt) now `depends_on` the refund
> seam (T-0161) + seam migration (T-0164); both **deferred to Wave 3** (the admin-feature block).
>
> **Q-REFUND-03** (non-blocking) remains the one open item — even-split backfill ships in T-0231; owner sets
> per-bundle weights via T-0232.
>
> --- (Wave-1 history below, kept for traceability) ---
>
> **Batch 1A — the 4 ADRs — all `done`.** T-0141 → **ADR-0005** (integration), T-0140 → **ADR-0006** (refund
> seam) + superseding **ADR-0009** (refund policy), T-0152 → **ADR-0007** (soft-delete), T-0155 →
> **ADR-0008** (outbox table + drainer).
>
> **L-splits (Q-W1-2):** T-0142 → T-0152/T-0153/T-0154 (a→{b∥c}); T-0143 → T-0155/T-0156/T-0157/T-0158
> (a→b→c→d serial). Parents T-0142/T-0143 are `[SPLIT]` epics (tracking only). BLIND-2 = T-0159.

### Wave 1 — live roster (updated 2026-06-06)

**Batch 1A — the 4 ADRs — `done` ✅ (reviewer-reconciled 2026-06-06). The gate is cleared.**

| ID | Title | Size | Status | ADR produced | blocks | Layers |
|----|-------|------|--------|--------------|--------|--------|
| **T-0141** | ADR-INTEGRATION (IHttpClientFactory + error-class + async-email) | M | **done ✅** | ADR-0005 | T-0144→T-0145, T-0146, T-0147 | architect, backend |
| **T-0140** | ADR-REFUND (refund/dispute money path + chargeback) | M | **done ✅** | ADR-0006 + **ADR-0009** | T-0160…T-0165 (Wave-2) | architect, backend |
| **T-0152** | ADR: soft-delete policy (Deactivate vs Remove) | M | **done ✅** | ADR-0007 | T-0153, T-0154, T-0191 | architect |
| **T-0155** | ADR: outbox table + in-Functions drainer (ADR-0002 D1.3) | M | **done ✅** | ADR-0008 | T-0156→T-0157→T-0158 | architect |

**Batch 1B — contract/plumbing code. ALL `done` ✅ (merged in `a4f14094`; PM-reconciled 2026-06-07).**

| ID | Title | Size | Status | depends_on | Layers | sec | manual_step |
|----|-------|------|--------|-----------|--------|-----|-------------|
| T-0150 | Centralize CZE/Mapbox-bounds/2000-char constants | S | **done ✅** | — | backend, frontend, android | no | — |
| T-0149 | Refresh-token rotation re-checks profile (per host) | S | **done ✅** | T-0100✓ | backend | **yes** | — |
| **T-0159** | BLIND-2: Mapbox token in request URL → correct auth + scrub logs + rotate | S | **done ✅** | — | frontend, config | **yes** | rotate-mapbox-token ⚠️ **still outstanding (owner)** |
| T-0144 | Stripe + SendGrid via IHttpClientFactory (ADR-0005) | M | **done ✅** | T-0141✓ | backend | no | — |
| T-0146 | Registration/reset email off critical path (async, ADR-0005 D3) | M | **done ✅** | T-0141✓, T-0118✓ | backend, functions | **yes** | — |
| T-0147 | Membership commands: provider try/catch + S7 (ADR-0005 D4) | M | **done ✅** | T-0141✓ | backend | **yes** | — |
| T-0148 | Tier-threshold config read + persist grant/revoke Reason | M | **done ✅** | T-0112✓ | backend | no | — |
| T-0153 | SavedAddress soft-delete + IsActive filters + null-FK + migration (ADR-0007) | M | **done ✅** | T-0152✓ | backend, db | no | ef-migration |
| T-0154 | Device soft-delete verdict (UnregisterDevice, ADR-0007) | S | **done ✅** | T-0152✓ | backend | no | — |
| T-0156 | Outbox table + EF config + migration flag (ADR-0008) | S | **done ✅** | T-0155✓ | db | no | ef-migration |
| T-0151 | Migrate remaining queue consumers onto Functions.Core | M | **done ✅** | T-0121✓ | functions | no | — |
| T-0145 | Error classification across integration layer | M | **done ✅** | T-0141✓, T-0144✓ | backend | no | — |
| T-0157 | Durable IPendingDispatch backing + drainer + host (ADR-0008) | M | **done ✅** | T-0156✓, T-0118✓ | backend, functions | no | — |
| T-0158 | Bucket-B sweeps migrate onto per-iteration outbox row | M | **done ✅** | T-0157✓, T-0148✓ | backend | no | — |

> **Batch 1B = 14 `done`** (merged `a4f14094`). Reconciled 2026-06-07 from stale `ready`/`draft`. The only
> residual owner action is **T-0159's `rotate-mapbox-token`** — the code fix shipped (token off the URL) but
> the exposed token still needs rotating in the Mapbox account (a live exposure until done). Surfaced in
> `status/sprint-4.md` §3.

**Wave 2 — refund BUILD from ADR-0006/0009 + fiscal go-live gates + fast-follows. ALL `done` ✅ (merged in `8ff35d49` / PR #75; PM-reconciled 2026-06-09 from stale `draft`). Plan: `status/sprint-4.md`.**

| ID | Title | Size | Status | depends_on | blocks | Layers | sec | manual_step |
|----|-------|------|--------|-----------|--------|--------|-----|-------------|
| **T-0160** | AUD-01a: Refund entity + EF + PaymentStatus.PartiallyRefunded + RefundReason enum | M | **done ✅** | — | T-0161, T-0163, T-0164, T-0167 | backend, db | no | ef-migration |
| **T-0161** | AUD-01b: IRefundService impl (seam, ceiling, RefundKey) + IStripeClient key param | M | **done ✅** | T-0160 | T-0164, T-0167, T-0170, T-0173 | backend, clients | **yes** | nswag-regen* |
| **T-0231** | AUD-02p1 (split of T-0165): PackageService.PriceWeight + even-weight backfill + bundled-gross (incl. T-0231b: PriceWeight + serviceWeights on package DTO) | M | **done ✅** | — | **T-0167**, T-0232 | db, backend | no | ef-migration |
| **T-0163** | AUD-01d: ILoyaltyService.RevokeForPartialRefundAsync (proportional, keyed) | M | **done ✅** | T-0160 | — | backend, db | no | ef-migration |
| **T-0164** | AUD-01e: Migrate CancelOrder + ResolveDispute onto the seam | M | **done ✅** | T-0160, T-0161 | T-0170, T-0173 | backend | **yes** | — |
| **T-0167** | AUD-01c1 (split of T-0162): admin partial-refund cmd + allocator + RefundPolicy + PartiallyRefunded + per-country Stripe-fee config | M | **done ✅** | T-0160, T-0161, **T-0231** | T-0168, T-0170, T-0173 | backend | **yes** | nswag-regen |
| **T-0168** | AUD-01c2 (split of T-0162): admin partial-refund UX (incl. bundled-service selection) | M | **done ✅** | T-0167 | — | frontend | no | nswag-regen (consumes) |
| **T-0232** | AUD-02p2 (split of T-0165): admin package-form weight UX | S | **done ✅** | T-0231 | — | frontend | no | nswag-regen (consumes) |
| **T-0220** | FISCAL-SEQ: gapless fiscal sequence allocator (FiscalCounter) — **DE/AT/ES go-live gate** | M | **done ✅** | T-0119✓ | — | backend, db | **yes** | ef-migration |
| **T-0221** | FISCAL-AUTH-IDEMP: per-provider RegisterReceiptAsync idempotency — **DE/AT/ES go-live gate** | M | **done ✅** | T-0119✓ | — | backend, clients | **yes** | — |
| **T-0219** | Anon-catalog entities → platform config (Service/Category/Package/Extra/ServiceCity) | M | **done ✅** | T-0100✓, T-0113✓ | — | backend, db | **yes** | ef-migration |
| **T-0222** | SplitPayForMultipleEmployees — currency-minor-unit split + remainder reconciliation | S | **done ✅** | — | — | backend | no | — |

> **Wave 2 = 12 `done`** (merged `8ff35d49` / PR #75). Reconciled 2026-06-09 from stale `draft`. Plus the
> new backend DTO field `PackageDetails.IncludedServiceItems [{Id,Name}]` and two runtime fixes folded in
> (OutboxMessageRepository non-composable FromSqlRaw; AppHost pinned Postgres password). Split epics
> **T-0162**/**T-0165** remain `[SPLIT]` tracking with all four children `done`. The fiscal go-live gates
> (T-0220/T-0221) are `done` in code but only **activate** on a DE/AT/ES launch — not CZ/SK/PL (see
> `status/sprint-5.md` §3 carry-forward).

**Wave 3 — admin-feature block T-0170…T-0195. ✅ CLOSED 2026-06-12 — 25/26 `done` (T-0179 NOT built, carried forward). Commits: `8aa7bcc1` (3A) → `5d631f8c` (backend 3B/3D/3C/3E) → `8ddfef9d` (frontend/Android mega-batch) → `66cc823d` (3F). Q-W3-1 answered (b). Plan + close-out: `status/sprint-5.md`.**

| ID | Title | Size | Status (commit) | depends_on (✓ = done) | Batch | Layers | sec | manual_step |
|----|-------|------|--------|------------------------|-------|--------|-----|-------------|
| **T-0170** | Admin order ops (cancel/reassign/refund/status-override) + generalized cancel | **L→split** | **done ✅** `8aa7bcc1` (170a–d + UI) | T-0100✓, T-0140✓, T-0161✓, T-0164✓ | 3A | backend, frontend | **yes** | nswag-regen ✓ |
| **T-0172** | Dispute transition-guard: Close/Escalate/LinkStripe reachable + guarded | M | **done ✅** `8aa7bcc1` | T-0140✓ | 3A | backend | **yes** | — |
| **T-0174** | Wire Stripe chargeback linkage (LinkStripeDispute) | M | **done ✅** `8aa7bcc1` | T-0140✓ | 3A | backend | **yes** | — |
| **T-0173** | Admin dispute management + issue refund; remove dead Partner endpoints | **L→split** | **done ✅** `8aa7bcc1` (173a+173b) | T-0100✓, T-0140✓, T-0161✓, T-0164✓, T-0172✓, T-0171✓ | 3A | backend, frontend | **yes** | nswag-regen ✓ |
| **T-0171** | Payroll adjustment + settlement lifecycle + partner payroll surface | **L→split** | **done ✅** `5d631f8c` (171a/b/c) + `8ddfef9d` (171d/e UI + Android) | T-0100✓, T-0143✓, T-0170✓ | 3B | backend, frontend, android | **yes** | nswag-regen ✓, ef-migration (none needed) |
| **T-0180** | Implement GenerateInvoiceFunction (revive generate-invoice queue) | S | **done ✅** `5d631f8c` | T-0143✓, T-0171✓ | 3B | functions | no | — |
| **T-0175** | Admin Membership-Plan CRUD surface | **L→split** | **done ✅** `5d631f8c` (175a) + `8ddfef9d` (175b) | T-0100✓, T-0173✓ | 3C | backend, frontend | **yes** | nswag-regen ✓ |
| **T-0176** | Admin referral intervention + wire by-user endpoint + sidebar | M | **done ✅** `5d631f8c` + `8ddfef9d` | T-0100✓, T-0148✓, T-0175✓ | 3C | backend, frontend | **yes** | nswag-regen ✓ |
| **T-0177** | Invoke referral expiry sweep (timer) | S | **done ✅** `5d631f8c` | T-0143✓ | 3C | backend, functions | no | — |
| **T-0178** | /r/{code} referral landing route | M | **done ✅** `8ddfef9d` | — | 3C | frontend | no | — |
| **T-0179** | Unify membership subscribe path (web/mobile) | S | **⚠️ NOT BUILT in Wave 3 — carried; now `ready` in Wave-4 Batch 4A** (verified: `CreateMembershipSubscription.cs` untouched since Wave 1) | T-0111✓ | 3C→4A | backend, frontend | no | nswag-regen* |
| **T-0181** | SendSitewidePromo fan-out: resume cursor + idempotent enqueue | M | **done ✅** `5d631f8c` | T-0143✓ | 3D | functions, backend | **yes** | — |
| **T-0182** | Idempotent push dispatch (per-message key; fix at-most-once) | M | **done ✅** `5d631f8c` (+ **ADR-0010** produced) | T-0143✓, T-0141✓ | 3D | functions, backend | **yes** | — |
| **T-0183** | Fix cron cadence on 4 notification/recurring timers | S | **done ✅** `5d631f8c` | — | 3D | functions | no | — |
| **T-0184** | FiscalRetryService per-receipt durability (no all-or-nothing batch) | S | **done ✅** `5d631f8c` | T-0143✓ | 3D | backend | no | — |
| **T-0185** | Mapbox 429/rate-limit handling | M | **done ✅** `5d631f8c` | T-0141✓, T-0145✓ | 3D | backend | no | — |
| **T-0186** | Admin GDPR back-office UI + partner GDPR self-service | **L→split** | **done ✅** `5d631f8c` + `8ddfef9d` (186a/b) | T-0100✓, T-0176✓ | 3E | backend, frontend | **yes** | nswag-regen ✓ |
| **T-0187** | Customer-web notification-preferences UI (11-category API) | M | **done ✅** `8ddfef9d` | — | 3E | frontend | no | — |
| **T-0188** | Device / active-session management (GetMyDevices + revoke UI) | M | **done ✅** `5d631f8c` (backend) + `8ddfef9d` (Android) — optional AC6 admin panel **deferred** | — | 3E | backend, frontend, mobile | **yes** | nswag-regen ⚠️ customer client pending |
| **T-0189** | LastLoginAt tracking (field + write + surface) | M | **done ✅** `5d631f8c` | — | 3E | backend, db, frontend | no | ef-migration ✓ |
| **T-0190** | Admin self-service profile/password; accept BirthDate/PreferredLanguageCode | M | **done ✅** `5d631f8c` + `8ddfef9d` | T-0100✓, T-0172✓ | 3E | backend, frontend | no | nswag-regen ✓ |
| **T-0191** | Service/Package in-use guard + activate/deactivate; default-currency/-language | L (internal split a/b/c/d) | **done ✅** `5d631f8c` (a–d backend; CC-06 per Q-W3-1 path b) + `8ddfef9d` (UI) | T-0142✓ | 3E | backend, frontend | **yes** | ef-migration (none needed), nswag-regen ✓ |
| **T-0192** | Customer dispute evidence+refund UI; status filter/unread; saved-address UI | M | **done ✅** `8ddfef9d` | — | 3E | frontend | no | — |
| **T-0193** | Account-lockout / per-confirmation-code throttle (rate-limit fast-follow) | M | **done ✅** `66cc823d` (⚠️ **AC4 closes after owner ef-migration + `Cleansia.IntegrationTests`**) | T-0115✓, T-0189✓, T-0190✓ | 3F | backend, db | **yes** | **ef-migration ⚠️ PENDING (owner)** |
| **T-0194** | Rate-limit coverage for uncovered money/side-effect endpoints | S | **done ✅** `66cc823d` (recorded **AC6 deviation** — runtime 429 harness → **T-0235**, Wave 4) | T-0115✓, T-0171✓, T-0173✓, T-0179 (waived — doc-only, endpoints annotated regardless), T-0188✓ | 3F | backend | **yes** | — |
| **T-0195** | Client-side Retry-After back-off jitter (SPA + mobile) | S | **done ✅** `66cc823d` | T-0115✓ | 3F | frontend, mobile | no | — |

> \* T-0179's `nswag-regen` footnote is moot until it is built (likely comment-only → no regen). The
> T-0176/T-0190 hold-point regens were satisfied by the owner mid-wave (the `8ddfef9d` frontend slices
> built against the regenerated admin client). **Still pending: the customer-client regen**
> (`DisputeReason.Chargeback` + device endpoints) — flagged in the Wave-3 CLOSED banner + sprint-5 §8.

**Wave-3 close follow-ups (filed 2026-06-12, all `draft`) — review/security-gate findings made tickets. T-0236 MUST land before any multi-tenant onboarding; T-0233/T-0234 are security fast-follows.**

| ID | Title | Size | Status | depends_on | Layers | sec | manual_step | Source |
|----|-------|------|--------|-----------|--------|-----|-------------|--------|
| **T-0233** | Targeted-lockout DoS mitigation — trusted-device bypass / CAPTCHA on locked-account login | M | draft | T-0193✓ | backend, frontend | **yes** | — | T-0193 security note N1 |
| **T-0234** | Bound ChangeOwnPassword current-password guessing (authenticated surface) | S | draft | T-0193✓ | backend | **yes** | — (ef-migration only if a dedicated counter is chosen) | T-0193 security note N5 |
| **T-0235** | Runtime 429 flood-harness test (the T-0194 AC6 deviation; Wave-4 test slice) | S | **ready** (Wave-4 Batch 4C) | T-0194✓ | backend | no | — | T-0194 AC6 deviation |
| **T-0236** | Multi-tenant token-revoke asymmetry: TenantId=null token writes vs tenant-filtered revoke reads | M | draft | T-0188✓ | backend | **yes** | ef-migration (TBD at contract-lock) | T-0188 security note; `security/auth-sessions.md` |
| **T-0237** | Catalog delete TOCTOU → FK Restrict + violation→`in_use` mapping; + RecurringBookingTemplate JSON-id dangling refs | M | draft | T-0191✓ | backend, db | **yes** | ef-migration | T-0191a security re-gate notes 1+2 |
| **T-0238** | EmployeeInvoice DTOs gain PdfGenerationFailed/PdfGenerationError + admin regen (closes Q-W3-3 / T-0171d AC4) | S | draft | T-0171✓ | backend, frontend | no | nswag-regen | Q-W3-3 |
| **T-0239** | Module-boundary sweep: customer features off `@cleansia/partner-services` (14 files) + eslint boundary rule | M | draft | — | frontend | no | — | Wave-3 review finding |
| **T-0240** | Android `.kotlin` build-artifact dir → `.gitignore` | S | draft | — | android | no | — | T-0195 reviewer nit |
| **T-0241** | Admin-app selector-prefix eslint alignment + Nx generator default | S | draft | — | frontend | no | — | recurring 3A+ baseline noise |

**Wave-4 close follow-ups (filed 2026-06-13, all `draft`, Wave-5 candidates) — production findings the test wave uncovered but (correctly) did NOT fix in a test-only wave. T-0242–T-0244 from 4A; T-0245/T-0246 from 4C. ⚠️ T-0245 is a MULTI-TENANT GO-LIVE BLOCKER (must land before any multi-tenant onboarding, alongside T-0236).**

| ID | Title | Size | Status | depends_on | Layers | sec | manual_step | Source |
|----|-------|------|--------|-----------|--------|-----|-------------|--------|
| **T-0242** | Cancellation-fee free-window override semantics: larger Plus override makes the free window STRICTER, contradicting "Plus = more generous" — confirm intent + fix direction (either smaller override on the Plus path or invert override semantics) + update the T-0211 pinning tests | S | draft | T-0211✓ | backend | no (money — adversarial review) | — | T-0211 (TC-7) carried finding |
| **T-0243** | `CreateMembershipCheckoutSession` `UserNotFound` uses `nameof(Command)` → `nameof(userId)` (same B5 smell T-0179 fixed in the sibling handler, scoped out there); mechanical rename, pin if practical | XS | draft | T-0179✓ | backend | no | — | T-0179 (LG-07) carried finding |
| **T-0244** | `EmployeeInvoice.GenerateVariableSymbol` uses per-process-randomized `string.GetHashCode()` (cross-process recompute → silent fiscal/payment-reference mismatch); replace with a deterministic stable hash (or persist-and-never-recompute) + cross-invocation determinism test | S | draft | T-0213✓ | backend | no | ef-migration (only if persist-and-never-recompute is chosen) | T-0213 (TC-6) carried finding |
| **T-0245** ⚠️ **MULTI-TENANT GO-LIVE BLOCKER** | Multi-tenant Stripe webhook validator/handler tenant-scope mismatch: order-exists VALIDATOR rule (`BaseRepository.ExistsAsync`) is tenant-scoped while the handler read (`GetByIdIgnoringTenantAsync`) is tenant-ignoring → a non-null-tenant paid `checkout.session.completed` FAILS VALIDATION and the order is never confirmed/paid (silent money/lifecycle failure). Masked today (web Checkout is single-tenant, `TenantId==null`). Fix: tenant-ignoring existence check + non-null-tenant integration test. Sibling of T-0236. | M | draft | T-0210✓ | backend | **yes** | — | T-0210 (TC-2/3) review + Security; verified by 4C webhook suite |
| **T-0246** | StartOrder handler NRE→500 on validator/handler load divergence: `StartOrder.cs:137` `order!.StartOrder()` derefs an unguarded Include-shaped `FirstOrDefaultAsync` while the validator (`:45`) gated existence via `ExistsAsync` (a different query path); when they disagree the handler NREs into a 500 instead of a clean business not-found. Reproduced live on the Mobile partner host with tenant-consistent seed data. Fix: guard the null load (`OrderNotFound`) + reconcile handler query with validator + regression test. | S | draft | T-0215✓ | backend | no | — | T-0215 (TC-9) Ac14 carried finding |
>
> **L-splits authorized (5)** — children created as part of execution intake, contract-first per
> `routing.md`: **T-0170**→170a/b/c/d, **T-0173**→173a/b, **T-0171**→171a/b/c/d/e, **T-0175**→175a/b,
> **T-0186**→186a/b. Parents become `[SPLIT]` tracking epics. **T-0191** keeps its id but runs as four
> internal sub-tickets (a CC-02 / b CC-03 / c CC-04 / d CC-06); sub-(d) is **held on Q-W3-1**.
>
> **Build order:** 3A (refund-seam consumers — the spine) → 3B (payroll, gated by 3A's T-0170) → {3C, 3D,
> 3E} largely parallel after their spines, with the dispute-backend serialization cluster
> (T-0172 → T-0173) and the PolicyBuilder/admin-shell clusters serializing inside 3A/3C/3E → 3F last
> (T-0194 depends on 3B/3A/3C consumers existing; T-0193 depends on T-0189/T-0190). Per-batch rationale +
> serialization detail: `status/sprint-5.md`.

> \* T-0161 `nswag-regen` only if a refund **response DTO** surfaces on a client; the admin refund command DTO
> regen is on **T-0167**.
>
> **Split epics (tracking only):** **T-0162** (AUD-01c, `L`) → **T-0167** + **T-0168**; **T-0165** (AUD-02p,
> `L`) → **T-0231** + **T-0232**. The old `T-0162 depends_on T-0165` edge is now **T-0167 depends_on T-0231**.
>
> **Load-bearing cross-edge (DAG over id order): AUD-02p1 (T-0231) → AUD-01c1 (T-0167)** — a bundled service
> has no gross until `PriceWeight` exists; T-0231 must be `done` before T-0167 goes `ready`.
> **Q-REFUND-03** (non-blocking) gates only T-0231's per-bundle *business* weighting (even-split default
> ships; owner sets weights via T-0232). The admin-feature consumers **T-0170/T-0173** now depend on the
> refund seam + seam migration and are **Wave 3**, not Wave 2.

**Split epics (tracking only — do not run as one ticket):**

| ID | Title | Status | Split into |
|----|-------|--------|-----------|
| T-0142 | [SPLIT] ADR + soft-delete sweep | draft (epic) | T-0152 → {T-0153 ∥ T-0154} |
| T-0143 | [SPLIT] Full transactional outbox | draft (epic) | T-0155 → T-0156 → T-0157 → T-0158 |

> ## 📋 FULL TICKETED BACKLOG — 87 tickets, all waves (2026-06-01)
> Every wave is now ticketed as a file in `tickets/` (collision-checked twice; 18 serializing
> `depends_on` edges applied). Dependency graph + shared-file serialization clusters: `TICKET-MAP.md`.
> All 3 gating ADRs accepted (0001 authz, 0002 outbox, 0003 ratelimit). All `draft` → PM promotes to
> `ready` wave by wave. Built **test-first (TDD)**; reviewer + security run in parallel per ticket.
>
> | Wave | Ids | Count | What |
> |---|---|---|---|
> | **0 — PROD gate** | T-0100…T-0128 | 29 | security/correctness blockers + the Wave-0 test slice |
> | **1 — ADRs + contracts** | T-0140…T-0151 | 12 | ADR-REFUND, ADR-INTEGRATION, soft-delete, full outbox, integration plumbing |
> | **2 — features (story-backed)** | T-0170…T-0195 | 26 | admin order ops, payroll, disputes, membership/referral/GDPR/device, catalog activate/deactivate, rate-limit fast-follows |
> | **3 — consistency & quality** | T-0196…T-0206 | 11 | the 187 canonicalization sweep, god-unit decomposition, de-triplication, dead/unsafe code, S6 logging, perf |
> | **4 — tests + a11y** | T-0210…T-0218 | 9 | webhook/refund/invoice/Functions/authz/fiscal integration tests, error-contract parity, accessibility |
>
> **Execution order:** strictly wave-by-wave (Wave N fully `done` before N+1 opens). Within a wave the
> PM fans out by `depends_on`; the serialization clusters prevent same-file races. **Wave 0 is the PROD
> gate — nothing ships to prod until it's green.** Per-ticket detail is in each `tickets/T-NNNN-*.md`.

> ## 🔴 WAVE 0 — PROD-BLOCKING (from the COMPLETE audit, 2026-06-01)
> The full audit overturned the earlier "no security defect" verdict: **8 of 9 criticals are security
> defects.** **Nothing ships to PROD until Wave 0 is green.** Full plan + verdicts:
> `audits/AUDIT-2026-06-01-execution-plan.md`. Findings: `audits/AUDIT-2026-06-01-findings.md`.
> Stories (83): `stories/AUDIT-2026-06-01-user-stories.md`. **Everything is built test-first (TDD).**
> **FUP-1 (the suspected webhook-signature gap) is REFUTED** — verification proved signature
> verification is present; residual SEC-W2/W3 tracked below.

| ID | Title | Wave | Sev | Size | Status | Layers | ADR |
|----|-------|------|-----|------|--------|--------|-----|
| BSP-1 (+BSP-6) → **T-0100** | One PolicyBuilder ticket: fail-closed fallback + complete Map + startup assertion (BSP-6 merged in) | 0 | crit | M | **done ✅** | backend, config | ADR-AUTHZ (pre-decided) |
| IDA-SEC-01 → **T-0105** | Google sign-in trusts client email/GoogleId → verify ID-token claims server-side | 0 | crit | M | **done ✅** (⚠️ owner: IMP-1 ClientId for live OAuth) | backend | ADR-AUTHZ (S1/D5) |
| IDA-SEC-03 → **T-0106** | Reset/confirm codes 6-digit non-crypto, looked up by code → crypto tokens + scoped lookup | 0 | crit | M | **done ✅** (migration regenerated 2026-06-03: 64-char token cols in Initial) | backend, db | — |
| SEC-DSP-01 → **T-0102** | `IsStaffMessage` client-supplied → derive staff flag from caller role | 0 | crit | S | **done ✅** (⚠️ owner: nswag-regen) | backend, nswag | — |
| SEC-DSP-02 → **T-0103** | CreateDispute doesn't check order ownership (S1/S3) | 0 | crit | S | **done ✅** | backend | ADR-AUTHZ |
| SEC-EMP-01 → **T-0104** | Partner analytics IDOR (EmployeeId from query string) | 0 | crit | S | **done ✅** (⚠️ owner: nswag-regen) | backend, nswag | ADR-AUTHZ |
| IDA-SEC-04 → **T-0101** | Any Employee reads any user's full PII by id | 0 | maj | S | **done ✅** | backend | — |
| EMP-GAP-01 → **T-0109** | Rejected cleaners can still take/start/complete orders → gate on ContractStatus==Approved | 0 | crit | M | **done ✅** | backend | ADR-AUTHZ |
| LG-SEC-01 → **T-0110** | Single-use promo over-redeemed via race → atomic conditional-UPDATE + tenant-scoped unique index | 0 | crit | M | **done ✅** (migration regenerated 2026-06-03: SlotOrdinal + unique index in `20260603090920_Initial`) | backend, db | — |
| LG-SEC-02 → **T-0111** | Mobile subscribe: Stripe subscription with no idempotency key → double-charge | 0 | crit | M | **done ✅** (⚠️ owner: nswag-regen; 2 review rounds) | backend, mobile, nswag | ADR-OUTBOX |
| LG-SEC-06 → **T-0112** | Admin loyalty grant/revoke non-idempotent → requestId + tenant-scoped filtered unique index + rate-limit | 0 | maj | M | **done ✅** (migration regenerated 2026-06-03: IdempotencyKey in Initial; ⚠️ owner: nswag-regen for admin Command) | backend, db, nswag | ADR-OUTBOX, ADR-RATELIMIT |
| IA-1 → **T-0108** | CreateAdminUser double-hashes password → new admins can't log in | 0 | crit | S | **done ✅** | backend | — |
| SEC-W2 → **T-0114** | Webhook auto-provision can create a 2nd active membership → active-check + filtered unique index | 0 | maj | M | **done ✅** (migration regenerated 2026-06-03: active filtered unique index in Initial) | backend, db | ADR-OUTBOX |
| SEC-W3 → **T-0116** | Webhook endpoints not rate-limited (S5) → per-IP "webhook" policy (independent) on 3 hosts | 0 | maj | S | **done ✅** | web, backend | ADR-RATELIMIT |
| BSP-4 / IDA-SEC-02 → **T-0115** | Global rate limiter (no partition) → partitioned per-IP/per-sub + forwarded-headers + fail-closed guard + host harness | 0 | crit | M | **done ✅** (⚠️ owner deploy gate: ForwardedHeaders config) | config, backend | ADR-RATELIMIT |
| F11 → **T-0117** | UnitOfWork pipeline commits even on validation failure → Validation-outer reorder + IsSuccess-gated commit | 0 | crit-root | S | **done ✅** | backend | ADR-OUTBOX D4 |
| FUNC-CORE → **T-0121** | Extract Cleansia.Functions.Core so queue consumers are unit-testable (precondition for F2/F4/F3) | 0 | — | S | **done ✅** (16/16 triggers discovered; pure move) | functions | ADR-OUTBOX D5.1 |
| F2 / SEC-W1 → **T-0118** | Enqueue-before-commit → tactical post-commit dispatch (PostCommitDispatchBehavior + idempotent receipt consumer) | 0 | maj | L | **done ✅** | appservices, functions, queue | ADR-OUTBOX D1-D3 |
| F3 → **T-0120** | No poison/dead-letter consumer → 5 per-queue poison consumers + DeadLetter store + classification | 0 | maj | M | **done ✅** (⚠️ owner: DeadLetter table ef-migration folds into Initial regen) | functions, db | ADR-OUTBOX D3 |
| F4 → **T-0119** | Receipt idempotent: claim-before-register, at-most-once fiscal seq + authority registration (S7) | 0 | maj | M | **done ✅** (go-live gates → T-0220/T-0221/T-0122) | functions, backend | ADR-0004 |
| FISCAL-RECON → **T-0122** | Reconciliation sweep: re-enqueue committed-but-unrealized fiscal work (no-receipt OR FiscalCode==null per C-B) | 0 | maj | S | **done ✅** (2 rounds; ADR-0004 outer net) | backend, functions | ADR-OUTBOX D3.4 + ADR-0004 C-B |
| IDA-SEC-08 → **T-0107** | Admin GDPR/deactivate: no self/last-admin protection | 0 | maj | S | **done ✅** | backend | ADR-AUTHZ |
| BLIND-1 → **T-0146** | Email synchronous on signup/reset critical path → async/queue | **1** | crit | M | **ready** (Wave 1 1B — ADR-0005/T-0141 done ✓ + T-0118 ✓; security gate) | backend, functions | ADR-0005 (T-0141) |
| BLIND-2 → **T-0159** | Mapbox access token in request URL query → use correct Mapbox auth + scrub logs + rotate token | **1** | crit | S | **ready** (Wave 1 1B — independent; **security_touching**; ⚠️ owner: rotate-mapbox-token) | frontend, config | — |
| PROD-CONFIG → **T-0123** | Hardening: CSRF-in-prod (BSP-3) + Swagger fail-closed + boot guard (BSP-5) + anon LookupBatch (BSP-9) | 0 | maj | S | **done ✅** (⚠️ owner: provision Csrf:Secret before prod deploy) | config | ADR-RATELIMIT |
| PERF-IDA-01 (+PERF-IDA-05) → **T-0124** | No DB index on User.Email + lookup columns → unique Email index + filtered lookup indexes | 0 | crit | S | **done ✅** (migration folds into Initial regen) | db | — |
| **PRE-0 ADR sprint** | ADR-AUTHZ + ADR-OUTBOX(contract) + ADR-RATELIMIT decided & accepted BEFORE the Wave-0 items that encode them | 0 | — | — | draft | architect | are the ADRs |
| TC-PAY → **T-0125** | Pay-calc tests (must-cover #1) — 70 tests across the 4 pure surfaces; pay math was untested | 0 | crit | S | **done ✅** (split-rounding follow-up → T-0222) | backend | — |
| TC-AUTHZ-0 → **T-0126** | Cross-tenant/cross-user write-path rejection tests + WebApplicationFactory host harness | 0 | crit | M | **done ✅** (Cleansia.HostTests; 32 e2e authz tests green) | backend | with BSP-1 |
| TC-IDEMP-0 → **T-0127** | "Safe to run twice" idempotency tests (webhooks + 3 LG money fixes) | 0 | crit | M | **done ✅** (cases shipped inline w/ fixes; audit confirmed full coverage) | backend | with the fix |
| TC-AUTH-TAKEOVER → **T-0128** | Token-claim binding + reset-code lookup tests | 0 | crit | M | **done ✅** (covered + GoogleTokenVerifier gap filled) | backend | with IDA-SEC-01/03 |
| LG-SEC-05 → **T-0113** | Anonymous-but-tenant-scoped MembershipPlan read → platform config (Option A) | 0 | maj | M | **done ✅** (migration regenerated 2026-06-03: MembershipPlans Code-unique, no tenant-scoping) | backend, db | ADR-AUTHZ A1 |
| LG-SEC-05-sibs → **T-0219** | Anon catalog entities (Service/Category/Package/Extra/ServiceCity) → platform config | 2 | maj | M | **done ✅** (Wave 2; merged 8ff35d49) | backend, db | ADR-AUTHZ A1 |
| FISCAL-SEQ → **T-0220** | Gapless-monotonic-atomic fiscal sequence allocator (FiscalCounter) — replace COUNT(*)+1 | 2 | maj | M | **done ✅** (Wave 2; merged 8ff35d49; **activates on DE/AT/ES go-live**) | backend, db | ADR-0004 |
| FISCAL-AUTH-IDEMP → **T-0221** | Per-provider RegisterReceiptAsync idempotency on ReceiptNumber (IFiscalService key) | 2 | maj | M | **done ✅** (Wave 2; merged 8ff35d49; **activates on DE/AT/ES go-live**) | backend, clients | ADR-0004 |

> ⚠️ **Plan corrected 2026-06-01** after a collision check (`audits/AUDIT-2026-06-01-plan-corrections.md`):
> 3 blocking defects fixed — ADRs frozen pre-Wave-0, outbox split tactical/strategic, BSP-1+BSP-6
> merged + PolicyBuilder edits serialized, and a real Wave-0 test slice added (TDD is now structural).

> **Waves 1–4** (foundational ADRs, story-backed features, consistency cleanup, tests + a11y) are in
> `audits/AUDIT-2026-06-01-execution-plan.md` — not duplicated here. The AUD-01…25 and T-0001…16
> backlogs below are folded into the wave plan (referenced in place). The prior-audit sprint-3 AUD
> tickets and the FUP passes are **superseded by this complete audit** — keep them for traceability but
> work the wave plan.

> **Prior (partial) codebase audit backlog** (sprint 3, superseded by the complete audit above; kept
> for traceability). AUD-01/02/04 carried into Wave 2. FUP-1 RESOLVED-REFUTED.

| ID | Title | Sprint | Size | Status | Owner | Depends on | Layers |
|----|-------|--------|------|--------|-------|-----------|--------|
| **FUP-1** | 🔴 Verify Stripe **subscription** webhook signature (suspected missing) + idempotency/replay | 2 | M | draft | — | — | backend, security |
| FUP-2 | Re-audit the 5 under-covered domains (loyalty-growth, disputes-addresses, identity-auth, catalog-config, employees) | 2 | M | draft | — | — | analyst, reviewer, security, optimizer |
| FUP-3 | Azure Functions trigger-graph pass — re-validate "dead lifecycle" verdicts (AUD-02/04); idempotency/poison/dead-letter | 2 | M | draft | — | — | backend, security |
| FUP-4 | Contract-parity checker: i18n key sets ×5 locales, BusinessErrorMessage↔errors.*, NSwag drift | 2 | M | draft | — | — | backend, frontend |
| FUP-5 | Test-coverage gap pass → prioritized must-cover backlog (orders/payments/payroll/fiscal/Functions) | 2 | M | draft | — | — | qa, backend |
| FUP-6 | AppHost/Aspire + secrets/CORS/host-exposure pass | 2 | S | draft | — | — | architect, security |
| FUP-7 | Migration/seed integrity pass (EF migrations vs configs; sql-scripts seeds) | 2 | S | draft | — | — | db |
| AUD-01 | Admin order operations + generalized cancellation (cancel/reassign/refund/status-override) | 3 | L | draft | — | — | architect, backend, frontend |
| AUD-02 | Wire up dead payroll adjustment & settlement lifecycle (bonus/deduction, Paid, Dispute/Reject, Reopen) | 3 | L | draft | — | FUP-3 | architect, backend, frontend, android |
| AUD-03 | Build admin Extras management (CRUD + translations + pricing) | 3 | L | draft | — | — | backend, frontend |
| AUD-04 | Reconcile partner payroll surface (my-period-pay screen, prune admin endpoints off partner host, failed-PDF invoices) | 3 | L | draft | — | FUP-3, FUP-6 | architect, backend, frontend, android |
| AUD-05 | Add order-cancellation flow to customer **web** (parity with mobile) | 3 | M | draft | — | — | frontend |
| AUD-06 | Decompose CreateOrder.Handler god-handler (484 lines, 15 deps) | 3 | L | draft | — | — | backend |
| AUD-07 | Split order-wizard god-facade (1048 lines) + migrate to C3 pipe | 3 | L | draft | — | T-0010 | frontend |
| AUD-08 | Move ownership/profile checks to handler in Take/Complete/Start order (B4/S3) | 3 | M | draft | — | — | backend |
| AUD-09 | Add RecurringBookingTemplate.MapToDto + Address.ToSingleLine; dedupe recurring projection/validators | 3 | M | draft | — | — | backend |
| AUD-10 | Move cleaner weekly-order-limit magic numbers into BookingPolicy | 3 | S | draft | — | — | backend |
| AUD-11 | Convert partner OrdersListUiState to sealed UiState + ActionState (E1/E2) | 3 | M | draft | — | — | android |
| AUD-12 | Fix off-by-one OrderStatus class/icon maps in partner web order-detail helpers | 3 | S | draft | — | — | frontend |
| AUD-13 | Standardize order/note/issue parity & remove dead endpoints across web/mobile | 3 | M | draft | — | — | backend, frontend |
| AUD-14 | Add OnTheWay case to admin order status badge/icon helpers | 3 | S | draft | — | — | frontend |
| AUD-15 | Type order-status email param as OrderStatus enum + CancelledBy enum (folds into AUD-01) | 3 | M | draft | — | AUD-01 | backend |
| AUD-16 | Type recurring-booking command enums instead of raw ints | 3 | M | draft | — | — | backend, frontend |
| AUD-17 | Remove geocoding **write** from GetPagedOrders query (restore CQRS read-only); extract pay/PII mapper | 3 | M | draft | — | — | backend |
| AUD-18 | Fix partner OrdersFacade cleanup/error handling + remove setTimeout(100) sequencing | 3 | M | draft | — | — | frontend |
| AUD-19 | Move customer recurring/wizard facade calls to the C3 pipe | 3 | M | draft | — | AUD-07 | frontend |
| AUD-20 | Refactor HandlePaymentNotification webhook (297 lines) + add tests | 3 | M | draft | — | — | backend |
| AUD-21 | Align GetFiscalFailures to IQueryHandler + decide paging (remove hidden 200 cap) | 3 | M | draft | — | — | backend |
| AUD-22 | Add Response records to fiscal commands (B1) | 3 | S | draft | — | — | backend |
| AUD-23 | Fix mobile collectAsState → lifecycle-aware; make CZ/CZK config-driven | 3 | M | draft | — | — | android |
| AUD-24 | Correct stale "no recurring UI" comment in MaterializeRecurringBookings | 3 | S | draft | — | — | backend |
| AUD-25 | Burn down the 187 machine-detected consistency violations (T-0001…T-0016 epic) | 3 | — | draft | — | — | backend, frontend, android |

---

> **Consistency canonicalization backlog** (from `audits/consistency-violations.md`). These are
> `draft` until the owner approves the setup and the PM promotes them to `ready`. Each maps to a rule
> in `knowledge/consistency.md`. Two (T-0009, T-0016) need an Architect ADR first because they are
> cross-cutting (soft-delete; mobile repo contract) — do not start those without the ADR.

| ID | Title | Sprint | Size | Status | Owner | Depends on | Layers |
|----|-------|--------|------|--------|-------|-----------|--------|
| T-0001 | Canonicalize GetPagedPromoCodes + GetPagedReferrals to the paged-query pattern | 1 | M | draft | — | — | backend |
| T-0002 | Make GetPagedPayConfigs.Filter init-only | 1 | S | draft | — | — | backend |
| T-0003 | Align GetPagedServices to canonical read-path order | 1 | S | draft | — | — | backend |
| T-0004 | Give CreateDispute/UpdateDisputeStatus/DeleteSavedAddress a Response record | 1 | S | draft | — | — | backend |
| T-0005 | Move ownership checks from validators to handlers (4 features) | 1 | M | draft | — | — | backend, security |
| T-0006 | Refactor validators to AbstractValidator + composed shared rules | 1 | M | draft | — | — | backend |
| T-0007 | Fix Error field name in CreateMembershipSubscription | 1 | S | draft | — | — | backend |
| T-0008 | Add idempotency + provider error handling to membership/order create | 1 | M | draft | — | — | backend, security |
| T-0009 | ADR + sweep: soft-delete for business entities | 2 | L | draft | — | — | architect, backend, db |
| T-0010 | Unify customer-feature facades on UnsubscribeControlDirective | 1 | M | draft | — | — | frontend |
| T-0011 | Normalize list facades (signals, finalize, no stray NgRx) | 1 | M | draft | — | — | frontend |
| T-0012 | Unify fiscal-failures table def + package-form builder | 1 | S | draft | — | — | frontend |
| T-0013 | Convert partner-app flag-bag UiStates to sealed states | 1 | M | draft | — | — | android |
| T-0014 | Standardize one-shot actions on ActionState | 1 | M | draft | — | — | android |
| T-0015 | Fix RecurringBookingsScreen state collection (lifecycle) | 1 | S | draft | — | — | android |
| T-0016 | ADR + migrate customer-app repos to ApiResult<T> and unify mobile structure | 2 | L | draft | — | — | architect, android, ios |

## Done

| ID | Title | Sprint | Merged |
|----|-------|--------|--------|
| _(none yet)_ | | | |

---

> First real job (pending owner approval of this setup): **a full codebase audit** across all
> layers — backend, db, frontend, android — to surface functional gaps, half-built features,
> spaghetti hotspots, hardcoded strings, security holes, and performance issues. The audit fans out
> one analyst + one reviewer (and `security`/`optimizer` where relevant) per subsystem in parallel,
> writes findings to `agents/backlog/audits/`, and the PM converts each finding into a ranked ticket
> here. See `agents/WAY-OF-WORKING.md`.
