# Open questions for the owner

One heading per question, id `Q-<AREA>-<NN>`. A question lives here only while it is **open** — when
the owner answers, the answer goes into the ticket or the ADR it unblocks and the question is deleted
from this file. This is a queue, not a record; the record is wherever the decision landed.

**If a question is blocking a ticket, the ticket's row in [`../INDEX.md`](../INDEX.md) is `blocked` and
names the `Q-` id.** A question with no blocked row behind it is a question nobody is waiting on.

> **Q-AUD-L1 … L6 and Q-AUD-O1 … O3 were answered by the owner on 2026-09-14** and are deleted from
> here per the rule above. The record is `docs/decisions/adr-0062.md` §Rulings (one table, the default
> filed beside the ruling and where it landed); the work is T-0738 … T-0748. **The three that batch
> raised — Q-GDPR-01 (erasure by e-mail), Q-GDPR-02 (disputes in the export), Q-AUD-O4 (name the
> refused account) — were answered on 2026-09-15**, all *yes*, and are deleted too; the record is
> ADR-0062's second §Rulings table and the work is T-0750 … T-0752. One question they raised is
> below (Q-GDPR-03).

> **Q-TENANCY-01 … 05 and Q-PUSH-01 were answered by the owner on 2026-09-15** and are deleted from
> here per the rule above. The record: `docs/decisions/adr-0061.md` §Rulings (one table — the default
> filed beside the ruling and where it landed — plus the dated Stripe note: *one holding Stripe account
> for now, card revenue settled intercompany; cross-market booking is Batch 3; deactivation's a → b → c
> is Batch 2*); the work is T-0754 … T-0759. Q-PUSH-01's record is `docs/decisions/adr-0054.md`
> §Rulings (*the digest is not silenceable*; T-0756). Q-VS-03 — the accounting premise ADR-0046 §D3.2
> was contingent on — was answered by the same ruling as Q-TENANCY-02 and is recorded in ADR-0046's
> superseding note.

## Q-GDPR-03 — Should a LIVE guest booking under the erased e-mail block the erasure?

**Raised by:** the review of T-0751 (erasure by e-mail — your ruling on Q-GDPR-01, 2026-09-15).
**Who answers:** the owner.
**Why it needs you:** your ruling made the erasure reach the guest bookings placed with the erased
account's e-mail. The first cut widened the *blocking* check to that set too, so a `New`/`Confirmed`
guest booking under the subject's e-mail refused the erasure with `gdpr.deletion_blocked_by_order` —
exactly as the account's own live orders do. The review reverted that, for this reason (the fix
commit's words): *"a guest booking has no cancel path (`CancelOrder` refuses `order.UserId != userId`;
nothing anonymous cancels), so the subject, and `AdminDeleteUserAccount` through the same
`FindRefusalAsync`, were dead-ended on an order the account does not list until the job completed or
an admin cancelled it, over what may be a stranger's typo. The blocking rule was never part of the
2026-09-15 ruling (T-0738 NOT: no change to the blocking rules), so it is back to the account's own
orders, and the walk leaves a live guest booking out instead … with the bounded residual stated on
`ErasureBlockingStatuses`: its contact data stays until the job ends and the order-PII sweep reaches
it (the guest audit rows go with the three-year sweep). Whether a live guest booking SHOULD refuse
stays an owner question; this is the default that needs no ruling."* **Default applied: left out, not
refused.** The two honest options: (a) keep it — the subject's erasure completes; the live guest
booking's name, phone and address stay on the order (a cleaner is about to work it) until it ends and
the two-year sweep reaches it; or (b) refuse, like the account's own live orders — consistent, but
only defensible once a guest can cancel their own booking (**T-0753**, filed on your word), otherwise
a stranger's mistyped address locks the subject out of their erasure until an admin cancels.
**Answer needed:** (a) or (b) — and if (b), whether it waits for T-0753.
**Blocks:** nothing. T-0753 is filed either way.
