# Feedback Batch — 2026-04-26

User-reported issues across customer web, customer Android, and backend. 14 tasks total.

Pre-decisions made by user:
- **Session policy**: long-lived 24h access token + sliding refresh (already implemented backend-side; only the lifetime constant needs bumping).
- **Cancellation policy**: acceptance-aware tiered — before any cleaner accepts → free anytime; after acceptance → free >24h, **25%** fee 4–24h, **50%** <4h. Backend uses `OrderStatusHistory.Any(s => s.Status == Confirmed)` as the "accepted" signal (no new column needed).

---

## TASK-001 — Rewards page: fix unresolved perk i18n key

```yaml
task: 'Rewards page perks render raw i18n key'
id: TASK-001
type: bug
priority: high
specialist: frontend
app: customer
estimated_complexity: small
recommended_model: haiku

context: |
  Backend's loyalty perk records store `labelKey` like `loyalty.perks.welcome_badge`
  (admin help text confirms this convention — apps/cleansia-admin.app/src/assets/i18n/en.json:1379).
  Frontend template at rewards.component.html:81 prefixes ANOTHER `pages.rewards.perks.`
  on top, producing `pages.rewards.perks.loyalty.perks.welcome_badge` which has no
  i18n entry → renders raw. Strip the prefix; backend already provides full path.

files_to_modify:
  - path: 'libs/cleansia-customer-features/rewards/src/lib/rewards/rewards.component.html'
    line_range: '81'
    change: |
      Replace
        {{ ('pages.rewards.perks.' + (perk.labelKey || '')) | translate }}
      with
        {{ (perk.labelKey || 'pages.rewards.perks.welcome_badge') | translate }}

  - path: 'apps/cleansia.app/src/assets/i18n/en.json'
    line_range: '983-990'
    change: |
      Move the entire `perks: { welcome_badge, discount_5_above_1000, discount_10,
      discount_15, priority_support, dedicated_pool }` block out from under
      `pages.rewards.perks` and re-nest as TOP-LEVEL `loyalty.perks.*`. Keep
      pages.rewards.perks empty or delete the key. Mirror in cs.json, sk.json,
      uk.json, ru.json (lines 983-990 for each).

dependencies: []

verification:
  - 'nx build cleansia.app passes'
  - 'Rewards page renders "Welcome to Cleansia rewards" instead of raw key'
```

---

## TASK-002 — Rewards page: add dark-mode styles

```yaml
task: 'Rewards page is unreadable in dark mode'
id: TASK-002
type: bug
priority: high
specialist: frontend
app: customer
estimated_complexity: small
recommended_model: sonnet

context: |
  rewards.component.scss has zero `:root.dark-mode` overrides. Light-mode hero
  uses `$primary-lighter` background, perks use `$primary-lighter` icon bg, and
  the gradient title text is hard to read on dark surface. Mirror the structure
  used by order-wizard.component.scss (line 1427+) — append a `:root.dark-mode
  .customer-rewards { ... }` block at end of file with overrides.

files_to_modify:
  - path: 'libs/shared/assets/src/styles/pages/cleansia-customer/rewards.component.scss'
    change: |
      Append after the closing brace of `.customer-rewards`:

      :root.dark-mode {
        .customer-rewards {
          &__hero {
            background: linear-gradient(180deg, rgba(56, 189, 248, 0.04) 0%, transparent 100%);
          }
          &__hero-title {
            background: linear-gradient(135deg, #7dd3fc 0%, #38bdf8 100%);
            -webkit-background-clip: text;
            background-clip: text;
          }
          &__progress-card,
          &__section {
            background: #1e293b;
            border-color: #334155;
          }
          &__progress-label,
          &__perk-label,
          &__tier-row-threshold,
          &__tier-row-discount {
            color: #94a3b8;
          }
          &__perk-icon {
            background: rgba(56, 189, 248, 0.12);
            color: #7dd3fc;
          }
          &__tier-row {
            background: #172033;
            border-color: #334155;
          }
          &__tier-row--current {
            border-color: #38bdf8;
            box-shadow: 0 0 0 1px #38bdf8;
          }
          &__tier-row-name {
            color: #e2e8f0;
          }
          &__section-title {
            color: #e2e8f0;
          }
        }
      }

dependencies: []

verification:
  - 'Toggle dark mode on /rewards — text readable, cards have dark backgrounds'
```

---

## TASK-003 — Order wizard filter chips: dark-mode

