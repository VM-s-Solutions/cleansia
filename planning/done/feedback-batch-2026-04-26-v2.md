# Feedback Batch v2 — 2026-04-26

12 tasks across backend (address security), customer web, and customer Android.

## Loyalty system reference (relevant to TASK-011)

- **Earning**: 1 point per 10 CZK spent (`Math.Floor(order.TotalPrice / 10m)`) on order completion. File: `src/Cleansia.Core.AppServices/Services/LoyaltyService.cs:42`.
- **Tiers** (by lifetime points, not current balance):
  - **Bronze Cleaner** — 0+ points (default)
  - **Silver Mopper** — 500+ → 5% off (≥1000 CZK orders)
  - **Gold Polisher** — 2000+ → 10% off
  - **Platinum Sparkler** — 5000+ → 15% off
- **Storage**: `LoyaltyAccount.LifetimePoints` (denormalized sum) + `CurrentTier` (recomputed on grant/revoke). Tier configs in `LoyaltyTierConfigs` table, seeded via `sql-scripts/insert_seed_data.sql:2236-2278`.
- **Cancellation**: `RevokeForCancelledOrderAsync` walks back the original earned points. Idempotent.

So the home "milestone" card on Android currently uses booking COUNT (5/10/25/50) which is wrong. It should use lifetime POINTS against the actual tier ladder above.

---

## PHASE 1 — Backend address security (run first, blocks frontend)

### TASK-001 — AddSavedAddress: require Mapbox coords, drop State

```yaml
id: TASK-001
file: src/Cleansia.Core.AppServices/Features/SavedAddresses/AddSavedAddress.cs
changes:
  - Make Latitude/Longitude REQUIRED (non-nullable double) on Command
  - Drop State parameter from Command (Address.Create still accepts nullable state — pass null)
  - Validator: range checks (-90..90, -180..180) with new BusinessErrorMessage.MapboxCoordsRequired
  - Handler: drop the geocode-fallback branch (~lines 95-119). Coords are mandatory.
  - Address.Create call uses null for state
acceptance:
  - Command no longer has State property
  - Lat/Lng required, validated
  - Posting without coords returns 400 with MapboxCoordsRequired
manual_step_after: NSwag regen for customer + partner clients
```

### TASK-002 — UpdateSavedAddress: same treatment

```yaml
id: TASK-002
file: src/Cleansia.Core.AppServices/Features/SavedAddresses/UpdateSavedAddress.cs
changes: mirror of TASK-001 — required Lat/Lng, drop State, drop geocode fallback
```

### TASK-003 — Add MapboxCoordsRequired error key + i18n

```yaml
id: TASK-003
files:
  - src/Cleansia.Core.AppServices/Common/BusinessErrorMessage.cs
    add: public const string MapboxCoordsRequired = "address.mapbox_coords_required";
  - apps/cleansia.app/src/assets/i18n/{en,cs,sk,uk,ru}.json
    add under errors.address:
      mapbox_coords_required: "Please pick an address from the suggestions."
```

---

## PHASE 2 — Customer web (after Phase 1 + NSwag regen)

### TASK-004 — Profile address dialog: hide street/city/zip inputs, read-only display

```yaml
id: TASK-004
files:
  - libs/cleansia-customer-features/profile/src/lib/profile/profile.component.html (lines 437-460)
    remove: 3 <cleansia-text-input formControlName="street|city|zip"> blocks
    add: read-only resolved-address display block under <cleansia-address-autocomplete>
    keep: isDefault toggle
  - libs/cleansia-customer-features/profile/src/lib/profile/profile.component.ts
    saveAddress: short-circuit + snackbar if !addressForm.value.street (Mapbox not picked)
    onAddressPicked: keep current behavior (already patches form)
  - libs/shared/assets/src/styles/pages/cleansia-customer/profile.component.scss
    add __dialog-resolved + __dialog-hint styles
  - apps/cleansia.app/src/assets/i18n/{en,cs,sk,uk,ru}.json
    add pages.profile.address_pick_required
```

