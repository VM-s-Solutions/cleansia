---
id: T-0753
title: A guest can cancel their booking — anonymous cancel keyed like the guest lookup, on web + Android + iOS
status: in_progress
size: M
owner: —
created: 2026-09-15
updated: 2026-09-16
depends_on: []
blocks: []
stories: []
adrs: [ADR-0051, ADR-0061, ADR-0062]
layers: [analyst, backend, frontend, android, ios, docs]
security_touching: true
manual_steps: []
sprint: —
---

## Context

Owner ruling 2026-09-15, on the T-0751 review: *"make a ticket for cancellation of guest order as
well."* Filed **open** — no lane until the owner opens it.

**Ground truth today** (`Cleansia.Core.AppServices/Features/Orders/CancelOrder.cs`,
`Cleansia.Web.Customer/Controllers/OrderController.cs`): `POST api/Order/Cancel` is behind
`Policy.CanCancelOrder` and the handler refuses `order.UserId != userId` as `order.not_found` (S3), so
a booking placed as a guest — `Order.UserId` null, the customer's e-mail its only handle — can be
cancelled by **nobody but an admin** (`AdminCancelOrder`). Nothing anonymous cancels. The guest can
*see* the booking: `GET api/Order/Lookup?orderNumber=&email=&confirmationCode=` and
`POST api/Order/LookupBatch` are anonymous on the two customer hosts, rate-limited on the web
`interactive` and mobile `auth` windows, tenant-ignoring on the shared secret and re-pinned to the
order (ADR-0051, ADR-0061 D4). The web `/track-order` page uses them; mobile guest lookup is added here.

**Why it matters beyond the guest's convenience:** the erasure walk (T-0751) leaves a **live** guest
booking under the erased e-mail out rather than refusing, precisely because the subject has no way to
cancel it; Q-GDPR-03 asks whether it should refuse instead, and option (b) there is only defensible
once this ticket ships.

## Doing (when picked up)

- **Backend.** An anonymous cancel on the two customer hosts, **keyed exactly like the guest lookup**:
  order number + e-mail + confirmation code (the shared secret is what makes an anonymous
  resource-by-id read lawful — S3; the e-mail alone is guessable). A new command beside `CancelOrder`
  rather than a branch in it (the signed-in handler reads the session; a guest has none), sharing the
  same pieces: `CancellationAssessor` (same tiers, same oops window, same "has a cleaner accepted"
  input), the same refund path (`IRefundService`, `RefundReason.CustomerCancellation`), the same
  credit unwind on an unpaid card order, the same express-waiver release, the same status transition
  (`CancelledBy.Customer`). Tenant: read past the filter on the secret and re-pin to the order's
  operator before any write (the `LookupOrder` / chargeback idiom), so the status track, the refund
  and the audit row are stamped with the order's company. Rate limit: the `auth` window, like the
  signed-in cancel. Refusals as the signed-in cancel refuses (`order.already_cancelled`,
  `order.in_progress_cannot_cancel`, …); a wrong key is `order.not_found`, never "wrong e-mail".
- **Audit (ADR-0062).** The same `customer.order.cancel` label with `AllowsAnonymousActor = true` on an
  `IOperatorScopedRequest` (the roster guard demands it — the market is the order's, resolved
  server-side, never sent), `UserId` null, the same `OrderCancellationEvidence` on the success row, the
  refusal key on a failure row; the guest cancel's row is then one of the guest rows the erasure blanks
  by order (T-0751).
- **Notification.** A guest has no feed, no push and no account: the cancellation (and the refund, when
  one is issued) is confirmed **by e-mail to the booking's address**, on the path the guest booking
  confirmation already uses — not `INotificationProducer`, which is keyed on a user id.
- **Web.** On `/track-order`, once a booking is looked up and is cancellable, a *Cancel booking*
  action with the same fee preview and the same confirmation sheet the signed-in order detail shows
  (the preview endpoint needs the same anonymous, secret-keyed twin, or the lookup response carries
  the figures); five locales.