```yaml
task: 'Booking filter chips (Home / Deep clean) stay light-themed in dark mode'
id: TASK-003
type: bug
priority: medium
specialist: frontend
app: customer
estimated_complexity: small
recommended_model: haiku

context: |
  __filter-chip rule (order-wizard.component.scss:287) uses literal `$white`
  background and `$text-body` color — survives the dark-mode block. Selected
  chip is `$primary` so it looks fine; unselected ones look like light-mode.
  Add overrides inside the existing `:root.dark-mode .order-wizard { ... }`
  block (starts line 1427).

files_to_modify:
  - path: 'libs/shared/assets/src/styles/pages/cleansia-customer/order-wizard.component.scss'
    change: |
      Inside `:root.dark-mode .order-wizard { ... }` block (insert near the
      `&__section` override, around line 1455), add:

      &__filter-chip {
        background: #172033;
        border-color: #334155;
        color: #cbd5e1;

        &:hover {
          border-color: #38bdf8;
          color: #7dd3fc;
        }

        &--selected {
          background: #38bdf8;
          border-color: #38bdf8;
          color: #0c1119;

          &:hover {
            background: #7dd3fc;
            border-color: #7dd3fc;
            color: #0c1119;
          }
        }
      }

      &__filter-label {
        color: #94a3b8;
      }

      &__filter-active {
        background: rgba(56, 189, 248, 0.1);
        color: #cbd5e1;

        > i, strong { color: #7dd3fc; }
      }

      &__filter-clear {
        background: #172033;
        border-color: rgba(56, 189, 248, 0.3);
        color: #7dd3fc;
      }

dependencies: []

verification:
  - 'Toggle dark mode on /order — Home/Deep-clean chips have dark background, blue selected pill stays visible'
```

---

## TASK-004 — Move room-config section above packages

```yaml
task: 'Room config section is buried below services + packages, hard to see'
id: TASK-004
type: improvement
priority: medium
specialist: frontend
app: customer
estimated_complexity: small
recommended_model: haiku

context: |
  Order wizard step 0 currently renders Services → Packages → Room Config.
  User reports rooms section is "almost invisible" at the bottom (same issue
  we fixed on mobile). Move Room Config (lines 190-244 in
  order-wizard.component.html) to render BEFORE the Packages section
  (currently at lines 153-188) but AFTER Services. New order:
  Services → Room Config → Packages.

files_to_modify:
  - path: 'libs/cleansia-customer-features/order-wizard/src/lib/order-wizard/order-wizard.component.html'
    line_range: '150-244'
    change: |
      Cut the Room Configuration block (lines 190-244, comment "<!-- Room Configuration -->"
      through closing </div>) and paste it BEFORE the Packages section (currently
      starting at line 153 "@if (facade.packages().length > 0) { <!-- Packages Section -->").
      Resulting order: Services Section → Room Configuration Section → Packages Section.

dependencies: []

verification:
  - 'Open /order — confirm Services first, then Room Config, then Packages'
```

---

## TASK-005 — Show services included in each package

```yaml
task: 'Package cards do not show which services are included'
id: TASK-005
type: feature
priority: medium
specialist: frontend
app: customer
estimated_complexity: small
recommended_model: sonnet

context: |
  Backend already returns `PackageListItem.includedServices: PackageServiceSummary[]`
  (NSwag client confirmed at customer-client.ts:8210). Each summary has a `name`
  and `translations` map. Currently package cards render only name/desc/price.
  Add an expandable list of included services beneath the description.

files_to_modify:
  - path: 'libs/cleansia-customer-features/order-wizard/src/lib/order-wizard/order-wizard.component.html'
    line_range: '160-186'
    change: |
      Inside the package card (after the `__selection-desc` <p> at line 178-180,
      before the `__selection-price`), add:

      @if (pkg.includedServices && pkg.includedServices.length > 0) {
        <ul class="order-wizard__selection-included">
          @for (svc of pkg.includedServices; track svc.name) {
            <li>
              <i class="pi pi-check"></i>
              {{ getTranslation(svc, 'name') }}
            </li>
          }
        </ul>
      }

  - path: 'libs/shared/assets/src/styles/pages/cleansia-customer/order-wizard.component.scss'
    change: |
      Add inside `.order-wizard { ... }` (near other __selection-* rules) and
      mirror the pattern in `:root.dark-mode .order-wizard`:

      &__selection-included {
        list-style: none;
        padding: 0;
        margin: 0.5rem 0;
        display: flex;
        flex-direction: column;
        gap: 0.25rem;

        li {
          display: flex;
          align-items: center;
          gap: 0.5rem;
          font-size: 0.85rem;
          color: $text-muted;

          i {
            color: $green;
            font-size: 0.75rem;
          }
        }
      }

      // In dark-mode block:
      &__selection-included li {
        color: #94a3b8;
        i { color: #4ade80; }
      }

dependencies: []

verification:
  - 'nx build cleansia.app passes'
  - 'Package cards on /order display a green-tick list of included service names'
```

