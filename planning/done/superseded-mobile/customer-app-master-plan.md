# Cleansia Customer App — Master Plan

> **Scope:** this document tracks the end-to-end plan to take the Android customer app from its current polished-UI state to a fully launched, store-compliant product with backend integration, iOS parity, and web parity. It is the source of truth across Claude sessions — pick up here, don't re-derive.

**Status (2026-04-18):** Android UI/UX is feature-complete for MVP. Dark theme polished. No backend wiring yet. No biometric auth. No analytics/crash reporting. iOS customer app does not exist. Web customer app exists but lags in some features (map address picker, at minimum).

**Owner:** Michael Chaban.

---

## Guiding principles

1. **Phase gates, not big-bang.** Each phase ends with a written artifact Michael reviews before the next phase starts. No surprise scope creep.
2. **Parity first, features second.** Before adding new features to any platform, the three apps (web, Android, iOS) should expose the same core flows.
3. **Contract-first backend integration.** Lock the API shape, generate typed clients, THEN wire screens. No ad-hoc fetch calls.
4. **Don't start iOS until Android is stable enough to mirror.** Concretely: don't start iOS until Phase 3 is done. A moving target produces divergence.

---

## Phase overview

| # | Phase | Output | Status |
|---|---|---|---|
| 1 | Play Store readiness audit (Android) | `customer-app-playstore-audit.md` + punch list | **Done** |
| 1.5 | Backend: refresh token migration | `refresh-token-migration-plan.md` + new entity/endpoints + web apps updated | **Planned, awaiting execution** |
| 2 | Phase 1 blocker fixes (Android, depends on 1.5 for auth) | PRs for each blocker | Partially planned |
| 3 | Web ↔ Android feature parity audit | `customer-app-web-android-parity.md` (written diff) | Not started |
| 4 | Parity fixes (web + Android, one feature per PR) | Feature parity reached | Not started |
| 5 | Backend contract lock-in + typed client generation | Kotlin API client + auth flow design | Not started |
| 6 | Android ↔ backend integration (screen by screen) | Real data everywhere, no mocks | Not started |
| 7 | Android final polish + Play Store internal track release | First APK to internal testers | Not started |
| 8 | iOS customer app — architecture doc | `customer-app-ios-spec.md` (based on shipped Android, not aspiration) | Not started |
| 9 | iOS customer app — implementation (Michael does this on Mac with Claude in new thread) | Shipped iOS app | Not started |
| 10 | Cross-platform regression suite + release cadence | Documented release process | Not started |

---

## Phase 1 — Play Store readiness audit (Android)

**Goal:** identify everything Google Play will check before accepting the Android app.

**Output:** `planning/mobile/customer-app-playstore-audit.md` with:
- Store policies that currently apply (data safety, permissions, target SDK, signing, content rating)
- Technical gaps (R8, ProGuard rules, app bundle config, keystore, versionCode strategy)
- Security gaps (biometric auth, network security config, certificate pinning, token storage)
- Privacy gaps (privacy policy URL, data deletion flow, consent, GDPR/Czech-specific)
- Accessibility basics (content descriptions, touch targets, contrast)
- A prioritized punch list — each item tagged P0 (blocker) / P1 (pre-launch) / P2 (nice-to-have)

**Michael reviews, then approves which items to execute.**

**NOT in Phase 1 scope:**
- Actually writing a privacy policy (legal work)
- Applying for developer account / verification (Michael does this)
- Creating store listing copy / screenshots (marketing)

---

## Phase 2 — Phase 1 blocker fixes (Android)

**Input:** approved P0 items from Phase 1 report.

**Output:** a PR per item. Each PR should:
- Reference the specific audit finding
- Include the code change
- Include any config file updates (gradle, manifest, proguard)
- Include a verification step Michael can run locally

