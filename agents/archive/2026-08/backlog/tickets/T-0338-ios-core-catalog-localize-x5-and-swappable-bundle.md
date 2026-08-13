---
id: T-0338
title: Localize the CleansiaCore catalog ×5 + route Core localization through a swappable bundle (so the in-app language switch reaches Core toasts)
status: done
size: S
owner: pm
created: 2026-06-27
updated: 2026-07-19
depends_on: [T-0310]
blocks: []
stories: []
adrs: [0013, 0014]
layers: [ios]
security_touching: false
manual_steps: []
sprint: 12
source: T-0310 Slice C reviewer MINOR (sprint-12 §7.7 / Preferences)
---

> **Pre-existing debt, surfaced by T-0310 — NOT a Phase-3 regression.** The reviewer's T-0310 Slice C
> (Preferences) MINOR: the CleansiaCore-owned user-facing strings ship **en-only** (the Core `Package.swift`
> `defaultLocalization: "en"`, a single `Resources/en.lproj/Localizable.strings`) and resolve via
> `bundle: .module`, so the **new runtime in-app language switch** (T-0310 Slice C — language picker + the
> System/follow-device row, theme via `.preferredColorScheme`) does **NOT** re-localize them: a Core error toast
> or the snackbar dismiss label stays **English even on a non-en device / after the user switches language
> in-app**. The app-target catalogs (`CleansiaPartner`/`CleansiaCustomer` `Localizable.xcstrings`) are already
> ×5 and the switch reaches them; only the **Core** package's own strings are stuck. **No-decision panel
> skipped is NOT claimed** — there IS a small decision (how the runtime locale reaches a Swift package that
> resolves through `bundle: .module`), so this carries a one-line implementer-seam note, not a no-decision note:
> the implementer picks the swappable-bundle mechanism below and records it; no new ADR (it applies ADR-0013's
> Core-package seam + the §7.7 D4 settings-store decision).

## Context

CleansiaCore owns a small set of user-facing strings that render in both apps:

- **`ApiErrorLocalizer`** (`src/cleansia_ios/CleansiaCore/Sources/CleansiaCore/Snackbar/ApiErrorLocalizer.swift:20-30`)
  — the **6 status-fallback error toasts**: `error.unauthorized` (401/403), `error.not_found` (404),
  `error.request` (4xx), `error.server` (5xx), `error.unreachable` (nil status), `error.generic` (default). Each
  is `String(localized: "<key>", bundle: .module)`.
- **`GlobalSnackbarHost`** (`Snackbar/GlobalSnackbarHost.swift:49`) — the **`snackbar.dismiss`** accessibility
  label, `Text("snackbar.dismiss", bundle: .module)`.

That is **7 Core-owned user-facing keys**. They live in a **single** catalog,
`src/cleansia_ios/CleansiaCore/Sources/CleansiaCore/Resources/en.lproj/Localizable.strings`, and the package
declares `defaultLocalization: "en"` (`CleansiaCore/Package.swift:6`). Because there is no `cs/sk/uk/ru` lproj
in the package **and** because `bundle: .module` resolves against the package bundle's locale (not the app's
selected locale or `Locale.current` override), these strings:

1. show **English on a device whose system language is cs/sk/uk/ru** (no localized variant exists), and
2. do **NOT** follow the **T-0310 Slice C in-app language switch** (the switch changes the app's resolved
   locale for the app-target catalogs, but the Core package keeps resolving `.module` against `en`).

The app-target catalogs are already ×5 and the switch reaches them; the gap is **Core-only**. The Android/
partner equivalents are already localized ×5 (`partner-app/src/main/res/values{,-cs,-sk,-uk,-ru}/strings.xml`),
so the translations exist to lift verbatim.

