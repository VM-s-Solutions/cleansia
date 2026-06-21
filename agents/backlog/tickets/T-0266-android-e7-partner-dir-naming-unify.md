---
id: T-0266
title: "E7 — unify partner-app dir/naming to the inline-singular features/<name>/ convention (structural move, no logic change)"
status: done
size: M
owner: —
created: 2026-06-21
updated: 2026-06-21
depends_on: []
blocks: [T-0269]
stories: []
adrs: []
layers: [android]
security_touching: false
manual_steps: []
sprint: 9
source: audits/consistency-violations.md F16 (E7 half); knowledge/consistency.md §E7
---

## Context

Residual Android consistency rule **E7** from the codebase audit (`audits/consistency-violations.md`
F16; rule in `knowledge/consistency.md` §E7). T-0197 closed the **E5/ApiResult** half of F16; the
**dir/naming unification (E7)** is its own ticket and is filed here.

**The divergence, verified against current `master`** (`find … features -type d`):

- **customer-app = the canonical form** — every feature has `<Name>ViewModel.kt` + `<Name>Screen.kt`
  **inline** in `features/<name>/`, singular naming. (Only deliberate sub-namespaces remain:
  `features/orders/photos/`, `features/main/`.)
- **partner-app = the divergent form** — features split into
  `features/<name>/{screens,viewmodels,components}/` sub-packages. The features carrying the split:
  `auth`, `dashboard`, `earnings`, `invoices` (+`components`), `notifications`, `onboarding`
  (`screens` only), `orders` (+`components`), `profile` (+`components`), `settings`. Already-inline
  partner features (leave as-is): `devices`, `main`, `payroll`.

E7 is a **structural move/rename only** — collapse the `screens/`+`viewmodels/`(+`components/`)
sub-packages back to inline `features/<name>/`, update the Kotlin `package` declarations and imports.
**No logic change, no behavior change, no UiState/flow change.** ("Details" plural-drift, if any
survives, is renamed to singular in the same pass.)

## Acceptance criteria

- [ ] **AC1 (characterization-first, per `testing.md`)** — Before the move, the partner-app unit
  suite (`:partner-app:testDebugUnitTest`) is recorded GREEN on plain JVM as the baseline (T-0265
  made the email-validating VMs JVM-testable, so the suite is green on `master`). The move must leave
  it green — same test classes, only their `package`/import lines change.
- [ ] **AC2 (inline-singular layout)** — Every split partner feature collapses its
  `screens/`+`viewmodels/`(+`components/`) sub-package contents up into `features/<name>/`, matching
  the customer-app convention. No `viewmodels/` or `screens/` sub-package remains under
  `partner-app/.../features/` (except the deliberate sub-namespaces, which stay).
- [ ] **AC3 (packages + imports updated)** — Each moved file's `package cz.cleansia.partner.features.<name>.{screens|viewmodels|components}`
  is rewritten to `package cz.cleansia.partner.features.<name>`; every import referencing the old
  sub-package (across the app, incl. `navigation/PartnerNavHost.kt` and test sources) is updated.
- [ ] **AC4 (singular naming)** — Any `Details`/plural-drift in a moved type or filename is renamed to
  the singular convention; the rename is reflected everywhere it is referenced.
- [ ] **AC5 (behavior identical / compiles)** — partner-app compiles (`:partner-app:assembleDebug` or
  `compileDebugKotlin`) and the customer-app is untouched and still compiles; **no diff to any
  function body** — the reviewer confirms the diff is moves/renames/package/import only.
- [ ] **AC6 (consistency gate)** — `node agents/tools/check-consistency.mjs --paths=src/cleansia_android/partner-app`
  reports **no new** violation introduced by the move; the E7 structural divergence is gone.
- [ ] **AC7 (suite green)** — `:partner-app:testDebugUnitTest` green on plain JVM after the move (AC1
  baseline preserved).

## Out of scope

- E1 sealed-UiState (T-0267), E2 ActionState (already done by T-0252; verified), E6
  collectAsStateWithLifecycle (T-0269) — **no flow/state-shape change in this ticket.**
- Any logic, behavior, navigation-graph, or API change. Pure structure.
- The customer-app (already canonical) and the deliberate partner sub-namespaces (`main`, `payroll`,
  `devices` are already inline; leave them).

## Implementation notes

