# Partner Android — Order Detail redesign

## 1. What's wrong with the current screen

Looking at the live screenshot (CLS-2026-0012, Confirmed):

- **Three full-width primary buttons stacked** (Take / Notify on the way / Start) for the same order — visually screams "panic button row." A cleaner only needs the **current** action; the others are noise.
- **`Take Order` is shown on a Confirmed order** because the screen can't tell whether the current user owns it (see line 318 comment in `OrderDetailsScreen.kt`). Server returns 4xx on a foreign Take. Bad UX masking a missing field.
- **Property section dumps raw extra slugs** (`eco_products, pet_friendly, extra_vacuum`) — not localized, not human-readable, no icons.
- **Hero card is information-dense but flat** — date, name, address are three lines of identical white-on-blue text. No visual hierarchy.
- **No earnings shown** — the cleaner has no idea what they're being paid until they swipe Take on the list. On a 4h+ job this is *the* number that matters.
- **No way to call / text / navigate** to the customer — the phone field exists in the DTO (`customerPhone`, `customerEmail`) and is never surfaced.
- **No map** — the address is a string, no spatial context for a cleaner trying to plan a route.
- **Services / packages list is missing entirely** — the order has `selectedServices` and `selectedPackages` arrays with names + prices, never rendered.
- **No status timeline / history** — `statusHistory` is in the DTO, never surfaced. A cleaner can't see when the order was confirmed vs. notified vs. started.
- **No pay breakdown** — `originalSubtotal`, `tierDiscountAmount`, `membershipDiscountAmount`, `promoDiscountAmount` all exist in the DTO and never appear.
- **Photos and Notes sections live at the bottom**, below payment — but during an active job, photos + notes are the *most* used part of the screen.

## 2. Cleaner's actual job-to-be-done by phase

The cleaner reads this screen at four moments. Each moment wants different info up top.

| Phase | Status | What the cleaner needs in the first viewport |
|---|---|---|
| Browsing offers | (not on detail) | (covered by list) |
| Just took an offer | `Confirmed` (mine) | Date/time, pay, where, how to get there, how to contact customer |
| En route | `OnTheWay` | Address + map, customer phone, ETA-able info |
| Cleaning | `InProgress` | Photos before/after, notes input, time tracking |
| Wrapping up | `InProgress` → completing | Complete button, payment summary, what they earn |
| After | `Completed` | Receipt info, review (if any), no more actions |

The current screen has the same layout for all of these. We can do better.

## 3. Proposed layout (status-aware)

### 3.1 Hero card — slimmer + denser

Replace the three-line gradient with a two-row hero:

```
┌──────────────────────────────────────────────┐
│ #CLS-2026-0012             [Confirmed pill] │
│ Wed 20 Mar · 09:00      Earnings:  450 Kč  │
└──────────────────────────────────────────────┘
```

- Order number + status pill on top row.
- Date/time + **earnings** on the second row. Earnings is the brand-color number on a white chip — same treatment as the OfferCard on the list, for cross-screen consistency.

**Backend gap:** detail DTO doesn't carry `estimatedCleanerPay`. Either add it to the partner detail handler (one-line projection) or compute client-side from `originalSubtotal × commission` if the rule is stable. Add to the backend — list and detail should agree.

### 3.2 Customer card with contact actions

A dedicated card immediately below the hero, status-aware:

```
┌──────────────────────────────────────────────┐
│ 👤  Marie Svobodová                          │
│ 📍  Národní třída 25, Praha, 11000           │
│                                              │
│  [📞 Call]   [💬 SMS]   [🗺 Navigate]        │
└──────────────────────────────────────────────┘
```

- The three action chips open `tel:`, `smsto:`, and a geo intent (`geo:lat,lng?q=address`) respectively.
- `Call` and `SMS` only render when status ≥ Confirmed AND owned by current user — protects customer privacy from offer browsers.
- `Navigate` always renders if there's an address.

### 3.3 Map preview (status-aware)

```
┌──────────────────────────────────────────────┐
│       ┌───────────────────────────────┐      │
│       │                               │      │
│       │       [Mapbox tile]           │      │
│       │           📍                  │      │
│       │                               │      │
│       └───────────────────────────────┘      │
│            "Tap to open in maps"             │
└──────────────────────────────────────────────┘
```

