# Open questions for the owner

One heading per question, id `Q-<AREA>-<NN>`. A question lives here only while it is **open** — when
the owner answers, the answer goes into the ticket or the ADR it unblocks and the question is deleted
from this file. This is a queue, not a record; the record is wherever the decision landed.

**If a question is blocking a ticket, the ticket's row in [`../INDEX.md`](../INDEX.md) is `blocked` and
names the `Q-` id.** A question with no blocked row behind it is a question nobody is waiting on.

## Q-CAP-01 — When an admin sets or lowers a cleaner's order cap, must the platform notify that cleaner?

**Raised by:** ADR-0053 (architect panel, author D5b; filed by the lead 2026-08-23 — the ADR recorded
this as escalated and it was not yet here).
**Why it needs you:** `Employee.WeeklyOrderLimit` is settable by one admin against one cleaner, unset
by default. Today the cleaner's only feedback is a refusal at the moment they try to take work
(`order.weekly_limit_reached`, an **argless** key — `BusinessErrorMessage.cs:98` — so the copy cannot
say what the cap is, who set it, or when it resets). A restriction on how much a person may earn,
delivered silently and discovered at the till, is a decision about how the platform treats the people
who earn on it, not an implementation detail.
**Answer needed:** notify on set / on lower / not at all — and does the same answer apply to **raising
or clearing** it? (Clearing is a gift; lowering is an income cut.)
**Blocks:** the admin read-surface ticket for the cap. Until it is answered, ADR-0053's standing
constraint holds: the write endpoint is **not exercised in production**.

## Q-PUSH-01 — May a cleaner silence the evening "jobs tomorrow" digest?

**Raised by:** ADR-0054 (architect panel, author D4; filed by the lead 2026-08-23 — same reason).
**Why it needs you:** `order.reminder_tomorrow` has no `GetCategoryFor` arm
(`NotificationEventCatalog.cs:154-174`), so it is non-mutable by omission — an unsilenceable 18:00
push, plus a feed row on every evening the cleaner has work. The ADR defends that on the digest's own
facts (it is the only notice that arrives in time to *arrange* the day — transport, childcare, a
second job — and the T-2h reminder cannot substitute for it). The two per-job reminders are not in
question; they are the last line before a no-show.
**Answer needed:** does `ReminderTomorrow` get a category (silenceable), or stay non-mutable? If it
gets one, ADR-0054's required change 4 (collapsing the feed row) becomes optional, so the two should
be answered together.
**Blocks:** nothing today — the digest ships non-mutable and granting a category later is an additive
arm on one switch.