**Expected items (predicted, Phase 1 will confirm):**
- Biometric auth for session re-entry
- ProGuard/R8 rules for Mapbox, Hilt, Retrofit, Kotlin Serialization
- `networkSecurityConfig` + `android:usesCleartextTraffic="false"`
- Crashlytics or Sentry integration
- Analytics baseline (Firebase Analytics or alternative — Michael picks)
- Signing config + keystore generation instructions
- `versionCode` + `versionName` strategy
- App bundle (AAB) build verification
- Accessibility passes (talkback labels, touch target 48dp minimum, contrast)
- Data safety section content (for Play Console form)

---

## Phase 3 — Web ↔ Android feature parity audit

**Goal:** list every feature the web customer app has that Android doesn't, and vice versa.

**Output:** `planning/mobile/customer-app-web-android-parity.md` with:
- **Feature matrix** — one row per feature, columns for web/Android/iOS (iOS empty for now)
- For each mismatch: which direction should it go? (Promote to all, or drop from the one that has it)
- Backend-API-side differences — endpoints used by one client but not the other
- Design-token differences — color palette, typography scale, spacing, component vocabulary

**Known gaps going in:**
- Web: no map-based address picker (Android has it)
- Web: no biometric auth (not applicable)
- Web: no offline mode
- Web customer app is an Angular SSR app; Android is Compose

**Deliberately not addressing in Phase 3:** the Partner and Admin web apps — they're separate products with different audiences.

---

## Phase 4 — Parity fixes

One feature per PR. Each touches both platforms where applicable. Web changes go to `src/Cleansia.App/apps/cleansia.app/`; Android changes go to `src/cleansia_customer_android/`.

Every PR verifies:
- Same data flows through both
- Same success/error states
- Same copy in all 5 languages (en/cs/sk/uk/ru)

---

## Phase 5 — Backend contract lock-in

**Before** any integration, write down:

1. **Auth flow** — JWT obtain, refresh, biometric unlock of cached refresh token, logout, session timeout
2. **Endpoint surface** — list all endpoints the customer app will call. Group by feature (auth, addresses, booking, orders, payments, profile)
3. **Error shape** — standardized error envelope (status code, business error key, user-facing message)
4. **Pagination + filtering conventions** — match backend's existing `PagedData<T>` shape
5. **Offline strategy** — what's cached, what's network-only, how staleness is handled
6. **Client generation** — NSwag for web already exists. For Android, either:
   - Use the same OpenAPI spec with a Kotlin generator (OpenAPI Generator, Kotlin-multiplatform codegen)
   - OR hand-write with Retrofit + kotlinx.serialization (current setup)
   **Michael to decide.**

**Output:** `planning/mobile/customer-app-api-contract.md` + a worked example (one endpoint fully wired end to end).

---

## Phase 6 — Android ↔ backend integration

One screen at a time. Order of integration:

1. **Auth** (sign in, sign up, email verify, forgot password, logout)
2. **Profile** (read, edit, avatar upload)
3. **Addresses** (list, create, update, delete, set default) — replaces DataStore mock
4. **Services + Packages catalog** (read for booking step 1)
5. **Booking create** (submit order with chosen services/packages/address/time)
6. **Orders** (list, detail, cancel)
7. **Payments** (Stripe integration via PaymentSheet)
8. **Rewards** (read balance, referral code)
9. **Notifications** (push via FCM, in-app list)

Each step replaces the screen's in-memory mock with a real repository call. Loading + error states become real, not simulated.

---

## Phase 7 — Android final polish + internal track release

- Performance pass (Compose recomposition counts, network jank, cold-start time)
- Memory leak audit (LeakCanary in debug)
- Bundle size audit
- Internal test track upload to Play Console
- Smoke test on 3+ real devices (low-end, mid, high-end)
- Tag v0.1.0 in git

---

## Phase 8 — iOS customer app architecture doc

**Input:** shipped Android app (phases 1–7 done).

