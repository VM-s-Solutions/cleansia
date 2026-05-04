# Cleansia Customer Mobile — What's Next

> **Purpose:** Paste-ready session starter. Read this at the top of a new Claude Code session to know exactly what to do next on the customer mobile app build.

---

## Quick-start prompt (paste into new session)

```
Read planning/mobile/customer-app-next-steps.md, then execute Phase 0.
Don't write code yet. Verify the Figma MCP is connected (/mcp), then
report what's in stitch_cleansia_customer_app/ and what you see in the
Figma file, then ask me the Phase 2 questions.
```

---

## Prerequisites (must be running before starting)

- [ ] **Figma Desktop app open** with the Stitch-exported design file loaded
- [ ] **Dev Mode MCP Server enabled** in Figma: Preferences → Enable Dev Mode MCP Server
- [ ] **Figma MCP registered** in Claude Code config:
  ```json
  "my-figma-server": {
    "url": "http://127.0.0.1:3845/mcp",
    "type": "http"
  }
  ```
- [ ] **Graphify installed** at `graphify-out/` (already done in this repo)
- [ ] **Stitch exports** dropped into `stitch_cleansia_customer_app/` at repo root (designs, component sheet, any tokens file, optional README)

---

## Phase 0 — Read context (first thing, every session)

Files to read in order:

1. **This file** — you're here
2. **`planning/mobile/customer-app-handoff.md`** — full 8-phase playbook with don't-dos
3. **`planning/mobile/customer-app-stitch-brief.md`** — the design brief (palette, fonts, 30-screen inventory, flows)
4. **`CLAUDE.md`** at repo root — project conventions
5. **`graphify-out/GRAPH_REPORT.md`** — codebase architecture map (use this instead of grepping)
6. **`stitch_cleansia_customer_app/README.md`** if present — user's notes on deviations or priorities

Then:
- Run `/mcp` to confirm Figma MCP (`my-figma-server`) is connected
- List what's in `stitch_cleansia_customer_app/` (screens, components, tokens)
- Query the Figma file via MCP to get the frame/screen list

**Do not write code.** Report findings and move to Phase 1.

---

## Phase 1 — Reconcile designs vs. brief

The brief specified colors, fonts, radii, screen inventory. Stitch may have deviated. Compare and document.

### Tasks

1. Query the Figma file's design tokens (colors, text styles, effects) via MCP
2. Compare against the brief's spec in `customer-app-stitch-brief.md`
3. Count the screens Stitch produced vs. the 30-screen inventory
4. Identify any components Stitch designed that weren't in the brief's component list
5. Note anything ambiguous (missing screens, renamed components, novel patterns)

### Deliverable

Create **`stitch_cleansia_customer_app/RECONCILIATION.md`** with these sections:

```markdown
# Design Reconciliation — Stitch output vs. brief

## Color palette
| Role | Brief hex | Stitch hex | Match? |
|---|---|---|---|
| Primary base | #0284c7 | <actual> | ✓ / ✗ |
...

## Typography
...

## Screen inventory
- In brief AND in Stitch: <list>
- In brief, NOT in Stitch: <list — flag as missing>
- In Stitch, NOT in brief: <list — flag as new>

## Components
- Match: <list>
- Deviations: <list>

## Open questions for user
- <e.g., "Stitch introduced a 'Promo banner' on Home — include in MVP?">
```

**Stop here and get user sign-off before Phase 2.**

---

## Phase 2 — Get 4 decisions from the user

Before any code, ask:

### Q1. Platform first
> iOS (SwiftUI), Android (Kotlin Compose), or both in parallel?

### Q2. MVP screen scope
> Which screens for v1? Default suggestion if no preference:
> - Sign in, Sign up, Email verify, Forgot password
> - Home (tab)
> - Order wizard (5 steps + payment + success)
> - Orders list, Order detail
> - Profile

### Q3. API client strategy
> OpenAPI codegen from `http://localhost:5003/swagger/v1/swagger.json`, or hand-written client?
> - iOS: Swift OpenAPI Generator (if codegen)
> - Android: Retrofit + OpenAPI generator (if codegen)

