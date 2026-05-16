# Shipped — Cumulative Summary

Last updated: 2026-05-16

This document is the rolled-up record of every planning spec that has been
implemented end-to-end. Original spec files are no longer kept in this
folder — the source of truth for what's actually in the codebase is the
code itself, and these summaries are navigation aids, not specs to
re-execute.

For anything still pending, see `planning/active/`. For the in-progress
refactoring sweep that supersedes feature work, see
`planning/active/refactor-plan.md`.

---

## Domain & data foundations

### Address domain unification
Backend `Address` and `SavedAddress` separated correctly. `Address` is the
immutable snapshot used in orders + employee home addresses; `SavedAddress`
is the per-user wrapper with label and default flag. Lat/lng nullable on
`Address`. Mapbox geocoding service with optional client lat/lng hints.
`AddSavedAddress`, `UpdateSavedAddress`, `SetDefaultSavedAddress`,
`DeleteSavedAddress` commands all in place. `CreateOrder` accepts either
inline `customerAddress` or `savedAddressId` (XOR validation).

### Address Phase B — mobile
Mobile `SavedAddressApi`, `AddressRepository` rewritten against real backend,
booking submit sends `savedAddressId` when applicable, refresh on sign-in.

### Address Phase C — web
Customer web `SavedAddressStore`, profile address dialog wired to backend,
order wizard pulls saved addresses + can save current selection. localStorage
path removed.

### NSwag customer address nullability
Customer NSwag client regenerated with proper nullable annotations on address
fields after backend hardening.

---

## Auth & session

### Refresh token migration
OAuth2-style access+refresh token pattern rolled out across the backend
+ all three Angular web apps + Android customer app. Replaces the old
single-JWT-with-long-lifetime design. `RefreshToken` entity with
`IRefreshTokenRepository`, hash-based lookup, rotation on use, revoke
on logout. `Login`, `GoogleAuth`, `ConfirmUserEmail`, `RefreshToken`
handlers all updated. Web auth interceptors use silent-refresh on 401;
Android `AuthAuthenticator` performs blocking refresh through OkHttp.

### Profile completion onboarding
`isProfileComplete` signal on `UserRepository`. Phone field at signup.
`ProfileOnboardingScreen` shown post-signin when profile is incomplete.
`BookingViewModel.submit()` returns `ProfileIncomplete` outcome that
navigates to onboarding instead of failing the order. `MainShell` gates
onboarding visibility per-user via `hasSeenOnboarding(userId)`. The
booking VM also force-refreshes the profile snapshot before submit so a
stale cache can't leak an incomplete user past the gate.

---

## Orders integration

### Mobile booking submission
Real `CreateOrder` API call from mobile (no more mock). `BookingViewModel`
manages state, validates submission, returns `BookingSubmitOutcome` (Success
/ Failed / ProfileIncomplete / CardPending). Confirmation code + orderId
flow into the success screen. `CategoryDto` first-class on services.
`OrderPricingCalculator` extracted server-side. `Quote` endpoint + handler
shipped.

### Mobile live quote
Mobile booking flow uses server-authoritative pricing — debounced
`POST /api/Order/Quote` on selection changes, cached quote reused at submit
when inputs match.

### Web live-quote parity
Customer web order wizard now uses server-authoritative pricing identical
to mobile pattern. Debounced quote signals + RxJS bridge in
`OrderWizardFacade`, submit sends `quote.totalPrice` + `quote.currencyId`.
Replaces previous client-side calculation.

### Booking date/time picker
14-day mobile range with optional `DatePickerDialog` capped at +60 days.
Time slots derived from `BookingPolicy` lead-time math
(StandardLeadTimeHours = 4, ExpressLeadTimeHours = 2). Express slots
visually marked + `BookingState.isExpressBooking` flag drives the price
footer surcharge preview. Backend remains pricing authority via
`CreateOrder.Validator`'s `RequiresExpressSurcharge` check.

### Booking success polish
Real `OrderDetailScreen` (no more mock), real `OrderApi` + `OrderRepository`,
`OrderDetailViewModel` with proper Loading/Error/Loaded states. Booking
success screen shows arrival window, order summary card, data-driven
4-step timeline (status + cleaner-aware), CTAs for "View order" / "Back home".

### Booking guest checkout
Guest flow on web — no auth required for the order wizard. `GuestOrderService`
manages session state. Guest order lookup at `/orders/lookup`.

### Orders Wave 1 — read path
Mobile `OrderApi`, `OrderRepository`, `OrderRepositoryEntryPoint`. OrdersTab
rewired to real repo. OrderDetail rewired with skeleton + retry. Home tab
shows recent orders. Booking success enrichment wired.

### Orders Wave 2 — actions
Cancel order sheet + cancellation reason flow. Submit review sheet (rating
+ comment, edit-mode for existing reviews). Receipt download (streaming
PDF via `OrderApi.downloadReceipt`, opened via system PDF viewer intent).
Photos summary on order detail + dedicated photos screen with fullscreen
pager. Disputes feature: list, create, detail screens + repository.

