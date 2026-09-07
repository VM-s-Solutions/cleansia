# Handover — pre-release work, written 2026-09-07

Paste this at the top of a new session. It is the map; the detail is in the tickets it names.

## How to run these

**One item per session.** Not because sessions get long, but because a session that carries two
agreed scopes starts blending them — which is what `CLAUDE.md`'s working agreement exists to stop.
Reading is safe to batch; building is not.

Each item below follows the same flow, and it is the flow that worked:

1. **Investigate first, and ground-truth the ticket.** Every ticket here is a claim about the code
   on 2026-09-07. Check it still holds before acting on it — one grep is cheaper than one wasted
   session, and this backlog has been wrong before.
2. **Agree the scope, with a NOT list**, before writing anything.
3. **Decide the open questions.** Most items below are blocked on a decision, not on typing. The
   ticket names them under *Open decisions*.
4. **Then build**, and measure whatever the ticket says to measure.

## Start here — these are defects, not improvements

Three of them are live in production right now.

| | |
|---|---|
| **[T-0685](backlog/tickets/T-0685-retention-job-never-runs-in-production.md)** | The job that deletes personal data the platform may no longer keep has **never run in production**. It starts, decides it is switched off, and logs success. Verified four ways. |
| **[T-0686](backlog/tickets/T-0686-terms-acceptance-is-never-recorded.md)** | **Nothing records that a customer accepted the terms**, or which version. The social sign-ins check the box and throw it away. This is the first thing any chargeback argument needs. |
| **[T-0688](backlog/tickets/T-0688-multicurrency-is-a-mechanism-that-has-never-run.md)** | Multicurrency has **never run at an exchange rate other than 1**, and `CreateOrder` accepts any currency id from any caller. Decide "single-currency at launch" explicitly and the ticket shrinks to almost nothing. |

Two cheap ones worth taking while nearby:
**[T-0687](backlog/tickets/T-0687-admin-order-list-renders-a-blank-status.md)** (every `New` order
shows a blank status pill in the admin list — `OrderStatus.New` is `0` and the guard is `if (!value)`)
and **[T-0689](backlog/tickets/T-0689-feature-flags-that-gate-nothing.md)** (six admin switches that
control nothing — an admin can turn Stripe "off" and payments keep working). Do T-0689 together with
T-0685; they ask the same question about what a production flag table should contain.

## Then, in this order

**1 — Order status: card vs cash.** *Investigate is already done, and the finding is not what it
looked like.* Both payment types are created identically as `(New, Pending)`. The card path diverges
seconds later because the Stripe webhook writes the **fulfilment** axis: it sets `Paid` **and**
appends `Confirmed`. So `New`-cash and `Confirmed`-card are the same business state — "live, nobody
assigned". `Confirmed` is also what a cleaner taking the job produces, so the word covers two
unrelated events. **The decision:** is `Confirmed` allowed to keep meaning two things (ADR-0037 says
yes)? If yes this is presentation only and T-0687 is most of it. If no, it is a domain change across
eight-plus consumers including both mobile apps.

**2 — Legal documents vs. the built processes.** Needs the documents; nothing to read in the repo
until they arrive. **Do this BEFORE the customer audit log** — the review defines what must be
provable, and building the log first means recording the wrong fields, which cannot be back-filled.

**3 — Customer audit log.** *Answer to the question already asked: there isn't one.* Only
`AdminActionAudit` and `EmployeeActionAudit` exist. The audit pipeline does run on the customer host,
but everything drops because the gate requires an Administrator role claim. Today a support agent can
prove an order cancellation, dispute messages the customer wrote, and login IP while the token lives.
They cannot prove what T-0686 covers. Size L, and it needs the legal basis first — evidence kept for
incident defence has to survive an erasure request.

**4 — Automatic admin notifications** (disputes created/updated, orders cancelled by us or by a
cleaner, and similar edge cases). **Do this AFTER the status decision** — every notification names a
status, and encoding today's ambiguity into templates across five locales means paying twice.

**5 — Responsiveness of the customer site.** Known starting point: the order page wants its summary
moved to the bottom as an expandable panel. Iterative and visual; give it its own session and a real
device.

**6 — iOS/Android content under the system clock.** Real and structural, and bigger than it sounds:
`toolbarBackground`, `scrollEdgeAppearance` and `UINavigationBarAppearance` return **zero hits**
across both iOS apps — eleven sites deliberately hide the system bar and paint a flat colour behind
it, so there is nothing to blur. ~16 iOS screens across ~15 files, plus 4 Android screens, with no
shared top-chrome component to change once. The true system fade is iOS 26 `scrollEdgeEffectStyle`;
at this project's iOS 16 floor it needs a material inset instead. Android has no blur primitive at
`minSdk 26` — a gradient scrim is the approximation.

**7 — Remove the membership free trial** — [T-0690](backlog/tickets/T-0690-remove-the-membership-free-trial.md).
*Investigated, and the headline is not the one expected.* Plus costs 199 Kc/month with a **14-day free
trial**, and during it a customer gets almost every paid benefit. The 5% discount is real but it is
the **third**-largest giveaway. Ahead of it:

1. **Recurring schedules, which outlive the trial.** A trialing member can create a recurring
   booking, and the schedule keeps generating orders after the trial lapses — deliberately
   (`MaterializeRecurringBookingTemplate.cs:205-206`). The code's own comment worries that "one paid
   month buys a permanently re-specifiable scheduling engine"; with the trial, zero paid months do.
2. **The free-cancellation window**, widened 24h -> 4h. A non-member pays a 25% fee in that band; a
   trialing member pays nothing, with no cap on how often.

The express-upgrade waiver is the only benefit already withheld during a trial. Credit and loyalty
points are not membership benefits at all.

**The cheapest removal is a data change** — set `TrialPeriodDays = 0`, no deploy and no migration —
**but it is not sufficient on its own**: Android and iOS degrade correctly, while the Angular Plus
page and the order wizard advertise the trial unconditionally. Route A plus those frontend guards is
the recommendation; the ticket has both routes.

Two things worth deciding alongside it: the recurring-schedule giveaway is **separable and outlives
the trial** (a single paid month still buys a forever schedule), and the written benefit list in
`docs/product/business-rules.md` is already incomplete.

## Two process changes made on 2026-09-07

- **Nothing is owner-only any more.** NSwag client regeneration, the EF migration regen and the DEV
  database drop are ordinary work — run them, do not write `manual_step:` on a ticket, and do not
  hold work waiting for the owner. `CLAUDE.md` → *"Manual steps — there are none left"*. The one
  genuine exception is Xcode signing and provisioning. **In exchange: name every such step you ran
  in the report**, because that is now the only way the owner learns it happened.
- **Write for someone who has not read the conversation.** The owner does not follow the scrollback.
  Every question and every summary states the situation in plain language first — what a customer or
  an admin actually sees, what it costs — and puts the mechanism second. Expand an identifier the
  first time it appears rather than assuming the name carries its meaning.
