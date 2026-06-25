# iOS — Mac Handover

> **Read this first when you move iOS development to the Mac.** It orients you: where the project
> stands, what's done vs. what's next, the exact first-session sequence, and how to keep working with
> Claude on the Mac. The step-by-step *commands* live in [`MANUAL_STEPS.md`](MANUAL_STEPS.md) and
> [`README.md`](README.md) — this doc is the map, not the manual.
>
> **Branch:** `master`. **iOS lives at** `src/cleansia_ios/`. Last iOS commit at handover: `c1009c63`.

---

## 1. Where the whole project stands (30-second picture)

Cleansia is a cleaning-services platform: **.NET 10 backend, Angular web (3 apps), Kotlin/Compose
Android** — and now **iOS** (Swift/SwiftUI), built as a **parity port** of the Android apps.

| Track | Status |
|---|---|
| Backend + web | Shipped, in the repo. |
| **Azure dev environment** | Bicep authored + deploying; **owner is finishing it** (the APIs boot once the last re-deploy lands — Sentry + ForwardedHeaders + secret fixes are all in). The iOS apps will point at the `api-cleansia-{partner,customer}-mobile-weu-dev.azurewebsites.net` hosts. |
| **iOS Phase 0 (foundation)** | ✅ **DONE + committed.** Built on Windows (pure code, no toolchain). 65 Swift files + 11 tests. |
| **iOS Phase 1+ (screens)** | ⏳ **Next — needs the Mac.** |

---

## 2. What iOS Phase 0 delivered (what you're inheriting)

A complete, reviewed foundation — **not stubs** (the auth spine was independently verified: real Keychain
storage, actor-based single-flight refresh, the device-id header contract):

- **Xcode workspace + `CleansiaCore` SPM package** (iOS-16 floor) + the **CleansiaPartner / CleansiaCustomer**
  app targets (XcodeGen `project.yml`, bundle ids `cz.cleansia.{partner,customer}`).
- **Auth/session/header spine** (`CleansiaCore/Auth/`) — hand-written, mirrors Android `core/auth`:
  `KeychainTokenStore`, `AuthClient` + separate no-auth refresh session, `actor SessionRefresher`
  (single-flight 401→refresh→retry, replace-refresh-token), `HeaderAdapter` (X-Device-Id == Device/Register
  id, X-Device-Label, X-Time-Zone, no-Bearer-on-anon allow-list), `DeviceIdProvider`, `SessionManager`.
- **Design system** (spacing/colors/typography tokens) + **native SwiftUI components** that mirror the
  Compose ones (Button/TextField/PhoneInput/Dropdown/Checkbox/Dialog/SectionHeader/CodeInput/EmptyState).
- **State** = sealed `UiState`/`ActionState` enums + `ObservableObject` view-model base (ADR-0014: NOT
  `@Observable`, which is iOS-17-only).
- **DI** composition root (`AppContainer` per app), **global snackbar/error center**, the **codegen
  toolchain wiring**, and the **header-parity contract doc** (`docs/header-parity-contract.md`).
- **Strict SwiftLint + SwiftFormat** configs (the ADR-0016 quality gate).
- 3 intentional later-phase namespaces: `Format`, `Location`, `Push` (Push = T-0311).

**The governing decisions** are in the ADRs — read these, don't re-litigate:
`agents/backlog/adr/0013` (architecture), `0014` (iOS-16 + ObservableObject), `0016` (Apple App Review),
`0018` (design parity / Gate-DP). Plan + tickets: `agents/backlog/status/sprint-12.md`. The big-picture
handoff: `agents/IOS-AND-AZURE-HANDOFF.md`.

---

## 3. Why the Mac, and what changes

Phase 0 was pure code authoring against the Android reference — that worked fine on Windows. **Phase 1+
needs a real toolchain**, which only the Mac has:

| Needs the Mac | Why |
|---|---|
| `xcodegen generate` + open the workspace | XcodeGen + Xcode |
| `swift build` / `swift test` | Swift toolchain |
| **Generate the API client** | openapi-generator + the specs |
| Run in a **simulator** / on a device | Xcode + signing |

So from here on, **iOS development happens on the Mac.** The first thing to do is prove Phase 0 actually
compiles — it was verified *structurally* on Windows (no duplicate symbols, idiomatic Swift, correct
imports), but the definitive `swift build` only happens on the Mac. Better to confirm a 65-file
foundation builds than to stack features on an unverified base.

---

## 4. First session on the Mac — do this in order

> Full commands: [`MANUAL_STEPS.md`](MANUAL_STEPS.md). The short version:

1. **Toolchain** (once): `brew install xcodegen swiftlint swiftformat openapi-generator`
2. **Generate the Xcode projects** + open the workspace:
   ```sh
   cd src/cleansia_ios/CleansiaPartner  && xcodegen generate
   cd src/cleansia_ios/CleansiaCustomer && xcodegen generate
   open src/cleansia_ios/Cleansia.xcworkspace
   ```
