---
id: T-0507
title: LEGAL — partner consent is required on web, never persisted, and never asked on mobile
status: draft
size: M
owner: db
created: 2026-08-02
updated: 2026-08-02
depends_on: [T-0504]
blocks: []
stories: []
adrs: [0012]
layers: [db, backend, frontend, android, ios]
security_touching: true
manual_steps: [ef-migration]
sprint: 15
---

## Context

**Source: the partner-onboarding investigation (2026-08-02).** *"Consent is required on web, never
persisted, never asked on mobile."*

**Status: RELAYED, NOT re-verified by the PM.** AC1 re-establishes it.

### Why this is filed as a legal exposure and not as a defect

**The platform blocks a user from proceeding until they consent, and then keeps no record that they
did.** That is the worst of both worlds:

- **We cannot prove consent.** Under GDPR the controller must be able to **demonstrate** that the
  data subject consented (Art. 7(1)). A checkbox that gates a form and writes nothing demonstrates
  nothing. If a partner disputes it, the platform's evidence is *"our UI would not have let them
  continue"* — which is an argument about our code, not a record about them.
- **A second cohort never consented at all.** Every cleaner who onboarded through **mobile** was never
  asked. So the population splits into "asked, not recorded" and "not asked" — and **the platform
  cannot currently tell which of its partners is in which group**, because neither leaves a trace.

**This is not post-demo polish.** It is a compliance gap that grows monotonically with every new
partner, and it cannot be retro-fixed for the people already onboarded — you cannot generate a
consent record for a consent that was never captured. **Every day this stands, the un-provable
cohort gets larger and permanently so.**

### What makes this a schema change, and therefore owner-gated

T-0504 AC5 specifies the record's shape, and it is deliberately **not a boolean**:

| Field | Why |
|---|---|
| **what** was consented to | terms, privacy policy, marketing — these are separate consents with separate legal bases |
| **which version** | terms change; a consent to v1 does not cover v3 |
| **when** (UTC) | the demonstrable timestamp |
| **from which client / context** | web, Android, iOS — provenance |
| **how it is withdrawn** | GDPR Art. 7(3): withdrawal must be as easy as giving it |

**That is a new entity or a new set of columns → an EF migration → `manual_steps: ef-migration`,
which is owner-only.** The PM flags it and **holds** the dependent client work until the owner
confirms.

## Acceptance criteria

- [ ] **AC1 — RE-ESTABLISH all three claims at file:line.** Where web requires it; where it is
      dropped; and the negative search proving neither mobile app asks. Evidence: three answers plus
      the search commands.
- [ ] **AC2 — the record's shape matches T-0504 AC5 exactly**, including the version field.
      **A boolean column does not pass this AC.** Evidence: the entity/columns plus the mapping to
      AC5's list.
- [ ] **AC3 — the migration is WRITTEN AS A SPEC, and the migration itself is FLAGGED, not run.**
      **No agent runs `dotnet ef migrations add` or `database update`** (`CLAUDE.md`). The ticket
      carries the exact schema the owner must migrate. Evidence: the spec, and the owner's
      confirmation recorded before any dependent work starts.
- [ ] **AC4 — consent is captured on ALL THREE onboarding clients**, or the ones it is not is named
      with a date. Partner web, partner Android, partner iOS. Evidence: three screenshots plus three
      round trips.
- [ ] **AC5 — the existing partner population is ADDRESSED, and "we cannot fix it" is written down if
      that is the answer.** State how many partners exist with no consent record, what the platform
      does about them (re-consent prompt on next login? a flag for legal? nothing?), and — **most
      importantly — state plainly that consents given before this ticket cannot be reconstructed.**
      Evidence: the count, the ruling, and the sentence.
- [ ] **AC6 — withdrawal exists or is named as a follow-up with a date.** GDPR Art. 7(3). If it is
      not built here, it is a **named ticket**, not an omission. Evidence: the path, or the named
      ticket.
- [ ] **AC7 — the consent record is auditable and PII-minimized.** ADR-0012 governs, and
      `Q-AUDIT-01`'s answer set the posture: **ids and changed fields, never raw subject PII**, and
      a GDPR-delete audit legitimately survives the subject's erasure as a legal-basis exception.
      **A consent record is exactly that class** — it must survive an erasure request, and the story
      must say so. Evidence: the retention statement checked against ADR-0012.
- [ ] **AC8 — the consent text itself is versioned and its source is named.** A version field with
      nothing to point at is decoration. Where does the terms version come from, and what happens
      when it changes? Evidence: the mechanism.
- [ ] **AC9 — a test that goes red against the pre-fix code (Gate 0.5 leg 1)**: an assertion that a
      completed onboarding produces a consent row. It fails today by construction. Evidence: the red
      run, then green.
- [ ] **AC10 — the SECURITY gate runs.** `security_touching: true`. The gate checks the record's PII
      posture and that consent state cannot be forged or set by a client that should not.
- [ ] **AC11 (Gate 0.5)** — `Cleansia.Tests` / `Cleansia.IntegrationTests` / `Cleansia.HostTests`
      **locally**, baselines **2295 / 108 / 75**; plus the client suites.

## Out of scope

- **The customer apps' consent.** Partner-scoped. **⚠️ If the customer flow has the same defect, that
  is a bigger exposure by population — record it in `## Review` and file it separately, immediately.
  Do not widen this ticket and do not leave it unfiled.**
- **Writing the terms or the privacy policy.** Legal content is the owner's.
- **Running the migration.** AC3.
- **Email** — T-0505. **Language** — T-0506.

## Implementation notes

**No panel of its own — T-0504 is the panel**, and AC5 of that story is this ticket's schema.

**`db` owns this ticket** (`routing.md`: new entity/column/migration → `db`), then `backend`, then the
three clients. **Contract before consumers**, and **manual steps block**: the client legs **hold**
until the owner confirms the migration.

**⚠️ This ticket has the longest owner-gated tail in the sprint** — an `ef-migration` **and**
possibly an `nswag-regen` if the DTO changes. **Start the schema spec early even if the rest waits**,
because the wait is the schedule.

**Read first:** `agents/knowledge/security-rules.md`, **ADR-0012** + `Q-AUDIT-01`'s answer in
`questions/answered.md`, `Cleansia.Infra.Database/EntityConfigurations/`, and the multi-tenancy
`TenantId` convention (a consent record is tenant-scoped).

## Status log
- 2026-08-02 — **draft (created by pm from the partner-onboarding investigation).** Finding marked
  RELAYED; AC1 re-establishes it. **Filed as a legal exposure with `manual_steps: ef-migration`.**
  The framing the PM added and the investigation did not: the population **splits into "asked, not
  recorded" and "never asked"**, the platform **cannot tell which partner is in which**, and
  **consents already given cannot be reconstructed** — so the un-provable cohort grows monotonically
  and permanently. That is why AC5 forces the sentence to be written rather than the problem quietly
  deferred.

## Review
