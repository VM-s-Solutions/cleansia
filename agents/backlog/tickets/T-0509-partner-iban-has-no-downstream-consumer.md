---
id: T-0509
title: The cleaner's IBAN is collected at onboarding and read by nothing
status: draft
size: S
owner: backend
created: 2026-08-02
updated: 2026-08-02
depends_on: [T-0508]
blocks: []
stories: []
adrs: []
layers: [backend]
security_touching: true
manual_steps: []
sprint: 15
---

## Context

**Source: the partner-onboarding investigation (2026-08-02).** *"The cleaner's IBAN has no downstream
consumer."*

**Status: RELAYED, NOT re-verified by the PM.** AC1 re-establishes it.

### The shape of the defect

This is the fourth instance of the pattern that runs through the whole onboarding investigation:
**the flow collects information and does nothing with it.** Email (T-0505), language (T-0506), consent
(T-0507) — and here, bank details. The IBAN differs from the other three in one way that makes it
**worse, not better**: it is **successfully stored**. The other three are dropped. This one is
**retained**.

**So the platform is holding financial account identifiers for every cleaner, for no purpose it can
name.** Under GDPR's data-minimisation principle (Art. 5(1)(c)), personal data must be *adequate,
relevant and limited to what is necessary for the purposes for which it is processed*. **Stored data
with no consumer has no purpose to be necessary for.** Either the purpose exists and the data should
be used, or it does not and the data should not be held.

That gives the ticket its two legitimate outcomes, and **the second is a real option**:

- **(a)** wire it to its purpose — the payout document (**T-0508**);
- **(b)** if T-0508's ruling says the platform does not pay by bank transfer against this field
  (e.g. the cleaner invoices us and supplies their own details), **stop collecting it and delete what
  is held.**

### Why `depends_on: [T-0508]`

**"Where should the IBAN be consumed" is answered by "what must the payment document contain".**
Wiring it somewhere before that ruling risks wiring it to the wrong document — and T-0508 is itself
blocked on two owner legal questions. **This is deliberately the last link in that chain**, and it is
small, which is the right shape for a tail dependency.

### And there is a country question underneath it

**IBAN is a European scheme.** `CLAUDE.md` records that `Address.State` is deliberately kept for
*"US/CA when we launch there"*, so non-IBAN markets are on the roadmap. **T-0504 decision 4** —
which countries we pay cleaners in and by which bank scheme — determines whether the stored field is
"an IBAN" or "bank details, of which IBAN is one shape". **That is an owner answer and it changes the
column.**

## Acceptance criteria

- [ ] **AC1 — RE-ESTABLISH the finding, in both directions.** Where is the IBAN written, and prove
      the negative: **nothing reads it.** Search the backend, the Functions, the PDF generation, the
      email templates and the admin app. Evidence: the write site at file:line plus the search
      commands establishing zero readers.
- [ ] **AC2 — the outcome matches T-0508's ruling, and (b) is a legitimate outcome.** Either wire it
      to the payout document, or **stop collecting it and delete what is stored** — stated with the
      reason. **A ticket that resolves "we hold financial data with no purpose" by continuing to hold
      it fails this AC.** Evidence: the ruling reference plus the diff.
- [ ] **AC3 — if (a): validation exists and is real.** An IBAN has a checksum (ISO 13616 mod-97). If
      the platform is going to pay against this field, a typo is a failed transfer or a transfer to
      a stranger. State whether validation exists today and add it if not. Evidence: the validator
      plus tests for a valid and an invalid IBAN.
- [ ] **AC4 — if (a): the field's shape matches T-0504 decision 4's country answer.** If non-IBAN
      markets are in scope, the column is "bank details" with a scheme discriminator, not an IBAN
      string. **If the answer forces a schema change, this stops being `S`** — carry
      `manual_steps: ef-migration` and re-file. Evidence: the shape decision.
- [ ] **AC5 — if (b): the deletion covers stored data, not just the capture.** Removing the input
      while retaining the column and its rows is not the fix. State what is deleted and how.
      **`manual_steps: ef-migration`** if a column is dropped. Evidence: the plan.
- [ ] **AC6 — the value is never logged, and this is CHECKED rather than assumed.** Sprint-14
      established this platform writes PII into Information-level request logs on all five hosts
      (**T-0457**, `ready`, P1) and that **a secret whose field name was never in the redaction token
      list is caught by nothing** (**T-0470**). **An IBAN is exactly a T-0470-class value** — it is
      not `*Secret*`/`*Token*`/`*Key*`/`*Password*` shaped and no list names it. Check the DTO's
      exposure and record the result on **both** tickets. Evidence: the check plus the cross-note.
- [ ] **AC7 — the SECURITY gate runs.** `security_touching: true`. Financial account identifiers.
      The gate reviews storage, transport, logging and access.
- [ ] **AC8 — a test that goes red against the pre-fix code (Gate 0.5 leg 1)** for whichever outcome
      lands. Evidence: the red run, then green.
- [ ] **AC9 (Gate 0.5)** — `Cleansia.Tests` / `Cleansia.IntegrationTests` / `Cleansia.HostTests`
      **locally**, baselines **2295 / 108 / 75**.

## Out of scope

- **The invoice document's contents** — **T-0508**, which this depends on.
- **Building a payout/transfer integration.** If T-0508's ruling implies the platform should
  *initiate* transfers, that is a **new epic**, named here and not started. Wiring a field into a
  document is not the same as moving money.
- **Email / language / consent** — T-0505 / T-0506 / T-0507.
- **The customer payment path.** Different money, different direction, Stripe.

## Implementation notes

**No panel of its own — T-0504 is the panel** and **T-0508** is the ruling this consumes.

**Deliberately sized `S` and placed last in the onboarding chain.** It is one wiring decision once the
document is specified. **AC4 and AC5 both contain a re-file trigger** so it cannot silently absorb a
schema change.

**AC6 is worth doing even if the rest of the ticket waits** — it is a five-minute check that either
closes a T-0470-class exposure or confirms it, and it does not depend on T-0508.

**Read first:** `agents/knowledge/security-rules.md`, sprint-14's **T-0457** and **T-0470**, the
partner onboarding write path, and `Core.Domain/Employee*`.

## Status log
- 2026-08-02 — **draft (created by pm from the partner-onboarding investigation).** Finding marked
  RELAYED; AC1 re-establishes it in both directions. **The PM's framing, added to the ticket:** this
  is the only one of the four "collected and unused" fields that is **successfully retained**, which
  makes it a **data-minimisation** problem rather than a data-loss one — so **"stop collecting it and
  delete it" (AC2 option b) is an explicitly legitimate outcome**, not a failure to deliver.
  `depends_on: [T-0508]` because the consumer is the document that ticket specifies.

## Review
