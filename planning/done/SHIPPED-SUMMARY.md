# Shipped — Cumulative Summary

Last updated: 2026-05-01

This document is the rolled-up record of every planning spec that has been
implemented end-to-end. The original spec files are preserved in this `done/`
folder for reference, but the source of truth for what's actually in the
codebase is the code itself — these summaries are navigation aids, not
specs to re-execute.

For anything still pending, see `planning/active/`.

---

## Domain & data foundations

### Address domain unification (`address-domain-unification.md`)
Backend `Address` and `SavedAddress` separated correctly. `Address` is the
immutable snapshot used in orders + employee home addresses; `SavedAddress`
is the per-user wrapper with label and default flag. Lat/lng nullable on
`Address`. Mapbox geocoding service with optional client lat/lng hints.
`AddSavedAddress`, `UpdateSavedAddress`, `SetDefaultSavedAddress`,
`DeleteSavedAddress` commands all in place. `CreateOrder` accepts either
inline `customerAddress` or `savedAddressId` (XOR validation).

### Address Phase B — mobile (`address-unification-phase-b-mobile.md`)
Mobile `SavedAddressApi`, `AddressRepository` rewritten against real backend,
booking submit sends `savedAddressId` when applicable, refresh on sign-in.

### Address Phase C — web (`address-unification-phase-c-web.md`)
Customer web `SavedAddressStore`, profile address dialog wired to backend,
order wizard pulls saved addresses + can save current selection. localStorage
path removed.

### NSwag customer address nullability (`nswag-customer-address-nullability.md`)
Customer NSwag client regenerated with proper nullable annotations on address
fields after backend hardening.

---

## Orders integration

### Mobile booking submission (`mobile-booking-submission.md`)
Real `CreateOrder` API call from mobile (no more mock). `BookingViewModel`
manages state, validates submission, returns `BookingSubmitOutcome` (Success
/ Failed / ProfileIncomplete). Confirmation code + orderId flow into the
success screen. `CategoryDto` first-class on services. `OrderPricingCalculator`
extracted server-side. `Quote` endpoint + handler shipped.

### Mobile live quote (`booking-live-quote.md`)
Mobile booking flow uses server-authoritative pricing — debounced
`POST /api/Order/Quote` on selection changes, cached quote reused at submit
when inputs match.

### Web live-quote parity (`booking-web-live-quote-parity.md`)
Customer web order wizard now uses server-authoritative pricing identical
to mobile pattern. Debounced quote signals + RxJS bridge in
`OrderWizardFacade`, submit sends `quote.totalPrice` + `quote.currencyId`.
Replaces previous client-side calculation.

### Booking success polish (`booking-success-polish.md`)
Real `OrderDetailScreen` (no more mock), real `OrderApi` + `OrderRepository`,
`OrderDetailViewModel` with proper Loading/Error/Loaded states. Booking
success screen shows arrival window, order summary card, data-driven
4-step timeline (status + cleaner-aware), CTAs for "View order" / "Back home".
Cache reset on home navigation via `BookingViewModel.reset()`.

### Orders Wave 1 — read path (`orders-integration-wave-1.md`)
Mobile `OrderApi`, `OrderRepository`, `OrderRepositoryEntryPoint`. OrdersTab
rewired to real repo. OrderDetail rewired with skeleton + retry. Home tab
shows recent orders. Booking success enrichment wired.

### Orders Wave 2 — actions (`orders-integration-wave-2.md`)
Cancel order sheet + cancellation reason flow. Submit review sheet (rating
+ comment, edit-mode for existing reviews). Receipt download (streaming
PDF via `OrderApi.downloadReceipt`, opened via system PDF viewer intent).
Photos summary on order detail + dedicated photos screen with fullscreen
pager. Disputes feature: list, create, detail screens + repository.

### Orders Wave 3 — disputes evidence + rebook (`orders-integration-wave-3.md`)
Backend `UploadDisputeEvidence` command + `[CanUploadDisputeEvidence]`
policy. Mobile multipart upload via `DisputeApi.uploadEvidence`.
"Book again" CTA on completed orders pre-fills the booking sheet via
`rebookFromOrderId`. Edit review mode wired.

---

## Loyalty system (4 phases — fully shipped)

### Phase A — Tier discount foundation (`loyalty-phase-a.md`)
`LoyaltyAccount`, `LoyaltyTier` (Bronze/Silver/Gold/Platinum),
`LoyaltyTierConfig`, `LoyaltyTransaction` entities. `ILoyaltyService` resolves
per-order tier discount. `Order` carries `TierDiscountAmount` +
`TierAtPurchase` snapshot. Mobile `core/loyalty/`, RewardsTab + activity
screen. Web rewards feature with rewards card, activity log, facade.
Discount applied during `CreateOrder.cs` handler. Refunded on cancel
proportionally. Earned on completion.

