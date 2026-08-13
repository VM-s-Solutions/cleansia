---
id: T-0491
title: STORY — Cleansia Plus advertises five perks; one is honoured. Define what each one promises.
status: draft
size: M
owner: analyst
created: 2026-08-02
updated: 2026-08-02
depends_on: []
blocks: [T-0492, T-0493, T-0495, T-0498]
stories: []
adrs: [0009]
layers: [analyst]
security_touching: false
manual_steps: []
sprint: 15
---

## Context

**Source: the Cleansia Plus audit (2026-08-02).** Of five advertised perks: **one is honoured, one is
worthless to the customers most likely to buy it, three are unenforced.** This is a **paid
subscription**, so an unenforced promise is not a backlog nit — it is a consumer-protection exposure
and it is live on DEV today.

**This story panel comes first because every one of the nine questions below is a product decision,
and four implementation tickets are blocked on its output.** Filing the fixes without it means four
developers each inventing what Plus means.

### Ground truth — the two findings the PM re-verified first-hand at `master` `0e4ede1b`

Per Gate 8 (verify-not-trust), applied to an investigation report the same way sprint-14 §1 applied it
to the owner's batch brief. **Both load-bearing claims confirmed, and one is sharper than reported:**

**1. The 12% combined cap is real and is exactly where the report said.**
`Features/Orders/OrderFactory.cs:39` — `public const decimal MaxCombinedDiscountFraction = 0.12m`,
documented at `:33-38` as *"LOY-003 — Hard cap on combined (Plus + tier) discount."* Applied at
`:184-197`: `combinedCap = rawSubtotal * 0.12m`; when `membershipDiscount + tierDiscount` exceeds it,
**both are pro-rated down** so their sum equals the cap. **If the Platinum tier rate is itself 12%,
a Plus subscriber at Platinum receives a Plus amount that is arithmetically forced to zero** — and
the pro-rating comment at `:186-188` says the split is kept *"so the user can see both chips on the
receipt with the right amounts"*, which is how a **0 Kč Plus line still prints on the receipt.** The
mechanism the report identified is confirmed at file:line.

**2. "On top of any other discount" is false, and it is DELIBERATE — which changes the fix.**
`OrderFactory.cs:200-210`:

```csharp
// Promo replaces the combined if it's larger. No stacking — keeps
// promo campaigns predictable and prevents stacking a code on top
// of a Plus+Gold combo that's already at the cap.
if (promoDiscount > combined && promoDiscount > 0m)
{
    return new DiscountResolution(MembershipAmount: 0m, TierAmount: 0m, PromoAmount: promoDiscount, …);
}
```

**A winning promo sets `MembershipAmount` to `0m` explicitly.** This is not a bug that slipped in;
it is a designed, commented, argued behaviour. **So the defect is a contradiction between the code and
the marketing copy, and either one may be the thing that is wrong.** A developer told "fix the
stacking" would change money math that someone deliberately wrote. **Only the owner can say which side
moves.**

### The other three findings, RELAYED from the investigation and labelled as such

The PM did **not** re-verify these; they are the investigation's traced findings and the panel must
re-ground them before building on them:
- **Express upgrade has no enforcing code at all.**
- **Favourite cleaner has no matching algorithm and is not Plus-gated.**
- **Recurring bookings are gated client-side only** — *(this one the PM DID confirm: see T-0494.)*
- **The trial is offered unconditionally to resubscribers.**
- **A currency bug in the express surcharge.**

## Acceptance criteria

- [ ] **AC1 — the five perks are ENUMERATED from the actual customer-facing copy, not from memory.**
      Quote each perk verbatim from where a customer reads it: the iOS Plus card, the Android perk
      pills, the web subscribe page, and any marketing string in the i18n bundles. **File:line for
      each.** A perk nobody can quote is not a perk we promised. Evidence: the quote table.
- [ ] **AC2 — each perk gets a written promise: what the customer is entitled to, in one sentence,
      in terms a test could check.** Evidence: the five sentences.
- [ ] **AC3 — the stacking question is DECIDED, and the decision names which side moves.** Given
      `OrderFactory.cs:200-210`'s deliberate no-stacking rule and the "on top of any other discount"
      copy: does the **code** change to stack, or does the **copy** change to say "the better of"?
      This is an **owner** call and the story escalates it rather than defaulting. Evidence: the
      question in `questions/open.md` with both options and their money consequences.