## Acceptance criteria
- [x] **AC1 — The Core catalog is localized ×5.** Add `cs`, `sk`, `uk`, `ru` variants for all **7** Core-owned
  keys (`error.unauthorized`, `error.not_found`, `error.request`, `error.server`, `error.unreachable`,
  `error.generic`, `snackbar.dismiss`) to the CleansiaCore package — either as `cs/sk/uk/ru` `.lproj`
  `Localizable.strings` siblings of the existing `en.lproj`, or by migrating to a `.xcstrings` String Catalog
  with all 5 languages. The translations are lifted **verbatim** from the already-×5 Android/partner equivalents
  (the `values-{cs,sk,uk,ru}/strings.xml` error/dismiss strings). `Package.swift`'s `defaultLocalization`
  stays `"en"`.
- [x] **AC2 — Core localization is routed through a swappable bundle (or fed the resolved locale).** Change the
  Core resolution so the **T-0310 Slice C language switch reaches the Core strings**: feed the user-selected
  locale (from the one `AppSettingsStore`, §7.7 D4) into the Core localization path — e.g. a Core-side
  `localizedBundle(for: language)` / a `LocalizationProvider` seam that loads the matching `.lproj` from
  `Bundle.module`, OR `String(localized:locale:bundle:)` with the resolved `Locale`. The mechanism is the
  implementer's call (record it in-ticket); the **contract** is fixed: after the user switches language in-app,
  a freshly-rendered Core toast / the snackbar dismiss label is in the selected language **without an app
  restart** (parity with how the app-target catalogs already follow the switch). `ApiErrorLocalizer` and
  `GlobalSnackbarHost` are the only two call sites to re-point; no public-API break unless the seam needs the
  locale passed in (then thread it from `CleansiaCore`'s settings store, not from feature code).
- [x] **AC3 — A Core test asserts each key resolves in all 5 known regions.** Add a CleansiaCore unit test that,
  for each of the **5 known regions** (`en, cs, sk, uk, ru`), resolves **every** Core-owned key and asserts it
  is non-empty **and** differs from the `en` baseline for the non-en regions (i.e. an actual translation landed,
  not a silent fall-through to English) — mirroring the existing `ApiErrorLocalizerTests` shape. Extend or sit
  alongside `ApiErrorLocalizerTests.swift`.
- [x] **AC4 — Gates green.** `CleansiaCore` + both app targets compile; the Swift suites green on the simulator;
  the blocking SwiftLint/SwiftFormat iOS CI gate (T-0323) passes; reviewer-per-developer APPROVE. No Gate-DP
  (no screen change — strings + a Core seam only); no security gate (no authz/endpoint surface — user-facing
  copy only); no optimizer.

## Out of scope
- **No new user-facing strings / no app-target catalog change** — the app-target `Localizable.xcstrings` are
  already ×5 and already follow the switch; this is the **Core** package only.
- **No new locales** — the platform's 5 (`en/cs/sk/uk/ru`); no 6th language.
- **No change to the T-0310 Slice C switch UI** — the language/theme pickers + the System/follow-device row
  shipped in T-0310; this ticket only makes the **Core** strings honor the already-shipped switch.
- **No backend / contract change** — pure client-side i18n + a Core localization seam.

## Implementation notes
- Catalog: `src/cleansia_ios/CleansiaCore/Sources/CleansiaCore/Resources/` (today `en.lproj/Localizable.strings`).
- Call sites: `Snackbar/ApiErrorLocalizer.swift:20-30` (6 keys) + `Snackbar/GlobalSnackbarHost.swift:49`
  (`snackbar.dismiss`). `Package.swift:6` `defaultLocalization: "en"`.
- The resolved locale source is the one `AppSettingsStore` extended in **T-0310 D4** (`language` + the
  System/follow-device row). The seam must read it from Core (the store is Core-owned), not from feature code.
- Verbatim ×5 source: the Android `partner-app/src/main/res/values{,-cs,-sk,-uk,-ru}/strings.xml` (and the
  customer-app siblings) error/dismiss strings — already translated.
- Reviewer-per-developer; no `security`, no `optimizer`, no Gate-DP. **Routing:** `[ios]`. **Suggested home:**
  any time after T-0310 (the switch + the writable `AppSettingsStore.language` must exist — both shipped).

## Status log
- 2026-07-19 — **done** on `feature/payroll-invoice-paid-notify` (ios). **AC1 had pre-landed** with the
  business-error snackbar catalog (`6bf55f14` migrated the Core package to a single `Localizable.xcstrings`
  — 145 keys, all ×5, the 7 Core-owned keys included; `defaultLocalization: "en"` unchanged). **This pass
  closed AC2 + AC3.** **AC2 mechanism (the implementer-seam record):** a Core-side swappable bundle —
  `CleansiaCore/Localization/CoreL10n.swift` (`apply(languageTag:)` repoints an internal
  `CoreL10n.bundle` at the matching `.lproj` **inside `Bundle.module`**; unknown tag / no feed →
  `.module` exactly as today) — the Core mirror of the T-0310 app-target mechanism (`L10n.bundle`
  repointing + root `\.locale`), chosen over `String(localized:locale:)` threading because it re-points
  ONE seam instead of passing a locale through every call site. Fed from the one `AppSettingsStore`
  resolved tag by the same two models that already repoint the app bundles (`PreferencesModel` /
  `CustomerPreferencesModel`, init + language change) — no feature-code involvement, no public-API break
  (`apply(languageTag:)` is the only new public symbol). Call sites re-pointed: `ApiErrorLocalizer`
  (catalog probe + the 6 status fallbacks) + `GlobalSnackbarHost` (`snackbar.dismiss`). **AC3:**
  `CoreL10nCatalogTests` enumerates EVERY key from the compiled en catalog (145; asserts the canonical 7
  present), resolves each in all 5 regions asserting non-empty + differs-from-en (no identical-value
  allowlist needed — zero keys are legitimately identical today), plus a per-region own-`.lproj`
  existence check (catches a silent fall-through) and the no-restart switch contract
  (`apply("cs")` → a freshly-resolved Core toast is Czech). **AC4:** CleansiaCore 340 tests,
  CleansiaPartner 457 (2 pre-existing ignorable `LocalizableCatalogFormatTests` TCC failures),
  CleansiaCustomer 578 (2 pre-existing ignorable Stripe-key Booking*SubmitTests failures) — green on
  iPhone 17 **and** on the `iPhone14-iOS16` 16.4 floor (Gate 8.5, suite-run form); swiftformat 0.60.1
  `--lint` + swiftlint 0.65.0 `--strict` clean on the changed files. Reviewer-per-developer verdict
  pending in `## Review`.
- 2026-06-27 — draft (created by PM from the T-0310 Slice C reviewer MINOR). CleansiaCore-owned user-facing
  strings (the 6 `ApiErrorLocalizer` toasts + `snackbar.dismiss`) ship en-only behind `defaultLocalization: "en"`
  + `bundle: .module`, so the new T-0310 Slice C in-app language switch does not re-localize them (English on a
  non-en device / after an in-app switch). Pre-existing debt, surfaced by T-0310; NOT a Phase-3 regression.
  Dedup-checked vs INDEX + audits: distinct from T-0333 (Android Register/Forgot i18n) and T-0337 (Android
  profile-VM i18n) — those are Android `R.string.*` fixes; this is the iOS **Core package** catalog + a
  swappable-bundle seam. `depends_on: [T-0310]`; `security_touching: false`; `manual_steps: []`; sized **S**.
  No panel (applies ADR-0013's Core-package seam + §7.7 D4 settings store; the only open call — the swappable-
  bundle mechanism — is an implementer seam recorded in-ticket, not a new architectural decision).

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
- 2026-07-19 (ios, harvest note) — the `CoreL10n.apply(languageTag:)` seam folded into `patterns-mobile.md`
  (the Preferences sub-screens bullet): a preferences model that repoints only the app `L10n.bundle` and
  not `CoreL10n` is now a documented bug class.
- 2026-07-19 (reviewer verdict) — **PASS.** The batch's adversarial review (ios-core-seams dimension:
  CoreL10n thread-safety, default-path byte-parity with `Bundle.module`, both apps repointing on init AND
  on switch, no partner/customer asymmetry) returned zero findings; AC1–AC4 evidence verified in the
  status log. Reconciled done.
