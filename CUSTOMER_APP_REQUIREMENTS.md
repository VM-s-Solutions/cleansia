# Cleansia Customer App — Complete Redesign Brief

## Purpose

This document is a comprehensive brief for redesigning the Cleansia customer-facing web application. It covers every existing page, all known bugs, specific user feedback, and detailed expansion ideas. The app operates as an **e-shop for cleaning services** — users can browse and order without registration.

---

## 1. Application Overview

| Property | Value |
|---|---|
| **Type** | Customer-facing web app (responsive, mobile-first) |
| **Purpose** | E-shop for cleaning services — browse, configure, and order cleaning online |
| **Languages** | Czech (primary), English, Polish |
| **Currency** | CZK (Czech Koruna) |
| **Payment** | Stripe (card) + cash on delivery |
| **Target audience** | Residential customers in Czech Republic seeking professional cleaning |
| **Access model** | **Anonymous ordering** (e-shop model) — no registration required to browse or order. Registration unlocks order history, profile, disputes, GDPR tools |
| **Contact** | Phone: +420 739 788 108, Email: info@cleansia.cz |
| **Social** | Instagram (`instagram.com/cleansia.cz`), Facebook (`facebook.com/cleansia`) |

---

## 2. Brand Identity

| Element | Current State |
|---|---|
| **Logo** | `Logo.webp` — house icon with cleaning sparkle + "Cleansia" text |
| **Primary color** | Cyan/teal (#0EA5E9 range — PrimeNG Cleansia preset) |
| **Mascot** | Cartoon cleaning guy (blue overalls, cap, mop + bucket) — used on landing page |
| **Font** | System default (no custom typography) |
| **Tone** | Professional, approachable, clean |
| **Tagline** | "Professional cleaning services at your doorstep" |

---

## 3. Tech Stack (for designer context)

- **Framework:** Angular 19+ (standalone components, signals)
- **UI Library:** PrimeNG + PrimeFlex utility CSS
- **State:** NgRx
- **i18n:** @ngx-translate with JSON files (EN, CS, PL)
- **Monorepo:** Nx with feature libraries per page

---

## 4. Site Map & Pages

### 4.1 Public Pages (no login required)

| Page | Route | Purpose |
|---|---|---|
| Landing Page | `/` | Marketing homepage with hero, how-it-works, benefits, services preview, testimonials, FAQ, footer |
| Services Catalog | `/services` | Full list of cleaning services + packages with pricing |
| Order Wizard | `/order` | 5-step booking wizard (services → contact/address → date/time → payment → summary) |
| Checkout Success | `/checkout/success` | Post-payment confirmation page |
| Checkout Cancel | `/checkout/cancel` | Stripe payment cancelled page |
| Login | `/login` | Email + password + Google OAuth |
| Register | `/register` | Name + email + password + terms agreement |
| Confirm Email | `/confirm-email` | 6-digit OTP code verification |
| Forgot Password | `/forgot-password` | Two-phase: email → code + new password |

### 4.2 Authenticated Pages (login required)

| Page | Route | Purpose |
|---|---|---|
| My Orders | `/orders` | Paginated list split into upcoming/past orders |
| Order Detail | `/orders/:id` | Full order info, status timeline, receipt download, report issue |
| Profile | `/profile` | Personal info tab + change password tab |
| Disputes | `/disputes` | List + create + detail dispute dialogs |
| Privacy & Data (GDPR) | `/gdpr` | Export data, manage consents, delete account |

### 4.3 Missing Routes (dead links in current footer)

| Route | Status |
|---|---|
| `/terms` | Not implemented — footer links to it but returns 404 |
| `/privacy` | Not implemented — footer links to it but returns 404 |

---

## 5. Page-by-Page Analysis & Redesign Requirements

### 5.1 Landing Page (`/`)

**Current state (from screenshots):**
- Hero section with mascot, title, two CTA buttons — looks good overall
- "How it works" 3-step cards with mascot peeking from top — **mascot overlaps the heading text and cards**, needs z-index/positioning fix
- Benefits section — **mascot image overlaps benefit text on the right column**, makes it unreadable
- Services & Pricing section — **shows ALL services and packages** (too many cards), should show just a curated preview
- Testimonials carousel — works but could look more premium
- FAQ accordion — functional
- Footer (landing-specific) — cyan background, contact info, quick quote form, social links

**Redesign requirements:**

1. **Mascot positioning** — The mascot character images must not overlap text content. They should be decorative accents that complement the layout, not obscure it. Consider placing them in dedicated image columns or as subtle background elements.

2. **Services preview section** — Currently dumps ALL services/packages into a grid. Instead:
   - Show 3-4 featured/popular services only
   - Show 2-3 featured packages only
   - Add a clear "Explore All Services" CTA button (already exists, route `/services`)
   - Make the preview cards interactive and visually appealing (hover effects, etc.)

3. **Counter section** — The "2733 cleanings completed" counter animation is neat but the implementation has a bug (triggers on any scroll). The counter should be tied to actual scroll-into-view.

4. **Footer** — Year is hardcoded as `2025`, should use dynamic year. The quick quote form doesn't actually send data anywhere — consider either connecting it to an API or removing it.

5. **Hardcoded Czech "od"** — The "od" (from) prefix before prices is hardcoded, should use i18n key.

---

### 5.2 Services Catalog (`/services`)

**Current state (from screenshot):**
- Shows `pages.services.title` and `pages.services.subtitle` as raw translation keys (missing translations)
- Packages displayed as cards in a horizontal row with "Most Popular" badge on the middle one
- Package cards have **hardcoded English** feature bullets: "Premium quality cleaning", "Vetted professionals", "100% satisfaction guarantee"
- Package cards show **"per cleaning"** — hardcoded English
- Service cards are plain with an arrow button
- On the left edge, some content is cut off (looks like a "Popular" badge is partially visible off-screen)

**Redesign requirements:**

1. **Fix missing translations** — `pages.services.title`, `pages.services.subtitle`, `pages.services.from`, `pages.services.per_room`, `pages.services.book_now`, `pages.services.no_services` are all missing from i18n files

2. **Replace hardcoded English** — Package feature bullets and "per cleaning" label must use i18n keys

3. **Differentiate services vs packages visually** — Currently they look too similar. Ideas:
   - Packages: premium card design with gradient headers, included-services list, "best value" indicators
   - Services: simpler cards but with clear pricing structure (base + per-room)
   - Consider a tabbed or segmented layout: "Packages" tab vs "Individual Services" tab

4. **Better mobile layout** — Package cards shouldn't overflow horizontally. Consider a vertical stack or carousel on mobile.

5. **Each card should have a "Book Now" CTA** that pre-selects that service/package in the order wizard

6. **Add filtering/sorting** — By price, by type, alphabetically

---

### 5.3 Order Wizard (`/order`)

**Current state:**
- 5-step wizard using PrimeNG `p-steps`
- Step 0 (Services): service/package selection cards with checkboxes, room/bathroom counters, sticky price calculator at bottom
- Step 1 (Contact & Address): contact fields shown for anonymous users, address fields
- Step 2 (Date & Time): date picker + time dropdown (30-min slots, 07:00-20:00)
- Step 3 (Payment): card/cash radio buttons + special instructions textarea
- Step 4 (Summary): review all selections before submitting

**Known bugs:**
- **Summary step shows wrong service/package names** — uses `facade.services()[0]` instead of finding by actual selected ID. Always shows the first item regardless of selection.
- No validation feedback when required fields are empty (button is just disabled with no explanation)

**Redesign requirements:**

1. **Calculator/Price estimator** — The current sticky calculator at the bottom of step 0 is basic. Complete redesign:
   - Should be a persistent sidebar (desktop) or expandable bottom sheet (mobile)
   - Show line-item breakdown: each selected service with its price, per-room surcharges, package discounts
   - Live-updating as user changes selections, rooms, bathrooms
   - Show estimated cleaning duration based on selected services
   - Make it feel like a shopping cart summary

2. **Fix the summary bug** — Step 4 must look up services/packages by their actual IDs, not always show `[0]`

3. **Add validation messages** — Tell the user what's missing when "Next" is disabled

4. **Progress indicator** — The `p-steps` component works but could be more visual. Consider showing a progress bar with completion percentage.

5. **Pre-selection support** — When navigating from a service card's "Book Now" button, that service should be pre-selected in step 0

---

### 5.4 Orders Page (`/orders`)

**Current state:**
- Simple card list split into "Upcoming" / "Past" sections
- Missing translations: `pages.orders.upcoming_orders`, `pages.orders.past_orders`
- "Rebook" button uses literal `'Rebook' | translate` — broken translation
- Cards show: order number, status tag, date, address, total, payment icon
- Paginator at bottom

**Redesign requirements:**

1. **Move into Profile section** — The user wants orders to be a sub-page of a profile/account area, not a standalone top-level page. Create an **Account Dashboard** layout with sidebar navigation:
   - My Orders (default tab)
   - Profile Settings
   - My Disputes
   - Privacy & Data (GDPR)

2. **Order cards** — Make them more informative:
   - Add service/package names (not just order number)
   - Show a visual status progress bar (Pending → Confirmed → In Progress → Completed)
   - Add quick actions: View Detail, Download Receipt, Rebook

3. **Filtering** — Add status filter tabs (All, Pending, Confirmed, In Progress, Completed, Cancelled)

4. **Fix missing translations**

---

### 5.5 Order Detail (`/orders/:id`)

**Current state:**
- Breadcrumb with back button
- Header with order number + action buttons (Download Receipt, Report Issue)
- Status + payment info section
- Service details grid
- Address section
- Timeline
- Notes section

**Known issues:**
- `nav.orders` translation key missing (breadcrumb uses wrong key)
- Receipt download may fail silently if `receiptNumber` is null

**Redesign requirements:**

1. **Cleaner layout** — Use a card-based layout with clear sections
2. **Status timeline** — Make it more visual with colored progress steps
3. **Photos section** — The API supports order photos (before/after cleaning) — display them in a gallery
4. **Live status** — Consider showing real-time status updates if the order is in progress

---

### 5.6 Profile Page (`/profile`)

**Current state:**
- Two tabs: Personal Info + Change Password
- Fields: email (read-only), first name, last name, phone, birth date
- Password change uses `code: ''` — may fail since it's the same endpoint as forgot-password

**Redesign requirements:**

1. **Convert to Account Dashboard** — As mentioned in 5.4, create a unified account area:
   ```
   /account
   ├── /account/orders       (My Orders)
   ├── /account/profile      (Personal Info + Password)
   ├── /account/disputes     (My Disputes)
   └── /account/privacy      (GDPR / Data)
   ```

2. **Profile page enhancements:**
   - Profile photo upload
   - Preferred language selection (with save)
   - Address book (save multiple addresses for quick order placement)
   - Notification preferences (email notifications toggle)

3. **Fix password change** — The profile password change should use a different flow than forgot-password (should require current password, not a reset code)

---

### 5.7 Disputes Page (`/disputes`)

**Current state:**
- Table with: Order, Reason, Status, Created date, View button
- Create dialog: Order ID (free text!), Reason (hardcoded English dropdown), Description
- Detail dialog: reason, status, description, message thread, reply box

**Known issues:**
- Order ID is a free-text input — user must know/paste UUID
- Dispute reason options are hardcoded English, not translated

**Redesign requirements:**

1. **Move into Account Dashboard** (see 5.6)
2. **Order selector** — Replace free-text Order ID with a dropdown of the user's orders
3. **Translate dispute reasons** — Use i18n keys
4. **Better message thread UI** — Chat-bubble style with timestamps, staff vs customer visual distinction
5. **File attachments** — Allow photo uploads as evidence

---

### 5.8 GDPR Page (`/gdpr`)

**Current state:**
- Three sections: Export Data, Consent Management, Delete Account
- 4 consent toggles: Terms of Service, Privacy Policy, Marketing Emails, Data Processing

**Known issues — many missing translations:**
- `pages.gdpr.export_description`
- `pages.gdpr.consents_description`
- `pages.gdpr.delete_description`
- `pages.gdpr.consent_updated`
- `pages.gdpr.consent_types.*` (4 keys)
- `pages.gdpr.delete_confirm_message`, `delete_confirm_title`, `delete_confirm_yes`

**Redesign requirements:**

1. **Move into Account Dashboard** (see 5.6)
2. **Fix all missing translations**
3. **Add clear explanations** for each consent type
4. **Dangerous zone styling** — Delete account section should have a clear warning design (red accent, confirmation steps)

---

### 5.9 Checkout Success/Cancel Pages

**Current state:**
- Simple centered icon + title + message + navigation links
- Inline templates (no separate HTML files)

**Known issues:**
- Translation keys use wrong nesting level (`pages.checkout.success.title` vs actual `pages.checkout.success_title`)

**Redesign requirements:**

1. **Success page enhancements:**
   - Show order confirmation number
   - Show order summary (what was booked, when, where)
   - Clear next steps: "You'll receive a confirmation email"
   - For anonymous users: "Create an account to track your order" CTA
   - "Book Another Cleaning" button

2. **Cancel page:**
   - Reassure user (no charges made)
   - "Try Again" + "Contact Support" options

---

### 5.10 Login / Register / Forgot Password

**Current state:**
- Clean card-based design with floating cleaning icons background (`cleansia-dynamic-background`)
- Brand logo at top
- Language switcher
- Login: email + password + remember me + forgot password link + Google OAuth
- Register: name + email + password with live validation checklist + terms checkbox

**Known issues:**
- `auth.login.dont_have_account` key mismatch (template uses different key than json)
- `pages.forgot_password.email_input.dont_have_account` missing

**Redesign requirements:**

1. **Overall good** — The auth pages are the best-designed part of the app currently
2. **Fix translation key mismatches**
3. **Consider social login expansion** — Apple Sign-In, Facebook Login
4. **After login redirect** — Anonymous users who log in mid-order should return to their order wizard state

---

### 5.11 Navigation Bar

**Current state:**
- 64px sticky top bar
- Desktop: Logo + links (Home, Services, My Orders, Book Now) + language switcher + auth buttons/icons
- Mobile: hamburger menu with dropdown overlay
- Login/Register shown as text buttons (logged out) or icon links (logged in)

**User feedback:** "I don't see where I can login/register/logout, no profile button"

**Redesign requirements:**

1. **More prominent auth controls** — The current icon-only profile/logout buttons are hard to find. Replace with:
   - **Logged out:** Clear "Log In" and "Sign Up" buttons (keep but make more visible)
   - **Logged in:** User avatar/initial circle with dropdown menu containing: My Account, My Orders, Profile, Log Out

2. **Always-visible "Book Now" CTA** — Primary colored button that stands out in the navbar

3. **Language switcher** — Currently overlaps other elements. Needs proper positioning and size constraints.

4. **Mobile menu** — Improve with slide-in drawer instead of dropdown overlay

5. **Breadcrumbs** — Consider adding breadcrumbs below navbar for inner pages

---

### 5.12 Footer

**Current state — TWO footers exist:**
1. **App-shell footer** (every page): minimal — "Terms of Service" + "Privacy Policy" links (both broken/404) + copyright
2. **Landing page footer** (homepage only): rich cyan footer with contacts, quote form, social links

**User feedback:** "The footer looks poor, I want a complete redesign"

**Redesign requirements:**

1. **Single unified footer** for all pages (replace both current footers):
   - Company info: logo, tagline, brief description
   - Quick links: Home, Services, Book Now, FAQ
   - Legal: Terms of Service, Privacy Policy, GDPR (create actual `/terms` and `/privacy` routes)
   - Contact: phone, email, business hours
   - Social media: Instagram, Facebook
   - Newsletter signup (optional — currently the quote form doesn't send data anyway)
   - Copyright with dynamic year
   - Language selector (alternative position)

2. **Mobile footer** — Stack columns vertically, collapsible sections

---

## 6. Global Issues to Fix

### 6.1 Missing Translations (30+ keys)

**Services catalog:**
- `pages.services.title`, `pages.services.subtitle`, `pages.services.from`, `pages.services.per_room`, `pages.services.book_now`, `pages.services.no_services`

**Orders:**
- `pages.orders.upcoming_orders`, `pages.orders.past_orders`, `nav.orders`

**GDPR:**
- `pages.gdpr.export_description`, `pages.gdpr.consents_description`, `pages.gdpr.delete_description`, `pages.gdpr.consent_updated`, `pages.gdpr.consent_types.terms_of_service`, `pages.gdpr.consent_types.privacy_policy`, `pages.gdpr.consent_types.marketing_emails`, `pages.gdpr.consent_types.data_processing`, `pages.gdpr.delete_confirm_message`, `pages.gdpr.delete_confirm_title`, `pages.gdpr.delete_confirm_yes`

**Checkout:**
- `pages.checkout.success.title`, `pages.checkout.success.description`, `pages.checkout.success.view_orders`, `pages.checkout.cancel.title`, `pages.checkout.cancel.description`, `pages.checkout.cancel.try_again`, `pages.checkout.cancel.view_orders`

**404 page:**
- `global.actions.go_back`, `global.actions.go_home`

**Auth:**
- `auth.login.dont_have_account` (template expects this but json has `auth.login.no_account`)
- `pages.forgot_password.email_input.dont_have_account`

### 6.2 Hardcoded Strings (not translated)

- Landing page: Czech `"od"` before service prices
- Services catalog: "Premium quality cleaning", "Vetted professionals", "100% satisfaction guarantee"
- Services catalog: "per cleaning"
- Orders: `'Rebook'` used as translation key (will show raw "Rebook")
- Disputes: Reason options in create dialog
- Landing footer: `© 2025` hardcoded year

### 6.3 Logic Bugs

1. **Order wizard summary** — `facade.services()[0]` always shows first service name regardless of which were selected
2. **Profile change password** — Uses empty `code` in `ChangePasswordCommand`, likely fails
3. **Landing page counter** — `@ViewChild('counter')` declared but `#counter` not in template; counter triggers on any scroll
4. **App footer** — `/terms` and `/privacy` links go to 404 (routes don't exist)

---

## 7. New Features & Expansion Ideas

### 7.1 Account Dashboard (Priority: High)

Create a unified account area at `/account` with sidebar navigation:
- **Overview/Dashboard** — Quick stats: total orders, upcoming cleanings, account status
- **My Orders** — Current orders page moved here
- **Profile** — Personal info, addresses, preferences
- **Disputes** — Current disputes page moved here
- **Privacy & Data** — Current GDPR page moved here
- **Notifications** — Email notification preferences
- **Payment Methods** — Saved cards (Stripe customer portal)

### 7.2 Address Book (Priority: Medium)

Allow users to save multiple addresses:
- Home, Office, Parents' place, etc.
- Quick-select in order wizard instead of typing address every time
- Default address auto-fills

### 7.3 Reorder / Rebook (Priority: Medium)

- One-click reorder from any past order
- Pre-fills the order wizard with same services, address, and preferences
- Only needs date/time selection

### 7.4 Order Tracking (Priority: Medium)

- Real-time status updates on the order detail page
- Push/email notifications on status changes
- Assigned cleaner info (name, photo) when available
- Before/after cleaning photos gallery

### 7.5 Reviews & Ratings (Priority: Medium)

- Post-cleaning rating prompt (1-5 stars + comment)
- Display reviews on service catalog cards
- Average rating badges on landing page

### 7.6 Loyalty Program (Priority: Low)

- Points earned per order
- Discount codes / referral program
- "Frequent cleaner" tiers

### 7.7 Subscription / Recurring Orders (Priority: Low)

- Weekly/bi-weekly/monthly recurring cleaning
- Discounted pricing for subscriptions
- Pause/resume subscription

### 7.8 Live Chat / Support (Priority: Low)

- In-app chat widget for quick support
- Could integrate with existing dispute system

### 7.9 Blog / Tips Section (Priority: Low)

- Cleaning tips and tricks articles
- SEO content for organic traffic
- Seasonal cleaning guides

### 7.10 Terms of Service & Privacy Policy Pages (Priority: High)

- Create actual `/terms` and `/privacy` pages (currently 404)
- Required for legal compliance
- GDPR consent links should point to these

---

## 8. Design Direction Notes

### What's working well:
- Auth pages (login/register) — clean, modern card design with animated background
- Landing page hero section — good CTA hierarchy
- Color scheme — cyan/blue feels clean and appropriate for a cleaning service
- Mascot character — adds personality (but needs positioning fixes)

### What needs the most work:
1. **Footer** — Complete redesign, unify into one rich footer for all pages
2. **Services catalog** — Needs visual differentiation between services and packages, fix translations
3. **Order calculator** — Rethink as a smart price estimator with breakdown
4. **Navigation** — Auth controls need to be more visible; add user dropdown menu
5. **Account area** — Consolidate orders/profile/disputes/GDPR into single account section
6. **Mascot images** — Fix z-index/positioning so they don't cover content

### Design constraints:
- Must work in 3 languages (CS, EN, PL) — Czech texts are typically 20-30% longer than English
- PrimeNG components are the UI foundation — custom styling should work with, not against them
- PrimeFlex utility classes are used for layout — responsive breakpoints: sm(576), md(768), lg(992), xl(1200)
- All text must come from i18n JSON files — no hardcoded strings in templates

---

## 9. Pages Summary

| # | Page | Priority | Status | Key Issues |
|---|---|---|---|---|
| 1 | Landing Page | High | Needs fixes | Mascot overlaps, too many services shown, hardcoded strings |
| 2 | Services Catalog | High | Broken | Missing translations, hardcoded English, cards look same |
| 3 | Order Wizard | High | Needs redesign | Calculator redesign, summary bug, no validation feedback |
| 4 | Checkout Success | Medium | Broken translations | Wrong i18n key nesting, no order summary shown |
| 5 | Checkout Cancel | Medium | Broken translations | Wrong i18n key nesting |
| 6 | My Orders | High | Needs redesign | Move to account section, fix translations, improve cards |
| 7 | Order Detail | Medium | Works | Minor translation fix needed |
| 8 | Profile | High | Needs redesign | Merge into account dashboard, fix password change |
| 9 | Disputes | Medium | Needs fixes | Hardcoded reasons, free-text order ID |
| 10 | GDPR | Medium | Many missing translations | 11+ missing translation keys |
| 11 | Login | Low | Good | Minor key mismatch |
| 12 | Register | Low | Good | Works well |
| 13 | Forgot Password | Low | Mostly good | 1 missing translation |
| 14 | Confirm Email | Low | Good | Works |
| 15 | Navbar | High | Redesign | Auth buttons hard to find, language switcher overlaps |
| 16 | Footer | High | Redesign | Two footers, poor design, broken links |
| 17 | Terms of Service | High | Missing | Route doesn't exist (404) |
| 18 | Privacy Policy | High | Missing | Route doesn't exist (404) |
| 19 | Account Dashboard | High | New | Proposed new unified account area |
