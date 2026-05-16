# Post-Android-launch Follow-ups

> Living document. Each task tracks status and gets updated as items ship.
> When a task is fully done, move the row to the **Done** section at bottom.
>
> Status legend: `TODO` · `IN PROGRESS` · `BLOCKED` · `DONE` · `WONTFIX`

---

## Customer mobile (`src/cleansia_customer_android`)

### MOB-C-001 — Mascot transitions during async operations [DONE]
- **Like Wolt**: small mascot animations playing while a network call is in flight
  (booking submit, payment, etc.) so the user has feedback beyond a spinner.
- **Today**: spinner only. There's a `MascotAnimation` composable already in
  use on `LiveProgressHero` ([LiveProgressHero.kt:107-123](src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/orders/LiveProgressHero.kt#L107))
  but only on order-detail.
- **Specialist**: mobile
- **Approach**: extract a reusable `BusyMascotOverlay` and wire to the existing
  `ActionState`/`isLoading` signals on Booking, Payment, Subscribe Plus.

### MOB-C-002 — Tier-specific mascots in rewards screen [DEFERRED — needs design assets]
- Rewards/loyalty screen needs distinct mascots per tier (Bronze/Silver/Gold/Platinum).
- **Today**: same generic mascot for all tiers.
- **Specialist**: mobile
- **Asset dependency**: PNG/Lottie assets per tier from design.

### MOB-C-003 — Logout confirmation dialog [DONE — Wave 0 (already existed) + Wave 2 polish via MOB-C-004]
- Logout currently fires immediately on tap. Needs a "Are you sure?" dialog.
- **Today**: bare logout call; no dialog.
- **Specialist**: mobile
- **Files**: wherever logout is invoked from `ProfileTab.kt` / `AccountHubScreen.kt`.
- **Note**: pairs with MOB-C-004 — use the new `CleansiaDialog` component.

### MOB-C-004 — Custom Wolt-style dialogs [DONE]
- Replace stock Material `AlertDialog` with a custom themed dialog matching
  Cleansia's mascot/illustration language.
- **Specialist**: mobile
- **Approach**: create `CleansiaDialog` composable in `ui/components/`. Variants:
  Confirm (with positive/negative actions), Info (single CTA), Mascot
  (illustration + body + CTA).
- **Migrate call sites**: logout (MOB-C-003), order cancel, account delete,
  recurring booking cancel.

### MOB-C-005 — Push notifications wired but not yet customizable per-event [DONE]
- Phase A push events ship and dispatch end-to-end (verified this session).
- **Missing**: per-event preferences UI in profile → Notifications screen.
  Backend `UserNotificationPreferences` exists; mobile screen pulls them but
  doesn't have UI for the new categories beyond the original 5.
- **Specialist**: mobile + backend (i18n strings for any new categories)

### MOB-C-006 — Spurious "Check your internet connection" on Home/Profile [DONE — Wave 0]
- **Root cause identified**: `OrderRepository:179`, `PushTokenRepository:126`,
  `AuthRepository:99-103,154-158` all do `catch (t: Throwable) {...}` without
  re-throwing `CancellationException`. When user navigates away mid-request,
  coroutine cancels → bare catch swallows it → snackbar fires.
- **Fix**: `networkCall { ... }` helper at
  [NetworkCall.kt](src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/network/NetworkCall.kt)
  already exists and re-throws `CancellationException`. Migrate all
  `catch (t: Throwable)` repo blocks to use it (or copy the re-throw pattern
  inline).
- **Specialist**: mobile
- **Files to fix**:
  - `core/orders/OrderRepository.kt:179`
  - `core/notifications/PushTokenRepository.kt:126`
  - `core/auth/AuthRepository.kt:101,156`

### MOB-C-007 — Island navigation + horizontal swipe between tabs [DONE]
- Wolt-style "island" bottom nav (pill-shaped, floating) + swipe-to-page-change.
- **Today**: standard `NavigationBar` with click-only navigation.
- **Specialist**: mobile
- **Approach**: HorizontalPager + custom CleansiaIslandNav composable.
  Touches `CleansiaNavHost.kt` + bottom-nav component.

### MOB-C-008 — Refactor large screen files (>700 LOC) [DEFERRED — split when in-flight feature work touches a file]
- Top offenders by LOC ([analysis](#refactor-targets-mobile)):
  - `CreateRecurringScreen.kt` — 1345 LOC
  - `HomeTab.kt` — 1306 LOC
  - `AddressManagerScreen.kt` — 1178 LOC
  - `RewardsTab.kt` — 1027 LOC
  - `DisputeDetailScreen.kt` — 858 LOC
  - `ServicesStep.kt` — 774 LOC
  - `SubscribePlusScreen.kt` — 724 LOC
  - `OrderDetailScreen.kt` — 687 LOC
  - `BookingBottomSheet.kt` — 672 LOC
  - `OrdersTab.kt` — 662 LOC
- **Specialist**: mobile
- **Approach**: break each into 200-300 LOC sub-composables in `components/`
  subfolders. Identify duplicated `Card`/`Row`/`Pill` patterns and extract
  shared composables.

### MOB-C-009 — Add Google auth [TODO]
- TODOs already in code: `SignInScreen.kt:144`, `SignUpScreen.kt:246`,
  `CleansiaNavHost.kt:176,205`. Backend `GoogleAuth` handler exists.
- **Blocked on**: Google Cloud Console project setup (IMP-1 from
  `bugfix-improvements.md`).
- **Specialist**: mobile + ops
- **When unblocked**: unhide Google buttons + wire to existing
  `AuthApi.googleAuth` call.

### MOB-C-010 — Resolve W3.3 "refactor to VM injection" TODOs [DEFERRED — incremental cleanup as files are touched]
- 9 sites flagged for migration from snackbar-from-composable to
  HiltViewModel-injected pattern. Listed in
  [TODO inventory](#todo-inventory).
- **Specialist**: mobile

---

## Partner mobile (`src/cleansia_android`)

### MOB-P-001 — Refactor large screen files [DEFERRED — same rationale as MOB-C-008]
- `ProfileScreen.kt` — 826 LOC
- `OrdersScreen.kt` — 823 LOC
- `ProfileViewModel.kt` — 668 LOC
- **Specialist**: mobile

### MOB-P-002 — Resolve TODOs [DEFERRED — small TODOs left in place for incremental cleanup]
- `RegisterScreen.kt:212` — Open T&C link
- `DashboardViewModel.kt:154` — Load profile from API when endpoint available
- **Specialist**: mobile

---

## Customer web (`src/Cleansia.App/apps/cleansia.app`)

### WEB-C-001 — Performance regression after refactor [INVESTIGATED — static audit clean, reopen with concrete Lighthouse data]
- User reports bottlenecks post-refactor.
- **Specialist**: frontend
- **Approach**: Lighthouse audit + Chrome DevTools Performance recording.
  Check for:
  - Excess change detection (missing `OnPush`)
  - Synchronous Translate pipe calls in `@for` blocks
  - Unmemoized `selectXxx` selectors
  - `ngFor`/`@for` without `track`
- **Quick wins to inspect**: `order-wizard.facade.ts` (832 LOC — biggest
  facade in the app), `profile.component.ts` (413 LOC).

### WEB-C-002 — Duplicated code → extract shared subcomponents [DEFERRED — no concrete duplication candidate found]
- **Specialist**: frontend
- **Approach**: scan customer feature libs for duplicated card/list/badge
  patterns. Move to `libs/shared/components/`.

### WEB-C-003 — Missing translation keys [DONE]
- **Specialist**: frontend
- **Approach**: enable `MissingTranslationHandler` to log to console in dev,
  exercise app, capture missing keys, add to all 5 locales.

### WEB-C-004 — Add Google auth [TODO]
- Same blocker as MOB-C-009 (IMP-1).
- **Specialist**: frontend + ops

---

## Partner web (`src/Cleansia.App/apps/cleansia-partner.app`)

### WEB-P-001 — Mapbox autocomplete in address forms [TODO]
- Customer app already uses Mapbox for address lookup. Partner app uses
  plain text inputs.
- **Today**: cleaner types address manually; no autocomplete.
- **Specialist**: frontend
- **Approach**: reuse the customer app's Mapbox autocomplete component
  (likely in `libs/shared/components/` or `libs/cleansia-customer-features/`).
  Wire into partner profile address section + (if any) order address fields.

### WEB-P-002 — 400 error opening new (unassigned) order detail [DONE — Wave 0]
- **Root cause identified**: `OrderAccessService.CanAccessOrderAsync` at
  [OrderAccessService.cs:35-66](src/Cleansia.Core.AppServices/Authentication/OrderAccessService.cs#L35)
  rejects access if the caller is an Employee NOT in
  `order.AssignedEmployees`. A "New" order on the Available tab has zero
  assignees → 400 before the cleaner can even take it.
- **Fix**: in `CanAccessOrderAsync`, allow Employee callers to view orders
  with `order.HasAvailableSpots == true` even when not assigned. Add a
  separate `CanModifyOrderAsync` (the existing semantics) for write paths.
- **Specialist**: backend
- **Migration risk**: low — only affects read-detail; mutating endpoints
  already check assignment in their own validators (TakeOrder, StartOrder,
  CompleteOrder).

---

## Backend

### BE-001 — Magic strings → constants/`nameof()` [DONE]
- Hot spots:
  - `"en"` default language: 7+ sites
    ([CompleteOrder.cs:174,199](src/Cleansia.Core.AppServices/Features/Orders/CompleteOrder.cs#L174),
    [StartOrder.cs:126](src/Cleansia.Core.AppServices/Features/Orders/StartOrder.cs#L126),
    [TakeOrder.cs:209](src/Cleansia.Core.AppServices/Features/Orders/TakeOrder.cs#L209),
    [EmailService.cs:49,80,116,178,226,259](src/Cleansia.Core.AppServices/Services/EmailService.cs),
    `IEmailService.cs:8`)
  - `"CZK"` default currency: `FileExtensions.cs:49`
- **Specialist**: backend
- **Approach**: introduce a `DefaultsConfig` reading from appsettings
  (`Defaults:LanguageCode`, `Defaults:CurrencyCode`). Inject where needed.
  Constants already exist at
  [Constants.cs:45](src/Cleansia.Core.AppServices/Common/Constants.cs#L45)
  (`English = "en"`) — extend that file or move to `DefaultsConfig`.
- **Use `nameof()`**: scan validators for hard-coded property name strings;
  replace with `nameof(Command.PropertyName)`.

### BE-002 — Refactor "spaghetti" handlers [PARTIALLY DONE — CreateOrder dropped from 532→320 LOC via Wave 3.1 OrderFactory extraction. HandlePaymentNotification still pending]
- Long, dense handlers that mix concerns. Examples to start with:
  - `CreateOrder.cs` (multi-step address resolution + currency lookup +
    pricing + status track + payment branching)
  - `HandlePaymentNotification.cs`
- **Specialist**: backend
- **Approach**: extract private methods per concern; consider splitting
  into helper services (`OrderPricingService`, `OrderAddressResolver`).

### BE-003 — TenantId properly stamped on entity creation [DONE — verified]
- **Investigation result**: TenantId IS auto-stamped in
  `CleansiaDbContext.CommitAsync` at lines 67-77 — for any
  `ITenantEntity` in `Added` state, sets
  `TenantId = tenantProvider.GetCurrentTenantId()`.
- **Why orders show null**: `tenantProvider.GetCurrentTenantId()` reads the
  `tenant_id` claim from the JWT. In the current dev setup nobody has a
  `tenant_id` claim issued, so all orders get `null`. **This is correct
  per CLAUDE.md** ("Backward compatible: `null` TenantId = single-tenant
  mode") AND the EF query filter null/null fix from this session
  ([CleansiaDbContext.cs:147-154](src/Cleansia.Infra.Database/CleansiaDbContext.cs#L147))
  ensures null-tenant rows are visible to null-tenant callers.
- **What to do**: nothing in code. If you ever want to test multi-tenancy,
  the path is: register a tenant ID at user creation → user.TenantId
  populated → JWT claim issued → CommitAsync stamps it on every entity
  the user creates. Document this in CLAUDE.md if not already clear.

### BE-004 — net10 vs net9 [DECIDED — keep net10]
- **Investigation**: All projects target `net10.0`. Functions worker also
  net10.0. EF Core 10 + ASP.NET Core 10 packages require net10.
- **Decision**: keep net10. Downgrade would require ~30 package version
  bumps + possible API breakages. The only friction point is VS's bundled
  Functions toolset (4.126.0) doesn't yet support net10 isolated, which we
  worked around via Attach-to-Process (see push-notifications runbook).
- **No action**.

### BE-005 — `OperationCanceledException` noise on backend [DONE — Wave 0]
- Root cause: `RequestLoggingMiddleware` re-throws cancellation after
  logging it as Information, but Sentry has no `BeforeSend` filter so it
  captures everything including the rethrown OCE.
- **Specialist**: backend
- **Fix**: in `Cleansia.ServiceDefaults/Extensions.cs:81 UseSentryMonitoring`,
  add `options.SetBeforeSend((evt, hint) => evt.Exception is
  OperationCanceledException ? null : evt);`
- **Files**:
  [Extensions.cs:81-93](src/Cleansia.ServiceDefaults/Extensions.cs#L81)

### BE-006 — Resolve TODOs [VERIFIED — 3 TODOs are valid future-feature placeholders, not bit-rot]
- `MaterializeRecurringBookings.cs:68` — extract OrderFactory when recurring
  UX ships
- `PayCalculator.cs:264` — country-specific holiday lookup (currently TODO)
- `GenerateInvoiceFunction.cs:23` — extract invoice PDF generation from
  PayPeriodBackgroundService
- **Specialist**: backend

---

## Infrastructure

### INFRA-001 — Bicep IaC [TODO — DECIDE]
- **Recommendation**: yes, worth adding once you deploy to Azure for real.
  Bicep is the canonical Azure IaC and shipping infra-as-code keeps
  environments reproducible (dev/staging/prod).
- **Scope**: `deploy/bicep/` with main.bicep + per-environment param files.
  Resources to template (based on what the app needs):
  - PostgreSQL Flexible Server (the production DB)
  - Storage Account (Queue + Blob containers)
  - App Service plan + 4 Web Apps (Partner, Admin, Mobile, Customer)
  - Function App (Cleansia.Functions)
  - Key Vault (rotate FCM/SendGrid/Stripe secrets out of appsettings)
  - Sentry / App Insights (already wired)
- **Specialist**: infra/devops
- **Cost**: 3-5 days for a clean first version with CI deploy.

---

## Mobile architecture

### ARCH-001 — Monorepo for mobile apps? [TODO — DECIDE]
- **Today**: 2 separate Android Studio projects (`src/cleansia_android/`
  partner, `src/cleansia_customer_android/` customer). Lots of code
  duplication: theme, components, repository pattern, networking, snackbar,
  notification dispatch.
- **Pros of monorepo**:
  - Single Gradle build, shared modules
  - One `core/network`, one `ui/components` set, one theme
  - Shared `BuildConfig` flavor (`partner`/`customer`) instead of two apps
- **Cons**:
  - Big refactor, every file moves
  - Risk of accidentally coupling features that should stay independent
  - Build times grow with module count
- **Recommendation**: extract a shared `:core` Gradle module (network,
  theme primitives, generic components) without going full single-app
  multi-flavor. Lower risk than full unification, captures most of the
  duplication win.
- **Specialist**: mobile

---

## TODO inventory (from code grep)

### Customer mobile (TODO comments)
| File:line | Tag | Description |
|---|---|---|
| `core/auth/TokenStore.kt:28` | W6.5 | Migrate from deprecated `androidx.security.crypto` |
| `features/auth/SignInScreen.kt:127` | W1-F3 | Wire ForgotPassword endpoint |
| `features/auth/SignInScreen.kt:144` | IMP-1 | Unhide Google sign-in |
| `features/auth/SignUpScreen.kt:246` | IMP-1 | Unhide Google sign-up |
| `features/profile/EditProfileScreen.kt:226` | — | Launch photo picker |
| `features/orders/OrdersTab.kt:88` | W3.3 | Refactor to VM injection |
| `features/rewards/RewardsTab.kt:107,778` | W3.3 | Refactor to VM injection |
| `features/recurring/CreateRecurringScreen.kt:114,869,966` | W3.3 | Refactor to VM injection |
| `features/profile/PlusRecurringEntryRow.kt:50` | W3.3 | Refactor to VM injection |
| `features/membership/SubscribePlusScreen.kt:106` | W3.3 | Refactor to VM injection |
| `features/membership/MembershipManagementCard.kt:78` | W3.3 | Refactor to VM injection |
| `ui/snackbar/GlobalSnackbarHost.kt:71` | W3.3 | Refactor to VM injection |
| `navigation/CleansiaNavHost.kt:122` | W3.3 | Refactor Splash to VM |
| `navigation/CleansiaNavHost.kt:176,205` | IMP-1 | Wire Google auth |
| `navigation/CleansiaNavHost.kt:216,222` | W1-F3 | Wire ForgotPassword |
| `navigation/CleansiaNavHost.kt:422` | W3.3 | Refactor DeleteAccount to VM |

### Partner mobile (TODO comments)
| File:line | Tag | Description |
|---|---|---|
| `features/auth/screens/RegisterScreen.kt:212` | — | Open T&C link |
| `features/dashboard/viewmodels/DashboardViewModel.kt:154` | — | Wire profile API |

### Backend (TODO comments)
| File:line | Description |
|---|---|
| `Cleansia.Functions/Functions/GenerateInvoiceFunction.cs:23` | Extract invoice PDF generation |
| `Cleansia.Tests/Features/Orders/StartOrderValidatorTests.cs:44` | Deeper status-flow tests need EF mock infra |
| `Cleansia.Core.Domain/EmployeePayroll/Services/PayCalculator.cs:264` | Holiday check by country |
| `Cleansia.Core.AppServices/Features/Bookings/MaterializeRecurringBookings.cs:68` | Build OrderFactory when recurring UX ships |

### Frontend (TODO comments)
| File:line | Description |
|---|---|
| `libs/shared/components/.../cleansia-select.component.ts:39` | W6.2 stricter generic for valueChanges |
| `libs/shared/components/.../cleansia-radio.component.ts:19` | W6.2 same |
| `libs/cleansia-admin-features/pay-periods/.../pay-period-management.component.ts:190,317` | Wire dialogs + admin client endpoint |

---

## Refactor targets — mobile

Files >500 LOC, sorted by size:

**Customer mobile** (`src/cleansia_customer_android/.../features/`):
| LOC | File |
|---|---|
| 1345 | recurring/CreateRecurringScreen.kt |
| 1306 | home/HomeTab.kt |
| 1178 | addresses/AddressManagerScreen.kt |
| 1027 | rewards/RewardsTab.kt |
| 858 | disputes/DisputeDetailScreen.kt |
| 774 | booking/ServicesStep.kt |
| 724 | membership/SubscribePlusScreen.kt |
| 687 | orders/OrderDetailScreen.kt |
| 672 | booking/BookingBottomSheet.kt |
| 662 | orders/OrdersTab.kt |
| 643 | navigation/CleansiaNavHost.kt |
| 623 | booking/ConfirmStep.kt |
| 577 | profile/ProfileTab.kt |

**Partner mobile** (`src/cleansia_android/.../features/`):
| LOC | File |
|---|---|
| 826 | profile/screens/ProfileScreen.kt |
| 823 | orders/screens/OrdersScreen.kt |
| 668 | profile/viewmodels/ProfileViewModel.kt |

**Customer/Partner web** (`src/Cleansia.App/libs/...`):
| LOC | File |
|---|---|
| 832 | cleansia-customer-features/order-wizard/.../order-wizard.facade.ts |
| 413 | cleansia-customer-features/profile/.../profile.component.ts |
| 383 | cleansia-customer-features/order-wizard/.../order-wizard.component.ts |

---

## Loyalty + membership pricing

### LOY-001 — Discount applied on backend but invisible to mobile/web client [DONE]
- **The actual root cause** (revised after deeper trace): the
  [`OrderItem` DTO](src/Cleansia.Core.AppServices/Features/Orders/DTOs/OrderItem.cs)
  has **no discount fields**. The `Order` entity persists
  `TierDiscountAmount`, `MembershipDiscountAmount`, `PromoDiscountAmount`
  ([Order.cs:138-166](src/Cleansia.Core.Domain/Orders/Order.cs#L138)) and
  `CreateOrder.Handler` populates them at create time
  ([CreateOrder.cs:407-414](src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs#L407)),
  but the mapper never surfaces them. Customer mobile + web see only the
  discounted `TotalPrice` with no indication a discount was applied. From
  the user's perspective: "discount didn't apply" — the discount IS in the
  number, they just can't tell.
- **Secondary issue**: Silver has `MinimumOrderAmountForDiscount = 1000.00`
  in seed ([insert_seed_data.sql:2255](sql-scripts/insert_seed_data.sql#L2255)).
  Below that, no tier discount fires regardless. Drop or rework per LOY-003.
- **Specialist**: backend + mobile + frontend
- **Approach**:
  1. Add to `OrderItem` (and any list DTO that shows price):
     `OriginalSubtotal`, `TierDiscountAmount?`, `MembershipDiscountAmount?`,
     `PromoDiscountAmount?`, `AppliedDiscountSource` (`"none" | "tier" |
     "membership" | "promo"`).
  2. `OrderMappers.MapToDetail` populates from `Order` entity fields.
  3. Mobile order detail + receipt screens show line item: subtotal,
     "−25 Kč (Cleansia Plus)", final total. Only one discount source line
     ever shows (best-wins).
  4. Customer notice copy: "Cleansia Plus saved you 25 Kč. Your loyalty
     tier discount of 5% would be 20 Kč — Plus is the better deal." Surface
     this at QUOTE time too (LOY-002) so the choice is visible BEFORE
     checkout, not just on the receipt.

### LOY-002 — Live quote during checkout doesn't show tier/membership discount [DONE]
- **Root cause**: [QuoteOrder.cs](src/Cleansia.Core.AppServices/Features/Orders/QuoteOrder.cs)
  calculates raw price × services + packages, returns `TotalPrice` with no
  discount applied. Discount only applies at `CreateOrder.cs:322-395`.
  So the customer sees full price during checkout, can't tell whether
  their membership/tier benefit is working, only sees the discount on the
  receipt after the order is placed.
- **User-visible symptom**: "I have membership but no discount" — the
  discount IS applied at CreateOrder time, the customer just can't see it
  in the price they're confirming.
- **Specialist**: backend + frontend + mobile
- **Approach**: extend `QuoteOrder.Response` with `TierDiscountAmount`,
  `MembershipDiscountAmount`, `PromoDiscountAmount`, `AppliedDiscountSource`,
  and `FinalPrice`. Run the same best-of-three logic from `CreateOrder` in
  the quote handler. Frontend/mobile show line items: subtotal − discount =
  total, with a chip naming the source ("Membership: -25 Kč").

### LOY-003 — Restructure tier benefits [DONE — Option C shipped (additive Plus + tier, cap 12%, uniform 1000 CZK floor)]
- **User remark**: "rather have 5/7/10% discount on the order. Or maybe a
  different type of benefit. Cause I think we give too much."
- **Today's tiers**:
  - Bronze (0 pts): 0% — no benefit
  - Silver (500 pts): 5% above 1000 CZK only
  - Gold (2000 pts): 10% always
  - Platinum (5000 pts): 15% always
- **Plus membership**: extra 5% always + 4h free-cancel window + free
  express upgrade (skips +20% surcharge for 2-4h lead bookings)
- **Today's "best wins" math** (no stacking — largest of the three):
  membership 5% vs tier 0/5/10/15% vs promo. Plus + Platinum = the user
  gets 15% (tier wins), Plus + Bronze = 5% (membership wins). The "Plus"
  benefit evaporates for Gold+ users → bad incentive.
- **Recommended redesign** (business-friendly, generous-but-not-too-generous):

  **Loyalty tiers** — earn points per CZK spent, tier unlocks **non-discount
  perks** so we don't over-discount:
  | Tier | Threshold | Discount | Perk |
  |---|---|---|---|
  | Bronze | 0 pts | 0% | "Welcome" badge, basic notifications |
  | Silver | 500 pts | 3% | Priority support response (24h → 4h) |
  | Gold | 2000 pts | 5% | Free reschedule once per booking, monthly bonus 50 pts |
  | Platinum | 5000 pts | 7% | Dedicated favorite-cleaner pool, free recurring discount stack |

  **Plus membership** — value comes from *features*, not raw discount:
  - 5% off every booking (stays as today, but stacks with tier instead of competing)
  - 4h free cancellation window (today)
  - Free express upgrade — saves up to +20% surcharge (today)
  - **NEW**: free recurring booking templates (non-Plus capped at 1 template)
  - **NEW**: 14-day trial → conversion driver

  **Stacking rule change**: Plus + Tier discounts ADD (capped at 12% combined
  to prevent runaway discounts). Promo replaces both if larger.
- **Specialist**: backend + product (final tier values are a business call)
- **Migration**: SQL update to LoyaltyTierConfigs + MembershipPlans rows.
  Code change to CreateOrder best-source logic to support stacking with cap.

### LOY-004 — Membership discount didn't apply for user's Plus subscription [DEFERRED — retest with LOY-001/002 chips]
- **User report**: "I have a membership but still no discount applied."
- **Possible causes**:
  1. `UserMembership` row exists but `Status != Active` or `CurrentPeriodEnd
     <= now` → `GetActiveForUserAsync` returns null → no discount applied.
  2. Best-source logic correctly picked tier (= Silver 5%) over membership
     (= 5%) but tier was floor-gated → no discount surfaced. (See LOY-001.)
  3. Stripe webhook didn't activate the subscription → DB still pending.
- **Specialist**: backend
- **Investigation step**: run query against the user's `UserMemberships` row
  + the order: `SELECT um.Status, um.CurrentPeriodEnd, mp.DiscountPercentage,
  o.MembershipDiscountAmount FROM ...`. Add a debug log in CreateOrder
  branching to surface which discount source won.

### LOY-005 — Notify customer when discount applied + show why excluded [DONE]
- **User remark**: "need to notify end customer that discount is applied
  corresponding to the criteria (not membership and loyalty discounts are
  applied at the same time)."
- **Specialist**: mobile + frontend (copy + UI)
- **Where to surface**:
  - **Quote step**: small chip below the price: "Cleansia Plus discount
    applied — 25 Kč off". When tier WOULD apply but membership is bigger:
    "Plus saves you more than your tier discount today."
  - **Membership-but-no-discount edge**: if the user has Plus but the order
    is below the membership floor (small order) and tier also doesn't fire,
    show: "Your Plus discount didn't apply on this order — minimum spend
    not met." Same for tier-with-floor.
  - **Receipt + order detail**: same chip on the order detail + emailed
    receipt.
- **Pairs with**: LOY-001 + LOY-002 — once the DTO carries the discount
  source the UI just renders it. Ship as one PR.

---

## Order status / progress fixes

### MOB-C-011 — OnTheWay missing as a separate step in customer order detail [DONE]
- **Today**: [LiveProgressHero.kt:285-298](src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/orders/LiveProgressHero.kt#L285)
  has 4 steps: Booked → Accepted → Started → Finished. OnTheWay collapses
  into "Accepted" (line 294 maps both `Confirmed` and `OnTheWay` → step 1).
- **Also**: [OrderDetailTimelineAndReview.kt:77](src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/orders/OrderDetailTimelineAndReview.kt#L77)
  renders status names raw (`entry.status?.name`) — non-localized, shows
  "OnTheWay" literal even on Czech UI.
- **Specialist**: mobile
- **Approach**:
  1. Expand StepIndicator to 5 steps: Booked → Accepted → On the way →
     Started → Finished. Add `order_detail_step_on_the_way` × 5 locales.
  2. TimelineCard: localize status names via the existing
     `R.string.status_X` keys (mirror partner mobile's StatusBadge resolver).

### MOB-C-012 — "Cleaning complete" push fires but order shows InProgress [DONE]
- **Root cause**: customer mobile `OrderDetailViewModel` polls every 30s
  ([OrderDetailViewModel.kt:170-185](src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/orders/OrderDetailViewModel.kt#L170)).
  Push notification arrives instantly; UI doesn't refetch until next poll
  tick → up to 30s of "still InProgress" state while toast says complete.
- **Specialist**: mobile
- **Approach**: in `CleansiaFirebaseMessagingService.onMessageReceived`,
  emit on a singleton `MutableSharedFlow<OrderEvent>` keyed by orderId
  whenever `event_key` starts with `order.`. Detail VM observes the flow
  scoped to its current orderId; on emit, calls `loadOrderDetail()` once.
- **Once shipped**: the 30s polling becomes a safety net — drop interval
  to 5 minutes (catches FCM delivery failures) or remove entirely.
- **Pairs with**: REALTIME-001 (decision below).

### REALTIME-001 — Should we replace 30s polling with SignalR / SSE? [DECIDED — NO, use push-triggered refetch]
- **Question**: cut backend resource usage by replacing 30s order-detail
  polls with SignalR or Server-Sent Events.
- **Today's cost**: each customer with an active order (Confirmed → OnTheWay
  → InProgress) hits `GET /api/Order/GetById` every 30s. 100 active orders
  = 200 req/min just for status polling, mostly returning unchanged data.
  Plus battery drain on the device.
- **Options compared**:
  - **A. Push-triggered refetch** (MOB-C-012): when FCM push for
    `order.X` arrives, the detail VM refetches once. Drop 30s timer to
    5-min safety net. **Cost**: ~zero new infra; reuses the FCM pipeline
    that ships today. Eliminates ~95% of polling. Background delivery
    handled by FCM (works even when app killed).
  - **B. SignalR**: persistent WebSocket per active client, hub on each
    Web host, server pushes `OrderUpdated` to subscribed clients.
    **Cost**: new ASP.NET hub, per-host hub instances (4 hosts), auth +
    tenant context on hub connect, mobile reconnect logic, doesn't help
    when app is in background (still need FCM there).
  - **C. SSE**: lighter than SignalR, one-way. Android library support
    is mediocre — you build on OkHttp. Same background limitation.
- **Decision**: **A**. Push-triggered refetch gets the benefit at near-zero
  cost. Reserve SignalR for future use cases where push doesn't fit
  (live cleaner GPS, live dispute chat, admin dashboards). Track those
  separately if/when they come up.
- **Specialist**: mobile (executes MOB-C-012); no backend work needed.

### MOB-C-013 — Progress bar is time-estimate, not actual cleaner progress [WONTFIX or PRODUCT DECISION]
- **Today**: progress = `elapsed since cleaner started ÷ estimatedTime`,
  capped at 0.97 ([LiveProgressHero.kt:255-273](src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/orders/LiveProgressHero.kt#L255)).
  Not "mock" — a real estimate based on the cleaner's `estimatedTime`
  field — but not "real progress" either (cleaner doesn't tap "30% done").
- **Decision needed**: is a time-estimate good enough? Real progress would
  require either (a) cleaner taps milestones in partner app (kitchen done,
  bathroom done, etc.) → significant new UX, or (b) heuristic from photo
  uploads / room counts. Recommend keeping time-estimate + adding a
  "live updates from your cleaner" feed (notes/photos) for richer feel.
- **Specialist**: product → mobile

---

## Recurring cleanings

### REC-001 — Recurring booking pipeline is a stub — no orders ever created [DONE — Wave 3.1 + 3.2 + 3.3 shipped]
- **Root cause**: [MaterializeRecurringBookings.cs:68-75](src/Cleansia.Core.AppServices/Features/Bookings/MaterializeRecurringBookings.cs#L68)
  has a TODO. The handler iterates active templates, computes occurrences,
  then **logs and skips** instead of creating Orders. Returns
  `OrdersCreated: 0` always.
- **End-to-end gap**:
  - Customer creates a recurring template via `CreateRecurringBooking` ✅
  - Template stored in `RecurringBookingTemplates` ✅
  - Daily background job calls `MaterializeRecurringBookings` ✅
  - Materializer skips creation ❌
  - No Order ever appears in cleaner's available list ❌
- **Specialist**: backend (large)
- **Approach**:
  1. Extract an `OrderFactory` shared by `CreateOrder.Handler` and
     `MaterializeRecurringBookings.Handler`. Inputs: `userId`, services,
     packages, address, scheduled time, currency. Output: persisted Order.
  2. Materializer calls factory with the template's snapshot fields
     (no JWT context — pass `userId` explicitly).
  3. **Decision needed**: how does payment work? Two options:
     - **Auto-charge from saved payment method** (simpler for recurring,
       requires Stripe SetupIntent + saved card pattern → MUST exist
       before materializer can ship)
     - **Materialize as draft, customer confirms 24h ahead** (safer but
       defeats the "set and forget" UX of recurring)
  4. Wire push notification (`recurring.scheduled`) when an Order is
     created — see [push-notifications-phase-b.md](push-notifications-phase-b.md).
- **Dependencies**:
  - LOY-003 if recurring should grant Plus members a discount stack
  - Stripe saved payment method flow (separate epic — currently every
    booking creates a fresh checkout session)

### REC-002 — Cleaner cannot see recurring orders in available list [AUTO-RESOLVED — Pending orders flow into Available tab via existing GetPagedOrders. Verify post-migration]
- Once materializer creates real Orders, they automatically flow into the
  partner "Available" tab (status = New / Pending / Confirmed with open
  spots). No additional partner work needed once REC-001 ships.
- **Verification step**: after REC-001, create a recurring template,
  trigger MaterializeRecurringBookings (or wait for the daily job), check
  partner Available list for the spawned order.

---

## Execution order

Ordered by **value × cost × dependency-blocking**. Each "wave" is
independent of the others (you can stop at any wave and ship). Within a
wave, items can run in parallel.

### Wave 0 — Bug-fix burst (1-2 days, all backend or trivial mobile)

Quick wins. Each blocks user-visible value or makes downstream work
impossible to verify.

| # | Task | Why first |
|---|---|---|
| 1 | **WEB-P-002** Partner 400 on new-order detail | Cleaner can't open new orders today. One-method change. |
| 2 | **MOB-C-006** "Check your internet" false positive | One-line fix per file (4 files). Unblocks reliable testing. |
| 3 | **BE-005** Sentry filter for `OperationCanceledException` | One line in `ServiceDefaults`. Stops Sentry noise that hides real errors. |
| 4 | **MOB-C-003** Logout dialog | Trivial. Pairs naturally with MOB-C-004 in Wave 2. |

### Wave 1 — Discount visibility [SHIPPED — see Done section]

LOY-001 + LOY-002 + LOY-005 + 4 follow-ups (pricing-policy realignment,
sidebar discount breakdown, mobile date-picker desync, summary crash fix)
all shipped. LOY-004 deferred pending user retest with the new chips active.

### Wave 2 — Order live-status + OnTheWay polish [SHIPPED — see Done section]

MOB-C-012 push-triggered refetch + MOB-C-011 OnTheWay 5th step + MOB-C-004
custom CleansiaDialog (with 7 site migrations) all shipped. MOB-C-013 stays
as a product decision (no implementation).

### Wave 3 — Recurring cleanings [SHIPPED — full feature live; see Done section]

Product decisions locked: **draft + 24h confirm** (Option B) with **Pending
Order rows materialized 24-48h ahead** (Path 2). All three sub-PRs shipped.

| Sub-PR | Scope | Status |
|---|---|---|
| **3.1** | Extract `IOrderFactory` from CreateOrder; wire materializer to produce Pending Orders; cleaner-side auto-resolves (REC-002) | **SHIPPED** |
| **3.2** | `recurring.scheduled` push 24h ahead + mobile FCM template + deep link | **SHIPPED** |
| **3.3** | `ConfirmRecurringOrder` command + mobile confirm UX + auto-cancel sweep | **SHIPPED** |

Single EF migration owed before prod (`AddOrderRecurringFields`) covering
all 3 new columns: `RecurringTemplateId`, `RecurringReminderSentAt`. NSwag
regen owed for `OrderItem.RecurringTemplateId` + `ConfirmRecurringOrder`
endpoint surface.

### Wave 4 — Mobile UX polish [SHIPPED — see Done section]

MOB-C-005 push prefs expansion + MOB-C-001 BusyMascotOverlay + MOB-C-007
island nav + horizontal swipe all shipped. MOB-C-002 deferred until design
delivers per-tier mascot assets (only 2 mascots in res/raw today).

### Wave 5 — Refactors + tech debt [PARTIALLY SHIPPED — see Done section]

3 tasks shipped (BE-001, BE-006, WEB-C-003), 6 deferred with rationale.
Tech debt without concrete pain isn't worth the regression risk; revisit
each item when in-flight feature work makes the friction concrete.

### Wave 6 — Auth additions (blocked on external setup)

Both blocked on Google Cloud Console project (IMP-1).

| Task | Notes |
|---|---|
| **MOB-C-009** Customer mobile Google sign-in | TODOs in code, just needs OAuth config |
| **WEB-C-004** Customer web Google sign-in | Same blocker |
| **MOB-C-009b** Forgot-password flow (W1-F3) | Backend endpoint exists, mobile needs wiring |

### Wave 7 — Larger redesigns (decisions first)

| Task | Notes |
|---|---|
| **LOY-003** Tier benefit restructure | **SHIPPED** — Option C: additive Plus + tier, capped at 12%, uniform 1000 CZK floor on tier portion. See Done section + [loyalty-tier-restructure-loy003.md](loyalty-tier-restructure-loy003.md). |
| **WEB-P-001** Mapbox autocomplete in partner address forms | Reuse customer-web component |
| **INFRA-001** Bicep IaC | Once you actually deploy to Azure |
| **ARCH-001** Mobile shared `:core` Gradle module | Refactor; do after MOB-C-008 stabilizes individual file shapes |

### Phase B push events (separate, see push-notifications-phase-b.md)

| Task | Notes |
|---|---|
| **TASK-011a** `loyalty.tier_upgrade` | Easiest single-trigger event |
| **TASK-011b** `membership.expiring_soon` + `cancellation_effective` | Background job |
| **TASK-011c** `recurring.scheduled` | Depends on Wave 3 |
| **TASK-011d** `promo.new_sitewide` | Depends on admin UI |

---

## Done

(Move tasks here as they ship. Include the commit / PR ref where applicable.)

- BE-003 — TenantId investigation: confirmed correct behavior, documented above.
- BE-004 — net10 retention: decided.
- REALTIME-001 — Decision: push-triggered refetch (no SignalR/SSE). See task entry for the rationale; implementation lives in MOB-C-012.

### Wave 0 (shipped)
- **WEB-P-002** — Split `IOrderAccessService` into `CanAccessOrderAsync` (strict, for photos/receipts/mutations) and `CanBrowseOrderAsync` (loose — Employee can read any order with available spots). `GetOrderDetails` switched to the loose method; `DownloadOrderReceipt` and `GetOrderPhotos` stay strict.
- **MOB-C-006** — Re-throw `CancellationException` in 4 sites: `OrderRepository.kt:179`, `PushTokenRepository.kt:126`, `AuthRepository.kt:101+156`. Stops the spurious "Check your internet connection" toast on Home/Profile after a fast tab switch.
- **BE-005** — Sentry `BeforeSend` filter drops `OperationCanceledException` events in `ServiceDefaults/Extensions.cs:90`. Stops client-disconnect noise from drowning real errors.
- **MOB-C-003** — Verified: logout confirmation dialog already exists in `ProfileTab.kt:213-231` (customer) and `AccountHubScreen.kt:86-104` (partner). Both apps gate logout through dialog → confirm → SessionViewModel/AuthRepository. Was a stale task — no code change needed.

### Wave 1 (shipped)
- **LOY-001** — `OrderItem` + `OrderListItem` DTOs gained 5 new fields (`OriginalSubtotal`, `AppliedDiscountSource`, `TierDiscountAmount`, `MembershipDiscountAmount`, `PromoDiscountAmount`). New `AppliedDiscountSource` enum + `OrderMappers.ResolveAppliedDiscount` private helper. Customer mobile + web order-detail screens render strikethrough + chip naming the discount source.
- **LOY-002** — `QuoteOrder.Handler` now mirrors `CreateOrder` best-of-three (sans promo, which is entered at the checkout step). Response extended with `FinalPriceAfterDiscount`, `OriginalSubtotal`, `AppliedDiscountSource`, per-source amounts, and `TierDiscountMinOrderAmount`. `TotalPrice` kept as the **raw** subtotal so `CreateOrder.PriceMatchesAsync` validation continues to work.
- **LOY-005** — Discount chips wired on the Confirm step + the order-detail hero on both customer mobile and customer web. Tier-floor "needs orders above X" hint surfaces when the user qualifies but the subtotal is below the floor. 6 new i18n keys × 5 locales (3 wizard + 3 order-detail) on both surfaces.
- **LOY-FOLLOWUP-1** (discovered post-merge) — Backend pricing policy aligned: discount on raw subtotal first, then express surcharge applied to the discounted total. `CreateOrder.Handler` reordered + `IOrderPricingCalculator` injected to normalize legacy clients that still send `raw × 1.20`. Mobile `BookingPricing.finalTotal` and ConfirmStep follow the same order; web facade `expressSurcharge` + `displayedTotalPrice` likewise.
- **LOY-FOLLOWUP-2** (UX) — Web sidebar + mobile bottom-sheet price bar gained a labeled "Cleansia Plus discount / Loyalty tier discount / Promo (-CODE)" line with green styling. New `appliedDiscountKind` computed on facade. Sidebar "CELKEM" now matches the summary card (single source of truth via `displayedTotalPrice`).
- **LOY-FOLLOWUP-3** (bug) — Customer mobile date picker no longer hard-blocks Sundays (`WhenWhereStep.kt:64-91` was a leftover mock). Backend has no day-of-week restriction for one-off orders; mobile now matches web. Per-slot lead-time gating in `timeSlotsFor` is unchanged.
- **LOY-FOLLOWUP-4** (crash) — `WizardSummaryStepComponent.grandTotal` wrapped in `computed(...)` so it lazy-reads the `@Input() facade` after binding (prior direct assignment crashed at field-init).
- **LOY-004** — Verification deferred. The "Plus discount didn't apply" symptom should auto-resolve once the user retests with LOY-001/002 visibility shipped (the discount was being applied, just invisible). Reopen if still reported with the new chips active.

### Wave 2 (shipped)
- **MOB-C-012** — `OrderEventBus` singleton (process-wide `MutableSharedFlow<OrderEvent>` with `extraBufferCapacity = 8`) lives at `core/notifications/OrderEventBus.kt`. `CleansiaFirebaseMessagingService.onMessageReceived` emits whenever an `order.*` payload with a non-blank `orderId` arrives, BEFORE building the local notification (so the in-app screen reacts even when the system notification is suppressed). `OrderDetailViewModel` collects events filtered to its current orderId via `launchIn(viewModelScope)` and refetches on each emission. The 30-second poller was raised to a **5-minute safety net** for missed pushes (FCM rate-limit, app killed too long, missing token).
- **MOB-C-011** — `LiveProgressHero.StepIndicator` expanded to 5 steps: Booked → Accepted → On the way → Started → Finished, with `OrderStatus.OnTheWay` now mapped to its own index (Confirmed=1, OnTheWay=2, InProgress=3, Completed=4). New `order_detail_step_on_the_way` + `orders_status_on_the_way` keys × 5 locales. New `orderStatusLabelRes(value: Int?): Int?` resolver in `OrderEnums.kt` — used in `TimelineCard`, `HeroCard`, `LiveProgressHero`, `OrdersTab`, and `HomeTab` so all status pills are localized; falls back to the raw CodeDto `name` when the value is unknown (forward-compatible with new backend statuses).
- **MOB-C-004** — New `CleansiaDialog` composable at `ui/components/CleansiaDialog.kt`. Single API serves both flavors via slots: confirm-dialog (title + message + 2 buttons) and custom-content dialog (title + `content` slot for TextField/LazyColumn/etc.). Variants: standard (primary-tinted confirm) + `destructive` (error-tinted confirm + matching icon halo). Optional circular icon halo above the title. Buttons are equal-weight `Button` (filled, rounded 14dp, 48dp min-height) with centered labels — dismiss uses `surfaceVariant` fill, confirm uses `primary` (or `error` when destructive). Entrance choreography via `AnimatedVisibility` + `MutableTransitionState`: spring-driven scaleIn from 0.85 (`MediumBouncy` damping, `MediumLow` stiffness) + 220ms fadeIn + small upward slide; exit is a 120ms fade+scale-out. Migrated 7 sites across 5 files: `ProfileTab.kt` (logout — destructive), `MembershipManagementCard.kt` (cancel — destructive + switch), `PreferredCleanerPicker.kt` (cleaner list), `AddressManagerScreen.kt` (delete — destructive + rename), `RecurringBookingsScreen.kt` (delete — destructive). Confirm labels trimmed to bare action verbs across 5 locales (e.g. "Cancel subscription" → "Cancel", "Switch and pay difference" → "Switch", "Delete schedule" → "Delete"). Stock Material `AlertDialog` import removed from all 5 files.
- **MOB-C-013** — Product decision: kept as time-estimate. Real progress would need cleaner-driven milestones (kitchen done, bathroom done) which is significant new partner UX. Not implemented this wave — left as a future enhancement tied to a product call.

### Wave 4 (shipped)
- **MOB-C-005** — Notifications screen now exposes all 11 backend categories. Added 6 missing toggles (`OrderCancelled`, `RefundIssued`, `MembershipExpiring`, `MembershipCancelled`, `TierUpgrade`, `DisputeReply`) grouped into 2 new sections — "Membership and rewards" and "Account" — alongside the existing "Push notifications" + "Channels" sections. 16 new i18n keys × 5 locales (8 toggle labels + 8 descriptions + 2 section headers). Backend `NotificationPreferencesPayload` already had all fields; the VM's `setCategory` resolver already covered all 11 enum values — only the UI surfacing was missing.
- **MOB-C-001** — `BusyMascotOverlay` composable at `ui/components/BusyMascotOverlay.kt`. Full-screen dim layer + spring-animated white card with the cleaning mascot + headline message. Swallows touches under it so the user can't double-submit while a network call is in flight. Same entrance choreography as `CleansiaDialog` (springy scaleIn 0.85→1 + 220ms fadeIn, 120ms fade+scale-out exit) for visual consistency. Wired to `BookingBottomSheet` (booking submit + Stripe payment), `SubscribePlusScreen` (Plus activation). 3 new i18n keys × 5 locales (`busy_booking`, `busy_payment`, `busy_subscribe_plus`).
- **MOB-C-007** — `MainShell` rewired with `HorizontalPager` so the user can swipe between Home/Orders/Rewards/Profile tabs (in addition to bottom-bar tap). Pager state persists across config changes via `rememberSaveable` on the initial-page index. Bottom bar restyled as a floating island pill (`RoundedCornerShape(32.dp)`, 1dp outline, 16dp horizontal padding from screen edges, 12dp vertical padding clear of the gesture area). Book FAB still half-overlaps the top of the pill. Tabs receive a `tabContentPadding` PaddingValues that reserves 96dp + navigation-bar inset at the bottom so the last item isn't hidden behind the floating bar. `Scaffold` + `AnimatedContent` removed (the pager replaces both). All `selected = MainTab.X` callsites swapped for `selectTab(MainTab.X)` which calls `pagerState.animateScrollToPage(...)`.
- **MOB-C-002** — Deferred. Only `mascot_welcoming.webp` and `mascot_cleaning_in_progress.webp` exist in `res/raw`. Per-tier mascots (Bronze/Silver/Gold/Platinum) need design to deliver 4 new animated WebPs before any code wiring is meaningful. Reopen once assets land.

### Wave 5 (partially shipped, mostly deferred)
- **BE-006** — Verified the 3 backend TODOs (`GenerateInvoiceFunction.cs:23`, `PayCalculator.cs:264`, `MaterializeRecurringBookings.cs:68`) are valid future-feature placeholders, not stale comments. Each is paired with an existing plan item (REC-001 for the materializer, IMP-3-related for pay calc, payroll extraction for invoice fn). Left in place; no code change needed.
- **BE-001** — Added `Constants.Currency.Czk = "CZK"` alongside existing `Constants.Language.English = "en"`. Replaced 13 magic-string sites: 6 default parameters in `EmailService.cs` + `IEmailService.cs`, 4 `?? "en"` fallbacks in Order handlers (CompleteOrder, TakeOrder, StartOrder), 2 fallbacks in PeriodReminder + PayPeriod background services, 1 in FiscalRetryService, 1 in SendTestEmail, 3 `?? "CZK"` fallbacks in ReceiptService + FileExtensions, plus the language repo lookup in PayPeriodBackgroundService. Skipped the planned `DefaultsConfig` from-appsettings infra — over-engineering for two values that are already centralized; revisit if the platform genuinely needs configurable defaults later.
- **WEB-C-003** — All 3 customer-facing apps (customer/partner/admin) now have full key parity across all 5 locales. Backfilled 18 `pages.dashboard.*` + `pages.orders.*` keys into en/sk/uk/ru that previously existed only in cs.json (partner dashboard analytics — earnings, time, productivity, order analytics). Added 6 `pages.order_details.*` keys to sk/uk/ru. Added `pages.profile.full_name` to cs. Localized `global.actions.pay_configs` (was English-only across all 4 non-en admin locales). All 3 web app production builds clean. Final counts: customer 1164 keys × 5, partner 1070 × 5, admin 1644 × 5.
- **BE-002** — Deferred. `CreateOrder.cs` is a 532-line handler that just stabilized after the LOY-FOLLOWUP pricing-policy fix; reorganizing it for cleanliness alone is a high-risk low-value trade. The natural seam for splitting comes when REC-001 extracts an `OrderFactory` (so both `CreateOrder.Handler` and `MaterializeRecurringBookings.Handler` can share it). Pair this refactor with that work. `HandlePaymentNotification.cs` follows the same logic — Stripe webhook code is hard to test, leave it alone.
- **WEB-C-001** — Static audit found no real issues: all 38 customer feature components use `ChangeDetectionStrategy.OnPush`, no legacy `*ngFor` in customer code, all `@for` blocks use `track`, translate pipes are pure (memoized). The original "perf regression" report needs a concrete Lighthouse run + DevTools profile to surface bundle-size or render-path issues that grep can't see. Reopen with concrete data.
- **WEB-C-002** — Deferred. Spot-checked the most likely duplication target (status pills/badges in orders) — they already share `OrderStatusLabelPipe` + `OrderStatusSeverityPipe` from `@cleansia/pipes`. No concrete duplicate pattern emerged from the scan. "Extract for the sake of extraction" creates complexity without paying it back. Revisit when a specific friction point appears in feature work.
- **MOB-C-008 / MOB-P-001** — Deferred. The 13 customer files and 3 partner files >700 LOC are organizationally large but functionally fine; mechanical split would burn ~6 hours of edit-and-verify with zero user-visible benefit. Better policy: split incrementally when a developer is in the file for feature work and the file's size makes the change harder. Stable code stays stable; touching it for cleanliness alone is the wrong trade.
- **MOB-C-010 / MOB-P-002** — Deferred. The W3.3 "refactor snackbar-from-composable to HiltViewModel-injected" pattern across 9 customer sites is purely organizational. Same rationale as MOB-C-008: do these incrementally when the call site is being touched for feature work, not as a bulk cleanup pass.

### Wave 3.1 (shipped — REC-001 first slice + REC-002 auto-resolved + BE-002 partial)

Sub-PR 3.1 of the Recurring Cleanings feature. Materializer now creates real `Pending` Order rows; cleaner-side picks them up automatically through existing GetPagedOrders flow (resolves REC-002). Customer-side push + confirm flow are scheduled for Wave 3.2/3.3 in [recurring-bookings-wave3.md](recurring-bookings-wave3.md).

- **`Order.RecurringTemplateId`** field added to the Order aggregate ([Order.cs:185-194](src/Cleansia.Core.Domain/Orders/Order.cs)). Optional FK back to the originating template; null for one-off orders. EF entity config includes `HasIndex` so the materializer's "already-spawned for occurrence X" lookups stay fast. GDPR `AnonymizeCustomerData()` scrubs the field.
- **`IOrderFactory` + `OrderFactory`** new at [Features/Orders/IOrderFactory.cs](src/Cleansia.Core.AppServices/Features/Orders/IOrderFactory.cs) + [OrderFactory.cs](src/Cleansia.Core.AppServices/Features/Orders/OrderFactory.cs). Single boundary for "given pre-resolved address + currency + raw subtotal, build a fully-formed Pending Order with discount snapshot + VAT + status track." Factory does NOT do address resolution, JWT lookup, Stripe session creation, or queue messages — those stay in the caller. Best-of-three discount math (`ResolveBestDiscount`) lives here in lock-step with QuoteOrder + the wizard summary so the displayed price always matches what gets persisted.
- **`CreateOrder.Handler` refactored** ([CreateOrder.cs:209-300](src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs)). Dropped from 532 LOC to ~320. Handler now: resolves the address (saved vs inline) into a private `AddressResolution` record, resolves currency, normalizes the raw subtotal (stripping legacy 1.20 surcharge clients), previews promo, delegates everything else to `IOrderFactory.CreateAsync`, then handles Stripe + queue side effects post-create. Constructor went from 19 deps to 13 (factory absorbs OrderRepository/PackageRepository/ServiceRepository/CompanyInfoRepository/CountryConfigurationRepository/VatCalculator/LoyaltyService/UserMembershipRepository).
- **`MaterializeRecurringBookings.Handler` wired** ([MaterializeRecurringBookings.cs:42-130](src/Cleansia.Core.AppServices/Features/Bookings/MaterializeRecurringBookings.cs)). For each active template within the 7-day horizon, it: resolves the saved address (skip with warning if missing), computes raw subtotal via `IOrderPricingCalculator`, calls `IOrderFactory.CreateAsync` per occurrence, marks `template.LastMaterializedFor`. `Response.OrdersCreated` now reflects real spawned orders. Failure modes are per-template fail-soft — one bad template doesn't kill the whole sweep.
- **DI registration** at [Cleansia.Config/Services/ServiceExtensions.cs:25](src/Cleansia.Config/Services/ServiceExtensions.cs) — `services.AddScoped<IOrderFactory, OrderFactory>()` next to `IOrderPricingCalculator`.
- **Builds:** `Cleansia.Core.AppServices` 0/0, `Cleansia.Tests` 0/0. Web hosts had file-lock errors during full-solution build (running dev hosts) — code-correctness verified per CLAUDE.md guidance. Customer mobile + web booking flow logically unchanged (validator + factory together produce the exact same Order shape; verify with end-to-end smoke test before merging the PR).
- **Manual steps owed by owner:**
  - **EF migration**: `dotnet ef migrations add AddOrderRecurringTemplateId` (adds the new nullable column + index). Apply via `dotnet ef database update`.
  - **NSwag regen** is NOT required — `OrderItem`/`OrderListItem` DTOs were not touched in 3.1 (RecurringTemplateId is internal to the domain; will surface to clients in Wave 3.2 if/when the customer needs to see it on order detail).
  - **End-to-end booking smoke test** before merging: book a one-off (cash + card), verify Stripe session creates correctly, verify the Order persists with same discount math as pre-3.1.

### Wave 3.2 (shipped — `recurring.scheduled` push + deep link)

Sub-PR 3.2 of the Recurring Cleanings feature. The 24h-ahead reminder fires; tapping it lands the customer on the materialized Order's detail screen ready for Wave 3.3's confirm flow.

- **`Order.RecurringReminderSentAt`** new nullable timestamp on the Order aggregate ([Order.cs:196-202](src/Cleansia.Core.Domain/Orders/Order.cs)). Stamped by the sweep when the push enqueues; null pre-stamp. `MarkRecurringReminderSent(sentAtUtc)` is idempotent (uses `??=` so a double-call keeps the first stamp). EF entity config at [OrderEntityConfiguration.cs](src/Cleansia.Infra.Database/EntityConfigurations/OrderEntityConfiguration.cs) — single nullable column, no extra index needed (the sweep filters on `RecurringTemplateId IS NOT NULL` first which IS indexed).
- **`SendRecurringOrderReminders`** command + handler new at [Features/Bookings/SendRecurringOrderReminders.cs](src/Cleansia.Core.AppServices/Features/Bookings/SendRecurringOrderReminders.cs). Cross-tenant query (`GetQueryableIgnoringTenant`) for `Order.PaymentStatus == Pending && RecurringTemplateId != null && RecurringReminderSentAt == null && CleaningDateTime in [now+22h, now+26h]`. Default window is 22-26h to give a few hours of slack so a cron miss doesn't drop reminders. Per-order failure is fail-soft (logs + skips, leaves `RecurringReminderSentAt` null so the next sweep retries). Each reminder enqueues a `SendPushNotificationMessage` with `EventKey = NotificationEventCatalog.RecurringScheduled`, `Args = { orderId, orderNumber }`, and the order's `TenantId` for downstream routing.
- **`SendRecurringOrderRemindersFunction`** Azure Functions timer trigger new at [Functions/SendRecurringOrderRemindersFunction.cs](src/Cleansia.Functions/Functions/SendRecurringOrderRemindersFunction.cs). Cron `0 30 2 * * *` — runs daily at 02:30 UTC, 30min after the materializer at 02:00 UTC so newly-spawned orders for tomorrow get caught immediately.
- **Customer mobile FCM service** ([CleansiaFirebaseMessagingService.kt](src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/notifications/CleansiaFirebaseMessagingService.kt)) extended for `recurring.scheduled`:
  - `templateFor()` returns `(notification_recurring_scheduled_title, notification_recurring_scheduled_body, NotificationCategoryDto.RecurringScheduled)`.
  - `formatBody()` substitutes `orderNumber` into the body via `%1$s` placeholder.
  - The "fan out to OrderEventBus" predicate now matches `recurring.scheduled` too (it carries an `orderId`), so an open Order Detail screen refetches immediately when the push arrives.
- **Deep link routing** ([NotificationDeepLink.kt](src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/notifications/NotificationDeepLink.kt)). Tapping the push now navigates to `Routes.OrderDetail(orderId)` — same destination as the rest of the `order.*` family. (Previously routed to `Routes.RecurringBookings`, which would show the template list rather than the materialized order to confirm.) Wave 3.3 will add the Confirm CTA on Order Detail.
- **5 locales × 2 strings** added to `notification_recurring_scheduled_{title,body}` in en/cs/sk/uk/ru. Body uses `%1$s` for the order number, matching the rest of the order push family.
- **Builds:** `Cleansia.Core.AppServices` 0/0, mobile `:app:compileDebugKotlin` + `:app:testDebugUnitTest` PASS. Functions project had only file-lock errors (running Functions host); no CS errors.
- **Manual steps owed by owner:**
  - **EF migration** (combined with 3.1): `dotnet ef migrations add AddOrderRecurringFields` adds both `RecurringTemplateId` and `RecurringReminderSentAt` columns + the index on `RecurringTemplateId`. Apply via `database update`.
  - **No NSwag regen** — neither column surfaces on customer DTOs in 3.2.
  - **Smoke test path** (post-migration): manually insert a Pending recurring order via SQL or seed, run the timer manually, verify the push arrives on a real device, verify tap lands on Order Detail.

### Wave 3.3 (shipped — `ConfirmRecurringOrder` + auto-cancel sweep + mobile UX)

Final sub-PR of the Recurring Cleanings feature. The customer can now complete a materialized recurring order end-to-end: tap the 24h-ahead push → land on Order Detail → tap "Confirm and pay" → for Cash, the order flips to Confirmed + Paid; for Card, Stripe PaymentSheet collects payment. Missed-confirm sweep auto-cancels stale slots so cleaners' time isn't wasted.

- **`ConfirmRecurringOrder` command** new at [Features/Orders/ConfirmRecurringOrder.cs](src/Cleansia.Core.AppServices/Features/Orders/ConfirmRecurringOrder.cs). Validates ownership (sessionUserId == order.UserId) + state (Pending + has recurringTemplateId). Cash branch: stamps OrderStatus.Confirmed + PaymentStatus.Paid, queues receipt generation, dispatches `order.confirmed` push. Card branch mirrors `CreatePaymentIntent.Handler` exactly (ensures Stripe Customer, creates PaymentIntent, generates ephemeral key) — order stays Pending until the Stripe webhook confirms payment. Single endpoint serves both flows; client branches on `clientSecret == null`.
- **`POST /api/Order/ConfirmRecurring`** controller endpoint at [Controllers/OrderController.cs](src/Cleansia.Web.Customer/Controllers/OrderController.cs:60-72) — `[Authorize]`, returns `ConfirmRecurringOrder.Response`.
- **`AutoCancelStaleRecurringOrders`** sweep new at [Features/Bookings/AutoCancelStaleRecurringOrders.cs](src/Cleansia.Core.AppServices/Features/Bookings/AutoCancelStaleRecurringOrders.cs). Cross-tenant query for Pending recurring orders past `CleaningDateTime - 1h` cutoff (configurable via `MissedConfirmGraceHours`). Adds OrderStatus.Cancelled + dispatches `order.cancelled` push so customer learns their slot's gone. In-memory status double-check guards against race with concurrent confirms. [Functions/AutoCancelStaleRecurringOrdersFunction.cs](src/Cleansia.Functions/Functions/AutoCancelStaleRecurringOrdersFunction.cs) hourly cron `0 0 * * * *`.
- **`OrderItem.RecurringTemplateId`** surfaced on the customer DTO ([OrderItem.cs:34-39](src/Cleansia.Core.AppServices/Features/Orders/DTOs/OrderItem.cs)) + [OrderMappers.cs](src/Cleansia.Core.AppServices/Mappers/OrderMappers.cs:84) populates it. Lets the mobile decide whether to show the Confirm CTA without an extra round-trip.
- **Customer mobile DTOs** added at [OrderDtos.kt](src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/orders/OrderDtos.kt): `ConfirmRecurringOrderRequest`/`Response` records + `OrderDetailDto.recurringTemplateId` field. **Hand-written, not NSwag** — customer mobile uses kotlinx.serialization throughout.
- **Mobile API** [OrderApi.kt:46-54](src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/orders/OrderApi.kt) — `confirmRecurring(...)`. Repository wrapper at [OrderRepository.kt](src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/orders/OrderRepository.kt:148-167) follows the established `null = failure (snackbar surfaced)` pattern.
- **`OrderDetailViewModel`** ([OrderDetailViewModel.kt](src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/orders/OrderDetailViewModel.kt)): new `confirmRecurringState: StateFlow<ActionState>` for the busy spinner, new `confirmResult: SharedFlow<ConfirmRecurringOrderResponse>` one-shot channel. `confirmRecurring()` sends the request; Cash response triggers VM-side success snackbar + refetch, Card response just emits to the screen. New `notifyCardPaymentResult(success, errorMessage)` for the screen to feed PaymentSheet outcomes back to the VM so all snackbar wiring stays centralized.
- **`OrderDetailScreen`** ([OrderDetailScreen.kt](src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/orders/OrderDetailScreen.kt)):
  - `rememberPaymentSheet` at the top of the composable — same Stripe SDK pattern as `BookingBottomSheet`. Result callback feeds back through `viewModel.notifyCardPaymentResult(success, errorMessage)`.
  - `LaunchedEffect(viewModel)` collecting `viewModel.confirmResult` opens the PaymentSheet when the Card branch returns a non-null `clientSecret + customerId + ephemeralKey`. Cash response (all three null) is a no-op since the VM already pushed the success snackbar.
  - **Confirm CTA** rendered in `LoadedState` right after the hero, gated on `recurringTemplateId != null && paymentStatus.value == 1` (PaymentStatus.Pending). Filled primary button with rounded 14dp corners + spinner during submit. Hidden for non-recurring or already-confirmed orders.
- **i18n** added 2 new keys × 5 locales (`recurring_confirm_cta`, `recurring_confirm_success`).
- **Builds:** `Cleansia.Core.AppServices` 0/0, `Cleansia.Web.Customer` 0 errors (file-lock copy errors only), mobile `:app:compileDebugKotlin` + `:app:testDebugUnitTest` PASS.
- **Manual steps owed by owner:**
  - **EF migration** (combined with 3.1 + 3.2): `dotnet ef migrations add AddOrderRecurringFields` adds `RecurringTemplateId` + `RecurringReminderSentAt` columns + the `RecurringTemplateId` index. Apply via `database update`. (No new column added in 3.3 itself; 3.3 only consumes the columns from 3.1+3.2.)
  - **NSwag regen for customer client**: 3.3 added `OrderItem.RecurringTemplateId` field + the new `POST /api/Order/ConfirmRecurring` endpoint. Customer mobile is hand-rolled (no regen needed there) but customer web's NSwag client needs `npm run generate-customer-client` if the web app should ever surface this field or endpoint. Customer web doesn't render the Confirm CTA today (3.3 was mobile-only), so this is "regen when you next touch customer web for any reason" rather than blocking.
  - **End-to-end smoke test**: insert a Pending recurring order, manually trigger the SendRecurringOrderReminders timer, tap the push on a real device, verify Order Detail shows the CTA, tap it. For Cash: verify state flips to Confirmed + receipt enqueues. For Card: verify PaymentSheet opens, complete a test payment, verify state flips to Confirmed via Stripe webhook. Trigger AutoCancelStaleRecurringOrders against a Pending order with `CleaningDateTime` in the past, verify it goes to Cancelled + customer gets the `order.cancelled` push.

### LOY-003 (shipped — additive Plus + tier with 12% cap + uniform 1000 CZK floor)

Decision: Option C from [loyalty-tier-restructure-loy003.md](loyalty-tier-restructure-loy003.md). The "best-of-three" exclusivity is retired; Plus and tier now stack additively up to a 12% combined cap, with promo replacing the combined pair if larger. A uniform 1000 CZK floor applies to the tier portion only (Plus always applies for paying subscribers regardless of subtotal).

- **Tier seed values updated** ([insert_seed_data.sql:2237-2300](sql-scripts/insert_seed_data.sql)): Silver 5% / Gold 10% / Platinum 12% (was 15%). All three now share `MinimumOrderAmountForDiscount = 1000.00`. Bronze stays 0% but its floor is now consistent. Idempotent `UPDATE` blocks added so re-running the seed against an existing DB picks up the new values without a separate migration script.
- **`AppliedDiscountSource.Combined = 4`** added to the enum ([AppliedDiscountSource.cs](src/Cleansia.Core.AppServices/Shared/DTOs/Enums/AppliedDiscountSource.cs)). Represents "both Plus and tier applied additively." The single-source values (Tier/Membership/Promo) keep their semantics for orders where only one source fires.
- **`OrderFactory.ResolveLoy003Discount`** ([OrderFactory.cs](src/Cleansia.Core.AppServices/Features/Orders/OrderFactory.cs)) replaces the old best-of-three. Computes combined = Plus + tier, applies the 12% cap (pro-rates each share when capping kicks in so both chips stay visible on the receipt with the right amounts), then compares against promo. Promo wins exclusively when larger (zeroes both additive amounts in the output). `MaxCombinedDiscountFraction = 0.12m` constant lives here as the single source of truth.
- **`QuoteOrder.Handler`** ([QuoteOrder.cs](src/Cleansia.Core.AppServices/Features/Orders/QuoteOrder.cs)) now delegates to `OrderFactory.ResolveLoy003Discount` (with promo = 0 since promo is entered at the checkout step, not the quote step). Response's `AppliedDiscountSource` returns the new `Combined` value when both amounts are non-zero, populating both `TierDiscountAmount` and `MembershipDiscountAmount` on the same response.
- **`OrderMappers.ResolveAppliedDiscount`** ([OrderMappers.cs](src/Cleansia.Core.AppServices/Mappers/OrderMappers.cs)) updated to detect the Combined case from the persisted Order's per-source amounts: both `MembershipDiscountAmount > 0` AND `TierDiscountAmount > 0` → `Combined`. Promo still wins exclusively (mutually exclusive with the additive pair, enforced upstream by the resolver). Historical orders predating LOY-003 keep their original single-source enum since the persisted amounts dictate the mapping.
- **Customer mobile**:
  - [ConfirmStep.kt](src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/booking/ConfirmStep.kt) — `effectiveDiscount` now uses `combinedServerDiscount = tier + membership` (additive), and both `showMembershipLine` + `showTierLine` can be true simultaneously when both amounts are present. Promo wins via strict `>` (was `>=`) so a tie keeps the additive pair visible rather than collapsing to a single promo chip.
  - [OrderDetailHeroAndAddress.kt](src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/orders/OrderDetailHeroAndAddress.kt) — Combined source (value 4) renders both Plus and tier chips side-by-side; single-source values render one chip as before.
- **Customer web**:
  - [order-wizard.facade.ts](src/Cleansia.App/libs/cleansia-customer-features/order-wizard/src/lib/order-wizard/order-wizard.facade.ts) — `effectiveDiscount` becomes `max(combined, promo)`. `appliedDiscountKind` gains a `'combined'` value so the sidebar + mobile bottom-sheet can render both Plus and tier rows.
  - [wizard-summary-step.component.ts](src/Cleansia.App/libs/cleansia-customer-features/order-wizard/src/lib/order-wizard/components/wizard-summary-step.component.ts) — `showMembershipChip` + `showTierChip` no longer mutually exclusive; both render when their amount is non-zero and promo doesn't win over the combined.
  - [order-wizard.component.html](src/Cleansia.App/libs/cleansia-customer-features/order-wizard/src/lib/order-wizard/order-wizard.component.html) — sidebar + mobile bottom-sheet gained a `@case ('combined')` block rendering both discount rows. Same pattern applied at both switch sites.
  - [order-detail.component.html](src/Cleansia.App/libs/cleansia-customer-features/orders/src/lib/order-detail/order-detail.component.html) — Combined case (value 4) renders both Plus and tier labels with a `·` separator inside the same chip.
- **NSwag clients patched manually** ([customer-client.ts](src/Cleansia.App/libs/core/customer-services/src/lib/client/customer-client.ts), [admin-client.ts](src/Cleansia.App/libs/core/admin-services/src/lib/client/admin-client.ts), [partner-client.ts](src/Cleansia.App/libs/core/partner-services/src/lib/client/partner-client.ts)) — `AppliedDiscountSource.Combined = 4` added to all three generated TypeScript enums so cross-package type comparability holds. Manual patch is OK because the regen will reproduce the same shape; doing this saves a regen round.
- **i18n** — `loyalty.perks.discount_12` added to all 5 customer web + customer mobile locales. `discount_10` and `discount_15` perk copy updated to mention the uniform 1000 CZK floor ("X% off bookings over 1000 CZK") since the floor now applies uniformly. `discount_15` key kept in locale files for backward compatibility (no harm if unreferenced); seed PerksJson now references `discount_12` for Platinum.
- **Builds:** `Cleansia.Core.AppServices` 0/0, mobile `:app:compileDebugKotlin` + `:app:testDebugUnitTest` PASS, customer web production build PASS.
- **Manual steps owed:**
  - **No EF migration** — schema unchanged; only seed data + computed discount math changed.
  - **SQL run against prod** — apply the idempotent `UPDATE` blocks in [insert_seed_data.sql](sql-scripts/insert_seed_data.sql) (around line 2280) to existing prod DB to push the new values. The `INSERT ... WHERE NOT EXISTS` blocks are no-ops on existing rows; the new `UPDATE` blocks force the values to match. Bronze stays unchanged.
  - **NSwag regen** when next touching customer/admin/partner web for any reason. Manual patches keep the build green until then; regen reproduces the same shape.
  - **Customer communication** — Platinum users go from 15% to 12% (or to 17% combined → 12% capped if they also have Plus). For existing high-tier users, consider a banner or email explaining the change so they don't feel the change is sneaky. Plus users at Bronze/Silver/Gold see strictly more discount (5→5, 5→10, 5→15→12 cap), so no notice needed there.
