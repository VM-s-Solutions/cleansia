---
id: T-0393
title: "Mobile FEATURE gap (iOS+Android) — the Home notification bell is inert on both platforms; no notifications inbox/feed exists (owner: \"clicking notifications does nothing\")"
status: proposed
size: L
owner: pm
created: 2026-07-08
updated: 2026-07-17
depends_on: []
blocks: []
stories: []
adrs: []
layers: [analyst, backend, ios, android]
security_touching: false
priority: medium
manual_steps: []
sprint: 12
source: phase/ios-fix2 fix-round-4 owner remark #7 — reported on iOS, confirmed as a shared unbuilt feature (Android bell is also a no-op)
---

> **Owner remark #7 (phase/ios-fix2, 4th device pass): "When I click on notifications on the home page then
> nothing happens."** Verified: this is **not an iOS regression** — the bell is faithful Android parity. Android
> wires `onNotificationClick = {}` (`HomeTab.kt:228`) — an explicit no-op — and neither platform has a
> notifications inbox/feed screen. The iOS `HomeTab.swift` renders the same inert bell and documents it
> (`HomeTab.swift:162-163`). Building an iOS-only handler would diverge the two platforms; the real work is a
> cross-platform **notifications inbox** feature.

## Context
- iOS `CleansiaCustomer/.../Features/Home/HomeTab.swift:161-204` — `AddressTopBar` renders a `bell` SF symbol
  with no action; comment: "The bell is rendered but inert — Android wires `onNotificationClick = {}`".
- Android `customer-app/.../features/home/HomeTab.kt:228,316,353` — `IconButton(onClick = onNotificationClick)`
  where `onNotificationClick = {}`.
- The only notifications surface that exists on either platform is **notification preferences** (iOS
  `Profile/NotificationsView.swift`, Android settings) — the push/email opt-in toggles, NOT a message feed.
- There is no backend notifications-feed endpoint (no `GET /api/Notifications` list contract) today.