### TASK-005 — Order wizard inline address: same treatment

```yaml
id: TASK-005
files:
  - libs/cleansia-customer-features/order-wizard/src/lib/order-wizard/order-wizard.component.html (lines 368-445)
    remove: 3 editable street/city/zipCode <input pInputText> blocks
    add: read-only resolved-address display block
    keep: "Save this address" checkbox + label input
  - libs/cleansia-customer-features/order-wizard/src/lib/order-wizard/order-wizard.facade.ts
    canProceed step 1: require lat & lng non-null when isCustomAddress()
  - libs/shared/assets/src/styles/pages/cleansia-customer/order-wizard.component.scss
    add __address-resolved + __address-hint (light + dark)
  - apps/cleansia.app/src/assets/i18n/{en,cs,sk,uk,ru}.json
    add pages.order.address_pick_required
```

### TASK-006 — Profile address dialog: "Set as default" auto-checked when no addresses exist

```yaml
id: TASK-006
file: libs/cleansia-customer-features/profile/src/lib/profile/profile.component.ts (line 328)
change: openAddAddress() — if addresses().length === 0, default isDefault to true
verify: toggle is unconditionally rendered in template (current code already does)
```

### TASK-007 — Package cards: spacing, equal heights, vertical centering

```yaml
id: TASK-007
file: libs/shared/assets/src/styles/pages/cleansia-customer/order-wizard.component.scss
changes:
  - __card-grid: add align-items: stretch
  - __selection-card: ensure height: 100%
  - __selection-included:
      margin: 0.5rem 0 1rem;
      flex: 1;
      display: flex;
      flex-direction: column;
      justify-content: center;  # vertically center list when card is tall
      gap: 0.4rem;
  - __selection-price: margin-top: auto (anchor at bottom)
  - __selection-desc: drop flex:1 (let included-list eat the slack)
verify: service-only cards (no included list) still render correctly
```

### TASK-008 — Date/time tab: replace one-line cancel hint with full 4-tier breakdown

```yaml
id: TASK-008
files:
  - libs/cleansia-customer-features/order-wizard/src/lib/order-wizard/order-wizard.component.html (lines 512-515)
    replace: <p class="__time-hint--cancel"> with the same 4-tier block from wizard-summary-step.component.ts:247-267
    use: __cancel-policy-row + __cancel-policy-free|mid|full classes (already styled)
  - libs/shared/assets/src/styles/pages/cleansia-customer/order-wizard.component.scss
    add: __cancel-policy-inline + __cancel-policy-inline-title (light + dark)
  - apps/cleansia.app/src/assets/i18n/{en,cs,sk,uk,ru}.json
    REMOVE: pages.order.cancel_hint (now obsolete; tier keys reused)
```

### TASK-009 — Rewards page + profile rewards-card: dark mode polish + width

```yaml
id: TASK-009
files:
  - libs/shared/assets/src/styles/pages/cleansia-customer/rewards.component.scss
    add inside :root.dark-mode .customer-rewards:
      - tier-row-badge variants (bronze/silver/gold/platinum) with darker, less-saturated gradients
      - referral card dark mode: __referral-card / __referral-code / __referral-subtitle / __referral-stats
      - hero card dark variants softened
    change: customer-profile__rewards-slot max-width 1200px → 1100px (match profile section cards)
acceptance:
  - tier badges visibly darker / less saturated in dark mode
  - referral code section uses dark surface + readable code color
  - profile rewards-card spans same width as profile section cards
  - no light-mode regressions
```

---

## PHASE 3 — Customer Android (parallel with Phase 2)

### TASK-010 — Home trust strip: equal-width items, single-line labels

