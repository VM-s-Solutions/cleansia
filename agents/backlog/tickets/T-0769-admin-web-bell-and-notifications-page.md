---
id: T-0769
title: Admin web — the bell and the notifications page (ADR-0065 D5)
status: todo
size: M
owner: —
created: 2026-09-19
updated: 2026-09-19
depends_on: [T-0768]
blocks: []
stories: []
adrs: [ADR-0065]
layers: [frontend]
security_touching: false
manual_steps: []
sprint: —
---

## Context

ADR-0065 D5 — the admin app's half of "administrators are told". Built against the admin client T-0768
regenerates (the four `AdminNotification` routes and the third `NotificationFeedAudience` member); nothing
generated hand-edited. The copy for **all nine** event keys ships here although only one is raised yet:
a key belongs in a keyset only once the audience's clients render it (`NotificationFeedEventKeys.cs:20-23`),
so the later tickets must never write a row the page cannot render.

## Doing

- Bell in the admin top bar: unread count from `unread-count`, polled every 60 s while visible.
- `/notifications` route + sidebar item (`sidebar.notifications`, permission `CanViewAdminNotifications`):
  newest-first list, 20 a page, per-event title/body from `pages.notifications.events.<key>.*` in five
  locales for **all nine keys**, unread emphasis, mark read on click + navigate to the resource, mark all
  read.
- `POLICY_MAP` gains `CanViewAdminNotifications: AdminOnly`.
- Facade specs and the locale-parity spec.

## NOT

- No toast/real-time channel; no per-event settings UI; no partner/customer web change.
- Not the mailbox setting editor (T-0774 web part). Not the roles-based hiding (T-0773).

## Done looks like

An administrator sees a badge, opens the list, reads an event, lands on the order/dispute/request/lifecycle
page it names, the badge decrements; all five locales complete for all nine keys; `nx affected` green.

## Acceptance criteria

- [ ] **AC1** — *Given* three unread admin rows, *when* the app loads, *then* the bell shows 3; *when* one is
      clicked, *then* the row is marked read on the server, the app navigates to its resource and the bell
      shows 2 within the next poll.
- [ ] **AC2** — *Given* an `admin.order.new` row with `orderNumber` and `amount` args, *then* the list line
      reads the localised sentence with both substituted, in each of the five locales; the same for an
      `admin.order.crew_lost` row whose `statusAtLoss` is `OnTheWay` (the sentence says the clean was under
      way).
- [ ] **AC3** — The sidebar item is hidden when `hasPolicy(CanViewAdminNotifications)` is false.
- [ ] **AC4** — A spec walks `AdminNotificationEventCatalog`'s nine keys (from the generated client's enum or
      a pinned list) and asserts a `title` and `body` key per locale.

## Implementation notes

ADR-0065 D5. The bell sits beside the language switcher (`app.component.ts:27-31`); the list page follows
the paged shape of `GetPagedUserNotifications` (20 a page, newest first); the translation path nests the
dotted event key the way the company-settings page nests catalogue keys (`company-settings.models.ts:18-22`).
Deep links: `orderId` → order detail, `disputeId` → dispute detail, `requestId` → the data-protection
requests page, the company keys → the company lifecycle page. Components delegate to a facade holding
signals; no raw controls; every string through `TranslatePipe`.

## Status log

- 2026-09-19 — filed by the docs lane from the batch-6 panel; runs after T-0768's admin client regen.
- 2026-09-19 (review) — `todo`, no owner: it was filed `in_progress` for a lane that does not exist yet.
  The frontend lane flips it when it picks the ticket up, once T-0768's regenerated client is committed.