## Acceptance criteria
- [ ] **AC1 (decision first)** — analyst/architect decide the MVP: does the bell open (a) a real notifications
  inbox (list of order/booking/system events with read/unread), or (b) an interim empty-state sheet ("No
  notifications yet") on BOTH platforms? Record the choice; do NOT diverge iOS from Android.
- [ ] **AC2 (backend, if inbox)** — if (a): a mobile-audience `GET` notifications list endpoint (paged,
  per-user, tenant-scoped, S1–S10 reviewed) + a mark-read command; DTOs + mobile spec re-dump + BOTH mobile
  client regens (MANUAL_STEP). If (b): no backend.
- [ ] **AC3 (mobile UI, parity)** — the Home bell navigates to the chosen surface on iOS and Android
  identically (same route, same empty-state copy ×5 locales); an unread badge only if AC2 delivers unread
  state.
- [ ] **AC4 (non-regression)** — both apps compile; existing home tests green; the bell remains reachable/
  accessible with a proper accessibility label.

## Out of scope
- Push-notification delivery/registration (separate; APNs is blocked on the paid Apple account T-0342).
- Notification **preferences** (already shipped on both platforms) — this is the message **feed**, not the
  toggles.

## Implementation notes
- Cheapest first increment = option (b): an interim "No notifications yet" empty-state sheet on both platforms,
  so the bell stops being a dead tap, with zero backend. Promote to (a) when the feed contract lands.
- iOS: the `AddressTopBar` bell already has the tap target; only an `onNotificationClick` closure + a route need
  wiring once the destination exists.

## Status log
- 2026-07-08 — filed `proposed` by pm from phase/ios-fix2 fix-round-4 owner remark #7. Confirmed the iOS bell is
  faithful Android parity (both inert; no feed feature exists), so it was correctly NOT hacked iOS-only in the
  fix round. Medium priority: a visible dead-tap on the most-seen screen, but a genuine feature (not a fix).
- 2026-07-08 (fix-round 5) — owner re-flagged the dead bell, so **option (b) was implemented on iOS**: the bell
  is now a live Button opening a `NotificationsInboxSheet` with a "No notifications yet" empty state (mascot +
  5-locale keys `notifications_inbox_*`). This is a **tracked iOS-first interim** per the owner's repeated
  request. **Android still wires `onNotificationClick = {}` (dead).** Remaining scope: (1) mirror the interim
  empty-state on Android (Home `HomeTab.kt:228`) to re-converge, then (2) the real inbox feed (AC1/AC2). iOS AC3
  (empty-state parity) is effectively done on iOS.
- 2026-07-17 — analyst panel (AC1, decision-first): the real-feed design was deliberated and
  finalized — see `## Feed design (analyst panel)`. Option **(a)** is the ratified target; the merged
  interim empty-state becomes the feed's zero-rows state. OPEN items Q-FEED-01/Q-FEED-02 filed
  (non-blocking, defaults taken); living doc `agents/analysts/notifications.md` created.

## Feed design (analyst panel)

> **AC1 outcome — DECIDED:** the bell's real destination is **(a) a server-backed notifications
> inbox**: one `UserNotification` row per targeted push send, written **transactionally by the
> producer**, listed by a paged per-user endpoint on **both mobile hosts**, rendered **client-side
> from the same event templates pushes use** (the ADR-0025 loc-key model), with server-side
> read/unread and an unread bell badge. The interim (b) empty-state sheet — merged on both customer
> apps (`NotificationsInboxSheet.kt` / `NotificationsInboxSheet.swift`) — becomes the feed's
> zero-rows state, not a separate surface.

*(Analyst defense panel, 2026-07-17 — author / challenger / lead per
`agents/process/deliberation.md`. Grounded in: `NotificationEventCatalog.cs` +
`NotificationCategory.cs`, `SendPushNotificationHandler.cs` (fire-and-forget, at-most-once after the
D2.2 claim), `NewJobsDigestService.cs:170-190` (the transactional-outbox producer shape),
the partner Android Room feed (`NotificationDao.kt`, `CleansiaFirebaseMessagingService.kt`),
ADR-0025, and the two mobile hosts' controller conventions (`DeviceController.cs`). D-sections below
are shown **as revised** after the challenge round; revisions are marked `(revised per FCH-n)`.)*

### D1 — Persistence: yes, a row per send — `UserNotification`, written by the PRODUCER in the same transaction

Push dispatch keeps nothing: FCM is transient, and the consumer is deliberately **at-most-once after
the idempotency claim** (`SendPushNotificationHandler.cs:19-28`) — a feed cannot be derived from it.
So the feed requires its own persisted row per targeted send.

**Where the row is written — producer-side, transactional.** Every producer already enqueues the
push through the transactional outbox (e.g. `NewJobsDigestService.cs:172-189` — the outbox row
commits with the domain change; the drainer puts it on the wire). The `UserNotification` insert joins
**that same transaction**: the feed row commits iff the status change the user will read about
committed. Rejected alternative — writing the row in the Functions consumer
(`SendPushNotificationHandler`): one code site instead of many, but (a) a crash between the claim and
the write loses the row **forever** (at-most-once is acceptable for a re-notifiable push, not for a
durable inbox), (b) the feed would be empty in every environment where the Functions host or FCM
isn't running — FCM is *deliberately unconfigured* in dev/CI and the consumer ACKs skipped sends
(`:149-155`), and (c) the mute check sits before dispatch, so muted events would need re-ordering
anyway. The feed records **business truth**, so it belongs in the business transaction.

**Entity sketch** (backend dev fills in EF details): `UserNotification` / table `UserNotifications`
— `Id` (Guid PK), `TenantId` (nullable, tenant-scoped with the global filter; batch producers running
cross-tenant set it explicitly, exactly as they already set it on the queue envelope), `UserId`,
`EventKey` (≤64), `ArgsJson` (jsonb — **exactly the push `Args` dictionary**, nothing more),
`CreatedOn` (UTC), `ReadOn` (UTC, null = unread). Indexes: `(UserId, CreatedOn DESC)` and a partial
`(UserId) WHERE ReadOn IS NULL` for the badge count. `MANUAL_STEP`: migration (owner; pre-prod policy
= fold into the Initial migration).

**The seam (revised per FCH-2):** there are currently **17 non-test construction sites** of
`SendPushNotificationMessage` across AppServices. In-scope backend work consolidates them onto **one
shared notify seam** (architect names it) that atomically (insert feed row where the event is
feed-scoped) + (enqueue the outbox push). After this ticket the seam and `SendSitewidePromoFanoutHandler`
(promo — excluded from feed v1) are the only non-test construction sites — grep-verifiable
(FD-AC12), so a future producer *cannot* send a push without the feed row.

**Digest collapse rule:** `order.new_available` **updates** the cleaner's existing *unread* digest
row (Args + CreatedOn) instead of inserting; a read digest row gets a fresh unread row. Without this,
the 30-min sweep writes up to ~48 rows/day/cleaner of junk. Parity with the push side, which already
collapses by notification tag (`CleansiaFirebaseMessagingService.kt:122-124`).

**Mute semantics:** category toggles gate the **interruption (push)**, not the **record (feed)** —
for transactional events the row is written even when the push is muted, skipped, or undelivered.
One deliberate exception: the digest already skips muted cleaners at the producer
(`NewJobsDigestService.cs:160-168`, watermark still advances) — no digest row for them. Consistent:
job availability is ephemeral, not history.

### D2 — Content scope v1 (per audience, drawn from the existing catalog — no invented events)

- **CUSTOMER (11 keys):** the 6 order-lifecycle keys (`order.confirmed`, `order.on_the_way`,
  `order.in_progress`, `order.completed`, `order.cancelled`, `order.refunded`) + `dispute.reply` +
  `recurring.scheduled` + `membership.expiring_soon` + `membership.cancellation_effective` +
  `loyalty.tier_upgrade`. That is **everything currently dispatched to customers** except promo —
  with one shared write seam, including all is *cheaper* than filtering, and each is an observable
  business event the customer may have missed as a push.
  **Excluded v1: `promo.new_sitewide`** → **OPEN Q-FEED-01** (recommended default: exclude; it is
  the only event with literal server-authored text, its Promo category defaults to **off**, and iOS
  promo display is already its own deferred marketing ticket per the ADR-0025 verdict — feed promo
  rides that ticket, not this one).
- **PARTNER (1 key): `order.new_available`** — the **only** partner-targeted dispatch that exists.
  Verified: every `order.*`/`dispute.reply` producer targets the **order's customer** `UserId`; the
  partner Android service documents exactly this
  (`CleansiaFirebaseMessagingService.kt:33-42` TODO). Partner assignment/cancellation and
  `invoice.generated` events **do not exist as producers** and are not invented here → **OPEN
  Q-FEED-02** (recommended: dedicated follow-up ticket; once a producer exists, the seam gives it a
  feed row for free).
- **The audience filter is the HOST's (revised per FCH-4):** two keysets defined **next to
  `NotificationEventCatalog`** (single source of truth) — `CustomerFeedEventKeys` (the 11),
  `PartnerFeedEventKeys` (`order.new_available`). List, unread-count, mark-read AND mark-all-read
  are all scoped to the calling host's keyset. This keeps a dual-role user (an `Employee` extends
  `User`) from having the customer app's badge count — or its mark-all — touch partner digest rows,
  and vice versa.

