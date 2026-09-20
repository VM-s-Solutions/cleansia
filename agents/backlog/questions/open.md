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
> for now, card revenue settled intercompany*). Batch 1 is T-0754 … T-0759; the lifecycle is built in
> Batch 2 below. **Q-TENANCY-01/05’s cross-market backend is implemented in T-0765 (Batch 3,
> 2026-09-16)**: bookings follow the address’s operator, account loyalty and credit keep their company,
> and owner-pinned history spans operators. Web/Android labels and the admin customer panel are
> implemented and locally verified; iOS label work remains outstanding. The ticket
> remains `in_progress`; ADR-0061 D5.1/D6 and ADR-0062 D7 carry the dated implementation amendments.
> Q-PUSH-01's record is `docs/decisions/adr-0054.md`
> §Rulings (*the digest is not silenceable*; T-0756). Q-VS-03 — the accounting premise ADR-0046 §D3.2
> was contingent on — was answered by the same ruling as Q-TENANCY-02 and is recorded in ADR-0046's
> superseding note.

> **Q-TENANCY-03 (*"I'd build up to (c). Archive is also a good functionality to introduce in the
> beginning"*, 2026-09-15) is built.** The record is `docs/decisions/adr-0064.md` (`accepted` 2026-09-16 —
> deactivate, wind down, archive, with the panel's six defaults O-1..O-6 named for the owner to overrule);
> the work is T-0760 … T-0764, shipped the same day. Three of its defaults are queued below as
> questions (Q-LC-01, Q-LC-02, Q-LC-03); O-2 (every Plus ended, any currency), O-5 (credit written off at
> closing) and O-6 (no storage immutability policy) stand with their defaults applied and are recorded in
> the ADR's §OPEN items only — none blocks anything.

> **Q-WC-01 … 07 were raised by ADR-0068 (the contract for work, `accepted` and shipped 2026-09-20 as
> T-0777 … T-0784).** Every one shipped as its default; an overruling is now a change to shipped
> behaviour and lands as a dated amendment block on the ADR, never an edit of its decision text. Five
> are the lawyer's to answer with the owner (01, 03, 04, 05, 07); two are the owner's alone (02, 06).
> None blocks anything.

## Q-WC-01 — Which figure is the *cena díla* on the contract record — the customer's price or the cleaner's pay?

**Raised by:** ADR-0068 D2 (2026-09-20). **Who answers:** the owner, with the lawyer (and the
accountant — the lawyers' gap table calls the intermediary invoicing model a direct conflict with the
code, owner plate A8).
**Why it needs you:** the acceptance row freezes the job as shown to the cleaner, and the price on it
is **`Order.TotalPrice` in the order's currency** — the customer's price of the work. The lawyer's
model forms the contract between the customer and the cleaner, so the customer's price is the price
of *that* contract; the cleaner's pay is the platform ↔ cleaner relation and is not on the row.
**Default applied: the customer's price and currency; the pay estimate is not stored.** Overruling
adds a member to `WorkContractFacts` (no schema) and a line to the sheet.
**Blocks:** nothing.

## Q-WC-02 — The web gesture: a tick and a button on the partner web, or a dragged slider like the mobile swipe?

**Raised by:** ADR-0068 D6 (2026-09-20). **Who answers:** the owner.
**Why it needs you:** the owner's note says *"uklízečka potvrdí přejetím prstu"*; a finger is a
phone. On the partner web the dialog shows the facts and the text, a checkbox *I have read and accept
the contract for work for this job* and *Accept and take the job* enabled by the tick — the
platform's existing legal-act control on the web (the register form's tick). A mouse-dragged slider
is a worse control than the tick and proves nothing more.
**Default applied: the tick on the web, the swipe on Android and iOS.** Overruling is one ticket
(a slider component on the web dialog).
**Blocks:** nothing.

## Q-WC-03 — How are the parties named on the rendered contract?

**Raised by:** ADR-0068 D4 (2026-09-20). **Who answers:** the owner, with the lawyer.
**Why it needs you:** the customer sees the cleaner's **given name only** (as the crew list does) and
the cleaner sees the customer's name through the order at render time; a consumer contract with a
trader normally identifies the trader (`Employee.RegistrationNumber`, `LegalEntityName`) — and since
the same day's company-registration ruling a cleaner contracts as a natural person, so the identity
to print is a person's, not a company's.
**Default applied: given name only.** Adding the legal identity is a DTO member on the read, no
schema. The likeliest ruling to arrive.
**Blocks:** nothing.

## Q-WC-04 — Does an in-app swipe form a B2C contract for work under Czech law, or is a qualified signature (Signi) needed?