### Orders Wave 3 — disputes evidence + rebook
Backend `UploadDisputeEvidence` command + `[CanUploadDisputeEvidence]`
policy. Mobile multipart upload via `DisputeApi.uploadEvidence`.
"Book again" CTA on completed orders pre-fills the booking sheet via
`rebookFromOrderId`. Edit review mode wired.

### Orders list refresh after booking (this session)
[MainShell.kt] now fires `orderRepo.refresh()` from the booking sheet's
`onComplete` callback so the new order shows up on the Orders tab without
the user pulling-to-refresh. [OrdersTab.kt] also auto-refreshes on tab
entry as a safety net for cross-session race conditions.

---

## Loyalty system (4 phases — fully shipped)

### Phase A — Tier discount foundation
`LoyaltyAccount`, `LoyaltyTier` (Bronze/Silver/Gold/Platinum),
`LoyaltyTierConfig`, `LoyaltyTransaction` entities. `ILoyaltyService` resolves
per-order tier discount. `Order` carries `TierDiscountAmount` +
`TierAtPurchase` snapshot. Mobile `core/loyalty/`, RewardsTab + activity
screen. Web rewards feature with rewards card, activity log, facade.
Discount applied during `CreateOrder.cs` handler. Refunded on cancel
proportionally. Earned on completion.

### Phase B — Promo codes
`PromoCode`, `PromoCodeRedemption`, `PromoCodeType`. `IPromoCodeService` with
validate + redeem. Customer `ValidatePromoCode` handler + `PromoCodeController`.
Best-wins precedence with tier (bigger discount applies, never stack).
Mobile `PromoCodeBottomSheet` integrated into ConfirmStep. Web order-wizard
promo row with dialog (Wolt-style). Order columns `PromoDiscountAmount` +
`PromoCodeId`. Slide-to-pay total fixed (this session) to honor the
post-discount `finalTotal` math instead of raw `quote.totalPrice`.

### Phase C — Referrals
`Referral`, `ReferralCode`, `ReferralStatus`. `IReferralService` with accept,
qualify, expire methods. Sign-up referral flow on register screen. Reward
credit applied to inviter on invitee's first qualifying order completion.
**Late-acceptance referral path removed (this session)** — referral codes
are signup-only on both mobile and web. The booking-flow referral entry
was removed because backend enforces one-per-invitee and the post-signup
re-entry created confusing "already referred" errors. `ReferralService.
EnsureCodeForUserAsync` now commits explicitly inside the Query path so
the referrer's lifetime code persists on first read.

### Phase D — Admin
Admin handlers for tier configs (`GetAllTierConfigs`, `UpdateTierConfig`,
`PreviewTierThresholdImpact`), promo codes (CRUD + redemptions list),
referrals (paged list + by-user), user loyalty (account view, manual
grant/revoke points). Admin web feature libs: `loyalty-promo-codes`,
`loyalty-tier-configs`, `loyalty-referrals`, `loyalty-user-detail`. All
admin routes registered.

---

## Cleansia Plus (subscription) — fully shipped on mobile + web

### Backend foundation
`MembershipPlan` entity (Stripe-backed pricing per billing interval),
`UserMembership` entity with `MembershipStatus` lifecycle. `IMembershipService`
with create-checkout-session, cancel-at-period-end, get-mine. Stripe
webhooks for `customer.subscription.created/updated/deleted`. Each
`User` has a persistent `StripeCustomerId`. Pricing pipeline accepts
`membershipDiscount` alongside loyalty tier + promo code (best-wins
precedence across 3 sources).

### Plus features (gated by `MembershipStatus.Active`)
- Member discount on every order (configurable per plan)
- Wider free-cancellation window
- Express upgrade benefit (no surcharge inside the express window)
- Recurring booking schedules (see below)
- "Request your favorite cleaner" — `preferredEmployeeId` on order with
  matching algorithm boost

### Mobile Plus UI
SubscribePlusScreen with dark hero, monthly/annual toggle, perks tiles,
sticky CTA, Stripe `PaymentSheet` SDK integration. Annual pricing shows
year price (not per-month) so the savings versus monthly are clear.
Mascot anchor on the hero. Plus management card on Profile tab with
gold-gradient header for active state, amber gradient for cancellation-
requested state.

### Mobile post-purchase celebration (this session)
`MembershipSuccessScreen` replaces the silent snackbar+pop flow that left
users staring at a blank Subscribe screen for a beat after Stripe confirmed.
Mascot animation, gradient header, perk preview card, primary "Set up
recurring cleaning" CTA + secondary "Back home". `SubscribePlusScreen`
gained a `navigatedAway` guard so the auto-back LaunchedEffect doesn't
double-fire and bounce users out of the success screen.

### Web Plus UI
- `MembershipSubscribeComponent` — marketing + Stripe Checkout Session
  redirect (subscription mode).
- `MembershipManagementComponent` — active card with PLUS badge, plan
  name, perks pills, period info row, switch-to-annual CTA, cancel button.
  Cancellation-requested state uses an amber/red palette and "ENDING"
  badge so it visually contrasts with the gold "active+renewing" state.
