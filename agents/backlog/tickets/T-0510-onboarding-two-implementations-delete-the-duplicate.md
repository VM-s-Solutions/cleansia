---
id: T-0510
title: Partner onboarding has two implementations — web posts one all-or-nothing command, mobile posts six; delete the duplicate
status: draft
size: M
owner: architect
created: 2026-08-02
updated: 2026-08-02
depends_on: [T-0504]
blocks: []
stories: []
adrs: []
layers: [architect, backend, frontend]
security_touching: false
manual_steps: []
sprint: 15
---

## Context

**Source: the partner-onboarding investigation (2026-08-02).** *"Web posts one all-or-nothing command
while mobile has six granular ones; the analyst recommends a scoped rewrite whose main move is
deleting the duplicate."*

**Status: RELAYED, NOT re-verified by the PM.** AC1 re-establishes it.

### Why this is the root of the other five tickets, and why it is still filed LAST

**One flow, two server-side implementations.** That is why the defects in T-0505…T-0509 are
*asymmetric* rather than uniform — consent is required on web and never asked on mobile; language is
unreachable on mobile and unpersisted everywhere. **A field that only one implementation knows about
is a field that behaves differently depending on which client you used**, and nobody has to make a
mistake for that to happen. It is the structure producing the bugs.

**So why not fix the structure first?** Two reasons, and they are the sequencing argument:

1. **A rewrite that lands before the field-level rulings would have to be redone.** T-0507 adds a
   consent record; T-0506 adds a language field; T-0508 may add IČO/DIČ capture. **Consolidating the
   commands and then adding three new fields to the consolidated command means touching it twice.**
2. **A rewrite bundled with five defect fixes is unreviewable.** Each of T-0505…T-0509 has its own
   test, its own error contract and, in two cases, its own migration. Folding them into a
   consolidation diff means a reviewer cannot tell a behaviour change from a refactor.

**The counter-argument is real and the panel must weigh it:** doing the field work twice (once per
implementation) is more total work than consolidating first. **T-0504 AC6 is where that trade-off is
decided** — and it explicitly asks whether the rewrite is *necessary* for any defect fix or merely
tidier. **This ticket's sequencing follows that answer, not this ticket's own preference.**

## Acceptance criteria

- [ ] **AC1 — RE-ESTABLISH both implementations at file:line, side by side.** The web command and the
      six mobile ones, with a field-by-field matrix: which fields each accepts, which each validates,
      which each persists. **The matrix is the deliverable that makes the rest decidable.** Evidence:
      the matrix.
- [ ] **AC2 — the surviving shape is CHOSEN per T-0504 decision 7, with a why-not.** All-or-nothing
      or granular. **The trade-off, stated so it is not hand-waved:** granular lets a partner resume a
      half-finished onboarding and lets each step fail independently; all-or-nothing gives one
      transactional write and one validation surface. **A half-finished onboarding is a real state on
      mobile** — an app can be backgrounded and killed mid-flow (sprint-14 filed **T-0467** for
      exactly this class on the customer booking draft). Evidence: the ruling plus the why-not.
- [ ] **AC3 — deleting the loser is proved SAFE, endpoint by endpoint.** For each command removed:
      who calls it (web, mobile, admin, tests, anything generated), and what replaces the call.
      **A removed endpoint that a shipped mobile binary still calls is a broken app in the field, not
      a compile error.** State the client-version consideration explicitly. Evidence: the caller
      inventory per endpoint.
- [ ] **AC4 — behaviour is PRESERVED, and every divergence is deliberate.** The AC1 matrix has rows
      where the two implementations differ (consent, language). After consolidation, each row has one
      behaviour and the ticket states which one won. **A field that silently stops being validated
      because the surviving command never validated it is a regression introduced by a refactor.**
      Evidence: the matrix, after.
- [ ] **AC5 — characterization tests are written BEFORE the change, against both implementations.**
      They pin what each does today so the consolidation is provably behaviour-preserving. Evidence:
      the tests plus their green run against the pre-change code.
- [ ] **AC6 — the contract change is FLAGGED, not performed.** Removing or reshaping commands changes
      the OpenAPI surface → **`manual_steps: nswag-regen` + `mobile-spec-redump`**, the **owner's**
      bundle. **The PM holds every client leg until the owner confirms**, and sprint-14's record is
      that the step immediately after a regen has a demonstrated failure history (T-0438, PR #166).
      Evidence: the flag before any client leg starts.
- [ ] **AC7 — the sequencing against T-0505…T-0509 is STATED and followed.** Per T-0504 AC6: either
      this lands first and the field tickets build on the consolidated command, or it lands last and
      the field tickets touch both implementations. **Whichever it is, it is written down once and
      every affected ticket's `depends_on` is updated to match** — this is the PM's to reconcile, and
      the ticket must surface it rather than assume. Evidence: the stated order.
- [ ] **AC8 — no defect is fixed in this diff.** Consolidation only. If a bug is found during the
      rewrite, it is **recorded in `## Review` and filed**, not fixed in place. **Mixing them is what
      makes the diff unreviewable.** Evidence: `git diff` contains no behaviour change beyond AC4's
      declared divergence resolutions.
- [ ] **AC9 (Gate 0.5)** — `Cleansia.Tests` / `Cleansia.IntegrationTests` / `Cleansia.HostTests`
      **locally**, baselines **2295 / 108 / 75**, plus the web client suite. **Leg 1:** AC5's
      characterization tests are the mutation target — they must fail if the consolidation changes
      behaviour.

## Out of scope

- **Every field-level defect** — T-0505 (email), T-0506 (language), T-0507 (consent), T-0508
  (invoice), T-0509 (IBAN). **AC8 is explicit.**
- **The onboarding UI on any client**, beyond what a changed contract forces.
- **Running the regen.** AC6.
- **Consolidating any other duplicated command in the codebase.** If the pattern exists elsewhere,
  **record it** — it would be a genuinely valuable finding — and file it separately.

## Implementation notes

**Architect panel** — but note that **T-0504 AC6 may have already made this call**, in which case
this ticket **implements** the ruling and does not re-litigate it. If T-0504 deferred it, the panel
convenes here: author + 2 challengers + lead, with AC2's trade-off as the subject.

**Contract before consumers** (`routing.md` rule 1), **manual steps block** (rule 6).

**Sized `M` with a hard bound: AC8.** A consolidation that also fixes bugs is how an `M` becomes an
`L`, and an `L` may not go `ready`. If AC1's matrix shows the two implementations have diverged more
than expected, **split before starting** rather than growing in flight.

**Read first:** both onboarding implementations in full, `Cleansia.Web.Partner` and
`Cleansia.Web.Mobile.Partner` controllers, and sprint-14's **T-0467** for the process-death argument
AC2 must weigh.

## Status log
- 2026-08-02 — **draft (created by pm from the partner-onboarding investigation).** Finding marked
  RELAYED; AC1 re-establishes it with a field-by-field matrix, which is the artifact that makes
  everything else decidable. **Filed LAST in the onboarding chain despite being the root cause**, with
  the reasoning written into `## Context` and the decision explicitly deferred to **T-0504 AC6** —
  because a rewrite landing before the field rulings gets redone, and a rewrite bundled with five
  defect fixes cannot be reviewed. **AC8 forbids fixing any defect in this diff**, which is the bound
  that keeps it an `M`.

## Review
