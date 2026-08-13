# Offerability and the take

How a job reaches a cleaner's board, and what happens when two of them tap it at the same moment.

The *rule* is documented once in [Offerability](/domain/offerability). This page is the journey.

## The path

```mermaid
sequenceDiagram
  autonumber
  participant O as Order becomes offerable
  participant H as Preferred hold
  participant B as Open board
  participant C as Cleaner
  participant DB as Postgres

  O->>H: PreferredEmployeeId set?
  alt held
    H-->>C: visible to the preferred cleaner only
    Note over H: until PreferredHoldUntilUtc
    H->>B: lapses — opens to everyone AT ONCE
  else not held
    O->>B: straight to the open board
  end
  B-->>C: new-job push / digest
  C->>DB: take
  DB-->>C: seat won, or "no available spots"
```

## Two synchronised broadcasts

Both arrows into the board wake **many cleaners at the same instant**: the new-job push, and
`NotifyLapsedPreferredOffers` when a hold expires. That is a designed thundering herd onto a single
seat, and it is why the seat needs a real arbiter rather than a check.

## The seat is decided by the database

The take passes three capacity checks — validator, handler re-check, and an in-memory guard — and
**all three are unlocked reads**. Two cleaners loading the order before either commits both pass all
three.

What separates them is a unique index on `(OrderEmployee.OrderId, SeatOrdinal)`. The loser's insert is
rejected at commit and mapped back to the same `no_available_spots` refusal the in-memory path gives,
so the two paths cannot disagree.

The ordinal is the **smallest free** one rather than a count: releasing seat 0 of {0,1} and counting
would derive 1, which is taken — the seat would be permanently unusable while the order read as full.

## Edge cases

| Case | What happens |
|---|---|
| Two cleaners, one seat, same instant | Exactly one assignment survives. The loser sees "no available spots". |
| Two cleaners, **multi-seat** order, same instant | Both derive the same ordinal from the same stale read, so the loser is refused *while a seat is free*. Their next tap succeeds. Known and accepted — a retry loop is real complexity for a self-healing window. |
| Order held for someone else | Indistinguishable from a missing order. The refusal must not reveal that someone else was named. |
| Cleaner already on a conflicting job | Refused by the time-conflict check, last in the cascade. |
| Cleaner over the weekly cap | Refused. |
| Profile incomplete, or contract not approved | Refused before the seat is even considered. |
| Order cancelled but with a free seat | "This job is gone", not "this job is full" — those checks sit *before* offerability on purpose. |

## Taking one job can end another

Confirming a take ends any preferred-cleaner reservation this cleaner can no longer honour in that
time window. Taking a conflicting job **is** a decline: nothing else re-checks the beneficiary's
availability between the grant and the confirmation, so this is the moment it becomes knowable. The
order just taken is excluded, so it earns the assignment notice and never also a closure message.
