# Cleansia Customer Mobile — Design Spec

> **Revision 3 (2026-04-15).** Self-contained design spec. The customer web app at [src/Cleansia.App/apps/cleansia.app/](../../src/Cleansia.App/apps/cleansia.app/) is the **canonical source of truth**. No Stitch, no Figma — Claude designs each screen here in markdown first, then implements directly in Kotlin Compose.
>
> **Process:** for each screen Claude (a) reads the corresponding web component HTML/SCSS, (b) translates to mobile-native (bottom tabs, full-screen flows, bottom sheets, native payment), (c) writes Kotlin Compose, (d) ships to emulator for owner review.

---

## What changed in Revision 3

- **Stitch and Figma eliminated.** The customer web app is now the design source of truth; mobile is a native translation, not an interpretation.
- **Primary color corrected to `#0284C7`** (web's actual brand — Tailwind sky-600). Stitch and my prior work had drifted to `#006194`, which was wrong.
- **Fonts corrected:** Poppins (headings) + Nunito (body), confirmed from the web app's [`<head>`](../../src/Cleansia.App/apps/cleansia.app/src/index.html) — NOT Plus Jakarta Sans.
- **Mascots are real PNGs** at [`src/Cleansia.App/apps/cleansia.app/src/assets/images/mascot/`](../../src/Cleansia.App/apps/cleansia.app/src/assets/images/mascot/). Copy them into the Android `res/drawable-nodpi/` for direct use.
- **Web pages drive the screen list.** Every customer web feature has a mobile equivalent; mobile-only additions (Splash, Welcome Carousel, Booking Success, Preferences, Empty/Error/404) are clearly tagged.

---

## Source-of-truth mapping

The web app's customer features live in [`src/Cleansia.App/libs/cleansia-customer-features/`](../../src/Cleansia.App/libs/cleansia-customer-features/). One subdirectory per feature:

| Web feature | Web HTML location (look here first) | Mobile screen(s) |
|---|---|---|
| `home` | `home/src/lib/home/components/` (cta, benefits, faq, features, floating-bg) | Home Tab |
| `login` | `login/src/lib/login/login.component.html` | Sign In |
| `register` | `register/src/lib/register/` | Sign Up |
| `confirm-email` | `confirm-email/src/lib/` | Email Verify |
| `forgot-password` | `forgot-password/src/lib/` | Forgot Password + Confirmation + Reset |
| `services-catalog` | `services-catalog/src/lib/` | Services tab content / Book Step 1 |
| `order-wizard` | `order-wizard/src/lib/order-wizard/` | Booking Wizard (5 steps) |
| `checkout` | `checkout/src/lib/` | Payment Sheet |
| `orders` | `orders/src/lib/orders/` + `order-detail/` + `track-order/` | Orders List + Order Detail + Track Order |
| `disputes` | `disputes/src/lib/` | Disputes List + Dispute Thread |
| `profile` | `profile/src/lib/` | Profile Home + Edit Profile + Addresses + Payment Methods |
| `legal-pages` | `legal-pages/src/lib/` | Legal & Privacy + Terms + Privacy Policy |
| `gdpr` | `gdpr/src/lib/` | GDPR data request |

**Mobile-only screens (no web equivalent):**
- Splash, Welcome Carousel (3 slides)
- Booking Success (web shows inline confirmation; mobile gets a dedicated celebratory screen)
- Preferences (web has a Settings page; mobile splits theme/language/notifications into one Preferences screen)
- Language Bottom Sheet
- Empty / Error / 404 templates

---

## Visual Language (locked from web app)

### Primary palette — Tailwind sky scale

Source: [`cleansia-preset.ts`](../../src/Cleansia.App/libs/shared/assets/src/lib/cleansia-preset.ts) (PrimeNG Aura preset)

| Role | Hex | Usage |
|---|---|---|
| primary-50 | `#F0F9FF` | Lightest tint, soft fills |
| primary-100 | `#E0F2FE` | Tag bg, glass surfaces |
| primary-200 | `#BAE6FD` | Subtle highlights, focus rings |
| primary-300 | `#7DD3FC` | Hover states |
| primary-400 | `#38BDF8` | **Dark-mode primary accent** (WCAG AA on slate-900) |
| primary-500 | `#0EA5E9` | Mid-tone, illustrations |
| **primary-600** | **`#0284C7`** | **Brand primary — buttons, CTAs, active states, links** |
| primary-700 | `#0369A1` | Brand secondary, top-bar title, pressed states |
| primary-800 | `#075985` | Headings on light surfaces |
| primary-900 | `#0C4A6E` | Deepest, dark-mode contrast text on light primary |
| primary-950 | `#082F49` | Edge cases |

### Semantic colors

| Role | Hex / pair |
|---|---|
| Success | `#22C55E` (green-500) bg / `#15803D` (green-700) text |
| Error | `#EF4444` (red-500) bg / `#B91C1C` (red-700) text |
| Warning / rating star | `#F59E0B` (amber-500) |

### Neutrals — Light (web tokens)

Source: [`styles.scss`](../../src/Cleansia.App/apps/cleansia.app/src/styles.scss)

| Role | Hex | CSS var |
|---|---|---|
| Page bg | `#F8FAFC` | `--surface-ground` |
| Card / surface | `#FFFFFF` | `--surface-card` |
| Surface variant (input fill, segmented track) | `#F1F5F9` (slate-100) | derived |
| Border | `#E2E8F0` (slate-200) | `--surface-border` |
| Text primary | `#0F172A` (slate-900) | `--text-color` |
| Text body | `#334155` (slate-700) | derived |
| Text secondary | `#64748B` (slate-500) | `--text-color-secondary` |
| Text muted (placeholder) | `#94A3B8` (slate-400) | derived |
| Top-bar brand title | `#0369A1` (primary-700) | `--p-primary-700` |

### Neutrals — Dark (web tokens, slate scale)

| Role | Hex | CSS var |
|---|---|---|
| Page bg | `#0F172A` (slate-900) | `--surface-ground` |
| Card / surface | `#1E293B` (slate-800) | `--surface-card` |
| Elevated surface | `#283548` | `--surface-300` |
| Border | `#334155` (slate-700) | `--surface-border` |
| Text primary | `#E2E8F0` (slate-200) | `--text-color` |
| Text secondary | `#94A3B8` (slate-400) | `--text-color-secondary` |
| Primary accent (dark) | `#38BDF8` (sky-400) — overrides `#0284C7` for WCAG AA | `--p-primary-color` |

### Typography

Source: [`index.html`](../../src/Cleansia.App/apps/cleansia.app/src/index.html) line 25

```html
<link href="https://fonts.googleapis.com/css2?family=Nunito:wght@400;600;700&family=Poppins:wght@500;600;700&display=swap" />
```

- **Headings: Poppins** — weights 500 / 600 / 700
- **Body: Nunito** — weights 400 / 600 / 700
- **Body default style:** `body { font-family: 'Nunito', sans-serif; font-weight: 400; }` — confirmed in [`index.html`](../../src/Cleansia.App/apps/cleansia.app/src/index.html) line 84-87
- **System fallback:** `system-ui, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif`

### Type scale (mobile-tuned, sized down ~10% from web)

| Role | Face | Size | Line height | Tracking | Weight |
|---|---|---|---|---|---|
| Display Large (greeting, order #) | Poppins | 32sp | 40sp | -0.5px | 700 |
| Display (hero titles) | Poppins | 28sp | 36sp | -0.4px | 700 |
| Headline Medium (section headers) | Poppins | 22sp | 28sp | -0.2px | 600 |
| Headline Small (card titles) | Poppins | 18sp | 24sp | 0 | 600 |
| Title Large (list-item titles) | Nunito | 16sp | 22sp | 0 | 700 |
| Title Medium (list-item secondary) | Nunito | 15sp | 22sp | 0 | 700 |
| Body Large (paragraphs, field text) | Nunito | 16sp | 24sp | 0 | 400 |
| Body Medium (secondary copy) | Nunito | 14sp | 20sp | 0 | 400 |
| Label Large (button text, field labels) | Nunito | 14sp | 20sp | 0 | 700 |
| Label Small (uppercase badges, captions) | Nunito | 12sp | 16sp | 0.6px | 700 |

### Shape scale

PrimeNG Aura uses `border-radius: var(--p-border-radius-md)` ≈ 6px on most components. Mobile gets a more rounded, modern feel:

| Token | Value | Usage |
|---|---|---|
| xs | 6dp | Inline chips, tags |
| sm | 12dp | Inputs, small buttons |
| md | 16dp | Standard cards (default) |
| lg | 24dp | Hero cards, section cards |
| xl | 32dp | Bottom sheets, large feature cards |
| pill | 9999dp | CTAs, badges, FAB, bottom nav |

### Elevation / Shadows

Web uses subtle shadows from Aura preset. Mobile uses Material 3 elevation tokens that approximate them:

| Tier | Material elevation | Approx visual |
|---|---|---|
| Card subtle | 1dp | `0 1px 2px rgba(0,0,0,0.05)` |
| Card elevated | 4dp | `0 4px 8px rgba(0,0,0,0.08)` |
| Modal / sheet | 8dp | `0 8px 16px rgba(0,0,0,0.12)` |
| Floating nav | 16dp | `0 16px 24px rgba(2,132,199,0.15)` |

### Iconography

- **PrimeIcons on web** (`pi-*`). On mobile use **Material Symbols Outlined** with the same metaphor mapping:
  - `pi-home` → `Icons.Outlined.Home`
  - `pi-calendar-plus` → `Icons.Outlined.EventAvailable`
  - `pi-inbox` → `Icons.Outlined.Inbox`
  - `pi-verified` → `Icons.Outlined.Verified`
  - `pi-heart` → `Icons.Outlined.FavoriteBorder`
  - `pi-sparkles` → `Icons.Outlined.AutoAwesome`
  - `pi-arrow-right` → `Icons.AutoMirrored.Outlined.ArrowForward`
  - `pi-user` → `Icons.Outlined.Person`
  - `pi-shopping-cart` → `Icons.Outlined.ShoppingCart`
  - `pi-credit-card` → `Icons.Outlined.CreditCard`
- Star ratings always filled amber `#F59E0B`
- Flag icons for language picker — use `flag-icons` style (rounded-rect)

### Mascot illustrations

Located at [`src/Cleansia.App/apps/cleansia.app/src/assets/images/mascot/`](../../src/Cleansia.App/apps/cleansia.app/src/assets/images/mascot/):

| File | Used on |
|---|---|
| `mascot-waving.png` | Sign In, Welcome Carousel slide 1 |
| `mascot-cleaning.png` | Welcome Carousel slide 2 alternate, empty home |
| `mascot-leaning.png` | Empty states (Orders, Addresses) |
| `mascot-mopping.png` | Welcome Carousel slide 3 |
| `mascot-ready.png` | Booking Success |
| `mascot-idea.png` | Onboarding tips, "did you know" hints |

**Implementation:** copy these into `app/src/main/res/drawable-nodpi/` (Android handles density scaling for nodpi vector-like PNGs). Use `Image(painterResource(R.drawable.mascot_waving), ...)` in Compose. Names converted to lowercase snake_case.

---

## Web → Mobile translation rules

The web is desktop-first with a top nav bar, sidebars, and dropdowns. Mobile keeps the **brand DNA** (palette, fonts, component shapes, iconography) but **redesigns layouts** for mobile-native patterns:

| Web pattern | Mobile pattern |
|---|---|
| Top horizontal nav with links | Top app bar with brand mark + back/back-arrow |
| No bottom nav (pages routed in main area) | Floating bottom tab bar (4 tabs: Home / Book / Orders / Profile) |
| Centered card forms (login, register) | Full-screen forms with mascot above the fold |
| Dropdowns for selects | Bottom sheets |
| Modals | Bottom sheets (preferred) or full-screen sub-pages |
| Tables for orders list | Stacked order cards with skeleton loading |
| Inline calendar | Full-screen month calendar with grouped time slots |
| Stripe Checkout redirect | Native Apple Pay / Google Pay sheet first, card form below |
| Hover states | Pressed states + ripple |
| `cleansia-button` raised | `Button` with `tonalElevation = 1.dp` and pill shape |

**Animation language:** subtle. Slide-up bottom sheets, fade-in for skeleton-to-content swap, spring scale on press. No flashy transitions.

---

## Screen Inventory (32 screens total)

Each screen below will be:
1. Designed first as a markdown sketch in this doc (or in [customer-app-implementation.md](customer-app-implementation.md) if it gets long)
2. Translated to Kotlin Compose using primitives from `ui/components/`
3. Reviewed on emulator before moving on

Status legend: 🌐 has web equivalent · 📱 mobile-only

### Auth & Onboarding (8 screens)

| # | Screen | Source | Notes |
|---|---|---|---|
| 1 | Splash | 📱 | Brand mark + sky gradient bg, auto-advance after 1.2s |
| 2 | Welcome Carousel 1 | 📱 | `mascot-waving.png`, "Spotless homes, on your schedule", soft sky orb backdrop |
| 3 | Welcome Carousel 2 | 📱 | `mascot-cleaning.png`, "Trustworthy professionals", rating + verified inline pills |
| 4 | Welcome Carousel 3 | 📱 | `mascot-mopping.png`, "Pay your way", payment-cards visual |
| 5 | Sign In | 🌐 [`login.component.html`](../../src/Cleansia.App/libs/cleansia-customer-features/login/src/lib/login/login.component.html) | Mascot above form, brand-name top, email + password (float-labels), remember me + forgot password row, Login primary, OR divider, Google btn, "Don't have an account? Register" link |
| 6 | Sign Up | 🌐 `register/` | Mirror Sign In structure with name/email/password/phone/terms checkbox |
| 7 | Email Verify | 🌐 `confirm-email/` | 6-digit code input, resend timer, mascot-ready pose above |
| 8 | Forgot Password | 🌐 `forgot-password/` | Email entry → confirmation card → reset password form (3 sub-states or 3 screens) |

### Main app — bottom-tab host (4 tabs)

Tabs: **Home · Book · Orders · Profile**

#### Home tab (1 screen, multiple sections)

| # | Screen | Source | Notes |
|---|---|---|---|
| 9 | Home | 🌐 [`home/components/`](../../src/Cleansia.App/libs/cleansia-customer-features/home/src/lib/home/components/) | Greeting + upcoming booking card + "Book a Cleaning" CTA card + service grid + benefits row + testimonials + FAQ accordion |

#### Book tab → Wizard (6 screens including success)

| # | Screen | Source | Notes |
|---|---|---|---|
| 10 | Book Step 1: Services | 🌐 `services-catalog/` + `order-wizard/` | Multi-select service cards (Standard / Deep / Move-out / Office) |
| 11 | Book Step 2: Property | 🌐 `order-wizard/` | Bedroom + bathroom steppers |
| 12 | Book Step 3: Schedule | 🌐 `order-wizard/` | Month calendar + Morning/Afternoon/Evening grouped slots |
| 13 | Book Step 4: Address | 🌐 `order-wizard/` | Street/city/zip form + GPS button + map preview |
| 14 | Book Step 5: Extras & Summary | 🌐 `order-wizard/` | Tailor extras + textareas + price summary card |
| 15 | Payment | 🌐 `checkout/` | Apple Pay / Google Pay buttons up top, card form below, total sticky bottom |
| 16 | Booking Success | 📱 | Gradient checkmark + Order #, mascot-ready, "View Order" + "Book Another" CTAs |

#### Orders tab (3 screens)

| # | Screen | Source | Notes |
|---|---|---|---|
| 17 | Orders List | 🌐 [`orders.component.html`](../../src/Cleansia.App/libs/cleansia-customer-features/orders/src/lib/orders/orders.component.html) | Hero (title + subtitle) + segmented tabs (Upcoming/Past/Cancelled) + order cards. Empty state: 3-step quick guide + trust badges + Book CTA. Skeleton: 3× 8rem cards. |
| 18 | Order Detail | 🌐 `order-detail/` | Status timeline + service breakdown w/ icons + cleaner card + map + payment status + Report Issue |
| 19 | Track Order | 🌐 `track-order/` | Order # + email entry → live status (no Contact Cleaner / Report Issue, no nav chrome) |

#### Profile tab (5 screens)

| # | Screen | Source | Notes |
|---|---|---|---|
| 20 | Profile Home | 🌐 `profile/` | Avatar + name card, menu list, sign out |
| 21 | Edit Profile | 🌐 `profile/` | Name + email + phone form |
| 22 | Saved Addresses | 🌐 `profile/` | List + add button, empty state w/ mascot |
| 23 | Payment Methods | 🌐 `profile/` | Saved cards + Apple/Google Pay toggles |
| 24 | Preferences | 📱 | Language (sheet) + Theme (Light/Dark/System) + Notifications toggles |

#### Disputes (under Orders)

| # | Screen | Source | Notes |
|---|---|---|---|
| 25 | Disputes List | 🌐 `disputes/` | Active/Resolved tabs, dispute cards, empty state |
| 26 | Dispute Thread | 🌐 `disputes/` | Chat bubbles, attachment thumbnails, sticky composer |

#### Legal (under Profile)

| # | Screen | Source | Notes |
|---|---|---|---|
| 27 | Legal & Privacy | 🌐 `legal-pages/` | Menu of Terms / Privacy / Cookie / GDPR / Delete account |
| 28 | GDPR data request | 🌐 `gdpr/` | Form to request user data export / deletion |

#### Mobile-only utility (4 screens)

| # | Screen | Source | Notes |
|---|---|---|---|
| 29 | Language Bottom Sheet | 📱 | EN / CS / SK / UK / RU with flag icons, drag-to-dismiss |
| 30 | Empty State (template) | 📱 | Mascot-leaning + title + subtitle + CTA, used by Orders/Addresses/Payment Methods/Disputes |
| 31 | Error State (template) | 📱 | Mascot-idea + "Something went wrong" + Try Again + Contact Support |
| 32 | 404 / Not Found | 📱 | Mascot + "This page packed its bags" + Back home |

**Total: 32 screens.** Each gets light + dark mode (one composable per screen with theme-aware tokens; no separate dark frames needed).

---

## Component Library

Build these primitives in `app/src/main/java/cz/cleansia/customer/ui/components/`. **One file per component.** All must be theme-aware (light + dark) via `MaterialTheme.colorScheme`.

### Atoms

- **CleansiaButton** — Primary / Secondary / Outlined / Text / Apple / Google variants. Pill shape. Sizes Sm/Md/Lg.
- **CleansiaTextField** — float-label variant matching web's `cleansia-text-input` (label sits above when focused/filled, fades down to placeholder when empty). Helper + error states.
- **CleansiaTextArea** — multi-line variant of TextField, 24dp radius
- **CleansiaCheckbox** — square with rounded corners, primary fill on check
- **CleansiaSelect** — opens bottom sheet, not a dropdown
- **CleansiaCodeInput** — 6 cells for verification
- **CleansiaPhoneInput** — country dial-code + number
- **CleansiaDatePicker** — full-screen calendar
- **CleansiaTimeSlotGrid** — 4-col pills with disabled state
- **CleansiaAvatar** — circle, photo or initials
- **CleansiaBadge** — status pill (6 variants for order lifecycle)
- **CleansiaChip** — selectable tag
- **CleansiaRatingStars** — read-only + interactive

### Molecules

- **CleansiaCard** — surface card, 16dp default radius, 1dp elevation
- **CleansiaHeroCard** — gradient bg variant for "Book a Cleaning" CTA
- **CleansiaOrderCard** — avatar + title + cleaner + date + status badge + price + Details button
- **CleansiaServiceCard** — bento-style with circular icon badge top-left
- **CleansiaPriceLine** — label + value row
- **CleansiaPriceSummaryCard** — line items + subtotal/tax/total
- **CleansiaSectionHeader** — title + optional "See all" link
- **CleansiaInfoRow** — icon + label + value (used in detail screens)
- **CleansiaSegmentedTabs** — pill segmented control
- **CleansiaStepIndicator** — wizard progress bar
- **CleansiaEmptyState** — mascot + title + subtitle + CTA
- **CleansiaSkeleton** — shimmer placeholder

### Organisms

- **CleansiaTopAppBar** — translucent surface, brand mark or back arrow + title, optional right action
- **CleansiaBottomNav** — floating pill, 4 tabs, active = filled circle
- **CleansiaBottomSheet** — wrapper for ModalBottomSheet with drag handle
- **CleansiaSnackbar** — top-aligned with accent stripe
- **CleansiaOrderTimeline** — vertical 4-node status timeline
- **CleansiaMapPreview** — static map tile with center pin (placeholder until Google Maps SDK is added)
- **CleansiaDynamicBackground** — animated gradient backdrop matching web's `cleansia-dynamic-background` (used on auth screens)

### Reusable patterns

- **Float-label TextField** — match the web's `cleansia-text-input [floatVariant]="'on'"` pattern. Placeholder slides up to become the label on focus/fill.
- **3-step quick-guide row** (used in Orders empty state) — numbered circles + step text + chevron arrows
- **Trust badges row** — icon + text triplets ("Verified", "Satisfied", "Eco")

---

## Mobile-specific additions

Patterns the web doesn't have but mobile needs:

1. Bottom tab bar (replaces web's top nav)
2. Native payment sheets (Apple Pay / Google Pay) — visually prominent above the credit-card form
3. Push notifications — register device token via `/api/v1/notifications/register`
4. Biometric unlock (FaceID / TouchID) — optional toggle in Preferences
5. GPS address autofill — "Use current location" button on Address step
6. Pull-to-refresh on Orders list
7. Swipe actions on order cards (cancel, re-book)
8. Bottom sheets instead of modals (Language picker is canonical)
9. Haptic feedback on CTAs and step transitions
10. Native share sheet for receipt or referral

---

## Status colors (order lifecycle)

Match web's badge styling, mobile uses pill shape consistently:

| Status | Background | Text | Notes |
|---|---|---|---|
| Pending | `#E0F2FE` (primary-100) | `#0369A1` (primary-700) | Soft sky |
| Confirmed | `#0284C7` (primary-600) | `#FFFFFF` | Solid primary |
| In Progress | `#38BDF8` (primary-400) | `#0C4A6E` (primary-900) | Sky-light, no animation |
| Completed | `#DCFCE7` (green-100) | `#15803D` (green-700) | Soft green |
| Cancelled | `#F1F5F9` (slate-100) | `#64748B` (slate-500) | Soft gray, strikethrough on lists |
| Failed payment | `#FEE2E2` (red-100) | `#B91C1C` (red-700) | Soft red |

---

## Translations / i18n

Mobile reuses the web's i18n keys exactly. Source: [`src/Cleansia.App/apps/cleansia.app/src/assets/i18n/`](../../src/Cleansia.App/apps/cleansia.app/src/assets/i18n/) — `en.json`, `cs.json`, `sk.json`, `uk.json`, `ru.json`.

**Implementation:** convert each `i18n/*.json` to Android `res/values{-cs|-sk|-uk|-ru}/strings.xml` via a `gradle` task at scaffold time. Keys preserved verbatim (e.g. `pages.orders.hero_title`, `auth.login.email`).

When the web app updates a key, mobile updates next release. Don't edit translations manually — re-run the conversion task.

---

## Accessibility

- Minimum tap target: 44×44dp
- Text contrast: WCAG AA (4.5:1 for body, 3:1 for large)
- Every screen in light AND dark — derived from theme tokens, no hard-coded hexes in screens
- Icons paired with labels (no icon-only CTAs except close/back/chevron)
- Support font scaling — layouts must not break at 200%
- Focus ring (2dp primary) on all interactive elements

---

## Implementation order (proposed)

Build in this order so each screen unblocks the next:

1. **Tokens + theme + fonts** (Color.kt, Type.kt, Shape.kt, Theme.kt) → owner reviews on emulator with a sample screen
2. **Bottom nav + top app bar + nav graph skeleton** (4 empty tabs)
3. **Splash → Welcome (3 slides) → Sign In** (auth flow renders end-to-end)
4. **Sign Up + Email Verify + Forgot Password** (full auth)
5. **Home tab** (greeting, hero CTA, services grid, testimonials)
6. **Orders tab + Order Detail + Track Order**
7. **Booking Wizard (5 steps) + Payment + Success**
8. **Profile tab + Edit Profile + Addresses + Payment Methods + Preferences**
9. **Disputes (List + Thread)**
10. **Legal + GDPR**
11. **Empty / Error / 404 templates**
12. **Polish: skeleton states, pull-to-refresh, swipe actions, haptics**
13. **API wiring** — replace mock data with real OpenAPI-generated calls
14. **Push notifications + Apple Pay/Google Pay + Biometric**
15. **Firebase App Distribution upload**

Each step is reviewable before the next starts.

---

## Don't-do list (lessons learned)

- ❌ Don't use any color outside the sky scale + neutral slate scale + semantic green/red/amber. No `#006194`, no `#191C1D`, no Be Vietnam Pro fill colors.
- ❌ Don't use Plus Jakarta Sans, Be Vietnam Pro, Liberation Serif, Inter, or any font other than Poppins + Nunito.
- ❌ Don't design a sidebar nav — bottom tabs are mandatory.
- ❌ Don't use absolute-positioned status badges that overlap titles — badges sit in the top row as siblings.
- ❌ Don't skip the top app bar on Home / Orders / Profile.
- ❌ Don't ignore dark mode — every screen must derive from `MaterialTheme.colorScheme`.
- ❌ Don't hard-code strings in screens — use `stringResource(R.string.key)` from converted i18n.
- ❌ Don't write a dev-menu start destination — Splash is always start in production.
- ❌ Don't leave placeholder screens in the main user flow (Splash → Sign In → Home → Book → Success → Orders).

---

## Reference data

- **Web app source:** [`src/Cleansia.App/apps/cleansia.app/`](../../src/Cleansia.App/apps/cleansia.app/)
- **PrimeNG preset:** [`cleansia-preset.ts`](../../src/Cleansia.App/libs/shared/assets/src/lib/cleansia-preset.ts)
- **i18n source:** [`assets/i18n/`](../../src/Cleansia.App/apps/cleansia.app/src/assets/i18n/) — 5 locales
- **Mascot assets:** [`assets/images/mascot/`](../../src/Cleansia.App/apps/cleansia.app/src/assets/images/mascot/)
- **Customer API Swagger:** `http://localhost:5003/swagger/v1/swagger.json` (codegen via OpenAPI Gradle plugin into `cz.cleansia.customer.api`)
- **Order number formats:** `CL-XXXXX` (customer-facing), `CS-XXXX` (support)
- **Booking granularity:** 30-min slots, 09:00–20:00 local time
- **Order pipeline:** New → Pending → Confirmed → InProgress → Completed (or Cancelled)
- **Target:** Android phones 360–420dp wide. All designs at 390dp canvas.
- **Min SDK 26 / Target 35 / Compile 35.** Kotlin 2.1, Compose BOM 2025.02.00, AGP 8.9.1, Gradle 8.11.1.