- `MembershipWelcomeComponent` (this session) — post-Stripe-success
  celebration page. Stripe `successUrl` flips from `/membership?subscribed=1`
  to `/membership/welcome`. Primary "Set up a recurring cleaning" CTA +
  secondary "Back to membership".

### Plus profile inactive-card redesign (this session, mobile)
`MembershipManagementCard.InactiveCard` redesigned with PLUS pill badge,
mascot_ready anchor, perks-summary subtitle, full-width "Try free for 14
days" primary CTA bar.

---

## Recurring bookings (Plus perk — mobile + web)

### Mobile (PA14)
- Backend `RecurringBookingTemplate` entity + materializer Azure Function
  (daily job that spawns concrete `Order` rows from active templates).
- `IRecurringBookingRepository`, `IRecurringBookingTemplateRepository`.
  Customer commands: `CreateRecurringBooking`, `UpdateRecurringBooking`,
  `SetRecurringBookingActive` (pause/resume), `DeleteRecurringBooking`.
- `RecurringBookingsScreen` list view with redesigned cards (cadence
  badge, address row, pause/resume + delete actions).
- `CreateRecurringScreen` — multi-step wizard:
  - **Step 1 — When**: outlined frequency cards with cadence subline
    + "MOST POPULAR" badge on biweekly, day chips (Mo/Tu/We/Th/Fr · gap ·
    Sa/Su) with weekend tint, time slots grouped Morning/Afternoon/
    Evening with sun/sun/moon glyphs.
  - **Step 2 — What**: package cards with "Includes:" bulleted service
    list, service cards with description, rooms/bathrooms steppers.
  - **Step 3 — Where & Pay**: saved-address picker with inline "Add new
    address" → AddressManagerSheet, side-by-side Cash/Card payment cards,
    Material3 DatePickerDialog for starts-on.
- Live summary banner at top of wizard restates the user's choices in
  plain language ("Every Thursday at 10:00") with calendar icon. Updates
  every tap.
- 3-dot step indicator with checkmarks for completed steps. Sticky
  Back/Next/Create bottom action bar with `navigationBarsPadding()`
  above gesture indicator.
- Path A entry: empty-state CTA + populated-list FAB on the recurring
  list. Path B entry: "Make this recurring" CTA on Completed order detail
  (Plus-gated, pre-fills 6 fields from order).
- Hilt entry points for Address + Snackbar + Catalog from non-VM Compose
  context. `RecurringBookingsFacade` signal-only state.
- Smart defaults: Thursday + 10:00 (mid-week, lowest-conflict). Plus
  members with no recurring schedule see a "Set up recurring" carousel
  slide on home.

### Web (PA14 web — Wave A + B, this session)
- New library `libs/cleansia-customer-features/recurring-bookings/` mirrors
  mobile structure. `RecurringBookingsListComponent`, `CreateRecurringWizard
  Component`, `RecurringBookingsFacade` (signal-only, no NgRx slice — same
  pattern as `OrderWizardFacade`).
- 3-step wizard with parity for mobile features: live summary banner,
  step indicator, frequency cards with badge, day chips with weekend gap,
  time-slot groups with period labels, package cards with "Includes" list,
  payment cards, calendar starts-on picker.
- Path B prefill from past Completed order via sessionStorage +
  `?prefill=true` query param. "Make this recurring" CTA on order detail
  page (Plus-gated; non-Plus users routed to `/membership/subscribe`).
- Profile membership-management page gained a "Recurring cleanings" entry
  link card (Plus-only, between active card and inactive card).
- Routes: `/membership/recurring` (list) + `/membership/recurring/create`
  (wizard) + `/membership/welcome` (Stripe success destination).
- Full i18n in 5 locales (en/cs/sk/uk/ru) — ~70 new keys per locale.
- Scoped SCSS in `libs/shared/assets/.../recurring-bookings.component.scss`
  with full dark-mode overrides. Membership management page also gained
  full styles (was previously unstyled — pre-existing gap discovered and
  fixed this session).

---

## Mascot system

### Web + mobile mascot animations
- Custom green-screen pipeline (`green_pipeline.py`) for processing
  OpenArt-generated mascot videos. Per-file green hex auto-detected
  from corner samples, tight chroma-key (`0.08:0.02`), encodes WebP
  for Android + WebM with VP9 alpha for web.
- **Welcoming mascot**: plays once on success screens (BookingSuccess,
  MembershipSuccess on Android, checkout-success on web).
- **Cleaning mascot**: loops continuously when order is `InProgress`
  (LiveProgressHero on Android order detail, order-detail web).
- Coil 3 + `coil-gif` extension registered in `CleansiaApp` for animated
  WebP rendering on Android. `MascotAnimation` composable with `loop` flag.