### D3 — Endpoint contract (both mobile hosts, mirrored; NOT on web hosts)

Hosts: **`Cleansia.Web.Mobile.Customer` + `Cleansia.Web.Mobile.Partner`** — new
`NotificationController` per each host's conventions (`[Route("api/[controller]")]`,
`[Permission(Policy.Authenticated)]`, MediatR, `HandleResult`). `UserId` always from the JWT claims,
never from the request (S1). One set of AppServices handlers under `Features/Notifications/`, the
audience keyset passed by the host.

| Verb + route | Contract |
|---|---|
| `GET api/Notification/Paged?pageNumber&pageSize` | `PagedData<UserNotificationDto>`, `CreatedOn` desc; `pageSize` default 20, cap 50 |
| `GET api/Notification/UnreadCount` | `UnreadNotificationCountDto(int Count)` — cheap partial-index count |
| `POST api/Notification/MarkRead` | body `{ id }` → `BusinessResult`; row must belong to the caller AND the host keyset, else `general.not_found`; idempotent (first `ReadOn` wins) |
| `POST api/Notification/MarkAllRead` | body `{ upToCreatedOn? }` *(revised per FCH-1)* → `BusinessResult`; marks the caller's unread rows in the host keyset with `CreatedOn <= upToCreatedOn` (null = all) |

