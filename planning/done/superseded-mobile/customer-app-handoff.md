# Cleansia Customer Mobile App — Session Handoff

> **Purpose:** Read this file at the start of a new Claude Code session to pick up the customer mobile app build with full context. Self-contained — no prior session memory required.

---

## Current state (as of 2026-04-13)

### What's been done

1. **Customer web app exists and is shipping** — Angular 19 SSR app at `src/Cleansia.App/apps/cleansia.app/`. This is the source-of-truth for design language and feature set, but the mobile app is a fresh build, not a port of the web codebase.

2. **A Stitch AI design brief was written** at `planning/mobile/customer-app-stitch-brief.md`. It captures the existing brand (sky-blue palette `#0284c7`, Poppins/Nunito typography, 5 languages, 16/24px radii), the 6 core user flows, 30-screen inventory, and mobile-specific additions (bottom tabs, Apple Pay / Google Pay, push notifications, biometric unlock, etc.).

3. **Stitch AI generated the designs.** The exports are dropped into the project root at `stitch_cleansia_customer_app/` (sibling to `src/`, `docs/`, `planning/`).

4. **Customer API (backend) is live and stable.** It runs on port 5003 (`src/Cleansia.Web.Customer/`). The mobile app will consume it.

5. **No customer mobile app code exists yet.** The existing `src/cleansia_android/` is the *partner* (employee) app — different audience, different design, must NOT be confused with the customer app.

### What's NOT been done

- No mobile project scaffolded (no `src/cleansia_customer_ios/` or `src/cleansia_customer_android/` yet)
- No design-system port from Stitch designs to native code
- No API client codegen for mobile
- No CI/CD entry for mobile builds
- No mobile-specific entries in CLAUDE.md or planning/active/

---

## What the next Claude session should do

Tasks are listed in dependency order. **Stop and confirm with the user after each major phase** — don't run ahead.

### Phase 0 — Read context (do this first, every session)

1. Read this file (you are here).
2. Read `planning/mobile/customer-app-stitch-brief.md` for the design brief Stitch worked from.
3. Read `CLAUDE.md` at the project root for project conventions.
4. Inspect `stitch_cleansia_customer_app/` to see what designs are actually there. Look for:
   - PNG/JPG exports of screens
   - A component library sheet
   - Any tokens file (JSON, CSS, etc.)
   - A README from the user noting deviations or priorities
5. Read the user's README inside `stitch_cleansia_customer_app/` if present — it overrides anything in the brief.

### Phase 1 — Reconcile designs vs. brief

Stitch may have made decisions that diverge from the brief. Before writing any code:

- Compare Stitch's color palette to the brief. If it shifted the primary blue or introduced new colors, document the actual values.
- Compare screens. Did Stitch add/remove/rename screens? List the actual screen inventory.
- Identify the components Stitch designed. Compare to the brief's component list (atoms / molecules / organisms).
- **Output:** a short markdown file `stitch_cleansia_customer_app/RECONCILIATION.md` listing every deviation. Get user sign-off before proceeding.

### Phase 2 — Get answers from the user (4 questions)

Before scaffolding anything, ask the user:

1. **Platform first:** iOS (SwiftUI), Android (Kotlin Compose), or both in parallel?
2. **MVP scope:** Which screens are v1 must-haves? Default suggestion if user has no preference: Sign in, Sign up, Email verify, Home, Order wizard (5 steps + payment), Booking success, Orders list, Order detail, Profile.
3. **API client:** OpenAPI codegen (similar to NSwag for the web) or hand-written? The Customer API exposes Swagger at `http://localhost:5003/swagger`.
4. **Hosting / distribution plan for v1:** TestFlight + Firebase App Distribution? Direct APK side-load? Production stores? This affects bundle identifiers, signing, and CI.

### Phase 3 — Scaffold the project

Once Phase 2 answers are in:

**For iOS (if chosen):**
- Create `src/cleansia_customer_ios/` with an Xcode workspace
- SwiftUI app, iOS 17+ minimum
- Folder structure: `App/`, `DesignSystem/`, `Features/`, `Networking/`, `Resources/`
- Add `.gitignore` for Xcode artifacts
- Add to `Cleansia.Api.sln`? — NO, the .NET solution doesn't host iOS. Just add to the repo root.

**For Android (if chosen):**
- Create `src/cleansia_customer_android/` mirroring the structure of `src/cleansia_android/` (the partner app) but with package `cz.cleansia.customer`
- Kotlin + Jetpack Compose, Min SDK 26, Target 35 (match partner app)
- Hilt for DI, Retrofit for networking, kotlinx.serialization
- Architecture: MVVM + Clean Architecture, mirror the partner app's conventions

**For both:**
- Document the choice in a new file `planning/mobile/customer-app-implementation.md` (not stitch-brief, not handoff — this becomes the living implementation tracker)

### Phase 4 — Port the design system

Translate the Stitch designs into reusable native components:

**iOS:**
- `DesignSystem/Colors.swift` — every color from the palette as `Color` extensions, with light/dark variants
- `DesignSystem/Typography.swift` — Poppins (headings) and Nunito (body) registered, with a Type scale
- `DesignSystem/Components/` — Button, TextField, Card, Badge, StepIndicator, etc. — one Swift file per component
- Match the corner radii (12/16/24/pill), shadow tiers, glass morphism style

**Android:**
- `app/src/main/java/cz/cleansia/customer/ui/theme/` — Color.kt, Type.kt, Theme.kt
- Material 3 base, override with brand colors
- `ui/components/` — composable functions for each design-system primitive

### Phase 5 — API client

