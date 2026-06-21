# Sprint 9 — Wave 7 plan (Android consistency debt: deferred E1/E2/E6/E7)

- **Date:** 2026-06-21
- **Goal:** Clear the **last** engineering debt left after the full ticketed backlog (Waves 0–6 +
  the T-0197 mobile slice + T-0264/T-0265) closed: the **deferred Android consistency-sweep rules
  E1/E2/E6/E7** from the codebase audit. T-0197 resolved **E5/ApiResult** only; these four were filed
  STILL-OPEN in `audits/consistency-violations.md` (F13/F14/F15/F16). All four are **Android-only,
  mobile-only, behavior-preserving** — no go-live / money / correctness impact.
- **Status:** **🟢 IN PROGRESS — tickets created `ready`, awaiting orchestrator execution.** Backlog-only
  pass (no code, no commits).
- **Branch:** orchestrator's call (suggest `feature/wave-7-android-consistency` cut from `master`
  tip = `b9e91cd8`, PR #81). PM never merges.

---

## 0. Pre-flight reconciliation (verified against current `master`, 2026-06-21)

- **Master tip = `b9e91cd8`** ("T-0264 + T-0265 …", PR #81). Waves 0–6 + T-0197 + T-0264/T-0265 all
  merged. The whole ticketed backlog is DONE.
- **The audit is STALE in two places — reconciled, not taken at face value:**
  1. **E1/E2 (F13/F14):** **T-0252 (Wave 5) already did the audit-named E1/E2 work** and it's on
     `master`. Verified: partner `Dashboard`/`Earnings`/`OrderDetails` are sealed `*UiState`;
     `LoginViewModel` is already canonical (`LoginFormState` + shared `ActionState` + `SharedFlow` —
     the audit's "Login is a flag-bag" line is stale); customer `CreateDispute`/`Membership`/`Profile`
     + partner ex-`OrderAction inFlight` all use the shared `ActionState`. So **E2 has ZERO
     implementation work left** (→ verify-and-close ticket), and **E1's only residual** is the
     partner flag-bags T-0252 did not name.
  2. **E6 (F15):** the audit's "~22 across the mobile screens" **undercounts** current `master` (it
     predates the Wave-3 Android build-out). The real filtered count is **≈56** (see §3).
- **No deliberation panel needed** for any of the four: each is a **mechanical canonicalization
  against an already-ratified consistency rule** (§E1/§E2/§E6/§E7 in `knowledge/consistency.md`, with
  §E5/E7 backed by ADR-0011) with **no new behavior or decision** → each ticket carries a one-line
  no-decision note and skips the panel. The only judgments are *which existing data classes are
  genuine flag-bags* (E1) and *which collectAsState calls are real violations* (E6) — both are
  scoping calls the dev records, not new product/architecture decisions.

---

## 1. Scope — the 4 Wave-7 tickets (all S/M, no `L`)

| ID | Rule | Title (short) | Size | Layers | depends_on | manual_step | Panel? |
|----|------|---------------|------|--------|-----------|-------------|--------|
| **T-0266** | **E7** | Unify partner-app dir/naming → inline-singular `features/<name>/` (structural move) | M | android | — | — | no (no-decision) |
| **T-0267** | **E1** | Convert residual partner flag-bag `*UiState` → sealed (the ones T-0252 didn't name) | M | android | T-0266 | — | no (no-decision) |
| **T-0268** | **E2** | Verify-and-close shared `ActionState` one-shot-effect coverage (done by T-0252) | S | android | — | — | no (no-decision) |
| **T-0269** | **E6** | `collectAsStateWithLifecycle()` sweep over screen/VM-flow collections (both apps) | M | android | T-0266, T-0267 | — | no (no-decision) |

**Common AC spine on all four** (per `testing.md` + quality-gates): behavior-preserving,
characterization-test-first, **both apps compile + unit suites green on plain JVM**, and
**`check-consistency.mjs` clean for the rule afterward**. `security_touching: false`, `manual_steps:
[]` (mobile-only — **no nswag-regen, no ef-migration, no i18n change**) on every one.

---

## 2. Execution order / lanes — the CRITICAL serialization (read before dispatch)

**The collision risk:** E7 (T-0266) **moves/renames** partner screen+VM files; E1 (T-0267)
**refactors** some of those same partner VMs; E6 (T-0269) **edits** the same partner screens. Running
any two of these concurrently on the **partner files** = two instances editing the same files →
forbidden. So the partner-app work is a **single serial lane**:

```
PARTNER-FILES LANE (strictly serial — one instance at a time on partner screens/VMs):
   T-0266 (E7 structural move)  →  T-0267 (E1 sealed-UiState residual)  →  T-0269 (E6 sweep, partner half)
   ───────────────────────────     ──────────────────────────────────     ───────────────────────────────
   moves files FIRST so the         refactors on the SETTLED paths          sweeps over the SETTLED + refactored
   later tickets target stable      (depends_on T-0266)                     files LAST (depends_on T-0266, T-0267)
   package/import paths
```

**What CAN run in parallel (disjoint files):**
- **T-0268 (E2 verify-close)** edits **no production files** (scan + gate-run + close) → runs
  **concurrently with everything**, no lock. (Only if its AC1 scan surfaces a genuine un-migrated
  one-shot action does it touch a VM — and then it serializes against whichever of T-0267/T-0269 owns
  that exact file. Expected: none.)
- **T-0269 customer-app half** is **partner-disjoint** (E7/E1 are partner-only). The dev MAY sweep the
  9 customer-app E6 files while the partner lane is still settling — kept inside T-0269 as one ticket
  for a single clean gate, but the customer files carry no cross-lock with T-0266/T-0267.

**Reviewer-per-developer on every ticket.** No security gate (all `security_touching: false`). No
optimizer gate (no hot path / no new dependency — the E6 change *improves* runtime by pausing
background collection, but it's a behavior-preserving swap, not a perf ticket). QA = suite-green +
AC↔evidence mapping + `check-consistency` clean.

**Dispatch summary:** start **T-0266** + **T-0268** + (optionally) the **customer half of T-0269**
concurrently → on T-0266 done, start **T-0267** → on T-0267 done, finish **T-0269** (partner half).

---

## 3. E6 — the FILTERED real-vs-raw count finding (headline)

| Measure | Count | Note |
|---|---|---|
| **Raw `grep collectAsState()`** | **85 occ / 36 files** | matches the audit's raw number |
| Audit's scoped estimate | "~22 across screens" | **stale — undercounts** (predates Wave-3 Android build-out) |
| **FILTERED real E6 violations** | **≈56 occ / ~30 files** | screen/composable collections of a **VM-owned lifecycle flow** |
| Excluded NON-violations | **≈29 occ** | breakdown below |

**Excluded (correctly plain `collectAsState()`):**
- **`@Singleton` repository StateFlows** collected in screens — `loyaltyRepo`/`referralRepo`
  (`RewardsTab`), `orderRepo` (`OrdersTab`), `catalogRepo` (`ServicesStep`/`ConfirmStep`), the
  app-scoped `membership`/`membershipRepository` flows. Verified the repos are `@Singleton` → the flow
  outlives the screen, so lifecycle-aware collection is **not** required. **Not violations.**
- **NavHost-level collections** — `CleansiaNavHost` ×9, `PartnerNavHost` ×1 (nav-graph pattern, not a
  screen body). **Excluded.**
- **`:core` infra** — `GlobalSnackbarHost` (`SnackbarInsetState.insetDp`). **Excluded.**

**Tool caveat (recorded on T-0269):** `check-consistency.mjs`'s E6 regex only matches receivers named
`viewModel`/`vm`, so it **misses** real violations whose receiver is `bookingVm`, `chainViewModel`,
`settingsViewModel`, `checklistViewModel`, `profileVm`, etc. "Tool-clean" is necessary but not
sufficient — T-0269 fixes the full *conceptual* screen/VM-flow set and confirms by re-grep, not only
the tool's narrow regex.

---

## 4. E1/E2 reconciliation against T-0252 (don't redo)

- **E2 (T-0268):** **fully done by T-0252**, verified on `master`. No implementation work; ticket is
  verify-and-close (F14 cleared on pass).
- **E1 (T-0267):** the **four audit-named** partner VMs are done by T-0252. Residual = partner
  flag-bags T-0252 didn't name. The dev applies the §E1 judgment call (form-state holders are OK;
  only page-state flag-bags are violations):
  - **Convert:** `InvoiceDetailsViewModel` (`isLoading`/`invoice`/`error`), `OrderPhotosViewModel`
    (`isLoading`/`photos`/`error`).
  - **Do NOT convert (documented design / form-state / §E2-effect, recorded per AC3):** the
    dual-spinner list VMs `OrdersList`/`InvoicesList`/`RegistrationLock`
    (`isUserRefreshing`/`isBackgroundRefreshing`/`hasLoadedOnce` — intentional pull-to-refresh design);
    the partner form-section VMs (`PersonalSection`/`BankSection`/`Emergency`/`Identification`/
    `Documents`/`AddressSection`/`Profile`/`Register`/`ForgotPassword`/`ConfirmEmail`/`Settings` — form
    holders); `OrderNotesViewModel` (action-effect, §E2 not §E1).

**Per-app E1/E2 VM disposition:**
- **customer-app:** E1 — none residual (already canonical). E2 — `CreateDisputeViewModel`,
  `MembershipViewModel`, `ProfileViewModel` **done by T-0252**.
- **partner-app:** E1 — `Dashboard`/`Earnings`/`OrderDetails` **done by T-0252**; `Login` already
  canonical; **residual to convert: `InvoiceDetails`, `OrderPhotos`** (T-0267). E2 — ex-`OrderAction
  inFlight` → `ActionState` **done by T-0252**.

---

## 5. E7 — the actual dir/naming divergence (verified)

- **customer-app = canonical:** inline-singular `features/<name>/<Name>ViewModel.kt`+`<Name>Screen.kt`
  (deliberate sub-namespaces `orders/photos`, `main` stay).
- **partner-app = divergent:** `features/<name>/{screens,viewmodels,components}/` split on `auth`,
  `dashboard`, `earnings`, `invoices`(+components), `notifications`, `onboarding`(screens), `orders`
  (+components), `profile`(+components), `settings`. Already-inline (leave): `devices`, `main`,
  `payroll`. T-0266 collapses the split to inline-singular (move + package/import rewrite, no logic).

---

## 6. Owner items / gates

- **No owner manual steps this wave** — all four tickets are mobile-only, behavior-preserving:
  **no nswag-regen, no ef-migration, no i18n change.**
- **No open blocking questions** gate Wave 7.
- **No new ADR** — E5/E7 are already ratified by **ADR-0011**; E1/E2/E6 are §E rules in
  `knowledge/consistency.md`. Pure BUILD against accepted contracts.
- On close, the reviewer/PM clears the **F13/F14/F15/F16-E7** entries in
  `audits/consistency-violations.md` and updates this doc with the per-ticket landing + the final
  confirmed E6 count.