---

## TASK-006 — Profile country field: hide / readonly

```yaml
task: 'Country field in saved-address dialog is editable, should be CZ-only'
id: TASK-006
type: improvement
priority: medium
specialist: frontend
app: customer
estimated_complexity: small
recommended_model: haiku

context: |
  Single-deployment per country (cleansia.cz now, cleansia.pl later). User
  shouldn't pick country per-address. Hide the country select entirely in the
  profile address dialog. Default value is auto-set in facade.

  Order wizard already hardcodes country (per inline comment at order-wizard.component.html:406).

files_to_modify:
  - path: 'libs/cleansia-customer-features/profile/src/lib/profile/profile.component.html'
    line_range: '457-463'
    change: |
      Remove the entire <cleansia-select formControlName="country" ... /> block
      (lines 457-463). Country defaults to "CZ" in the form initializer.

  - path: 'libs/cleansia-customer-features/profile/src/lib/profile/profile.component.ts'
    change: |
      Verify the `addressForm` FormGroup builder defaults `country: 'CZ'` (or
      whatever current default is). If not, add it. Remove `countryOptions()`
      computed/signal if it becomes unused after the template change.

dependencies: []

verification:
  - 'nx build cleansia.app passes'
  - 'Open profile → add address: Country dropdown is gone'
  - 'Saved address still has CZ country in db'
```

---

## TASK-007 — Address autocomplete: provision Mapbox token (MANUAL_STEP)

```yaml
task: 'Address autocomplete invisible — Mapbox token not configured'
id: TASK-007
type: configuration
priority: high
specialist: backend
app: customer
estimated_complexity: small
recommended_model: n/a (manual)

context: |
  CleansiaAddressAutocompleteComponent renders nothing when MAPBOX_ACCESS_TOKEN
  is empty (cleansia-address-autocomplete.component.html:1 wraps everything in
  @if (isMapboxConfigured)). All three environment files have empty `mapboxToken: ''`.
  Owner must provision a public Mapbox token (geocoding-only scope is sufficient)
  and populate envs.

files_to_modify:
  - path: 'apps/cleansia.app/src/environments/environment.ts'
    line_range: '15'
    change: 'Set mapboxToken to dev/local token'
  - path: 'apps/cleansia.app/src/environments/environment.staging.ts'
    line_range: '13'
    change: 'Set mapboxToken to staging token (or share dev key)'
  - path: 'apps/cleansia.app/src/environments/environment.prod.ts'
    line_range: '12'
    change: 'Set mapboxToken to production token via deploy-time replacement'

dependencies: []

verification:
  - 'Token shows up in network tab when typing in address field'
  - 'Mapbox dropdown appears beneath the search field'

manual_step: true
notes: |
  Frontend code is correct. This is purely an env-var issue. Once the token is
  set in environment.ts the component activates and TASK-006's hidden country
  field is no longer relevant (Mapbox suggestions auto-populate city / zip too).
```

---

## TASK-008 — Anonymous order detail navigation from track-order list

```yaml
task: 'Track-order page lists orders but has no navigation to detail'
id: TASK-008
type: bug
priority: high
specialist: frontend
app: customer
estimated_complexity: small
recommended_model: sonnet

context: |
  /track-order auto-loads recent guest orders from localStorage (ListBatch).
  Cards at track-order.component.html:33-96 are static — no click handler / link.
  Detail route already exists at `/orders/lookup/:orderId` → GuestOrderDetailComponent
  (order-lookup.routes.ts:16). Wrap the card or add a footer button that navigates.

files_to_modify:
  - path: 'libs/cleansia-customer-features/orders/src/lib/track-order/track-order.component.html'
    line_range: '33-96'
    change: |
      Wrap the recent-orders `<div class="track-order__order-card">` in either:
       (a) <a [routerLink]="['/orders/lookup', order.id]" class="track-order__order-card-link"> ... </a>
       (b) Add a footer "View details" button using <cleansia-button> after line 76 (`__order-card-footer`)
      Recommend (b) — preserves the timeline expansion which conflicts with whole-card click.

      Add inside __order-card-footer:
      <cleansia-button
        [title]="'pages.track_order.view_details' | translate"
        [severity]="'secondary'"
        [icon]="'pi pi-arrow-right'"
        (clickFn)="viewDetails(order.id!)"
      />

  - path: 'libs/cleansia-customer-features/orders/src/lib/track-order/track-order.component.ts'
    change: |
      Inject Router; add method:
      viewDetails(orderId: string): void {
        // Cache the order so detail page doesn't re-fetch.
        this.cache.set(orderId, order, this.email());  // need to find email association
        this.router.navigate(['/orders/lookup', orderId]);
      }

      May need to inject GuestOrderLookupCacheService and GuestOrderService
      to look up the email. Check order-lookup.component.ts:67-103 for the pattern.

i18n_keys:
  - key: 'pages.track_order.view_details'
    en: 'View details'
    cs: 'Zobrazit detail'
    sk: 'Zobraziť detail'
    uk: 'Переглянути деталі'
    ru: 'Подробнее'

dependencies: []

verification:
  - 'nx build cleansia.app passes'
  - 'Open /track-order with cached guest order → click "View details" → /orders/lookup/<id> renders'
```