- **Canonical form:** `knowledge/consistency.md` §E7 — `features/<name>/<Name>ViewModel.kt` +
  `<Name>Screen.kt` inline, singular.
- **This must run lane-isolated (see sprint-9 §execution order).** E7 touches the SAME partner
  screen/VM files that E1 (T-0267) and E6 (T-0269) edit — running it concurrently with either would
  collide on the same files. **E7 goes FIRST** so the later per-VM (E1) and the E6 sweep operate on
  the settled file paths. T-0269 (E6) `depends_on` this ticket; T-0267 (E1) is sequenced after this in
  the same lane (sprint-9).
- **Mechanical move discipline:** move file → rewrite its `package` line → fix all references. Prefer
  an IDE/scripted move so no body bytes change; the reviewer's job is to confirm the diff is
  move-only. Watch the `R` class and `BuildConfig` imports (unaffected — same module).
- **No `manual_steps`** — mobile-only structural refactor: no nswag-regen, no ef-migration, no i18n
  change.

## Status log
- 2026-06-21 — ready (created by pm). Wave 7 (Android consistency debt). DoR met: AC observable +
  characterization-first, sized M (structural move across ~9 partner features — if the move regrows
  past M at dispatch the dev stops and the PM splits per-feature), no deps, mobile-only (no
  migration/regen), behavior-preserving. **No new behavior/decision → no deliberation panel** (pure
  structural canonicalization against the already-ratified §E7 convention; one-line no-decision note).
  Reviewer-per-developer. **Lane-isolated, runs FIRST** (blocks T-0269; precedes T-0267 in the same
  partner-files lane).
- 2026-06-21 — in-progress → done (android). E7 structural move executed:
  - AC1/AC7: baseline `:partner-app:testDebugUnitTest` GREEN (26/26, 6 classes); GREEN after the move
    (26/26 — same classes, only package/import lines changed; `OrderDetailsViewModelTest` →
    `OrderDetailViewModelTest`).
  - AC2: all 74 split files in `auth/dashboard/earnings/invoices/notifications/onboarding/orders/
    profile/settings` `{screens,viewmodels,components}/` collapsed inline into `features/<name>/`; the
    empty sub-package dirs removed. Deliberate inline features (`devices`, `main`, `payroll`) left as-is.
  - AC3: every moved file's `package …{screens|viewmodels|components}` rewritten to
    `…features.<name>`; all imports across prod + `navigation/PartnerNavHost.kt`, `features/main/
    MainScaffold.kt`, `core/notifications/NotificationDeepLink.kt` and the 4 test sources updated; 72
    now-redundant same-package self-imports (cross-sub-package refs that collapsed) removed.
  - AC4: `Details` plural-drift renamed to singular to match the customer-app `OrderDetail`/`Detail`
    convention — types `OrderDetails*`→`OrderDetail*`, `InvoiceDetails*`→`InvoiceDetail*` (incl.
    `NavRoute.OrderDetails`/`InvoiceDetails`, `OrderDetailsUiState`, `OrderDetailsBottomSheetLayout`,
    `OrderDetailsSheetContent`, `OrderDetailsCompactHeader`) and the 6 files carrying it; the lambda
    params `onOpenOrderDetails`/`onOpenInvoiceDetails` and the API `EmployeeInvoiceDetailDto` left
    untouched (not types/filenames in scope).
  - AC5: proven move/rename/package/import-only — all 75 moved/renamed file bodies are byte-identical
    to HEAD modulo package + import + the Details→Detail token (blob-sha comparison, 0 body diffs); the
    8 path-stable edited files changed only import/route-rename lines. customer-app + `:core` untouched.
  - AC6: `check-consistency.mjs --paths=…/partner-app` — type counts identical to HEAD baseline
    (17 E1, 1 E5, 27 E6, **0 E7**); no new violation introduced, E7 structural divergence gone. The
    remaining E1/E6 are the pre-existing debt owned by T-0267 (E1) / T-0269 (E6), out of scope here.
  - Encoding: all 132 partner `.kt` clean UTF-8, no BOM, no mojibake (`Ã`/`Â`/`â€`); per-file line
    endings preserved (124 CRLF + 1 pre-existing LF unchanged). Paths settled for T-0267/T-0269.

## Review
<!-- reviewer writes verdict here; PM reconciles before advancing state -->
