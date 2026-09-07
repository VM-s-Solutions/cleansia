---
id: T-0687
title: Every New order shows a blank status pill in the admin list — OrderStatus.New is 0, and 0 is falsy
status: todo
size: S
owner: —
created: 2026-09-07
updated: 2026-09-07
depends_on: []
blocks: []
stories: []
adrs: []
layers: [frontend]
security_touching: false
manual_steps: []
sprint: 16
---

## Context

**In plain terms:** in the admin order list, an order paid by cash shows an empty grey pill where its
status should be, while a card order beside it shows a blue "Confirmed". That looks like the two
orders are in different states. They are not — one of the labels simply failed to render.

`getOrderStatusLabel` in `order-management.helpers.ts` begins `if (!order.orderStatus?.value)
return ''`. **`OrderStatus.New` is `0`**, which is falsy, so the guard meant for "no status" swallows
a real one. Every `New` order renders an empty label.

It compounds: `getOrderStatusClass` has no `case` for `New` or for `OnTheWay`, so both fall through
to `default: 'status-pending'` — and there is no `.status-new` rule in either admin stylesheet. The
result is a blank amber pill.

The admin DETAIL page does not share the bug: its facade guards on `!status?.name` and does have a
`New` case. So the same order reads blank in the list and "New" on the detail page.

### Why this matters more than a missing label

This is what makes the card-vs-cash status difference look like a defect. The underlying difference
is real but deliberate (see the status session in the handover): both payment types are created as
`(New, Pending)`, and the Stripe webhook then moves a card order to `Confirmed` while a cash order
stays `New` until a cleaner takes it. Both are "live, no cleaner yet". Fixing the label does not fix
the naming, but it removes the false signal that something is broken.

## Acceptance criteria

- [ ] **AC1** — Given an order in any status including `New`, When the admin list renders, Then it
      shows that status' label and a class that has a stylesheet rule behind it.
- [ ] **AC2** — Given the same order, Then the list and the detail page agree on what it says.
- [ ] **AC3** — Given a test, Then a status whose numeric value is `0` is covered. That is the whole
      bug and the obvious thing for a future guard to reintroduce.

## Out of scope

- Renaming or splitting `OrderStatus.Confirmed`. That is a domain decision with eight-plus consumers
  and belongs to its own session.

## Findings this ticket carries

- **The same falsy-zero idiom may exist on other zero-valued enums.** `!value` is used as a
  null-check in several helpers; worth a sweep while in there.
- **The admin status-override dropdown offers `Pending` and `Cancelled`**, both of which the backend
  refuses unconditionally. Two options that cannot work.

## Status log

- 2026-09-07 — found while establishing why card and cash orders look different in the admin list.