---

## TASK-009 — Promo + referral row: dark-mode styles

```yaml
task: 'Promo / referral code rows dont fit dark theme'
id: TASK-009
type: bug
priority: medium
specialist: frontend
app: customer
estimated_complexity: small
recommended_model: haiku

context: |
  __code-row rule (order-wizard.component.scss:994) uses literal `$white` background
  and `$text-dark` label. Just-built styles in TASK-006 of previous batch — were
  never extended to dark-mode block.

files_to_modify:
  - path: 'libs/shared/assets/src/styles/pages/cleansia-customer/order-wizard.component.scss'
    change: |
      Inside `:root.dark-mode .order-wizard { ... }` block, add:

      &__code-row {
        background: #172033;
        border-color: #334155;

        &:hover, &:focus-visible {
          background: rgba(56, 189, 248, 0.06);
          border-color: #38bdf8;
        }

        &-icon {
          color: #7dd3fc;
        }

        &-label {
          color: #e2e8f0;
        }

        &-applied, &-action, &-clear {
          color: #94a3b8;
        }

        &-clear:hover {
          background: rgba(248, 113, 113, 0.15);
          color: #f87171;
        }
      }

dependencies: []

verification:
  - 'Open dark mode /order summary step — promo + referral rows have dark background, readable text'
```

---

## TASK-010 — Cancellation policy copy + tier values update

```yaml
task: 'Cancellation policy says "Free up to 24h" but actual policy charges 50% in 4-24h window'
id: TASK-010
type: content
priority: high
specialist: frontend
app: customer
estimated_complexity: small
recommended_model: haiku

context: |
  Update i18n + tier strong-text colors to reflect new acceptance-aware policy.
  The "Free up to 24h" hint at order-wizard.component.html:514 is misleading;
  also tier values must be updated to 25% (mid) / 50% (last-4h).

  Backend will be updated separately in TASK-013 to ALSO honour the new tiers
  AND skip fees entirely when no cleaner has accepted (order in New status).

files_to_modify:
  - path: 'apps/cleansia.app/src/assets/i18n/en.json'
    line_range: '700, 733-739, 1191'
    change: |
      Replace:
        cancel_hint: "Free cancellation up to 24 hours before start"
      with:
        cancel_hint: "Free cancellation until a cleaner accepts your booking, then standard tiered policy applies"

      Update cancel_policy_tier values (lines 733-739):
        cancel_policy_tier1_when: "Before a cleaner accepts" (NEW)
        cancel_policy_tier1_value: "Free"
        cancel_policy_tier2_when: "Accepted, 24+ hours before start"
        cancel_policy_tier2_value: "Free"
        cancel_policy_tier3_when: "Accepted, 4–24 hours before start"
        cancel_policy_tier3_value: "25% charge"
        cancel_policy_tier4_when: "Accepted, less than 4 hours before start"
        cancel_policy_tier4_value: "50% charge"

      Update legal section (line 1191):
        section4_text: "Cancellations are free until a cleaner accepts your
        booking. Once accepted: free 24+ hours before start, 25% fee 4-24 hours
        before, 50% fee under 4 hours before start."

      Mirror in cs.json, sk.json, uk.json, ru.json.

  - path: 'libs/cleansia-customer-features/order-wizard/src/lib/order-wizard/components/wizard-summary-step.component.ts'
    line_range: '247-260'
    change: |
      Add a 4th tier row to render. Existing template renders 3 — add a row
      for "Before a cleaner accepts" → Free at the TOP. Update the existing
      tier indices accordingly:
        tier1 → "Before a cleaner accepts" / Free (use __cancel-policy-free)
        tier2 → "Accepted, 24+ hours before" / Free (free)
        tier3 → "Accepted, 4–24 hours before" / 25% (mid)
        tier4 → "Accepted, less than 4 hours" / 50% (full)

dependencies: ['TASK-013']

verification:
  - 'Open /order summary → cancellation policy panel shows 4 tiers with correct text'
  - 'Open Privacy/Terms page → cancellation paragraph updated'
```

