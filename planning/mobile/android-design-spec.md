# Cleansia Partner — Android App Design Specification

> For use with Stitch AI or similar design generation tools.
> App purpose: A cleaning service marketplace partner app for cleaners/cleaning companies to manage orders, track earnings, and handle their business profile.

---

## App Identity

- **Name:** Cleansia Partner
- **Platform:** Android (Material 3)
- **Theme:** Light/Dark mode (system-aware)
- **Primary Color:** #3B82F6 (blue)
- **Languages:** English, Czech, Slovak, Ukrainian, Russian
- **Corner Radius:** 12dp default, 8dp for small elements, 16dp for cards
- **Typography:** System default with HeadlineLarge, HeadlineSmall, BodyLarge, BodyMedium, LabelMedium hierarchy

---

## SCREEN 1: Onboarding (First Launch)

**Layout:** Full-screen horizontal pager with bottom navigation dots

**Pages (7 slides):**
1. Dashboard — "Track your earnings and schedule at a glance"
2. Orders — "Browse and accept cleaning jobs near you"
3. Invoices — "Automatic invoicing and payment tracking"
4. Analytics — "Detailed performance insights and trends"
5. Profile — "Set up your business identity and availability"
6. Notifications — "Real-time updates on new orders"
7. Availability — "Set your weekly working schedule"

**UI Elements:**
- Centered icon illustration per page (cleaning-themed)
- Title text (headline)
- Description text (body, secondary color)
- Page indicator dots at bottom
- "Skip" text button (top-right)
- "Get Started" primary button on last page

**Navigation:** Skip → Login; Complete → Login

---

## SCREEN 2: Login

**Layout:** Centered card on dynamic background

**Elements:**
- App logo + "Cleansia" brand name at top
- Email text field with email keyboard
- Password field with visibility toggle
- "Remember me" checkbox
- Biometric auth button (fingerprint icon, shown conditionally)
- Primary "Login" button (full width)
- "Forgot password?" text link
- "Don't have an account? Register" text link

**Interactions:** Biometric auth animates fingerprint icon on scan

---

## SCREEN 3: Register

**Layout:** Scrollable form card

**Fields:**
- First name, Last name
- Email
- Phone number (with +420 prefix)
- Password + Confirm password (with strength indicator)
- Terms & conditions checkbox with linked text

**Button:** "Create Account" primary button

---

## SCREEN 4: Email Confirmation

**Layout:** Centered card

**Elements:**
- "Email Confirmation" title
- Instruction text: "Check your email and enter the 6-digit code"
- **6 individual digit input boxes** in a row (numeric keyboard, auto-advance focus)
- Countdown timer for resend (e.g., "Resend in 45s")
- "Resend Code" secondary button (disabled during countdown)
- "Verify" primary button
- Auto-submits when 6th digit is entered

---

## SCREEN 5: Profile Completion Wizard

**Layout:** Multi-step vertical wizard with progress indicator

**Steps:**
1. **Personal Info** — Name (pre-filled), date of birth, phone
2. **Address** — Street, city, zip code, country dropdown
3. **Business Identity** — Entity type toggle (Natural Person / Legal Entity), Registration Number (IČO), VAT Number (DIČ, optional), Legal Entity Name (shown only for Legal Entity)
4. **Bank Details** — IBAN field, bank name
5. **Availability** — Weekly schedule grid (Monday-Sunday, add time ranges per day)
6. **Documents** — Drag-drop upload zone, per-file type selector, staged file list with colored file-type badges

**UI Pattern:** Each step has a "Continue" button; final step has "Complete Profile"

---

## SCREEN 6: Main App — Bottom Navigation

**Tabs:**
1. 🏠 Dashboard
2. 📋 Orders
3. 🧾 Invoices

**Top Bar:**
- Left: User avatar (initials circle) → taps to Account Hub
- Center: (empty or current page title)
- Right: Search icon, Notification bell icon

---

## SCREEN 7: Dashboard

**Layout:** Vertical scroll with cards

