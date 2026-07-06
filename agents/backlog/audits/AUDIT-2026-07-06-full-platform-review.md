# AUDIT 2026-07-06 — Full-platform review (post-iOS-wave merge)

**Scope:** the entire codebase across backend / web / Android / iOS / infra, weighted toward the
`bc56d4d7..HEAD` merge batch (the iOS wave from the Mac + backend changes, ~176 files).
**Method:** 10 specialist finder agents (reviewer ×4, security ×2, optimizer ×3, architect ×1) →
cross-lane dedup → **adversarial verification of every critical/high finding** (verifiers instructed
to refute; default-refuted when uncertain) → synthesis. 20 agents, 0 errors.
**Exclusions:** T-0354..T-0367 (already ticketed 2026-07-02); the 2026-07-02 infra audit was not
re-run (delta only). Not examined: NgRx/SSR internals, Android partner orders/earnings/photos/push
Room internals beyond notifications, gitignored generated mobile clients, live EXPLAIN, running
mobile builds, Stripe/SendGrid client internals, loyalty/referral detail.

## Executive verdict

The merge lands healthy at its core: the iOS wave is high-quality and faithful to ADR-0022, the
backend delta is small and sound (single OTP resolve seam, correct tenant posture, 32/32 new tests),
and every hot security surface traced — webhooks, refresh rotation, ownership, rate limits — passes
the S1–S10 laws. **The dominant defect pattern is fixes stopped at first occurrence**: the int-enum
wire convention is guarded for mobile specs but breaks two live admin pages; the ISO alpha-2 fix
missed the Android partner consumer and all of iOS; the `%1$s` sweep fixed the customer catalog but
left the partner one corrupt in 5 locales. Money paths compute correctly server-side, but customers
see a double-counted express surcharge at checkout and admins cannot render the fiscal-failures or
membership-plans pages. Security is solid at the control layer with a cluster of S4/S6 leaks.
Performance debt is structural, not regressive — the missing persisted order status forces
whole-table correlated subqueries on every dashboard load and on the 288×/day fiscal sweep.

## Confirmed findings (adversarially verified — 9 confirmed/partial, 0 refuted)

| # | Sev | Verdict | Finding | Where | Effort |
|---|-----|---------|---------|-------|--------|
| 1 | high | CONFIRMED | Web wizard double-counts the express surcharge — quote already folds +20 %, the facade re-adds it (~+44 % shown at checkout) | `order-pricing.facade.ts:114` | S |
| 2 | high | CONFIRMED | Admin fiscal-failures page crashes at render — client `FiscalErrorKind` string enum vs int wire; `kind.toLowerCase()` throws | `fiscal-failures-list.component.ts:118` | S |
| 3 | high | CONFIRMED | Admin membership-plans list throws / edit form blanks — `BillingInterval` string enum vs int wire | `membership-plan-list.models.ts:44` | S |
| 4 | high | CONFIRMED | Partner iOS xcstrings carry Android-style `%1$s` in `invoice_card_paid_on/generated_on` — printf garbage in ALL 5 locales | `L10n+Earnings.swift:134` | S |
| 5 | high | CONFIRMED | Available-orders count scans the whole Orders table with per-row latest-status correlated subqueries on every partner dashboard load | `OrderSpecification.cs:111` | L |
| 6 | med | CONFIRMED | Fiscal reconciliation sweep (288×/day) seq-scans Orders — OR predicate defeats the (PaymentStatus, CreatedOn) index | `OrderRepository.cs:256` | M |
| 7 | med | PARTIAL | Android partner AddressSection still on the alpha-3 prefix hack — saved addresses render CountryNotServiced; SK/UA/PL saves blocked | `AddressSectionViewModel.kt:201` | S |
| 8 | med | PARTIAL | iOS customer `CountryResolver` compares alpha-2 geocoder codes to alpha-3 backend codes — never matches | `CountryResolver.swift:17` | M |
| 9 | med | PARTIAL | Partner address-save ISO prefix heuristic fails SVK/POL on BOTH iOS and Android | `AddressSectionViewModel.swift:122` | S |

## Top issues by real-world risk (synthesis)

1. **MONEY/UX** — express-surcharge double count at web checkout (render server `ExpressSurchargeAmount`; delete the client gross-up).
2. **MONEY-OPS** — admin blind to fiscal registration failures (enum crash); membership-plans unmanageable (same class). `[SwaggerEnumAsInt]` + regen + **extend the T-0370 spec-enum guard to the WEB clients**.
3. **AUTH** — `RegisterEmployee.cs:82-90` leaves an existing unconfirmed Customer's `Profile=Customer`; partner login then refuses the account forever. `UpgradeToEmployee()` exists with zero callers.
4. **AUTH/S6** — `SendEmailHandler.cs:41,50` logs the raw queue payload at Warning: email + live confirmation/reset code into App Insights — account-takeover material for log readers.
5. **PII/S4** — customers receive the cleaner's FULL name + personal phone via `AssignedEmployeeDto` (`OrderMappers.cs:159-165`); contradicts the first-name-only rule GetOrderPhotos enforces.
6. **DATA-LEAK** — partner Android notification feed (Room) survives sign-out; next account on the device sees the prior account's history and badge.
7. **TRANSPORT** — partner Android release APK allows cleartext HTTP (`usesCleartextTraffic="true"` in main manifest); customer app does it correctly.
8. **S6** — guest order-lookup emails + every caller's JWT email logged at Information (`RequestLoggingMiddleware.cs:78,179,195`).
9. **UX-BLOCKING** — ISO alpha-2/alpha-3 drift on Android-partner + all-iOS (findings 7–9 above): one canonical normalizer per platform Core.
10. **PERF** — persisted order status missing → dashboard whole-table scans + 25 sequential round trips + fiscal sweep seq-scans (denormalize `CurrentStatus` + index).