---

## TASK-011 — Mobile home: replace fake trust strip with real signals

```yaml
task: 'Home trust strip shows fake "Insured | Vetted | 4.9 · 2.3k"'
id: TASK-011
type: content
priority: medium
specialist: mobile
app: android
estimated_complexity: small
recommended_model: sonnet

context: |
  HomeTab.kt:370-387 renders three TrustItem composables with hardcoded labels
  including a fake star rating "4.9 · 2.3k". User says it's fake. Replace with
  three TRUE statements that don't require backend wiring:

    1. "Insured up to 1M CZK" (already true — backend has insurance docs flow)
    2. "Background-checked cleaners" (true — partner onboarding requires KYC)
    3. "Same-day available" (true — express tier exists at 2-4h lead time)

  Drop the rating column entirely. Booking confirm step already uses these
  same labels (R.string.booking_trust_insured, .booking_trust_vetted), so
  reuse those strings if helpful.

files_to_modify:
  - path: 'app/src/main/java/cz/cleansia/customer/features/home/HomeTab.kt'
    line_range: '370-401'
    change: |
      In TrustStrip(): replace the three TrustItem calls with:

        TrustItem(Icons.Outlined.Shield, stringResource(R.string.home_trust_insured))
        Box(...divider...)
        TrustItem(Icons.Outlined.VerifiedUser, stringResource(R.string.home_trust_vetted))
        Box(...divider...)
        TrustItem(Icons.Outlined.Bolt, stringResource(R.string.home_trust_same_day))

      Remove the WarningStar tint usage — only one icon style now.
      In TrustItem(): default tint param can drop the `tint = WarningStar` override.

  - path: 'app/src/main/res/values/strings.xml'
    change: |
      Update existing strings:
        home_trust_insured: "Insured up to 1M CZK"
        home_trust_vetted: "Background-checked"
      Remove: home_trust_rated
      Add: home_trust_same_day: "Same-day available"

  - path: 'app/src/main/res/values-cs/strings.xml'
    change: |
      home_trust_insured: "Pojištěno až do 1M Kč"
      home_trust_vetted: "Prověření uklízeči"
      Remove: home_trust_rated
      Add: home_trust_same_day: "K dispozici ještě dnes"

dependencies: []

verification:
  - 'Open mobile home — trust strip shows 3 true items, no fake rating'
```

---

## TASK-012 — Mobile home: rename VIP milestone + remove fake activity

```yaml
task: 'Home shows fake "Progress to VIP Member" + "14 cleanings booked in Prague 2"'
id: TASK-012
type: content
priority: medium
specialist: mobile
app: android
estimated_complexity: small
recommended_model: sonnet

context: |
  Two issues:

  (a) MilestoneProgressCard (HomeTab.kt:589) DOES use real data
  (totalOrders count) but the title "Progress to VIP Member" is wrong — the
  loyalty system uses Bronze / Silver / Gold / Platinum tiers (R.string.rewards_tier_bronze etc.).
  Rename the milestone to align with actual rewards naming, e.g. "Progress to next tier"
  and use the actual next-tier name from the rewards backend if available, otherwise
  a generic "next tier" string.

  (b) LocalActivityCard (HomeTab.kt:653) hardcodes "14 cleanings booked in Prague 2 this week"
  with subtitle "Join your neighbors". This is fake. Either remove the entire card,
  or replace with something true. Recommend REMOVE the card to keep the home
  page honest until we have a real "X bookings this week in your area" feed.

files_to_modify:
  - path: 'app/src/main/java/cz/cleansia/customer/features/home/HomeTab.kt'
    line_range: '169-171, 650-691'
    change: |
      (a) Update MilestoneProgressCard call site at lines 162-167 — keep the
          guard, but change title resource to a more honest one.

      (b) Delete the LocalActivityCard composable entirely (lines 650-691) AND
          the call site at lines 169-171 (`LocalActivityCard()` + spacer).

  - path: 'app/src/main/res/values/strings.xml'
    line_range: '142, 146-147'
    change: |
      Replace home_milestone_title:
        from "Progress to VIP Member"
        to "Progress to next tier"

      Replace home_milestone_subtitle:
        from "%1$d more bookings to unlock priority support"
        to "%1$d more bookings to unlock the next tier"

      Remove: home_activity_title, home_activity_subtitle

  - path: 'app/src/main/res/values-cs/strings.xml'
    change: |
      Mirror with Czech translations:
        home_milestone_title: "Pokrok k další úrovni"
        home_milestone_subtitle: "Ještě %1$d rezervací k odemčení další úrovně"
      Remove: home_activity_title, home_activity_subtitle

dependencies: []

verification:
  - 'Open mobile home — milestone shows honest tier-progress title'
  - 'No more "14 cleanings booked in Prague 2" card'
  - './gradlew :app:assembleDebug succeeds'
```