`UserNotificationDto(Guid Id, string EventKey, IDictionary<string, string> Args, DateTime CreatedOn,
DateTime? ReadOn)` — record, positional. **No server-side title/body/deepLink fields** (D5). CQRS:
`GetPagedUserNotifications`, `GetUnreadNotificationCount` (queries) /
`MarkNotificationRead`, `MarkAllNotificationsRead` (commands); errors under `notification.*` in
`BusinessErrorMessage` (+ `errors.notification.*` i18n ×5 where surfaced).
`MANUAL_STEP`: mobile OpenAPI re-dump + **both** mobile client regens.

### D4 — Read/unread + badge semantics

- **Unread** = `ReadOn == null`. **Badge** = the host-keyset unread count: fetched on Home load and
  on app-foregrounding; displayed numeric up to 99, then `99+`; hidden at 0. A push received while
  the app runs bumps the badge locally (+1) without a refetch — the pattern the partner Android app
  already uses live (`NotificationDao.observeUnreadCount`).
- **Opening the inbox marks it seen** — the shipped partner-Android semantic
  (`NotificationDao.kt:22-24`, "Called when the feed opens — everything visible is now seen"), kept
  for cross-platform consistency: after the **first page loads successfully**, the client calls
  `MarkAllRead` with `upToCreatedOn` = the newest fetched `CreatedOn` *(watermark — revised per
  FCH-1)*; rows fetched as unread keep their unread indicator for that viewing session (rendered
  from the fetched `ReadOn`, which was null). A row created *after* the fetch stays unread.
- **`MarkRead` (single)** serves the row-tap path and any future per-item UX; v1 list UX relies on
  the watermarked mark-all.
- **Retention (revised per FCH-3):** hard-delete rows **older than 90 days**, plus a runaway safety
  cap of **newest 500 per user** — both enforced by the existing `DataRetentionTimerFunction` sweep.
  **GDPR:** user erasure (`GdprDeletionService`) deletes all the user's rows (ArgsJson carries
  `orderNumber` — order-linked data leaves with the user).

### D5 — i18n: the server stores loc-keys + args, never rendered text (the ADR-0025 model)

- The row stores `EventKey` + `ArgsJson` only; the client renders title/body from **its bundled
  event templates in the device locale** — the same templates, the same moment, the same locale
  source as push display (Android `strings.xml` `templateFor`; iOS the `push.*` catalog keys
  ADR-0025 D4.1 mandates in both app targets). Frozen-rendered-text alternative rejected: it would
  require the server-side template store × 5 languages that does not exist (settled in ADR-0025
  Options A1) and would freeze each row's language at write time — with loc-keys, the whole feed
  re-renders correctly after a locale switch.
- **(revised per FCH-5)** Feed rendering is *programmatic* string lookup, NOT APNs `loc-args`
  substitution — so the ADR-0025 D3 constraint that forced the iOS *push* body to be argless for
  `loyalty.tier_upgrade` does **not** apply here: both platforms render the tier label from
  `args.tier` via client-side lookup (Android push parity, "You reached Silver Mopper!").
- **Unknown `EventKey`** (old app, newer backend) → the client **hides the row** — drop-parity with
  push (`templateFor(...) ?: return`). Accepted residual: the badge may briefly count a hidden row;
  bounded by the same **client-first rule** ADR-0025 D5 pinned (templates ship in both apps before
  the backend catalog gains a key).
- **Deep link is derived client-side** from `EventKey` + `Args` (`orderId` → order detail;
  `disputeId` → dispute thread; `order.new_available` → available jobs), reusing the push tap
  resolvers (`PartnerNotificationDeepLink` / the ADR-0025-mandated `CustomerNotificationDeepLink`).
  No server `deepLink` field.
- Known cosmetic residual inherited from push: `order.refunded` via `ResolveDispute` ships no
  `orderNumber` arg → renders empty on all surfaces; the one-line producer fix is a courtesy for the
  backend lane (already flagged by ADR-0025).

