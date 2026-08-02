---
id: T-0490
title: Android partner invoice detail — the back button is drawn under the status bar (no window inset)
status: draft
size: S
owner: android
created: 2026-08-02
updated: 2026-08-02
depends_on: []
blocks: []
stories: []
adrs: []
layers: [android]
security_touching: false
manual_steps: []
sprint: 15
---

## Context

**Owner remark #9 (2026-08-02):** *"No back button on the invoice detail page, Android."*

### Ground truth — PM-verified on `master` at `0e4ede1b`. The button exists; you cannot reach it.

**The back button is present and correctly wired:**

- `partner-app/.../features/invoices/InvoiceDetailScreen.kt:117-122` — an `IconButton(onClick =
  onNavigateBack)` with `Icons.AutoMirrored.Outlined.ArrowBack` and a `R.string.back`
  `contentDescription`.
- `navigation/PartnerNavHost.kt:282-289` — `InvoiceDetailScreen(onNavigateBack = {
  navController.popBackStack() }, …)`. The callback is real.

**What is missing is the window inset.** The screen's root is a plain
`Column(Modifier.fillMaxSize().background(...))` (`InvoiceDetailScreen.kt:107-111`) whose first child
is the header `Row` with `padding(horizontal = Spacing.XS, vertical = Spacing.XS)` (`:112-116`). There
is **no** `statusBarsPadding()`, **no** `WindowInsets` read, and **no** `Scaffold` to supply one —
PM-grepped the whole file: **zero inset handling.**

**And the partner app is edge-to-edge:** `partner-app/.../MainActivity.kt:76` calls
`enableEdgeToEdge()`. So the app draws behind the system bars and every screen owes its own inset.

**Its sibling gets it right, which is what makes this a defect rather than a design:**
`InvoicesListScreen.kt:96` — `val statusBarTop = WindowInsets.statusBars.asPaddingValues()
.calculateTopPadding()`. The **list** insets; the **detail** does not. Same feature, same package,
adjacent files.

**Net effect:** on any device with a status bar (all of them) and especially on a notch/punch-hole
device, the 4dp-padded header `Row` renders **inside the status-bar strip**. The arrow is behind the
clock and the battery icon. It is not "missing"; it is underneath the system UI. That also explains
why the title "Invoice details" would look clipped or oddly high.

## Acceptance criteria

- [ ] **AC1 — the header clears the status bar.** Given the partner app on a device with a status bar
      (and on one with a display cutout), When the invoice detail opens, Then the back arrow and the
      "Invoice details" title are fully visible below the system UI and the arrow is tappable at its
      full 48dp target. Evidence: before/after screenshots on a cutout device, with the system clock
      visible in frame for reference.
- [ ] **AC2 — the mechanism matches the sibling, or the divergence is argued.** `InvoicesListScreen.kt:96`
      reads `WindowInsets.statusBars.asPaddingValues().calculateTopPadding()`. Use the same mechanism,
      or state why a different one (`Modifier.statusBarsPadding()`, `Scaffold`) is better **for the
      whole partner app**. Evidence: the stated choice.