```yaml
id: TASK-010
files:
  - app/src/main/java/cz/cleansia/customer/features/home/HomeTab.kt (lines 366-398)
    TrustStrip Row: SpaceEvenly → spacedBy(0.dp), each TrustItem gets weight(1f)
    TrustItem: accept Modifier param, Text → maxLines = 1, ellipsis, textAlign Center
  - app/src/main/res/values/strings.xml + values-cs/strings.xml
    audit longest labels; shorten if 360dp width truncates
acceptance:
  - 3 items equal width, icons aligned, labels on 1 line
```

### TASK-011 — Milestone card: drive from loyalty points, not bookings

```yaml
id: TASK-011
files:
  - app/src/main/java/cz/cleansia/customer/features/home/HomeTabEntryPoint.kt
    add: fun loyaltyRepository(): LoyaltyRepository
  - app/src/main/java/cz/cleansia/customer/features/home/HomeTab.kt (lines 99-174, 583-645)
    observe: loyaltyRepo.account as State<LoyaltyAccountDto?>
    gate: account?.let { MilestoneProgressCard(it) }; hide when account null OR nextTier null
    rewrite: MilestoneProgressCard(account: LoyaltyAccountDto)
      - tier from LoyaltyTier.fromValue(account.currentTier)
      - target = lifetimePoints + pointsToNextTier
      - progress = lifetimePoints / target
      - Title: "{currentTierLabel} — Progress to next tier"
      - Subtitle: "{pointsToNext} more points to {nextTierLabel}"
  - app/src/main/java/cz/cleansia/customer/features/main/MainShell.kt
    verify: loyaltyRepository().refresh() prefetched on first composition
  - app/src/main/res/values/strings.xml + values-{cs,sk,uk,ru}/strings.xml
    new keys:
      home_milestone_title_v2: "%1$s — Progress to next tier"
      home_milestone_subtitle_v2: "%1$d more points to %2$s"
acceptance:
  - card hides when account null or at max tier
  - shows current tier label + lifetimePoints / nextTierThreshold
  - subtitle shows real "X more points to {NextTier}"
  - old totalOrders booking logic removed
```

### TASK-012 — Booking sheet: reset state on submit success and on fresh open

```yaml
id: TASK-012
files:
  - app/src/main/java/cz/cleansia/customer/features/booking/BookingViewModel.kt
    add public fun reset() that resets:
      _state = BookingState()
      _quote = null
      _quoting = false
      _submitting = false
      _promoCodeState = PromoCodeUiState.Idle
      _referralCodeState = ReferralCodeUiState.Idle
      lastQuoteInputs = null
  - app/src/main/java/cz/cleansia/customer/features/booking/BookingBottomSheet.kt
    On SwipeToConfirmButton onConfirmed Success branch: call bookingVm.reset() before onComplete()
    Add LaunchedEffect(visible) at the TOP (before rebook effect):
      if (visible && rebookFromOrderId == null && lastRebookedFrom == null) {
        bookingVm.reset()
        currentStep = 1
      }
acceptance:
  - submit + close + reopen Book Now → empty state
  - rebook flow still works (rebook effect runs after reset)
  - currentStep starts at 1 each fresh open
```

---

## Execution order

1. **Phase 1** parallel: TASK-001, 002, 003
2. **MANUAL_STEP**: regenerate customer NSwag client (`npm run generate-customer-client`)
3. **Phase 2** parallel groups (after manual step):
   - Group A: TASK-004 + TASK-006 (profile)
   - Group B: TASK-005 (order wizard address)
   - Group C: TASK-007 (package cards SCSS)
   - Group D: TASK-008 (cancel policy inline)
   - Group E: TASK-009 (rewards dark mode)
4. **Phase 3** parallel (independent of phases 1-2):
   - Group F: TASK-010 + TASK-011 (HomeTab)
   - Group G: TASK-012 (BookingViewModel + sheet)

i18n: all 5 locales (en, cs, sk, uk, ru) for web; values + values-cs at minimum for Android, plus values-{sk,uk,ru} for the new milestone keys.

Verification: `dotnet build`, `dotnet test`, `npx nx build cleansia.app`, `./gradlew :app:assembleDebug`.
