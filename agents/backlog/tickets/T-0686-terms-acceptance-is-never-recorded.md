---
id: T-0686
title: Nothing records that a customer accepted the terms, or which version they accepted
status: done
size: S
owner: —
created: 2026-09-07
updated: 2026-09-14
depends_on: []
blocks: []
stories: []
adrs: [ADR-0062]
layers: [backend]
security_touching: true
manual_steps: []
sprint: 16
---

## Context

**In plain terms:** if a customer disputes a charge and support wants to answer "you agreed to this
when you signed up", there is nothing to point at. The platform never wrote it down.

- `Register` contains no terms or consent reference at all — nothing is persisted at sign-up.
- The Google and Apple sign-in paths *validate* a `TermsAccepted` boolean and then **discard it**.
  The customer is refused if it is false, and nothing is stored when it is true.
- **No `TermsVersion` field exists anywhere in the model**, so even where consent rows exist they do
  not say WHICH text was agreed to. Terms that change cannot be told apart afterwards.

This is separable from, and much smaller than, the customer audit log it is usually discussed with
(see the audit-log session in the handover). It is also the single piece of evidence most likely to
be needed, because it is the one every chargeback argument starts from.

### The related weakness in what does exist

GDPR consent records are **destructive**: one row per (user, consent type), and re-granting
overwrites the previous IP and user-agent in place. There is no history, so "they consented, then
withdrew, then consented again" cannot be reconstructed. Evidence that overwrites itself is not
evidence.

## Acceptance criteria

- [ ] **AC1** — Given a customer registers by any of the three paths (email, Google, Apple), Then
      the acceptance is persisted with a timestamp and the version of the terms accepted.
- [ ] **AC2** — Given the terms text changes, Then an older acceptance still identifies the text it
      referred to.
- [ ] **AC3** — Given a support agent handling a dispute, Then they can retrieve the acceptance for
      a given customer without a database query written by hand.

## Open decisions

1. **What identifies a version?** A row in a documents table, a semver string, a content hash, or
   the effective date. This decides whether the terms themselves must become data.
2. **Does consent become append-only?** That is the fix for the overwrite problem, and it changes
   the GDPR consent model rather than adding to it. It may be better handled with the legal review.
3. **Backfill.** Every existing customer has no acceptance record. Decide whether they are treated
   as unaccepted, backfilled at their registration date against the then-current text, or prompted
   on next login. This is a legal question, not a technical one.

## Status log

- 2026-09-07 — found while establishing whether a customer audit log exists. Filed separately
  because it is smaller, more urgent, and does not depend on the audit-log design.
- 2026-09-13 — **absorbed by ADR-0062 D4 / T-0732 (T-AUD-3)**, shipped on
  `fix/remove-membership-free-trial`. AC1: `Register.Command.TermsAccepted`, and the server grants
  `TermsOfService` + `PrivacyPolicy` with `UserConsents.DocumentVersion`, IP and device on email,
  Google and Apple sign-up; an email registration also leaves a `customer.account.register` audit row
  with both versions. AC2: `LegalDocumentVersions.CustomerTerms` / `.CustomerPrivacy` =
  `"2026-09-draft"`, pinned to the legal pages' `terms_page.version` / `privacy_page.version` by the
  parity checker; the handle becomes ADR-0041's `AgreementVersion.Version` when those tables land.
  AC3: the admin trail (`/audit-log/customers`, the timeline on `/customers/:userId`) and the subject
  export. The open decisions: (1) a dated string (Q-AUD-L2 default); (2) consent stays
  overwrite-in-place — the `customer.consent.*` audit rows are the history; (3) no backfill, no
  re-prompt, legacy rows `null` = "version unknown" (Q-AUD-L2 default). Mobile clients still send
  nothing and are recorded as *not asserted*, never refused (Q-AUD-L4 default).