### LiveProgressHero (Android)
Hero component on OrderDetailScreen that replaces static `HeroCard` for
active orders (`Confirmed` / `InProgress`). Status pill, contextual
headline (mutates per state, includes cleaner name), optional ETA
subhead, live progress bar (only `InProgress`, computed from `(now -
startedAt) / estimatedDurationMin`, capped at 97%), 4-step indicator,
mascot overlay top-right. 30s self-tick. `OrderDetailViewModel` polls
backend every 30s while status ∈ {Confirmed, InProgress}, auto-cancels
on terminal states.

---

## Home tab (mobile customer app)

### Smart upsell carousel + state-aware home (this session)
- `SmartUpsellCarousel` replaces static promo cards with a 4-5 slide
  carousel ordered by user state: Plus pitch (hidden when subscribed),
  "Set up recurring" (Plus + no schedules), Welcome (no orders yet),
  Referral (always), Book (always). Auto-rotates 6s; skips advance
  during user touch.
- 5th slide "Set up recurring cleaning" appears when user is Plus +
  no active templates.
- Address top bar, recurring schedules section (Plus-only, when at
  least one active schedule exists), popular packages section
  (top-3 packages tap-to-book → opens booking sheet pre-filled with
  the package), Order Again card (replaces trust strip when user has
  a Completed order to rebook).
- Loading skeleton shown until orders + membership + catalog all
  loaded at least once. 1.5s ceiling so partial loads don't get stuck.

### Booking sheet improvements (this session)
- `prefillPackageId` parameter on `BookingBottomSheet` — popular-
  packages tap on home opens the wizard with the package pre-selected.
- Bug fix: sheet wouldn't reopen after a fast full-dismiss because
  `snapshotFlow { currentValue }.distinctUntilChanged()` replayed the
  cached `Hidden` value as its first emission, immediately re-firing
  `onDismiss()`. Added `.drop(1)` to skip the replay.

---

## UX polish + bug fixes (this session)

### Cancellation policy card (mobile + web parity)
Now config-aware. Standard window 24h (matches backend `BookingPolicy`),
penalty band 4h. Tier 1 always shows "{N}+ hours · Free" with N pulled
from Plus config when applicable. Mid-tier collapses entirely when Plus
config is mis-configured to ≤4h (no contradiction with tier 3). Badge
upgraded from cryptic "PLUS" to "PLUS PERK" with subtitle line "You
get N hours of free cancellation as a Plus member" — only shown when
the Plus rate is actually different from the standard rate.

### Mobile membership active card redesign
Premium gold for active+renewing state, amber for cancellation-requested.
Glance-able "ENDING" pill in the cancellation state. Body row with
auto-renew or event-busy icon + "Renews on / Active until" + helper
hint underneath.

### Mobile recurring booking improvements (this session)
- Multi-step wizard (was a single long form initially).
- Frequency wording clarified: "Every week / Every 2 weeks / Every month".
- 2-letter day abbreviations (Mo, Tu, We, Th, Fr, Sa, Su) so weekend
  letters aren't ambiguous; weekday/weekend visual gap.
- Time picker switched from text input → hourly slot grid matching the
  order booking flow.
- Calendar `DatePickerDialog` for Starts On (was +1d/+1w/+1m buttons).
- "Add new address" inline on Step 3 opens AddressManagerSheet.
- Service/package descriptions render under each chip in card layout.
- Recurring template card redesigned with header strip + body + actions.
- Delete dialog rewritten with explicit "what stops / what stays" copy.

### Onboarding gate hardening (this session)
- `MainShell` onboarding gate calls `userRepo.refreshCurrentUser()`
  *before* deciding so it never reads a stale `isProfileComplete`.
- Booking VM also force-refreshes profile + per-field check before
  submit so an incomplete profile can't slip past to backend.

### Booking sheet KB freeze + global keyboard suppression bugs
- `ReferralCodeBottomSheet` had `verticalScroll` inside `ModalBottomSheet`
  + `OutlinedTextField`, which deadlocked the input pipeline on Compose
  BOM 2025.02.00 when IME appeared. Removed inner scroll, added
  `imePadding()`.
- Root `Surface(modifier = Modifier.fillMaxSize().clickable { focusManager.
  clearFocus(); keyboardController.hide() })` in `MainActivity` was
  hiding the keyboard on every text-field focus. Removed entirely.

### Referral code persistence bug (backend)
`GetMyReferral` is a Query; `UnitOfWorkPipelineBehavior` only commits
for Commands. The lazy-create code path inside `EnsureCodeForUserAsync`
called `Add(rc)` but never committed, so the row never persisted —
the referrer saw their code in the app, friend tried to validate it,
got "not found." Fixed by committing explicitly inside the Ensure
method (since by definition this is the only mutation in the Query
path).

### Customer web: NSwag sub-client base-URL bug
NSwag generates each sub-client with `@Injectable({ providedIn: 'root' })`
and a constructor that defaults `baseUrl = ""` when `CUSTOMER_API_BASE_URL`
isn't injected. Direct injection of `MembershipClient`,
`RecurringBookingClient` etc. resolved against the SPA's own origin
(`localhost:4200`) instead of the configured API URL. Fixed by adding
`membershipClient` + `recurringBookingClient` properties on the
`CustomerClient` wrapper and switching all 4 consumer sites
(`recurring-bookings.facade`, `order-detail.component`,
`wizard-summary-step.component`, `membership-management.component`,
`membership-subscribe.component`) to use the wrapper instead of
injecting sub-clients directly.