- [ ] **AC3 — the bottom is checked too.** The screen ends in a `CleansiaPrimaryButton` (open PDF, per
      the screen's own doc comment at `:80`). Under `enableEdgeToEdge()` that button can sit under the
      gesture bar. State whether it does, and fix it in the same change if so. Evidence: the bottom-of-
      screen screenshot.
- [ ] **AC4 — the SWEEP, and it is the valuable half.** Grep every partner-app screen for inset
      handling and produce the list of screens that have **none** while being reachable as a
      full-screen destination. **Do not fix them here.** Report them in `## Review` with file:line so
      one follow-up ticket can be filed with a real scope. **PM's own grep found `statusBarsPadding`
      in ZERO partner-app files** — so the answer is unlikely to be "just this one", and shipping a
      one-screen fix while leaving five identical ones is the wrong trade. Evidence: the list.
- [ ] **AC5 — a test that goes red against the current code (Gate 0.5 leg 1).** The repo's own idiom
      for a Compose-layout invariant with no test harness is a source-reading guard
      (`NotificationsScreenTogglesTest.kt:17-21`, the precedent T-0473's Android leg cited). Prove it
      fails against the pre-fix file. Evidence: the red run, then green.
- [ ] **AC6 (Gate 0.5)** — `:partner-app` compile + `testDebugUnitTest` **un-cached**
      (`--rerun-tasks --no-build-cache`), task outcomes recorded.

## Out of scope

- **Fixing every screen the AC4 sweep finds.** The sweep produces a list; a separate ticket fixes it
  with a size that reflects the real count.
- **The customer app.** Its own hero carries a hand-rolled `Spacer(Modifier.height(12.dp))` stand-in
  for a status-bar inset (`ProfileTab.kt:141-143`) — **that is T-0453**, already filed.
- **Redesigning the invoice detail.** Only the inset.
- **iOS.** Safe-area handling is automatic there; not reported.

## Implementation notes

**No panel — one-line "no-decision" note:** this applies an existing mechanism, already used by the
sibling screen in the same package, to a screen that skipped it. No new behaviour.

**Shared-file lane:** `InvoiceDetailScreen.kt` has no other sprint-15 claimant.

**This is the cheapest ticket in the batch and it fixes a screen the owner cannot navigate out of.**
It should run early regardless of what else is happening.

## Status log
- 2026-08-02 — **draft (created by pm from the owner's remark #9).** **The report was corrected before
  ticketing:** the back button exists (`InvoiceDetailScreen.kt:117-122`) and is correctly wired
  (`PartnerNavHost.kt:283`). The defect is a **missing window inset** under `enableEdgeToEdge()`
  (`MainActivity.kt:76`), proved by the sibling `InvoicesListScreen.kt:96` doing it correctly. A
  developer told "add a back button" would have added a second one.
- 2026-08-02 — **implemented (android)** on `fix/PR-B-android-nav-and-invoice-back` (shared with
  T-0479; disjoint files). Red→green recorded below. No second back button was added — the existing
  one now clears the system UI.

## Review — android (2026-08-02)

**AC1 — the header clears the status bar.** `InvoiceDetailScreen.kt:94` reads the inset and `:117`
spends it as the root `Column`'s first child, so the header `Row` (`:118-123`) and its `IconButton`
(`:124-130`) start below the system UI at the button's full 48dp target.
**The rendered result is UNVERIFIED — there is no emulator or attached device in this environment, so
no before/after screenshot exists and nothing about the pixels was observed.** The mechanism is pinned
by test; the cutout-device screenshot AC1 asks for is still owed by a device pass.

**AC2 — the mechanism matches the sibling.** Used
`WindowInsets.statusBars.asPaddingValues().calculateTopPadding()` + `Spacer(Modifier.height(...))`,
verbatim from `InvoicesListScreen.kt:96,113`. It is the partner app's **majority** idiom, not just
this feature's — `PeriodPayScreen.kt:84,91` is the identical two lines. The third pushed detail
screen, `OrderDetailScreen.kt:315`, uses `Modifier.windowInsetsPadding(WindowInsets.statusBars)` on a
header it already wraps; equally correct, but adopting it here would mean adding a wrapper around this
screen's header purely to hang a modifier on. **No `Scaffold` conversion** — that is a partner-wide
chrome decision, not something a one-screen bug fix should settle.

**AC3 — the bottom was checked, and it is also broken.** Confirmed, fixed in the same change. The
content column ended at `.padding(bottom = Spacing.L)` = **24dp** against a **48dp** three-button
navigation bar, so the `CleansiaPrimaryButton` "Open invoice PDF" (`:177-183`) sat partly underneath
it on every 3-button device (on gesture nav the ~24dp handle made it a near-miss rather than a hit).
Now `bottom = Spacing.L + navigationBarBottom` (`:158`; value read at `:95`). Precedent for the bottom
inset in this app: `OrderDetailScreen.kt:696`. The padding is applied **after** `verticalScroll`, so it
is scrollable content padding and does not shrink the scroll viewport. Screenshot evidence is
**UNVERIFIED** for the same reason as AC1.

**AC4 — the SWEEP.** Every `composable<…>` in `PartnerNavHost.kt:95-462` was traced to its screen and
to the source of its inset. **The answer is narrower than predicted, because `statusBarsPadding` was
the wrong token to grep** — this app never uses that modifier; it insets three other ways, two of them
parent-supplied:

| mechanism | screens |
|---|---|
| reads `WindowInsets.statusBars` directly | `InvoicesListScreen.kt:96` · `PeriodPayScreen.kt:84` · `OrderDetailScreen.kt:315` · `ProfileScreen.kt` · `DashboardScreen.kt` · `OrdersListScreen.kt` · `DevicesScreen.kt` · `AddressPickerScreen.kt` · `RegistrationLockScreen.kt` |
| `Scaffold` + `TopAppBar` (M3 supplies `contentWindowInsets`) | `SectionScaffold.kt:53` (→ Personal/Address/Identification/Bank/Emergency/Documents) · `EarningsSummaryScreen.kt:76` · `NotificationsScreen.kt:78` · `LanguagePickerScreen.kt:117` (→ Language + Theme pickers) |
| bare `Scaffold { p -> …padding(p) }`, no topBar — M3 still passes the status-bar inset through `innerPadding` | `LoginScreen.kt:62` · `RegisterScreen.kt:82` · `ForgotPasswordScreen.kt:72` · `ConfirmEmailScreen.kt:83` |
| **none** | `InvoiceDetailScreen.kt` (**this ticket**) · `OnboardingScreen.kt:116-119` |

The follow-up ticket's real scope is therefore **one screen**:

- **`features/onboarding/OnboardingScreen.kt:116-119`** — root is a plain
  `Column(Modifier.fillMaxSize().background(...))` with **zero** inset handling, reachable as a
  full-screen destination at `PartnerNavHost.kt:120`. Its first child (`:126-146`) is a
  `Row(padding(Spacing.M))` = 16dp carrying the **`LanguageChooser`** and the **Skip**
  `CleansiaTextLink` — **two interactive controls** inside the status-bar strip, on the first screen a
  new cleaner ever sees. Same defect class, same one-line fix. **Not fixed here** per this ticket's
  Out-of-scope line; it is one line if the PM would rather fold it in than file for it.
- `PartnerNavHost.kt:503` (splash) has no inset either and is **not** a defect — `WordmarkSplash`
  (`:core`) is a deliberately full-bleed gradient `Box` with centre-aligned content and nothing in the
  top strip. Refuted, not omitted.

**AC5 — a test that goes red against the current code.** New source-reading guard on the cited
precedent: `partner-app/src/test/java/cz/cleansia/partner/features/invoices/InvoiceDetailInsetsTest.kt`
(3 tests). Against the pre-fix file: **3 completed, 3 failed**. After the fix: **3 passed**.
Named mutation (Gate 0.5 leg 1): deleting `Spacer(Modifier.height(statusBarTop))` reddens
**`the header is pushed below the status bar before the back arrow is drawn`** — **1 failed
pre-restore, 0 after restore** — and *only* that test moved, so the three assertions are independent.
Restore confirmed **byte-exact** by md5 (`f88167b8303caebd734ac7540c8a44fa` before and after).

**AC6 (Gate 0.5) — un-cached.**
`./gradlew :customer-app:compileDebugKotlin :customer-app:testDebugUnitTest :partner-app:compileDebugKotlin :partner-app:testDebugUnitTest --rerun-tasks --no-build-cache --no-daemon`
→ **BUILD SUCCESSFUL**, exit 0, **88 actionable tasks: 88 executed** — zero `FROM-CACHE`; the only
`UP-TO-DATE` lines are the actionless `pre*Build` lifecycle anchors. `:partner-app` **185 tests,
0 failed, 0 skipped**; `:customer-app` **358 tests, 0 failed, 0 skipped** (counts read from the JUnit
XML, not from the console tail).
`node agents/tools/check-consistency.mjs --paths=…/cleansia_android` → **22 violations, identical to
the master baseline of 22**; none in a touched file. Encoding: every touched file `utf-8`, no BOM.

**Observation, not fixed (out of scope):** `InvoiceDetailScreen.kt:145` renders
`is InvoiceDetailUiState.Error -> Unit` — a load failure leaves the header sitting over an empty
screen with no message and no retry. Real, but it is a state-handling gap rather than the inset, and
this ticket says "only the inset". Worth its own ticket.

**UNVERIFIED-LOCALLY:** the AC1/AC3 screenshot evidence (cutout device; bottom of screen). No emulator
or device exists in this environment — nothing about the rendered result was observed. The mechanical
checks that DID run are recorded above.
