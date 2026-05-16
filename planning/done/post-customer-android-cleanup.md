# Post `feat/customer-android-app` — Cleanup & Follow-ups [CLOSED — all items resolved]

> Audit closed 2026-05-12. Every actionable item is either shipped on this
> branch or deliberately skipped with reason. Doc retained for branch
> traceability — once the branch merges, this file moves to `planning/done/`.

## Final status

| ID | Title | Disposition |
|---|---|---|
| TASK-001 | `OrderStatusUpdateTemplateId` in Partner/Admin/Mobile appsettings | **SKIPPED by design** — empty key in non-customer hosts is the intended off-switch. The handlers' per-call `try/catch` keeps callers safe. |
| TASK-002 | Customer auth bypasses `AUTH_COOKIE_KEYS` injection | **SHIPPED** — see commit touching `customer-auth.service.ts` + `apps/cleansia.app/src/app/app.config.ts`. `PermissionService` and other shared consumers now read prefixed `customer_*` cookies inside the customer app. |
| TASK-003 | Validator unit tests via `MockQueryable.Moq` | **SHIPPED** — added `MockQueryable.Moq 7.0.3` to central package management; 16 new tests across `NotifyOnTheWayValidatorTests` + `StartOrderValidatorTests` with a shared `ValidatorTestHelpers` builder. All 20 tests in the filter pass. |
| TASK-004 | `paymentStatusLabel` pipe + customer templates | **SHIPPED** — new pipe at `libs/shared/pipes/src/lib/order-status/payment-status-label.pipe.ts`; 4 customer templates swapped from raw `.name`; `enums.payment_status.*` block added to all 5 customer locale files. |
| TASK-005 | Git-stage planning doc reconciliation | **SHIPPED** — 3 deleted active docs + 29 deleted done/ docs + 2 deleted mobile/ docs staged; 4 shipped specs moved `active/` → `done/`; 5 remaining new active docs staged; SHIPPED-SUMMARY.md gained ~110-line section for this branch; stray `</content></invoke>` tags at file tail cleaned up. |
| TASK-006 | Startup-warn on missing SendGrid template ID | **SKIPPED** — dropped alongside TASK-001. Empty template in non-customer hosts is the off-switch, so a startup warning would be noise. |
| TASK-007 | `promo.new_sitewide` event trigger | **DEFERRED** — gated on admin "send sitewide promo" UI that doesn't exist. See [push-notifications-phase-b.md](push-notifications-phase-b.md) "Deferred" section for the protocol divergence (admin-authored per-locale body, not `strings.xml`). |
| TASK-008 | Push setup runbook | **SHIPPED** — pre-existing [docs/architecture/push-notifications.md](../../docs/architecture/push-notifications.md) covers all 6 gotchas plus 2 extras (Functions VS-debug attach, EF null/null tenant filter). Was untracked; now staged. |
| TASK-009 | Customer-auth signals refactor | **SHIPPED** — `BehaviorSubject<boolean> isLoggedIn$` replaced with `Signal<boolean> isLoggedIn`. Imperative cookie/JWT check method renamed to `hasValidSession()` so the signal can own the `isLoggedIn` name (call sites unchanged — reading a signal is `()`). Navbar dropped its `.subscribe()` mirror in favour of an `effect()` that dispatches `loadCustomerUser()` on transition; footer's stale-after-mount `isAnonymous` bug fixed; gdpr.facade.ts now reactive. |
| TASK-010 | Enum-literal grep audit | **SHIPPED** — zero integer-literal comparisons against `OrderStatus.value` or `PaymentStatus.value` in customer/admin/partner. Admin order-management helper's `value as OrderStatus` casts are type-narrowing on `.value`, not literals. |

## Counts

| Disposition | Count | IDs |
|---|---|---|
| SHIPPED | 7 | TASK-002, TASK-003, TASK-004, TASK-005, TASK-008, TASK-009, TASK-010 |
| SKIPPED by design | 2 | TASK-001, TASK-006 |
| DEFERRED | 1 | TASK-007 |
| **Total resolved** | **10** | |

## Verification baseline (what was already shipped before this audit ran)

The following pre-existing items were verified done in the initial audit pass
and required no follow-up. Kept here so future readers don't re-investigate:

| Original concern | Status | Evidence |
|---|---|---|
| Customer logout `.subscribe()` bug | DONE | [user.effects.ts:31-39](../../src/Cleansia.App/libs/data-access/customer-stores/src/lib/user/user.effects.ts#L31) — proper `mergeMap` + `pipe(map, catchError)` |
| Take/Start/Complete email catch-and-swallow | DONE | All 3 handlers inject `ILogger<Handler>` and log warnings: [TakeOrder.cs:213-216](../../src/Cleansia.Core.AppServices/Features/Orders/TakeOrder.cs#L213), [StartOrder.cs:130-133](../../src/Cleansia.Core.AppServices/Features/Orders/StartOrder.cs#L130), [CompleteOrder.cs:203-206](../../src/Cleansia.Core.AppServices/Features/Orders/CompleteOrder.cs#L203) |
| Customer/Admin/Partner web OnTheWay i18n (5 locales) | DONE | `on_the_way` key present in all 15 i18n JSON files |
| `orderStatusLabel` pipe + customer web wiring | DONE | [order-status-label.pipe.ts](../../src/Cleansia.App/libs/shared/pipes/src/lib/order-status/order-status-label.pipe.ts) plus pipe used in 7 customer order templates |
| Pipes use `OrderStatus.OnTheWay` enum (NSwag regen) | DONE | [order-status-severity.pipe.ts:33](../../src/Cleansia.App/libs/shared/pipes/src/lib/order-status/order-status-severity.pipe.ts#L33), [order-status-icon.pipe.ts:20](../../src/Cleansia.App/libs/shared/pipes/src/lib/order-status/order-status-icon.pipe.ts#L20) |
| Customer `OrderStatusUpdateTemplateId` config key | DONE (customer only) | [appsettings.json:48](../../src/Cleansia.Web.Customer/appsettings.json#L48), [appsettings.Production.json:36](../../src/Cleansia.Web.Customer/appsettings.Production.json#L36) |
| Per-list NgRx slices for partner orders | DONE | [order.state.ts:16-19](../../src/Cleansia.App/libs/data-access/partner-stores/src/lib/order/order.state.ts#L16) |
| Partner My Orders Start + Complete `visible` guards | DONE | [orders.models.ts:259-279](../../src/Cleansia.App/libs/cleansia-partner-features/orders/src/lib/orders/orders.models.ts#L259) |
| Partner orders form `orderStatus_N` dynamic | DONE | [orders.component.ts:110-115](../../src/Cleansia.App/libs/cleansia-partner-features/orders/src/lib/orders/orders.component.ts#L110) |
| Customer Android OnTheWay UX, i18n × 5 locales, push template | DONE |
| Push Phase A (Confirmed/OnTheWay/InProgress/Completed/Cancelled/Refunded/DisputeReply) | DONE | 14 enqueue sites verified |
| Push Phase B (TierUpgrade/MembershipExpiring/MembershipCancellation/RecurringScheduled) | DONE | [LoyaltyService.cs:79](../../src/Cleansia.Core.AppServices/Services/LoyaltyService.cs#L79), [SendMembershipLifecycleNotifications.cs](../../src/Cleansia.Core.AppServices/Features/Memberships/SendMembershipLifecycleNotifications.cs), [SendRecurringOrderReminders.cs:81](../../src/Cleansia.Core.AppServices/Features/Bookings/SendRecurringOrderReminders.cs#L81) |
| `NotifyOnTheWay` controller endpoint, validator, NSwag wire | DONE | [OrderController.cs:72-81](../../src/Cleansia.Web/Controllers/OrderController.cs#L72), [partner-client.ts:4542](../../src/Cleansia.App/libs/core/partner-services/src/lib/client/partner-client.ts#L4542) |

## Manual steps still owed by owner

(These were never in the audit task list — they're DB/secret operations only
the owner can perform. Carried forward from prior session notes for the
merge checklist.)

- **EF migration** — `UserMembership` gained two nullable columns
  (`RenewalReminderSentAt`, `CancellationReminderSentAt`) for the Phase B
  membership-lifecycle sweep. Generate + apply migration before deploying
  the API.
- **Stage TASK-009 frontend changes for commit** — the customer-auth signals
  refactor, validator tests, paymentStatus pipe, runbook, and central
  package additions are unstaged in the working tree as of the audit close.
  Staging is the owner's call.