**Sections:**
1. **Greeting Card** — "Good morning, [Name]!" with time-aware greeting
2. **Quick Stats Row** — 3 metric cards in horizontal scroll:
   - Active Orders (count)
   - Completed This Month (count)
   - Pending Earnings (amount in CZK)
3. **Next Up Card** — Upcoming order with countdown ("Starts in 2h 15m"), customer name, address, service type. Tap → Order Details
4. **Earnings Overview** — Small bar chart showing last 7 days
5. **Working Hours** — Today's schedule status (e.g., "Working 09:00 – 17:00")
6. **Upcoming Orders List** — Next 3 orders as compact cards

**Interactions:** Pull-to-refresh, tap any card to navigate, scroll hides top bar

---

## SCREEN 8: Orders

**Layout:** Tab bar + filtered list

**Tabs:** Available | My Active | Completed | All

**Filter Bar:**
- Filter button → bottom sheet with: date range picker, status checkboxes, location radius
- Sort dropdown: By date, earnings, distance
- Active filter chips (dismissible)

**Order Card:**
- Order ID + status badge (color-coded: green=confirmed, blue=in-progress, gray=completed, red=cancelled)
- Customer name + address (truncated)
- Date/time with calendar icon
- Service type label
- Price in CZK
- Urgency indicator (optional colored dot)

**View Modes:** List (default) | Calendar week strip

**Empty State:** Illustration + "No orders available" text

---

## SCREEN 9: Order Details

**Layout:** Long scrollable detail page with collapsible sections

**Header:** Order ID, large status badge, priority indicator

**Sections:**
1. **Quick Info** — Date/time, location (with map pin icon), customer name, payment status
2. **Services** — List of services with descriptions and prices
3. **Workflow Stepper** — Horizontal step indicator: Assigned → Started → In Progress → Completed
4. **Timer** — Elapsed time display when order is in progress (HH:MM:SS)
5. **Customer Contact** — Name, phone (tap to call), email, address. Two action buttons: Call / Message
6. **Photos** — Before/After photo pairs. Camera button to take photos. Gallery grid
7. **Notes** — Text area for adding notes. "Add Note" button
8. **Payment Info** — Method, total, line-item breakdown
9. **History** — Timestamped audit trail (created, started, completed)

**Action Buttons (bottom sticky):**
- "Accept Order" (for available orders)
- "Start Order" (for assigned orders — swipe gesture)
- "Complete Order" (after photos uploaded)
- "Report Issue" → bottom sheet with issue type selector

---

## SCREEN 10: Invoices

**Layout:** Filtered list

**Filter:** Status (Paid/Pending/Overdue), date range, amount range
**Sort:** Date (newest/oldest), amount

**Invoice Card:**
- Invoice number
- Amount (bold, large)
- Status badge (green=Paid, yellow=Pending, red=Overdue)
- Date issued
- Tap → Invoice Details

---

## SCREEN 11: Invoice Details

**Layout:** Document-style view

**Content:**
- Invoice header: number, date, status
- From/To company details
- Line items table: description, qty, unit price, total
- Subtotal, tax (21% VAT), total
- Payment method and status
- "Download PDF" and "Share" buttons

---

## SCREEN 12: Profile

**Layout:** Scrollable sections with edit capability

**Header Card:** Profile photo (with edit overlay), name, email, phone, verification badge

**Sections:**
1. **Personal Info** — Editable fields: name, phone, DOB
2. **Business Identity** — Entity type selector (segmented control), IČO, DIČ, Legal Entity Name
3. **Address** — Street, city, zip, country dropdown
4. **Bank Details** — IBAN (formatted with spaces), bank name
5. **Availability** — Weekly grid. Each day: toggle on/off + time range picker. Date override list
6. **Documents** — Upload dropzone, staged files with per-file type selector, existing documents as cards with colored file-type badges (PDF=red, DOC=blue, JPG=yellow) and status badges
7. **GDPR Consent** — Checkbox with linked privacy policy text

**Interactions:** Inline editing per section with Save button, photo picker for avatar

---

## SCREEN 13: Settings

**Layout:** Simple list of options