### D6 — Explicitly OUT of v1 (named)

- **Real-time feed updates** — no websocket/SSE/silent-push refresh; poll-on-open + foreground
  refetch + local badge bump only.
- **Web feeds** (Angular customer/partner/admin) and any **admin "message a user" composer**.
- **Admin visibility** into a user's feed.
- **`promo.new_sitewide` rows** (Q-FEED-01) and **new partner-targeted events**
  (assignment/cancellation, `invoice.generated` — Q-FEED-02).
- **Partner-app UI migration** from the local Room feed to the server feed (+ any partner-iOS feed
  UI) — a named follow-up; server rows accumulate from day one, so the later switch is client-only.
- **Deleting individual notifications** (swipe-to-dismiss) — retention handles cleanup.
- **Notification preferences** (shipped; unrelated to the feed).

### Acceptance criteria (feed — FD-AC1…FD-AC12)

- [ ] **FD-AC1 (row per send, transactional)** — Given any v1-keyset event fires for a user (e.g. a
  cleaner takes order A-1042 → `order.confirmed` for its customer), When the producing command's
  transaction commits, Then exactly one `UserNotifications` row exists with that `EventKey`, the push
  `Args` as `ArgsJson`, `CreatedOn` (UTC) and `ReadOn = null`, committed atomically with the domain
  change and the outbox row; And Given the transaction rolls back, Then no row exists.
- [ ] **FD-AC2 (feed is delivery-independent)** — Given FCM is unconfigured, or the user has zero
  registered devices, or the event's category is muted, When the event commits, Then the row still
  exists and appears in the feed. (Exception: a category-muted cleaner gets no `order.new_available`
  row — the producer already skips them.)
- [ ] **FD-AC3 (digest collapse)** — Given a cleaner has an unread `order.new_available` row, When
  the next sweep targets them, Then that row's `Args`/`CreatedOn` are updated in place and no second
  unread digest row exists; Given their latest digest row is read, Then a new unread row is inserted.
- [ ] **FD-AC4 (paged list, per host)** — Given a signed-in customer with 25 rows, When
  `GET api/Notification/Paged?pageNumber=1&pageSize=20` on the customer mobile host, Then a
  `PagedData<UserNotificationDto>` page of 20 ordered `CreatedOn` desc, each item exposing
  `id/eventKey/args/createdOn/readOn`; And `pageSize` caps at 50; And a partner-keyset event never
  appears on the customer host (nor vice versa) — including for a dual-role user.
- [ ] **FD-AC5 (badge)** — Given 3 unread rows in the host keyset, When Home loads or the app
  foregrounds, Then `UnreadCount` returns 3 and the bell shows "3"; Given >99, Then "99+"; Given 0,
  Then no badge; And Given a push arrives while the app is open, Then the badge increments locally
  without a refetch.
- [ ] **FD-AC6 (open-marks-seen, watermarked)** — Given unread rows exist, When the inbox opens and
  the first page loads, Then the client calls `MarkAllRead` with `upToCreatedOn` = the newest fetched
  `CreatedOn`; the rows fetched as unread keep their unread indicator for that viewing session; And
  Given a row is created after the fetch, Then it remains unread and is counted on the next badge
  fetch.
- [ ] **FD-AC7 (single mark-read + S1)** — Given a row belongs to user A, When user B — or user A via
  the wrong host audience — calls `MarkRead` on it, Then `general.not_found` and the row is
  unchanged; And When user A calls `MarkRead` twice on their own row, Then both succeed and `ReadOn`
  keeps the first timestamp.
- [ ] **FD-AC8 (client render, device locale)** — Given a row `order.completed` with
  `args.orderNumber = "A-1042"` on a device in Czech, When the feed renders, Then the Czech
  order-completed template with "A-1042" substituted; When the device locale switches to English,
  Then the same row renders in English (no stored text); And Given a row whose `eventKey` this app
  version doesn't know, Then the row is hidden — no crash, no raw key.