- If OpenAPI codegen: set up the toolchain (Swift OpenAPI Generator for iOS, Retrofit-OpenAPI for Android), point at `http://localhost:5003/swagger/v1/swagger.json`, regenerate
- Wire up auth interceptors (JWT bearer, refresh on 401)
- Smoke test: hit `GET /api/v1/services` and render the response

### Phase 6 — Build screens (MVP)

In the order user prioritized in Phase 2. For each screen:
1. Implement the screen from the Stitch design
2. Wire to the API client
3. Hook up navigation (NavigationStack on iOS, NavHost on Android)
4. Test on simulator/emulator in both light and dark mode
5. Confirm with user before moving to next screen

### Phase 7 — Polish & ship MVP

- i18n: add string catalogs for EN/CS/SK/UK/RU (mirror keys from `apps/cleansia.app/src/assets/i18n/`)
- Push notifications setup (FCM for both platforms, APNS for iOS)
- Apple Pay / Google Pay integration in checkout
- Crash reporting (Sentry — already used by web)
- Build & distribute per Phase 2 answer

---

## Important context the next session must know

### Project conventions (from CLAUDE.md)

- This is a multi-tenant cleaning marketplace platform
- Backend: .NET 10, PostgreSQL 16, EF Core 10, MediatR (CQRS), Aspire orchestration
- Web frontends: Angular 19 in an Nx monorepo at `src/Cleansia.App/`
- Existing partner mobile app: native Kotlin Compose at `src/cleansia_android/`
- Languages: 5 (EN, CS, SK, UK, RU) — mobile must support all 5
- Brand color: `#0284c7` (sky blue)
- Order lifecycle: New → Pending → Confirmed → InProgress → Completed (or Cancelled)

### Things the web app does NOT have that mobile MUST add

Per the Stitch brief (read it for the full list):
- Bottom tab navigation (web uses sidebar)
- Native payment sheets (Apple Pay / Google Pay)
- Push notifications
- Biometric unlock (FaceID/TouchID)
- GPS-based address autofill
- Pull-to-refresh, swipe actions on lists
- Bottom sheets instead of modals
- Haptic feedback on CTAs

### Backend touchpoints

- **Customer API base URL:**
  - Local dev: `http://localhost:5003`
  - DEV: ask user, likely `https://api-customer-dev.cleansia.cz`
  - PROD: ask user
- **Auth:** JWT bearer tokens, refresh flow exists. Endpoints under `/api/v1/auth/*`
- **Order endpoints:** `/api/v1/orders/*`
- **Services / packages:** `/api/v1/services`, `/api/v1/packages`
- **Payment:** Stripe checkout sessions — backend creates session, mobile redirects to Stripe-hosted checkout OR uses native Apple Pay / Google Pay → backend webhook confirms

### Files & folders the next session will create

```
src/
├── cleansia_customer_ios/           (NEW — if iOS first)
│   ├── Cleansia.xcodeproj
│   ├── Cleansia/
│   │   ├── App/
│   │   ├── DesignSystem/
│   │   ├── Features/
│   │   ├── Networking/
│   │   └── Resources/
│   └── README.md
│
└── cleansia_customer_android/       (NEW — if Android first)
    ├── app/
    │   └── src/main/java/cz/cleansia/customer/
    └── README.md

planning/mobile/
├── customer-app-stitch-brief.md     (already exists — input to Stitch)
├── customer-app-handoff.md          (this file — read at every session)
├── customer-app-implementation.md   (NEW — living implementation tracker, created in Phase 3)
└── ios-implementation.md            (legacy — partner iOS plan, ignore for customer work)

stitch_cleansia_customer_app/        (NEW — Stitch design exports, dropped by user)
├── README.md                        (user's notes — read this!)
├── screens/                         (PNG/JPG per screen)
├── components/                      (component library sheet)
└── tokens/                          (if Stitch exported any)
```

### What NOT to do

- ❌ Don't start coding before reading the Stitch designs and reconciling deviations
- ❌ Don't port the partner Android app code — different audience, different design, will mislead
- ❌ Don't add the iOS/Android projects to `Cleansia.Api.sln` — the .NET solution doesn't host them
- ❌ Don't pick the platform yourself — ask the user (Phase 2 question 1)
- ❌ Don't run `npm run generate-*-client` — that's for the web app
- ❌ Don't commit Stitch design exports without checking file size — large image folders may belong in git LFS or a separate asset repo

---

## Quick-start prompt for the next session

If you want to drop into a new Claude session and have it pick up immediately, paste this:

> Read `planning/mobile/customer-app-handoff.md` then start at Phase 0. Don't write any code yet — just read context and tell me what's in `stitch_cleansia_customer_app/`, then ask me the Phase 2 questions.

---

## Status checklist (update as you progress)

- [ ] Phase 0: Context read, Stitch folder inspected
- [ ] Phase 1: Reconciliation doc written, user signed off
- [ ] Phase 2: Platform / MVP / API client / distribution decisions captured
- [ ] Phase 3: Project scaffolded
- [ ] Phase 4: Design system ported
- [ ] Phase 5: API client wired and smoke-tested
- [ ] Phase 6: MVP screens built
- [ ] Phase 7: i18n, push, payments, crash reporting, distribution
- [ ] MVP shipped to TestFlight / Firebase App Distribution

---

## Related docs

- **Design brief (input to Stitch):** [planning/mobile/customer-app-stitch-brief.md](customer-app-stitch-brief.md)
- **Project conventions:** [CLAUDE.md](../../CLAUDE.md) at repo root
- **Partner Android app (reference only — different audience):** [src/cleansia_android/](../../src/cleansia_android/)
- **Customer web app (source of feature truth):** [src/Cleansia.App/apps/cleansia.app/](../../src/Cleansia.App/apps/cleansia.app/)
- **Customer API (backend):** [src/Cleansia.Web.Customer/](../../src/Cleansia.Web.Customer/)