### Customer web: dark-theme parity for membership + recurring pages
All new SCSS got `:root.dark-mode` override blocks (project convention).
Active card background flips to `#1e293b`, neutral text to `#f1f5f9` /
`#94a3b8`, accents (primary blue, gold, red) stay as-is since they pop
on both surfaces. Cancellation-requested state retains amber palette
in dark mode but with brightened tones for legibility.

### Express booking validator + SwipeToConfirm reset (earlier)
`PriceMatchesAsync` validator in `CreateOrder.cs` previously rejected
bookings inside the 2-4h express window because client sent
`base × 1.20` but validator computed base only. Fixed by mirroring the
handler's express surcharge logic in the validator.
`SwipeToConfirmButton` got a `resetTrigger` parameter; `BookingBottomSheet`
increments a counter on `Failed` outcome to snap the thumb back.

---

## Notes for the refactor sweep

The following spec files were preserved into `done/` as historical
record but have been deleted as of 2026-05-03. The information they
contained is rolled into the sections above. If a future agent needs
the deep design rationale for any of those features, it lives in:
- This document (high-level + behavioral)
- The code itself (file-level decisions, often comments at the top of
  feature directories)
- Git history (commit messages from when each spec landed)

The remaining open work is captured in `planning/active/`:
- `refactor-plan.md` — master plan for the in-progress refactoring
  + security audit (the most important item right now).
- `mobile-theming-i18n.md` — sk/uk/ru full translations still missing.
- `booking-extras-and-surcharge.md` — extras pricing not yet implemented
  in `OrderPricingCalculator`. Wire is shipped (extras dict persisted on
  Orders), pricing is not.

Partner-app planning (Android + iOS) preserved in `planning/mobile/`
since that workstream hasn't started.

---

## `feat/customer-android-app` branch (2026-05-12)

Single very large branch that delivered the customer Android app plus a
stack of cross-cutting backend + web work. Each item below ships with a
corresponding spec preserved in `done/` (see filenames in parens).

### Customer Android app — native launch
First end-to-end native customer client. Kotlin + Jetpack Compose +
Hilt + Navigation 2.8 typed routes. Features: sign-in / sign-up /
Google OAuth, profile editing + onboarding gate, address book, order
booking wizard, my orders, order tracking, recurring cleanings, Plus
membership subscribe + manage, rewards / loyalty tier, in-app push
notifications. Auth flow uses OkHttp `AuthAuthenticator` for blocking
refresh; network layer uses `core/network/NetworkCall.kt` for uniform
repo error handling. 55 unit tests green.

### OnTheWay order status (new lifecycle slot)
Inserted `OnTheWay = 3` between `Confirmed = 2` and `InProgress = 4`.
Backend `NotifyOnTheWayCommand` + validator + `OrderController` endpoint
+ NSwag wire. Partner My Orders gained a "Notify on the way" action
guarded by status. All three web apps (customer / partner / admin)
display the localized pill via `orderStatusLabel` pipe, with
`enums.order_status.on_the_way` translated across all 5 locales.
Customer Android shows the status on order detail + timeline. Partner
Android has the action button as well.

### Push notifications — Phase A (`push-notifications.md`)
Backend pipeline: `IPushDispatcher` (FCM Admin SDK) + `IQueueClient`
dispatch + Functions queue consumer + per-user `NotificationCategory`
preferences + `Device` registration with token rotation. Phase A
events wired: `order.confirmed`, `order.on_the_way`, `order.in_progress`,
`order.completed`, `order.cancelled`, `order.refunded`,
`dispute.reply`. Customer Android `CleansiaFirebaseMessagingService`
maps events → local notifications with deep-links into typed Compose
routes. i18n × 5 locales.

### Push notifications — Phase B (`push-notifications-phase-b.md` — partial)
Three of four Phase B events shipped: `loyalty.tier_upgrade` (dispatched
from `LoyaltyService.GrantForCompletedOrderAsync` via before/after tier
snapshot), `membership.expiring_soon` + `membership.cancellation_effective`
(daily sweep handler `SendMembershipLifecycleNotifications` + Functions
timer trigger; `UserMembership` gained per-period idempotency stamps
`RenewalReminderSentAt` + `CancellationReminderSentAt` cleared on
Stripe period rollover and on plan swap). `promo.new_sitewide` deferred
pending admin UI.

### Loyalty tier restructure — LOY-003 Option C (`loyalty-tier-restructure-loy003.md`)
Additive Plus+tier discount stack capped at 12%. Uniform 1000 CZK floor
on the tier portion (Plus always applies regardless of order size). Web
UI shows both chips simultaneously. Backend `LoyaltyService` applies the
discount with the cap; admin tier-config view + customer rewards screen
both display the new model.

