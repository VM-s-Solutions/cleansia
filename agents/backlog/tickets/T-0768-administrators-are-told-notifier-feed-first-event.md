---
id: T-0768
title: Administrators are told — the notifier, the feed audience, the admin feed routes, the first event (ADR-0065)
status: in_progress
size: M
owner: backend
created: 2026-09-19
updated: 2026-09-19
depends_on: []
blocks: [T-0769, T-0770, T-0774, T-0775]
stories: []
adrs: [ADR-0065]
layers: [backend]
security_touching: true
manual_steps: []
sprint: —
---

## Context

Owner ruling 2026-09-19 on D5 (*"both in-app and email"*), designed as ADR-0065 and split by the panel
(challenge B11) into three backend tickets: this one (the notifier's feed half and the first event), T-0774
(the e-mail channel) and T-0775 (the seven remaining sites). Nothing tells an administrator anything today
— `RetryFailedUserDeletions.cs:28-32` says so in its own doc comment, and `CreateDispute.cs:168-195` writes
audit evidence and nothing else. This ticket is `security_touching`: a new admin-host surface and two new
tenant-ignoring reads.

The batch runs backend lanes one at a time in this order: **T-0768 → T-0770 → T-0771 → T-0772 → T-0774 →
T-0775 → T-0748**; T-0769 (web) follows this ticket's admin client regen.

## Doing

- `NotificationFeedAudience.Admin = 2`; **`AdminNotificationEventCatalog` in `Cleansia.Core.Domain`** (the
  nine keys + `All`); `NotificationFeedEventKeys.Admin = AdminNotificationEventCatalog.All`; the
  disjointness/prefix/equality guard test.
- `AdminEventCatalog` (AppServices: per key `Audience` — `PhysicalPolicy.AdminOnly` for every entry until
  T-0748 — and `EmailArgOrder`) for all nine keys. `admin.order.crew_lost` and the seven T-0775 keys are
  *declared* here; only `admin.dispute.filed` is *raised* here.
- `IAdminNotifier` / `AdminEvent` / `AdminNotifier` — **feed rows only in this ticket**: recipients by
  argument, one `UserNotification` per eligible administrator, no push, no commit. The e-mail step is a
  seam left in the class (a no-op until T-0774).
- `IUserRepository.GetActiveAdministratorsAsync(tenantId)` — tenant-ignoring with the explicit predicate
  (T-0748 adds `AdminRole` to its projection).
- `IUserNotificationRepository.AnyForEventAsync(tenantId, eventKey, argName, argValue)` — tenant-ignoring,
  explicit predicate, `ArgsJson` fragment match; a unit test pins the fragment to the notifier's serialiser.
- `Policy.CanViewAdminNotifications` → `AdminOnly` (+ `FrozenPermissionMapTests` additive row);
  `AdminNotificationController` (get-paged, unread-count, mark-read, mark-all-read) with the audience
  server-enriched; `[AuditAction(Audited = false)]` on `MarkNotificationRead.Command` and
  `MarkAllNotificationsRead.Command`.
- The one call site: `CreateDispute.cs:181` → `admin.dispute.filed`.
- Tests per ADR-0065 §Verification 1, 2 (feed half), 5, 9 (the audit assertion), 11; the admin NSwag client
  regenerated and committed.

## NOT

- No e-mail (T-0774): no `EmailType`, template, message shape, mailbox key or settings value type.
- Not raising `admin.order.crew_lost` (T-0770) or the seven other events (T-0775).
- Not filtering recipients by role (T-0748). No web (T-0769). No schema. No change to `NotificationProducer`.
- Not fixing `AdminReassignOrder`'s missing `OrderStatusHistory` include (reported finding, ADR-0067).

## Done looks like

A customer filing a dispute writes one `UserNotification` per active confirmed administrator of the
order's company; the admin host serves the feed under the new policy; a mark-read writes no admin audit
row; all three backend test projects green; the regenerated admin client committed in the same change.

## Acceptance criteria

- [ ] **AC1** — *Given* company A with two active confirmed administrators and company B with one, *when* a
      customer of A files a dispute, *then* two `UserNotification` rows with `EventKey = admin.dispute.filed`
      exist for A's administrators and none for B's — **also when the ambient tenant is overridden to B** at
      the notifier call (Testcontainers).
- [ ] **AC2** — *Given* a deactivated, an unconfirmed and an anonymised administrator of A, *then* none of
      them receives a row.
- [ ] **AC3** — *Given* an administrator token on the admin audience, *when* it calls `get-paged`, *then* it
      sees only `admin.*` rows of its own user; `mark-read` on another administrator's row is refused; an
      Employee token is refused; anonymous is 401; a mark-read by an administrator writes **no**
      `AdminActionAudits` row.
- [ ] **AC4** — `AdminNotificationEventCatalog.All`, `AdminEventCatalog`'s keys and
      `NotificationFeedEventKeys.Admin` are equal, disjoint from the customer and partner keysets, every key
      starts with `admin.`, and every key maps to `null` in `GetCategoryFor`.
- [ ] **AC5** — `AdminNotifier` calls neither `INotificationProducer` nor `IEmailService` nor `IQueueClient`
      nor any `CommitAsync` nor the ambient `GetTenantSettingAsync(key)` (a reflection/mock assertion).
- [ ] **AC6** — No arg value at the site is a name, e-mail, phone or free text (catalogue guard test).
- [ ] **AC7** — `AnyForEventAsync` returns true for a row whose `ArgsJson` the notifier wrote with that
      `orderId`, false for another order and false for another company with the same id.

## Implementation notes

ADR-0065 D1 (the row and the third audience), D2 steps 1 and 3 (recipients by argument, feed rows, no
push), D4 (the catalogue table — the `Audience` column is placed now and read for nothing until T-0748),
§What is NOT built (the `[AuditAction(Audited = false)]` mechanism). Ticket ids never go into source
comments. The admin NSwag regen is the lane's to run and to report (CLAUDE.md §Manual steps).

## Status log

- 2026-09-19 — filed `in_progress` by the docs lane from the batch-6 panel; ADR-0065 `proposed` the same day.
