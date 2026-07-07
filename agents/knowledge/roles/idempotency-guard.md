# Role — `IIdempotencyGuard` (CRC card)

> Introduced by **ADR-0002 D2.2** (the canonical consumer-dedup abstraction), made durable by
> **ADR-0010** (`DbIdempotencyGuard` over the `ProcessedMessage` unique-row, mirroring
> `ProcessedStripeEvent`), given its **two-mode surface** by **ADR-0023**. Interface:
> `src/Cleansia.Core.Queue.Abstractions/IIdempotencyGuard.cs`; production backing:
> `DbIdempotencyGuard` (scoped, registered in `RepositoryExtensions.AddRepositories`); test double:
> `InMemoryIdempotencyGuard`.

## Responsibility (one sentence)
Record and answer, durably and per deterministic `MessageKey`, whether a queue consumer's terminal
effect has been claimed (**Mode A**, `AlreadyProcessedAsync` — atomic claim-before-act, at-most-once
after the marker) or completed (**Mode B**, `HasProcessedAsync` non-claiming read +
`MarkProcessedAsync` post-success claim — at-least-once), each write committing in the guard's **own**
unit of work with the UNIQUE `MessageKey` index as the arbiter (PG 23505 → "already claimed"/benign
no-op).

## Collaborators
- `ProcessedMessage` (tenant-global `BaseEntity`, UNIQUE `MessageKey` — ADR-0010 D1) + its repository.
- The scoped `DbContext` — reads are non-tracking existence checks via `IgnoreQueryFilters().AnyAsync`
  (the table is tenant-global, a reasoned S8 exception).
- The shared PG-23505 detection helper (the webhook path's) — never hand-rolled SQLSTATE parsing.
- The consumers: `SendPushNotificationHandler` (Mode A), `SendEmailHandler` (Mode B, ADR-0023).

## Does NOT know
- **What the message is or what the effect does** — the `MessageKey` is opaque to it (the prefix is
  self-describing for ops, not for the guard).
- **Which mode a consumer should use** — that is the ADR-0023 repeatable-effect test, decided per
  consumer in an ADR/ticket, visible as the member name at the call site.
- **The ack/retry policy** — the *handler* decides what to do with the answer, including catching a
  transient `MarkProcessedAsync` failure and acking ("sent but unclaimed"); the guard only swallows the
  unique violation, never other failures.
- **The consumer's commit** — the guard owns its own `CommitAsync`; it never rides the consumer's (or
  MediatR's) unit of work.
- **The tenant** — the row is deliberately tenantless, so tenant scoping never applies: Mode A claims
  before any tenant override is set; Mode B's mark may run inside one, harmlessly.

## Watch-list (from ADR-0010 D2, still live)
If a future consumer ever shares one `DbContext` across the claim **and** a deferrable business commit
that could roll the claim back, the guard MUST open a fresh scope for the claim. Today no consumer
does: the push consumer's only other commit is the post-send dead-token prune; the email consumer's
only commit is the guard's own.