- Hide entirely for `New` / `Cancelled`.
- Show for `Confirmed (mine)`, `OnTheWay`, `InProgress`.
- Static `MapboxMap` (no interaction inside the card — just a marker preview, ~160dp tall). Tap → opens the same geo intent as the Navigate chip above. Mapbox already wired in `:core`.

**Backend gap:** detail DTO needs `customerAddressLatitude` / `customerAddressLongitude` (same fields the list already has) — alternative is to reverse-geocode the address string client-side which costs an extra Mapbox call per open. **Recommendation: add the coords to the detail DTO.** One-line addition.

### 3.4 Primary action — single button, context-aware

Replace the stack-of-three with **one** primary button at a fixed bottom-of-content position (just above the scroll's lower padding), keyed to status:

| Status | Action button | Visibility rule |
|---|---|---|
| `New` / `Confirmed` unassigned | "Slide to take" | Only if unassigned (needs `IsAssignedToMe` flag on DTO) |
| `Confirmed` (mine) | Two stacked: "Notify on the way" (primary) + "Start now" (secondary outline) | OnTheWay is the expected next step; Start is a skip-ahead |
| `OnTheWay` | "Slide to start cleaning" | The slide-gesture commits the timer |
| `InProgress` | "Complete order" | Opens existing dialog |
| `Completed` / `Cancelled` | (none) | — |

Reuses the same `SlideToTake` composable from the list for Take/Start — consistent muscle memory across both screens. **Backend gap:** add `isAssignedToCurrentUser: bool` to the detail DTO. Removes the current "we don't know who owns this" hack and prevents Take-button-on-stranger-order.

### 3.5 Scope card — services, packages, extras with proper labels

```
┌──────────────────────────────────────────────┐
│ 🏠 Property                                  │
│ 5 rooms · 3 bathrooms                        │
│                                              │
│ Standard cleaning           150 Kč           │
│ Deep clean package          800 Kč           │
│                                              │
│ Extras                                       │
│ 🔥 Inside oven cleaning     200 Kč           │
│ ❄ Inside fridge cleaning    150 Kč           │
└──────────────────────────────────────────────┘
```

- Render `selectedServices` and `selectedPackages` by name + base price (already in DTO).
- Render `extras` map using the same emoji mapping the web wizard now uses (oven/fridge/windows/laundry/pets). Strip the slug, show the name from the catalog. **Backend gap:** the detail DTO returns extras as `Map<string, bool>` (slug → enabled). To localize the labels we either (a) include extra catalog names in the response or (b) cache the catalog client-side. Option (b) is fine — partner-app already fetches `/api/Catalog/extras` for the list cards, just reuse it here.

### 3.6 Notes section — split by author

The current "Customer notes" card mashes customer notes + access + special instructions together. Split into two cards:

- **From customer** (read-only): `notes`, `accessInstructions`, `specialInstructions`. Each with a small label badge.
- **My notes** (read/write — `NotesAndIssuesSection` already exists): keep where it is.

The existing `NotesAndIssuesSection` stays. Just move it ABOVE the payment summary, since notes are used during/before the job, not after.

### 3.7 Photos — promote when relevant

Current location (mid-page) is fine for `InProgress`. For `Completed` it should auto-expand "after" photos.

- `InProgress`: card collapsed to a thumbnail strip with a prominent "+ Add before photo" / "+ Add after photo" CTA.
- `Completed`: card expanded showing both sets.

Component (`PhotosSection`) already exists — only the visual presentation in the parent card changes.

### 3.8 Status timeline (new, collapsible)

For `Completed` orders, render `statusHistory` as a vertical timeline:

```
✓  Created           Mar 20, 8:55 AM
✓  Confirmed (you)   Mar 20, 8:57 AM
✓  On the way        Mar 20, 8:59 AM
✓  Started           Mar 20, 9:01 AM
✓  Completed         Mar 20, 11:43 AM (3h 42m)
```

Bottom of the screen, collapsible. Skipped entirely if the array is empty.

### 3.9 Payment summary card — show discount lineage

Replace the bare "Total / Payment Type" with a breakdown when discounts apply:

```
┌──────────────────────────────────────────────┐
│ 💳 Payment                                   │
│ Subtotal              1 250 Kč               │
│ Membership discount   −125 Kč  [Plus chip]   │
│ ─────────────────────                        │
│ Total                 1 125 Kč               │
│ Method: Card · Paid                          │
└──────────────────────────────────────────────┘
```

- Show breakdown only when `originalSubtotal != totalPrice` (i.e. some discount applied). Otherwise show just "Total + Method".
- `paymentStatus` deserves a colored pill (Paid green / Pending amber / Failed red) — currently rendered as plain text via `name`.

### 3.10 Mascot (optional, low-priority polish)

For the empty-but-active `OnTheWay` window (no photos yet, no notes yet), show the `mascot_thumbs_up` we already have in `drawable-nodpi`, with a tiny encouragement string ("On your way! See you soon.") at the bottom of the scroll. Skip when there's content.

## 4. Final section order

| # | Section | Always | Status filter |
|---|---|---|---|
| 1 | Hero (number, status, date, earnings) | ✓ | always |
| 2 | Primary action button(s) | | hide on Completed/Cancelled |
| 3 | Customer card (name + address + contact chips) | ✓ | contact chips hidden on unassigned offers |
| 4 | Map preview | | hide on New / Cancelled |
| 5 | Scope (rooms / services / packages / extras) | ✓ | always |
| 6 | From customer (notes, access, special) | | hide if all three empty |
| 7 | My notes & issues | ✓ | always |
| 8 | Photos (before / after) | ✓ | always (presentation differs by status) |
| 9 | Payment | ✓ | always |
| 10 | Status timeline | | only on Completed |
| 11 | Mascot encouragement | | only on OnTheWay with no content yet |

## 5. Implementation phases

### Phase A — UI-only changes (no backend, no NSwag)
Lowest risk, highest visible delta.

- **A1.** Slimmer 2-row hero with earnings placeholder (use list item's `estimatedCleanerPay` until backend catches up — pass via nav arg, or just say "—" for now).
- **A2.** Single context-aware action button (collapses today's 3-button stack). Keep current "we may surface a 4xx on Take" until backend ships `isAssignedToCurrentUser`.
- **A3.** Customer card with Call / SMS / Navigate intents. Skip Map until backend ships coords; navigate intent uses address string fallback (`geo:0,0?q=<address>`).
- **A4.** Scope card with services + packages + emoji extras (catalog cached client-side).
- **A5.** Section reorder per §4.
- **A6.** Payment breakdown card with discount lines + colored payment-status pill.
- **A7.** Mascot for empty OnTheWay.

### Phase B — Backend additions (one PR, one NSwag regen)
- **B1.** Add `customerAddressLatitude` / `customerAddressLongitude` to the partner detail DTO (`GetOrderDetails.cs`). They already exist on the OrderAddress entity.
- **B2.** Add `estimatedCleanerPay` to the partner detail DTO. Same projection used in `GetPaged`.
- **B3.** Add `isAssignedToCurrentUser` bool — `order.EmployeeId == currentEmployeeId`. Used by Phase C's button + customer-card gating.

### Phase C — Wire backend additions to UI (after regen)
- **C1.** Light Mapbox map preview card using the new coords. Tap → existing geo intent.
- **C2.** Earnings on hero uses real DTO field.
- **C3.** Take button gating uses `isAssignedToCurrentUser` instead of "render-and-surface-4xx".

### Phase D — Status timeline (lowest priority)
- **D1.** Render `statusHistory` array on Completed orders as a vertical timeline. Pure UI work using existing data.

## 6. Open questions for the user

1. **Earnings field:** confirm we add `estimatedCleanerPay` to detail DTO in Phase B. Alternative: pass it via nav arg from the list, which is uglier but ships entirely in Phase A.
2. **Map coords:** Phase B (backend adds lat/lng) vs. Phase A-with-geocoding (Mapbox call on detail open). Going with Phase B — coords are cheap to add and we avoid an extra HTTP per detail view.
3. **Mascot:** the empty-OnTheWay mascot is optional polish. Want it in Phase A or skip?
4. **Status timeline:** do you want the detail view to show on **all** statuses (with the current step highlighted), or just on Completed?
5. **Slide-to-start:** today Start is a regular button. Are you OK switching it to a slide gesture (same as Take) for symmetry, given that Start commits a timer and Complete is the only "ending" action?

## 7. Not in scope

- iOS — partner iOS app doesn't exist yet.
- Web partner order-detail — separate redesign, different surface (desktop-first).
- Push notifications for status changes.
- Live-location share with the customer while OnTheWay.
- In-app navigation (we hand off to Google Maps / Mapy.cz / etc. via geo intent — building our own routing is out of scope).
- Reviews UI on the detail page — `review: OrderReviewDto` is in the DTO and could render at the bottom on Completed orders, but that's its own card and feature.