### Phase B — Promo codes (`loyalty-phase-b-promo-codes.md`)
`PromoCode`, `PromoCodeRedemption`, `PromoCodeType`. `IPromoCodeService` with
validate + redeem. Customer `ValidatePromoCode` handler + `PromoCodeController`.
Best-wins precedence with tier (bigger discount applies, never stack).
Mobile `PromoCodeBottomSheet` integrated into ConfirmStep. Web order-wizard
promo row with dialog (Wolt-style). Order columns `PromoDiscountAmount` +
`PromoCodeId`.

### Phase C — Referrals (`loyalty-phase-c-referrals.md`)
`Referral`, `ReferralCode`, `ReferralStatus`. `IReferralService` with accept,
qualify, expire methods. Late-acceptance referral path (paste code at
booking time). Sign-up referral flow on register screen. Mobile
`ReferralCodeBottomSheet`, RewardsTab referral section. Web register +
rewards screens reference referrals. Reward credit applied to inviter on
invitee's first qualifying order completion.

### Phase D — Admin (`loyalty-phase-d-admin.md`)
Admin handlers for tier configs (`GetAllTierConfigs`, `UpdateTierConfig`,
`PreviewTierThresholdImpact`), promo codes (CRUD + redemptions list),
referrals (paged list + by-user), user loyalty (account view, manual
grant/revoke points). Admin web feature libs: `loyalty-promo-codes`,
`loyalty-tier-configs`, `loyalty-referrals`, `loyalty-user-detail`. All
admin routes registered.

---

## Profile & onboarding

### Profile completion onboarding (`profile-completion-onboarding.md`)
`isProfileComplete` signal on `UserRepository`. Phone field at signup.
`ProfileOnboardingScreen` shown post-signin when profile is incomplete.
`BookingViewModel.submit()` returns `ProfileIncomplete` outcome that
navigates to onboarding instead of failing the order. `MainShell` gates
onboarding visibility per-user via `hasSeenOnboarding(userId)`.

---

## Mascot system (this session)

### Web + mobile mascot animations
- Custom green-screen pipeline (`C:\Users\cmisa\AppData\Local\Temp\green_pipeline.py`)
  for processing future OpenArt-generated mascot videos. Auto-detects
  per-file green hex from corner samples, applies tight chroma-key
  (`0.08:0.02`), encodes WebP for Android + WebM with VP9 alpha for web.
- **Welcoming mascot**: plays once on success screens (BookingSuccessScreen
  on Android, checkout-success on web).
- **Cleaning mascot**: loops continuously when order is `InProgress`
  (LiveProgressHero on Android order detail, order-detail web).
- Coil 3 + `coil-gif` extension registered in `CleansiaApp` for animated
  WebP rendering on Android. `MascotAnimation` composable with `loop` flag.
- OpenArt prompt template documented for future mascot generations.

### LiveProgressHero (Android)
New hero component on OrderDetailScreen that replaces static `HeroCard`
for active orders (`Confirmed` / `InProgress`). Status pill, contextual
headline (mutates per state, includes cleaner name), optional ETA subhead,
live progress bar (only `InProgress`, computed from
`(now - startedAt) / estimatedDurationMin`, capped at 97%), 4-step indicator,
mascot overlay top-right. 30s self-tick. `OrderDetailViewModel` polls
backend every 30s while status ∈ {Confirmed, InProgress}, auto-cancels on
terminal states.

---

## Bug fixes

### Express booking validator (this session)
`PriceMatchesAsync` validator in `CreateOrder.cs` previously rejected
bookings inside the 2-4h express window because client sent `base × 1.20`
but validator computed base only. Fixed by mirroring the handler's express
surcharge logic in the validator.

### SwipeToConfirm stuck after error (this session)
`SwipeToConfirmButton` stayed at the end after a failed submit, no way
to retry. Added `resetTrigger` parameter; `BookingBottomSheet` increments
a counter on `BookingSubmitOutcome.Failed` to snap the thumb back.

### Booking sheet address re-hydration after reset
`LaunchedEffect` key changed to `(visible, preferred?.id)` so address
fields re-populate when the sheet is reopened after a previous booking.

---

## Files moved into this folder

For full historical specs:
- `mobile-booking-submission.md`, `booking-live-quote.md`,
  `booking-web-live-quote-parity.md`, `booking-success-polish.md`
- `orders-integration-wave-1.md`, `wave-2.md`, `wave-3.md`
- `address-domain-unification.md`, `address-unification-phase-b-mobile.md`,
  `address-unification-phase-c-web.md`,
  `nswag-customer-address-nullability.md`
- `loyalty-phase-a.md`, `loyalty-phase-b-promo-codes.md`,
  `loyalty-phase-c-referrals.md`, `loyalty-phase-d-admin.md`
- `profile-completion-onboarding.md`