**Output:** `planning/mobile/customer-app-ios-spec.md` covering:
- SwiftUI vs UIKit decision (recommend SwiftUI, iOS 16+ target)
- MVVM with Observation framework (iOS 17+) or Combine
- Feature → screen mapping (one entry per Android feature with the corresponding iOS approach)
- Native-only differences Michael needs to know about:
  - Biometric = LocalAuthentication (Face ID / Touch ID)
  - Push = APNS
  - Map = MapKit OR Mapbox iOS SDK (pick same as Android for consistency)
  - Keychain for tokens (not Keystore)
  - Sign in with Apple is mandatory if Google Sign-In is offered
  - Stripe iOS SDK + Apple Pay
- App Store compliance checklist (privacy nutrition labels, tracking transparency, age rating, etc.)
- Implementation phase plan

**NOT in scope:** writing Swift code. Michael does that on Mac in a separate Claude session.

---

## Phase 9 — iOS implementation (Michael, separate Claude session)

Not planned here. Michael will:
1. Pull the repo on Mac
2. Start a fresh Claude session
3. Hand Claude `customer-app-ios-spec.md` + `customer-app-master-plan.md`
4. Build + test on real iPhone

Location in repo: `src/cleansia_customer_ios/` (new folder).

---

## Phase 10 — Release cadence + cross-platform regression

- Define release checklist (all 3 apps)
- Semantic versioning across all platforms
- Staged rollout strategy
- Bug triage flow (which platform? regression? which release?)

---

## Running decision log

| Date | Decision | Rationale |
|---|---|---|
| 2026-04-18 | iOS lives in same monorepo (`src/cleansia_customer_ios/`), not separate repo | Matches existing web/Android/backend layout; nothing to gain from split |
| 2026-04-18 | Delay iOS until Android is shipped + web parity reached | Avoids chasing a moving target |
| 2026-04-18 | Write master plan to disk so no work is lost across Claude sessions | Context-window resilience |
| 2026-04-18 | Insert Phase 1.5: introduce refresh tokens before Android auth wiring | Greenfield now; forced migration later is expensive. Benefits web apps too. |
| 2026-04-18 | Reuse `Cleansia.Web.Customer` API (no `Cleansia.Web.Customer.Mobile` split) | Customer feature set small enough for single API; avoids NSwag double-sync overhead |
| 2026-04-18 | Use OpenAPI Generator gradle plugin for Kotlin client (not hand-rolled Retrofit) | Same drift-prevention argument that makes NSwag worth it for web |
| 2026-04-18 | Drop biometric auth from Phase 2 | Weak fit for low-frequency service app; EncryptedSharedPreferences is the real security boundary |
| 2026-04-18 | API base URL via `~/.gradle/gradle.properties` → BuildConfig (pattern matches Mapbox token) | Consistent secret handling; one override for real-device testing |

---

## What's explicitly out of scope for this plan

- **Cleansia Partner app (Android + iOS)** — separate product, separate plan exists (`android-implementation.md`, `ios-implementation.md`)
- **Cleansia Admin app** — web only, no mobile plans
- **Backend feature development** — this plan assumes the backend exposes what's needed. Backend changes required to serve the customer app are tracked inside the relevant phase but not planned here.
- **Marketing, ASO, store listings, screenshots** — product work, not engineering
- **Legal docs** — Terms of Service, Privacy Policy text — must be drafted by a lawyer or a proper service; the app just needs to link to them

---

## How to resume this plan in a future Claude session

1. Share this file's path with Claude: `planning/mobile/customer-app-master-plan.md`
2. Say "we are currently in Phase X, here's where we left off"
3. Claude re-reads the relevant phase section + any produced artifacts
4. No need to re-explain context

## Related documents (produced as phases complete)

- `customer-app-playstore-audit.md` — Phase 1 output
- `refresh-token-migration-plan.md` — Phase 1.5 plan
- `customer-app-web-android-parity.md` — Phase 3 output
- `customer-app-api-contract.md` — Phase 5 output
- `customer-app-ios-spec.md` — Phase 8 output