### Q4. Distribution for v1
> TestFlight + Firebase App Distribution, direct APK, production stores? Affects bundle IDs, signing, CI.

**Wait for answers. Don't scaffold until all 4 are in.**

---

## Phase 3 — Scaffold the mobile project

Once Phase 2 answers are captured:

### If iOS first

```
src/cleansia_customer_ios/
├── Cleansia.xcodeproj
├── Cleansia/
│   ├── App/              (entry point, App.swift)
│   ├── DesignSystem/     (Colors.swift, Typography.swift, Components/)
│   ├── Features/         (Auth/, Home/, Booking/, Orders/, Profile/)
│   ├── Networking/       (API client, interceptors)
│   └── Resources/        (Assets, fonts, Localizable.strings)
├── CleansiaTests/
└── README.md
```

- SwiftUI, iOS 17+ min
- Register Poppins + Nunito fonts
- Bundle ID: `cz.cleansia.customer` (or per user preference)
- Do NOT add to `Cleansia.Api.sln`

### If Android first

```
src/cleansia_customer_android/
├── app/
│   └── src/main/java/cz/cleansia/customer/
│       ├── ui/theme/     (Color.kt, Type.kt, Theme.kt)
│       ├── ui/components/
│       ├── features/
│       ├── data/         (Retrofit, DTOs)
│       └── di/           (Hilt modules)
├── build.gradle.kts
└── README.md
```

