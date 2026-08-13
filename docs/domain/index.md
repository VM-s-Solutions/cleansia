# Domain

The nouns of the platform: what each entity is, what it owns, and which states it can be in.

## What belongs here

- The **entity model** — every persisted aggregate, its fields, and the relationships between them,
  derived from the EF configurations rather than described from memory.
- **State machines** — the order lifecycle, dispute transitions, pay-period states, membership
  status. Each as a diagram, because a state machine written as prose is a state machine nobody
  checks.
- **Invariants** — the things that must be true of a row no matter which code path wrote it, and
  which constraint enforces each one.

## Why the invariants matter more than the fields

A field list decays the moment someone adds a column. An invariant is the durable part: *an order
never carries more assigned cleaners than it has seats*, *a promo code is redeemed at most once per
user*, *a receipt number is never reused*.

Each of those is enforced by something specific — a unique index, a check, an append-only seam — and
naming the enforcer is what stops a future change quietly removing it. Where an invariant is enforced
only by convention rather than by the database, this section says so plainly.

## What is here now

The entity reference and the state-machine diagrams are being written. Until they land,
[Architecture → Database](/architecture/database) carries the schema description.