---

## TASK-013 — Mobile disputes empty state: vertical center

```yaml
task: 'Disputes empty state hugs the top instead of centering'
id: TASK-013
type: bug
priority: low
specialist: mobile
app: android
estimated_complexity: small
recommended_model: haiku

context: |
  EmptyState() at DisputesListScreen.kt:352 uses Column with no
  verticalArrangement. Same fix pattern we applied to OrdersTab —
  add Arrangement.Center.

files_to_modify:
  - path: 'app/src/main/java/cz/cleansia/customer/features/disputes/DisputesListScreen.kt'
    line_range: '352-384'
    change: |
      In EmptyState() Column(), add:
        verticalArrangement = Arrangement.Center,
      and remove the literal `vertical = 48.dp` from padding (now centered).

      If EmptyState is rendered inside a parent with verticalScroll, wrap it in
      BoxWithConstraints + heightIn(min = maxHeight) like the Orders fix
      (OrdersTab.kt ScrollableStateContainer pattern).

dependencies: []

verification:
  - 'Open Disputes tab when empty — mascot + text + CTA centered vertically'
```

---

## TASK-014 — Mobile delete-account button: match logout style

```yaml
task: 'Delete account button outlined-red, doesnt match Log out filled-red style'
id: TASK-014
type: improvement
priority: low
specialist: mobile
app: android
estimated_complexity: small
recommended_model: haiku

context: |
  ProfileTab.kt LogoutRow (line 451) uses:
    background = surface, circular icon container with errorContainer alpha,
    bold title text.
  DeleteAccountRow (line 488) uses:
    border = error.alpha(0.35), no background, plain icon, non-bold text.

  Make DeleteAccountRow match LogoutRow's visual structure (surface bg + circular
  error-tinted icon container + bold text), keeping its destructive-red color
  on text + icon.

files_to_modify:
  - path: 'app/src/main/java/cz/cleansia/customer/features/profile/ProfileTab.kt'
    line_range: '488-517'
    change: |
      Rewrite DeleteAccountRow to mirror LogoutRow structure:

      Row(
          modifier = Modifier
              .fillMaxWidth()
              .padding(horizontal = 20.dp)
              .clip(RoundedCornerShape(18.dp))
              .background(MaterialTheme.colorScheme.surface)
              .clickable(onClick = onClick)
              .padding(horizontal = 16.dp, vertical = 16.dp),
          verticalAlignment = Alignment.CenterVertically,
      ) {
          Box(
              modifier = Modifier
                  .size(32.dp)
                  .background(MaterialTheme.colorScheme.errorContainer.copy(alpha = 0.5f), CircleShape),
              contentAlignment = Alignment.Center,
          ) {
              Icon(
                  Icons.Outlined.DeleteForever,
                  null,
                  tint = MaterialTheme.colorScheme.error,
                  modifier = Modifier.size(18.dp),
              )
          }
          Spacer(Modifier.width(14.dp))
          Text(
              stringResource(R.string.profile_delete_account),
              style = MaterialTheme.typography.bodyLarge.copy(fontWeight = FontWeight.SemiBold),
              color = MaterialTheme.colorScheme.error,
              modifier = Modifier.weight(1f),
          )
      }

      Drop the `.border(...)` modifier entirely.

dependencies: []

verification:
  - 'Open Profile screen — Log out and Delete account look identical in structure'
```

---

## TASK-015 — Backend: bump JWT access token to 24h