- Jetpack Compose, Min SDK 26, Target SDK 35
- Hilt, Retrofit, kotlinx.serialization — mirror `src/cleansia_android/` (partner app) conventions
- Package: `cz.cleansia.customer` (different from partner's `cz.cleansia.partner`)

### Both cases

- Create **`planning/mobile/customer-app-implementation.md`** — living tracker that logs scaffold choices, versions, decisions as they happen

---

## Phase 4 — Port the design system

Translate Figma tokens → native code.

### Tasks

1. Query Figma MCP for every design token (colors, text styles, effects, spacing)
2. Build the token layer:
   - **iOS:** `DesignSystem/Colors.swift`, `Typography.swift`, `Spacing.swift`, `Shadows.swift`
   - **Android:** `ui/theme/Color.kt`, `Type.kt`, `Theme.kt`, `Dimens.kt`
3. Build component primitives — one file per component:
   - Button (primary/secondary/ghost/destructive, 3 sizes, with/without icon, pill)
   - TextField, Textarea, Select, Multiselect, Checkbox, Radio
   - DatePicker, TimeSlotPicker, CodeInput (6-digit), PhoneInput
   - Badge (status colors), Chip, Avatar, RatingStars
   - OrderCard, ServiceCard, StepIndicator, EmptyState, SectionHeader
   - TopAppBar, BottomTabBar, OrderStatusTimeline, PriceSummaryCard
4. Light and dark mode variants for all components
5. Smoke test: render one screen's-worth of components in a preview gallery

---

## Phase 5 — API client

### Tasks

1. Confirm Customer API is running: `http://localhost:5003/swagger`
2. If codegen:
   - **iOS:** Add Swift OpenAPI Generator SPM dep, point at swagger.json, regenerate
   - **Android:** Add OpenAPI Gradle plugin, point at swagger.json, regenerate
3. Wire JWT bearer interceptor + refresh-on-401 handler
4. Store tokens in Keychain (iOS) / EncryptedSharedPreferences (Android)
5. Smoke test: `GET /api/v1/services` returns services, render in a throwaway list screen
6. Wire error handling (network errors, 4xx/5xx, offline)

---

## Phase 6 — Build MVP screens

Build in the order user prioritized in Phase 2. For each screen:

1. Query Figma MCP for the screen's frame
2. Translate layout to SwiftUI / Compose using design-system components (not raw primitives)
3. Wire to API client / local state
4. Hook into navigation (NavigationStack / NavHost)
5. Test in light AND dark mode
6. Ship one screen at a time — show the user in simulator before moving on

Default MVP order (if user didn't specify):

1. Splash
2. Sign in
3. Sign up
4. Email verify
5. Forgot password
6. Home tab (with bottom tab scaffold)
7. Book tab → Order wizard step 1 (services)
8. Order wizard step 2 (property size)
9. Order wizard step 3 (date & time)
10. Order wizard step 4 (address)
11. Order wizard step 5 (extras & instructions)
12. Order wizard step 6 (summary & payment)
13. Booking success
14. Orders tab → Orders list
15. Order detail
16. Profile tab → Profile home
17. Preferences (language switcher, theme toggle)

---

## Phase 7 — Polish & ship MVP

### Tasks

1. **i18n:** Add string catalogs for EN/CS/SK/UK/RU
   - iOS: `Localizable.xcstrings` or `.strings` files per locale
   - Android: `res/values-cs/strings.xml`, `values-sk/`, `values-uk/`, `values-ru/`
   - Mirror keys from `src/Cleansia.App/apps/cleansia.app/src/assets/i18n/en.json`
2. **Push notifications**
   - iOS: APNS + Firebase Cloud Messaging
   - Android: FCM
   - Backend wiring: register device token via Customer API
3. **Native payment**
   - iOS: Apple Pay via StoreKit + Stripe iOS SDK
   - Android: Google Pay via Stripe Android SDK
4. **Crash reporting:** Sentry (used by web app already)
5. **Biometric unlock:** FaceID / TouchID (iOS), BiometricPrompt (Android) — optional toggle in Preferences
6. **Distribution:**
   - iOS: TestFlight internal build
   - Android: Firebase App Distribution or direct APK
7. **Screenshots for store listing** (if going to production stores in this phase)

---

## Token-efficiency checklist (keep costs low)

- ✅ Use Figma MCP queries instead of reading image exports
- ✅ Use Graphify (`graphify-out/GRAPH_REPORT.md`) for architecture questions instead of grepping
- ✅ After any code changes, run the graphify rebuild command (see CLAUDE.md §graphify)
- ✅ Batch Figma queries — fetch all frames of one screen at once, not one call per layer
- ❌ Don't re-read the same brief or CLAUDE.md sections repeatedly — if it's in context, refer back
- ❌ Don't ask me to describe Stitch screens — query Figma MCP directly

---

## Files the next session will create

```
src/cleansia_customer_ios/                  (NEW — if iOS)
src/cleansia_customer_android/              (NEW — if Android)

planning/mobile/
├── customer-app-handoff.md                 (exists)
├── customer-app-stitch-brief.md            (exists)
├── customer-app-next-steps.md              (this file)
└── customer-app-implementation.md          (NEW — created in Phase 3, living tracker)

stitch_cleansia_customer_app/               (already created by user)
└── RECONCILIATION.md                       (NEW — created in Phase 1)
```

---

## Don't-do list (repeated from handoff doc — critical)

- ❌ Don't start coding before Phase 1 reconciliation and Phase 2 decisions
- ❌ Don't port the partner Android app code — different audience, different design
- ❌ Don't add mobile projects to `Cleansia.Api.sln`
- ❌ Don't pick the platform yourself — ask the user
- ❌ Don't skip light/dark mode — every screen needs both
- ❌ Don't introduce brand colors outside the palette
- ❌ Don't design web-style modals — use bottom sheets on mobile

---

## Progress tracker (update as you go)

- [ ] Phase 0: Context read, Figma MCP verified, Stitch folder inspected
- [ ] Phase 1: Reconciliation doc written, user signed off
- [ ] Phase 2: 4 decisions captured
- [ ] Phase 3: Project scaffolded
- [ ] Phase 4: Design system ported to native
- [ ] Phase 5: API client wired, smoke-tested
- [ ] Phase 6: MVP screens built
- [ ] Phase 7: i18n, push, payment, crash reporting, distribution
- [ ] MVP shipped to TestFlight / Firebase App Distribution