- Language selector (dropdown)
- Theme: Light / Dark / System (radio group)
- Push notifications toggle
- Biometric authentication toggle
- App version (read-only)
- Terms of Service link
- Privacy Policy link
- Support/Contact link

---

## SCREEN 14: Account Hub

**Layout:** Bottom sheet or half-screen overlay

**Content:**
- Profile photo + name + email
- "View Profile" button
- "Settings" button
- Recent orders (last 3, compact)
- "Logout" button (with confirmation dialog)

---

## SCREEN 15: Analytics

**Layout:** Scrollable dashboard with charts

**Period Selector:** This week | This month | Last month

**Metrics:**
- Daily average earnings (large number)
- Best/worst day cards
- Earnings by Day (bar chart, 7 bars)
- Order Distribution (donut chart: Completed, In Progress, Cancelled, Pending)
- Performance Score (circular gauge, 0-100)
- Customer Rating (star display, e.g., 4.8/5)
- On-Time Rate (percentage with progress ring)
- Monthly Earnings Trend (line chart)
- Revenue by Service (horizontal bar chart)
- Schedule Utilization (available vs booked, progress bar)

---

## SCREEN 16: Registration Lock

**Layout:** Full-screen overlay (shown on protected pages when profile incomplete)

**Content:**
- Progress bar (X/4 steps complete)
- 4 category cards, each with:
  - Icon + title
  - Status indicator: ✅ Done (green) | ⏳ Pending (amber) | ❌ Missing (red)
  - Detail text (e.g., missing field names, or "Awaiting admin review")
- "Go to Profile" primary button

**Categories:**
1. Profile Information (user icon)
2. Availability (calendar icon)
3. Required Documents (file icon)
4. Admin Approval (shield icon)

---

## SHARED COMPONENTS

| Component | Description |
|---|---|
| Primary Button | Rounded, filled, primary color, loading spinner state |
| Secondary Button | Outlined, secondary color |
| Text Field | Material 3 outlined input with floating label, error state |
| Dropdown | Material 3 exposed dropdown menu |
| Status Badge | Rounded pill, color-coded (green/yellow/red/blue/gray) |
| Card | Rounded corners (12dp), subtle shadow, white background |
| Bottom Sheet | Draggable, rounded top corners, scrim overlay |
| Skeleton Loader | Shimmer animation placeholders |
| Empty State | Centered icon + title + subtitle |
| Snackbar | Bottom notification bar for success/error messages |
| FAB | Floating action button (scroll-to-top) |
| Filter Chips | Rounded, dismissible, active state with checkmark |
| Photo Gallery | Grid of thumbnails, tap to expand, swipe to navigate |
| Time Range Picker | Start/end time selectors in a row |
| File Type Badge | Rounded square with extension text, color-coded by type |

---

## NAVIGATION FLOW

```
Onboarding → Login ↔ Register
                ↓
         Email Confirmation
                ↓
    Profile Completion Wizard (if new)
                ↓
┌─────────────────────────────────┐
│         MAIN APP                │
│  Dashboard | Orders | Invoices  │
│         ↓        ↓        ↓    │
│  Analytics  Order    Invoice    │
│             Detail   Detail     │
│                                 │
│  Top Bar → Account Hub         │
│          → Settings             │
│          → Profile              │
│          → Notifications        │
└─────────────────────────────────┘
```

---

## DESIGN GUIDELINES

1. **Consistency:** All screens use the same card style, spacing (8/12/16/24dp grid), and typography scale
2. **Accessibility:** Minimum touch target 48dp, sufficient color contrast, screen reader labels
3. **Loading States:** Every data-fetching screen shows skeleton shimmer, never a blank screen
4. **Error States:** Inline field errors (red text below input), snackbar for API errors, retry buttons
5. **Empty States:** Custom illustration + descriptive text + action button for every list screen
6. **Offline:** Banner at top when no connection, cached data still visible
7. **Haptics:** Light haptic feedback on button presses and swipe actions
8. **Animations:** Shared element transitions between list → detail, smooth page transitions, pull-to-refresh bounce