```yaml
task: 'Customer session expires after 15 min'
id: TASK-015
type: configuration
priority: high
specialist: backend
app: backend
estimated_complexity: small
recommended_model: haiku

context: |
  AccessTokenExpMinutes is set to 15 in all 4 appsettings.json. Refresh
  infrastructure already exists (RefreshTokenService.RotateAsync, frontend
  CustomerErrorInterceptorFn handles 401 → refresh → retry). The 15-min ceiling
  is just felt as "constant logout" because the refresh only triggers on a 401
  (i.e. user must make a request first). Bump to 24h (1440) so sessions feel
  long-lived without changing refresh semantics.

files_to_modify:
  - path: 'src/Cleansia.Web.Customer/appsettings.json'
    line_range: '24'
    change: 'AccessTokenExpMinutes: 15 → 1440'
  - path: 'src/Cleansia.Web.Customer/appsettings.Production.json'
    change: 'Same — bump to 1440 (or higher if explicitly desired)'
  - path: 'src/Cleansia.Web/appsettings.json'
    change: 'AccessTokenExpMinutes: 15 → 1440'
  - path: 'src/Cleansia.Web/appsettings.Production.json'
    change: 'Same'
  - path: 'src/Cleansia.Web.Mobile/appsettings.json'
    change: 'AccessTokenExpMinutes: 15 → 1440'
  - path: 'src/Cleansia.Web.Mobile/appsettings.Production.json'
    change: 'Same'
  - path: 'src/Cleansia.Web.Admin/appsettings.json'
    change: 'AccessTokenExpMinutes: 15 → 1440 (or keep tighter for admin if desired)'
  - path: 'src/Cleansia.Web.Admin/appsettings.Production.json'
    change: 'Same as above'

dependencies: []

verification:
  - 'dotnet build Cleansia.Api.sln passes'
  - 'Sign in to customer app, leave tab idle 30+ min, return → not logged out'
  - 'Existing refresh-on-401 flow still works (test by manually expiring token)'
```

---

## TASK-016 — Backend: acceptance-aware cancellation policy

```yaml
task: 'Cancellation policy should waive fees if no cleaner has accepted yet'
id: TASK-016
type: improvement
priority: high
specialist: backend
app: backend
estimated_complexity: medium
recommended_model: sonnet

context: |
  Per user decision: before any cleaner accepts → free anytime. After acceptance:
  free >24h, 25% fee 4-24h, 50% fee <4h. Acceptance signal is
  `OrderStatusHistory.Any(s => s.Status == OrderStatus.Confirmed)`
  (TakeOrder.cs:159 sets this when a cleaner takes the job).

  Two changes:
    (a) BookingPolicy.CalculateCancellationFeeRate signature gets a new bool
        `hasBeenAccepted` parameter, returning 0m when false.
    (b) PartialCancellationFeeRate constant changes from 0.50m → 0.25m.
        Last-4h tier needs explicit handling — current `_ => 1m` becomes
        `>= 0 => 0.50m` since after-start is blocked elsewhere by the
        InProgress check.
    (c) CancelOrder.Handler computes hasBeenAccepted from the loaded
        OrderStatusHistory and passes it into the calculator.

files_to_modify:
  - path: 'src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs'
    line_range: '49, 89-109'
    change: |
      (a) Constant: PartialCancellationFeeRate from 0.50m → 0.25m
      (b) Add new constant:
          public const decimal LastMinuteCancellationFeeRate = 0.50m;
      (c) CalculateCancellationFeeRate signature add `bool hasBeenAccepted`:

          public static decimal CalculateCancellationFeeRate(
              DateTime cleaningUtc,
              DateTime bookingCreatedUtc,
              DateTime cancelUtc,
              bool isFirstTimeCustomer,
              bool hasBeenAccepted)
          {
              // No cleaner has taken the order yet — always free.
              if (!hasBeenAccepted) return 0m;

              // "Oops window" still applies after acceptance for accidental taps.
              var oopsMinutes = isFirstTimeCustomer ? OopsWindowMinutesFirstTime : OopsWindowMinutesStandard;
              if ((cancelUtc - bookingCreatedUtc).TotalMinutes <= oopsMinutes)
                  return 0m;

              var hoursBeforeStart = (cleaningUtc - cancelUtc).TotalHours;
              return hoursBeforeStart switch
              {
                  >= FreeCancellationHours => 0m,
                  >= PartialCancellationHours => PartialCancellationFeeRate,  // 25%
                  _ => LastMinuteCancellationFeeRate,                          // 50%
              };
          }

      Update XML doc at top to reflect the new "free until accepted" rule.

  - path: 'src/Cleansia.Core.AppServices/Features/Orders/CancelOrder.cs'
    line_range: '111-117'
    change: |
      Before the fee-rate calculation, derive hasBeenAccepted:

      var hasBeenAccepted = order.OrderStatusHistory
          .Any(s => s.Status == OrderStatus.Confirmed);

      var feeRate = BookingPolicy.CalculateCancellationFeeRate(
          order.CleaningDateTime, order.CreatedOn.UtcDateTime, now,
          isFirstTime, hasBeenAccepted);

      Update the XML doc at the top of the class (lines 15-25) to mention the
      acceptance-aware policy.

  - path: 'src/Cleansia.Tests/'
    change: |
      Find existing BookingPolicy tests (grep "CalculateCancellationFeeRate") —
      update test cases to pass the new boolean. Add new tests for the
      "before acceptance" case (returns 0m regardless of timing).

dependencies: []
no_migration_needed: true  # No DB schema change — uses existing OrderStatusHistory

verification:
  - 'dotnet build Cleansia.Api.sln passes'
  - 'dotnet test src/Cleansia.Tests passes'
  - 'Manual smoke: cancel a "New" order via API → fee = 0 regardless of cleaningDateTime'
  - 'Manual smoke: cancel a "Confirmed" order 1h before start → fee = 0.50 (50%)'
  - 'Manual smoke: cancel a "Confirmed" order 12h before start → fee = 0.25 (25%)'

manual_step_after: |
  None — pure C# change, no migration, no NSwag.
```