- **Android + iOS customer apps.** The same action on the guest order lookup screen, with the same
  fee sheet; the endpoints join the anonymous allow-lists (`ANON_ENDPOINTS` / `AnonymousAllowList.customer`);
  generated clients regenerated (iOS on the owner's Mac); unit tests for the view models; XCTest
  written, unrun on Windows.
- **Docs.** `/flows/booking` (or the cancellation flow) gains the guest branch; business rules
  `#cancellation` says a guest cancels under the same tiers; ADR-0062 D5 as amended 2026-09-15 and
  Q-GDPR-03 are re-read once this ships.

## Acceptance criteria

- [ ] **AC1** — Given a guest booking in `New` or `Confirmed` looked up with its order number, e-mail
      and confirmation code, When the guest cancels it, Then the order is `Cancelled` with
      `CancelledBy.Customer`, the fee tier and refund are the ones `CancellationAssessor` computes for a
      signed-in customer at the same notice, the refund is issued on a paid card order and the credit
      returned on an unpaid one, and the response carries the same figures the signed-in cancel returns.
- [ ] **AC2** — Given a wrong e-mail, a wrong confirmation code or an order number that is not a guest
      booking, When the cancel is attempted, Then it is refused `order.not_found` — the same answer for
      all three — and nothing changes.
- [ ] **AC3** — Given a guest booking a cleaner has started (`InProgress`), When the guest cancels,
      Then it is refused `order.in_progress_cannot_cancel`, as for a signed-in customer.
- [ ] **AC4** — Given a successful guest cancel, Then exactly one `customer.order.cancel` row exists
      with `UserId` null, `ResourceType = Order`, the order id, `OrderCancellationEvidence` and the
      caller's IP, stamped with the **order's** operator; a refusal leaves one failure row with the key.
- [ ] **AC5** — Given a guest booking placed in a second operator's market, When the guest cancels
      from a client that names no market, Then the cancel succeeds and every row it writes carries that
      operator (`SecondTenantIsolationHostTests` shape).
- [ ] **AC6** — Given a guest cancel, Then a cancellation e-mail (with the refund line when one was
      issued) is sent to the booking's e-mail address, and no notification row or push is attempted
      against a user id.
- [ ] **AC7** — Given the web `/track-order` page, the Android and the iOS guest lookup screens, When
      a cancellable booking is shown, Then a cancel action with the fee preview is offered and works
      end to end; the routes sit in the `auth` rate window (`RateLimitCoverageGuardTests` green, the
      roster guard green — the marker is on an `IOperatorScopedRequest`).
- [ ] **AC8** — HostTests: the route answers `200` anonymous with a valid key on both customer hosts,
      `404` on the partner and admin hosts; the request log suppresses the e-mail (S6).

## Out of scope (the NOT list)

- **No account creation** — the guest stays a guest; no "register to cancel" prompt.
- **No change to the fee policy** — the tiers, the oops window and the Plus free window are read, not
  edited; a guest is never a Plus member and gets the standard window.
- No admin-side change (`AdminCancelOrder` already cancels a guest booking).
- No guest *reschedule*, no guest dispute, no guest review — cancel only.
- No change to the erasure's blocking rule (that is Q-GDPR-03's call).

## Status log

- 2026-09-16 — Backend and generated-contract checkpoint verified: 1,322 affected unit tests,
  all 501 PostgreSQL integration tests, all 314 host tests before the additive lookup alias, then
  all 22 guest host cases after it. Three web lookup wire tests and all three app compilation
  units pass. Regenerated `_nswag:partner`, `_nswag:admin`, `_nswag:customer`, then refreshed both
  mobile OpenAPI specs with `refresh-mobile-spec.sh partner` and `customer`; only the customer
  contracts changed. Stopped the five recorded local hosts and removed their disposable PostgreSQL
  container. No deployed DEV drop or deployment ran. Web lookup now uses POST; cancellation UI
  work remains in progress. Dedicated reviews closed the guest audit actor and mobile lookup
  rate-policy findings; the existing audit payload roster now asserts the actual refund field.
- 2026-09-16 — All 501 PostgreSQL integration and 314 HTTP host tests passed. Android preparation
  found that the existing GET lookup exposes its secret in HTTP URLs. Added a POST alias on both
  customer hosts, reusing the query, handler, each host's existing lookup window (web `interactive`,
  mobile `auth`) and sensitive-body suppression;
  retained GET compatibility. Added both-host valid/invalid key and compatibility cases and both-body
  logging guards. Five generated contracts and client work follow verification of this addition.
- 2026-09-16 — Client ground truth corrected the original assumptions: neither mobile customer app
  has a guest lookup screen, and signed-in web order detail has no fee confirmation dialog. The
  approved minimal mobile lookup and guest web dialog use the existing UI components; no additional
  signed-in web cancellation feature is added.
- 2026-09-16 — Regenerated the single `Initial` as `20260916102615` with dotnet-ef 10.0.12,
  `Cleansia.Web.Partner` startup and the localhost-only design-time factory. Removed then added;
  generated migration files were not hand-edited. `CurrentStatus` concurrency metadata changed,
  while all 87 table create operations remain identical. MS-2 now tracks this id. No deployed DEV
  database drop or deployment ran. Final build passed with zero errors; all 501 PostgreSQL
  integration tests passed, including 15 guest cancellation scenarios.
- 2026-09-16 — Dedicated money/security review found an account actor leaking into guest audit rows
  when a JWT accompanied the secret. Four new success/refusal and session/snapshot cases first
  failed, then passed after forcing the guest customer actor to null. The separate admin builder
  retains its existing actor rule. Updated the explicit operator-request roster for both new routes;
  74 affected unit tests pass. The host regression now covers both success and refusal with a JWT.
- 2026-09-16 — Opened by the approved Batch 4 programme. Backend implementation compiles with
  zero errors; security, refund-recovery and PostgreSQL concurrency tests are being added and are
  not yet verified. `Order.CurrentStatus` is now an EF concurrency token to reject stale status
  writes; no new SQL column, but the model metadata requires a regenerated single `Initial` and
  full integration verification before commit. DEV drop remains timed with deployment. Clients
  follow regenerated contracts; dependent Swift work awaits the existing contract clarification.
- 2026-09-16 — Shared cancellation extraction moved waiver release and policy assessment. Updated
  the living membership-benefit-usage and refund-policy role citations; historical ADR bodies stay
  unchanged. The catalog checker has zero claim violations after those corrections.
- 2026-09-15 — filed `todo` by the docs lane on the owner's word; waiting on the owner to open it.
