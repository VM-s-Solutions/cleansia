# Role — `DeadLetter` + `IDeadLetterStore` + `PruneDeadLetters` (CRC card)

> Introduced by **ADR-0002 D3** (the poison floor: persist + alert + ack, never re-process). Its
> **retention** half is governed by ADR-0002's **partial supersede of 2026-08-10 (T-0584)** — the row
> is evidence with two clocks, not an archive. Entity:
> `src/Cleansia.Core.Domain/DeadLettering/DeadLetter.cs`; store interface:
> `src/Cleansia.Core.Queue.Abstractions/IDeadLetterStore.cs`; backing:
> `src/Cleansia.Infra.Database/DeadLetterStore.cs`; writers: `PoisonHandlerBase.cs:80` (all seven
> `-poison` consumers) and `OutboxDrainerService.cs:86` (retry budget exhausted).
>
> **Status of the sweep:** `PruneDeadLetters` is **not yet built** — it lands with T-0584's build spec
> (ADR-0002 §A6). This banner retires when
> `src/Cleansia.Core.AppServices/Features/DataRetention/PruneDeadLetters.cs` exists.

## Responsibility (one sentence)
Hold, for a **bounded** and **stated** window, durable evidence that one message failed terminally —
which queue, which deterministic `MessageKey`, which tenant, what error, when, how many bytes — so a
poisoned effect is noticed and triaged instead of silently lost.

## Collaborators
- Every `-poison` consumer (one per `QueueNames` constant) via `PoisonHandlerBase` — which persists
  **before** it alerts, guards the persist so a DB fault cannot re-poison, and acks either way.
- `PoisonAlert` — the alert-side projection. It computes `(MessageKey, TenantId, Bytes, Fingerprint)`
  at `PoisonHandlerBase.cs:69`; two of those become the row's identity columns (ADR-0002 §A4.2).
- `OutboxDrainerService` — the second writer; its row is a **duplicate** of a `Failed`
  `OutboxMessage` that is itself never pruned.
- `PruneDeadLetters` (the two clocks) driven by the existing `PruneOutboxTimerHandler`.
- `AnonymizationMarker.Value` — the redaction token, shared with the order/dispute/user anonymizers.

## Does NOT know
- **How to replay itself, or that anyone could.** No code reads a `DeadLetter` row — no query, no
  admin endpoint, no replay command (verified 2026-08-10; **this claim retires the moment
  `IDeadLetterRepository` gains a reader**, which §Watch-list makes a superseding-ADR event, so it
  cannot rot quietly). **The row is the record that a thing failed — never the mechanism for making
  it succeed.** Recovery is somebody else's
  job: `FiscalReconciliationService` re-derives the fiscal message from domain state, and a poisoned
  `send-email` is recovered by a **re-issue** (`ResendConfirmationEmail` / `RequestPasswordChange`),
  never by replaying a 15-minute-expired token.
- **What is inside `RawBody`.** It never parses the body for meaning, never derives a tenant from it,
  and must not grow a column extracted from it beyond the two identity fields the envelope declares
  by name. That polarity — allowlist by field name, fail-closed — is `PoisonAlert`'s, and it is why a
  message type that gains a field tomorrow is withheld by default.
- **Which queue it came from, for retention purposes.** Both clocks are **uniform**; a `switch` on
  `SourceQueue` is a denylist maintained by memory, and its premise is backwards (the fiscal bodies
  carry no PII and no credential).
- **Whether the effect is still wanted.** It cannot be told, because nothing reads it — so it can
  never hold a body "because someone might need it". A clock it cannot extend is the whole point.
- **Who the subject is.** There is no `UserId` column, by design. The only subject handle is the
  `MessageKey` (`email:{purpose}:{userId}:…`, `push:{userId}:…`, `pay:…:{employeeId}`), which is why
  GDPR erasure of these rows is a **key-keyed** operation behind one named repository method — never a
  navigation walk and never an inlined `LIKE` over `RawBody`.

## Watch-list
- **A row with `MessageKey = "<unparseable>"` has no subject handle and never will.** For that subset
  the retention clock is the *only* control that ever removes the data — any completeness claim about
  erasure must exclude it in writing.
- **Amplification is real.** A permanently-failing fiscal message is re-enqueued by the reconciliation
  sweep every 5 minutes forever, minting a fresh row each poison cycle. Any change that lengthens or
  removes the row clock has to answer that first.
- **If a replay endpoint is ever proposed**, it changes the recovery role from nominal to real and
  therefore invalidates the retention shape. That is a superseding ADR, not a feature ticket.
