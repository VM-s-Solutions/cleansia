# ADR-0023 — Per-consumer claim ordering: claim-before-act stays mandatory for non-repeatable effects; the send-email consumer moves to claim-AFTER-successful-send (at-least-once)

- **Status:** accepted   <!-- proposed | accepted | superseded | rejected -->
- **Date:** 2026-07-08
- **Supersedes:** ADR-0010 **partially** — only (a) the **at-most-once-after-the-marker ordering as
  applied to the send-email consumer** (ADR-0010 D2's semantic as the email path consumes it via D6)
  and (b) the **"no interface change" invariant** (header + D6): `IIdempotencyGuard` gains two
  **additive** members; `AlreadyProcessedAsync` itself stays byte-for-byte. **Everything else in
  ADR-0010 stays in force** — the durable `ProcessedMessage`/`CampaignProgress` tables (D1/D3), the
  unique-index dedup, the claim-in-its-own-committed-unit-of-work algorithm (D2), the S8 tenant-global
  exception, the scoped DI shape (D5), and the push consumer's guard-first contract are untouched.
- **Superseded by:** —
- **Backs / extends:** ADR-0002 D2.2 — this ADR **selects the alternative D2.2 itself recorded** for the
  email terminal effect ("…OR explicitly accept the rare double-email and document it",
  `0002-outbox-dispatch-contract.md:272-273`) and generalizes it into a per-consumer rule. The D2.1
  deterministic keys, D2.1a dual-read, D2.3 fan-out carve-out, and the D3 poison/dead-letter floor are
  unchanged. Also backs ADR-0005 (its D2.2 cross-reference is satisfied by either mode).
- **Applies to:** backend | functions | cross-cutting
- **Ticket:** — (owner ruling, approved 2026-07-08) · **Consumer:** one backend ticket (PM to file):
  the two additive `IIdempotencyGuard` members + the `SendEmailHandler` reorder + the interface
  doc-comment rewrite + the test contract below. **No migration** (the `ProcessedMessages` table is
  unchanged), **no NSwag** (no DTO/endpoint surface).

> **One decision:** *when the idempotency marker is written* is a **per-consumer decision governed by
> the repeatable-effect test** — claim-**before**-act stays **mandatory** where a duplicate effect is
> not safely repeatable (anything money-shaped: receipt/invoice generation, pay calculation, fiscal
> registration); claim-**after**-successful-act is **permitted** where a duplicate is benign. The first
> and only consumer ratified onto claim-after here is **`SendEmailHandler`** (at-least-once: a failed
> send stays unclaimed so the queue retry genuinely retries; worst case is a rare duplicate email).
> The rule + its motivating application are one decision — the ordering flip *is* the rule's first
> instance, and the rule without an instance is folklore. Once `accepted` this is immutable —
> supersede, never edit.

> **Owner decision this ADR records (approved 2026-07-08):** the send-email queue consumer moves from
> claim-before-send (at-most-once — a send failing *after* the claim is permanently lost, which really
> happened during the SendGrid config-gap incident) to claim-after-successful-send (at-least-once — a
> failed send stays unclaimed so the queue retry genuinely retries; worst case is a rare duplicate
> email). Claim-before-act stays **mandatory** for consumers whose effect is not safely repeatable.
> **Push notifications are a CANDIDATE follow-up, NOT in scope** — `SendPushNotificationHandler` stays
> byte-untouched.

---

## Context

### The incident: claim-then-act + a config gap = permanent, silent loss

`SendEmailHandler.HandleAsync` (`src/Cleansia.Functions.Core/Handlers/SendEmailHandler.cs`) is
claim-first: it claims the deterministic key via `idempotencyGuard.AlreadyProcessedAsync(messageKey)`
at `:72` **before** the terminal `SendAsync` at `:86`. Since ADR-0010 the claim is **durable** — a
`ProcessedMessage` row committed in the guard's own unit of work, by design surviving anything that
happens afterwards.

During the SendGrid config-gap incident (the dynamic-template ids the app binds were missing from the
deployed configuration — since closed by routing all 6 template ids via GitHub secret → Key Vault; see
the deploy runbook), every send threw. Under claim-first the failure sequence was:

1. First delivery: the claim row **commits**, `SendAsync` **throws**, the handler re-throws (`:96`) so
   the queue retries — so far correct.
2. Every redelivery: `AlreadyProcessedAsync` finds the durable claim → logs
   `"Email {MessageKey} already sent, skipping (idempotent)"` (`:74`) → **acks**.

Net effect: each confirmation/password-reset email got exactly **one** attempt against a broken
config; the retry machinery — the whole reason the effect rides a queue — was neutered by the claim;
the messages drained green ("skipping (idempotent)") while **zero emails were sent, permanently**.
The at-most-once residual ADR-0010 D2 recorded as "a crash between claim and send loses that one
push/email — accepted" turned out, for email, to cover not just a rare crash but **every send failure**
— a whole outage class, invisible in telemetry because the loss is logged as success.

### The trade-off was already on the record — this ADR picks the other branch

ADR-0002 D2.2 explicitly named both options for the email terminal effect: *"mark the email-sent state
in a commit that precedes the send (claim-first), accepting a rare lost email on crash, **OR explicitly
accept the rare double-email and document it**"* (`0002:272-273`). The implementation took claim-first;
the incident showed the accepted residual was mispriced for email (loss is permanent + silent; a
duplicate is a nuisance the recipient shrugs off). The owner has now ruled: for email, the rare
duplicate is the right residual. This ADR records that ruling and draws the general boundary so future
consumers don't re-litigate it per-ticket.

### What is NOT in question

- The **durable backing** (ADR-0010): `ProcessedMessage` + unique `MessageKey` index + own-unit-of-work
  commit + 23505 conversion. Both modes below run on exactly this table and this guard implementation.
- The **deterministic keys** (ADR-0002 D2.1) and the dual-read (D2.1a): the email key stays
  `email:{Type}:{UserId}:{hash}` via `MessageKeys.Email(...)` (`SendEmailHandler.cs:70`).
- The **failure classification** (malformed/business-rejected → ack; infra/transport → throw so the
  runtime retries to maxDequeueCount and dead-letters): unchanged.
- The **push consumer**: `SendPushNotificationHandler` stays guard-first and **byte-untouched**.

---

## Decision

### D1 — The per-consumer claim-ordering rule (the repeatable-effect test)

Every effect-realizing consumer still MUST carry a dedup control (ADR-0002 D2.2: domain target-state
check preferred, the `IIdempotencyGuard`/`ProcessedMessage` backstop otherwise). **When the marker is
written is chosen per consumer by one test:**

> **The repeatable-effect test:** *if this consumer's terminal effect ran twice, would anything need
> un-doing — a refund, a reversal, a duplicate document or ledger row, a double charge, a double pay
> row?* **If yes → Mode A is mandatory. If a second run is at worst a nuisance → Mode B is permitted.**

- **Mode A — claim-before-act (at-most-once after the marker). MANDATORY for non-repeatable effects:**
  receipt/invoice generation, pay calculation, fiscal registration — anything money-shaped or
  state-mutating where a duplicate must be reversed. The consumer calls `AlreadyProcessedAsync(key)`
  (unchanged) before acting. Residual: a crash between the claim and the act loses that one effect —
  accepted, because the duplicate would be worse. Reference: `SendPushNotificationHandler` (today).
- **Mode B — claim-after-successful-act (at-least-once). PERMITTED where a duplicate effect is benign:**
  today **the send-email consumer only**. The consumer does a **non-claiming pre-check** → acts →
  **claims after success**. Residual: rare duplicates (the two windows in Consequences) — accepted by
  explicit owner choice. A failed act leaves **no row**, so the queue retry genuinely retries.

**Adoption discipline:** choosing Mode B for a new or existing consumer requires an ADR (or an explicit
ticket decision note citing this ADR's test), plus the two duplicate windows documented in the
consumer's doc-comment. **Never mix modes within one consumer.** Push notifications are a **candidate**
for Mode B (a duplicate ding is arguably benign) — explicitly **not decided here**; that flip, if ever,
is its own ruling with its own ADR/ticket.

### D2 — The send-email consumer moves to Mode B

`SendEmailHandler.HandleAsync` reorders to:

```
1. parse + validate (unchanged: malformed / missing-fields → log names only, ack)
2. key = MessageKeys.Email(...)                                  // unchanged formula (D2.1)
3. if (await guard.HasProcessedAsync(key, ct)) return;           // NON-claiming read → ack duplicate
4. tenant override (unchanged) → await SendAsync(message, ct)    // throw ⇒ NOTHING claimed ⇒ queue
                                                                 //   retry is a REAL retry
5. try { await guard.MarkProcessedAsync(key, ct); }              // post-success claim, own unit of work
   catch (Exception e) { log warning "sent but unclaimed — a redelivery may duplicate"; }  // ACK
```

Pinned details:

- **Step 3 is a filter, not the control.** It stops the common case (redelivery after a prior success)
  from re-sending; it does not pretend to be atomic. The residual race is window W1 (Consequences),
  owner-accepted.
- **Step 4's failure path is unchanged** — transport/infra failures still throw (`:91-97` today) so the
  runtime retries and eventually dead-letters; the difference is that the retry now finds no claim and
  actually re-sends.
- **Step 5 never throws.** Inside the guard, a unique violation (PG 23505 — a concurrent duplicate also
  sent and claimed first) is a **benign no-op** (both sent; the row exists; ack). Any *other* claim-write
  failure is caught **in the handler**, logged, and **acked** — throwing there would convert a
  bookkeeping failure after a *successful* send into a *guaranteed* duplicate via redelivery. The
  missing row only costs a duplicate if the queue independently redelivers (window W2's class).
- **Semantics of the row shift for `email:` keys:** a `ProcessedMessage` row now means **sent**, not
  *attempted*. (For `push:` keys it still means *claimed*.) The `MessageKey` prefix disambiguates —
  note this in the entity's doc-comment when touched, no schema change.

### D3 — `IIdempotencyGuard`: two additive members; the existing member is byte-frozen

```csharp
public interface IIdempotencyGuard
{
    // MODE A (byte-for-byte unchanged — the push consumer and any money-shaped consumer):
    Task<bool> AlreadyProcessedAsync(string messageKey, CancellationToken ct = default);

    // MODE B (new, additive):
    /// Non-claiming read: true iff the key has already been marked processed. Racy by design —
    /// a redelivery filter, not an atomic control.
    Task<bool> HasProcessedAsync(string messageKey, CancellationToken ct = default);

    /// Post-success claim: inserts the ProcessedMessage row in its OWN committed unit of work.
    /// A unique violation (23505) is swallowed — a concurrent duplicate already recorded success.
    Task MarkProcessedAsync(string messageKey, CancellationToken ct = default);
}
```

- `DbIdempotencyGuard` implements both new members against the **same** `ProcessedMessage`
  table/repo/unique index (ADR-0010 D1/D2): `HasProcessedAsync` = `AsNoTracking` existence read
  (`IgnoreQueryFilters` belt-and-braces, as the table is tenant-global); `MarkProcessedAsync` =
  `Add` → own `CommitAsync` → swallow only the shared PG-23505 helper's match, re-throw the rest
  (the handler's step-5 catch decides ack policy — the guard does not).
- Two **named** members, not a `bool claimFirst` parameter — the mode must be greppable at the call
  site (the same canonical-token logic as ADR-0002's verification check #3).
- The interface's XML doc-comment is **rewritten** by the consumer ticket: it currently hard-codes
  claim-then-act semantics *with the send-email consumer as its example* (`IIdempotencyGuard.cs:3-13`)
  — under this ADR that example is wrong. It must describe both modes and point the email example at
  Mode B, the push example at Mode A.
- `InMemoryIdempotencyGuard` (the retained test double) gains the same two members.

### D4 — Scope boundary

- **In scope:** `IIdempotencyGuard` (+ its two backings), `SendEmailHandler`, tests, the catalog +
  living docs (updated in the same change).
- **Byte-untouched:** `SendPushNotificationHandler`, `ICampaignProgressStore` and its backing, the
  `ProcessedMessages`/`CampaignProgresses` schema, the `MessageKeys` formulas, the queue/poison
  topology, the receipt/invoice/pay consumers (they remain Mode A / target-state, mandatory).
- **Candidate follow-up (needs its own ruling):** push → Mode B. Not assumed, not pre-approved.

---

## Alternatives considered

- **Keep claim-first and alert on the "already sent, skipping" log.** Rejected: alerting improves
  *detection*, not *delivery* — the retry machinery stays neutered, and the incident class (any
  config/provider gap) still permanently eats every affected email; a human then has to re-drive sends
  by hand with no record of what was actually sent (the claim row says "sent" for emails that weren't).
- **Claim-first + compensating un-claim (delete the row) on send failure.** Rejected: it shrinks the
  window instead of removing it — a process crash between the failed send and the delete (or during the
  delete) strands the claim and reproduces the permanent loss; compensation only works when the process
  survives to compensate. The ordering flip removes the window structurally.
- **Naked at-least-once (no pre-check, no marker — just send and ack).** Rejected: every
  visibility-timeout redelivery and every duplicate enqueue would re-send; the pre-check + post-claim
  keeps the common-path dedup that D2.1's deterministic keys exist to provide.
- **Make the send transactional with the claim.** Impossible — SendGrid is not a transactional
  resource; a send cannot roll back. Same rejection ADR-0002 recorded for enrolling FCM in the EF
  transaction. Only claim-before or claim-after exist; the choice is which failure mode you buy.
- **A `bool claimAfter` parameter on `AlreadyProcessedAsync`.** Rejected: hides a semantic mode behind
  a boolean at every call site and breaks the greppable canonical-token verification (ADR-0002 check
  #3). Two named members make the mode reviewable.
- **Flip push to Mode B in the same change (uniform at-least-once).** Rejected by the owner's explicit
  scope: a duplicate push is user-visible noise (a second ding) with a different annoyance profile, the
  incident evidence is email-only, and the ruling mandates `SendPushNotificationHandler` byte-untouched.
  Candidate follow-up, own ADR.

---

## Consequences

**Cheaper / safer:**
- **The email retry is real again.** A send failure — transient outage *or* a config gap — leaves the
  key unclaimed; the queue retries to maxDequeueCount and then dead-letters (D3 floor), where the
  poison row + alert make the failure *visible* instead of green. The incident class is closed
  structurally, not by monitoring.
- **The `email:` claim row becomes truthful** — it now records *sent*, not *attempted*, so ops queries
  against `ProcessedMessages` mean what they say.
- The rule is now explicit and cheap to apply: future consumers get a one-question test instead of
  re-deriving the trade-off, and the mode is greppable per call site.

**More expensive (accepted, by explicit owner choice):**
- **Email is at-least-once — duplicates are possible in exactly two windows:**
  - **W1 — concurrent redeliveries:** two deliveries of the same message pass the non-claiming
    `HasProcessedAsync` check simultaneously → both send; the loser's `MarkProcessedAsync` hits 23505
    (benign no-op). Rare: requires overlapping in-flight deliveries of one message.
  - **W2 — crash between send-success and claim-write** (including a transient claim-write failure that
    step 5 logs-and-acks): the row is missing, so a *subsequent redelivery* re-sends. Rare: requires a
    failure in that narrow window *plus* an actual redelivery.
  Both are bounded to "the recipient gets the same confirmation/reset email twice" — the codes are
  equally valid; no state is corrupted; nothing needs un-doing. That is precisely what the
  repeatable-effect test certifies.
- Two additive interface members + a doc-comment rewrite — a small, contained diff, but ADR-0010's
  "interfaces byte-for-byte unchanged" claim no longer holds in full (hence the partial supersede).
- Reviewers must now check *which mode* a consumer uses, not just *that* it has a guard — the
  verification section below and the catalog edit make that mechanical.

**No migration, no NSwag** — the table, index, and wire contracts are untouched.

---

## How a reviewer verifies compliance

**Mechanical:**
1. `SendEmailHandler.HandleAsync` order is: parse/validate → `HasProcessedAsync` (ack on true) →
   tenant override → `SendAsync` → `MarkProcessedAsync` wrapped in a narrow catch that logs and
   **returns** (acks). Grep: **no** `AlreadyProcessedAsync` call remains in the email path.
2. A failed `SendAsync` still **throws** out of the handler (transient classification unchanged) and
   the run has written **no** `ProcessedMessage` row for the key.
3. `IIdempotencyGuard.AlreadyProcessedAsync`'s signature is byte-unchanged; the two new members match
   D3; `DbIdempotencyGuard.MarkProcessedAsync` commits in its **own** unit of work and swallows only
   the shared PG-23505 helper's match; `HasProcessedAsync` is a non-claiming `AsNoTracking` read.
4. `git diff` shows `SendPushNotificationHandler.cs` **byte-identical**; `ICampaignProgressStore` and
   both entities/EF configs untouched; no new migration files.
5. The `IIdempotencyGuard` doc-comment describes **both modes** and no longer cites the email consumer
   as the claim-then-act example.

**Test contract (red first — TC-EMAIL-ALO-*):**
6. **TC-EMAIL-ALO-0 (the incident test).** `SendAsync` throws → the handler throws AND no
   `ProcessedMessage` row exists for the key; a subsequent delivery of the same message **sends again**.
   This test is **red against the current claim-first code** — it is the pin that the retry is real.
7. **TC-EMAIL-ALO-1 (dedup after success).** A successful send writes the row; a redelivery of the same
   message acks without a second send (`SendAsync` called once across both deliveries).
8. **TC-EMAIL-ALO-2 (post-success claim tolerance).** `MarkProcessedAsync` hitting a unique violation →
   the handler acks (no throw, no re-send); `MarkProcessedAsync` throwing a transient error → the
   handler logs the "sent but unclaimed" warning and acks.
9. **TC-EMAIL-ALO-3 (Mode A untouched).** The existing push idempotency tests pass unmodified.

---

## Roles affected

- **`agents/knowledge/roles/idempotency-guard.md`** (created with this ADR — the CRC card ADR-0002 and
  ADR-0010 promised): responsibility updated to carry the **two modes**; "does NOT know" still includes
  the ack policy (the handler owns step-5's catch) and what the message is. The ADR-0010 watch-list
  note (fresh scope if a consumer ever shares a context across the claim and a deferrable business
  commit) carries over unchanged.

Catalog + living docs updated **in the same change** (living docs are editable; ADRs are not):
- `agents/knowledge/patterns-backend.md` — new "Queue-consumer idempotency — the claim-ordering rule"
  section: the repeatable-effect test, both modes, the adoption discipline.
- `agents/architecture/decisions/outbox.md` — invariant 3 and the honest-guarantee table split
  push (Mode A) from email (Mode B); the incident + rule recorded as the current shape.

---

## Challenges pre-answered (deliberation trail)

The core trade-off (lost email vs. duplicate email) was **ruled by the owner on 2026-07-08** after a
real production incident — the panel's remaining scope was the mechanism and the boundary. The obvious
attacks, answered:

| # | Challenge | Disposition |
|---|---|---|
| CH-1 | "You traded silent loss for duplicates — that's not obviously better, just different." | **OWNER-RULED.** A non-transactional send only lets you pick which failure mode you buy (see Alternatives: transactional is impossible). For email the duplicate is benign and the loss was a real outage; the ruling prices the residuals and is recorded above. Not re-litigable per-ticket — that is what the repeatable-effect test is for. |
| CH-2 | "Keep claim-first and un-claim on failure — smaller diff, same result." | **REBUT.** Compensation shrinks the loss window; the ordering flip removes it. A crash between the failed send and the compensating delete strands the claim → the exact incident again. Evidence: the guard's own-commit design (ADR-0010 D2) makes the claim durable *by construction* — you cannot durably claim first and reliably un-claim on every failure path. |
| CH-3 | "This breaks ADR-0010's byte-frozen interface promise." | **CONCEDE → SUPERSEDE.** Correct — which is why this is a superseding ADR, not an edit (the house rule). The supersede is surgical: two additive members; `AlreadyProcessedAsync`, both entities, the schema, and the push consumer are byte-unchanged, verified mechanically (§verify #3-4). |
| CH-4 | "Push has the same shape — flip it too or the rule is inconsistent." | **SCOPED BY OWNER.** The rule *is* consistent — it is per-consumer by design; consistency of *mechanism* (one guard, one table) is preserved. Push's duplicate profile (user-visible ding) differs from email's, the incident evidence is email-only, and the ruling explicitly holds push as a candidate follow-up with the handler byte-untouched. |
| CH-5 | "The non-claiming pre-check is racy — either make it atomic or drop it." | **REBUT.** It is deliberately a *filter*, not the control (D2): dropping it re-sends on every redelivery-after-success; making it atomic *is* Mode A and reintroduces the incident. The race residual is window W1, named, bounded, and owner-accepted. |

**Verdict:** consensus — no challenge stands. The owner ruling settles the trade-off; the mechanism
survives the attacks above; the boundary rule makes the next consumer's choice a one-question test
instead of a debate. Accepted 2026-07-08.