3. **Verify Phase 0 builds** (the critical checkpoint):
   ```sh
   cd src/cleansia_ios/CleansiaCore && swift build && swift test
   ```
   Then build both app schemes for an **iOS-16 simulator** in Xcode. **If anything fails, fix it here
   before any Phase 1 work** — this is the structural-verification gap closing.
4. **Lint check:** `swiftlint --strict` and `swiftformat --lint .` at `src/cleansia_ios/` — the configs
   are checked in; this is the ADR-0016 bar.
5. **Bundle the brand fonts** (Poppins/Nunito `.ttf`) into each app — see MANUAL_STEPS §6. Until then the
   apps fall back to the system font but still build.

**Then, before Phase 1 features** (these are the two real prerequisites):
6. **The Azure dev API must be live** — confirm `https://api-cleansia-partner-mobile-weu-dev.azurewebsites.net`
   responds (the owner is finishing this deploy). The app's `API_BASE_URL` in each `project.yml` already
   points at the `-mobile-weu-dev` hosts.
7. **Generate the API client** (`manual_step: mobile-spec-regen`) — refresh the committed mobile specs
   from the running hosts, then run `scripts/generate-api-clients.sh`, then wire the generated packages
   into each `project.yml`. See MANUAL_STEPS §7 + `openapi/README.md`. The auth client stays
   hand-written (excluded from codegen).

---

## 5. What's next after Phase 0 verifies — Phase 1

Per `sprint-12.md`: **the partner lead vertical (T-0303)** — partner **login → read-only Dashboard** —
proving the whole architecture end-to-end (hand-written auth → Keychain → headers → generated client →
`UiState` rendering) on a real screen. Partner-first is deliberate (ADR-0013): its first authed screen is
read-only, so it proves the foundation without Mapbox/Stripe/Google. Then the Phase-2 feature waves by
complexity (the hard areas: the customer booking wizard + Stripe, maps across both apps, the partner
order work-loop).

**Every screen ticket** is held to: SwiftLint/SwiftFormat (blocking), **Gate-AR** (Apple App Review,
ADR-0016 + `agents/backlog/ios-app-review-checklist.md`), and **Gate-DP** (design parity — looks like the
cited Android Compose screen, ADR-0018).

---

## 6. Keeping Claude in the loop on the Mac

You have two ways to work with Claude from here:

- **Best: run Claude Code ON the Mac.** Then Claude has the full toolchain — it can `swift build`, run
  tests, generate the client, and drive Phase 1 **compile-verified** (the same way it became reliable on
  the Bicep once it had the bicep CLI). This is the recommended setup for iOS. Point it at this doc + the
  ADRs and it can pick up cold.
- **Alternative: keep Claude on Windows** authoring Phase 1 screens against the Android reference, with
  **you** compiling/running on the Mac and feeding back errors. Workable, but slower and re-introduces
  the "can't verify it builds" gap that produced stubs earlier. Prefer the first option.

**To resume cold (either way), point Claude at:** this doc → `agents/IOS-AND-AZURE-HANDOFF.md` →
`sprint-12.md` → the ADRs (0013/0014/0016/0018) → skim the Android reference for the screen you're
porting (`src/cleansia_android/{partner,customer}-app/.../features/<screen>`).

---

## 7. Owner-only / blocked items (quick reference)

- **Apple Developer signing** — Team + provisioning profiles for `cz.cleansia.{partner,customer}`
  (MANUAL_STEPS §4). Needed to run on a device / TestFlight.
- **APNs auth key** — for push (T-0311).
- **Sign in with Apple** — required on the customer app (Guideline 4.8, it offers Google Sign-In); built
  with the customer auth wave via a backend `appleauth` endpoint (ADR-0016).
- **Fonts** (MANUAL_STEPS §6) and the **mobile-spec regen** (§7) — both yours.
- **Azure dev** — finish the deploy so the iOS apps have a backend (owner is on this).

---

## 8. The one-paragraph status (paste at the top of a fresh Mac session)

> Cleansia iOS = Swift/SwiftUI parity ports of the Android apps (`src/cleansia_android/`), at
> `src/cleansia_ios/` on `master`. **Phase 0 (foundation) is done + committed** — the CleansiaCore SPM
> package (iOS-16, ObservableObject), 2 app targets, the hand-written auth/session/header spine, design
> system, native SwiftUI components, DI, snackbar, codegen wiring; 65 Swift + 11 test files. It was
> authored on Windows and verified structurally; **the first Mac task is `xcodegen generate` → open
> `Cleansia.xcworkspace` → `swift build && swift test` → build both app schemes for an iOS-16 simulator**
> to confirm it compiles. Then (needs: the Azure dev API live + the mobile-spec regen) proceed to **Phase
> 1 = the partner login → Dashboard vertical (T-0303)** per `agents/backlog/status/sprint-12.md`. Governing
> decisions: ADR-0013/0014/0016/0018. Quality gates per screen: SwiftLint/SwiftFormat + Gate-AR + Gate-DP.