## Per-platform themes

- **Backend** — sound delta, structural debt. Anchor surface clean (single OTP Resolve seam, tenant posture correct, clamp invariant held, 32/32 tests). Standing code carries the S4/S6 leak cluster and the no-persisted-status query flaw; plus the RegisterEmployee upgrade brick and sweep N+1s (NewJobsDigest O(cleaners×orders), Task.Run geocode backfill).
- **Web** — the contract lie the mobile guard was built for is live here: backend writes enums as ints, the admin client carries two string enums (two broken pages), customer `MembershipStatus` stale-but-lucky. Reuse-gate erosion: hand-rolled `AdminPayConfigService` duplicating the generated client; a hand-edited generated client. Express-surcharge double count is the customer-facing money bug. i18n parity clean.
- **Android** — right laws, incomplete rollout, no pinning tests. Tri-state service-area law holds in :core and customer consumers, but the alpha-2 normalization stopped short of the partner AddressSectionViewModel, and both service-area fixes shipped testless. Hygiene: release cleartext, allowBackup PII, notification DB survives sign-out, ghost backup-rule paths, MVVM drift + hardcoded labels in AddressManagerScreen.
- **iOS** — high-quality wave whose gaps are parity, not craft. ADR-0022 restructure faithful, install seams guard-tested, wire changes test-pinned, L10n complete ×5 locales. But: partner catalog kept two `%1$s`-corrupted keys, the ISO normalizer was never ported (CountryResolver dead; partner prefix hack), iOS partner AddressSection **fails closed** on fetch failure (the UNKNOWN≠empty class Android just fixed), hard `[cz,sk]` searchBias filter, URLCache survives sign-out, blank `ApiError()` masks auth loss.
- **Infra** — tiny delta, CI-lane friction only: backend-ci push trigger runs full Testcontainers on every push with no paths filter/concurrency and gates nothing; ios-ci paths omit the spec dir its codegen consumes; openapi-generator unpinned while now wire-load-bearing; android-ci missed the push-gate fix.

## Verified strengths

- iOS wave craft: ADR-0022 restructure faithful; SlideToConfirm hoist left no dangling refs; wire changes test-pinned (BirthDateWireFormatTests, spec-enum guard); all used L10n keys present in all 5 locales.
- Mobile security controls pass on both platforms: Keychain `AfterFirstUnlockThisDeviceOnly`, EncryptedSharedPreferences AES-256, EXIF stripped from uploads, Bearer suppressed on anonymous paths, push unregister on logout.
- Deep-link/push authorization enforced server-side: a crafted push to another user's order is blocked by `OrderAccessService.CanBrowseOrderAsync` on both platforms.
- Hot auth/payment surfaces verified clean: webhook signature-then-tenant-override ordering, refresh rotation, OTP budget atomicity, rate-limit coverage, no mass assignment across the 5 hosts.
- `MobileSpecEnumGuardTests` is the right guard class — extend to web, don't discard.
- Perf fundamentals hold: outbox claim path, MessageKey unique index, auth indexes clean; iOS 5-min poller cancels correctly; repositories dedupe in-flight refreshes.
- Web i18n 5-locale key parity clean across all three apps; Android locale files complete in both apps.

## Unverified medium/low findings (58 — for PM triage)

Full structured list in the workflow output
(`…\tasks\wlws7ry69.output`, key `unverifiedMediumLow`); highlights beyond the ticketed set:
TolerantDateOnlyConverter local-midnight truncation; iOS BirthDate timezone boundary untested;
hand-rolled AdminPayConfigService; `as any` cluster in services-catalog; NumberFormatter-per-call in
iOS money rows; 8.4 MB mascot PNGs + WebP reload per body eval; dashboard ~25 sequential round trips;
GetAverageRating in-memory averaging; unindexed CustomerPhone scan in profile update; unindexed
StripePaymentIntentId webhook lookup; `LOWER(Name)` vs plain btree on ServiceCities; CLAUDE.md still
documents the 6-state order lifecycle (backend is 7-state with OnTheWay=3); BookingPolicy constants
duplicated in all three clients; web service-area advisory sticky-error never retries.

**Tickets filed from this audit: T-0380..T-0388** (see INDEX).
