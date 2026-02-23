# Cleansia Partner App — UX/UI & Functionality Audit

**Date:** 2026-02-16
**Compared with:** Altegio (alteg.io) — business management platform for service industries

---

## Current State Summary

The app has a solid technical foundation (Material 3, dark theme, pull-to-refresh, proper state handling) but needs polish in visual feedback, information density, and micro-interactions to feel premium.

---

## UI Issues

### 1. No Skeleton Loading (highest visual impact)

Every screen shows a generic spinner while loading. Modern apps use skeleton screens — grey placeholder shapes that shimmer into real content. This makes loading feel 2-3x faster perceptually.

**Affects:** Dashboard, Orders, Invoices, Order Details, Profile
**Fix:** Create a reusable `SkeletonLoader` composable with shimmer animation. Build screen-specific skeleton layouts matching the real content shape.

### 2. Dashboard Information Overload

9+ sections competing for attention:
- Greeting hero
- Working hours card
- Quick stats (4 cards in 2x2 grid)
- Next up card
- Completion rate card
- Earnings overview
- Upcoming orders list

Users must scroll extensively and can miss important info like "Next Up" below the fold.

**Fix:** Reduce to 5-6 sections max. Move analytics/earnings to the dedicated Analytics screen. Make "Next Up" card the most prominent element.

### 3. Inconsistent Button Placement on Order Cards

- "Take Order" = full-width button above the address
- "Start Order" = small inline button next to the address

Breaks muscle memory. Users expect primary actions in the same location.

**Fix:** Standardize — always use full-width for primary actions.

### 4. No Urgency Indicators on Orders

All order cards look identical. An order starting in 30 minutes looks the same as one starting tomorrow.

**Fix:** Add colored left border or accent for urgent orders. Add countdown chip ("Starts in 45 min"). Sort upcoming orders by urgency.

### 5. Inconsistent Spacing

Mixed use of 4dp, 8dp, 12dp, 16dp without a clear system. Cards use different corner radii.

**Fix:** Establish an 8dp base grid. Use only multiples: 4, 8, 12, 16, 24, 32. Define standard card styles (corner radius, elevation, padding).

### 6. No Transition Animations

Screen transitions are instant. Tab switches snap without animation.

**Fix:** Add shared element transitions for navigation. Add animated indicator slide between bottom nav tabs. Add subtle scale animation on card tap.

### 7. OrderDetailsScreen is Monolithic

1,872 lines in a single file with 10+ sections. Too much content without progressive disclosure.

**Fix:** Break into smaller composables. Add "Show more details" toggle for secondary info. Add floating action button for quick actions.

---

## UX Issues

### 1. No Haptic Feedback

Tapping buttons, switching tabs, completing actions — all silent. Modern apps use subtle vibrations for confirmation, especially for critical actions.

**Fix:** Add `HapticFeedback.performHapticFeedback()` to: tab switches, button taps, swipe-to-confirm, action completions.

### 2. No Visual Feedback During Card Actions

When tapping "Take Order", only the button shows a tiny spinner. The rest of the card stays interactive — user might tap again.

**Fix:** Disable entire card during action. Add subtle overlay/dimming. Show snackbar immediately on success.

### 3. No Optimistic UI

User taps "Take Order" -> waits -> spinner -> server responds -> UI updates. Feels slow.

**Fix:** Show immediate visual feedback ("Order taken!") and roll back on error. Update local state optimistically before server confirmation.

### 4. Notification Permission Dialog on Every Load

OrderDetailsScreen checks notification permission every time it's opened.

**Fix:** Check once, store the result in preferences. Only re-ask after a reasonable interval (e.g., 7 days).

### 5. No Scroll-to-Top or Section Shortcuts

Long screens (dashboard, order details) require manual scrolling to return to top.

**Fix:** Add scroll-to-top FAB that appears after scrolling down. Consider section quick-jump for order details.

---

## Theme & Design System Issues

### Inconsistent Color Access Patterns

Components access colors in 3 different ways:
- Direct import: `cz.cleansia.partner.ui.theme.Success`
- CleansiaColors object: `CleansiaColors.success`
- MaterialTheme: `MaterialTheme.colorScheme.primary`

**Fix:** Standardize on CleansiaColors for custom colors, MaterialTheme for standard Material colors.

### Hard-coded Colors

Dashboard hero gradient and some component colors are hard-coded instead of using theme.

**Fix:** Define all gradients and special colors in theme files.

### Magic Opacity Values

Components use `.copy(alpha = 0.15f)` without explaining purpose.

**Fix:** Define named constants: `SurfaceOverlayAlpha = 0.15f`, `DisabledAlpha = 0.38f`, etc.

---

## Functionality Gaps vs Altegio

### High Impact

| Feature | Description | Effort |
|---|---|---|
| **Offline mode** | Cache schedule for next 48 hours. Partners often work in basements with poor signal | Medium |
| **Calendar/week view** | Toggle between list and calendar view for orders. Altegio's strongest UX pattern | Medium |
| **Client communication** | Automated SMS/push reminders to clients before scheduled cleaning | High |

### Medium Impact

| Feature | Description | Effort |
|---|---|---|
| **Loyalty/repeat client** | Simple "5th cleaning free" or discount system for repeat customers | High |
| **Configurable permissions** | Role-based access (cleaner vs team lead vs admin). Needed at scale | Medium |
| **Supplies tracking** | Track cleaning product usage per order to optimize costs | Medium |

### Low Impact (for now)

| Feature | Description | Effort |
|---|---|---|
| **Custom payroll formulas** | Configurable salary calculation (base + tips + bonuses) | High |
| **Multi-branch** | Employees across multiple locations without duplicating records | High |

---

## What Cleansia Already Does Better Than Altegio

These are competitive advantages — don't lose them:

1. **Before/after photo workflow** — critical for cleaning accountability, Altegio doesn't have it
2. **Order timer with foreground notifications** (caution -> urgent -> overtime) — very sophisticated
3. **Swipe-to-confirm for critical actions** — prevents accidental completions
4. **Document management with versioning and approval workflow** — Altegio has basic employee profiles only
5. **Full order lifecycle** (available -> take -> start -> complete with photos) — Altegio is appointment-based only
6. **Notes & issues tracking per order** — field reporting capability

---

## Priority Implementation Plan

### Tier 1 — Visual Polish (highest impact, least effort)

- [ ] Skeleton loading screens (shimmer effect) for all screens
- [ ] Haptic feedback on all interactive elements
- [ ] Standardize spacing to 8dp grid system
- [ ] Card loading overlay during async actions
- [ ] Tab switch animation for FloatingBottomNavigation

### Tier 2 — UX Improvements

- [ ] Reduce dashboard to 5-6 sections, move analytics to dedicated screen
- [ ] Standardize order card button placement (always full-width)
- [ ] Urgency indicators on order cards (colored border + countdown chip)
- [ ] Shared element transitions for screen navigation
- [ ] Progressive disclosure on OrderDetailsScreen

### Tier 3 — Functionality

- [ ] Calendar/week view toggle for orders
- [ ] Basic offline mode (cache next 48h schedule)
- [ ] Scroll-to-top FAB on long screens
- [ ] Optimistic UI for take/start/complete actions
- [ ] Client SMS reminders before scheduled cleaning

---

## Accessibility Gaps

- [ ] Test with large text / text scaling
- [ ] Verify touch targets meet 48dp minimum everywhere (inline Start button may be smaller)
- [ ] Verify color contrast against WCAG AA standards
- [ ] Test full screen reader experience
- [ ] Add visible focus indicators for keyboard navigation