- [ ] **AC4 — the 12%-cap-vs-Platinum arithmetic is COMPUTED and put in front of the owner as
      numbers.** Read the actual tier discount rates from the code and produce the table: *for a
      subscriber at each tier, on a representative basket, what is Plus worth in Kč?* The report says
      **0 Kč at Platinum and 40 Kč at Gold**. **Re-derive it; do not inherit it.** If the number is
      0, the honest framing for the owner is "Plus has negative value for your best customers".
      Evidence: the table with the rates cited at file:line.
- [ ] **AC5 — for each of the three unenforced perks, the story states what enforcement WOULD mean**
      before anyone builds it. Specifically: what is "express" (a surcharge? a scheduling priority? a
      guaranteed window?), and what is "favourite cleaner" (a preference the assignment respects? a
      hard requirement? a soft ranking?). **"Favourite cleaner" with no matching algorithm is not a
      bug to fix — it is a feature nobody has specified.** Evidence: the three specifications.
- [ ] **AC6 — the receipt is decided.** Given a Plus line that contributed 0 Kč, does it print,
      print as 0, or disappear? A receipt showing a benefit that delivered nothing is the most
      customer-visible instance of this whole problem. Evidence: the ruling.
- [ ] **AC7 — the nine owner questions are filed as ONE consolidated block in `questions/open.md`,
      each with `blocking:`, an owner, a resolve-by, and a stated default.** Not nine scattered
      entries. Evidence: the block.
- [ ] **AC8 — the story states which perks can be enforced WITHOUT new product surface** and which
      need a build. That is the line between "we are not honouring what we sold" (urgent) and "we
      have not built this yet" (a roadmap item). Evidence: the split.
- [ ] **AC9 — the analyst's living doc is created/updated in the same step** — a domain doc for
      memberships under `agents/analysts/`, with the discount resolution as a Mermaid decision tree.
      `deliberation.md`: a finalized artifact with stale docs is not finalized.
- [ ] **AC10 (Gate 0.5 leg 3)** — the story states which of the investigation's findings it
      **re-grounded itself** and which it carried forward on trust. The PM re-verified two; the panel
      owns the rest.

## Out of scope

- **Any code.** `git diff --stat -- src/` must be **empty**.
- **The Stripe dashboard check** (does the trial re-offer create a false price or an unlimited free
  loop) — **T-0497**, owner-gated, and it cannot be answered from the repo.
- **The client-side-only recurring gate** — **T-0494**. It is an authorization defect that is true
  whichever way this story rules, so it is deliberately given **no dependency on this ticket**.
- **The express-surcharge currency bug** — **T-0496**, mechanical, no product decision.
- **Pricing the subscription.** What Plus costs is not in scope; what it delivers is.

## Implementation notes

**Analyst panel: author + 3 challengers + lead** (three, not two — five perks and a money path).
The `architect` sits on AC3, because changing `ResolveLoy003Discount` is a money-math change in a
Gate 6.5 class and LOY-003 is a documented rule.

**Challenges the panel must survive:** *"just remove the cap"* — the counter is that the cap exists
to bound platform discount exposure and removing it is a margin decision, not an engineering one.
And *"the copy is just marketing, fix the copy"* — the counter is that the customer paid for the copy.

**Read first:** `OrderFactory.cs` in full (especially `:29-45` and `:160-215`),
`Shared/DTOs/Enums/AppliedDiscountSource.cs`, `Core.Domain/Memberships/*`, and the loyalty tier rate
source. **ADR-0009** governs the money path this touches.

## Status log
- 2026-08-02 — **draft (created by pm from the Cleansia Plus audit).** **Two of the investigation's
  findings were re-verified first-hand before ticketing** and one came back *sharper* than reported:
  the promo-beats-Plus behaviour is not an oversight but an explicit, commented design decision at
  `OrderFactory.cs:200-210` that sets `MembershipAmount: 0m` — which means the fix is a **product
  choice between the code and the copy**, not a bug fix. That distinction is the reason this story
  blocks four tickets instead of one developer guessing.

## Review