**Raised by:** ADR-0068 D7 (2026-09-20). **Who answers:** the lawyer.
**Why it needs you:** the row records what a qualified-signature envelope would reference — the exact
text row (hence its hash), the instant, the actor, the client, the IP and device, the session's signed
device id — and the owner's note names Signi as *možná*. A provider adds `SignatureEnvelopeId` +
`SignedOn` and a webhook and changes nothing else.
**Default applied: the swipe (the tick on the web), recorded as ADR-0068 D2 records it; Signi is the
upgrade path.**
**Blocks:** nothing.

## Q-WC-05 — The VOP wording: when the contract forms, and incorporating the template by reference

**Raised by:** ADR-0068 (the lawyer's model, cited; 2026-09-20). **Who answers:** the lawyer.
**Why it needs you:** (a) VOP 3.2 says the contract forms with the confirmation e-mail; the framework
agreement 2.3 and VOP 1.3 say at the cleaner's acceptance — **the code follows the latter** (the
cleaner's acceptance writes the record; the order is stamped with the template at booking). (b) The
VOP should incorporate the work-contract template by reference — *"the contract for work between you
and the cleaner is on the terms published at /work-contract, in the version in force when you
booked"* — so the customer's VOP consent covers it; today the wizard's confirm step carries the P074
information sentence and the `/work-contract` link.
**Default applied: the code's shape; the sentences are the lawyer's to write** (a new dated seed folder
plus a deploy, ADR-0063 D3/D7).
**Blocks:** nothing.

## Q-WC-06 — May an administrator force a crew member, or should an admin placement be an offer the cleaner takes?

**Raised by:** ADR-0068 D3 / challenge C3(b) (2026-09-20). **Who answers:** the owner.
**Why it needs you:** `AdminReassignOrder` puts a cleaner on a job without their act and writes no
acceptance (an admin cannot accept on the cleaner's behalf), so the platform carries a standalone
`AcceptWorkContract`, a gate on Start **and** Complete (`contract.acceptance_required`), the sheet's
*accept* mode and a banner on three clients — and a stated residual: a second crew member who neither
starts nor completes works under no acceptance. The structural alternative is ADR-0036's preferred
hold: an admin placement becomes an **offer** the cleaner takes, so every seat is formed by the
cleaner's own act and the standalone act, both gates and one client key are deleted — at the cost of
the admin's power to force a crew member, a seat left offered until the cleaner acts, and the hold's
lapse and `NotHeldFrom`'s crew term rethought for a partly crewed order.
**Default applied: (a) — the admin keeps the force; the gates and the standalone act close what they
can; the residual is stated.**
**Blocks:** nothing.

## Q-WC-07 — The coarse location on a permanent row

**Raised by:** ADR-0068 D2 / challenge C12 (2026-09-20). **Who answers:** the lawyer / DPO.
**Why it needs you:** `locationApproximate` (*"Praha · 120"*) is frozen on the acceptance row for
ever as a term of the contract (the *místo plnění*) — it is the pre-acceptance disclosure the board
already makes, produced by the same builder so the screen and the row cannot disagree — while the
platform's own erasure treats city and zip as personal data at the source (`Address.Anonymize()`
blanks both). Kept under the row's ground (Art. 6(1)(b), then 17(3)(e) after erasure).
**Default applied: keep it.** Overruling blanks one member of the facts at erasure — one line in
`Pseudonymise()`'s neighbourhood.
**Blocks:** nothing.

## Q-LC-01 — Should a deactivated company's own administrators be refused, like its cleaners?

**Raised by:** ADR-0064 O-1 (the panel's default, 2026-09-16).
**Who answers:** the owner.
**Why it needs you:** deactivation refuses the company's **cleaners** on the partner apps
(`auth.company_deactivated`) and admits its **administrators** everywhere — on the admin host and on the
partner audiences they also hold. The default was chosen because no holding role exists yet (T-0748):
the company's administrators are the only hands that can settle it — mark the last invoices paid, answer
the last dispute, run the wind-down again, request the archive, look things up for a tax office. Refusing
them would leave a deactivated company with nobody able to act on it from inside the product. The
residual: **any** administrator of the company can reactivate it, and the holding cannot prevent that.
**Default applied: not refused.** The gate's profile set is one constant
(`CompanySignInGate.RefusedProfiles = { Employee }`); moving administrators into it is a one-line change
once T-0748 gives the holding a role that can act on any company.
**Answer needed:** once T-0748 lands, should a deactivated company's administrators be refused too (and
the company settled by the holding's role), or stay admitted?
**Blocks:** nothing. T-0748 is the ticket either way.

## Q-LC-02 — Should the archive bundle hold the personal-data estate too?

**Raised by:** ADR-0064 O-3 (the panel departed from the orchestrator's default, 2026-09-16).
**Who answers:** the owner.
**Why it needs you:** the orchestrator's default was *receipt PDFs + payout-invoice PDFs + the books as
JSON, plus consents and audit rows*. The panel narrowed the bundle to **the books only** on the retention
argument: a sealed copy of a consent row outlives the three-year window the sweep enforces on the live
row, a copy of a customer audit row outlives its three years per row, and a copy of `Users` outlives
every erasure — so the bundle would hold personal data the platform has promised to blank. What the
bundle holds: orders as the two-year sweep leaves them (no name, contact, street, instruction or note),
status history, pay rows, receipts, refunds, disputes without their text or the customer, pay periods,
invoices, the cleaners as the invoice prints them, credit accounts and their ledger, promo codes and
redemptions, the company record, the counters, the settings, the admin and cleaner audit trails, every
receipt and payout-invoice PDF. **Not in it:** `Users`, `UserConsents`, `CustomerActionAudits`,
`EmployeePayoutDetails` (a bank account), `UserMemberships`, notifications, devices, notes, photos,
reviews, dispute messages and evidence. **Default applied: books only.**
**Answer needed:** keep the books-only bundle, or add the estate — in which case the bundle needs its own
retention rule (a sweep over the container, or a lock with an expiry), because the live rows' windows no
longer reach it.
**Blocks:** nothing.

## Q-LC-03 — Should `credit-accounts.jsonl` and `promo-code-redemptions.jsonl` carry the customer's `UserId`?

**Raised by:** the T-0763 review (2026-09-16); an **architect ruling is pending**, the owner may weigh in.
**Who answers:** the architect (the bundle-row rule is ADR-0064 D3's); the owner if the rule itself moves.
**Why it needs a ruling:** the rule for a bundle row is *what the platform's own retention regime would
leave on the live row after every window has run, plus the ids the books need to cross-reference each
other*. `orders.jsonl` drops `UserId` because `Order.AnonymizeCustomerData()` blanks it after two years;
the review dropped it from `disputes.jsonl` on the same argument. Two rows still carry it:
`credit-accounts.jsonl` (`CreditAccount.UserId` — the live row keeps it; erasure anonymises the `User`,
not the account's pointer) and `promo-code-redemptions.jsonl` (`PromoCodeRedemption.UserId` — the live
row keeps it; `/flows/gdpr-and-audit` — loyalty, referral and promo rows are never touched by erasure
because the user row survives anonymised). So both are consistent with the rule as written — the live row keeps the id for
ever — but an id that ties a ledger and a redemption to a person, in a ten-year copy outside the
database, is the kind of member the record guard exists to catch, and the guard's name list does not
contain `UserId`. The two honest options: (a) keep them — a books cross-reference, consistent with the
live rows and with `CreditTransaction.CreatedBy`, `OrderEmployeePay.EmployeeId` and every other actor
id the bundle already holds; or (b) drop them — the ledger and the redemptions become unattributable in
the bundle, which is what the anonymised order rows already are, and the database keeps the link.
**Default applied: (a), as shipped.**
**Answer needed:** (a) or (b); and if (b), whether `UserId` joins the record guard's forbidden list (which
would also force `CreditAccount` and `PromoCodeRedemption` records to rename or drop it).
**Blocks:** nothing.

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

## Q-VS-01 — Does the accountant accept the ten-digit variable symbol, now numbered per company?

**Raised by:** ADR-0046 §D1 (2026-08-31), cited as filed; lost in the backlog reset and re-filed on
2026-09-16 by the full-branch sweep. **Who answers:** the owner, with the účetní.
**Why it needs you:** every payout invoice carries a *variabilní symbol* of `YYYY` + a six-digit
ordinal (`PayoutReferenceAllocator`), since T-0757 allocated per operating company (`(TenantId, Year,
Scope)` counter). Czech banks accept up to ten digits; whether the accountant wants a company digit
inside it, or a different series per company, is an accounting fact about the s.r.o.s. **Default
applied: ten digits, per-company sequence, no company digit** (the column is `varchar(10)` and the
digits are spent). Blocks nothing until the first real payout.

## Q-ART-01(b) — Should the dispute-evidence upload accept fewer file types?

**Raised by:** ADR-0043 (dispute evidence) and `docs/architecture/security-rules.md` cite it as an
owner question; lost in the backlog reset and re-filed on 2026-09-16. **Who answers:** the owner.
**Why it needs you:** uploads are sniffed against `SniffedContentType.Signatures` (images, PDF, …) and
size-capped; narrowing to images only is a product decision (fewer attack surfaces, but a customer
cannot attach an invoice PDF as evidence). **Default applied: the accept set unchanged.** Blocks nothing.