- [ ] **FD-AC9 (deep link)** — Given an `order.*` row, When tapped, Then the app navigates to the
  order detail for `args.orderId` (`dispute.reply` → the dispute thread; `order.new_available` → the
  partner's available-jobs list), via the same resolvers push taps use; And Given the target no
  longer exists, Then the standard not-found surface shows and the feed remains intact.
- [ ] **FD-AC10 (retention + GDPR)** — Given rows older than 90 days or beyond the newest 500 for a
  user, When the retention sweep runs, Then they are hard-deleted; And Given a GDPR erasure of the
  user, Then all their rows are deleted and `UnreadCount` returns 0.
- [ ] **FD-AC11 (platform parity)** — ticket AC3/AC4 apply unchanged: identical bell → feed route and
  copy ×5 locales on both customer apps; the interim empty-state becomes the feed's zero-rows state;
  the bell keeps its accessibility label and exposes "N unread" when badged; both apps compile, home
  tests green.
- [ ] **FD-AC12 (seam tripwire)** — Given the merged backend, When the reviewer greps
  `new SendPushNotificationMessage(`, Then the only non-test construction sites are the shared
  notify seam and `SendSitewidePromoFanoutHandler` (promo, excluded v1); And a unit test pins that
  every key in `CustomerFeedEventKeys`/`PartnerFeedEventKeys` resolves to a `NotificationCategory`
  (catalog closure).

`MANUAL_STEP`s carried by this design: (1) EF migration for `UserNotifications` (owner; fold into
Initial per pre-prod policy); (2) mobile OpenAPI re-dump + both mobile client regens after the
backend lands.

### Challenge

*(Analyst panel, challenger mode — attacked against source, not the author's citations.)*

- **FCH-1 — BLOCKING (race → wrong read state):** the author's original "opening the feed fires
  mark-ALL-read" copies `NotificationDao.markAllRead` — but server-side, a row can be **created
  between the page-1 fetch and the mark-all call** (push producers run continuously). Unbounded
  mark-all would flag as read a notification the user never saw, and the badge would silently eat
  it. The local Room version never had this race (insert and mark happen on one device). Demand a
  watermark.
- **FCH-2 — BLOCKING (unenforceable invariant):** "every producer writes a row in the same
  transaction" spans **17 construction sites** with no shared seam (grep:
  `new SendPushNotificationMessage(` — TakeOrder, NotifyOnTheWay, StartOrder, CompleteOrder,
  CancelOrder, AdminCancelOrder, AdminRefundOrder, ConfirmRecurringOrder, HandlePaymentNotification
  ×2, AutoCancelStaleRecurringOrders, SendRecurringOrderReminders, SendMembershipLifecycleNotifications
  ×2, AddDisputeMessage, ResolveDispute, LoyaltyService, NewJobsDigestService). An AC that says
  "every site remembered" is not observable; the next producer will forget. Demand a single seam +
  a mechanical tripwire.
- **FCH-3 — STANDS-UNLESS-REVISED (cap deletes unread history):** the drafted "last 100 per user"
  cap is reachable by a legitimate customer: ~6 lifecycle rows per order → a bi-weekly recurring
  customer with a couple of disputes clears 100 rows inside 90 days, and the cap would silently
  delete **unread** rows before the 90-day window elapses. Either exempt unread rows or move the cap
  to a genuine runaway guard.
- **FCH-4 — (audience bleed for dual-role users):** `Employee` extends `User` — one person can hold
  rows from both keysets. The draft scoped only the *list* by host keyset; `UnreadCount`,
  `MarkRead` and `MarkAllRead` were audience-blind, so opening the customer inbox would consume the
  partner badge (and expose cross-app rows to mark-read). All four operations must be
  keyset-scoped.
- **FCH-5 — (over-imported constraint):** the draft copied ADR-0025 D3's "loyalty body is argless on
  iOS" into the feed. That constraint exists because APNs `loc-args` are literal; the feed renders
  **programmatically** with full `Args` in hand — both platforms can (and should) show the tier
  label via client-side lookup, as Android push already does. Don't ship a downgraded feed row on a
  surface that isn't constrained.
- **FCH-6 — (dead surface):** v1 wires only the **customer** apps' UI, yet the contract ships a
  `NotificationController` on the **partner** host too — an endpoint with no consumer is exactly the
  "half-built" shape our audits flag. Justify shipping it now or cut it.

**Checked and found sound (silence is not assent):** producer-side write vs consumer-side (the
at-most-once + FCM-unconfigured argument holds — `SendPushNotificationHandler.cs:19-28,149-155`);
mute-gates-the-push-not-the-record split incl. the digest's producer-side skip
(`NewJobsDigestService.cs:160-168`); promo exclusion default + its OPEN escalation (consistent with
the ADR-0025 verdict's separate marketing ticket); loc-keys over frozen text (locale-switch
re-render; no server template store — settled in ADR-0025 A1); digest collapse rule (matches the
push tag collapse); retention via `DataRetentionTimerFunction` + GDPR via `GdprDeletionService`
(both exist); deep-link reuse of the push resolvers; unknown-key drop-parity + the client-first
rule; tenancy for batch producers (explicit `TenantId`, same as the queue envelope); S1 ownership
checks; empty-state reuse. No further challenge.

### Defense

- **FCH-1 — CONCEDE + REVISE.** Real race, and cheap to close: `MarkAllRead` gains optional
  `upToCreatedOn`; the client sends the newest fetched `CreatedOn`. Folded into D3/D4 and FD-AC6.
- **FCH-2 — CONCEDE + REVISE.** The invariant is now *mechanical*: the backend work consolidates the
  17 sites onto one shared notify seam (row + outbox push atomically); FD-AC12 pins the grep (only
  the seam + the promo fan-out construct the message outside tests) plus the keyset→category closure
  test. The seam's exact name/shape is the architect's call — the observable invariant is not.
- **FCH-3 — CONCEDE + REVISE.** The 100-cap was arbitrary and could eat unread rows. Revised: 90-day
  retention is the policy; the per-user cap moves to **500** — an order of magnitude above the
  realistic 90-day maximum — purely as a runaway/abuse guard. Simpler than an unread-exemption and
  achieves the same in practice.
- **FCH-4 — CONCEDE + REVISE.** All four operations are now host-keyset-scoped (D2, D3, FD-AC4/5/7);
  the keysets live beside `NotificationEventCatalog` as the single source of truth.
- **FCH-5 — CONCEDE + REVISE.** Correct — the APNs literalness constraint does not bind programmatic
  feed rendering. D5 now states both platforms render the tier label from `args.tier` client-side.
- **FCH-6 — REBUT.** Shipping the partner-host controller now is the cheaper and *less* half-built
  path: (1) partner rows (`order.new_available`) accumulate from day one regardless — the endpoint
  is readable truth, not dead scaffolding; (2) partner **iOS** has no local feed at all, so the
  server feed is its only path to parity, and the partner-Android Room→server migration is a named
  follow-up in D6 — cutting the host now would force a second OpenAPI re-dump + dual client regen
  (`MANUAL_STEP`) round for that follow-up; (3) the marginal cost is one thin controller over shared
  handlers. Evidence of precedent: both hosts already mirror controllers wholesale (Device,
  FeatureFlag, Gdpr…). Condition accepted: the PM files the partner-UI follow-up ticket at
  conversion time so the surface is tracked, not forgotten.

### Verdict

*(Analyst panel, lead mode — 2026-07-17.)* **CONSENSUS — zero blocking challenges remain.** The
design above (D1–D6 + FD-AC1…12) is the finalized AC1 decision for this ticket.

| Challenge | Ruling |
|---|---|
| FCH-1 (mark-all race) | CONCEDED + REVISED — watermarked `upToCreatedOn`. Resolved. |
| FCH-2 (17-site invariant) | CONCEDED + REVISED — shared seam + FD-AC12 tripwire. Resolved. |
| FCH-3 (cap eats unread) | CONCEDED + REVISED — 90 days + 500 safety cap. Resolved. |
| FCH-4 (audience bleed) | CONCEDED + REVISED — all four ops keyset-scoped. Resolved. |
| FCH-5 (loyalty over-constraint) | CONCEDED + REVISED — programmatic render uses `args.tier`. Resolved. |
| FCH-6 (partner dead surface) | DEFENDED — partner host ships, with the PM-filed follow-up ticket as the ratified condition. Resolved. |

**Escalations (non-blocking, defaults taken):** `Q-FEED-01` (promo rows in the customer feed —
default: excluded v1) and `Q-FEED-02` (partner-targeted assignment/cancellation + invoice events —
default: dedicated follow-up ticket) filed in `agents/backlog/questions/open.md`.

**Living doc:** `agents/analysts/notifications.md` created in the same step (deliberation protocol
§parallel documentation) — event catalog + mute-vs-record rule + feed row lifecycle + story map.

**Handoff notes:** backend lane = D1 entity + seam consolidation + D3 handlers/controllers +
retention/GDPR hooks + FD-AC12 tests (red-first); iOS + Android lanes = bell badge + feed list +
watermarked mark-all + render/deep-link per D4/D5, both customer apps, FD-AC11 parity. Two
`MANUAL_STEP`s (migration; spec re-dump + both mobile regens). Architect input needed only for the
seam's name/shape — no ADR required beyond what ADR-0002/0025 already settle.

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->

## Status log (feed — Android customer)
- 2026-07-18 — Android CUSTOMER feed UI shipped on `feature/i18n-cluster-3` (FD-AC5/6/8/9 + AC11's
  Android half). Surface for the iOS 1:1 port:
  - **Wire:** hand-written `NotificationFeedApi` (`GET Paged?pageNumber&pageSize=20` /
    `GET UnreadCount` / `POST MarkRead {id}` / `POST MarkAllRead {upToCreatedOn}` — `audience`
    omitted, server-enriched). `NotificationFeedRepository` (`@Singleton`, `ApiResult`, joins the
    `SessionScopedCache` wipe set) owns the unread count as a hot flow.
  - **Badge (FD-AC5):** bell shows the count ("99+" cap, hidden at 0; a11y label "Notifications,
    N unread"); refetched on Home ON_START (covers Home entry + app foreground); FCM receipt of any
    feed-scoped key bumps it locally (+1, no refetch; promo + unknown keys excluded).
  - **Inbox (FD-AC6/8):** the bell's `NotificationsInboxSheet` is now the real feed —
    sealed Loading/Error(retry)/Loaded, mascot empty state for zero rows, newest-first paged list
    with scroll-triggered load-more. On open: fetch page 1, then `MarkAllRead` with `upToCreatedOn`
    = the newest FETCHED row's `createdOn` (fires only when ≥1 row fetched), then badge refresh;
    fetched-unread rows keep their dot for the session. Rows render title/body from
    `NotificationTemplates` — the SAME templates the push display uses (extracted from
    `CleansiaFirebaseMessagingService`), device locale, args substituted (incl. `args.tier` label
    per FCH-5); unknown `eventKey` rows are hidden (drop-parity). Relative timestamps via platform
    `DateUtils` (localized for free).
  - **Tap (FD-AC9):** `NotificationDeepLink.resolve(eventKey, args)` — the push-tap resolver,
    now shared — maps `order.*`/`recurring.scheduled`→OrderDetail(orderId), `dispute.reply`→
    DisputeDetail, `membership.*`→SubscribePlus, `loyalty.tier_upgrade`→RewardsActivity; null →
    mark-read only. Unread tap = optimistic dot clear + local badge decrement + idempotent
    single MarkRead; navigation via VM effect → NavHost.
  - **Tests:** 10 VM + 9 repo unit tests (watermark-from-fetched-row, unknown-key hidden,
    optimistic mark-read, error states, badge flow, session clear). Partner UIs remain T-0430.

## Status log (feed backend)
- 2026-07-18 — feed v1 BACKEND shipped on `feature/i18n-cluster-3` (entity + producer seam over 18
  sites + dual-host NotificationController + retention/GDPR; 31 new tests; adversarial review held
  on every attack class, 3 minor items closed). **PM reconciliation:** D3/FD-AC7's
  `notification.not_found` is amended to the existing `general.not_found` — it is already localized
  ×5 on web, both Android apps and the iOS catalog (the error-i18n sweep), a not-found needs no
  finer semantics here, and minting a new key would ripple translations for zero user benefit.
  MANUAL_STEPS pending (owner): fold the `UserNotifications` table into the Initial migration;
  re-dump both mobile specs + regen clients. Mobile UI lanes: customer inbox next (this ticket),
  partner UIs = T-0430, new partner events = T-0431.