---

## Execution Plan

### Phase 1 — Backend (sequential, parallelizable internally)
- **TASK-015** Bump JWT access token to 24h (haiku, ~3k tokens)
- **TASK-016** Acceptance-aware cancellation policy (sonnet, ~12k tokens)

### >> No MANUAL_STEP needed — both backend tasks are config / pure C# (no migrations, no NSwag).

### Phase 2 — Frontend web (parallelizable per task)
- **TASK-001** Rewards i18n key fix (haiku, ~5k tokens)
- **TASK-002** Rewards dark mode (sonnet, ~6k tokens)
- **TASK-003** Filter chips dark mode (haiku, ~3k tokens)
- **TASK-004** Move room-config above packages (haiku, ~3k tokens)
- **TASK-005** Show included services in packages (sonnet, ~5k tokens)
- **TASK-006** Hide country field (haiku, ~3k tokens)
- **TASK-008** Anonymous order detail navigation (sonnet, ~7k tokens)
- **TASK-009** Promo+referral row dark mode (haiku, ~3k tokens)
- **TASK-010** Cancellation policy copy + 4-tier display (haiku, ~6k tokens — depends on TASK-016 mental model only)

### Phase 3 — Frontend mobile (parallelizable per task)
- **TASK-011** Trust strip real signals (sonnet, ~4k tokens)
- **TASK-012** Milestone rename + remove fake activity (sonnet, ~4k tokens)
- **TASK-013** Disputes empty state center (haiku, ~3k tokens)
- **TASK-014** Delete account button match logout (haiku, ~3k tokens)

### Phase 4 — Manual configuration
- **TASK-007** Provision Mapbox token (owner — env vars only)

### Phase 5 — Verification
- `dotnet build Cleansia.Api.sln`
- `dotnet test src/Cleansia.Tests`
- `npx nx build cleansia.app`
- `cd src/cleansia_customer_android && ./gradlew :app:assembleDebug`

### Token Estimate
- Phase 1: ~15k
- Phase 2: ~41k (run in 2-3 parallel agents)
- Phase 3: ~14k (run in 2 parallel agents)
- Phase 5: build verification ~5k
- **Total: ~75k tokens** (vs ~250k+ if specialists explored from scratch)

### Parallelization Recommendations
- Phase 2 group A (parallel agent): TASK-001, TASK-002, TASK-003, TASK-009 (all SCSS / minor template tweaks in customer-web shared assets)
- Phase 2 group B (parallel agent): TASK-004, TASK-005, TASK-006 (order-wizard + profile templates)
- Phase 2 group C (parallel agent): TASK-008, TASK-010 (track-order + cancel-policy copy)
- Phase 3 group A (parallel agent): TASK-011, TASK-012 (HomeTab.kt + strings)
- Phase 3 group B (parallel agent): TASK-013, TASK-014 (DisputesListScreen + ProfileTab)

### Model recommendations
- Default Phase 1, 2, 3 to **sonnet**.
- Use **haiku** for: TASK-001, TASK-003, TASK-004, TASK-006, TASK-009, TASK-013, TASK-014, TASK-015 (precise diffs, no judgment).
- Phase 4 (TASK-007) is owner-only — Claude does nothing.