### Per-app cookie isolation
Added `AUTH_COOKIE_KEYS` injection token in `@cleansia/services`. Each
of the three Angular apps (customer / partner / admin) provides its own
prefixed cookie set (`customer_token` / `partner_token` / `admin_token`)
at bootstrap. Eliminates cross-app session collisions on `localhost`
(cookies aren't port-scoped). `PermissionService` and all three auth
services now read through the token.

### Recurring cleanings — Wave 3 (`recurring-bookings-wave3.md`)
Customer creates a recurring template; `MaterializeRecurringBookings`
Function spawns concrete `Pending` Order rows 7 days ahead so cleaners
see them in their Available tab; `SendRecurringOrderReminders` Function
pushes a 24h-ahead reminder; `ConfirmRecurringOrder` command runs the
customer through Stripe Checkout. `AutoCancelStaleRecurringOrders`
Function reaps unconfirmed orders. UI on customer web + customer
Android.

### Per-list NgRx slices for partner orders
Replaced shared paged slice on partner web orders with per-list slices
(My Orders / All Orders / Available) so paginating one view no longer
clobbers the others' state.

### TakeOrder confirmation email
`TakeOrder`, `StartOrder`, `CompleteOrder` handlers now log warnings
on email-send failure instead of swallowing silently. The
`OrderStatusUpdateTemplateId` SendGrid template id is wired into the
customer API only — by design, non-customer hosts (Partner/Admin/Mobile)
have the key empty so the email is suppressed.

### Bug-fixes worth calling out
- Customer logout effect at `user.effects.ts:35` rewired with
  `mergeMap` + `pipe(map, catchError)` (was calling
  `authService.logout()` without subscribing — fire-and-forget bug).
- EF tenant filter null/null edge case fixed (TenantId null + JWT
  TenantId null was failing the filter; now handled).
- Functions host `IHostAudienceProvider` sentinel binding so queue
  consumers resolve audience.
- FCM service-account JSON expected base64-encoded (raw JSON breaks
  through user-secrets escaping).

### Security remediation summary (`security-remediation-summary.md`)
All 22 CRITICAL findings closed. All audit-confirmed HIGH items closed
(4 deliberately deferred per dedicated sanity audit). Build green.

### Remaining follow-ups (`post-customer-android-cleanup.md`)
- TASK-003: validator unit tests via MockQueryable.Moq (should-fix)
- TASK-007: `promo.new_sitewide` admin UI (nice-to-have, large)
- TASK-008: push setup runbook docs (nice-to-have)
- TASK-009: customer-auth signals refactor (nice-to-have)
- TASK-010: enum-literal grep audit (nice-to-have, 2-min check)

### Manual steps owed by owner
- EF migration for `UserMembership.RenewalReminderSentAt` +
  `CancellationReminderSentAt` columns.
- SendGrid `OrderStatusUpdateTemplateId` template provisioning + seeding
  of `EmailTemplateTranslations` for `EmailType.OrderStatusUpdate` ×
  5 languages.

---

## 2026-05-15 — Frontend cleanup waves + mobile i18n + Phase B closeout

This session closed the long-running frontend cleanup plan, finished the
mobile translation parity gap, and shipped the last Phase B push event.

### Frontend cleanup Waves 1–6 (`frontend-cleanup-plan.md`)
Full audit + execution of the 6-wave plan. Most originally-flagged work
turned out already done in prior sessions; the audit estimates were stale
by ~5× across the board.
- **Wave 1 (cross-imports / audience leaks / orphans):** 0 changes —
  all 4 findings were already fixed.
- **Wave 2 (OnPush + `: any` + facade teardowns):** 2 app shells got
  `OnPush` + `toSignal` for `isLoggedIn`; 10 `: any` typed
  (`ApiException`, `ValidationErrors`); `order-wizard.facade.ts`
  standardized on `takeUntilDestroyed(destroyRef)` for all subscribe
  sites. Audit confirmed 155/155 components already had `OnPush`.
- **Wave 3 (inline templates):** 9 components extracted to `.html`
  files (`cleansia-code-input-dialog`, 4 skeletons, `unauthorized`,
  `cleansia-code-input`, `cleansia-dev-banner`, `cleansia-scroll-top`).
- **Wave 4 (component → facade):** 13 of 14 originally-flagged targets
  already done; 1 real leak fixed (`profile.component.ts` password
  `valueChanges`).
- **Wave 5 (`CleansiaPermissionDirective`):** Directive existed
  already; applied to 8 admin Create buttons across service /
  package / language / country / currency / admin-user / company /
  promo-codes management features.
- **Wave 6 (polish):** 7 of 8 items already done. `cleansia-scroll-top`
  aria-label made configurable via `ariaLabel` input; `global.scroll_to_top`
  i18n key added across all 5 customer locales; 5 consumer sites wired.

All 3 web apps build green at the end of each wave.

### Bonus session fixes
- **Anonymous 401 noise:** `wizard-summary-step.component.ts` and
  `order-detail.facade.ts` were calling `membershipClient.getMine()` for
  anonymous users (booking wizard pre-login + guest checkout). Both now
  gate on `isAuthenticated()` / `authService.isLoggedIn()` and short-
  circuit to `membership = null` (template already treats null as
  "not Plus").
- **Track-order footer alignment:** Czech "Show Detail" button was
  crammed against the price. Footer SCSS was `justify-content: flex-end`
  with no gap. Fixed to `space-between` + `align-items: center` +
  `gap: 1rem`.

### Mobile customer + partner full i18n parity
Audit revealed customer mobile was already at full 5-locale parity
(965 strings each). Partner mobile had 117 missing CS keys (sk/uk/ru
already complete at 603 each). Translated all 117 missing CS strings
grouped by domain (onboarding, schedule setup, day labels, order
list, swipe actions, analytics, notification permission prompt).
Partner Android `compileProdDebugKotlin` green; XML valid, resources
merge clean.

### Push Phase B — `promo.new_sitewide` shipped (`push-notifications-phase-b.md` complete)
The last deferred Phase B event. Architecture:
- **Backend command:** `SendSitewidePromo` accepts 5 locale variants
  of (title, body) — admin types them in the UI. FluentValidation
  rejects missing/over-length entries. Returns immediately after
  enqueueing a single `SendSitewidePromoMessage` to the new
  `sitewide-promo-fanout` queue. Permission: new
  `Policy.CanSendSitewidePromo` (AdminOnly).
- **Backend fan-out Function:** `SendSitewidePromoFanoutFunction` queue-
  triggered; pages `UserNotificationPreferences` where `Promo == true`
  joined with `User.PreferredLanguageCode`, picks the matching locale
  variant from the campaign payload, enqueues one
  `SendPushNotificationMessage` per recipient on the existing
  `notifications-dispatch` queue. Per-user enqueue failures don't
  poison the whole campaign — logged and skipped.
- **Mobile customer:** `CleansiaFirebaseMessagingService.onMessageReceived`
  branches on `event_key == "promo.new_sitewide"` to read title+body
  directly from the FCM data payload (skipping the
  `templateFor`/`strings.xml` lookup the other events use). This is
  the only Phase B event whose body isn't a fixed template. Customer
  mobile compileDebug + 55 tests green.
- **Admin UI:** New top-level "Marketing" sidebar entry with
  "Sitewide Push" child. Single-page form with 5 title inputs + 5
  textarea bodies + Send button. Confirm dialog warns the action is
  unstoppable once enqueued. Uses raw `HttpClient` POST against
  `/api/AdminMarketing/send-sitewide-promo` pending NSwag regen — the
  swap to `adminClient.adminMarketingClient.sendSitewidePromo(...)`
  is mechanical once the typed client is generated. New library
  `@cleansia/admin-features/marketing`. i18n × 5 locales (sidebar +
  page strings + per-locale title/body labels + confirm + success +
  error). New SCSS page entry.

### Manual steps owed by owner (for the Phase B promo work)
- **NSwag regen for admin client.** Run `npm run generate-admin-client`
  (admin-side). After regen, swap the raw `HttpClient.post` call in
  `sitewide-push-form.component.ts:send()` to
  `this.adminClient.adminMarketingClient.sendSitewidePromo(command)`.
- No EF migration needed — no new schema.
- No new queue declaration in Aspire AppHost — the emulated Azure
  Storage auto-creates `sitewide-promo-fanout` on first send.
- After deploy: smoke-test with a low-fanout campaign (e.g. one opt-in
  admin user) before broadcasting to thousands.

---

## 2026-05-16 — ARCH-001 Android monorepo + shared :core module

The two Android apps moved into a single Gradle root with a shared
`:core` library. Full execution log at
`planning/done/arch-001-android-monorepo.md`.

### Repo structure (now)

```
src/cleansia_android/
├── settings.gradle.kts          # includes :core, :partner-app, :customer-app
├── gradle/libs.versions.toml    # unified version catalog
├── core/                        # cz.cleansia.core — shared lib
│   ├── src/main/java/cz/cleansia/core/
│   │   ├── network/             # NetworkCall, IntValueEnumSerializer
│   │   ├── auth/                # TokenStore, AuthInterceptor, AuthAuthenticator,
│   │   │                        # NetworkErrorInterceptor, SessionManager, JwtDecoder
│   │   ├── snackbar/            # SnackbarController, GlobalSnackbarHost, SnackbarInset
│   │   ├── ui/theme/            # Spacing, Type (Poppins/Nunito), Shape
│   │   ├── ui/components/       # CleansiaPrimaryButton / Secondary / Outlined /
│   │   │                        # TextButton / TextLink, CleansiaTextField,
│   │   │                        # CleansiaSectionHeader, CleansiaCheckbox,
│   │   │                        # CleansiaDialog, LabelledDivider
│   │   ├── format/              # OrderFormatters, DisputeFormatters
│   │   └── sentry/              # SentryUserTracker
│   └── src/main/res/values/font_certs.xml
├── partner-app/                 # cz.cleansia.partner (was src/cleansia_android/app/)
└── customer-app/                # cz.cleansia.customer (was src/cleansia_customer_android/app/)
```

### Toolchain alignment (Phase 1)

Partner bumped from Kotlin 2.0.20 / Java 17 / Coil 2 to **Kotlin 2.1.10 +
Java 21 (with core library desugaring) + Coil 3 + Compose BOM 2025.02 +
Hilt 2.54** to match customer. Customer bumped AGP 8.9.1 → 8.13.2 +
Gradle 8.11.1 → 8.13 to match partner. KSP2 disabled globally — partner
hit "unexpected jvm signature V" with Hilt under KSP2; KSP1 is the safe
default for both apps. The 3 Coil 2 → 3 import sites in partner were
migrated by hand (`coil.compose.AsyncImage` → `coil3.compose.AsyncImage`).

### Unified Gradle root (Phase 2)

Both `app/` directories `git mv`'d into the new root. Unified version
catalog took customer's broader entry set and added partner-only
dependencies (Room, biometric, Lottie, splashscreen, espresso). The
Hilt plugin alias appears twice — `hilt` (customer's name) and
`hilt-android` (partner's name) — both resolve to the same plugin id so
neither app's build file needed touching. Mapbox repo moved to root
settings; gradle.properties merged with the larger heap of customer's
config.

### Customer migrated to :core (Phases 3a, 4a, 5)

- `core/network/NetworkCall.kt` + `IntValueEnumSerializer` (the customer-
  specific list of registered enum bindings stays in `customer-app`).
- `core/auth/`: `TokenStore`, `AuthInterceptor`, `AuthAuthenticator`,
  `NetworkErrorInterceptor` (refactored to take string-resource IDs via
  constructor so `:core` doesn't need each app's `R` class), `SessionManager`,
  `SessionScopedCache`, `JwtDecoder`. `SessionScopedModule` (multibinds
  customer repos into the cache) and `TokenStoreEntryPoint` stayed in
  customer-app since they reference customer-specific symbols.
- `core/snackbar/`: `SnackbarController`, `GlobalSnackbarHost`,
  `SnackbarInset` + all their helpers (`SnackbarMessage`, `Severity`,
  `SnackbarInsetState`, `SnackbarControllerEntryPoint`).
- `core/ui/theme/`: `Spacing`, `Type`, `Shape` (with the Google Fonts
  certs xml resource added to `:core/src/main/res/values/`).
  `BrandGradients` stayed customer-only (depends on customer-specific
  app settings).
- `core/ui/components/`: `CleansiaPrimaryButton` + variants (Secondary /
  Outlined / Text / TextLink), `CleansiaTextField`, `CleansiaSectionHeader`,
  `CleansiaCheckbox`, `CleansiaDialog`, `LabelledDivider`.
- `core/format/`: `OrderFormatters`, `DisputeFormatters`.
- `core/sentry/`: `SentryUserTracker`.

~37 customer-app source files had their imports rewritten by a Python
helper script. Customer-app debug build + 53 unit tests green.

### Partner consumes :core (limited surface)

Partner-app added `implementation(project(":core"))` so the build graph
is set up, but currently uses none of `:core`'s code at runtime — it
still has its own `TokenManager`, `AuthInterceptor`, `CleansiaButton`
(enum-style API), `CleansiaTextField`, theme files, etc.

**Phase 3b/4b** (deferred): partner rewrites against `:core`'s patterns.
Estimated 1 day:
- Replace partner's `TokenManager` (197 lines, stores name/email/userId
  alongside tokens) with `:core`'s `TokenStore` (110 lines, tokens only)
  + a new app-specific `UserProfileStore` for the user metadata.
- Add `AuthAuthenticator` to partner — currently has no refresh-token
  retry; falls through to a session-expired event instead.
- Rewrite partner's ~15 `CleansiaButton(text, style=PRIMARY)` call sites
  to use `:core`'s `CleansiaPrimaryButton(text, onClick=…)` shape.
- Rewrite ~6 `CleansiaTextField` call sites (signature differs).

### Verification (Phase 6)

```
./gradlew :core:assembleDebug :partner-app:assembleProdDebug \
          :customer-app:assembleDebug \
          :partner-app:testProdDebugUnitTest \
          :customer-app:testDebugUnitTest
```

→ BUILD SUCCESSFUL. Both APKs assemble side-by-side under different
`applicationId`s (`cz.cleansia.partner` / `cz.cleansia.customer`).

### Wins so far

- One version catalog — no more "AGP version drift" between the two apps.
- Adding a new shared primitive lands in one place; both apps consume it.
- Customer's snackbar / auth / token plumbing is now version-controlled
  inside `:core` — drift requires a deliberate per-app override, not
  passive forgetfulness.
- New `:core` module ready to accept partner's Phase 3b/4b adoption
  whenever convenient — no further plumbing needed.

### Files removed
- `src/cleansia_android/` (now `src/cleansia_android/partner-app/`)
- `src/cleansia_customer_android/` (now `src/cleansia_android/customer-app/`)

### CLAUDE.md updated
Quick-reference table + repo-structure tree both reflect the new path.