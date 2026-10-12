# System jobs — the shapes a job takes, and how one item or one command fails without the others

> **Status of this page: THE CURRENT SHAPE — binding.** [ADR-0072](/decisions/adr-0072) is `accepted`
> (2026-10-12) and shipped with this page. The ADR is the immutable record with the
> `## Challenge` / `## Defense` / `## Verdict` trail; this page is the evolving companion.
>
> Companion pages: [`order-concurrency.md`](./order-concurrency.md) (the order-ending credit return that joins
> its commit, and the order several customers' credit locks are taken in),
> [`multi-tenancy-and-region.md`](./multi-tenancy-and-region.md) (where a job's tenant comes from).
> Published view: `docs/flows/cross-cutting.md` (the tenancy of a job), `docs/architecture/infrastructure.md`
> (the timer table).

---

## The shapes a job takes

A system job carries no JWT, so its input decides both its tenancy and its unit of work.

| Shape | Input | Unit of work | Reference |
|---|---|---|---|
| Row-driven | rows read across every company | one commit per company's group, inside the loop | `CleanupStalePendingOrders` |
| Row-driven, one scope per item | ids read across every company | one DI scope per item, committed inside it (by the pipeline; the materializer commits itself) | `MaterializeRecurringBookings`, `AutoCancelStaleRecurringOrders`, `RetryFailedUserDeletions` |
| Per company, from the registry | `ITenantRepository.GetAllIdsAsync()` | each sweep commits under the company's override | `DataRetentionBackgroundService` (its tenancy; its catch is a deviation, below) |
| Per company, named by an administrator's act | the message envelope's company | every step commits on its own | `CompanyWindDownService`, `CompanyArchiveService` |

**Which row-driven form a job takes** is decided by whether its items must fail independently. If one bad row
must not cost the others their run — one customer's broken template, one occurrence whose notice cannot be
staged — the job reads ids and gives each item a scope. If the job can commit a whole company at once and a
refused company may end the run, it keeps the plain form with no catch at its commit; the next run takes the
company again.

## The unit rule (ADR-0072 D2)

**A job whose items must fail independently gives each item its own DI scope. A loop never continues past a
failed commit, or past staging that a later commit in the same scope will save, with or without `Rollback()`.**

The dispatcher reads ids only and, per id, checks for cancellation, opens `await using var scope =
serviceScopeFactory.CreateAsyncScope()`, and sends the item's command. It rethrows an
`OperationCanceledException` while cancelled; it logs any other exception, and any failure result, at Error and
counts it in its response. The item's handler reads its row again, checks again that it still qualifies, runs
under that row's company, and does not catch. `AutoCancelRecurringOccurrence` and `AdminRetryUserDeletion` leave
the commit to the unit-of-work pipeline inside their own scope, where the same tenant override and the same
credit-lock transaction live. `MaterializeRecurringBookingTemplate` commits itself, and switches the override to
the market's operator before its last commit, because a new row is stamped from the tenant ambient at commit
time; it is a known exception to the handler rule, not the shape to copy.

Why a scope and not a catch:

- A failed `CommitAsync` leaves every tracked entry as it was, so a caught failure rides the next item's commit,
  or the pipeline's commit after the loop.
- `Rollback()` turns an `Added` row into an `Unchanged` phantom that a later commit drags along, and the
  pending-dispatch buffer keeps the outbox keys it already saw.
- Since ADR-0073 a refused commit rolls back the credit its order returned, so the leftovers would commit the
  order's end without its credit.

## A timer that carries several commands (ADR-0072 D1)

A timer shares one trigger between commands only to save a trigger. Each command is sent from a DI scope of its
own; an `OperationCanceledException` while the host is stopping is rethrown at once; any other exception is
logged at Warning, naming the command, and the next command runs; after the last, one failure is rethrown as
itself and several as an `AggregateException`, so the invocation still fails and the host's error middleware
logs it once. A failure result is logged at Error and never thrown.

| Timer | Its commands, each in its own scope |
|---|---|
| `CleanupStalePendingOrders`, every 15 minutes | `CleanupStalePendingOrders`, `ReleaseOrphanedBenefitReservations`, `CancelUnfilledOrders` |
| `AutoCancelStaleRecurringOrders`, hourly | `AutoCancelStaleRecurringOrders`, `RedrivePendingRefunds` |

Only these two timer handlers send more than one command. The source guard in `TimerCommandIsolationTests`
fails any handler under `src/Cleansia.Functions.Core/Handlers` that gains a second `Send(` without
`CreateAsyncScope`.

## Known deviations

The jobs that still catch past a commit, or past staging, in a shared scope are listed in
`agents/knowledge/consistency.md` §"A loop that continues past a failed commit in a shared scope", with the search
that found them. The roster is descriptive; the rule decides the next case. Converting them is a canonicalization
ticket for the owner to schedule. `SendRecurringOrderReminders` comes first: since ADR-0071 made the order's
payment status a concurrency token, one payment landing during its run fails every later reminder of that run.
`RedrivePendingRefunds` moves money.

## Residuals

- **No attempt cap.** A poison occurrence, like a poison template, is retried and logged at Error on every run.
  The visible places are Sentry and the response's `Failed` count; a cap would need persisted state for a
  failure nobody has seen yet.
- **The listed deviations stay live** until the canonicalization ticket is scheduled.

## Tests that pin it

- `src/Cleansia.Tests/Functions/TimerCommandIsolationTests.cs` — each command on the two shared timers runs in its
  own scope, a thrown one never stops the others and escapes after the last, and the source guard.
- `src/Cleansia.Tests/Features/Bookings/RecurringSweepPerTemplateIsolationTests.cs` — a broken template neither
  starves the others nor leaks its half-built order into their commits.
- `src/Cleansia.Tests/Features/Bookings/RecurringAutoCancelPerOrderIsolationTests.cs` — a failing occurrence stays
  as it was and holds back nobody, each occurrence is stamped with its own company, an occurrence already
  cancelled is not taken, and the unit leaves an occurrence that no longer qualifies.
- `src/Cleansia.Tests/Features/Bookings/AutoCancelRecurringOccurrenceValidatorTests.cs` — the unit's cutoff is at
  most a day ahead.
- `src/Cleansia.IntegrationTests/Features/Bookings/RecurringAutoCancelCreditAtomicityTests.cs` — on Postgres, a
  refused cancellation returns no credit and the next run returns it once.
