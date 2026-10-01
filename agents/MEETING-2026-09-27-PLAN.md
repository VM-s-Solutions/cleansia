# Meeting summary 2026-09-27 — alignment, decisions and plan

Source: `lawyers-docs/analyza-2026-09-27/Cleansia_obchodni_shrnuti_rev_2026-09-27 V.pdf` (14 pages, "Obchodní mapa
platformy, revize 27. září 2026"). Every statement in it was checked against the code on
`fix/post-policy-findings-2026-09-27` (PR #266):
- seven topic investigations, each re-checked by an adversarial verifier;
- a completeness pass over the full text;
- an independent coverage and accuracy review of this document.

That gives 204 verified items. Item ids (A1, B4, …) refer to that record.

> **Owner ruling 2026-09-28: every decision in Part 2 is taken at its recommendation (the bold option), and every
> Part 3 default is accepted.** Phases 0–4 and launch readiness are executed phase by phase. Phase 5 stays deferred,
> because the accepted defaults tie each of its items to a trigger that has not happened: the lawyer (PR-3 Q4, PR-10),
> launch, VAT registration, the fiscal law, or a second market. Phase 4 texts are our own drafts under the "Návrh"
> banner until the lawyer delivers.

---

## Part 1 — How the summary lines up with the project

### Where it matches what is built

- **Cash rule:** signed-in, one cleaner, never a guest.
- **Plus 60-minute grace.**
- **Cancellation fees:** the 25 % and 50 % tiers, and free cancellation while no cleaner is assigned.
- **Refunds:** the card and credit split.
- **Disputes:**
  - signed-in customers only;
  - the 24 h window only marks timeliness;
  - a failed refund keeps the dispute open.
- **Receipt content:** issuer, lines, language, local date.
- **The order belongs to the market's company.**
- **The four admin roles.**
- **Credit:**
  - 70 % cap, kept per currency;
  - never paid out;
  - expires 12 months after the last movement;
  - does not reduce the sale price.
- **Discounts:** member plus loyalty capped at 12 %; a better promo replaces them.
- **Loyalty points and their clawback.**
- **Express surcharge** of +20 % inside 4 h (one-off bookings only; recurring occurrences skip the 2 h floor).
- **Size limits:** 8 rooms and 4 bathrooms enforced on the server (some clients offer more, see bug 17), and no m² input.
- **Travel is in the price.**
- **No tips anywhere.**
- **The favourite cleaner is a priority offer, not a guarantee.** The summary omits two conditions: it is a Plus perk, and the cleaner must have served that customer before.

**Partial matches:**
- **The page-8 retention table matches, with two exceptions:**
  - logged-out devices are never deleted;
  - cancelled orders are never anonymised.
- **The after-photo is required to complete, with two holes:**
  - an admin override completes without one;
  - a cleaner can delete the photo after completion.

### Where the summary is out of date (correct it before it goes to the lawyer)

| Summary says | Actually |
|---|---|
| Positive credit blocks account deletion ("Kladný kredit blokuje výmaz účtu", p.7, p.8) | Since your 2026-09-24 ruling, deletion **forfeits** credit; nothing blocks on it. |
| Cleaner pay includes any recorded distance ("případné evidované vzdálenosti", p.5) | Distance was removed from pay on 2026-09-24. |
| PR-1: registration requires a saved card ("Registrace vyžaduje uloženou kartu") | **Never true.** Registration takes no card, a cash booking involves no card, and the web never saves cards. |
| Q7 asks whether credit may expire ("Smí kredit propadnout?") as an open question | Already ruled on 2026-09-05 (12 months after the last movement) and built. |
| Q8 asks about orders outside the customer account's market ("objednávka mimo trh zákazníkova účtu") as open | Already ruled on 2026-09-15 and shipped on 2026-09-16. |
| PR-8: the VOP gives no refund deadline | The lawyer's draft VOP **does** say 5 working days (art. V, 5.4). The in-app terms give none. |
| The Plus cancellation benefit is "60 minut zdarma" (p.7) | Plus also has a **4-hour free-notice window** (instead of 24 h). It is built, seeded and advertised on every client, but missing from the summary and the terms. |
| The draft VOP tiers are 50 / 100 % | The draft also uses **2 h**, not 4 h, and offers "50 % or credit". |

### Where the decisions require change (the big ones)

1. **60 minutes of free cancellation for a new customer.** This reverses your 2026-09-24 ruling (new customers get 15 min).
2. **Cash receipt only after the handover.** Today the receipt is issued, numbered and e-mailed **at booking**.
3. **Recurring cash.** The customer's confirm marks the occurrence **Paid** and e-mails a "paid" receipt before any money moves. The cleaner is never asked to collect, and nothing records that cash changed hands. This was excluded so far; the cash-receipt decision now depends on it.
4. **Lockout (100 %).** No flow exists: no "cannot get in" action, no reason, no fee path.
5. **Cleaner no-show (100 % + 250).** It is automatic only when *nobody took* the job. When an **assigned** cleaner does not come, the only self-service path is a self-cancel that costs **50 %**. Otherwise it is a dispute an admin settles by hand.
6. **Saved card as guarantee.** No off-session charge exists anywhere, the web never saves a card, and cash bookings have none.
7. **Dirtiness levels (+30 / +60 %).** Nothing exists. It is XL across price, receipt, pay, duration and three clients.
8. **Contract model.** The app's per-job contract is **customer ↔ cleaner, "Cleansia is not a party"**. You decided the company sells and subcontracts the cleaner.
9. **Reward via Stripe.** Cleaners are paid by a manual bank transfer; there is no Stripe Connect.
10. **Photos only in the app.** Android keeps capture files in its cache and allows picking from the gallery. iOS offers the photo library. Partner web accepts any file.

### The lawyer's drafts contradict your decisions (tell the lawyer in one package)

- Unused credit is paid out when an account is closed (2.4, 10.2).
- Tips go through the app (4.5).
- Cash is paid **directly to the cleaner** (4.2) and is due before the start (4.3).
- An invoice is issued only on request (4.4).
- The fee schedule uses 2 h / 50 % / 100 %, with "or credit" as an alternative to a refund.
- Rescheduling is promised (3.3, 5.4).
- The framework contract describes an **intermediary taking a commission**, settled twice a month.
- Penalties stack: 50 000 + 50 000 Kč, plus 150 000 Kč for the non-compete, plus 50 000 Kč in the DPA.
- The privacy draft says 18+ and "we verify age" (7.1); another draft says 16.
- The seeded VOP names no legal entity and lists services that are not sold (furniture, mattress cleaning).

### What it settles from the earlier open findings

| Earlier finding | Now |
|---|---|
| F1–F3 credit-leg money bugs | **Priority up.** Credit can never be paid out, so a lost credit leg can only be recovered by support. |
| F4 bookings record the newest terms version; F6 the legal catalogue shows every version "In force" | **Must be fixed before the next terms version**, which every decision here needs. |
| F5 the terms omit the Plus 4 h window | Still open (decision 5). |
| F7, F11 trial-member surfaces | **Close.** No trial can be created. |
| F8 multi-cleaner card orders lose the in-app Stripe repair | Accept (decision 27). |
| F9 the reminder invites confirming an ineligible legacy cash booking | **Close as moot.** Only pre-2026-09-24 DEV rows can carry it. |
| F10 recurring cash marked Paid early | **Now on the critical path** for the cash-receipt decision (decision 21). |
| F10 mobile "Try Plus free" | **In scope.** The summary asks to remove invalid trial promises. |
| F12, F13 | Unchanged: the mobile start-date cap, and no favourite cleaner at schedule creation. |
| F16 unused wizard signal | Delete it. |
| Old: web chargebacks miss the order; a dispute stores the requested refund; fiscal lines omit extras | Fix now. The summary asks the lawyer only about priority; these are engineering bugs. |
| Old: crew pay multiplied; no cash ledger | Now decisions 40 and 23. |

---

## Part 2 — Decisions only you can take

Each gives the situation, the options, what follows, and my recommendation in **bold**. Where my recommendation differs from the verified item's own, the entry says so.

### A. Conflicts with earlier rulings

1. **New-customer 60 minutes vs the 2026-09-24 ruling.** The meeting gives a new customer 60 min of free cancellation. On 2026-09-24 you put first-time customers at 15.
   - (a) **The meeting wins; record it as a ruling.**
   - (b) Keep 15.
   - **Recommendation: (a).**

2. **Who counts as "new".**
   - (a) **The first booking ever on that account, e-mail or phone.** Unpaid, abandoned checkouts do not count. One booking gets 60 min, so it cannot be farmed.
   - (b) Every booking until the first *completed* clean. A customer who keeps cancelling stays "new".
   - (c) No earlier non-cancelled order. Cancelling everything regains the 60 min.

   Newness is **computed from earlier orders at each call**, with no new column, and the booking evidence records the minutes shown.
   - **Recommendation: (a).**

3. **Recognising a returning guest ("only once").**
   - (a) E-mail only.
   - (b) **E-mail or phone, and guest and account bookings count against each other.**
   - (c) Card fingerprint. This means storing fingerprints, and they are known only after payment.
   - **Recommendation: (b).**

4. **Everyone else keeps 15 minutes.**
   - (a) **Confirm 15.**
   - (b) Another figure. The constant, the copy and the terms all change.
   - **Recommendation: (a).**

5. **Plus's 4-hour free-notice window.** A member cancels free until 4 h before the start instead of 24 h, so the 25 % tier disappears for them. It is built and advertised, but missing from the summary and the terms.
   - (a) **Keep it.** Add it to the terms, send it with PR-2, and cap the plan setting at 24 h.
   - (b) Drop it. Set the plans to 0 and remove the copy on three clients.
   - **Recommendation: (a).**

### B. Cancellation, lockout, no-show

6. **Which fee schedule we give the lawyer as ours.**
   - (a) **The app's: 24 h / 4 h at 25 % / 50 %, money back to the card.**
   - (b) The draft VOP's: 24 h / 2 h at 50 % / 100 %, with "or credit". The constants, the copy on three clients and the terms all change, and customers pay double.
   - **Recommendation: (a).**

7. **After the booked start, the assigned cleaner has not started.** Today the customer can self-cancel and pays 50 %.
   - (a) **Block self-cancel from the start time and offer "the cleaner did not arrive"**, which feeds the no-show path.
   - (b) Keep 50 %.
   - (c) Charge 100 %.
   - **Recommendation: (a).**

8. **Establishing a no-show by an assigned cleaner.**
   - (a) **An admin confirms**, after a customer report or an automatic "not started by start + 30 min" alert. One button then:
     - refunds 100 %;
     - grants the 250 credit;
     - cancels the order;
     - closes any linked dispute.
   - (b) Fully automatic at +30 min. This also refunds jobs where the cleaner came but forgot to tap Start.
   - **Recommendation: (a).**

9. **One of two cleaners does not come.**
   - (a) Refund that seat's share plus 250 credit.
   - (b) Treat it as a full no-show.
   - (c) **Admin discretion through the dispute.** Crews are rare and never pay cash.
   - **Recommendation: (c) for launch.**

10. **No-show credit for a guest.** Guests have no credit account.
    - (a) **Refund only.** The web home page already says so.
    - (b) An e-mailed single-use code. Guests cannot redeem codes today, so that must be built too (M).
    - (c) Credit held against the e-mail until they register (L).
    - **Recommendation: (a).**

11. **Declaring a lockout.**
    - (a) **The cleaner taps "cannot get in" after waiting 15 min past the start, with a door photo and a note of call attempts. An admin confirms before the 100 % fee applies.** The mobile FAQ already promises the 15-minute wait.
    - (b) Automatic when the customer does not answer.
    - **Recommendation: (a).**

12. **What the cleaner gets on a lockout or late cancellation.** Today nothing; the company keeps the fee.
    - (a) Nothing.
    - (b) **A fixed share (e.g. 50 %) of the fee, paid only once it is collected.**
    - (c) Full job pay, even when nothing is collected.
    - **Recommendation: (b), subject to PR-1 Q6.**

13. **Lockout on a guest booking.** Guests always prepay.
    - (a) **Keep the prepayment and never charge beyond it.**
    - (b) Refund it.
    - **Recommendation: (a).**

14. **Stuck refunds.** Today a refund that Stripe refuses or cannot reach stays Pending forever, and nobody retries it.
    - (a) **Automatic re-drive every hour, plus an admin alert after 24 h.**
    - (b) Alert only.
    - **Recommendation: (a).** The draft VOP already promises refunds within 5 working days.

15. **Rescheduling at launch.** No reschedule feature exists, yet the mobile FAQ promises a Reschedule button and free rescheduling until 12 h before, and the draft VOP promises it twice.
    - (a) **None at launch.** The customer cancels and rebooks (free while unassigned). Fix the FAQ and tell the lawyer.
    - (b) Self-service reschedule while no cleaner is assigned (M).
    - (c) Reschedule with the cleaner's consent (L).
    - **Recommendation: (a), then (b) after launch.**

### C. Cash and the card guarantee

16. **Must a cash booking have a saved card?** Card orders already secure their fee (it is withheld from the refund); cash orders have nothing.
    - (a) **Yes, captured once at the first cash booking, with consent that fees may be charged to it.**
    - (b) Offer cash only to customers who already have a card on file from a mobile card payment or Plus.
    - (c) Only after a first lockout or non-payment.
    - (d) No, and cash fees stay uncollectable.

    The charging machinery can be built now but **stays switched off until the Phase-4 terms carry the lawyer's consent wording.** (The item itself advised building nothing before that wording; this recommendation builds early and keeps it dark.)
    - **Recommendation: (a).**

17. **What may be charged to the saved card:**
    - the cash cancellation fee;
    - the 100 % lockout fee;
    - unpaid cash;
    - the dirtiness top-up.
    - **Recommendation: all four, subject to PR-2.**

18. **An off-session charge fails.**
    - (a) **E-mail a pay link and refuse new *cash* bookings until it is paid; card bookings stay allowed, since they are prepaid. An admin may write the fee off.**
    - (b) Refuse every new booking.
    - (c) Write it off.
    - (d) Send it to a collection agency above an amount.
    - **Recommendation: (a).**

19. **When the cash receipt is issued.**
    - (a) **At completion, after the cash is recorded.** Both dates exist, and it reuses the existing completion fallback.
    - (b) When the cash is recorded. No completion date exists yet.
    - (c) Allow cash to be recorded after completion.

    The receipt prints the slot, the completion time and the cash-received time, in market time. Cancelled cash orders get no receipt. An admin gets a "record cash received" action for the rare cases the cleaner cannot.
    - **Recommendation: (a).**

20. **The informational e-mail at booking.**
    - (a) **Cash bookings only.**
    - (b) Every booking. Card customers then get two e-mails seconds apart.
    - **Recommendation: (a).**

21. **When recurring cash may show "Paid".**
    - (a) **Only when the cleaner records the cash; the customer's confirm becomes its own marker.** About a week of work.
    - (b) Keep Paid at confirm, but block completion until cash is recorded. "Paid" still shows before any money moves.
    - (c) Drop cash for recurring schedules altogether, if it has no business case.
    - **Recommendation: (a), or (c) if recurring cash is not worth it.**

22. **Recording the amount of cash.** Today only who and when are recorded.
    - (a) **The server stamps the amount due; the cleaner confirms it on the existing screen, and a short payment is reported as an issue.** Small (S).
    - (b) The cleaner enters the amount received, and a lower figure opens an admin task. Adds an input on three partner clients (M).
    - (c) Keep who/when only. This breaks once top-ups exist.
    - **Recommendation: (a).**

23. **Setting cash off against the reward.** The cleaner holds the cash, and the reward is paid to them.
    - (a) Net it at each payout and carry a negative balance forward.
    - (b) Pay the full reward; the cleaner remits the cash within N days.
    - (c) (a), plus a remittance request when a negative balance lasts longer than N days.

    The two verified items disagree: one recommends (a), the other (c). The ledger is built before any Stripe payout work either way.
    - **Recommendation: (c).**

24. **Cash limits per customer.** None today; the one-cleaner rule already bounds the price.
    - **Recommendation: no amount cap; at most 2 open unpaid cash bookings per customer; rely on the saved card (16).**

25. **Cash limit per cleaner.**
    - (a) **Once the ledger exists, cap the cash a cleaner holds (e.g. one average month's reward), above which cash jobs are hidden from them, with the reason stated to them.**
    - (b) No cap.
    - **Recommendation: (a).**

26. **Web customers cannot confirm recurring occurrences.** There is no confirm button, so every occurrence of a web-only customer auto-cancels an hour before the slot.
    - (a) **Build a web confirm, after decision 21.** Building it before 21 would spread the early "Paid" to the web.
    - (b) Hide recurring schedules on the web and point customers to the app. Plus's recurring benefit then becomes app-only.
    - **Recommendation: (a).**

27. **Multi-cleaner card order whose Stripe webhook was lost.** Partner web no longer offers the cash action there, and that action was also the in-app Stripe repair.
    - (a) **Accept it; admins use the override.** It needs a webhook lost for days.
    - (b) Build a "check payment" action on three partner clients (M).
    - **Recommendation: (a).**

### D. Dirtiness levels, top-up, pricing and pay

28. **What +30 / +60 % applies to.**
    - (a) **The whole basket including extras, inside the subtotal: discounts come off it and express compounds on top** (heavy + express = ×1.92). This is the base express already uses, so refunds need no change.
    - (b) Services and packages only, or applied after discounts. Every price display then needs a second composition rule.
    - **Recommendation: (a).**

29. **Where the rates live.**
    - (a) **Constants, pinned by the parity checker, like express.**
    - (b) A per-market admin setting snapshotted on each order.
    - **Recommendation: (a)**, until a market needs other rates.

30. **Where the page-4 descriptions live.**
    - (a) **Client strings in five locales, with the level stored on the order.**
    - (b) A backend catalogue served to all clients and snapshotted on the order. A top-up dispute could then cite the exact text the customer saw.

    The verified items disagree.
    - **Recommendation: (a) now; move to (b) if PR-10 makes the top-up depend on the exact wording.**

31. **Must the customer choose actively?**
    - **Recommendation: yes on new clients (page 4 says to pick the higher level when unsure); the server defaults to Normal for old clients and API callers.**

32. **Does the level lengthen the booked time?** Today duration is fixed per service and sets the crew (120 min per cleaner).
    - (a) No. Crew and cash stay as they are, but page 4's "enough time" promise is false.
    - (b) **Yes, minutes × (1 + rate).** The time is honest, but a 120-min heavy job becomes two cleaners and loses cash.
    - **Recommendation: (b), together with decision 40 (split crew pay).**

33. **Does the level raise cleaner pay?**
    - (a) **Yes, by the same factor, applied after the min/max clamp.**
    - (b) No, the company keeps it. Cleaners will skip dirty jobs.
    - (c) A fixed bonus.
    - **Recommendation: (a).**

34. **Recurring schedules.**
    - (a) **The template carries a level, default Normal, editable.** By page 4's own definitions a monthly schedule is "increased".
    - (b) Always Normal, with mismatches going through the top-up.
    - **Recommendation: (a).**

35. **Overlap with the catalogue.** The pet-hair extra (150 CZK) overlaps "increased … with pets", and the Deep Cleaning services overlap "heavy".
    - (a) Keep everything. A pet owner pays twice.
    - (b) **Retire the pet-hair extra; deep services stay as scope, and the level applies on top.**
    - (c) Exempt deep services from the level.
    - **Recommendation: (b).**

36. **The on-site top-up (PR-10).**
    - (a) **Approve in principle; build only after the levels ship and the lawyer answers.** Terms of the recommendation:
      - the customer is present and approves and pays then (no automatic card charge; a signed-in customer with no card pays by link, like a guest);
      - a 15-minute answer window, while the cleaner starts the paid scope;
      - e-mail and push only, since no SMS provider exists;
      - pay rises by the rule in decision 33;
      - a separate document;
      - a top-up never re-crews the order, so a one-cleaner cash job stays one-cleaner.
    - (b) No top-up; the level is fixed at booking.
    - **Recommendation: (a).**

37. **Extra work on site.**
    - (a) **Not at launch. The cleaner does the booked scope only; extras need a new order.**
    - (b) Through the top-up.
    - **Recommendation: (a).**

38. **Who brings supplies.** The web says "our own machines and eco products included", which does not fit a self-employed subcontractor.
    - (a) **The cleaner always brings them, included in the price; reword the copy.**
    - (b) The customer provides them.
    - (c) A choice per order.
    - **Recommendation: (a).**

39. **Pricing basis.**
    - (a) **Keep rooms and bathrooms (plus dirtiness) for launch.**
    - (b) Area, effort or hourly (XL).
    - Should duration also scale with flat size? Today an 8-room and a 1-room flat with the same services get the same time and crew. **Yes, as per-room minutes on services, in the same batch as decision 32, since it touches the same code.** It needs the real service durations from decision 77.
    - **Recommendation: (a), with size scaling.**

40. **Crew pay.** Today every cleaner gets the full job pay, so a two-seat job pays twice against one price.
    - (a) Keep it.
    - (b) **Split the job pay equally across the seats.**
    - (c) Raise the customer price with the crew.
    - **Recommendation: (b).**

41. **The admin "rank template" button.** It already sets per-cleaner pay at junior 50 % / medior 75 % / **senior 100 %** of the customer price, which leaves zero margin, although you decided categories come "later".
    - (a) Remove it.
    - (b) **Rename it to neutral rate templates, with multipliers that leave a margin.**
    - (c) Keep it as is.
    - **Recommendation: (b), or (a) if nobody uses it.**

42. **Start times.** The server enforces nothing: recurring on iOS accepts 03:07, and the materialiser creates same-day occurrences that are past or express-charged.
    - **Recommendation:**
      - enforce 08:00–19:45 in 15-minute steps on the server for every booking path, in the market's time zone;
      - keep whole hours in the recurring UI;
      - one booking horizon (e.g. 60 days).

43. **What "monthly" means.** Today it is every 35 days, about 10 visits a year.
    - (a) The same date each month.
    - (b) **The nth weekday (e.g. the 2nd Thursday; "last" when there is no 5th).** 12 visits a year, and the weekday is kept.
    - (c) Every 4 weeks. 13 visits a year, and the label must change.
    - **Recommendation: (b).**

44. **Favourite cleaner when creating a schedule.** No client offers it; only an edit can keep one.
    - **Recommendation: build it on three clients (M), medium priority.**

### E. Sales model, contracts, payouts, tax

45. **Parties to the per-job contract.**
    - (a) **Operating company ↔ cleaner, stating that seat's reward. The customer no longer sees a contract with the cleaner.**
    - (b) Keep customer ↔ cleaner. This contradicts own-name sale, and likely brings DAC7 and payment-service questions.
    - **Recommendation: (a), confirmed by PR-3 Q1 before the text changes.**

46. **Commission.**
    - **Recommendation: none. Under own-name sale the company buys the cleaner's work at the reward and keeps the margin; the framework contract is rewritten as a subcontract.**

47. **How a cleaner accepts the framework contract, the self-billing agreement and the DPA.**
    - (a) **An in-app click-through, versioned, and enforced at approval and again at every take until the current version is accepted.**
    - (b) E-signature or paper.
    - **Recommendation: (a)**, unless the lawyer requires a signature. The machinery is built early, because of the 2 Dec deadline; the texts come from the lawyer.

48. **"Reward directly via Stripe".**
    - (a) **Keep the manual bank transfer at launch. Build Stripe Connect Express after PR-3 Q4 is answered:**
      - monthly payouts;
      - a transfer triggered when an admin approves the invoice;
      - fees borne by the company;
      - manual transfer kept as a fallback.
    - (b) Start Connect now (XL, with a risk of redesign).
    - **Recommendation: (a).**

49. **Whose Stripe account takes card payments.** Today one holding account, with intercompany settlement (ruling of 2026-09-15).
    - (a) **The operating company's own account**, so the payee matches the receipt issuer.
    - (b) Keep the holding account. This needs a collection arrangement the lawyer must approve.
    - **Recommendation: (a)**, while there is one company.

50. **VAT.**
    - (a) Register voluntarily before launch. About 17.4 % (21/121) of every price then goes to the state, with no input VAT from non-payer cleaners.
    - (b) **Register at the threshold.** Under own-name sale the full price counts toward it, so it comes soon.
    - Also for the tax adviser: which rate applies to household cleaning (possibly the reduced rate, about 10.7 %), and whether free credit keeps the VAT base at the full price.
    - **Recommendation: (b), confirmed by a tax adviser before pricing the launch.**

51. **Corrective tax documents on refund.**
    - **Recommendation: build them at VAT registration, not before.**

52. **Does a dispute refund reduce the cleaner's reward?**
    - (a) Never automatically (today there is only an unlinked manual deduction).
    - (b) Automatically, in proportion. This punishes goodwill refunds and reads as an algorithmic sanction.
    - (c) **Only when the dispute finds the cleaner at fault, linked to the dispute and visible to them.**
    - **Recommendation: (c).**

53. **Insurance promise.** The mobile apps say "Insured up to 1 000 000 Kč" and "background-checked"; neither is verified.
    - (a) The cleaner's own policy at 3 000 000 Kč, with the certificate required at approval.
    - (b) A company policy (the lawyer suggests at least 5 000 000 Kč).
    - (c) **No figure until you decide.**
    - **Recommendation: (c) now, then (a) or (b). Remove the figure and "background-checked" now, and require a valid certificate at approval either way.**

54. **Company identity.**
    - Is the incorporated name "Cleansia CZ s.r.o." (the registry) or "Cleansia s.r.o." (the receipts and drafts)?
    - Is +420 739 788 108 the public number?
    - Should the website footer be fixed text or read from the company record?
    - **Recommendation: one name everywhere; one public address (info@) plus privacy@; a fixed footer for a one-company launch** (it currently prints the literal "IČO [IČO] · DIČ [DIČ]").

### F. Platform-work directive (deadline 2 Dec 2026)

55. **Treat 2 December 2026 as a hard deadline for the platform-work changes**, ahead of feature work.
    - **Recommendation: yes.**

56. **Admin placement of a cleaner.** Today an admin can place a cleaner without their consent; the cleaner can drop at no cost and must accept the contract before starting.
    - (a) **An offer the cleaner may decline without consequence, written into the contract, with a reason given when an admin removes someone.**
    - (b) Binding.
    - **Recommendation: (a).**

57. **UI vocabulary.** The apps call cleaners "zaměstnanci", pay "mzda" and invoices "mzdové faktury", and tell admins to "sledujte výkon".
    - **Recommendation: replace them with "partner" and "odměna" in the UI, and "zhotovitel" in contracts.**

58. **A cleaner-facing "How jobs are offered" page.** It covers:
    - the board order;
    - the favourite-cleaner hold;
    - the push radius;
    - the automatic sweeps;
    - that no score affects access;
    - the approval criteria;
    - a human contact for a review.
    - **Recommendation: yes, before 2 Dec, with wording from the lawyer (PR-5 Q3).**

59. **Non-compete and penalties in the cleaner drafts.**
    - (a) Keep them and let the lawyer moderate the amounts.
    - (b) **Keep only non-circumvention for customers met through the platform; drop the non-compete.** Exclusivity is an employment indicator.
    - **Recommendation: (b).**

60. **Should cleaners see the rate card and accept changes to it?**
    - (a) **Yes, published and accepted like the framework contract, if the lawyer flags pay control under PR-5.**
    - (b) Keep rates internal.
    - **Recommendation: (a), conditional.**

### G. Consumer law, photos, privacy

61. **14-day withdrawal.** A booking can start 2 h after ordering, and the law needs an express consent to early performance.
    - (a) **A separate tick on every booking and recurring setup, recorded like other legal acts.**
    - (b) One clause in the terms.
    - (c) Rely on the draft VOP's § 1837 j) reading.
    - **Recommendation: (a).**

62. **Re-accepting new terms.** Every decision here needs a new terms version.
    - (a) **The tick reappears whenever the customer's accepted version is older. No new booking until they accept; existing bookings run on their own version.** This is needed for a card-guarantee charge to rest on provable consent.
    - (b) Let them keep booking under the old version.
    - **Recommendation: (a).**

63. **The 250 apology credit.**
    - (a) **Goodwill, on top of any legal claim.**
    - (b) A settlement the customer accepts in exchange for waiving claims. This needs an explicit acceptance step.
    - **Recommendation: (a).**

64. **Photo retention.** Photos are kept 7 days today, but banks raise chargebacks up to about 180 days later (the platform's own setting).
    - (a) 7 days.
    - (b) 180 days, to cover chargebacks.
    - (c) 30 days.
    - **Recommendation: wait for PR-6 Q2 and propose (b) to the lawyer; cap the company setting at the lawyer's figure.** Web chargebacks get fixed regardless.

65. **Admin force-complete without an after photo.** It is the only way to close an order stuck in progress.
    - (a) Refuse it. Such orders can then never close.
    - (b) **Allow it only with a written reason on the audit row.**
    - **Recommendation: (b).**

66. **Camera only.**
    - Job photos camera-only on Android and iOS (no gallery or photo library).
    - Partner web upload (a browser can never guarantee that no copy stays on the device):
      - (a) disabled, if every active cleaner has the mobile app;
      - (b) kept, with a capture hint.
    - **Recommendation: camera-only on mobile; on the web, (a) if every cleaner has the app, otherwise (b).**

67. **Cleaner access to the customer's address, phone and door instructions.** Today it never ends.
    - (a) End at completion.
    - (b) **24 h after completion, and immediately on cancellation.**
    - (c) 7 days.
    - (d) Never.
    - **Recommendation: (b).**

68. **Cookies.** Only necessary cookies exist, yet the banner offers analytics and marketing, and Google Fonts send visitors' IP addresses to Google. Is any tracker planned before launch?
    - (a) **No. The banner becomes a necessary-only notice, and fonts are self-hosted.**
    - (b) Yes, and it is chosen now so the lawyer reviews the real one.
    - **Recommendation: (a).**

69. **Marketing.**
    - (a) **Opt-in promo push is the only channel. Drop the MarketingEmails toggle, and show the terms and privacy consents read-only (version and date) instead of as toggles.**
    - (b) Plan marketing e-mail, which needs a sender and an unsubscribe.
    - **Recommendation: (a).**

70. **Minimum customer age.**
    - (a) **18, in the terms only, with no "we verify age" claim.**
    - (b) 15.
    - (c) 16 (the drafts' figure, which is not Czech law).
    - **Recommendation: (a).**

71. **Receipts keep name, e-mail, phone and address forever.**
    - (a) Keep them for the statutory tax period, then delete them.
    - (b) **(a), and also stop printing e-mail and phone.**
    - (c) Status quo.
    - **Recommendation: (b).**

### H. Plus, loyalty, credit, launch data

72. **A Plus renewal payment fails.** Today the app says "no membership", offers a second, double-billed subscription, and gives no way to cancel.
    - (a) **Show "payment failed", allow a cancel that takes effect immediately, and refuse a new subscription while the old one is alive.**
    - (b) The same, but the cancel takes effect at the period end.
    - **Recommendation: (a).**

73. **Loyalty tier going down.** Today the tier follows points both ways, so a refund clawback can drop it and its discount.
    - (a) **Keep it, and fix the stale tiers after a threshold edit.**
    - (b) Never drop.
    - **Recommendation: (a).**

74. **Settling a justified complaint with credit.** Credit expires and is never paid out.
    - (a) Admins settle with credit freely.
    - (b) **A card refund, unless the customer explicitly chooses credit.**
    - **Recommendation: (b).**

75. **Welcome offer at launch.** The web e-mails a 10 % placeholder code. The mobile apps advertise WELCOME15, which exists only in DEV. Neither is limited to a first order.
    - (a) **None at launch; remove both.**
    - (b) One approved offer on every channel, with a real first-order rule (M).
    - **Recommendation: (a)**, unless you want one.

76. **Currencies at launch.**
    - (a) **CZK only.**
    - (b) CZK plus EUR. This means a Slovak company, catalogue and texts (XL).
    - **Recommendation: (a).**

77. **The launch values sheet.** You supply these once:
    - Plus prices (monthly and yearly) with the **live** Stripe Price ids;
    - the Plus discount, free window and express quota;
    - tier thresholds and discounts;
    - the loyalty divisor;
    - the apology credit;
    - the insurance figure;
    - cleaner pay rates;
    - catalogue prices and the real service durations.

---

## Part 3 — Engineering defaults (applied unless you veto)

**Security and launch readiness** (launch phase unless marked)

- **E-1** The admin API goes behind the same Microsoft (Entra) sign-in as the admin website; today it accepts an e-mail and password from anywhere. Admin passwords need 12+ characters, with a forced change at first sign-in.
- **E-2** Customer and partner web sign-in tokens last 15–30 min instead of 24 h, with a silent refresh. *(Phase 0.)*
- **E-3** The production database and Key Vault become private (private endpoint), with migrations run through a scripted temporary window.
- **E-4** Storage moves to managed identity with shared keys disabled. The apps get a least-privilege database login; the admin login is kept for migrations only. The TLS certificate is verified.
- **E-5** Basic publishing credentials are disabled, and the customer site sends security headers. *(Phase 0.)*
- **E-6** gitleaks runs in CI. `local.settings.json` is untracked. The DEV admin whose password is in the README is replaced by invited admins. *(Phase 0.)*
- **E-7** Mobile release builds point at the production custom domains, and a release build fails on the placeholder Firebase config. *(Needs the domains, Part 6.)*
- **E-8** A production deploy is refused while any in-force legal text still carries the "Návrh" banner. *(Phase 0.)*
- **E-9** A reviewed bootstrap script creates the production skeleton: tenant, languages, countries, currency, market and templates. Tiers and figures come from decision 77, and prices, plans and company data are typed into the admin console.
- **E-10** CompanyInfo (name, IČO, bank) becomes editable by Administrator only, and the runbook gets a console-access checklist. *(Phase 0.)*

**Honest copy** (Phase 0 unless marked)

- **E-11** Copy fixes:
  - on mobile: the unconditional 14-day trial promise, "cancel anytime", and the referral wording ("after their first completed cleaning");
  - on web: "100 % Satisfaction" and the misleading "Track live" button;
  - the account-deletion screens on customer and partner apps say what is kept; the partner copy promises a self-billing agreement that does not exist yet.
  - Copy that depends on a decision ships with that decision in Phase 1: reschedule (15), supplies (38), insurance and "background-checked" (53), welcome offer (75).
- **E-12** The admin-editable figures (Plus discount, window and quota, tier discounts) are read dynamically on mobile. *(Phase 1.)*

**Records and policy housekeeping**

- **E-13** Mobile keeps "Find a guest booking", since it creates nothing. The rule is recorded as "guest *booking* only on the web".
  > **Reversed by the owner 2026-10-01.** The apps keep no guest surface at all: "Find a guest booking" and its lookup,
  > preview and cancel are gone from Android and iOS, so guest booking *and* lookup are web-only. The six guest routes
  > stay on the customer mobile host for now, because an installed build still calls three of them, and a follow-up
  > removes them. → `docs/flows/booking-and-pricing.md#guest-order-lookup`
- **E-14** F7, F9 and F11 are closed as unreachable. The 2026-09-15 cross-market rule and the 12-month credit expiry stay.

**Photos** (Phase 1)

- **E-15** Photo rules:
  - one before-photo window everywhere (Confirmed → InProgress), enforced on the server;
  - a cleaner may delete photos only before completion;
  - admin, customer and cleaner viewers keep today's behaviour ("no device copies" binds the cleaner);
  - shorter photo links;
  - no screenshot blocking;
  - one guidance line on the photo sheet (avoid people and documents) once PR-6 Q1 answers.

**Retention** (Phase 0)

- **E-16** Photos of never-completed orders are deleted 7 days after cancellation. Cancelled orders' personal data is anonymised on the same 2-year clock. Logged-out devices are deleted at 90 days.

**Platform-work housekeeping** (Phase 0)

- **E-17** The partner "Delay" row and the "justify payment adjustments" wording are removed. Work minutes are kept for estimates only.
- **E-18** Location and workload settings:
  - the per-cleaner weekly cap stays, with a reason shown to the cleaner;
  - the device-only distance and the home-radius push stay;
  - Android drops ACCESS_FINE_LOCATION;
  - the iOS purpose string covers the feed distance.

**Regulatory defaults**

- **E-19** When the Czech fiscal law arrives, the platform registers only what it requires.
- **E-20** The audit-retention floor stays at 1 year, configurable, with a default of 3, until PR-7 Q1 names a minimum.
- **E-21** DAC7 goes to the tax adviser with PR-3. Under own-name sale with a subcontract it likely does not apply.

---

## Part 4 — Bugs and gaps to fix regardless (no decision needed)

| # | Defect | Size | Phase |
|---|---|---|---|
| 1 | **The mobile API lets an anonymous caller create an order.** Two create routes on the customer mobile host are `[AllowAnonymous]`. | S | 0 |
| 2 | **Recurring times are generated as UTC.** A Prague 10:00 schedule runs at 11:00 in winter and 12:00 in summer; 19:00 becomes 21:00. | M | 0 |
| 3 | Order status e-mails print the cleaning time in UTC (1–2 h early); the receipt e-mail's date uses the server's culture. | S | 0 |
| 4 | F1–F3 credit-leg money bugs:<br>• a cancellation while Stripe fails loses both refund and credit;<br>• a stuck sweep refund still gets a "refunded in full" push, which is also false on every unfilled **cash** order;<br>• a credit balance can be overwritten;<br>• orders missed during an outage longer than 6 h are never swept.<br>Ships with the refund watchdog (decision 14). | M | 1 |
| 5 | Web card chargebacks never find their order: no dispute, no alert, and the photos are deleted at 7 days. | S | 0 |
| 6 | A dispute records the requested refund, not the money that moved. | S | 0 |
| 7 | Fiscal lines omit extras, express and discounts, so they do not sum to the total. | S | 0 |
| 8 | Account erasure fails to delete photo files (blob path); the DEV photo delete is also wrong on Azurite. | S | 0 |
| 9 | Erasure misses inactive saved addresses and saved-address-only originals; floor, apartment, access mode and cancellation reason survive anonymisation. | S | 0 |
| 10 | The replaced-document retention sweep re-reads a failing row forever. | S | 0 |
| 11 | Platform cancellations (no cleaner, admin, unpaid cleanup) tell a guest nothing and leave the old links live. Lands after bug 3, since both use the same e-mail. | M | 0 |
| 12 | Plus past-due (the decision is 72; these are bugs regardless):<br>• a second subscription is possible;<br>• erasure cancels only an Active subscription, so a past-due card keeps being retried;<br>• a late payment collides with the unique index. | L | 1 |
| 13 | Admin placement does not check that the cleaner is approved, serves the market and is free. | S | 0 |
| 14 | The admin legal catalogue shows every past version "In force" (F6). | S | 0 |
| 15 | Bookings record the newest terms version, not the accepted one (F4); ships with decision 62. | M | 1 |
| 16 | The Plus plan validator accepts a free-notice window above 24 h. | S | 0 |
| 17 | Room and bathroom pickers offer counts the server refuses: web recurring 0–6, mobile unbounded. | S | 0 |
| 18 | The mobile schedule edit lets the start pass the end date (F12). | S | 0 |
| 19 | An assigned cleaner can delete the only after photo after completion, and the server has no upload windows. | S | 1 (E-15) |
| 20 | A cash cancellation stores a "refund amount" for money never taken, the admin view shows no fee owed, and the customer sees "Maximum card refund". | S | 0 |
| 21 | The web wizard has an unused signal (F16), and `docs/domain/offerability.md` still states the pre-ADR-0057 rule. | S | 0 |

The cleaner-registration placeholder that records the **customer** terms for a cleaner is deliberate (pending ADR-0041). It is replaced by decision 47, not fixed separately.

---

## Part 5 — What goes to the lawyer (one package, corrected)

Send PR-1 … PR-10 with these corrections and additions.

- **PR-1**
  - Correct the premise: registration does **not** require a card. State decision 16 (the card is captured at the first cash booking).
  - Add the 14-day withdrawal consent (decision 61) and the legal nature of the fee.
- **PR-2**
  - Our schedule is the app's (24 h / 4 h, 25 / 50 %, money back to the card).
  - Add the Plus 4 h window (decision 5), the new-customer and guest 60 min, the 100 % lockout, and what the card may be charged for (decision 17).
- **PR-3**
  - The contract parties we intend (45), no commission (46), manual transfers at launch (48), and whose Stripe account collects (49).
  - The VAT questions (50), and DAC7.
- **PR-4**
  - The re-acceptance rule (62) and a complaints procedure (reklamační řád) as its own document.
  - Rewrite the VOP chapters on:
    - cash: the cleaner collects **for the company, after the clean**, and the receipt comes after the handover; no invoice-on-request clause;
    - tips: not in the app;
    - credit: never paid out, forfeited on deletion;
    - rescheduling: none;
    - guest booking without registration.
  - Name the seller (the VOP names no company today), and list only the services actually sold.
  - Decide whether the Plus, credit and loyalty rules sit inside the VOP or in a separate document.
- **PR-5**
  - The verified inventory:
    - admin placement;
    - the manual rank template that already sets pay;
    - ratings;
    - the weekly cap;
    - the work-minutes record;
    - the audit of access-instruction reads;
    - no location tracking.
  - The "How jobs are offered" page (58).
  - The 2 Dec 2026 deadline.
  - Whether a guest needs a complaint route.
- **PR-6 / PR-7**
  - The verified retention and processing inventory: photos, receipts, cookies, processors.
  - The legal basis for home photos and what to do when people or documents appear in them.
  - The 180-day chargeback horizon against 7-day photos.
  - Whether a damage report must carry a photo.
  - Age 18.
- **PR-8**
  - The draft VOP already says 5 working days.
  - Who answers to the customer for damage and the recourse against the cleaner. Today the work contract makes the cleaner liable and says Cleansia is not a party.
  - Mandatory notifications and insurer notice.
  - Our insurance position (53).
  - The apology credit as goodwill (63).
- **PR-9**
  - The company name (54), the trade licences for own-name sale, and the launch values (77).
- **PR-10**
  - Approved in principle (36): the customer is present and pays on approval, a 15-minute window, e-mail only, and no re-crewing.

---

## Part 6 — Only you can do these

1. **Leaked keys.** Revoke the two SendGrid API keys that sat in git history (Oct 2025 – Mar 2026), if either still exists. Confirm the DEV `JWT_KEY` is not the published test key, and generate a fresh production `JWT_KEY`.
2. **Console security.** Turn on MFA for every console account: Azure, GitHub, Stripe, SendGrid, Firebase, Apple, Google, Mapbox, Entra. Protect the `prod-weu` environment with required reviewers. Check that Stripe's own failed-payment e-mails are enabled.
3. **Public client identifiers.** Restrict them in their consoles: Google OAuth origins, Firebase keys by package and bundle, Mapbox tokens. Create production OAuth clients and Firebase projects.
4. **Company and launch data.** Register the operating company and obtain the trade licences for own-name sale; supply the launch values sheet (77); create the live Stripe Prices; register the production domains (DNS).
5. **After the production bootstrap:** create the first production administrator.
6. **Outside advice.** Send the lawyer package (Part 5), and take the VAT questions to a tax adviser.

---

## Part 7 — Plan

Sizes: S under a day, M 1–3 days, L about a week, XL more.

How phases run:
- Each phase is one branch and one PR, with a commit per item and CI green before the next phase.
- Items marked "chain" run in order. Everything else runs in parallel lanes.

**Phase 0 — now, needs no answer (about 2 weeks)**
- Bugs 1–3, 5–11, 13, 14, 16–18, 20, 21. Bug 3 lands before bug 11, which reuses the same e-mail.
- E-2, E-5, E-6, E-8, E-10, E-11 (the decision-free part), E-16, E-17, E-18.
- Docs:
  - correct business-rules and the ADRs where the summary's stale points came from;
  - record the 27 September ruling as an amendment to ADR-0068;
  - fix `offerability.md`.

**Phase 1 — decided policy, after your answers (about 4 weeks)**

- **Cancellation:**
  - the new-customer and guest 60 min (1–4);
  - the Plus window (5);
  - the post-start time gate and "cleaner did not arrive" (7);
  - the admin no-show flow for an assigned cleaner, with its alert (8);
  - the refund watchdog (14) together with bug 4.
- **Cash** (chain):
  - the recurring confirm marker (21);
  - the amount stamped at handover (22);
  - the receipt at completion and the informational e-mail (19, 20), including the admin "record cash received" action, no receipt for cancelled cash orders, and the fiscal-reconciliation cash path removed;
  - then the web recurring confirm (26).
- **Scheduling:**
  - the server start-time window and booking horizon (42), after bug 2;
  - monthly = nth weekday (43);
  - the favourite cleaner at schedule creation (44).
- **Plus:**
  - past-due handling (72) with bug 12;
  - the welcome offer (75);
  - loyalty re-tiering (73);
  - dynamic mobile figures (E-12).
- **Platform work, before 2 Dec 2026:**
  - vocabulary (57);
  - placement as an offer, with a removal reason (56);
  - the rank template (41);
  - the "How jobs are offered" page (58);
  - the machinery for cleaner document types and acceptance (47), with the texts slotting in when the lawyer delivers;
  - rate-card acceptance, if decided (60).
- **Terms groundwork:** bug 15 and re-acceptance (62), so the lawyer's text can ship the day it arrives.
- **Photos and privacy:**
  - E-15 with bug 19;
  - admin force-complete with a reason (65);
  - camera-only (66);
  - access cut-off (67);
  - the cookie notice and self-hosted fonts (68);
  - consent toggles (69);
  - receipt contact data (71).
- **Complaints:** credit or card settlement (74), and a cleaner deduction only on fault (52).
- **Copy tied to decisions:** 15, 38, 53, 75.

**Phase 2 — dirtiness levels (about 3–4 weeks, XL; needs the real service durations from 77)**
- The levels in price, order, receipt, fiscal lines, audit, the three booking flows and recurring templates (28–35).
- Duration, size scaling and crew (32, 39); cleaner pay (33); the crew-pay split (40).
- The parity checker and docs.
- Needs a migration regen and the DEV drop.

**Phase 3 — money infrastructure (about 4–6 weeks)**

The charges stay **switched off** until the Phase-4 terms carry the consent wording.
- **Chain:**
  - saved card at the first cash booking, with consent (16);
  - receivables and off-session charges with the pay link and the cash-booking block (17, 18);
  - the lockout flow end to end (11–13), and the cleaner's share of a collected fee (12).
- **Parallel:**
  - the cash ledger and set-off (23);
  - the per-customer and per-cleaner cash limits (24, 25).

**Phase 4 — legal texts, when the lawyer delivers (the company must be registered before this)**
- New terms covering:
  - cancellation, lockout and no-show;
  - the card guarantee;
  - credit, Plus and tips;
  - age;
  - the withdrawal consent (61);
  - the seller's identity.
- The complaints procedure.
- Contract parties operating company ↔ cleaner, with the per-seat reward (45); durable confirmations by e-mail with a PDF.
- The framework contract, self-billing agreement and DPA texts for cleaners (47).
- The privacy policy, insurance (53) and company identity (54).
- Then the Phase-3 charges switch on, and the E-8 launch gate clears.

**Launch readiness (parallel with Phases 1–4)**
- E-1, E-3, E-4, E-7, E-9; Part 6 items 1–5.
- The production Stripe keys follow decision 49.
- The photo-retention value (64) is set once PR-6 Q2 answers.

**Phase 5 — after launch, or on a trigger**
- Stripe Connect payouts (48), after PR-3 Q4.
- The on-site top-up (36), after Phase 2 and PR-10.
- Rescheduling (15 b).
- Corrective tax documents (51), at VAT registration.
- Czech fiscal registration, when the law is final.
- An EUR market.

**Critical path to launch**
1. Your answers.
2. Company registration and licences, alongside the lawyer's work.
3. Phases 0–1.
4. The lawyer's texts (Phase 4).
5. The launch values and the production bootstrap.

Dirtiness (Phase 2) and the card guarantee (Phase 3) are decided features, so they are on the path unless you choose to launch without them.

**The platform-work items have their own date, 2 Dec 2026.** If the lawyer's cleaner texts arrive late, the acceptance machinery will be ready but the texts will not.
