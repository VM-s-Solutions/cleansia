# Backlog Index

The manifest of every ticket. The **PM owns this file** and updates it on every state transition.
One row per ticket. Source of truth for "what's the team doing right now".

## Legend
- **Status:** draft · ready · in_progress · in_review · qa · done · blocked
- **Size:** S · M · L
- **Layers:** analyst, architect, db, backend, frontend, android, ios, docs

## Active

> ## 📱 PHASE/IOS-FIX1 — on-device iOS-16 shakeout (sprint-12): **16 owner-reported issues → 4-cluster diagnosis → 6 slices + 1 process ticket — ALL 6 SLICES DONE · PHASE GATE PASS · PR DRAFTED** (2026-07-03, `phase/ios-fix1`, 11 commits pushed; **remaining acceptance: the OWNER DEVICE PASS**)
>
> **The device-verification phase.** The owner tested BOTH iOS apps on a real **iOS 16** iPhone for the first
> time and reported **16 issues**; a 4-cluster diagnosis (**booking-ux · brand-assets · navigation-shell ·
> data-layer**, 2026-07-02) grounded ALL of them with file:line evidence — the diagnosis workflow is
> **verified** (every finding cause-rooted, several empirically reproduced on a booted iOS 16.4 simulator, the
> "non-optional field" suspicion DISPROVEN by an executed decode test). **Key structural lesson:** the
> crash/⚠️/island defects were **invisible on the latest-runtime simulator** (iOS 17+ nav-authority rework +
> system tab-bar styling masked them) and the brand misses were invisible to a gate whose citation unit is the
> `.kt` screen file — hence the T-0374 process ticket. **T-0314 addendum below: feature-complete ≠
> device-verified — this phase is the device-verification debt.** Filed **T-0368…T-0374** (each ticket embeds
> its cluster's evidence):
>
> | ID | Slice | Title | Size | Status | depends_on | Layers | sec | manual_step | Cluster |
> |----|-------|-------|------|--------|-----------|--------|-----|-------------|---------|
> | **T-0368** | **A** | **Customer shell RESTRUCTURE** — single shell `NavigationStack` + page-style pager + the custom pill-bar/FAB composite; fixes the iOS-16 `comparisonTypeMismatch` **CRASH** (Plus route), the yellow-⚠️ placeholder pushes, the never-ported island bar, the FAB overlap/Rewards clipping, and tab-swipe parity. iOS-16-specific, **SHIP-BLOCKER**. **Architect ruling IN FLIGHT** (changes the ADR-0020 customer-shell pattern; fallback = minimal outer-stack delete + `NavigationPath` conversion) | M | **done ✅** `d34eaf5e` (ruling ACCEPTED as **ADR-0022** `987f85f0`; reviewer workflow-verified, findings ROUTED → folded in `fef5745c`; 16.4 smoke 0 NavigationAuthority/comparisonTypeMismatch hits; signed-in walk = owner device pass) | — | architect, ios | no | — | navigation-shell (+ the nested-stack finding of data-layer) |
> | **T-0369** | **B** | **Partner nav-crash MIRROR** — `PartnerRootView.swift:17` identical outer-`NavigationStack` topology; minimal fix (delete the outer stack + `NavigationPath` conversion; NO restructure, ADR-0020 preserved) | S | **done ✅** `4e38a93f` (reviewer **APPROVE clean**; FIVE path sites converted incl. the unlisted RegistrationLockView one — gate #24 byte-unchanged; Partner 366/366; 16.4 smoke 0 hits. Ride-along: the Slice-B lint run exposed the ios-ci master-push bypass → closed `197352a9`, no ticket) | — | ios | no | — | navigation-shell (NOTES) |
> | **T-0370** | **C** | **Data-layer fixes** — `MembershipStatus` `[SwaggerEnumAsInt]` + spec re-dump + BOTH-client regen + the Android `IntEnumSerializersModule` entry (the contract lie kills `GetMyMembership` for any subscribed user, both platforms); `apiResponseQueue` off main (both apps) + parallel prefetch (the "lag"); offset-less date-decoder hardening; `ApiError.fromGenerated` → ProblemDetails code extraction (24/25 call sites showed raw JSON — **ABSORBS T-0367**) | M | **done ✅** `5252bfb9`+fold `ebf2fcfd` (reviewer PASSED on substance, 3 test/guard CHANGES folded red→green; **the manual steps ran IN-BRANCH** — disposable-postgres spec re-dump + Kotlin client regen, Android compile green, iOS client CI-regenerated; dotnet **1714/1714** incl. `MobileSpecEnumGuardTests` pinning every mobile enum schema to integer; closes T-0367) | — | backend, ios, android | no | **owner: VERIFY the committed spec diff at the PR (or re-run `scripts/refresh-mobile-spec.sh` + regens) — executed in-branch, see the ticket's AC2 deviation** | data-layer |
> | **T-0371** | **D** | **Booking wizard UX** — SlideToConfirm was NEVER implemented (static + tap w/ silent guard) → hoist the partner's working control to `CleansiaCore` + `resetTrigger`; in-sheet snackbar host (the root host is occluded by EVERY modal sheet — also promo/referral/Stripe-cancel) + `.profileIncomplete` → dismiss+edit-profile (Android parity); `BookingViewModel` hoisted out of the sheet so the draft survives dismissal. Serialize `CustomerShellView.swift` edits with T-0368 | M | **done ✅** `fef5745c`+fold `bfb1ca7a` (reviewer CHANGES: the **D-1 adversarial-verify catch** — the session-lived draft opened a DUPLICATE-ORDER path via sheet-swipe-over-success → fixed clear-first at the success outcome, test-pinned; also carried the Slice-A routed findings: `SnackbarController.bottomInset` shell lift + the 74pt FAB/ADR transcription fix → ratification T-0379; slide FEEL = owner device pass) | — | ios | no | — | booking-ux |
> | **T-0372** | **E** | **Brand/asset parity** — 6 mascot imagesets + the Core `Mascot` enum + `AnimatedMascotView` (ImageIO webp); mascots in auth/splash/success/hero/empty-states/membership (+ `BusyMascotOverlay`); **app icons BOTH apps** (`ASSETCATALOG_COMPILER_APPICON_NAME` was `""`); **branded launch screens BOTH apps**; category icon meaning + tints (no-broom Gate-DP note) | M | **done ✅** `62d9495b`+fold `0be26d5d` (reviewer CHANGES folded: animator restart Coordinator, the 33.6k-line xcstrings churn REVERTED + `SWIFT_EMIT_LOC_STRINGS: NO` pinned, BusyMascotOverlay attached — booking half via carrier T-0371. Recorded deviations: AC5 launch COLOR-ONLY → re-probe **T-0377**; AC6 no-broom → `bubbles.and.sparkles`. Partner splash follow-up **T-0378**) | — | ios | no | — | brand-assets |
> | **T-0373** | **F** | **Home upsell carousel** — the Android `HorizontalPager` of gradient mascot upsell slides was never ported (`HomeTab` is a flat stack — the largest visual gap on the most-seen screen); `TabView(.page)` + `BrandGradients` in Core | M | **done ✅** `e69a0283`+fold `bfb1ca7a` (shell wiring landed IN-slice — pre-seeded recurring path; reviewer CHANGES: the **F-1 floor catch** — `BrandGradientTests` red ONLY on the 16.4 runtime, the suggested `performAsCurrent` fix empirically WRONG on iOS 16 → hex stops as single source of truth, OS-independent; CTAs made REQUIRED. Android's hardcoded ", Michael" greeting → **T-0375**, not ported) | T-0368✓, T-0372✓ | ios | no | — | brand-assets (delta #1) |
> | **T-0374** | proc | **PROCESS: iOS 16 floor verification leg** — every iOS slice must smoke on the **iOS 16.4 simulator** (runtime now installed locally; devices under `-- iOS 16.4 --`), since ALL crash/⚠️/island issues were invisible on the latest-runtime sim; + the **Gate-DP §G hardening the architect is folding in** (the AR-DP-1 asset-counterpart sub-check — SF-symbol substitution only for Material ICONS, never brand raster art — + the one-time per-app app-chrome item: AppIcon + launch + splash) | S | **in_progress** (AC2 ✓ §G hardening landed `987f85f0` [AR-DP-1a + AR-DP-4]; AC3 ✓ every slice smoked on 16.4 — the F-1 catch is the leg's proof-of-value; **OPEN: AC1/AC4** — the durable `quality-gates.md` codification + WHY paragraph; qa+docs, high) | — | architect, qa, docs | no | — | cross-cluster NOTES |
>
> **PHASE CLOSE (2026-07-03) — ALL 6 SLICES DONE; PHASE GATE PASS on the final tree:** swiftformat 0.60.1
> **0/528** + swiftlint --strict clean TREE-WIDE · **CleansiaCore 272/272 on BOTH iPhone 17 (iOS 26.3) AND
> the iOS 16.4 floor sim** · Partner + Customer suites green (**Customer 406**) · **dotnet 1714/1714**
> (incl. the new `MobileSpecEnumGuardTests` spec-enum guard) · Android compile green (Kotlin client regen
> from the new spec) · the iOS 16.4 **boot-install-launch smoke of BOTH apps with 0
> NavigationAuthority/comparisonTypeMismatch hits**. Reviewer-per-developer held on every slice: B **APPROVE
> clean** · C **CHANGES folded** (`ebf2fcfd`) · A **workflow-verified, findings routed** (→ `fef5745c`) ·
> E **CHANGES folded** (`0be26d5d`) · D+F **CHANGES folded** (`bfb1ca7a` — the adversarial-verify catches:
> **D-1** the duplicate-order path, **F-1** the floor-runtime-red gradient tests, i.e. the T-0374 leg paying
> for itself). **ADR-0022 ACCEPTED** (`987f85f0`). **REMAINING ACCEPTANCE: the OWNER DEVICE PASS** on the
> iOS 16 iPhone (signed-in navigation, slide feel, visuals, subscribed-user membership, perceived lag — the
> full checklist is in the PR body). **OWNER note on the T-0370 manual steps:** executed IN-BRANCH
> (disposable-postgres spec re-dump; Kotlin regen, Android compile green; the iOS generated client is
> CI-regenerated) — verify the committed spec diff at the PR or re-run your pipeline;
> `MobileSpecEnumGuardTests` pins the representation either way. **Phase-level hardenings (no ticket):**
> `197352a9` ios-ci now also gates master PUSHES (the direct-push bypass that let `6bf55f14` land unlinted);
> `SWIFT_EMIT_LOC_STRINGS: NO` pinned in both project.ymls (kills the xcstrings catalog-sync churn at the
> source, SHA-proven). **Follow-ups filed 2026-07-03 (`proposed`, NOT dispatched):**
>
> | ID | Title | Size | Status | depends_on | Layers | sec | manual_step | Source |
> |----|-------|------|--------|-----------|--------|-----|-------------|--------|
> | **T-0375** | **Android BUG** — customer `home_hero_greeting` bakes the hardcoded name ", Michael" into EVERY user's greeting (`strings.xml:116`, ×5 locales; consumed `HomeTab.kt:489`; iOS deliberately shipped name-less). Fix name-less or a real placeholder | S | **proposed** (medium) | — | android | no | — | T-0373 Slice-F Gate-DP divergence (1) |
> | **T-0376** | **iOS partner pill-bar/pager parity** — the **ADR-0022 D4-mandated** follow-up (dedup-checked: not previously filed): Android partner HAS the pill (`FloatingIslandBottomBar.kt`) + `HorizontalPager`; iOS partner still ships the stock `TabView` (the ADR-recorded interim). Adopt D2/D3 with the partner pill variant (4 even slots, no FAB, no center gap) + harvest the shared pill to Core (the ≥2-call-sites rule) | M | **proposed** (medium) | — | ios | no | — | ADR-0022 D4 |
> | **T-0377** | **iOS launch-screen re-probe** — `UILaunchScreen` `UIImageName` on REAL hardware (known-broken on the 16.4 SIMULATOR: scaled-to-fill or silently blank; COLOR-ONLY shipped by T-0372; the in-app splash is branded, so the gap is one launch frame) | S | **proposed** (low) | — | ios | no | **owner: real-device probe** | T-0372 AC5 deviation |
> | **T-0378** | **iOS partner in-app splash branding** — partner `SplashGateView` is a bare `ProgressView`; the customer got the full branded splash (T-0372 AC5). Styling-only port; the fail-closed gate logic byte-untouched | S | **proposed** (low) | — | ios | no | — | T-0372 review-fold note |
> | **T-0379** | **Architect ratification** — (a) `fef5745c` ANNOTATED the accepted ADR-0022 body in place (the 74pt transcription correction) — ADRs are supersede-never-edit; ratify as a signed erratum or reverse into a proper supersede; (b) the T-0371 harvest (rode `e69a0283`) REPLACED the `patterns-mobile.md` SnackbarInsetState canonical-mapping row (view-local `bottomInset:` param → the published `SnackbarController.bottomInset` + pin-vs-follow) — a "one way" REDEFINITION to ratify. Dispatch BEFORE T-0376 (it cites both artifacts) | S | **proposed** (medium) | — | architect, docs | no | — | D+F review flags |
> | **T-0392** | **Mobile profile stats placeholder (iOS+Android)** — the customer Profile hero stats card ships hardcoded `3 bookings / 320 Kč saved / "Feb 2025" member-since / "Regular" tier` to EVERY user on BOTH platforms (`ProfileTab.kt:97-99`, iOS `ProfileStats.androidParity`); `"Feb 2025"` is also an untranslated English literal. No per-user source exists on the mobile contract (no `createdOn`, no savings, no orders VM on the tab). Wire bookings from the orders count, hide saved/member-since until a backend field lands (locale-format the date), reconcile `tier` to real membership — cross-platform to avoid drift | M | **proposed** (medium) | — | backend, ios, android | no | — | phase/ios-fix2 fix-round-4 S3 review (major) |
> | **T-0393** | **Mobile home notifications inbox (iOS+Android)** — owner remark #7: the Home bell is a dead tap. Confirmed faithful parity — Android `onNotificationClick = {}` (`HomeTab.kt:228`) is a no-op too and no notifications-feed exists on either platform (only prefs toggles). Build a cross-platform inbox (or an interim "no notifications" empty-state sheet), decision-first; needs a mobile notifications-list endpoint if a real feed. **iOS interim empty-state sheet SHIPPED in fix-round 5** (owner re-flagged); Android still dead → mirror, then real feed | L | **proposed** (medium) — iOS interim done | — | analyst, backend, ios, android | no | — | phase/ios-fix2 fix-round-4 owner remark #7 |
> | **T-0394** | **Backend: order-DETAIL snapshot carries no translations** — order-list localizes client-side (its DTOs carry `translations`), but `ServiceDetails`/`PackageDetails` on the order-detail projection do not, so order-detail service/package names render frozen-English in cs/sk/uk/ru. Add `translations` (or `serviceId` to re-localize, or server-side localize) to the order snapshot + wire both clients | M | **resolved-as-shipped ✅** (verified 2026-07-17: the backend half landed in `Phase/ios fix2 #107` — `ServiceDetails`/`PackageDetails` gained `Translations`, `MapToDetails` populates from the Service/Package JSON `Translations` column which loads with the entity (no Include needed — checked all 4 detail read paths: `GetByIdAsync`, `GetByIdIgnoringTenantAsync`, `LookupOrder`, `LookupOrderBatch`); the committed customer-mobile spec (`248e3c47`) already declares the fields; iOS consumes them on the detail cards via `OrdersFormat.localizedCatalogName/Description` (fix-round-5) and Android via T-0395's detail-card wiring. Nothing left to build) | — | backend, db, ios, android | no | — | phase/ios-fix2 fix-round-5 C-review (CASE b) |
> | **T-0395** | **Android: catalog-name localization parity** — iOS fix-round-5 localized Home "Popular packages" (`HomeTab.kt:915` raw `pkg.name`) + order-list summary (`OrdersTab.kt:521` raw `it.name`); Android has the `localizedName(translations,name)` helper (booking) but doesn't apply it on these two surfaces → English in non-English Android builds. Two call-site swaps | S | **done ✅** on `feature/hardening-cluster-2` (unblocked by the owner's customer-mobile spec regen `248e3c47` — `OrderListItem.selectedServices/Packages` now ref the translation-carrying `ServiceListItem`/`PackageListItem` schemas. Added `translations` to the 4 hand-written order wire DTOs (`OrderService/PackageSummaryDto` + `OrderService/PackageDetailsDto`); localized ALL THREE surfaces iOS localizes: order-list `servicesSummary`, Home `recentBookingTitle` (also swapped its hardcoded English "+ N more" for the existing 5-locale `orders_services_more`), and the order-DETAIL Services/Packages cards incl. descriptions via `localizedDescription` (iOS `localizedCatalogName/Description` parity). Wire-shape proven — same backend schemas the catalog surfaces already consume; orders cache is in-memory only, no serialization compat risk. Local gradle compile + customer-app unit tests green) | — | android | no | — | phase/ios-fix2 fix-round-5 A+C reviews |
> | **T-0396** | **iOS token: `errorContainer` dark = blue** — `CleansiaColors.errorContainer` resolves to `Palette.sky800` (blue) in dark mode, so any destructive "container" surface is blue-on-dark. Fix-round-5 delete/dialog routed around it with `.error.opacity`; the token itself is a latent trap. Make the dark value an error-family red (architect owns palette) | S | **proposed** (low) | — | ios | no | — | phase/ios-fix2 fix-round-5 D-review |
> | **T-0397** | **Architect: ratify fix-round-6 patterns harvest** — two new `patterns-mobile.md` "one way" rows (full-bleed header-to-top via GeometryReader + `.ignoresSafeArea(.container,.top)` + threaded top-inset; self-sizing short-entry-sheet detent) need Architect sign-off per the reviewer charter. Header idiom verified on-sim in the fix-round-6 fold; keep the mascot factual correction | S | **proposed** (low) | — | architect, ios, docs | no | — | phase/ios-fix2 fix-round-6 B/C/D reviews |
> | **T-0398** | **iOS customer device registration not wired (+ APNs-gated)** — the customer app constructs NONE of the Core Push stack (partner has registrar+observer+client+startPush+delegate; customer has zero), so the install never POSTs Device/Register and the owner's Devices page is genuinely empty (page code verified correct; regression test pins empty≠error). Even wired, registration is APNs-token-gated = paid Apple account (T-0342/T-0344). Wire the stack like partner + decide tokenless registration so Devices works pre-APNs | M | **proposed** (medium) | T-0342 | ios | no | owner: paid Apple Program + APNs key | phase/ios-fix2 fix-round-8 slice E |
> | **T-0405** | **Security: revoked device keeps API access up to 24 h** — revocation IS wired (RevokeDevice deactivates + `RevokeByDeviceAsync` kills the device's refresh tokens, tested), but `AccessTokenExpMinutes=1440` on every host and NO per-request device/session check → the outstanding JWT works until natural expiry. Architect picks: shorten TTL to 15–60 min (recommended, config-only) / per-request check / push-driven force-logout (needs T-0404 infra). Owner-observed: revoked iPhone from sim, iPhone stayed in | S | **done ✅** `c198a275` (**ADR-0024 accepted**: 30 min on the two mobile hosts ONLY, config-only; web sessions carry no DeviceId → separate follow-up T-0409; pins: raw-file config test red-proven at 1440 + real-host 3s-TTL expiry 401 + revoked-A/sibling-B Postgres integration; reviewer APPROVE on own runs) | — | architect, backend | yes | — | owner report + revoke-session audit (wf_88de1ca0) |
> | **T-0406** | **Android partner: no forced-signout collector** — AuthAuthenticator emits ForcedSignOut on refresh failure and customer's NavHost routes to SignIn, but the partner app never collects `SessionManager.events` → token-dead partner UI stays on screen until cold start. Mirror the customer collector (iOS is correct in both apps) | S | **done ✅** `26d2d6df` (SessionViewModel + nav-root collector; review hardening made ALL sign-out navigations graph-clearing, closing a mid-logout [Login,Login] back-stack race; 54/54; reviewer APPROVE after 1 fix round) | — | android | no | — | revoke-session audit byproduct |
> | **T-0404** | **iOS order-status pushes never surface** — after registration works, iOS still shows nothing: the backend sends **data-only** FCM (`FcmPushDispatcher.cs:64` — no `Notification`/`Apns`/`content-available`) and lets clients render locally (Android does; **iOS has NO receive-side** — no data-message handler, NSE, or `UIBackgroundModes`), and data-only FCM doesn't wake/display on iOS. Architect picks per-platform APNs alert (recommended) vs iOS NSE + ADR on the no-PII trade-off; then backend apns block + iOS tap→deep-link. Found by the push-chain adversarial audit | M | **code done ✅** `e956529e`+`d937de0f` (**ADR-0025 accepted**: loc-key APNs alert via pure FcmMessageFactory, 12-event map, {orderNumber,count} allowlist, Android byte-pinned, 36 red-proven tests; both iOS catalogs +24 keys ×5 locales + customer tap trio incl. membership/loyalty Android-parity routes; reviewers APPROVE ×2). **REMAINING: Mac lane** (xcodegen + both test schemes + device matrix — a Firebase-console test push is a FALSE positive; real events only) + **D5 release-train gate**: the Functions deploy activating the map must NOT precede the first catalog-carrying public build of both apps; delivery still needs T-0403 (FCM tokens) + the Firebase credential (push runbook §0) | T-0403 | architect, backend, ios | yes | — | phase/ios-fix3 push-chain audit (wf_a1afcd54) |
> | **T-0403** | **iOS Firebase Messaging / FCM-token integration** — the backend dispatches via FCM (needs FCM registration tokens) but the iOS apps have NO Firebase SDK and register a RAW APNs token FCM can't target, so iOS push cannot deliver even with the APNs key uploaded. Add FirebaseCore+Messaging (both apps) + GoogleService-Info.plist, register the FCM token instead of raw APNs, and fold the T-0398 customer push wiring. Depends on T-0342 (owner APNs key). **HIGH** | M | **done ✅** (`bb30cd1f` integration + `22bb7beb` the didFinishLaunching call-site fix that actually made it register — deferred-register was silently dropped by iOS; owner-confirmed on a real device. Delivery still needs the Functions Firebase credential + T-0404 display) | T-0342 | ios | no | owner plists dropped ✅ | phase/ios-fix3 push-enablement investigation |
> | **T-0402** | **Partner order-detail DTO carries no translations** — the T-0394 analogue for the PARTNER app: `ServiceDetails`/`PackageDetails` lack `translations`, so partner order-detail service/package names render frozen-English in cs/sk/uk/ru. Add `translations` + mapper + spec re-dump + partner client regen + wire `ScopeCard`; historical orders degrade to snapshot English | M | **backend half done ✅** `bcd375d5` — PREMISE SUPERSEDED: the shared DTO has carried translations since T-0394; the partner WIRE already emits them (reviewer traced both partner hosts to the shared mapper, no bypass). What's stale: `partner-mobile-api.json` + all three partner clients. Employee-caller + degradation pins added (2/2). **REMAINING:** partner-mobile spec re-dump → Android/iOS partner client regen + `npm run generate-partner-client` → wire `ScopeCard`/mobile cards | — | backend, ios, android | no | partner spec re-dump + client regen | phase/ios-fix3 partner localization round |
> | **T-0401** | **Backend: overlap check ignores status → false time_conflicts** — `HasOverlappingOrderAsync` (OrderRepository.cs:216) counts ALL assigned orders in the window regardless of status, so stale/blocked/past orders reject takes with `order.time_conflict` (hit empirically by the round-9 gate on dev). Count only real future/active work (status-set decision + tests) | S | **done ✅** `a415a3d2` (rule: non-terminal assigned orders block, Completed/Cancelled free the slot; persisted CurrentStatus with FAIL-CLOSED null-fallback — excluding NULL would double-book; 11 red-proven tests; catalog carve-out architect-ratified; NOTE: real enum has OnTheWay=3, root CLAUDE.md lifecycle corrected) | — | backend, db | no | — | phase/ios-fix3 round-9 gate finding |
> | **T-0400** | **Deployed web cookie auth needs same-site custom domains** — SameSite=Strict cookies cannot cross the Azure default hostnames (all PSL-separated), so deployed-dev web auth is impossible by design; owner chose the shipped local devremote proxy for dev (no SWA Standard spend). This ticket = the enabler for ANY deployed web URL: cleansia.cz subdomains per env (bicep customDomains + certs + DNS) then same-site cookies just work; prod appsettings already assume this shape | M | **enabler done ✅** `6cc6b39e` (default-off `customDomains` param: binding→managed-cert→SNI in one deploy, SWA domains, auto CORS/base-URL alignment; naming table + owner runbook AZURE-DEV-RUNBOOK §12; reviewer APPROVE). **GATES:** owner DNS (CNAME+asuid TXT per host, apex A for prod) + Google OAuth origins; a POPULATED-map `what-if` must pass before uncommenting any param block. **Residuals:** AC1 architect — admin prod auth targets api.cleansia.cz whose committed CorsOrigins lacks admin.cleansia.cz; AC3 frontend — deployed-dev web build config targeting the *.dev.cleansia.cz API origins | — | architect, backend, frontend | yes | owner: DNS + Google OAuth origins | phase/web-fix1 root cause + owner decision |
> | **T-0399** | **Android partner: null birthDate vs required DateOnly** — `PersonalSectionViewModel.kt:115` sends `birthDate.takeIf { isNotBlank() }` (null when blank) against the backend's REQUIRED `DateOnly` (`UpdatePersonalInfo.cs:38-42`) → the same opaque `validation.invalid_date` the owner hit on iOS, for any partner without a stored birth date. Android has the field UI but no required check; add the inline required error before save (mirror the iOS fix + firstName/lastName checks) | S | **done ✅** `caef1854` (inline required error before save mirroring the shipped iOS fix; null no longer reaches the wire; key in all 5 locales; 69/69) | — | android | no | — | phase/ios-fix2 iOS partner birth-date fix |
> | **T-0407** | **Security: password change revokes NO sessions** — `ChangePassword.cs` rotates the hash but leaves every refresh token + outstanding JWT alive; device revoke is currently the account holder's only kill switch. Revoke all (or all-other) refresh tokens on password change + tests | S | **done ✅** `7a0daabf` (change→revoke all-OTHER sparing the caller via cookie; reset→revoke ALL; atomic with the hash; 1855 unit +2 Postgres proofs; reviewer APPROVE). **owner manual_step: nswag-regen (admin client — ChangeOwnPassword.Command gained CurrentRefreshToken, additive)** | — | backend | yes | — | ADR-0024 panel challenge (D4.6) |
> | **T-0408** | **Mobile clients conflate transport failure/429 with refresh rejection** — Android `AuthAuthenticator.kt:75-87` catch(Throwable)→sign-out; iOS `Auth.swift:221-239` nil on network.unreachable→forced sign-out. With 30-min TTL (ADR-0024) refresh events ×48/day: a synchronized wake of ~10 co-located devices can trip the anonymous 10/min/IP auth bucket and force-sign-out the overflow. Classify retryable (timeout/5xx/429) vs terminal (401/invalid_grant); URGENCY TRIGGER: forced-signout complaints or the D8 counter spiking post-TTL-rollout | M | **done ✅** `3e2d4447` (one cross-platform rule: terminal=401/403/parseable-reject, retryable=transport/5xx/429/unknown fail-open; conflation now uncompilable; android core 54 + partner 69 + customer 221; iOS Mac-deferred; reviewers APPROVE ×2 → hardening folded to T-0415) | — | android, ios | yes | — | ADR-0024 panel challenge C3 |
> | **T-0409** | **Web/admin access-token TTL follow-up** — web hosts stay at 1440 min (structurally unrevocable by device — no web client sends X-Device-Id). ADR-0024 ruling: the ADMIN host is the priority, separable case (SPA, short TTL viable today); customer-SSR needs its rotating-cookie refresh path verified first; interacts with T-0400 custom domains | M | **proposed** (medium) | — | architect, backend | yes | — | ADR-0024 amendment A3 |
> | **T-0410** | **Token-minting time hygiene** — `TokenService.cs:74` and `RefreshToken.cs:125` mint from raw `DateTime.UtcNow` (duplicated); migrate to `TimeProvider` so expiry boundaries become testable without real waits | S | **proposed** (low) | — | backend | no | — | ADR-0024 panel C5 |
> | **T-0411** | **Android partner: UserProfileStore survives forced sign-out** — the `SessionScopedCache` multibinding wipes only PushTokenRepository + NotificationFeedCache (`NotificationsModule.kt:74-79`); authenticator-driven sign-out leaves the prior user's profile (incl. `employeeId`, reused at `AuthRepository.kt:258`) on device — cross-user local-state bleed on shared devices. Add UserProfileStore to the multibinding | S | **done ✅** `caef1854` (UserProfileStore + customer UserRepository join the session-wipe set; redundant explicit clear removed; 69/69 + 221/221; reviewer APPROVE — the class is NOT fully closed, stragglers → T-0416) | — | android | yes | — | T-0406 review carry-over |
> | **T-0412** | **promo.new_sitewide display on iOS** — deliberately OUT of the ADR-0025 loc-key map (no fixed template exists; Android renders server-authored title/body from the wire). Needs a literal ApsAlert pass-through + product decisions (apns-priority, interruption-level, Promo opt-in default). iOS customers get no promo pushes until then | S | **proposed** (medium, product) | T-0403 | architect, backend, ios | no | — | ADR-0025 panel CH-1 |
> | **T-0413** | **ResolveDispute refund push lacks orderNumber** — the alert body renders with an empty arg (tolerated by design). `DisputeRepository.GetForUpdateAsync` has no `.Include(d => d.Order)`, so the producer can't read `DisplayOrderNumber` without a repo/query change | S | **done ✅** `c654a392` (read-only Order Include, caller-audited; DisplayOrderNumber resolved after the refund succeeds, degrades to empty on missing nav; 1847→; reviewer APPROVE, red reproduced at clean HEAD) | — | backend, db | no | — | T-0404 backend lane skip note |
> | **T-0414** | **Immediate device revocation** — a deleted device kept API access until token expiry (≤30 min, ADR-0024). Owner: revoke IMMEDIATELY | M | **done ✅** `4eb04b51` (**ADR-0026 accepted** — challenger caught + fixed the re-registration blocker: poll keys on DeactivatedOn alone. device_id claim [login header / refresh persisted-record]; per-host polled RevokedDeviceDirectory ≤30s; OnTokenValidated 401→existing forced-signout chain; un-killable refresher, fail-open+TTL-bounded, config-gated; no migration; web byte-untouched; 1873 unit + 7/7 host enforcement; reviewer APPROVE, iat/A1 both verified; owner chose ≤30s over per-request) | — | architect, backend | yes | — | owner directive 2026-07-15 |
> | **T-0415** | **iOS refresh classification hardening** (T-0408 review folds) — (a) iOS business-key match reads `ApiError.code` only; add a `?? problem.type`/message-field fallback + a fixture with the real wire shape so a non-401 rejection still classifies terminal (parity with Android's body substring match); (b) align the patterns-mobile.md Android paragraph to also state the locally-expired-refresh-token terminal arm; (c) `Auth.swift:323` can persist `refreshToken:""` → treat empty-stored-refresh as locally-expired-terminal in `performRefresh` | S | **proposed** (low) | — | ios | yes | — | T-0408 iOS review F1/F3/Q1 |
> | **T-0416** | **Session-wipe class not fully closed** (T-0411 review sweep) — per-user `@Singleton` state still outside the SessionScopedCache set: partner `DashboardRepository` (MEDIUM — earnings/orders bleed to next user on a shared device, 60s window no-ops the refresh), customer `NotificationPreferencesRepository` (LOW-MED — replace-all PUT of prior user's payload), partner `OrderChecklistRepository` + the watermark-only `Staleness` holders (LOW), and `deleteAccount()`'s hand-maintained 5-of-9 clear list. Add each to the wipe set; iOS `SessionScopedCacheRegistry` membership audit for the profile stores | M | **done ✅ (Android)** `eb14a63e` (all 5 stragglers session-scoped incl. the DashboardRepository earnings/orders bleed + 60s-window no-op; customer deleteAccount iterates the injected Set; DataStore names preserved; 77/223; reviewer verified the set is EXHAUSTIVE). iOS SessionScopedCacheRegistry parity audit → follow-up | — | android, ios | yes | — | T-0411 review completeness sweep |
> | **T-0417** | **Architect: codify the session-wipe rule** — "per-user `@Singleton` state MUST implement SessionScopedCache" has recurred 4+ times (PushToken, NotificationFeed, UserProfileStore, customer UserRepository + T-0416 stragglers). A `check-consistency.mjs` rule needs an allowlist for legit device-level/public caches (state the exclusion reason). Also file the standing catalog ratify-harvest for the two patterns-mobile.md refresh-classification rows (T-0379/T-0397 precedent) | S | **done ✅** `19ec8d7b` (security law **S11** + wipe-triggers + reason-annotated allowlist E9; warn-only E9 check in check-consistency.mjs — a hard gate needs Kotlin/Swift type resolution the scanner lacks, roster-assertion test specced as follow-up; S2 harvest for ADR-0026/0027; class re-confirmed closed) | — | architect | no | — | T-0411 + T-0408 review Gate-4d |
> | **T-0418** | **Immediate access-token cutoff on password reset (ADR-0026 X1)** — reset kills refresh tokens (T-0407) but the outstanding access token still rides ≤30 min after an account-takeover recovery. Extend: drop the reset'd user's devices into the RevokedDeviceDirectory (user-keyed entries), or a userId-scoped iat cutoff. Needs its own short ADR (pre-analyzed in ADR-0026 CH-13) | M | **done ✅** `f7c89d5e` (**ADR-0027 accepted**: sibling RevokedUserDirectory keyed on userId, reuses T-0407 password_reset rows — zero migration; keys on sub so no claim-transition; RESET only, same-second self-heals; GroupBy+MAX proven on real Postgres; RED-proven by reviewer; web untouched; 1899/75/108) | T-0414 | architect, backend | yes | — | ADR-0026 X1 |
> | **T-0419** | **RefreshToken rotation↔revoke TOCTOU (ADR-0026 X2/D9.7)** — a rotation racing a revoke's read→commit window escapes the chain and then passes the directory (its iat postdates the revoke), invisible in the Devices list. Milliseconds-wide, `auth`-bucket-capped, defeats today's revocation identically. Fix: `xmin` concurrency token on RefreshToken (no migration) and/or set-based conditional revocation | S | **done ✅** `ffc0d19a` (xmin optimistic-concurrency token on RefreshToken, no migration; all 4 revoke paths retry-on-conflict RE-READ so a rotation-inserted child is caught — airtight kill switch, stronger than last-writer-wins; reviewer caught+fixed a blocker where xmin 500-d the revoke paths; red-proven maxAttempts=1). **owner: EF model-snapshot regen (schema-empty)**. Non-blocking notes → T-0421 | — | backend | yes | — | ADR-0026 X2/D9.7 |
> | **T-0420** | **Headerless mobile-login observability (ADR-0026 X3) + consistency debt** — (a) WARN-log mobile-host logins missing `X-Device-Id` to evidence-gate a future required-header login validator (claim-less tokens currently pass the directory check by design); (b) consolidate the two mobile hosts' near-identical `AddJwt`/`OnTokenValidated` bodies (ADR-0026 CH-10 deferral, flagged by 2 reviewers); (c) baseline the pre-existing B10 test-file consistency hits (Dispute/Fiscal seam tests) into `consistency-violations.md` or carve out test files | S | **part a done ✅** `ffc0d19a` (headerless mobile-login WARN, audience-only no PII; no hard requirement yet). Parts b (AddJwt consolidation) + c (B10 test-file ledger baseline) remain architect/ledger | — | architect, backend | yes | — | ADR-0026 X3 + review ledger notes |
> | **T-0421** | **Revocation exhaustion + theft-chain edges (T-0419 review non-blocking)** — (a) after 5 consecutive xmin collisions on the same rows the revoke retry helper propagates the exception → 500 + rollback = technically fail-OPEN for a kill switch; add a final bulk ExecuteUpdate revoke that ignores concurrency for strict fail-closed semantics; (b) CommitStagedChainRevokeWithRetryAsync re-revokes only ex.Entries (not a full re-read like the other paths), so a legit rotation of a DIFFERENT token racing a theft-chain revoke can leave its new child alive — pre-existing (pre-xmin it also survived), bounded by the ≤30s directory/TTL; make it re-read for parity | S | **done ✅** on `feature/hardening-cluster-2` ((a) exhaustion now runs `FailClosedBulkRevokeAsync`: detach stale tracked revoke marks → set-based revoke via new `BulkRevokeIgnoringConcurrencyAsync(RefreshTokenRevocationScope)` re-expressing each path's exact predicate in SQL (hash / user+device with null-guard / user+spared-hash / user-wide chain) → THEN commit sibling staged changes (deliberately non-atomic in the safe direction: "tokens dead + caller retries", never "tokens alive"); (b) chain revoke now reloads conflicted rows then RE-RUNS the full `RevokeChainAsync` walk so a race-inserted child can't escape — red-proven. **Adversarial security review found F1**: a rotation whose flush statement-OVERLAPS the bulk UPDATE (parent locked, child snapshot-invisible) beat the naive two-sided race argument → repaired with a revoke-then-VERIFY loop (fresh snapshot per pass, exit only when a verification read proves zero live rows in scope, cap 8 then throw — residual failure mode is availability, never a silently-live session); F2 hardened (null deviceId now a loud ArgumentException tripwire instead of tracked-matches-nothing/bulk-widens-to-user inversion); F3 documented (bulk stamps expired-but-unrevoked rows; security-neutral); F4 filed as T-0428. Also repaired the 2 `UserRevocationWiringPinTests` broken by T-0420's AddCleansiaMobileJwt consolidation — pin follows the indirection + forbids web hosts calling it. 6 new fail-closed tests; suite 1923/1923) | — | backend | yes | — | T-0419 review notes 4/theft-chain |
> | **T-0422** | **Self-revoke should sign out the CURRENT device instantly** — deleting YOUR OWN device from the Devices page leaves a brief zombie session until ADR-0026's ≤30s directory catches it; the client already knows it revoked its own deviceId, so it can sign out at 0s. Client-side complement to T-0414: on a successful revoke where revoked deviceId == local deviceId, run local sign-out + route to login; badge "This device" + confirm copy. Both platforms, both apps | S | **done ✅** on `feature/hardening-cluster-2` (all 4 surfaces, adversarial review round 2 clean. Archaeology: the iOS VMs ALREADY shipped the D7b sign-out path but all four UIs hid the revoke control on the isCurrent row, making it unreachable — the fix is the AFFORDANCE (sign-out icon + distinct `devices_self_revoke_*` dialog copy, 4 keys ×5 locales ×4 catalogs) + the missing Android plumbing, LOCAL-ONLY per AC1/AC2 after the reviewer caught round 1 calling the full `logout()` (redundant uncapped round trips against an already-dead session — offline a bounded zombie-window rerun): iOS Views → `authClient.signOutLocal()`; Android customer → new `AuthRepository.signOutLocal()` (wipe + ForcedSignOut → nav-root routes to SignIn); Android partner → existing `signOutLocal()` + emit on the shared `SessionManager` riding the never-unmounting root observer (no per-screen wiring, kills the back-press drop race). Sign-out ONLY on the Success branch — a failed self-revoke never wipes locally (pinned by tests, mutation-verified). Ledger candidates from review: partner clients discard `RevokeDeviceResponse.success` (unreachable today); iOS shows the success snackbar before self-sign-out where Android suppresses it. Both Android apps compile + unit tests green, both iOS apps BUILD SUCCEEDED, swiftformat/swiftlint clean; full log in the ticket file) | — | ios, android | no | — | owner self-revoke report; companion logout-hang fix (this branch) |
> | **T-0423** | **iOS: cold-launch order-notification tap is dropped** — tapping an order push from a TERMINATED app lands on Home, not the order. Payload/resolver/nav all verified correct; the break is the AppDelegate→SwiftUI bridge: `onTap` is wired in the SwiftUI `.task` which runs AFTER iOS calls `didReceive` on cold launch → the resolved destination fires into a nil closure, silently dropped (both apps; PartnerAppDelegate has the same race). Fix: buffer the destination in the delegate, flush on `onTap` didSet | S | **proposed** (high) | — | ios | no | — | owner remark + remarks-sweep (wf_064232d3) |
> | **T-0424** | **iOS: Booking-Confirmed shows only the code** — Android shows arrival/address/total + a 4-step progress tracker; iOS shows just the confirmation pill. iOS-only, no backend change: add BookingSuccessViewModel (getById(orderId)), an OrderSummaryCard + progress timeline reusing the existing OrdersFormat/price/LiveProgressLogic helpers, wired in BookingSheetView; degrade to the code on fetch failure; 5-locale copy | M | **proposed** (medium) | — | ios | no | — | owner remark + remarks-sweep |
> | **T-0425** | **iOS SwiftUI jank cluster** — 4 complaints, 3 root causes: (BIG) 6 mascots are 1024² PNGs decoded on the MAIN thread every render (`Mascot.swift:11-13`) → Plus-header drag + "animations slow everything"; Home pop-in (skeleton gate too narrow + late independent catalog/recurring loads + bare `if` sections shove layout); floating-label input drag + cursor delay (`CleansiaTextField.swift:56` animates on `floating` not `focused`). Fix ROI-ordered: downsample/cache mascots → parallel Home prefetch + crossfades → focus-driven float | L | **proposed** (high) | — | ios | no | — | owner perf remarks + remarks-sweep |
> | **T-0426** | **iOS Bolt/Wolt-style launch screen + app icon** — current OS launch screen is color-only (UIImageName scaling bug workaround); the branded splash exists only as an in-app SwiftUI view. Replace with a Launch Screen storyboard (brand full-bleed + centered logo/wordmark) + swap the 1024² app-icon masters + align both SplashGateViews (upgrade the partner spinner). Small dev work, GATED on owner artwork (icon + wordmark + brand hex) | S | **proposed** (medium) | — | ios | no | owner: icon + wordmark + brand-hex artwork | owner remark + remarks-sweep |
> | **T-0427** | **iOS Live Activity for an in-progress clean** — Wolt/Uber-style lock-screen + Dynamic Island live status. Real feature: a Widget Extension + ActivityKit (16.1) client, AND a SEPARATE APNs Live-Activity push channel (distinct from the T-0403/04 FCM alert path — likely direct-APNs, FCM can't target activity tokens) driven by order-status transitions + a per-order activity-token registration. ADR-gated | L | **proposed** (medium) | T-0403, T-0404 | architect, ios, backend | no | — | owner remark + remarks-sweep |
> | **T-0428** | **Logout of a rotated token should chain-revoke (T-0421 review F4, pre-existing)** — `RevokeAsync` silently succeeds when the target is already revoked (deliberate anti-probing + idempotent-logout design), so a thief who rotates stolen token H just before the victim's logout lands keeps child H′ alive while the victim believes they logged out — on both the tracked path AND the bulk `{TokenHash}` scope (which faithfully mirrors it). Consider: when logout hits a token with `RevokedReason == "rotated"`, treat it as the same theft signal the refresh path uses and chain-revoke (`RevokeChainAsync` = all user tokens). Weigh against the anti-probing property (an unauthenticated logout probe learning a token WAS valid via side effects); password change/reset/device-revoke remain the recovery paths meanwhile | S | **proposed** (low) | — | backend, security | yes | — | T-0421 adversarial review F4 |
>
> **Full-platform review tickets (filed 2026-07-06) — `proposed`, from AUDIT-2026-07-06-full-platform-review (20-agent review, crit/high adversarially verified, 0 refuted); NOT dispatched:**
>
> | ID | Title | Size | Status | depends_on | Layers | sec | manual_step | Source |
> |----|-------|------|--------|-----------|--------|-----|-------------|--------|
> | **T-0380** | **Web MONEY-UX (CONFIRMED high)** — order wizard double-counts the express surcharge: the server quote already folds +20% into TotalPrice, `order-pricing.facade.ts:114` re-adds it client-side → customers see ~+44% at checkout (the charge itself is correct — server-authoritative). Render the server's `ExpressSurchargeAmount`; delete the client gross-up; pin with a facade test | S | **done ✅** `ac7aa79d` (server-verbatim money lines; EXPRESS_SURCHARGE_RATE deleted; red-proven pin; 116/116 lib tests; reviewer APPROVE) | — | frontend | no | — | AUDIT-2026-07-06 #1 |
> | **T-0381** | **Web enum-as-int contract pair (CONFIRMED high ×2)** — admin fiscal-failures page CRASHES (`FiscalErrorKind` string enum vs int wire, `fiscal-failures-list.component.ts:118` `.toLowerCase()` throws → admins blind to fiscal registration failures) and membership-plans list throws/edit form blanks (`BillingInterval`, `membership-plan-list.models.ts:44`). Apply `[SwaggerEnumAsInt]` + regen admin client; ALSO extend the T-0370 `MobileSpecEnumGuardTests` to the three WEB specs (this exact class is live because the guard pins mobile only) + refresh the stale customer `MembershipStatus` | S | **done ✅** `4fb7aa80` (enum guard now sweeps ALL 5 hosts by reflection, red-proven vs the two offenders; admin libs int-wire-correct pre+post regen; 141/141 + 16/16 + 7/7) — **regen landed + verified ✅** `90480666` (2026-07-07: BillingInterval {1,2}, FiscalErrorKind {0..4}, MembershipStatus {1..4} all numeric; fiscal-failures 7/7 + membership-plan-management 16/16 re-run green against the regenerated client) | — | backend, frontend | no | ~~owner: admin+customer NSwag regen~~ **done** | AUDIT-2026-07-06 #2-3 |
> | **T-0382** | **Backend AUTH (medium)** — `RegisterEmployee.cs:82-90` leaves an existing unconfirmed Customer's `Profile=Customer`; partner login then refuses the account FOREVER (`PartnerLogin.cs:57` profile gate). `User.UpgradeToEmployee()` exists with zero callers — call it on the existing-user branch + pin with a test | S | **done ✅** `77958e9c` (UpgradeToEmployee wired on the reuse branch, red-proven; admin no-op pinned; CONFIRMED-customer widening = open product question; 1738/1738) | — | backend | **yes** | — | AUDIT-2026-07-06 #4 |
> | **T-0383** | **S4/S6 leak cluster (security, medium)** — (a) `SendEmailHandler.cs:41,50` logs the RAW queue payload at Warning: email + LIVE confirmation/reset code into App Insights (account-takeover material for log readers) — log EmailType + missing-field names only; (b) `RequestLoggingMiddleware.cs:78,179,195` logs guest order-lookup emails + every caller's JWT email at Information — add /order/lookup to sensitive paths, drop `{UserEmail}`; (c) `OrderMappers.cs:159-165` sends customers the cleaner's FULL name + personal phone via `AssignedEmployeeDto` — mirror the first-name-only rule GetOrderPhotos already enforces; (d) `CreatePaymentIntent.cs` StripeCustomerId at Information | M | **done ✅** `206b448e` (all four leak points closed across ALL 5 hosts’ middleware; masking tests 4/4; DTO shape unchanged — no regen; 1738/1738) | — | backend | **yes** | — | AUDIT-2026-07-06 #5-6,9 |
> | **T-0384** | **ISO/service-area rollout completion (CONFIRMED/PARTIAL ×3 + fail-closed class)** — finish what the Android customer fix started, ONE canonical normalizer per platform Core: (a) Android partner `AddressSectionViewModel.kt:201` still on the alpha-3 prefix hack (saved addresses render CountryNotServiced; SK/UA/PL saves blocked); (b) iOS customer `CountryResolver.swift:17` compares alpha-2 to alpha-3 — never matches; (c) iOS partner `AddressSectionViewModel.swift:122` same prefix heuristic; (d) iOS partner AddressSection FAILS CLOSED on fetch failure (the UNKNOWN≠empty class Android fixed in bc56d4d7) + hard `[cz,sk]` searchBias filter on geocode results; (e) pinning tests on ALL platforms (both Android service-area fixes shipped testless) | M | **done ✅** `013d62ff` (Android 83/83 incl. red-proven pins + blank-ISO latent fix; iOS IsoCountryCodes 249/249 vs JDK table + 41/41 seed pins, tri-state + bias-not-filter — Swift DEFERRED-TO-MAC; residuals: per-app bias-provider wiring; iOS city-level indicator needs a partner serviced-cities endpoint) | — | android, ios | no | — | AUDIT-2026-07-06 #7-9 |
> | **T-0385** | **iOS partner L10n printf garbage (CONFIRMED high)** — `invoice_card_paid_on`/`generated_on` carry Android-style `%1$s` in ALL 5 locales (`L10n+Earnings.swift:134` renders literal `%1$s`); the T-0373 sweep fixed only the customer catalog and shipped no guard. Transpose to `%1$@` + add a `$s`-scan test over both xcstrings catalogs | S | **done ✅** `6abe725a` (%1$@ ×5 locales; catalog-scan guard test added; 0 printf-$s residue repo-wide; Swift test run DEFERRED-TO-MAC) | — | ios | no | — | AUDIT-2026-07-06 #11 |
> | **T-0386** | **Android device hygiene (security, medium)** — (a) partner release APK allows cleartext HTTP to ANY host (`usesCleartextTraffic="true"` in src/main manifest; customer app does it right) → flip false + debug-only overlay; (b) partner notification feed (Room) SURVIVES sign-out — next account on the device sees the prior account's order/dispute history + unread badge (`NotificationDao.kt:9` no delete; store not session-scoped); (c) partner `allowBackup=true` backs up DataStore PII; customer backup exclusion rules name nonexistent files; (d) Bearer in debug logcat / full URLs at Log.w | M | **done ✅** `74885bb7` (cleartext off aapt2-verified + debug-only netsec config; Room feed wiped on sign-out via the session seam; backup rules point at REAL files both apps; Authorization redacted; SAS-URL log debug-gated — customer AuthModule sibling → T-0389) | — | android | **yes** | — | AUDIT-2026-07-06 #7-8 |
> | **T-0387** | **Backend perf: persist Order.CurrentStatus (CONFIRMED high, L)** — no persisted status forces per-row latest-OrderStatusHistory correlated subqueries: partner dashboard available-orders count scans the WHOLE Orders table per load (`OrderSpecification.cs:111`, no date bound), the 288×/day fiscal sweep seq-scans (`OrderRepository.cs:256` OR-predicate defeats the index), dashboards issue ~25 sequential round trips. Denormalize `CurrentStatus` (T-0341's Sequence work is the natural base) + index (Status, CleaningDateTime) + batch the dashboard stats; fold the smaller wins (StripePaymentIntentId index, CustomerPhone scan, GetAverageRating in-memory avg, order-list projections) | L | **done ✅ (code) — OWNER MIGRATION PENDING** `de6dd281` (persisted CurrentStatus denormalized at the single Order.AddOrderStatus seam, history authoritative + NULL fallback; hot reads migrated: OrderSpecification counts/lists + fiscal sweep restructured to index-served Cash/Paid UNION; 4 indexes; dashboard ~25→~13 round trips, DTO byte-unchanged; seed backfills in-tx; 35 equivalence/regression pins, suite 1759/1759; reviewer APPROVE). **OWNER MANUAL_STEP:** (1) `dotnet ef migrations add AddOrderCurrentStatusAndPerfIndexes --project src/Cleansia.Infra.Database --startup-project src/Cleansia.Web`; (2) in Up() after the column+index ops add `migrationBuilder.Sql(@"UPDATE ""Orders"" o SET ""CurrentStatus"" = h.""Status"" FROM (SELECT DISTINCT ON (""OrderId"") ""OrderId"", ""Status"" FROM ""OrderStatusHistory"" ORDER BY ""OrderId"", ""CreatedOn"" DESC, ""Sequence"" DESC) h WHERE h.""OrderId"" = o.""Id"";")`; (3) re-run that same idempotent UPDATE via psql ONCE after deploy (catches rows written in the migrate→deploy window); (4) apply the migration BEFORE running insert_seed_data.sql (its new backfill references CurrentStatus); (5) then run IntegrationTests (Docker) — blocked by PendingModelChangesWarning until the migration exists, by design. **Follow-ups (mustTell → T-0390):** cold-path latest-history subqueries (NewJobsDigestService, StartOrder validator, GetMyServingCleaners, GdprExportService), the deferred order-list full-graph projection refactor (GetCustomerOrders/GetPagedOrders), and the diverged Infra.Scripts startup-seed copy re-sync. | db, backend | no | **owner: EF migration (5 steps in-row)** | AUDIT-2026-07-06 #12 |
> | **T-0388** | **CI delta hygiene (infra, medium)** — backend-ci push-to-master runs the full Testcontainers suite on EVERY push with no paths filter or concurrency group and gates nothing (advisory beside the auto-deploy); ios-ci paths filters omit the shared OpenAPI spec dir its codegen consumes; openapi-generator installed unpinned via brew while now wire-load-bearing; android-ci missed the push-gate fix. One pass over the four workflow files | S | **done ✅** `327b79aa` (backend-ci paths+concurrency; ios-ci spec-dir paths + openapi-generator pinned 7.10.0 sha256-verified matching the Android toml; android-ci aligned; deploy gating stays an owner decision) | — | backend | no | — | AUDIT-2026-07-06 infra-delta |
> | **T-0389** | **Android customer log redaction** — customer-app `core/auth/AuthModule.kt:69-72` carries the same unredacted debug `HttpLoggingInterceptor` the partner app just fixed (Bearer tokens in debug logcat). Add `redactHeader("Authorization")` (+ mirror any release-path URL trimming) | S | **done ✅** `b796799c` (one-line parity port; the single interceptor feeds both auth/no-auth clients; reviewer sibling-sweep found no other URL/header leak site in the customer app; 216/216) | — | android | **yes** | — | T-0386 review fold |
> | **T-0390** | **Order status/query cleanup (T-0387 follow-ups)** — (a) migrate the remaining COLD-path latest-OrderStatusHistory derived-rule subqueries onto CurrentStatus: NewJobsDigestService.cs:112, StartOrder.cs:110 validator, GetMyServingCleaners.cs:28, GdprExportService.cs:46 (rule-equivalent, low-traffic — not blocking); (b) the deferred order-list projection refactor — GetCustomerOrders/GetPagedOrders materialize full entity graphs (7-8 split queries/page) instead of projecting to the list DTO; (c) re-sync the diverged startup-seed copy in Infra.Scripts (CleansiaStartupBase.cs:263, pre-existing broken); (d) IntegrationFailureMetricsTests flake (file hygiene). Dispatch AFTER the T-0387 migration lands | M | **done ✅** `3b97c29e` (all four parts: cold paths on CurrentStatus with null-fallback + Sequence-tiebreak equivalence pins; GetCustomerOrders/GetPagedOrders project to OrderListRow, DTO byte-identical, pay math funneled into the one existing implementation; Infra.Scripts seed re-synced + drift-guard test; meter flake fixed at the root via a serialized IntegrationFailureMeter collection + shared ConcurrentQueue capture; suite 1768/1768 ×3 runs; reviewer APPROVE after 1 fix round) | T-0387 | db, backend | no | — | T-0387 mustTell + review |
> | **T-0391** | **Review-fold hygiene (2026-07-07 batch)** — (a) `:core` `NetworkErrorInterceptor.kt:35,39,45` logs full request URLs without debug-gating (pre-existing, both apps); (b) codify a check-consistency rule: every `HttpLoggingInterceptor` provider must call `redactHeader("Authorization")` (2nd occurrence of the class); (c) baseline or canonicalize the 13 pre-existing B10 direct Dispute state-writes in tests (not in consistency-violations.md); (d) strip ticket-ID doc comments from IntegrationFailureClassifier/Registration/RetryBehavior tests; (e) confirm-email `setEmail()` should validate the query param before hiding the email field (customer + partner twins); (f) partner web resendCode is a bare subscribe — add success/error snackbars once the partner app gains the auth.confirm_email.* keys (mirror customer handlers); (g) Nx lib naming/tagging canonicalization: partner feature libs are untagged and named bare (`confirm-email`) vs the customer `cleansia-customer-*` + scoped tags; (h) Android partner ConfirmEmailViewModel flags success on ANY ApiResult.Success but the repo can return Success(UnverifiedEmail(hasToken=false)) on a 200-without-token — match on LoginOutcome.Authenticated like the customer app (pre-existing, contained) | S | **proposed** | — | android, frontend, reviewer | no | — | T-0363/T-0389/T-0390 review folds |
> (Standing notes: **T-0367 ABSORBED into T-0370** — row updated below; T-0366 stays separate/unchanged;
> the T-0314-recorded "customer brand asset + Google-G fidelity" note is delivered by **T-0372**.
> **T-0314 record ADDENDUM:** *feature-complete ≠ device-verified* — this phase paid the
> device-verification debt; **T-0374** makes the floor leg permanent once its `quality-gates.md`
> codification lands. **No-decision note:** T-0369/T-0370/T-0371/T-0372/T-0373 composed accepted ADRs
> (0011/0014/0018/0019/0020/0021), panel skipped; the ONE decision — the T-0368 shell-pattern change — ran
> the architect ruling and is now **ADR-0022, accepted**, folded into the living docs per AC8.)
> Do NOT commit — the owner commits the phase edits (the PM never commits; backlog edits left unstaged).
> Full phase status-log: **`status/sprint-12.md`** (STATUS-LOG 2026-07-03 — PHASE CLOSE).

> ## 🧱 HARDENING-1 — post-iOS-port cleanup/hardening (sprint-12): **8 follow-ups DONE · PR drafted** (2026-06-30)
>
> **HARDENING-1 IS DONE — `phase/hardening-1` (5 commits, pushed; off master `3e7ce52` = the merged Phase-8 PR
> #100). The PR is drafted; the owner commits the backlog edits + opens it (the PM does not commit).** This is the
> first post-iOS-port phase — it does NOT add features; it lands the deferred follow-ups the iOS port surfaced
> (the `proposed`/`draft`/`ready` tail) across backend / iOS / Android, plus a CI/tooling pin. **8 tickets →
> `done`** in 5 commits:
> - **`239323b` — CI/tooling hardening (NO ticket):** pinned SwiftFormat to **0.60.1** (the GitHub release
>   binary) to stop the recurring local-vs-CI version drift — CI's brew-latest kept adding default rules
>   (`docComments`, `redundantEmptyView`) that failed CI on locally-clean code. Lint clean at 0.60.1. Recorded
>   here as a CI hardening; no ticket needed.
> - **`d834e92` — T-0349** (iOS harvest) — hoist the address-picker VM into `CleansiaCore` (public, framework-pure,
>   `searchBias=["cz","sk"]` default; both apps repoint + delete copies; Views stay app-local). Core 218 /
>   Partner 366 / Customer 362 green; reviewer **APPROVE**; **no new ADR** (home change via the ADR-0013 escape
>   clause); harvest in `patterns-mobile.md` + `ios-app-architecture.md`.
> - **`64f6525` — the backend trio: T-0346 + T-0348 + T-0350.** **T-0346** gate `GoogleAuth` on `email_verified`
>   (parity with `AppleAuth`, fail-closed). **T-0348** PaymentIntent refund path for mobile card orders + extend
>   the refundable-surface gate to the two CANCEL paths so a cancelled paid mobile/recurring card order refunds
>   (a money-correctness fix; `Order.HasRefundableChargeSurface`; **NO schema change** — `StripePaymentIntentId`
>   already existed). **T-0350** rate-limit `NotificationPreferences` GetMine+Update on **both** hosts + a
>   coverage-guard for the lazy-create GET. **Security review CLEAN**; correctness/test findings folded
>   (cancel-refund coverage + stale-test retarget + GetMine guard); the architect "both-surfaces" finding
>   **refuted/dropped** (one charge surface per card order, by T-0347). Build 0 errors; **`Cleansia.Tests` 1685**.
> - **`e4e00b0` — T-0341** (backend) — deterministic order status-history "current status": new
>   `OrderStatusTrack.Sequence` (`int`) + canonical `Order.CurrentStatus`
>   (`OrderByDescending(CreatedOn).ThenByDescending(Sequence)`) routed through **all** in-memory + SQL call sites.
>   **Pre-prod Initial migration REGENERATED** (owner-authorized class; `Sequence` folded, timestamp preserved).
>   De-flake **20/20**; **IntegrationTests 97/97 + HostTests 60/60**.
> - **`1d99333` — the android parity-hygiene commit: T-0351 + T-0333 + T-0337.** **T-0351** wire the dead
>   customer `SecurityScreen` "Update password" stub to the existing reset-code flow. **T-0333** i18n the partner
>   Register/Forgot validation strings. **T-0337** migrate the 7 partner profile VMs to sealed
>   `Loading`/`Error`/`Loaded` + `ActionState` + i18n + a `BankSectionViewModelTest`. **Verified by a LOCAL
>   gradle build** (JDK21/SDK35 — partner+customer compile + the new test pass) since `android-ci` runs only on
>   PR; reviewer **APPROVE**.
>
> **Tickets reconciled to `done` (with verification + sha):** **T-0333, T-0337, T-0341, T-0346, T-0348, T-0349,
> T-0350, T-0351** (these were the Phase-8 / earlier follow-ups). **OWNER / manual note:** the **T-0341 pre-prod
> Initial-migration regeneration is ALREADY DONE in-branch** (owner-authorized regen class; `Sequence` folded
> into `20260623112626_Initial`) — there is **no pending migration step** for this phase. No NSwag regen needed
> (no DTO/endpoint surface changed). **Two NEW non-blocking follow-ups filed `proposed` (the android review
> surfaced them):** **T-0352** (cross-app password min-length policy drift — customer ≥12 vs partner ≥8; pick one
> canonical policy, ideally aligned with the backend `BaseAuthValidator`; low) · **T-0353** (the partner profile
> section-form `Error` state renders an empty form with no retry affordance — the screens consume the sealed
> state via `is Loading` + `as? Loaded` not an exhaustive `when`; behavior-preserved from before T-0337, a small
> UX enhancement; low). **NOT committed by the PM — the owner commits these backlog edits with the phase PR.**
> Full per-item verification in each ticket's status log; the phase status-log is appended to **`status/sprint-12.md`**.
>
> --- (iOS Wave-10 banner below — kept for traceability) ---
>
> ## 🍎 WAVE 10 — iOS PORT (sprint-12): PHASE 0 DONE + MERGED · **PHASE 1 (T-0303) DONE** · **PHASE 2 — T-0304 + T-0305 DONE** · **PHASE 3 — T-0306 + T-0310 DONE (merged #95)** · **PHASE 4 — T-0307 + T-0308 DONE (merged #96)** · **PHASE 5 — T-0309 + T-0311 DONE (merged #97) → the iOS PARTNER APP is FEATURE-COMPLETE** · **PHASE 6 — T-0343 (backend AppleAuth) + T-0312 (customer shell + auth) DONE (merged) → the iOS CUSTOMER AUTH FOUNDATION** · 🍎 **PHASE 7 — T-0313 (customer booking wizard + Stripe PaymentSheet) + T-0347 (backend money-safety) + T-0332 (dual-use Bearer carve-out) DONE (merged) → HARD AREA #1 CLEARED** · 🎉 **PHASE 8 — T-0314 (customer TAIL, the LAST customer feature, L→split into 6 slices A–F) DONE (PR drafted) → the iOS PORT [partner + customer] is FEATURE-COMPLETE** (2026-06-30)
>
> **iOS PHASE 8 IS DONE — `phase/ios-phase8` (7 commits, pushed; off master `8d90104`); the Phase-8 PR is drafted.
> This phase delivers T-0314 — the customer TAIL, the LAST customer feature ticket and the BROADEST in the port
> (`L → split` into 6 slices A–F) — and so COMPLETES the customer app AND the entire iOS port** (partner: Phases
> 0–5; customer: Phases 6–8). 🎉 **The FEATURE port of both Kotlin/Compose apps to Swift/SwiftUI is DONE.** It fills
> the four placeholder tabs T-0312 scaffolded plus every remaining customer surface that is neither the booking
> wizard (T-0313) nor auth (T-0312). The verified workflow ran: the **§7.17 Understand-pass** (parallel Android
> customer-tail + backend tail-contracts/security + cross-cutting iOS-mechanisms surveys) → architect cluster plan
> (§7.17, every choice composes an accepted ADR — NO new ADR) → the security design gate **PASS-WITH-REQUIREMENTS**
> (GDPR-delete verdict **PASS**) → 6 dev slices each with a concurrent reviewer + Gate-SEC on the security-touching
> C/D/F; **§7.17 is now CONFIRMED-AS-SHIPPED**. **T-0350** (the one LOW backend finding) was filed `proposed`.
> **T-0314 → `done`** (`phase/ios-phase8`, off master `8d90104`; 6 slices): **A** (`6a587cf`) Home + Orders/
> OrderDetail (the paged list, the 7-state OrderStatus incl. OnTheWay=3, lifecycle timeline/LiveProgressHero,
> cancel/review/receipt-via-QuickLook, the 5-min poller) + the T-0313 success→OrderDetail fold; **B** (`035c211`)
> Rewards/Loyalty/Referrals (tier/progress/perks + paged activity + referral copy/share); **C** (`cae96dc`,
> **Gate-SEC**) Membership/Plus (the two-phase Stripe **SetupIntent** via the T-0313 `StripePaymentController` seam
> extended with `PaymentIntentKind` — still the **sole Stripe importer**) + Recurring + the deferred ConfirmRecurring
> on OrderDetail — reviewer **APPROVE** + security **PASS**; **D** (`c10d819`, **Gate-SEC**) Disputes (list/create/
> detail + the generated **multipart** evidence upload, EXIF-strip, fail-closed file validation) + the customer's
> first camera/photo permission strings + `PrivacyInfo` — reviewer **APPROVE** + security **PASS**; **E** (`58d9d35`)
> Addresses (AddressManager 3-pane + saved-address CRUD on the Core map seam); **F** (`4a15fbf`, **Gate-SEC**)
> Profile/Settings hub + **GDPR DeleteAccount** (`signOutLocal`-not-`logout`; blocked→stay-signed-in; the SIWA-revoke
> owner-deferred + the "remove in Settings" note) + Devices (T-0310 D6-8) + NotificationPreferences (optimistic +
> debounced) + the REAL change-password flow + Language/Appearance/Help — reviewer **APPROVE** (after the Profile-
> Subscribe-CTA fix) + security **PASS** (GDPR verdict **PASS**). **Security posture (all PASS vs code,
> `security/ios-customer-auth.md`):** the membership money path (`.completed` UX-only / webhook-authoritative /
> fail-closed / idempotency-token replay); the dispute multipart upload (own-dispute server-scoped / fail-closed
> validation / EXIF-strip / no-secret); the GDPR delete (anonymize-not-resurrect / own-account-only / signOutLocal /
> SIWA-revoke deferred per §7.14 D4 + TN3194). **The Slice-F review caught an iOS-right/Android-stub parity gap** →
> the NEW follow-up **T-0351** (the Android customer `SecurityScreen` "Update" is a dead no-op — wire it to the
> existing reset-code flow). **Tests: CleansiaCore 216 + CleansiaCustomer 362** (+ the CleansiaPartner 366
> non-regression) on the iPhone 17 simulator; swiftformat + swiftlint --strict clean. **OWNER / manual steps (for
> go-live/App Store — NOT code blockers; the PM never runs these):** the **Stripe publishable key** (`pk_`, now live
> card AND membership); **T-0344** (Apple SIWA capability); **T-0345** (Google client ids/IMP-1); the
> **customer-mobile spec + client regen** (the membership `idempotencyToken` is cross-platform only after it — iOS
> already carries the field); the **camera/photo + customer-brand plist WORDING** sign-off; the **App Store
> compliance pass** — the `ios-app-review-checklist.md` (ADR-0016) must go green per app: 5.1.1 in-app delete ✓
> (shipped), SIWA ✓, Stripe-not-IAP framing, the ASC privacy answers + a demo account. **Follow-ups kept proposed
> (NOT dispatched):** **T-0348** (mobile PaymentIntent refund) · **T-0349** (address-picker→Core harvest) · **T-0350**
> (NotificationPreferences rate-limit) · **T-0351** (the Android `SecurityScreen` catch-up); + the recorded notes
> (the customer brand asset to replace the SF-Symbol `AuthHeaderImage` + the Google-"G" pre-submission fidelity check;
> the `LiveCountryResolver`/country-bias → T-0334; the membership `idempotencyToken` customer-mobile NSwag regen — iOS
> already carries the field; the dead `CreateOrderResponse.stripeSessionId` DTO cleanup at the next regen, S9
> hygiene). **Remaining iOS scope after this merges:** the App Store compliance/release pass + the owner provisioning
> + the deferred follow-ups (T-0334/0337/0338/0340/0348/0349/0350/0351 + the brand assets) — **the FEATURE port is
> done.** The owner commits these backlog edits + opens the Phase-8 PR (the PM does not commit). **Reconciliation
> note (the squash-merge caveat):** the prior phases were squash-merged to master (master is now `8d90104`) — verify
> any prior-phase status by master **TREE** content, not `git merge-base --is-ancestor` (a squash flattens the
> original commits). Full plan + the §3 ticket table + the §7.17 ruling + the Phase-8 status-log:
> **`status/sprint-12.md`** (top banner reconciled 2026-06-30).
>
> **--- (Phase-7 banner — kept for traceability) ---**
> **iOS PHASE 7 IS DONE — `phase/ios-phase7` (7 commits, pushed; off master `c47f34a`); merged.
> This phase delivers ADR-0013's named HARD AREA #1 — the customer booking wizard + Stripe PaymentSheet — the single
> hardest feature in the port AND the customer PRIMARY flow, plus the T-0347 backend money-safety fix it surfaced.**
> Commits: `f2113da` (the §7.16 Understand-pass + the T-0332 resolution + T-0347/0348/0349 docs), `afaa920` (T-0347
> backend), `db4a12f`/`67f12e2`/`c42679d`/`4e30aff`/`8a2b4c7` (T-0313 Slices A–E). The verified workflow ran (parallel
> Android/backend/Stripe survey → architect ruling §7.16 → security design gate → 5 dev slices each with a concurrent
> reviewer + Gate-SEC on D+E). **T-0347 (backend money-safety) → `done`** (`afaa920`): **one charge surface per card
> order** — a per-host `IOrderChannelProvider` so `OrderPaymentDispatcher` suppresses the Stripe Checkout Session for
> the mobile PaymentSheet channel (the mobile order's single surface is the PaymentIntent). Closes a **pre-existing
> double-capture defect** — the live **Android** card flow had it too. Host-based discriminator; NO contract change,
> NO regen, NO EF migration. Reviewer **APPROVE** + security **PASS**. **T-0313 (customer booking wizard + Stripe) →
> `done`** (5 slices): **A** (`db4a12f`) the 3-step modal anchored sheet (`.sheet`/`.presentationDetents`) + the
> shared `BookingViewModel` + the Book FAB action; **B** (`67f12e2`) Step 1 Services + the server-authoritative
> pricing engine (debounced live `Quote` + the `BookingPricing` display port) + the first customer generated-client
> auth spine (`CustomerCoreSpineRequestBuilderFactory`, the ADR-0019 twin); **C** (`c42679d`) Step 2 When&Where (the
> Core-map-seam address picker + lead-time time slots) + the Confirm extras/promo/referral FSMs; **D** (`4e30aff`)
> Step 3 Confirm + the CASH submit + the **T-0332 dual-use Bearer carve-out** (`HeaderAdapter` attaches the Bearer on
> the 3 booking endpoints **iff a token exists**; guest tokenless; pure-anon never; `CreatePaymentIntent` always
> authed) + the server-authoritative price echo + the double-submit debounce — the cash success screen is
> status-accurate ("Booking received", Pending+New, NOT "Confirmed"). Reviewer **APPROVE** + **SECURITY PASS** (no
> pure-anon Bearer leak; no under-charge; guest path + partner non-regression preserved); **E** (`8a2b4c7`) the CARD
> branch + Stripe **PaymentSheet** (`StripePaymentSheet` SPM dep, customer target only @25.17.0; the
> `PaymentSheetPresenting`/`StripePaymentController` seam = the **sole Stripe importer**; publishable-key fail-closed).
> Reviewer **APPROVE** + **SECURITY PASS** (`.completed` is UX-only — the webhook is the sole paid authority; no
> secret leak; fail-closed; single charge surface). **Resolves T-0332** (`draft` → `done`/RESOLVED — the dual-use
> carve-out shipped in Slice D). **New seams:** the customer ADR-0019 spine; the
> `PaymentSheetPresenting`/`StripePaymentController` Stripe seam; the `BookingPricing` display port. **Gate-DP
> divergences:** the Android anchored bottom-sheet → native `.sheet`/`.presentationDetents`; the official Stripe
> PaymentSheet; the address picker on the Core map seam. **Security posture (all PASS vs code,
> `security/ios-customer-auth.md`):** price-tampering NO; the dual-use Bearer scoping verified; `.completed`-is-UX-only
> / webhook-authority; no-secret-leak; fail-closed; single charge surface. **Tests: CleansiaCore 213 + CleansiaCustomer
> 156** (+ partner 366 non-regression) on iPhone 17; swiftformat + swiftlint --strict clean. The customer generated
> client is gitignored (CI regenerates). **OWNER / manual steps (the PM never runs these):** the **Stripe publishable
> key** (`pk_`, the sole LIVE-card provisioning; the code ships fail-closed; the T-0347 fix is landed); the standing
> owner items carry forward (**T-0344** Apple, **T-0345** Google, the **customer-mobile regen**, the **camera/photo
> plist wording** ×5). **Follow-ups kept proposed:** **T-0348** (mobile PaymentIntent refund path), **T-0349**
> (address-picker→Core harvest), + the **T-0314** items (success→OrderDetail nav + the lifecycle timeline/
> `OrderSummaryCard` when the customer Orders feature lands; the customer brand asset to replace the Phase-6 SF-Symbol
> `AuthHeaderImage` + the Google "G" brand-fidelity check; saved-address CRUD; the `LiveCountryResolver` fold into the
> T-0334 `ServiceAreaProvider` seam; a minor backend DTO cleanup — `CreateOrderResponse.stripeSessionId` is a dead
> legacy field iOS never reads, drop at the next NSwag/spec regen, S9 hygiene). The owner commits these backlog edits +
> opens the Phase-7 PR (the PM does not commit). Full plan + the §3 ticket table + the §7.16 ruling + the Phase-7
> status-log: **`status/sprint-12.md`** (top banner reconciled 2026-06-29). With T-0313 done, the customer app's
> primary flow is complete; the remaining Wave-10 scope is the **T-0314** parity tail. **Reconciliation note (the
> squash-merge caveat):** prior phases were squash-merged to master (master is now `c47f34a`) — verify any prior-phase
> status by master **TREE** content, not `git merge-base --is-ancestor` (Phase 6's AppleAuth + customer-auth files
> confirmed present in the master tree this session).
>
> **--- (Phase-6 banner — kept for traceability) ---**
> **iOS PHASE 6 IS DONE — `phase/ios-phase6` (6 commits, pushed; off master `c898e79` = the merged Phase-5 PR #97);
> the Phase-6 PR is drafted. This phase opens the CUSTOMER app — the FIRST customer feature** (the partner app
> shipped feature-complete in Phases 0–5). It stands up the customer app's root + shell scaffold + the complete auth
> front door incl. **Sign in with Apple** + **Google**. Both tickets passed the full workflow (architect
> Understand-pass → dev slices → reviewer/Gate-DP + security on the touching slices). **T-0343 (backend AppleAuth —
> Sign in with Apple) → `done`** (`a689d03`): a new `AppleAuth` CQRS + `IAppleTokenVerifier`/`AppleTokenVerifier`
> (**RS256-PINNED** JWKS via `JsonWebTokenHandler` + `ConfigurationManager`; `aud == Apple:BundleId` native,
> iss/exp/nonce, fail-closed, no SSRF) + `AppleConfig` + `User.AppleId`/`AuthenticationType.Apple`/`CreateWithApple`
> + `[AllowAnonymous] POST /api/Auth/AppleAuth` on the Customer Mobile host + `InvalidAppleUserToken` ×5 i18n.
> **Mirrors GoogleAuth 1:1.** Reviewer **APPROVE**; **SECURITY PASS** (account-takeover **NO** — the RS256-pin + the
> handler takeover-guard verified vs code). Ships **fail-closed**; LIVE Apple sign-in is gated on **T-0344** + the
> owner EF migration (`User.AppleId`) + the `customer-mobile-api` regen. **T-0312 (iOS customer shell + auth — 3
> slices, §7.15) → `done`** (`2cf0f1e`+`6f9c1de`+`2ae5982`): **Slice A** = `CustomerRootView` (flat-enum, COPIES the
> ADR-0020 pattern, the simpler customer gate — no RegistrationLock) + the 4-tab shell + the inert Book FAB (the
> FAB-as-overlay Gate-DP swap) + the new `CleansiaCustomerTests` target; ios-ci now `build test`s CleansiaCustomer.
> **Slice B** = the email auth chain (SignIn/SignUp/EmailVerify/Forgot) + the event-driven `CustomerAuthViewModel`
> (emits `AuthOutcome`, the router maps) + the **Core spine `RegisterEndpoint` fix** (a construction-time param:
> customer→`/api/Auth/Register`, partner→`RegisterEmployee`, byte-equivalent). **Security PASS** (no parallel write
> path; partner non-regression). **Slice C** = social — the official `ASAuthorizationAppleIDButton` + the real
> multicolor Google "G"; the **`SocialSignInProviding` Core seam** (the Apple nonce flow + GoogleSignIn-iOS, the
> sole framework consumers); two spine methods `googleAuth`/`appleAuth` (reuse the one empty-token gate + the single
> Keychain persist; appleauth anon-allow-listed); the GoogleSignIn SPM dep + the `com.apple.developer.applesignin`
> entitlement + the reversed-client-id placeholder; fail-safe when unconfigured. Reviewer **APPROVE**; **SECURITY
> PASS** (the iOS↔backend nonce-encoding **ALIGNED** — live SIWA won't silently fail; no parallel write path).
> **Gate-DP divergences:** the Android pager + floating-pill `CustomBottomBar` → native `TabView` + FAB-overlay; the
> official Apple button + the recreated Google "G"; the `AuthHeaderImage` SF-Symbol PLACEHOLDER → T-0314 brand
> asset. **T-0314 follow-ups recorded** (so they aren't lost): (a) ship the customer brand asset to replace the
> SF-Symbol `AuthHeaderImage` + add the Android SignIn brand wordmark; (b) a brand-fidelity check of the recreated
> Google "G" vs Google's official asset before App Store submission — both folded into the T-0314 §3 row/notes.
> **Tests:** **CleansiaCore 202 + CleansiaCustomer 42** (+ the CleansiaPartner 366 non-regression) on the iPhone 17
> simulator; swiftformat + swiftlint --strict clean. **CI:** ios-ci now `build test`s CleansiaCustomer; backend-ci
> runs the AppleAuth tests. The build-time security verifications are in `security/ios-customer-auth.md`. **OWNER /
> manual steps (all gate LIVE social sign-in; the code ships fail-closed):** T-0344 (Apple capability +
> `Apple:BundleId`), T-0345 (Google client ids + `Google:ClientId` / IMP-1), the **EF migration for `User.AppleId`**,
> the **customer-mobile-api spec + client regen** (also needed for the T-0314 business endpoints + the live social
> e2e). **Follow-up:** T-0346 (backend Google `email_verified` hardening, dep T-0343 now ✓). The owner commits these
> backlog edits + opens the Phase-6 PR (the PM does not commit). **Reconciliation note (the squash-merge caveat):**
> Phase 5 merged to master as the squash commit `c898e79` (PR #97); `git merge-base --is-ancestor` would misread the
> original Phase-5 commits — verify any prior-phase status by master **TREE** content, not the ancestor check. Full
> plan + the §3 ticket table + the §7.14/§7.15 rulings + the Phase-6 status-log: **`status/sprint-12.md`** (top
> banner reconciled 2026-06-28). The remaining Wave-10 scope is the customer tail (**T-0313** booking+Stripe →
> **T-0314** the parity tail).
>
> **--- (Phase-5 banner — kept for traceability) ---**
> **iOS PHASE 5 IS DONE — `phase/ios-phase5` (8 commits, pushed); merged via #97. This phase makes the
> iOS PARTNER APP FEATURE-COMPLETE** — every partner surface is now ported (auth → shell → dashboard → orders+photos
> → profile/devices/prefs → earnings/invoices → push); the remaining Wave-10 scope is the customer app
> (T-0312…T-0314). Both Phase-5 tickets passed the full workflow (ios dev → reviewer/Gate-DP + security on the
> touching slices). **T-0309 (partner earnings + invoices + PeriodPay) → `done`** (`59be42b`+`e4e7793`+`7daa412`):
> Slice A = the Earnings summary (reuses `getStats`) + PeriodPay (E1/E2 own-id) over the generated
> `PartnerEmployeePayrollAPI` + a Core `EarningsFormat`; the `.invoices` tab is now the Earnings surface (in-tab
> `NavigationStack`/`EarningsRoute`). Slice B = the invoices list/detail + the new Core `QuickLookPreview` seam
> (PDF) + the InvoicesStaleness silent-stale resume; **`RefreshPhase` LIFTED to `CleansiaCore/State`** (shared by
> Orders+Invoices). Reviewer **APPROVE**; **SECURITY PASS** (§7.11 E1–E4 + TC-IOS-EARNINGS-OWNERSHIP — backend
> EmployeePayroll already JWT-scoped, **no T-0339-class gap, no backend follow-up**; E4 PDF deleted from cache on
> dismiss). **T-0311 (partner APNs push registration) → `done`** (`f2a999f`+`8d53b18`+`b4fb556`): Slice A = the
> `PushRegistrar` Core seam + `PushTokenRegistrar` (`SessionScopedCache`) + the `Device/Register` client. Slice B =
> the `PushSessionObserver` (register on session×token) + the `@UIApplicationDelegateAdaptor` AppDelegate + the new
> `Auth.setPreLogout` hook (logout unregisters BEFORE the token wipe) + the `aps-environment` entitlement +
> tap→OrderDetail. Reviewer **APPROVE**; **SECURITY PASS** (§7.11 rules 1–4 + TC-IOS-PUSH-LOGOUT-CLEARS; the
> `setPreLogout` hook safe/non-regressing; no T-0339-class backend gap). **End-to-end push DELIVERY is OWNER-gated
> → T-0342** (the APNs `.p8` key + Push capability; code ships complete + the entitlement without it). **PLUS a CI
> hardening (`1eb346f`):** ios-ci now runs the CleansiaPartner test suite (`build test`), not just `build` — the
> partner VM + security tests (366) now actually gate in CI for the first time. Tests: **CleansiaCore 194 +
> CleansiaPartner 366** (iPhone 17); swiftformat + swiftlint --strict clean. **T-0339 reconciliation (RESOLVED — it IS in master):**
> PR #96 was SQUASH-merged (single parent `7055ef4`), so `merge-base --is-ancestor d688d30` reads NO (a squash
> flattens the original commits) — but master's TREE contains the T-0339 fix:
> `GetPagedOrdersScopeIntegrationTests.cs` + `RestrictToEmployeeId` + the GetPagedOrders caller-pin are all
> present in `origin/master` (verified by tree content). NO action needed. Full plan + the §3 ticket
> table + the Phase-5 status-log: **`status/sprint-12.md`** (top banner reconciled 2026-06-28).
>
> **--- (Phase-4 banner — kept for traceability) ---**
> **iOS PHASE 4 IS DONE — `phase/ios-phase4` (9 commits, pushed); merged via #96.** Both tickets
> passed the full workflow (ios dev → reviewer/Gate-DP + security on the touching slices). **T-0307 (partner order
> work-loop — OrdersList + OrderDetail + the OnTheWay lifecycle + checklist/notes/issues/timeline; `L → split` into
> 5 slices; HARD AREA #3) → `done`** (`4cb76ef`+`94050ae`+`3d0bf0d`+`7fca473`+`3c44356`+`42bb402`): **A** = Core
> `SnapSheet` (**ADR-0021** non-modal 3-snap) + `fullBleedMap(coordinate:)` (single-pin `MKMapView`); **B** = the
> 3-pane OrdersList (sealed per-pane `UiState` + `RefreshPhase` + the ported staleness cache + the Code→OrderStatus
> one-mapper; **security O3** own-id-only); **C** = the OrderDetail shell (SnapSheet over fullBleedMap + content
> cards); **D** = the lifecycle actions + the shared **pure `OrderPrimaryAction` machine** (**SECURITY PASS
> O1/O2/O4**, TC-IOS-ORDERS-OWNERSHIP); **E** = checklist (local store) / author-scoped notes+issues / status
> timeline. Reviewer **APPROVE** on all 5; security **PASS** — the one backend `GetPaged` D2b gap → **T-0339**
> (iOS proceeded in parallel). **T-0308 (partner photo upload — camera/library → base64-over-JSON; 2 slices) →
> `done`** (`c216392`+`cf6ea6d`+`a2a2184`): **A** = Core `CameraOrLibraryPicker` (the repo's first
> `UIViewControllerRepresentable`) + `ImageCompressor` (1920/0.7, **EXPLICIT EXIF strip**, TC-IOS-PHOTOS-EXIF-STRIP);
> **B** = the `PhotosSection` (before/after rails, capture → upload) + the upload/delete VM + the Complete-unblock +
> the bootstrapped `PrivacyInfo.xcprivacy` + the NSCamera/NSPhotoLibrary usage strings ×5. Reviewer **APPROVE**;
> **SECURITY PASS** (P1–P5, TC-IOS-PHOTOS-OWNERSHIP — backend SavePhotos/DeletePhoto/GetPhotos ownership VERIFIED
> safe, no backend change). Tests: **CleansiaCore 163 + CleansiaPartner 320** (iPhone 17); swiftformat + swiftlint
> --strict clean. **REQUIRED backend follow-up T-0339** (GetPagedOrders employeeId over-read — SECURITY, high, gates
> the GetPaged contract for go-live). **Owner items:** the camera/photo plist WORDING sign-off ×5 (T-0308 shipped en
> + cs/sk/uk/ru); T-0325 location string (owner, unused by Phase 4). Next runnable = **T-0309** (earnings/invoices,
> deps T-0304✓) ∥ **T-0311** (APNs push) ∥ the customer batch (T-0312…T-0314). Full plan + the §3 ticket table +
> the Phase-4 status-log: **`status/sprint-12.md`** (top banner reconciled 2026-06-27).
>
> **--- (Phase-3 banner — kept for traceability) ---**
> **iOS PHASE 3 IS DONE — `phase/ios-phase3` (7 commits, pushed); the Phase-3 PR is drafted.** Both tickets
> passed the full workflow (ios dev → reviewer/Gate-DP, + security on Devices). **T-0306 (iOS map seam + MapKit +
> partner AddressPicker) → `done`** (`480f5c4`+`03a00f3`+`199916b`): Slice A = the Core `MapProvider`/
> `GeocodingService` seam + `Coordinate`/`GeocodedAddress` + `CLGeocoderGeocodingService` + the **iOS-16
> `MapKitMapProvider`** (125 CleansiaCore tests); Slice B = the partner `AddressPickerView`/`VM` (pan + search,
> full-bleed map + static center-pin, 300/500ms debounce verbatim, NO `UiState`/`ActionState` — reviewer #27,
> returns `GeocodedAddress`). D2 current-location FAB DEFERRED → **T-0335** (recorded Gate-DP divergence).
> Reviewer **APPROVE**. **T-0310 (iOS partner Profile + Devices + Preferences) → `done`**
> (`ce6c5fc`+`ee2f044`+`2cdaf93`+`6c6155c`, 3 slices): Slice A = the profile hub + 6 section editors + onboarding
> chain + the now-live RegistrationLock Fix-CTAs (the lock owns its OWN stack, **fail-closed gate #24 byte-unchanged
> + verified**); Slice B = Devices (list + revoke) — **SECURITY PASS on all binding rules** (D6 single device-id
> source, D7a hide-on-current + D7b defensive self-revoke sign-out, D8 server-scoped revoke verified vs the backend;
> TC-IOS-DEVICES-SELF-REVOKE green); Slice C = Preferences (language [+ a System/follow-device row] + theme via
> `.preferredColorScheme`, the **first runtime in-app language switch**). D3 `ServiceAreaRow`→**T-0334**, D5
> sealed-state (Android E1 NOT replicated)→**T-0337**, Notifications DROPPED→spike **T-0336**. Reviewer **APPROVE**
> (incl. a re-review of the System-row fix); **CleansiaCore 125 + CleansiaPartner 185** tests pass. **Reviewer Slice-C
> MINOR → new follow-up T-0338** (CleansiaCore-owned strings ship en-only behind `bundle: .module` + Core
> `defaultLocalization: en`, so the in-app language switch doesn't reach the Core toasts — localize ×5 + a swappable
> Core bundle). **Standing latent SECURITY item (NOT a Phase-3 regression):** the multi-tenant asymmetry in
> `RefreshTokenService.RevokeByDeviceAsync`/`GetActiveByUserIdAsync` the device-revoke kill rides on
> (`security/auth-sessions.md`) is tracked by **T-0236** (`done` `b8f89202` — the read-side `IgnoreQueryFilters` fix
> covers it) + carried in `security/ios-devices.md`; dormant in single-tenant prod, **re-verify before onboarding any
> non-null-`TenantId` user**. Next runnable = **T-0307** (partner order work-loop) ∥ **T-0309** (earnings/invoices).
> Full plan + the §3 ticket table + the Phase-3 status-log: **`status/sprint-12.md`** (top banner reconciled 2026-06-27).
>
> **--- (Phase-1/2 banner — kept for traceability) ---**
> **iOS PHASE 1 IS DONE — the proving vertical (T-0303) is green on `phase/ios-phase1`.** Both owner
> blockers that held T-0303 are **CLEARED**: the dev mobile API is **live** and the owner ran the
> **mobile-spec-regen** (post-T-0272 specs committed `9232335`), so the T-0302 first real generation ran
> (`8d4cfe3`). T-0303 ships in **2 commits** — `8996df9` (Slice A: partner login spine) + `2a57f70`
> (Slice B: read-only Dashboard) — preceded by `d965c5b` (ADR-0019 + the §7.2 scope record). **The vertical
> works end-to-end:** partner login (hand-written auth, empty-token/unverified gate, router gates
> verified→dashboard vs unverified→`verifyEmail` placeholder) → authed read-only Dashboard (greeting +
> Weekly-earnings / Pay-period / Last-month cards + the 3-state hero) via the generated `dashboardGetStats`
> through the **ADR-0019 Core-spine-backed `RequestBuilderFactory`**. **Gates green:** reviewer **#13-gen
> PASS** + **TC-IOS-GEN** green (Bearer + device/time-zone headers despite `requiresAuthentication:false`; a
> 401 → single-flight refresh + exactly one retry with the rotated token); the required router-gate test
> (`requiresEmailConfirmation`→`verifyEmail`) present; `swiftformat --lint` + `swiftlint --strict` clean;
> **CleansiaCore 93 + CleansiaPartner 17** tests pass on the iPhone 17 simulator; **reviewer AND security
> APPROVE both slices.** Two security forward-notes recorded for the later authed waves (sprint-12 §7.3). The
> owner commits these backlog edits to the phase branch (the PM does not commit).
>
> **--- (Phase-0 foundation banner — kept for traceability) ---**
> **iOS Phase 0 (the foundation behind T-0296…T-0301 + the T-0302 codegen *wiring*) is implemented,
> committed, and MAC-VERIFIED this session, and the iOS CI gate (T-0323) is merged.** The foundation was
> authored earlier on Windows; this session **compile-verified + fixed it on a Mac** (Xcode 26.3, iOS-16
> simulator): **CleansiaCore builds + all 68 unit tests pass on the simulator; both app schemes
> (`CleansiaPartner`/`CleansiaCustomer`) build AND launch in the simulator; `swiftlint --strict` +
> `swiftformat --lint` are both clean.** A launch-crash blocker (`API_BASE_URL` never reaching
> `Info.plist` → `fatalError`) was **found, fixed, and proven by launching the app** — NOT tracked open
> (audit `audits/AUDIT-2026-06-26-ios-phase0-foundation.md`). **Merged:** `8220f4c` "ci: add iOS
> build/test/lint workflow (macOS runner) (#90)" + `6628172` "Fix/ios phase0 verification (#91)"
> (foundation commit `c1009c6`). Full plan + the §3 ticket table + the §7.1 blocker detail:
> **`status/sprint-12.md`** (top banner reconciled 2026-06-26).
>
> **Done + verified (6):** T-0296 (workspace+package+2 targets), T-0297 (tokens+components), T-0298 (DI
> root), T-0299 (snackbar/error center), T-0300 (auth/session/header spine — 68 CleansiaCore tests green),
> T-0301 (header-parity spec doc). **T-0323 done via CI** (`.github/workflows/ios-ci.yml`, **#90** —
> macOS, path-filtered `src/cleansia_ios/**`, **BLOCKING** `swiftformat --lint` + `swiftlint lint
> --strict`, then build+test CleansiaCore + both schemes on a simulator). **T-0302 DONE** — the codegen
> toolchain's **first real generation** ran against the regenerated post-T-0272 spec (`9232335`) on
> `phase/ios-phase1` (`8d4cfe3`); the earlier WIRING check (159 Swift files from the committed spec,
> throwaway output removed) proved the pipeline.
>
> | ID | Title | Size | Status | depends_on | Layers | sec | manual_step |
> |----|-------|------|--------|-----------|--------|-----|-------------|
> | **T-0296** | Xcode workspace + `CleansiaCore` SPM package + 2 app targets (iOS-16 floor) | M | **done ✅ (verified)** `c1009c6` | — | ios | no | — |
> | **T-0297** | Design tokens + `Cleansia*` SwiftUI components (`ObservableObject`/`@Published`) | M | **done ✅ (verified)** `c1009c6` | T-0296✓ | ios | no | — |
> | **T-0298** | DI composition root (`AppContainer` per app) | S | **done ✅ (verified)** `c1009c6` | T-0296✓ | ios | no | — |
> | **T-0299** | Global snackbar bus + error center (`ApiError→String` seam) | S | **done ✅ (verified)** `c1009c6` | T-0296✓ | ios | no | — |
> | **T-0300** | Auth/session/header spine (Keychain, single-flight 401-refresh, header adapter, anon allow-list) | L→split | **done ✅ (verified)** `c1009c6` (68 tests; 2 dormant findings → T-0331/T-0332) | T-0296✓, T-0298✓ | ios | no | — |
> | **T-0301** | Header-parity spec document (`docs/header-parity-contract.md`) | S | **done ✅ (verified)** `c1009c6` | — | ios, docs | no | — |
> | **T-0302** | Swift codegen toolchain (openapi-generator swift5+urlsession) | M | **done ✅** wiring `c1009c6` / first real gen `8d4cfe3` (regen `9232335`) | T-0296✓ | ios | no | **mobile-spec-regen (owner) ✓** |
> | **T-0323** | SwiftLint + SwiftFormat **BLOCKING** iOS CI gate (macOS) | S | **done ✅ (via CI)** `8220f4c` (**#90**) | T-0296✓ | ios | no | — |
> | **T-0303** | Phase-1 partner login → read-only Dashboard (the proving vertical) | M | **done ✅** `8996df9`+`2a57f70` (`phase/ios-phase1`; both owner blockers CLEARED; #13-gen + TC-IOS-GEN green; CleansiaCore 93 + Partner 17 pass; reviewer+security APPROVE both slices) | T-0300✓, T-0302✓ | ios | no | rides regen ✓ + dev-API-live ✓ |
> | **T-0304** | Phase-2 partner shell (`TabView` Dashboard·Orders·Invoices·Profile) + RegistrationLock (fails CLOSED) + SplashGate + ADR-0020 router | M | **done ✅** `55b39aa`+`c269360`+`df71181` (`phase/ios-phase2`; Slice A gate AND-predicate any-nil→LOCKED + BOTH error paths fail closed — reviewer #24 + TC-IOS-REGLOCK green, security APPROVE; ADR-0020 router #23 reseed `.dashboard`→`.splash` closed a latent T-0303 fail-OPEN; 14-token `missingFields` localized ×5. Slice B native `TabView` Gate-DP APPROVE. CleansiaCore 93 + Partner 61 pass on iPhone 17 sim. §7.4: contact-support INERT, silent-stale cache DEFERRED; Fix CTAs→T-0310, onboarding→T-0305) | T-0303✓ | ios | no | — |
> | **T-0305** | Phase-2 partner auth completeness — Register/Forgot/ConfirmEmail/Onboarding chain (+ Core `AppSettingsStore` + `PasswordPolicy`/`PasswordRuleList`) | M | **done ✅** `ccd25cd`+`e232147`+`3e70cdb`+`84d38bc` (`phase/ios-phase2`; 4 slices — §7.5 docs / A ConfirmEmail / B Register / C+D Forgot+Onboarding; every slice reviewer-APPROVE, Slice A also security-APPROVE — traced backend `ConfirmUserEmail` (CODE-resolved → anon double-skip SAFE), C+D gate-safety SAFE. ConfirmEmail replaces the placeholder + reuses the LIVE empty-token gate; #25: `send()` gained `httpMethod:` (ConfirmUserEmail PUT, no silent 405), no new anon entry, Logout authed, positive-control proves the double-skip non-tautological; `.verifyEmail(email:)` carries the email (no `UserProfileStore`); F1 iOS localizes ×5, Android bug NOT replicated → follow-up **T-0333**. Seed now UNCONDITIONALLY `.splash` (ADR-0020 living-doc fold-in — refines D2; gate #24 byte-unchanged, no bypass). CleansiaCore 114 + Partner 96 pass on iPhone 17 sim) | T-0303✓, T-0304✓ | ios | no | — |
> | **T-0306** | **Phase-3** map seam + MapKit default — Core `MapProvider`/`GeocodingService` seam + `Coordinate`/`GeocodedAddress` + `CLGeocoderGeocodingService` + iOS-16 `MapKitMapProvider` + the partner `AddressPickerView`/`VM` (returns `GeocodedAddress`; not wired into AddressSection — that's T-0310) | M | **done ✅** `480f5c4`+`03a00f3`+`199916b` (`phase/ios-phase3`; Slice A Core seam + iOS-16 MapKit — 125 CleansiaCore tests; Slice B partner AddressPicker pan+search, full-bleed map + static center-pin, 300/500ms debounce verbatim, best-effort geocode, NO `UiState`/`ActionState` (reviewer #27). D2 current-location FAB DEFERRED → T-0335 (recorded Gate-DP divergence). Reviewer **APPROVE**; swiftformat/swiftlint clean) | T-0300✓ | ios | no | — |
> | **T-0310** | **Phase-3** partner Profile tab (hub + 6 section editors + onboarding chain + the now-live RegistrationLock Fix-CTAs) + **Devices** (Device/Mine list + revoke, SECURITY-ruled D6–D8) + **Preferences** (Language/Theme) over a new `PartnerProfileClient` (ADR-0019 spine) | M | **done ✅** `ce6c5fc`+`ee2f044`+`2cdaf93`+`6c6155c` (`phase/ios-phase3`; 3 slices. A = hub + 6 editors + onboarding chain + Fix-CTAs (the lock owns its OWN `NavigationStack`+chain VM, pushes the SHARED section set `onboarding==true`, fail-CLOSED, gate #24 byte-unchanged + verified). B = Devices — **SECURITY PASS** (D6 single device-id source, D7a hide-on-current + D7b defensive self-revoke sign-out, D8 server-scoped revoke verified vs backend; TC-IOS-DEVICES-SELF-REVOKE green). C = Preferences (language [+ System/follow-device row] + theme via `.preferredColorScheme`, the first runtime in-app language switch). D3 `ServiceAreaRow`→T-0334; D5 sealed-state, Android E1 NOT replicated→T-0337; current-location FAB→T-0335; Notifications DROPPED→T-0336. Reviewer-MINOR (Slice C Core i18n)→T-0338. Reviewer **APPROVE** (incl. System-row re-review); 185 CleansiaPartner tests; swiftformat/swiftlint clean) | T-0304✓, T-0306✓ | ios | **sec** (Devices D6–D8 PASS) | — |
> | **T-0307** | **Phase-4** partner order work-loop (`L → split` into 5 slices, HARD AREA #3) — OrdersList (3-pane sealed per-pane `UiState`+`RefreshPhase`+ported staleness cache) + OrderDetail (SnapSheet over fullBleedMap + content cards) + the OnTheWay lifecycle (Take→NotifyOnTheWay→Start→Complete) via the shared pure `OrderPrimaryAction` machine + checklist/notes/issues/timeline + **ADR-0021** non-modal 3-snap sheet + the additive `MapProvider.fullBleedMap` | L→split | **done ✅** `4cb76ef`+`94050ae`+`3d0bf0d`+`7fca473`+`3c44356`+`42bb402` (`phase/ios-phase4`; **5 slices A–E**, reviewer **APPROVE** all 5; **SECURITY PASS** §7.8 O1–O4, TC-IOS-ORDERS-OWNERSHIP — the one backend `GetPaged` D2b gap → **T-0339** (pre-existing, iOS proceeds in parallel). E1 flag-bag NOT replicated→T-0337; SlideToCommit→native confirm Gate-DP swap; deferred checklist stable-id→T-0340; current-location FAB→T-0335. CleansiaCore 163 + CleansiaPartner 320 pass on iPhone 17 sim; swiftformat/swiftlint --strict clean) | T-0304✓, T-0306✓ | ios | **sec** (O1–O4 PASS) | — |
> | **T-0308** | **Phase-4** partner photo upload (2 slices) — Core `CameraOrLibraryPicker` (first `UIViewControllerRepresentable`) + `ImageCompressor` (1920/0.7, EXPLICIT EXIF strip) + the `PhotosSection` (before/after rails, capture→upload) + the upload/delete VM + the Complete-unblock + the bootstrapped `PrivacyInfo.xcprivacy` + NSCamera/NSPhotoLibrary usage strings ×5 | M | **done ✅** `c216392`+`cf6ea6d`+`a2a2184` (`phase/ios-phase4`; **2 slices A–B**, reviewer **APPROVE**; **SECURITY PASS** §7.10 P1–P5, TC-IOS-PHOTOS-OWNERSHIP + TC-IOS-PHOTOS-EXIF-STRIP — backend SavePhotos/DeletePhoto/GetPhotos ownership VERIFIED safe, no backend change. Owner sign-off pending on the camera/photo plist WORDING ×5) | T-0307✓ | ios | **sec** (P1–P5 PASS) | **owner: camera/photo plist WORDING sign-off ×5** |
> | **T-0309** | **Phase-5** partner earnings + invoices + PeriodPay (2 slices) — A Earnings summary (reuses `getStats`) + PeriodPay over the generated `PartnerEmployeePayrollAPI` (ADR-0019 spine) + a Core `EarningsFormat`; the `.invoices` tab is now the Earnings surface (in-tab `NavigationStack`/`EarningsRoute`). B invoices list/detail + the new Core `QuickLookPreview` seam (PDF) + the InvoicesStaleness silent-stale resume; `RefreshPhase` LIFTED to `CleansiaCore/State` (shared by Orders+Invoices) | M | **done ✅** `59be42b`+`e4e7793`+`7daa412` (`phase/ios-phase5`; reviewer **APPROVE**; **SECURITY PASS** §7.11 E1–E4 + TC-IOS-EARNINGS-OWNERSHIP — backend EmployeePayroll already JWT-scoped for non-admins (`GetPeriodPaysOwnershipTests` green 4/4), **NO T-0339-class gap, NO backend follow-up**; E4 PDF deleted from cache on dismiss; latent S5 rate-limit → BSP-4d. Android E1 invoices flag-bag NOT replicated → T-0337) | T-0304✓ | ios | **sec** (E1–E4 PASS) | — |
> | **T-0311** | **Phase-5** partner APNs push registration (2 slices) — A the Core `PushRegistrar` seam + `PushTokenRegistrar` (`SessionScopedCache`) + the `Device/Register` client (generated `PartnerDeviceAPI`, ADR-0019 spine; `deviceId`==`DeviceIdProvider`, `platform=="ios"`). B the Core `PushSessionObserver` (register on session×token) + the `@UIApplicationDelegateAdaptor` AppDelegate (`willPresent`+`didReceive` tap→OrderDetail via the `PartnerNotificationDeepLink` port) + the new `Auth.setPreLogout` hook (logout unregisters BEFORE the token wipe) + the `aps-environment` entitlement | M | **done ✅** `f2a999f`+`8d53b18`+`b4fb556` (`phase/ios-phase5`; reviewer **APPROVE**; **SECURITY PASS** §7.11 `security/ios-push.md` rules 1–4 + TC-IOS-PUSH-LOGOUT-CLEARS — verified vs code; the `setPreLogout` hook safe/non-regressing; no token in any DTO/log; no T-0339-class backend gap (`RegisterDevice`/`UnregisterDevice` JWT-scoped + soft-delete stops APNs delivery). FCM→APNs over the SAME `Device/*` contract = Gate-DP divergence (ADR-0013 D8). End-to-end DELIVERY OWNER-gated → **T-0342**. **CI hardening:** ios-ci's partner step is now `build test` (366 partner tests gate for the first time — `1eb346f`)) | T-0302✓, T-0303✓, T-0310✓, T-0331 | ios | **sec** (rules 1–4 PASS) | **owner: T-0342 (APNs `.p8` key + Push capability) — end-to-end-DELIVERY gate** |
> | **T-0312** | 🍎 **Phase-6** iOS CUSTOMER shell SCAFFOLD + FULL auth (the FIRST customer feature; 3 slices, §7.15) — **A** `CustomerRootView` (flat-enum, COPIES the ADR-0020 pattern, the simpler customer gate — NO RegistrationLock) + the 4-tab `TabView` shell + the inert Book FAB (FAB-as-overlay Gate-DP swap) + the new `CleansiaCustomerTests` target; ios-ci now `build test`s CleansiaCustomer. **B** the email auth chain (SignIn/SignUp/EmailVerify/Forgot) + the event-driven `CustomerAuthViewModel` (emits `AuthOutcome`, the router maps) + the Core spine **`RegisterEndpoint` fix** (construction-time param: customer→`/api/Auth/Register`, partner→`RegisterEmployee`, byte-equivalent). **C** social — the official `ASAuthorizationAppleIDButton` + the real multicolor Google "G"; the **`SocialSignInProviding` Core seam** (Apple nonce flow + GoogleSignIn-iOS, sole framework consumers); two spine methods `googleAuth`/`appleAuth` (reuse the one empty-token gate + the single Keychain persist; appleauth anon); the GoogleSignIn SPM dep + the `com.apple.developer.applesignin` entitlement + the reversed-client-id placeholder; fail-safe when unconfigured | M (3 slices) | **done ✅** `2cf0f1e`+`6f9c1de`+`2ae5982` (`phase/ios-phase6`, off master `c898e79`; the §7.15 ruling. Reviewer **APPROVE** all 3; **SECURITY PASS** on the security-touching B+C — no parallel write path, partner non-regression, the iOS↔backend nonce-encoding ALIGNED. Gate-DP divergences: pager+floating-pill → native `TabView`+FAB-overlay; the official Apple button + the recreated Google "G"; the `AuthHeaderImage` SF-Symbol PLACEHOLDER → T-0314 brand asset. **CleansiaCore 202 + CleansiaCustomer 42** (+ partner 366 non-regression) on iPhone 17; swiftformat/swiftlint --strict clean; build-time verifications in `security/ios-customer-auth.md`. T-0314 follow-ups recorded: ship the customer brand asset + the Android SignIn wordmark; brand-fidelity check the Google "G" pre-submission. LIVE social sign-in is OWNER-gated, code ships fail-closed) | T-0302✓, T-0306✓, T-0343✓ | ios | **sec** (B+C PASS) | **owner (gates LIVE social): EF migration (`User.AppleId`) + customer-mobile spec+client regen + T-0344 (Apple) + T-0345 (Google/IMP-1)** |
> | **T-0313** | 🍎 **Phase-7** customer booking wizard + Stripe PaymentSheet (**HARD AREA #1** — the single hardest feature in the port AND the customer PRIMARY flow; **L→split into 5 slices A–E**, §7.16) — **A** the 3-step modal anchored sheet (`.sheet`/`.presentationDetents`, ADR-0018 D3 modal mapping — NOT the ADR-0021 SnapSheet) + the shared `BookingViewModel` + the Book FAB action (replaces the inert T-0312 FAB). **B** Step 1 Services + the **server-authoritative pricing engine** (debounced live `Quote` + the `BookingPricing` display port; iOS echoes the raw `TotalPrice` verbatim, `PriceMatchesAsync` re-validates, the charge reads `order.TotalPrice` — NO client-trusted price) + the first **customer generated-client auth spine** (`CustomerCoreSpineRequestBuilderFactory`, the ADR-0019 twin). **C** Step 2 When&Where (the Core-map-seam address picker + lead-time time slots) + the Confirm extras/promo/referral FSMs. **D** Step 3 Confirm + the CASH submit + the **T-0332 dual-use Bearer carve-out** (Bearer on the 3 booking endpoints iff a token exists; guest tokenless; pure-anon never; `CreatePaymentIntent` always authed) + the server-authoritative price echo + double-submit debounce; the status-accurate cash success ("Booking received", Pending+New — NOT "Confirmed"). **E** the CARD branch + Stripe **PaymentSheet** (`StripePaymentSheet` SPM dep customer-only @25.17.0; the `PaymentSheetPresenting`/`StripePaymentController` seam = the sole Stripe importer; publishable-key fail-closed) | **L→split (5 slices)** | **done ✅** `db4a12f`+`67f12e2`+`c42679d`+`4e30aff`+`8a2b4c7` (`phase/ios-phase7`, off master `c47f34a`; the §7.16 ruling. **HARD AREA #1 CLEARED.** Reviewer **APPROVE** on all 5; **SECURITY PASS** on the money slices D+E — price-tampering NO (server-authoritative; iOS echoes raw `TotalPrice`, `PriceMatchesAsync` re-validates); the dual-use Bearer scoping verified (no pure-anon Bearer leak; guest path + partner non-regression preserved); `.completed` is UX-only / the webhook is the sole paid authority; no secret leak; fail-closed; single charge surface. **Resolves T-0332** (Slice D) + the backend money-safety prerequisite **T-0347** (`afaa920`, reviewer APPROVE + security PASS). Gate-DP divergences: the Android anchored bottom-sheet → native `.sheet`/`.presentationDetents`; the official Stripe PaymentSheet; the address picker on the Core map seam. **CleansiaCore 213 + CleansiaCustomer 156** (+ partner 366 non-regression) on iPhone 17; swiftformat + swiftlint --strict clean; the customer generated client is gitignored (CI regenerates); build-time verifications in `security/ios-customer-auth.md`. **LIVE card is OWNER-gated on the Stripe publishable key (`pk_`); the code ships fail-closed.** Follow-ups: **T-0348** (mobile PaymentIntent refund) + **T-0349** (address-picker→Core harvest) + the T-0314 items) | T-0312✓, T-0302✓ | ios | **sec** (D+E PASS) | **owner (gates LIVE card): Stripe publishable key (`pk_`) — the code ships fail-closed; the T-0347 fix is landed** |
>
> | **T-0314** | 🎉 **Phase-8** customer TAIL — the **LAST customer feature ticket** and the **broadest** in the port (`L → split into 6 slices A–F`, §7.17); fills the four T-0312 placeholder tabs + every remaining customer surface (neither booking nor auth). **A** Home + Orders/OrderDetail (paged list, the 7-state OrderStatus incl. OnTheWay=3, lifecycle timeline/LiveProgressHero, cancel/review/receipt-via-QuickLook, the 5-min poller) + the T-0313 success→OrderDetail fold. **B** Rewards/Loyalty/Referrals (tier/progress/perks + paged activity + referral copy/share). **C** Membership/Plus — the two-phase Stripe **SetupIntent** via the T-0313 `PaymentSheetPresenting`/`StripePaymentController` seam extended with a `PaymentIntentKind` SetupIntent path (still the **sole Stripe importer**; fail-closed) + Recurring + the deferred ConfirmRecurring PaymentIntent on OrderDetail. **D** Disputes (list/create/detail) + the **generated multipart** evidence upload (`disputeUploadEvidence(file:URL)`, EXIF/GPS strip via the T-0308 `ImageCompressor`, write-to-temp + fail-closed validation) + the customer target's first `NSCamera`/`NSPhotoLibrary` strings ×5 + `PrivacyInfo`. **E** Addresses (AddressManager 3-pane + saved-address CRUD on the Core map seam). **F** Profile/Settings hub + **GDPR DeleteAccount** (`gdprDeleteMyAccount` → `signOutLocal`-not-`logout`; blocked→stay-signed-in; SIWA-revoke owner-deferred per §7.14 D4 + TN3194 + the "remove in Settings → Apple ID" note ×5) + Devices (T-0310 D6-8 verbatim) + NotificationPreferences (optimistic+debounced) + the REAL change-password flow + Language/Appearance/Help | **L→split (6 slices)** | **done ✅** `6a587cf`+`035c211`+`cae96dc`+`c10d819`+`58d9d35`+`4a15fbf` (`phase/ios-phase8`, off master `8d90104`; the §7.17 ruling — **CONFIRMED-AS-SHIPPED**; every choice composes an accepted ADR, NO new ADR. **🎉 THE LAST CUSTOMER FEATURE → the iOS PORT [partner + customer] is FEATURE-COMPLETE.** Reviewer **APPROVE** on all 6 (Slice F after a Profile-Subscribe-CTA fix); **SECURITY PASS** on the security-touching C/D/F — membership money path (`.completed` UX-only / webhook-authoritative / fail-closed / idempotency-token replay / own-membership-only); dispute multipart upload (own-dispute server-scoped via `DisputeNotOwnedByUser` / fail-closed validation ≤10 MiB + jpeg|png|webp+pdf / EXIF-strip / server-controlled blob name / no-secret); **GDPR delete verdict PASS** (anonymize-not-resurrect, own-account-only parameterless command, the no-resurrect test green, `signOutLocal`-not-`logout`, blocked-mid-transaction→stay-signed-in, SIWA-revoke correctly DEFERRED). **The Slice-F review caught an iOS-right/Android-stub parity gap → the NEW follow-up T-0351** (the Android customer `SecurityScreen` "Update" is a dead no-op). **CleansiaCore 216 + CleansiaCustomer 362** (+ partner 366 non-regression) on iPhone 17; swiftformat + swiftlint --strict clean; build-time verifications in `security/ios-customer-auth.md`. **OWNER / manual steps (for go-live/App Store, not code blockers):** the Stripe publishable key (live card + membership); **T-0344** (Apple) + **T-0345** (Google/IMP-1); the customer-mobile spec+client regen (the membership `idempotencyToken` is cross-platform only after it — iOS already carries the field); the camera/photo + customer-brand plist WORDING sign-off; the **App Store compliance pass** (`ios-app-review-checklist.md`, ADR-0016 — 5.1.1 in-app delete ✓, SIWA ✓, Stripe-not-IAP framing, ASC privacy + demo account). Follow-ups: **T-0348** + **T-0349** + **T-0350** + **T-0351**; + notes (the customer brand asset + Google-"G" fidelity; the `LiveCountryResolver`/country-bias → T-0334; the membership `idempotencyToken` regen; the dead `CreateOrderResponse.stripeSessionId` DTO cleanup at the next regen). **Remaining iOS scope after merge:** the App Store compliance/release pass + the owner provisioning + the deferred follow-ups (T-0334/0337/0338/0340/0348/0349/0350/0351 + brand assets) — the FEATURE port is done. **ADDENDUM 2026-07-03 (phase/ios-fix1):** feature-complete ≠ **device-verified** — the owner's first real-device run (iOS 16 iPhone) surfaced 16 issues; the phase/ios-fix1 banner (T-0368…T-0374) is the device-verification debt) | T-0312✓, T-0306✓, T-0313✓ | ios | **sec** (C/D/F PASS) | **owner: Stripe pk_ (live card + membership) + T-0344 (Apple) + T-0345 (Google/IMP-1) + customer-mobile spec+client regen + camera/photo + customer-brand plist WORDING + the App Store compliance/ASC pass** |
>
> **Phase-8 follow-up tickets (filed 2026-06-29/30) — `proposed`, surfaced by the T-0314 Gate-SEC + the Slice-F review; NOT dispatched:**
>
> | ID | Title | Size | Status | depends_on | Layers | sec | manual_step | Source |
> |----|-------|------|--------|-----------|--------|-----|-------------|--------|
> | **T-0350** | **Backend (S5 consistency)** — add `[EnableRateLimiting("auth")]` to NotificationPreferences GetMine/Update (Update is a side-effecting replace-all that lacks the per-JWT-subject window its siblings carry). Own-prefs-only (JWT subject, no wire id) → **no cross-user leak**; the exposure is an un-throttled per-user write. No contract/DTO change, no regen, no migration | S | **done ✅** `64f6525` (HARDENING-1, off master `3e7ce52`; backend trio with T-0346+T-0348. Both hosts' `NotificationPreferencesController` GetMine+Update now carry `[EnableRateLimiting("auth")]` + a `RateLimitCoverageGuardTests` guard for the lazy-create GET; security review CLEAN; `Cleansia.Tests` 1685; reviewer APPROVE) | — | backend | **yes** (CLEAN) | — | T-0314 Gate-SEC §7.17 (LOW) |
> | **T-0351** | **Android** — the customer `SecurityScreen` is a DEAD STUB: the "Update" button calls `onChangePassword` which defaults to a no-op `{}` (`SecurityScreen.kt:38,66`) and the sole call site `CleansiaNavHost.kt:432` never passes one, so the typed password is discarded. Wire it to the existing reset-code change-password flow (`AuthViewModel.requestPasswordChange`/`changePassword`, already serving the forgot-password path) — the iOS-right/Android-stub parity catch-up | S | **done ✅** `1d99333` (HARDENING-1, off master `3e7ce52`; android parity-hygiene commit with T-0333+T-0337. Stub wired to the existing reset-code flow; the `CleansiaNavHost.kt:432` call site now passes the action; success/error via the existing snackbar bus; strings `R.string.*`; no backend change. Verified by a LOCAL gradle build [JDK21/SDK35]; reviewer APPROVE) | — | android | no | — | T-0314 Slice-F review §7.17 |
>
> **HARDENING-1 follow-up tickets (filed 2026-06-30) — `proposed`, surfaced by the HARDENING-1 android review; NON-blocking, NOT dispatched:**
>
> | ID | Title | Size | Status | depends_on | Layers | sec | manual_step | Source |
> |----|-------|------|--------|-----------|--------|-----|-------------|--------|
> | **T-0352** | **Cross-app password min-length policy drift** — customer-app enforces ≥12, partner-app ≥8 (each app's `register_pw_min_length` string matches its own threshold). Pick ONE canonical policy, ideally aligned with the backend `BaseAuthValidator`; align both apps + the surfaced strings ×5. layers `[android]` (+ maybe `ios`/`backend` if elevated to the canonical platform policy) | S | **proposed** (low) | — | android | no | — | HARDENING-1 T-0333 android review |
> | **T-0353** | **Partner profile section-form Error state has no retry affordance** — after T-0337 the section screens (Personal/Address/Identification/Emergency/Bank) consume the sealed state via `is Loading` + `as? Loaded` (not an exhaustive `when`), so the `Error` state renders an empty editable form with no retry. Behavior-preserved from before T-0337; a small UX enhancement (render the `Error(canRetry)` branch). layers `[android]` | S | **proposed** (low) | T-0337✓ | android | no | — | HARDENING-1 T-0337 android review |

**Audit-sweep follow-up tickets (filed 2026-07-02) — `proposed`, surfaced by the three-analyses audit (infra/agentic/codebase) + the approved-fixes sweep (commits `732dc9da`…`be087ae3`); NOT dispatched:**

| ID | Title | Size | Status | depends_on | Layers | sec | manual_step | Source |
|----|-------|------|--------|-----------|--------|-----|-------------|--------|
| **T-0354** | **Backend (money)** — the refund RE-DRIVE branch skips the refundable-ceiling recheck: `RefundService.cs:50-56` re-drives a Pending/Failed row's frozen amount without re-reading `TotalPrice - consumed`; RefundKeys are per-purpose so a cross-key sequence (Pending cancel + Succeeded admin refund + retry) can over-refund. Clamp/fail the re-drive against the live ceiling + a characterization test (the existing re-drive test arranges `ArrangeConsumed(0m)` — the gap is untested) | S | **proposed** (CONFIRMED by adversarial verify) | — | backend | **yes** | — | Codebase audit 2026-07-02 |
| **T-0355** | **Backend (money, 1-cent class)** — customer cancellation refund is unrounded and Stripe TRUNCATES: `CancelOrder.cs:113` `TotalPrice * (1 - feeRate)` (3-4 dp reachable), `StripeClient.cs:93/111` `(long)(amount * 100)` truncates toward zero, while `Refund.Amount` persists `numeric(18,2)` rounded — the ledger and Stripe can differ by 1 cent, feeding the Refunded/PartiallyRefunded comparison. `Math.Round(…, 2, AwayFromZero)` at the source + a truncation-boundary test | S | **proposed** (CONFIRMED) | — | backend | no | — | Codebase audit 2026-07-02 |
| **T-0356** | **Backend (defense-in-depth)** — the CSRF token is a stable per-user HMAC that never rotates: no issuer ever emits a `jti` claim, so `CsrfTokenService.GetSessionKey` always falls back to the user id (its own docstring promises per-jti rotation). Emit `jti` in `SetClaims`/both token issuers + key the CSRF derivation off it (the fallback code already exists); update `AuthCookieConfig.SessionKeyForCsrf` | S | **proposed** (CONFIRMED, low — SameSite=Strict is the primary control) | — | backend | **yes** | — | Codebase audit 2026-07-02 |
| **T-0357** | **Backend+infra** — `/health` is liveness-only (`AddCheck("self")` → 200 while Postgres/Storage are dead), so App Service never recycles a broken instance. Split liveness (`/alive`) from readiness: `AddDbContextCheck<CleansiaDbContext>` + a light blob check on `/health`, keep `healthCheckPath` pointed at readiness | M | **proposed** | — | backend | no | — | Infra audit 2026-07-02 |
| **T-0358** | **CI cost/speed** — every master push full-rebuilds both stacks: no NuGet cache, `--skip-nx-cache` on all 3 Angular builds, migrate-database re-restores the whole solution for the EF bundle, no path filters (a docs-only push runs the full Azure deploy). Cache NuGet + Nx, artifact-reuse the bundle, add path filters — now ONE place to fix (deploy-azure.yml) | M | **proposed** | — | backend | no | — | Infra audit 2026-07-02 |
| **T-0359** | **Prod reliability seam (pre-go-live)** — author the prod posture the dev Bicep deliberately skips: deployment slots + swap (S1 supports them; drop any stop/start), Postgres `highAvailability`/`geoRedundantBackup` params (env-switched), autoscale rule, ACR retention (images accumulate one per sha forever), App Insights sampling ratio + prod ingestion cap, and the VNet/private-endpoint seam for Postgres+Storage (Q-INFRA-03; the 0.0.0.0 Azure-services rule is dev-accepted only) | L | **proposed** | — | architect, backend | **yes** | — | Infra audit 2026-07-02 |
| **T-0360** | **Infra (observability)** — poison-queue depth alert: needs storage diagnostic settings + a scheduled-query rule over the queue logs; deliberately deferred out of `alerts.bicep` (which exports `actionGroupId` for exactly this attachment) | S | **proposed** | — | backend | no | — | alerts lane 2026-07-02 |
| **T-0361** | **Backend (tenant residual)** — `EmployeeRepository.GetByUserEmailAsync` is tenant-filtered but called on ANONYMOUS login (`TokenService.cs:60`) and refresh (`RefreshToken.cs:97`): a tenant-stamped EMPLOYEE gets a JWT **missing `employee_id`** — the same bug class the `e406584f` fix closed for User reads, left open because the file was outside that lane. Mirror the tenant-ignoring anonymous-path pattern + extend the pin test | S | **proposed** | — | backend | **yes** | — | tenant lane residual 2026-07-02 |
| **T-0362** | **Backend (latent money trap)** — `OrderEmployeePay.AddBonus/AddDeduction/UpdatePay` recompute `TotalPay` WITHOUT re-applying the min/max clamp (dead code today; the moment an admin adjust-pay feature wires them, the clamped base silently un-clamps). Re-apply the clamp inside the mutators + tests | S | **done ✅ (code) — OWNER MIGRATION PENDING** on `feature/hardening-cluster-2` (persist the calc-time clamp bounds `MinPay`/`MaxPay` on OrderEmployeePay — 0==unbounded, mirrors the `>0` guard; the 3 mutators now route through one `RecomputeTotalPay()` = `max(0, clamp(base+extras+expenses, MinPay, MaxPay) + bonus − deduction)`, reusing the now-public `PayCalculatorExtensions.ApplyMinMaxClamp` so the >0-guard + min>max-throw stay identical to calc time; new `AggregateBounds()` extension feeds `CalculateOrderPay.Create` the SAME floor/ceiling that produced the initial TotalPay. Side-benefit: `EmployeeInvoice.SumPayAmounts` now recovers the CLAMPED core, not the raw one (except the pre-existing floored-at-0 edge, where the backed-out subtotal is `Deduction − Bonus` — display-only, `TotalAmount == Σ TotalPay` still holds); invariant untouched. 8 exact-decimal clamp tests + 150 EmployeePayroll suite green; adversarial reviewer APPROVE — 6/6 break-attempts refuted, deletion-test verified; no production caller yet — defensive for the pending admin adjust-pay feature. **Residual (reviewer note, conscious decision):** pre-migration rows backfill `MinPay=MaxPay=0` = unbounded, so a future adjust-pay mutation on a LEGACY row that was historically capped re-un-clamps it (equals pre-fix behavior, no trigger today; a true backfill is infeasible — source configs drift). When the adjust-pay feature ships it must either backfill bounds or refuse to mutate default-0-bounds legacy rows). **OWNER MANUAL_STEP:** regenerate the EF migration/model-snapshot so OrderEmployeePay gains 2 columns `MinPay` + `MaxPay` `decimal(18,2)` default `0` (per the pre-prod single-Initial-migration approach — fold into the `Initial` migration; startup `src/Cleansia.Web.Partner`). Nullable-safe: default 0 backfills existing rows as unbounded, matching pre-fix behavior. | S | backend | no | **owner: EF migration (2 columns)** | invoice lane residual 2026-07-02 |
| **T-0363** | **Clients: send email with the 6-digit confirm code** (after `be087ae3` the OTP branch REQUIRES email; Android-customer already sends it) — (a) web customer+partner confirm-email facades pass the email after the NSwag regen; (b) Android partner after the mobile-partner OpenAPI spec regen (`ConfirmUserEmailCommand` is a generated model; thread `state.email` in `AuthRepository.confirmEmail` + the VM); (c) iOS parity on the Mac. Until each client sends email, its confirm of NEW codes fails validation (old long-token emails unaffected) | S | **partial ✅ — customer web done** `44cf8c99` (register + unconfirmed-login carry the email via query param; direct navigation renders an email input, key in all 5 locales; ALSO fixed the auth service importing the PARTNER client's code-only command — would have silently dropped email on the wire — and resend sending undefined on direct nav; facade spec red-proven, 8/8; SSR AOT build green). iOS done earlier `09901f29`. **Partner halves done after the owner regen (2026-07-07):** partner WEB `6572ed4a` (mirror of the customer fix; also fixed resend-undefined on direct nav + a latent forever-disabled resend button; spec red-proven 7/7; partner AOT build green) and Android PARTNER `f73bf472` (repo+VM thread the persisted unconfirmed-login email with a blank guard; new VM test; 52/52 with codegen from the regenerated spec) — **ALL FOUR CLIENTS NOW SEND {code, email}; ticket CLOSED** (residual: iOS suites still run on the Mac only) | — | frontend, android, ios | **yes** | — | OTP fix `be087ae3` |
| **T-0364** | **Backend (ADR-0010 revision, OWNER DECISION pending)** — `SendEmailHandler` claims idempotency BEFORE the send (at-most-once): any send that fails after the claim is PERMANENTLY lost on retry (bit during the broken-config window). Proposal: claim AFTER the successful send for the email consumer (a rare duplicate email beats a lost confirmation); keep claim-before-act where the effect is not safely repeatable | S | **done ✅** `fd0a9d40` (owner APPROVED 2026-07-08; **ADR-0023** ratifies the per-consumer claim-ordering rule, partially supersedes ADR-0010 header-pointer-only; email consumer now check→send→claim with two additive guard members HasProcessedAsync/MarkProcessedAsync; push + AlreadyProcessedAsync byte-unchanged — push→Mode B would need its own ruling; failed-send-retries + claim-failure-acks red-proven; suite 1776/1776; reviewer: only 2 doc-wording minors, fixed in-commit) | — | architect, backend | **yes** | — | SendEmail incident 2026-07-02 |
| **T-0365** | **Architect (multi-tenant activation pack)** — the decisions the tenant fixes deliberately did NOT make: registration semantics under the composite `(TenantId, Email)` unique index (same email in two tenants → anonymous email-keyed login/reset resolve nondeterministically and `RecordFailedLoginAsync` hits ALL rows), host/subdomain tenant-resolution middleware for anonymous requests, and whether confirm-family lookups stay filtered. Blocks multi-tenant go-live; single-tenant behavior is unaffected today | M | **proposed** | — | architect, backend | **yes** | — | tenant lane mustTell 2026-07-02 |
| **T-0366** | **i18n gap on BOTH mobile platforms** — 144 of the 250 `BusinessErrorMessage` keys have NO `error_*` string resource in either Android app (grep-verified: the whole `promo.*`, `membership.*`, `recurring_booking.*` families + `referral.not_accepted/not_qualified/reason_required`), so Android AND the new iOS localizer show the raw key. Translate the user-reachable subset ×5 locales in the Android apps + harvest into the iOS Core catalog (same pipeline as the 106 done). Also: 3 iOS entries were hand-translated (`snackbar.dismiss`, `error.not_found`, `error.request` cs/sk/uk/ru) — owner translation review | M | **proposed** | — | android, ios | no | **owner: review the 3 hand-translated entries** | iOS error-l10n lane 2026-07-02 |
| **T-0367** | **iOS residual** — `ApiError.fromGenerated` (`CustomerGeneratedError.swift:6`, `PartnerDashboardClient.swift:25`) maps most GENERATED-client failures with `code: nil` + the raw body as message, bypassing the new key-based localizer (only paths routed through `ProblemDetailsError.map` get catalog lookups). Route the generated-client error mapping through the shared ProblemDetails parse so booking/disputes/earnings errors localize too | S | **ABSORBED → T-0370 ✅ done** (2026-07-03 — phase/ios-fix1 Slice C carried this exact fix; landed in `5252bfb9`, T-0370 done at phase close) | — | ios | no | — | iOS error-l10n lane 2026-07-02 |
>
> **Phase-7 follow-up tickets (filed 2026-06-28/29) — `proposed`/`done`, surfaced by T-0313/T-0347; T-0348/T-0349 NOT dispatched:**
>
> | ID | Title | Size | Status | depends_on | Layers | sec | manual_step | Source |
> |----|-------|------|--------|-----------|--------|-----|-------------|--------|
> | **T-0347** | **Backend (money-safety)** — one charge surface per card order: a per-host `IOrderChannelProvider` so `OrderPaymentDispatcher` suppresses the Stripe Checkout Session for the mobile PaymentSheet channel (the mobile order's single charge surface is the PaymentIntent). Closes a **pre-existing double-capture defect** (the live Android card flow had it too). Host-based discriminator; NO contract change, NO regen, NO EF migration | M | **done ✅** `afaa920` (`phase/ios-phase7`; reviewer **APPROVE** + security **PASS**; the audience couldn't tell web from mobile — both register `JwtAudiences.Customer` — so a new per-host `IOrderChannelProvider` mirrors the `IHostAudienceProvider` seam: shared Web default, `Web.Customer`=Web, `Web.Mobile.Customer`=Mobile; mobile→`StripeSessionId==null`, web non-regressing; cash unaffected. Residual follow-up T-0348) | — | backend | **yes** (PASS) | — | T-0313 Gate-SEC §7.16 (HIGH double-capture) |
> | **T-0348** | **Backend** — add a PaymentIntent refund path for mobile-paid (PaymentSheet) card orders (after T-0347, a mobile card order has `StripeSessionId==null`; the only refund path keys off the Checkout Session → a mobile-paid order can't be refunded). Admin-host-only refund surface → latent, not a live breach | M | **done ✅** `64f6525` (HARDENING-1, off master `3e7ce52`; backend trio with T-0346+T-0350. `RefundPaymentIntentAsync` on `IStripeClient`/`StripeClient`; `RefundService` routes by charge surface (Session→web XOR `StripePaymentIntentId`→mobile); **NO schema change** (`StripePaymentIntentId` already existed). Money-correctness fold-in: extended the refundable-surface gate to the two CANCEL paths via `Order.HasRefundableChargeSurface` so a cancelled paid mobile/recurring card order refunds. The architect "both-surfaces" finding **refuted/dropped** (one charge surface per card order). Security review CLEAN; `Cleansia.Tests` 1685; reviewer APPROVE) | T-0347✓ | backend | no (CLEAN) | — | T-0347 Gate-SEC §7.16 residual (refund-coverage gap) |
> | **T-0349** | **Harvest (architect "one way")** — hoist the address-picker ViewModel into `CleansiaCore` (unify partner + customer; both ride the Core `MapProvider`/`GeocodingService` seam). NOT a reuse-fail (no Core VM existed); edits the committed partner surface + declares the one way | S | **done ✅** `d834e92` (HARDENING-1, off master `3e7ce52`. Public framework-pure `CleansiaCore/Location/AddressPickerViewModel` (`searchBias=["cz","sk"]` default, load-bearing for partner non-regression); both app-local copies deleted, both apps repoint; Views stay app-local (the sanctioned `pickerMap`/`fullBleedMap` MapKit binding). Core 218 / Partner 366 / Customer 362 green; swiftformat 0.60.1 + swiftlint --strict clean; **no new ADR** (home change via the ADR-0013 escape clause); harvest in `patterns-mobile.md` + `ios-app-architecture.md`; reviewer APPROVE) | T-0313✓ | ios, architect | no | — | T-0313 Slice C reviewer (address-picker VM duplicated partner↔customer) |
>
> **Phase-3/4 follow-up tickets (filed 2026-06-26/27) — all `draft`/`proposed`, deferred out of T-0306/T-0307/T-0308/T-0310; not dispatched:**
>
> | ID | Title | Size | Status | depends_on | Layers | sec | manual_step | Source |
> |----|-------|------|--------|-----------|--------|-----|-------------|--------|
> | **T-0334** | iOS `ServiceAreaProvider` Core seam + the advisory `ServiceAreaRow` (+ forward-geocode country-bias) | M | **draft** | T-0310✓, T-0306✓ | ios | no | — | sprint-12 §7.7 D3 (architect) |
> | **T-0325** | **OWNER TASK** — iOS location-permission purpose string `NSLocationWhenInUseUsageDescription` ×5 (do-it-later; proposed copy in the ticket; agent does the mechanical `project.yml` add once the wording is approved) | S | **proposed** (owner) | — | ios | no | **owner: approve copy ×5** | T-0306 §7.6 D2 + T-0310 §7.7 Scope A |
> | **T-0335** | iOS `LocationProvider` Core seam + the my-location FAB + picker auto-center — **gated on owner T-0325** (`NSLocationWhenInUseUsageDescription`) | M | **draft** (gated) | T-0310✓, **T-0325 (owner, `proposed`)** | ios | no | **T-0325 plist key (owner)** | sprint-12 §7.6 D2 + §7.7 Scope A |
> | **T-0336** | SPIKE — iOS partner in-app notifications feed (persistence choice + push-receipt contract + bell badge) | S | **draft** (spike — **dep T-0311 now ✓, unblocked**) | T-0311✓ | ios, analyst | no | — | sprint-12 §7.7 Scope B |
> | **T-0333** | **Android (E8/F1)** — localize the partner Register/Forgot ViewModel validation strings (move the hardcoded English literals to `R.string.*` across all 5 locales; inject `@ApplicationContext Context`). iOS does it right (T-0305); this is the Android-side parity fix | S | **done ✅** `1d99333` (HARDENING-1, off master `3e7ce52`; android parity-hygiene commit with T-0337+T-0351. Both auth VMs inject `@ApplicationContext Context` + source every validation string from `R.string.*` ×5; behavior identical except language; no rule/DTO change. Verified by a LOCAL gradle build [JDK21/SDK35]; reviewer APPROVE. **Review surfaced a cross-app policy drift → new follow-up T-0352** (customer ≥12 vs partner ≥8 password min-length)) | — | android | no | — | sprint-12 §7.5 D5 / consistency.md §E8 (F1 deviation, surfaced on T-0305) |
> | **T-0337** | Android partner profile VMs — flag-bag `UiState`→sealed (E1) + hardcoded validation/error strings→`R.string.*` (E8) | S | **done ✅** `1d99333` (HARDENING-1, off master `3e7ce52`; android parity-hygiene commit with T-0333+T-0351. The 7 partner profile VMs (Profile + the 6 section VMs) migrated to sealed `Loading`/`Error`/`Loaded` + `ActionState` + `R.string.*` ×5; behavior-preserved; added `BankSectionViewModelTest`. Verified by a LOCAL gradle build [JDK21/SDK35]; reviewer APPROVE. **Review surfaced a residual UX gap → new follow-up T-0353** (the section screens consume `is Loading`+`as? Loaded` not an exhaustive `when`, so the Error state renders an empty form with no retry)) | — | android | no | — | sprint-12 §7.7 D5 (consistency.md E1/E8) |
> | **T-0338** | Localize the CleansiaCore catalog ×5 + route Core localization through a swappable bundle (the Slice-C reviewer MINOR) | S | **draft** | T-0310✓ | ios | no | — | T-0310 Slice C reviewer MINOR |
> | **T-0339** | **SECURITY (backend)** — scope `GetPagedOrders` "mine" views to the JWT caller (client `Filter.EmployeeId` over-read leaks foreign-assigned order coords/codes/pay). Reachable, MEDIUM; pre-existing | S | **done ✅** — landed in master via the PR #96 **SQUASH-merge** (reviewer APPROVE + security PASS, closes D2b). The original commit `d688d30` (+ test-seed fix `fbe21e8`) isn't a master *ancestor* (a squash flattens originals — which is why `merge-base --is-ancestor` misreads NO), but master's TREE contains the fix: `GetPagedOrdersScopeIntegrationTests.cs` + `RestrictToEmployeeId` + the GetPagedOrders caller-pin all present (verified by tree content 2026-06-28) | — | backend | **yes** | — | T-0307 security gate §7.8 (`security/ios-orders.md` D2b) |
> | **T-0340** | Order-detail parity nits — iOS checklist stable-id keying (vs positional index) + Android status-label casing convergence ("On the way"/"In progress") + the stale placeholder-preview literal sweep | S | **proposed** | T-0307✓ | ios, android | no | — | T-0307 Slice E reviewer (Findings 2 + 3) |
> | **T-0341** | **Backend (flaky test)** — deterministic order status-history "current status" ordering (same-tick `CreatedOn` tie in `OrderByDescending(CreatedOn).First()` makes `AdminOverrideOrderStatusHandlerTests` flake 1–2/7); add a tiebreaker / canonical derivation + de-flake. Pre-existing on master (NOT from T-0339) | M | **done ✅** `e4e00b0` (HARDENING-1, off master `3e7ce52`; the architect ruling shipped verbatim. New `OrderStatusTrack.Sequence` (`int`) assigned at append from the aggregate history + canonical `Order.CurrentStatus` (`OrderByDescending(CreatedOn).ThenByDescending(Sequence)`) routed through all in-memory handlers + the mapper + the 2 SQL sites; the `.Any()`-existence checks untouched (out of scope). **manual_step done in-branch: pre-prod Initial-regen** (owner-authorized; `Sequence` folded into `20260623112626_Initial`, timestamp preserved); NO NSwag impact. De-flake **20/20**; **IntegrationTests 97/97 + HostTests 60/60**; `Cleansia.Tests` 1685; reviewer APPROVE) | — | backend, architect | **done in-branch (pre-prod Initial-regen, owner-authorized)** | found during the local backend-suite run for T-0339 |
> | **T-0342** | **OWNER TASK** — iOS APNs auth key (`.p8`) in the backend push provider + Push Notifications capability/provisioning on the App ID (gates end-to-end push DELIVERY; **T-0311 now `done` ships the code + the `aps-environment` entitlement without it** — this is the live-delivery gate) | S | **proposed** (owner — **the active push-delivery gate now T-0311 has landed**) | T-0311✓ | ios | no | **owner: APNs key + Push capability** | T-0311 §3B |
> | **T-0343** | **Backend — AppleAuth (Sign in with Apple)** — `AppleAuth` CQRS + `IAppleTokenVerifier`/`AppleTokenVerifier` (JWKS + **RS256-pinned** + iss/aud=bundle-id/exp/nonce + fail-closed) + `AppleConfig` + `User.AppleId`/`AuthenticationType.Apple`/`CreateWithApple` + `[AllowAnonymous] POST /api/Auth/AppleAuth` on the Customer Mobile host + `InvalidAppleUserToken` ×5 i18n + stubbed-verifier tests. **Mirrors GoogleAuth 1:1**; ships fail-closed (no provisioning). Resolves Q-IOS-04 (§7.14) | M | **done ✅** `a689d03` (`phase/ios-phase6`, off master `c898e79`; the RS256-PINNED JWKS via `JsonWebTokenHandler`+`ConfigurationManager`, `aud == Apple:BundleId` native, iss/exp/nonce, fail-closed, no SSRF; reviewer **APPROVE**; **SECURITY PASS** — account-takeover **NO**: the RS256-pin + the handler takeover-guard against `claims.Email` (covers Internal + Google) verified vs code; provision only on `claims.EmailVerified`. Ships fail-closed) | — | backend, db | **yes** (PASS) | **owner: EF migration (`User.AppleId`) + customer-mobile spec+client regen — gate LIVE Apple sign-in (code ships fail-closed)** | Q-IOS-04 §7.14 / blocked T-0312 |
> | **T-0344** | **OWNER TASK** — Apple SIWA provisioning: enable Sign in with Apple on the `cz.cleansia.customer` App ID (primary) + the Xcode entitlement + `Apple:BundleId` config. **No `.p8`/Services-ID/domain** (identity-token-only). Gates LIVE Apple sign-in (the code shipped in T-0343/T-0312) | S | **proposed** (owner — **now ACTIVE: T-0343+T-0312 have landed; this is a LIVE-Apple-sign-in gate; the code ships fail-closed without it — the T-0311-gated-by-T-0342 pattern**) | — | ios, backend | no | **owner: Apple capability + `Apple:BundleId`** | Q-IOS-04 §7.14 |
> | **T-0345** | **OWNER TASK** — Google Sign-In provisioning for iOS (concretizes **IMP-1**): Cloud Console project + iOS OAuth client id (+ reversed-client-id Info.plist scheme) + web/server client id + set `Google:ClientId`=iOS `serverClientID`=web client id. **Zero backend code** (GoogleAuth already live). Gates LIVE Google sign-in | S | **proposed** (owner — **now ACTIVE: T-0312 Slice C has landed; this is a LIVE-Google-sign-in gate; the code/SPM dep/reversed-client-id slot ship without it**) | — | ios, backend | no | **owner: Google client ids + `Google:ClientId`** | Q-IOS-04 §7.14 D5 |
> | **T-0346** | **Backend (security hardening)** — gate `GoogleAuth` provisioning on `email_verified` (parity with the new AppleAuth gate; the existing Google flow doesn't check it). Deliberately separate from T-0343 (changes live Google behavior) | S | **done ✅** `64f6525` (HARDENING-1, off master `3e7ce52`; backend trio with T-0348+T-0350. `IGoogleTokenVerifier`/`GoogleTokenVerifier` surface `email_verified`; `GoogleAuth.Handler` provisions/links **only** on `email_verified==true` (fail-closed, parity with AppleAuth); the `AuthenticationType != Google` takeover guard stays; added the unverified-token-rejected test. Security review CLEAN (account-takeover NO); `Cleansia.Tests` 1685; reviewer APPROVE) | T-0343✓ | backend | **yes** (CLEAN) | — | Q-IOS-04 security gate §7.14 (medium finding) |
>
> **OWNER / manual-step gates surfaced by Phase 6 (the PM never runs these — all gate LIVE social sign-in + the
> next customer wave; the Phase-6 code ships fail-closed without them):**
> - **EF migration for `User.AppleId`** (the new nullable column T-0343 added to `User`) — the owner creates + applies
>   it; until then the AppleAuth provisioning path can't persist an Apple user. **Gates LIVE Apple sign-in.**
> - **`customer-mobile-api` spec + client regen** — regenerate `customer-mobile-api.json` + the iOS `CleansiaCustomerApi`
>   (+ the Android customer client) after T-0343's `AppleAuth` endpoint/DTOs landed. **Needed for** (a) the T-0314
>   customer business endpoints and (b) the LIVE social e2e (the generated `AppleAuthCommand` confirms the wire shape
>   the hand-written spine DTO matches). The bulk of T-0312 was built ahead of it against the documented §7.14
>   contract; only the live Apple POST round-trip waits on it.
> - **T-0344** (Apple SIWA capability + `Apple:BundleId`) + **T-0345** (Google client ids + `Google:ClientId`, IMP-1)
>   — the per-provider LIVE-sign-in capability/config gates (rows above).
>
> **The standing latent backend SECURITY item — TRACKED, not new:** the multi-tenant asymmetry in
> `RefreshTokenService.RevokeByDeviceAsync` / `RefreshTokenRepository.GetActiveByUserIdAsync` that the iOS remote
> device-revoke session-kill (T-0310 Slice B) rides on (`security/auth-sessions.md` 2026-06-10) is **owned by
> T-0236** (`done ✅` `b8f89202`, Wave-6 6A — the read-side `IgnoreQueryFilters()` fix that covers
> `GetActiveByUserIdAsync` + `GetByTokenHashAsync` + `RevokeChainAsync`). It is **NOT a Phase-3 regression** —
> pre-existing class, correct in today's null-`TenantId` single-tenant mode. The T-0310 Devices Gate-SEC carries it
> as the standing dependency (`security/ios-devices.md` S8). **Standing gate: re-verify T-0236's fix before
> onboarding any non-null-`TenantId` user**, alongside the sibling go-live blockers T-0245 (Stripe webhook tenant
> scope, `done`) and the multi-tenant readiness checklist.
>
> **T-0303's two owner blockers are now BOTH CLEARED** (they previously held T-0303 + every generated-client
> ticket): (1) the owner ran the **mobile-spec-regen** — the formerly-stale committed
> `src/cleansia_android/openapi/{partner,customer}-mobile-api.json` (was 2026-05-31, `1d15484`, pre-T-0272)
> are regenerated to the post-T-0272 contract and committed (`9232335`), so iOS codegen ran against the
> current contract (T-0302 `8d4cfe3`); and (2) the **dev mobile-API hosts are live** (the
> `-partner-mobile-weu-dev` + `-customer-mobile-weu-dev` azurewebsites hosts that returned HTTP 000 on
> 2026-06-26 are up — Wave-11 provisioning T-0317/T-0318/T-0320). With both clear, **T-0303 is `done`** and
> the **ADR-0019 generated-client auth seam is proven** for the later authed waves to copy (sprint-12 §7.3
> records two security forward-notes: the customer wave installs its OWN host-specific factory + allow-list;
> the server-derived `employeeId` round-trip is safe only because the backend overrides the client
> `EmployeeId` for non-admin callers). **PHASE 2 — T-0304 (partner shell + RegistrationLock + SplashGate)
> AND T-0305 (partner auth completeness) are both `done`** on `phase/ios-phase2`. **T-0304** (3 commits —
> `55b39aa` ADR-0020 docs, `c269360` Slice A fail-closed gate, `df71181` Slice B shell; reviewer #23 + #24 +
> TC-IOS-REGLOCK green, security + Gate-DP APPROVE; CleansiaCore 93 + CleansiaPartner 61 pass). The ADR-0020
> router reseed (`.dashboard`→`.splash`) **closed a latent T-0303 fail-OPEN** (an authed-but-incomplete
> partner no longer lands on the authed area). **T-0305** (4 commits — `ccd25cd` §7.5 docs, `e232147` Slice A
> ConfirmEmail, `3e70cdb` Slice B Register, `84d38bc` Slices C+D Forgot+Onboarding; every slice
> reviewer-APPROVE, **Slice A also security-APPROVE** — security traced the backend `ConfirmUserEmail`
> handler, which resolves the user from the confirmation **CODE alone**, so the anon **double-skip** (Bearer
> withheld on the confirm path even with a token stored) is **SAFE**; Slices C+D got an explicit gate-safety
> review — **SAFE**). All four flows shipped: **Register** (+ Core `PasswordPolicy`/`PasswordRuleList`),
> **Forgot** (single-phase), **ConfirmEmail** (replaces the placeholder, **reuses the LIVE empty-token gate**:
> 200+empty → no app entry, 200+token → authenticated), **Onboarding** (2-page pre-auth intro + the SplashGate
> onboarding branch + `hasSeenOnboarding` in the new Core `AppSettingsStore`). #25: `send()` gained an
> `httpMethod:` param (ConfirmUserEmail **PUT**, no silent 405); **no new anon allow-list entry; Logout stays
> authed**; a positive-control test proves the double-skip is non-tautological. `.verifyEmail(email:)` carries
> the email (**no `UserProfileStore`**). **F1:** iOS **localizes ×5** the validation strings the Android
> partner Register/Forgot VMs hardcode in English — **iOS does it right; the Android bug is NOT replicated** —
> the android fix is the **PM-filed follow-up T-0333** (independent of the iOS wave). **Seed refinement:** the
> `PartnerRootView` launch seed is now **UNCONDITIONALLY `.splash`** (was `hasValidSession ? .splash : .login`)
> so the SplashGate is the sole launch resolver — recorded as an **ADR-0020 living-doc fold-in** (refines D2;
> the fail-closed gate #24 is byte-unchanged, no bypass — the no-session branch resolves only to
> `.unauthenticated`/`.needsOnboarding`, never `.authenticated`). `swiftformat`/`swiftlint` clean;
> **CleansiaCore 114 + CleansiaPartner 96** pass on the iPhone 17 sim. **The rest of Phase 2+ (T-0306…T-0314,
> and compliance T-0324…T-0329) remain `proposed`** in `status/sprint-12.md`; the next runnable tickets are
> **T-0306** (map seam + MapKit, deps T-0300✓), **T-0309** (earnings/invoices, deps T-0304✓), and **T-0310**
> (profile/devices, deps T-0304✓ + T-0306). The Phase-0 audit's 2 deferred findings (**T-0331** unblocked,
> **T-0332** booking checkpoint) are in the audit banner directly below; the **android F1 follow-up T-0333**
> (partner Register/Forgot VM validation i18n, `ready`, independent) is filed in `tickets/`.
>
> --- (iOS Phase-0 audit banner below) ---
>
> ## 🍎 iOS PHASE 0 FOUNDATION AUDIT — 2 deferred findings logged (2026-06-26) — NOT dispatched
> **The now-compiling iOS Phase 0 foundation (`CleansiaCore` + both app targets) passed an
> adversarially-verified multi-agent audit (analyst author + 2 adversarial verifiers), run 2026-06-26.**
> Build / tests / lint are **green**. The audit's **one blocker** — `API_BASE_URL` never reaching
> `Info.plist` → launch `fatalError` — was **already fixed + verified by launching the app in the
> simulator**, so it is NOT tracked here. The **two remaining findings** below are **low / latent severity
> and fully dormant** (no shipping screen exercises the affected code yet — only the auth spine + its tests
> reference it). **Logged ONLY — not for implementation now;** each is folded into its natural suggested-home
> ticket on the upcoming auth + booking waves, to be fixed via the normal workflow. Full audit:
> **`audits/AUDIT-2026-06-26-ios-phase0-foundation.md`** (F1/F2).
>
> | ID | Title | Size | Status | depends_on | Layers | sec | manual_step | Source / suggested home |
> |----|-------|------|--------|-----------|--------|-----|-------------|-------------------------|
> | **T-0331** | iOS `DeviceIdProvider` — persist own generated UUID (IDFV as seed only) + verify Keychain write `OSStatus` before caching | S | **draft** (deferred — not dispatched) | T-0300 (proposed) | ios | no | — | AUDIT-2026-06-26 **F1** → auth spine **T-0300** / partner-login **T-0303** |
> | **T-0332** | iOS booking-flow design checkpoint — send `Bearer` on dual-use `Order`/`Payment` endpoints when a session exists (withhold only for true guest) | S | **done ✅ / RESOLVED** — the dual-use Bearer carve-out shipped in **T-0313 Slice D** (`4e30aff`, `phase/ios-phase7`): `HeaderAdapter` attaches the Bearer on the 3 booking endpoints iff a token exists (signed-in→authed order; guest→tokenless; pure-anon→never; `CreatePaymentIntent`→always authed). AC1–AC4 satisfied, cross-referenced to ADR-0013 §D4.4 + header-parity §3; reviewer **APPROVE** + security **PASS** (no pure-anon Bearer leak; guest + partner non-regression). Recorded in `security/ios-customer-auth.md` | T-0313✓ | ios | no | — | AUDIT-2026-06-26 **F2** (DISPUTED→checkpoint) → booking-wizard **T-0313** (ADR-0013 §D4.4 / header-parity §3) |
>
> **F1 (T-0331):** `DeviceIdProvider.swift:42` persists IDFV as the `X-Device-Id` value (contract §2 says
> persist your **own** UUID, IDFV is "optional seed" only — "the single most breakable rule"), and
> `KeychainStore.write` (`KeychainTokenStore.swift:99-112`) discards the `SecItemAdd`/`SecItemUpdate`
> `OSStatus` so a pre-first-unlock write failure caches a value that diverges on the next launch — both
> break the `X-Device-Id` == `Device/Register` `deviceId` revoke invariant. **F2 (T-0332):** the customer
> `AnonymousAllowList` (`:28-39`) correctly no-Bearers the **guest**-booking surface, but those `Order`/
> `Payment` endpoints are **dual-use** — a signed-in customer's in-app booking would be sent with no Bearer
> (`HeaderAdapter.swift:29`) and the server (`CreateOrder` reads `GetUserId() ?? string.Empty`) would
> silently create a guest/empty-`UserId` order. **DISPUTED** in the audit → a booking-port **design
> checkpoint** (send Bearer iff a session exists; withhold only for true guests), attached as an AC on
> T-0313. **Both ids next-free after T-0330; both `draft`, dormant, awaiting their suggested-home wave.**
>
> --- (Wave-11 banner below) ---
>
> ## 🟦 WAVE 11 — Azure DEV deployment: Bicep IaC + region seam (ADR-0015/0017) — AGENT AUTHORING DONE; OWNER PROVISIONING PENDING (2026-06-23)
> **The agent-authorable half of Wave 11 is DONE, reviewed/verified, committed + pushed (`38a10375` on
> `feature/wave8-pre-ios-cleanup`).** The whole platform now has a clean-slate Bicep source-of-truth at
> `deploy/bicep/` (the iOS-pivot enabler — a stable dev API the Mac points at instead of running all five
> hosts + Functions + Postgres + Azurite locally). **6 tickets `done`** (T-0315, T-0316, T-0319, T-0321,
> T-0322, T-0330); **3 OWNER-provisioning tickets `blocked`** on the owner (T-0317, T-0318, T-0320). Full
> plan + the agent-vs-owner split + the OWNER PROVISIONING CHECKLIST: **`status/sprint-13.md`**.
>
> **What shipped (`38a10375`):** `main.bicep` (386 lines) + **10 modules** (appServicePlan B2 Linux,
> reusable appService, staticWebApp, functionApp container/ACR, acr, postgres B1ms, storage LRS, keyVault
> RBAC, roleAssignments, appInsights + Log Analytics) — **FIVE** API hosts incl.
> `api-cleansia-customer-mobile-weu-dev` (the host the old YAML omitted, the iOS customer app needs); **no
> secret value committed** (Key Vault refs + a `@secure()` Postgres password from a CI secret);
> least-priv MI (KV Secrets User / Storage data roles / AcrPull; CI = Secrets Officer); HTTPS-only +
> firewalled Postgres + mobile-host CORS closed; the **ADR-0017 region seam** (`region` param default
> `weu`, the `weu` token in every name, a region→location map). `weu.dev.bicepparam` + `weu.prod.bicepparam`
> (**prod authored, NOT deployed**). `deploy-dev.yml` rewritten (Bicep provision gate: what-if on PR /
> create on push; OIDC + the EF-migration bundle preserved; parallelized deploys behind the migrate edge;
> `matrix.region:[weu]`; `dev-weu` Environment; all five hosts). The **T-0330** region connection-string
> resolver (`IRegionConnectionStringResolver` + `RegionConnectionStringResolver`, the ADR-0017 data seam —
> one resolution point, behavior-preserving, **tenancy filter untouched**, no schema change).
>
> **Security gate PASSED on the module set (T-0315).** Reviewer-per-developer held — **except** the
> in-workflow StructuredOutput report tool failed (retry cap) on the **T-0319** and **T-0330** dev agents;
> that is a **REPORTING** failure, not a work failure (the work landed on disk), so the orchestrator
> **gated those two BY HAND** (read the resolver + CI; built `Cleansia.Config` 0 errors; secret-scanned;
> confirmed tenancy untouched + 5 hosts + OIDC/migration/provision gate). T-0319 + T-0330 are
> **verified-done** despite their in-workflow reviewer not running. Process lesson reinforced in
> `quality-gates.md` (3 occurrences across 2 waves now → standing rule + keep `buildEvidence`/`verifyEvidence` SHORT).
>
> | ID | Title | Size | Status | Phase | depends_on | Layers | sec | manual_step |
> |----|-------|------|--------|-------|-----------|--------|-----|-------------|
> | **T-0315** | Bicep skeleton + 10 reusable modules (`main.bicep`; five hosts incl. customer-mobile; KV-ref only; region-token names) | M (filed L→split) | **done ✅** `38a10375` | 0 FIRST | — | infra, backend, db | **yes** (PASS) | — |
> | **T-0316** | `weu.dev.bicepparam` + region/env-param wiring (five host names, dev SKUs, CORS, firewall) | M | **done ✅** `38a10375` (PASS-WITH-NOTES) | 0 | T-0315✓ | infra | no | — |
> | **T-0317** | **OWNER** — GitHub Environments (`dev-weu`/`prod-weu`) + flat-secret migration into per-env scopes | S | **blocked** (OWNER) | 1 | — | infra, docs | yes | **gh-environments + secret-migration** |
> | **T-0318** | **OWNER** — Key Vault values + RBAC grants + run/approve the first dev `az deployment` | M | **blocked** (OWNER) | 2 | T-0315✓, T-0316✓, T-0317 | infra | yes | **kv-secret-values + rbac-grants + az-deployment** |
> | **T-0319** | Rewrite `deploy-dev.yml` — Bicep-gated, OIDC + EF-bundle preserved, parallelized, 5 hosts, `dev-weu` | M | **done ✅** `38a10375` (**hand-gated** — SO report failed) | 2 | T-0315✓, T-0316✓ | infra, backend | no | — |
> | **T-0320** | Dev smoke + verification (5 APIs + SSR + 2 SPAs + Functions; queue→Functions live) — **needs the env up** | M | **blocked** (on T-0318 owner) | 2 | T-0318, T-0319✓ | infra, backend, qa | no | — |
> | **T-0321** | Catalog + living-doc edits (deployment/IaC pattern + tenancy=app/region=infra orthogonality) | S | **done ✅** `38a10375` | 2 | T-0315✓ | docs, architect | no | — |
> | **T-0322** | Author prod Bicep — `weu.prod.bicepparam` — **NOT DEPLOYED** | M | **done ✅** `38a10375` (authored, not deployed) | 3 | T-0315✓, T-0316✓ | infra, db | no | — |
> | **T-0330** | Connection-string resolver indirection (ADR-0017 data seam — one place, behavior-preserving, tenancy untouched) | S | **done ✅** `38a10375` (**hand-gated** — SO report failed) | 0 ∥ | — | backend | no | — |
>
> **Owner provisioning prerequisites (the path to a live dev env):** **T-0317** (create the `dev-weu`
> auto + `prod-weu` protected Environments, migrate the flat `*_DEV`/`*_PRO` secrets) → **T-0318**
> (populate the Key Vault values, grant CI = Secrets Officer + the MI roles, run the dev
> `az deployment group create`) → **T-0320** runs once the env is live (the smoke that confirms the five
> `api-cleansia-*-weu-dev` hosts are up — the iOS-pivot enabler). The agent **never** runs these — the
> exact ordered owner steps are on the OWNER PROVISIONING CHECKLIST (`status/sprint-13.md` §7 + the PM's
> checkpoint relay). **Q-INFRA-01/02/03 + Q-REGION-01/02/03 are all non-blocking for the dev provision**
> (tracked with their defaults in `questions/open.md`); prod (T-0322) is authored-not-deployed.
>
> --- (Wave-9 banner below) ---
>
> ## 🟣 WAVE 9 PLANNED — Admin Action Audit Log (ADR-0012, planned 2026-06-22) — backlog only, not yet dispatched
> **ADR-0012 (`adr/0012-admin-action-audit-log.md`) is `accepted`.** Owner approved building the **full**
> audit-log feature now (backend + admin UI + tests). **7 tickets filed (T-0282…T-0288).** Full plan —
> wave table, the 6-piece→5-ticket mapping, dependency-ordered batches, lanes (the Policy.cs/PolicyBuilder
> cluster = one writer), the owner manual-steps bundle B1, and the Q-AUDIT-01 default resolution:
> **`status/sprint-11.md`**. No code, no commits yet.
>
> **Q-AUDIT-01 RESOLVED (owner's "default now, ratify before prod"):** retention = **keep audit rows
> indefinitely, no auto-prune** (a window is a separate pre-prod call); PII = snapshots store **ids +
> changed fields only, never raw subject PII**; the GDPR-delete audit keeps **actor + scope + subject id**
> and **legitimately survives** the subject's erasure (legal-basis exception). Moved open→answered; the
> exact window + redaction list is a **pre-PROD readiness-checklist** ratification, not a blocker. Baked
> into T-0282 / T-0284 / T-0287.
>
> **Reviewer-per-developer on every ticket. Security gate on T-0283 / T-0284 / T-0285** (the compliance/
> authz seam). QA on all. No `L` tickets (the ADR's 6-piece outline → 5 feature tickets; the test bundle
> is test-first inside each per the ADR test list).
>
> | ID | Title | Size | Status | Batch | depends_on | Layers | sec | manual_step |
> |----|-------|------|--------|-------|-----------|--------|-----|-------------|
> | **T-0282** | `AdminActionAudit` entity + EF config (TenantId + global filter + 4 indexes) + migration | M | **ready** | **9A FIRST/ALONE** | — | db, backend | no | **ef-migration** |
> | **T-0283** | `AuditLogBehavior` (inner-to-UoW, atomic) + `IAuditContext` + `IAuditFailureSink` + `[AuditAction]` + generic capture | M | **ready** | 9B | **T-0282** | backend | **yes** | — |
> | **T-0284** | Sensitive-five before/after snapshots via `IAuditContext` (typed, pre-redacted, no raw subject PII) | M | **ready** | 9C | **T-0283** | backend | **yes** | — |
> | **T-0285** | `GetPagedAdminActionAudits` query (canonical `PagedData`) + new `AdminOnly` view policy (**owns Policy.cs/PolicyBuilder**) | M | **ready** | 9B | **T-0282** | backend | **yes** | **nswag-regen** |
> | **T-0286** | Admin `audit-log` feature lib (facade+signals+`cleansia-table`, filters, 5 locales, per-resource history) | M | **ready** (held on regen) | 9D | **T-0285** + regen | frontend | no | **nswag-regen** |
> | **T-0287** | Outbox retention-prune timer — Dispatched `OutboxMessage` + old `ProcessedMessage` rows (config-driven) | S | **ready** | independent | — | backend | no | — |
> | **T-0288** | Fix latent broken `order-management.component.spec.ts` (HttpClient inject — no test provider) | S | **ready** | independent | — | frontend | no | — |
>
> **Lanes/serialization:** **9A (T-0282) lands FIRST/ALONE** on the schema — the `AdminActionAudit` table
> is the spine; hold 9B/9C/9D until the owner confirms the migration. **9B = T-0283 ∥ T-0285** (disjoint
> files — behavior vs query+policy). **9C = T-0284** after T-0283 (serialize per sensitive-handler file,
> one writer each). **9D = T-0286** after T-0285 **and** the owner admin nswag-regen (facade authored
> test-first, held from `done` until regen + admin prod-build clean). **T-0285 is the SOLE writer of
> Policy.cs/PolicyBuilder.cs this wave** (both move together or `AssertComplete` fails boot). **T-0287 +
> T-0288 are independent** — fan out from day 1. **Owner manual-steps BUNDLE B1** (run once): the T-0282
> ef-migration (table + 4 indexes; PROD = `CONCURRENTLY`), then the T-0285 admin nswag-regen + all-three
> prod-builds → releases T-0286. **T-0281 (E2E sibling smokes) stays in Wave 8's close, NOT this wave.**
>
> --- (Wave-8 banner below) ---
>
> ## ✅ WAVE 8 CLOSED — Pre-iOS Cleanup (closed 2026-06-23) — 9/10 done; T-0280 + T-0279 carried
> **The E2E layer is decided + green; the pre-iOS contract surface is deduplicated + canonical.** The
> last two open items — **T-0271** (customer booking→checkout smoke) + **T-0281** (partner/admin sibling
> smokes) — are now **`done`** (real Playwright specs driving the actual UIs, network-stub seam @ the
> `/api/**` boundary, new `e2e-smoke` job in `frontend-ci.yml`; owner re-ran the customer smoke green
> `1 passed, 42.1s`). They join T-0272–T-0278 (8 earlier closures). **Honest caveats (non-blocking):**
> **T-0281's smokes were narrowed to login-and-land** — the partner job-accept transition (its AC1) and
> the admin seeded-row (its AC2) were not asserted under the empty-list stub → depth carried to **T-0293**,
> not silently passed. **T-0280** (FE comment cleanup, `ready`, deps satisfied) was **never dispatched** —
> it is a genuine open Wave-8 leftover and the **top of the next batch**. **T-0279** stays `blocked` on the
> separate IMP-3 regen. Per `status/sprint-10.md` §7 neither gates close. Full close summary +
> reconciled final states + the close-out follow-ups: **`status/sprint-10.md` 🏁 WAVE 8 CLOSE**.
>
> **Close-out follow-ups filed 2026-06-23 (the un-ticketed audit-log follow-ups + the E2E nit/depth):**
> **T-0289** (audit drill-in entry points, S, ready) · **T-0290** (single-row before/after audit diff +
> new endpoint, M, **sec**, **nswag-regen**, ready) · **T-0291** (consistency.md disputes-archetype note,
> XS, docs, ready) · **T-0292** (NG8102 dead `?? 0` cleanup, XS, ready) · **T-0293** (E2E partner
> accept-job + admin seeded-row depth, S, ready). Rows in the close-out table below this banner.
>
> **The audit-driven program (Waves 0–7) is closed + merged. Wave 8 is a discrete pre-iOS cleanup wave.**
> Scope = `audits/AUDIT-2026-06-22-pre-ios-cleanup.md` (13 findings) + owner points P1–P4. **10 tickets
> filed (T-0272…T-0281).** Full plan — wave table, dependency-ordered batches, lanes, the owner
> manual-steps bundle, the reconciliation notes: **`status/sprint-10.md`**.
>
> **Reconciliation headlines (honest):** `GetPagedDisputes` **REFUTED** as a paged offender (it is
> canonical A1–A8). `GetPagedMembershipPlans` **IS** an offender the audit MISSED but the tool flags →
> added to T-0273; net genuine paged offenders = **7 live** (not 6). P4's "add an A* rule" → **already
> satisfied** by the existing A1/A5 rules; the real gap was the offenders were never ticketed +
> `consistency-violations.md` was stale (the **meta-finding**, now recorded in F1b). Findings #12→T-0273,
> #13→T-0275 folded. Nothing else refuted.
>
> **Reviewer-per-developer on every ticket. Security gate on T-0272 only. QA on all.** No `L` tickets.
>
> | ID | Title | Size | Status | Batch | depends_on | Layers | sec | manual_step |
> |----|-------|------|--------|-------|-----------|--------|-----|-------------|
> | **T-0272** | Auth wire-contract shrink — `trustedDeviceToken` mobile-only + drop `RefreshToken` server fields (P1 + #9) | M | **done ✅** | **8A FIRST/ALONE** | — | architect, backend | **yes** | **nswag-regen** (all clients) ✓ |
> | **T-0273** | Canonicalize 7 bespoke paged queries → DataRangeRequest+Spec+Sort+PagedData (P4, #4/#5/#6/#12 + missed GetPagedMembershipPlans) | M | **done ✅** | 8B | — | backend | no | — |
> | **T-0274** | Dedup API error-key extractor across 8 facades → shared `@cleansia/services` helper (#1) | M | **done ✅** | 8B | — | frontend | no | — |
> | **T-0275** | Delete dead paged dups (GetAllEmployees, GetUserByEmail) + LOW drift cluster (#7/#8/#13) | S | **done ✅** | 8B | — | backend | no | — |
> | **T-0276** | Extract `SitewidePushFormFacade` → generated client + UnsubscribeControlDirective (#10) | S | **done ✅** | 8B | — | frontend | no | — |
> | **T-0277** | Hoist partner-app order formatters onto `:core` (#2) | S | **done ✅** | 8B (`:core` lane) | — | android | no | — |
> | **T-0278** | Hoist push-token cluster into `:core` behind `DeviceRegistrationClient` (#3) | M | **done ✅** | 8B (`:core` lane) | — | android | no | — |
> | **T-0279** | admin-pay-config.service → generated `AdminPayConfigClient` (#11) | S | **blocked** (IMP-3 regen) — CARRIED | — (not runnable) | — | frontend | no | **nswag-regen** (rides IMP-3) |
> | **T-0280** | Strip comment noise (FE auth services + audit pockets) (P2) | S | **ready** (OPEN — never dispatched; **top of next batch**) | 8C | **T-0272** ✓ + regen ✓ | frontend, backend | no | — |
> | **T-0281** | E2E sibling smokes — partner accept-job + admin login-and-land (P3) | M | **done ✅** (narrowed → T-0293) | 8C | **T-0271** ✓ | frontend, backend | no | — |
>
> **Lanes/serialization:** **8A (T-0272) landed FIRST/alone** — shrank the wire contract; the owner regen
> bundle B1 was confirmed. **8B fanned out concurrently** (T-0273–T-0278, all `done`; T-0277↔T-0278
> serialized on `:core`). **8C:** **T-0281 `done`** (on T-0271); **T-0280 stayed `ready` and was never
> dispatched** (its T-0272+regen deps are satisfied — it is the runnable Wave-8 leftover, top of the next
> batch). **T-0279 stays `blocked`** on the separate IMP-3 regen — does NOT gate Wave-8 close. **T-0271**
> (customer E2E smoke) is the foundation T-0281 reused; both `done` 2026-06-23.
>
> **Wave-8 CLOSE-OUT follow-ups — POST-ADMIN-REGEN BATCH CLOSED 2026-06-23 (2 more commits on
> `feature/wave8-pre-ios-cleanup`: `093ed944` FE + `7097d837` BE, pushed; orchestrator-verified on the
> combined tree). T-0290 is now FULLY `done` — BOTH halves end-to-end (the FE before/after diff view
> shipped against the regenerated `AdminAuditLogClient.getById`; `nx build cleansia-admin.app` prod clean,
> `nx test audit-log` 24/24, `nx build cleansia-partner.app` clean — **ADR-0012 follow-up (b) CLOSED**).
> T-0294 `done`. T-0295 BACKEND half `done`+verified (additive `AdminEmployeeDetail.UserId` + mapper +
> test 2/2); its FE half (employee-page drill-in) is HELD on a **2nd** owner admin nswag-regen for the new
> `UserId` field (mirrors how T-0286 / T-0290-FE were held). All six close-out follow-ups (T-0289…T-0293)
> `done`; of the two batch-close follow-ups T-0294 `done`, T-0295 `in_review`. A StructuredOutput-vs-on-disk
> process lesson recorded in `quality-gates.md` (a failed final-report call ≠ failed work — gate the tree
> by hand; keep buildEvidence concise).**
>
> | ID | Title | Size | Status | depends_on | Layers | sec | manual_step | Source |
> |----|-------|------|--------|-----------|--------|-----|-------------|--------|
> | **T-0289** | Per-detail-page drill-in → per-resource audit-history view (additive wiring of T-0286's shipped route) | S | **done ✅** `916014cb` | T-0286✓ | frontend | no | — | ADR-0012 follow-up (a) (T-0286 close) |
> | **T-0290** | Single-row before/after audit diff view + **new single-row backend endpoint** (snapshots off the PII-min list cut) | M | **done ✅** `093ed944` (BE `516e71c9` + FE `093ed944`; both halves, sec **PASS**) | T-0284✓, T-0285✓, T-0286✓ | backend, frontend | **yes** | **nswag-regen (admin) — DONE ✓** | ADR-0012 follow-up (b) (T-0286 close; ADR-0012 D4.1) — **CLOSED** |
> | **T-0291** | consistency.md note — prefer the disputes-management list archetype for new admin lists | XS | **done ✅** `916014cb` | — | docs | no | — | ADR-0012 follow-up (c) |
> | **T-0292** | Remove dead `?? 0` on non-nullable `extra.price` in `wizard-summary-step` (NG8102) | XS | **done ✅** `916014cb` | — | frontend | no | — | Wave-8 8C E2E dev-server boot |
> | **T-0293** | E2E depth — partner accept-job transition + admin seeded-row (the T-0281 narrowed slice) | S | **done ✅** `916014cb` | T-0281✓ | frontend, backend | no | — | T-0281 close (AC1/AC2 narrowed) |
>
> **Batch-close follow-ups (filed 2026-06-23):**
>
> | ID | Title | Size | Status | depends_on | Layers | sec | manual_step | Source |
> |----|-------|------|--------|-----------|--------|-----|-------------|--------|
> | **T-0294** | Remove now-unused `private readonly router` + `Router` import in `confirm-email.component.ts` (lint doesn't flag unused private members) | XS | **done ✅** `093ed944` | T-0280✓ | frontend | no | — | T-0280 latent smell (comment removal orphaned the injection) |
> | **T-0295** | Add `UserId` to `AdminEmployeeDetail` → enable the User-typed audit drill-in from the employee page | XS | **in_review** (BACKEND done+verified `7097d837`; **FE half HELD on a 2nd admin regen**) | T-0289✓ | backend, frontend | no | **nswag-regen (admin) — PENDING ON OWNER (2nd regen, for `AdminEmployeeDetail.UserId`)** | T-0289 deviation (employee page exposes `Employee.Id`, audit keys on `User.Id`) |
>
> **Parallel-shared-file lesson recorded** (`quality-gates.md` §"Serialize shared-file lanes …" + cross-ref
> in `routing.md` rule 3): in this batch T-0291 + T-0289 both edited `consistency.md` in parallel and
> T-0292's fix-agent ran `git restore consistency.md`, wiping T-0291's note (orchestrator restored it by
> hand). Future batches **serialize shared-file lanes** (`consistency.md`, `INDEX.md`, i18n bundles,
> `Policy.cs`/`PolicyBuilder.cs`) and **ban shared-file `git restore` in parallel agents**.
>
> ⚠️ **OWNER — admin nswag-regen PENDING (1 left):** the **T-0290** regen (the
> `AdminActionAuditDetailDto` + `GetAdminActionAuditById` endpoint) **LANDED** and released T-0290's FE
> half (now `done`). What remains is a **2nd admin nswag-regen for T-0295** — the new
> `AdminEmployeeDetail.UserId` field (added in the later backend commit `7097d837`, after the first regen)
> → it releases T-0295's FE half (the employee-page audit drill-in). After the regen run all three web
> prod-builds per quality-gates §after-regen. **Separately, `T-0279` still waits on the unrelated IMP-3
> admin regen** (a distinct, pre-existing owner item — not the same as the T-0295 regen).
>
> --- (Waves 0–7 close banner below, kept for traceability) ---
>
> ## 🏁 ALL WAVES (0–7) COMPLETE — the entire audit-driven program backlog is DONE (2026-06-21)
> **Every ticketed wave is closed.** Waves 0–6 + the T-0197 mobile slice + T-0264/T-0265 are merged to
> `master` (tip `b9e91cd8`, PR #81). **Wave 7 (Android consistency debt) is now COMPLETE** — 4 tickets
> done, committed + pushed (`9c1989e4`) on `feature/wave7-android-consistency`; PR to `master` is the
> owner's call (PM never merges). The **consistency audit (`audits/consistency-violations.md`) is
> essentially fully resolved** — all backend (F1–F8), frontend (F10–F12), and Android §E rules
> (E1/E2/E5/E6/E7) closed.
>
> **What remains across the WHOLE program — TWO open engineering follow-ups + standing owner items:**
> - **Engineering follow-ups (the open tickets):**
>   - **T-0270** (E2 residual — 3 post-Wave-5 one-shot-action VMs onto `ActionState`; S, `[android]`,
>     draft, sprint 8; behavior-preserving, non-blocking).
>   - **T-0271** (Phase-0 E2E smoke — customer **booking → checkout-intent** critical path in a real
>     browser, run in seeded CI; M, `[frontend]`+`[backend]`, **ready**, sprint 8). **Closes the no-E2E
>     gap** a retrospective surfaced: unit/integration/host tests cover the API seams but **nothing**
>     verified the rendered customer journey end-to-end — a dead CTA / broken route / wizard step that
>     won't advance is invisible to API-level tests. The Nx Playwright harness already exists but holds
>     only the scaffold `example.spec.ts`; this is the thin "decide the E2E layer early" smoke (one
>     spec + CI seed/boot wiring, no new framework), expandable later.
> - **Every other follow-up is `done`:** T-0263 (admin failed-PDF render — the owner's
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
> **Quality-foundation follow-up (filed 2026-06-21) — closes the no-E2E gap a retrospective surfaced:**
>
> | ID | Title | Size | Status | depends_on | Layers | sec | manual_step | Source |
> |----|-------|------|--------|-----------|--------|-----|-------------|--------|
> | **T-0271** | **Phase-0 E2E smoke** — customer **booking → checkout-intent** critical path in a real browser, run in seeded CI (one Playwright spec replacing the scaffold `example.spec.ts` + CI seed/boot wiring; reuses the existing Nx Playwright harness — no new framework). Thin "decide the E2E layer early" smoke; partner/admin/full-regression are explicit follow-ups. | M | **ready** (sprint 8) | — | frontend, backend | no | — | Workflow retrospective (no rendered-route/E2E coverage of the revenue path; harness = scaffold-only) |
>
> **T-0271 deferred-to-implementer seams:** the **Stripe handoff** (drive-to-handoff vs Stripe
> test-mode vs network-stub — recommend drive-to-handoff; **if** test-mode needs a CI secret that's an
> owner-only `manual_steps` flag to raise, not self-provision) and the **seed mechanism** (prefer the
> existing `sql-scripts/insert_seed_data.sql` or a test-only seed against a disposable CI Postgres).
> `manual_steps: []` unless the Stripe-test-mode-secret flag is raised.
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
