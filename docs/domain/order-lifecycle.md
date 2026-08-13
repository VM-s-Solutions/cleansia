# Order lifecycle

An order's state is **two independent axes, not one**. Reading only the fulfilment axis is the single
most common mistake made against this domain, and it is the reason this page exists before any of the
flow pages.

## The two axes

```mermaid
flowchart TB
  subgraph FULFILMENT["FULFILMENT — Order.CurrentStatus (non-nullable)"]
    direction LR
    New["New (0)"] --> Confirmed["Confirmed (2)"] --> OnTheWay["OnTheWay (3)"] --> InProgress["InProgress (4)"] --> Completed["Completed (5)"]
    New --> Cancelled["Cancelled (6)"]
    Confirmed --> Cancelled
    OnTheWay --> Cancelled
    InProgress --> Cancelled
    Pending["Pending (1) — DEAD"]
  end

  subgraph MONEY["MONEY — Order.PaymentStatus × Order.PaymentType"]
    direction LR
    PPending["Pending (1)"] --> Paid["Paid (2)"]
    PPending --> Failed["Failed (3)"]
    Paid --> Refunded["Refunded (4)"]
    Paid --> Partial["PartiallyRefunded (6)"]
    Paid --> Disputed["Disputed (5)"]
  end

  classDef dead fill:#e5e7eb,stroke:#9ca3af,color:#6b7280,stroke-dasharray: 4 3
  class Pending dead
```

`PaymentType` is `Cash (1)` or `Card (2)` and never changes after creation.

## Why one axis is not enough

**Every** order starts at `New` with `PaymentStatus.Pending` — cash and card alike. From there the two
axes move independently, and the combination is what any real question is actually asking:

| Situation | `CurrentStatus` | `PaymentType` | `PaymentStatus` |
|---|---|---|---|
| Card order awaiting the Stripe webhook | `New` | `Card` | `Pending` |
| Card order paid | `Confirmed` | `Card` | `Paid` |
| One-off cash order, nobody has taken it | `New` | `Cash` | `Pending` |
| Cash order a cleaner has taken | `Confirmed` | `Cash` | `Pending` |

Note the last two rows. A cash order reaches `Confirmed` **with no money having moved**, because on a
cash job the cleaner accepting it *is* the confirmation.

## `Confirmed` is deliberately overloaded

`Confirmed` means *either* "money settled" **or** "a cleaner took it". Four paths write it:

| Writer | What actually happened |
|---|---|
| `TakeOrder` | a cleaner took the job |
| `HandlePaymentNotification` | the Stripe webhook landed; also sets `PaymentStatus.Paid` |
| `ConfirmRecurringOrder` | the customer confirmed a recurring cash occurrence |
| `AdminOverrideOrderStatus` | an admin forced it |

> **Never read `Confirmed` as "a cleaner is on this job".** Read `AssignedEmployees` for that. A
> card-paid order is `Confirmed` the moment Stripe says so, with nobody assigned to it at all.

## `Pending (1)` is dead, and stays

Nothing in production writes `OrderStatus.Pending`. The state the old documentation described — *"card
payment initiated, waiting for the webhook"* — is real and shipping, but it lives on the **money**
axis, which is what the sweeps and the offerability rule actually read.

So the "missing" writer is not missing. It is a duplicate that was never built, and adding one would
give a single fact two sources of truth.

It is **not deleted**, for two reasons: the integer is on the wire to three generated clients, and
legacy rows may still hold it. Readers must keep tolerating it **in the conservative direction** — a
`Pending` row counts as live for the calendar and for GDPR erasure, and it stays rankable by the admin
override. It is never offerable, and the override refuses it as a *target*.

## `CurrentStatus` is a denormalisation with one writer

`Order.CurrentStatus` is non-nullable and is a persisted copy of the latest `OrderStatusHistory` row.
It is written **only** by the `Order.AddOrderStatus` append seam, which also assigns each history row a
strictly-increasing `Sequence` — `CreatedOn` is millisecond-resolution and ties when two transitions
land in the same tick.

There is no history fallback and no `!= null` conjunct. Dropping those is what lets Postgres seek on
`IX_Orders_CurrentStatus_CleaningDateTime`. **Do not reintroduce a nullable read.**

## Where the axes are read together

Two rules span both axes and neither can be expressed as a status list:

- **Offerability** — whether a cleaner may be shown, and may take, an order. See
  [Offerability](/domain/offerability).
- **The stale-order sweep** — matches `PaymentStatus == Pending && PaymentType == Card &&
  RecurringTemplateId == null`, with **no status term at all**.

## Seats

`RequiredEmployees = ceil(EstimatedTime / 120)`, and `MaxEmployees = RequiredEmployees +
BookingPolicy.SpareSeatsPerOrder`.

**`SpareSeatsPerOrder` is `0`.** There is no spare seat, by owner ruling: pay is one row per assigned
employee with no crew-size term, so a filled spare seat is a second full wage against an unchanged
customer price.

Which seat a cleaner occupies is recorded as `OrderEmployee.SeatOrdinal`, unique per order at the
database. That unique index — not the in-memory checks — is what stops two cleaners taking the same
seat concurrently.
