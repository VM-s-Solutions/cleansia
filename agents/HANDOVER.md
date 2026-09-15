# Handover — pre-release work, written 2026-09-07

Paste this at the top of a new session. It is the map; the detail is in the tickets it names.

## Where the repo is, before anything else

- Branch **`fix/customer-design-pass`**, **PR #252 open**, **167 commits ahead of `master`**, working
  tree clean, CI green at the time of writing.
- **Decide first whether that PR merges before this work starts.** Branching new work off it inherits
  167 unmerged commits; branching off `master` means the fixes land without the design pass. Neither
  is wrong, but picking by accident is.
- Everything below was established by reading the code on 2026-09-07. Nothing in this list has been
  fixed.

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

**Corrected 2026-09-08: none of these is live in production, because production has never been
deployed.** `deploy-pro.yml` has one run in its history and it was cancelled; `deploy-azure.yml` has
zero. These are "would ship broken on day one" defects, not incidents. That changes their urgency, not
their validity — and it is worth re-checking before anything else here is treated as on fire.

| | |
|---|---|
| ~~**T-0685**~~ **DONE (#253)** | The GDPR retention sweep never ran: its switch was a database row no deployed database ever had, and a missing row read as "off". Moved to configuration, defaulting ON. |
| **[T-0686](backlog/tickets/T-0686-terms-acceptance-is-never-recorded.md)** | **Nothing records that a customer accepted the terms**, or which version. The social sign-ins check the box and throw it away. This is the first thing any chargeback argument needs. |
| **[T-0688](backlog/tickets/T-0688-multicurrency-is-a-mechanism-that-has-never-run.md)** | Multicurrency has **never run at an exchange rate other than 1**, and `CreateOrder` accepts any currency id from any caller. Decide "single-currency at launch" explicitly and the ticket shrinks to almost nothing. |

One cheap one worth taking while nearby:
**[T-0687](backlog/tickets/T-0687-admin-order-list-renders-a-blank-status.md)** — every `New` order
shows a blank status pill in the admin list (`OrderStatus.New` is `0` and the guard is `if (!value)`).

> **The owner's actual feature-flag question was "can any of these features ship in V1?" — and the
> answer is that nothing is being held back by a flag.** The review *removes* work from the release
> list rather than adding it. Only two gates are real: `Fiscal:CzechEet2` is a genuine stub that
> returns `NOT_IMPLEMENTED` and cannot ship (it waits on a Czech tax API spec published for a 2027
> launch), and `APNS:Enabled` is **code-complete but credential-blocked** — the iOS Live Activity
> side exists, so it is a V1 option gated only on seeding three Key Vault secrets, not on
> engineering. Everything else named like a feature flag gates nothing at all.

## Then, in this order

**1 — Order status: card vs cash. ✅ DONE (T-0691, ADR-0057).** The decision was taken on 2026-09-08:
`Confirmed` is NOT allowed to keep meaning two things. It now means only "a cleaner took this job", a
paid card order rests at `New + Paid`, and the offerability status term dropped its payment qualifier
(the money term was always asking that question). ADR-0037 D1 is superseded in part by ADR-0057.

Two things worth carrying forward: the mobile booking timelines needed **no** change — they already key
on `cleanerAssigned` and caption the step "Cleaner confirmed", so the split made them more truthful, not
less — and item 4 below is now unblocked.

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
cleaner, and similar edge cases — the owner's words: "we have to know about those in advance").
**Do this AFTER the status decision** — every notification names a status, and encoding today's
ambiguity into templates across five locales means paying twice.

Decisions this one is blocked on, none of them technical: **email, in-app, or both** (the owner named
both and did not choose); **which events actually warrant interrupting someone**, since a channel
that cries wolf gets muted and then the real one is missed; and **who receives them** — one address,
a role, or per-tenant. Start from the event list, not from the transport. Note the platform already
has an outbox with a `MessageKeys.Push` uniqueness contract, so there is machinery to build on rather
than start from.

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

---

## The prompt to open the next session with

Copy the block below. Replace the one bracketed line with the item you want, and delete the rest of
that list.

```text
Read agents/HANDOVER.md first — it is the map for this work and I will not be
repeating its contents in chat.

This session does ONE item, and only this one:

  [ T-0685 — the GDPR retention job that never runs in production ]

Work it in this order, and stop at each boundary:

1. INVESTIGATE FIRST. The ticket is a claim about the code as it was on
   2026-09-07. Verify it still holds before you plan anything, and tell me
   plainly if it does not — I would rather hear "this is already fixed" than
   get a fix for a problem that moved.

2. THEN AGREE SCOPE WITH ME, with a NOT list, before you write any code. Tell
   me what you are changing, what you are deliberately leaving alone even
   though you can see it is imperfect, and what "done" looks like.

3. ANSWER THE OPEN DECISIONS in the ticket before building. Most of these
   items are blocked on a decision, not on typing. When you ask me, assume I
   have not read anything else in this session: state what exists today, what
   is wrong with it, the options with what each one costs, and your
   recommendation with its reason. Explain it in plain language — what a
   customer or an admin actually sees — before any file or symbol names.

4. THEN BUILD, and measure whatever the ticket says to measure. A change that
   does not move the number gets reverted, not kept.

Some things about how I work that are not obvious:

- Nothing is owner-only any more. Run the NSwag client regeneration, the EF
  migration regeneration and the DEV database drop yourself. Do not write
  manual_step: on a ticket and do not wait for me. The one exception is Xcode
  signing and provisioning. In exchange, name every one of those steps you ran
  in your report — that is now the only way I find out.
- Anything you find outside the agreed scope goes on a list and is reported at
  the end. Do not fix it while you are in there.
- Do not commit or push until I ask.
- Do not credit Claude anywhere: no commit trailer, no PR line, no comment.

Pick the item above, investigate it, and come back to me with what you found
and the scope you propose. Do not start changing files yet.
```

### Which item to put in that bracket

**If you want the most urgent:** `T-0685` — it is live in production now.
**If you want the cheapest visible win:** `T-0687` — the blank status pill in the admin list.
**If you want the biggest money question:** `T-0690` — the free trial, or the recurring-schedule
giveaway underneath it.
**If you want the thing that unblocks two others:** the legal-document review (item 2), because the
customer audit log waits on it — but only start it when you have the documents to hand.
