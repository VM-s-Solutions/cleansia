---
id: T-0504
title: STORY — partner onboarding collects what it does not keep; define capture, persistence and the legal minimum
status: draft
size: M
owner: analyst
created: 2026-08-02
updated: 2026-08-02
depends_on: []
blocks: [T-0505, T-0506, T-0507, T-0508, T-0510]
stories: []
adrs: []
layers: [analyst, architect]
security_touching: true
manual_steps: []
sprint: 15
---

## Context

**Source: the partner-onboarding investigation (2026-08-02).** It produced **12 defects, 13 gaps and
7 decisions**, and the analyst's recommendation is a **scoped rewrite whose main move is deleting a
duplicate**. The defects map one-to-one onto tickets; **the seven decisions do not, and this story is
where they get answered.**

**Status of the findings: RELAYED, traced by the investigation to file:line, NOT re-verified by the
PM.** Each child ticket re-establishes its own finding before fixing it.

### The five headline findings, and what unites them

| # | Finding | Shape |
|---|---|---|
| 1 | **Email is validated, then silently discarded — behind a success toast.** No email-change path exists **anywhere**, including for admins. | A lie to the user, and an operational dead end |
| 2 | **Language is unreachable during mobile onboarding** — the screen renders `EmptyView()` for that route. **No client persists language at all**; the endpoint has **zero consumers** and is **absent from the mobile partner API entirely.** Pay-period emails are frozen in the day-one language. | A feature that exists on the server and nowhere else |
| 3 | **Consent is required on web, never persisted, never asked on mobile.** | **Legal/GDPR** |
| 4 | **The cleaner's IBAN has no downstream consumer, and their invoice carries no IČO, no VAT and no bank details** — it is not a valid CZ/SK supplier document and **cannot be used to pay them.** | **Legal**, and it blocks paying real people |
| 5 | **Web posts one all-or-nothing command; mobile has six granular ones.** Two implementations of one flow. | The duplicate the rewrite deletes |

**What unites them: this flow collects information and then does nothing with it.** Email, language
and consent are all captured and all dropped; the IBAN is stored and never read. That is one pattern
appearing four times, not four unrelated bugs — and it is why a story comes before the fixes. Fixing
"email is discarded" without deciding **what an email change means** (it is an identity change; it
touches auth, notifications and possibly the login credential) produces a second defect.

### The seven decisions — only the owner can answer several of them

The story enumerates and answers these; the ones that are genuinely the owner's go to
`questions/open.md` as one block:

1. **Is the email an identity/login credential?** If yes, changing it is an auth flow (verification,
   re-issue, session handling), not a profile edit. **This determines whether T-0505 is `S` or an
   epic.**
2. **Who may change a partner's email — the partner, an admin, or both?** Today: neither.
3. **What consent is being collected, and what is the retention/withdrawal story?** Answering "we
   store a boolean" is not enough for GDPR; the record needs *what* was consented to, *which version*
   of the terms, and *when*.
4. **Which countries do we pay cleaners in, and by which bank scheme?** IBAN/SEPA covers CZ/SK/most
   of the EU. **This cannot be guessed** and it decides the shape of the stored bank details.
5. **What must a CZ/SK supplier invoice legally contain?** IČO, DIČ/VAT (and the not-VAT-registered
   case), bank account, variable symbol, issue/supply dates, sequential numbering. **This cannot be
   guessed and it is not an engineering question.**
6. **Is the cleaner an employee or a self-employed supplier (OSVČ/živnostník)?** The whole invoice
   question depends on it, and so does whether the platform issues a *self-billing* document or the
   cleaner issues one to us.
7. **Does the granular (mobile) or the all-or-nothing (web) command shape survive?**

## Acceptance criteria

- [ ] **AC1 — the current flow is DOCUMENTED end to end, both clients, at file:line.** What each
      step collects, what the client sends, what the server persists, and — critically — **what is
      collected and NOT persisted.** Evidence: the table, with the "collected but dropped" column
      populated.
- [ ] **AC2 — all 12 defects are re-listed with a file:line and mapped to a child ticket id**, so
      none is lost between the investigation and the backlog. Any defect **not** covered by
      T-0505…T-0510 is named for filing. Evidence: the mapping table.
- [ ] **AC3 — the seven decisions are answered or escalated, each explicitly.** Technical defaults are
      taken by the panel and recorded; business/legal calls go to `questions/open.md` **as one block**
      with `blocking:`, an owner and a resolve-by. **Decisions 4, 5 and 6 are owner/legal and must NOT
      be defaulted** — the story states the default it would take **and** marks it as needing
      ratification. Evidence: the seven answers plus the questions block.
- [ ] **AC4 — the email decision is made FIRST, because it sizes T-0505.** If the email is an
      identity credential, T-0505 is an auth epic and must be re-filed and re-sized. If it is a
      contact field, it is `M`. Evidence: the ruling with its consequence stated.
- [ ] **AC5 — the consent record's SHAPE is specified**, not just "persist it": what was consented
      to, which document version, when, from which client, and how it is withdrawn. **This is what
      makes T-0507 a schema change and therefore an owner `ef-migration`.** Evidence: the field list.
- [ ] **AC6 — the rewrite recommendation is EVALUATED, not adopted on the investigation's say-so.**
      The recommendation's main move is deleting the duplicate command. The story states which shape
      survives (decision 7), what deleting the other one breaks, and **whether the rewrite is
      necessary for any of the defect fixes or merely tidier.** If the defects can be fixed without
      it, say so — a rewrite bundled with five fixes is unreviewable. Evidence: the evaluation.
- [ ] **AC7 — the whole thing is SIZED and SPLIT so no child is `L`.** Five children are pre-filed
      (T-0505…T-0508, T-0510) plus T-0509. If the story's answers make any of them `L`, it proposes
      the split. Evidence: the sizing table.
- [ ] **AC8 — a living doc is created under `agents/analysts/`** for partner onboarding, with the
      flow as a Mermaid diagram (both clients) and the capture→persistence table. `deliberation.md`:
      a finalized artifact with stale docs is not finalized.
- [ ] **AC9 (Gate 0.5 leg 3)** — state which of the investigation's findings the panel re-grounded
      itself and which it carried on trust. **The PM re-verified none of them** and says so.

## Out of scope

- **Any code.** `git diff --stat -- src/` must be **empty**.
- **The fixes themselves** — T-0505 (email), T-0506 (language), T-0507 (consent), T-0508 (invoice
  contents), T-0509 (IBAN consumer), T-0510 (the duplicate command).
- **Legal advice.** The story states **what the platform must be told**; it does not decide CZ/SK
  invoicing law. Decisions 5 and 6 go to the owner with the question framed precisely enough to take
  to an accountant.
- **The partner *app*'s onboarding UI polish.** This is about what the flow captures and keeps.

## Implementation notes

**Analyst panel: author + 3 challengers + lead**, with the **`architect`** on decision 1 (is email an
identity credential — an auth-contract question) and decision 7 (command shape). Three challengers,
not two: 12 defects and two legal exposures.

**Challenges the panel must survive:** *"just persist the email"* — the counter is AC4. *"add a
consent boolean"* — the counter is AC5. *"the invoice just needs the IČO added"* — the counter is
decision 6, because a self-billing document and a supplier invoice are different documents with
different legal owners.

**⚠️ `security_touching: true`** — consent records and identity changes. The security gate runs on
this story's *output* (the child tickets), but the flag is set here so it is not forgotten downstream.

**Read first:** both onboarding implementations, `Features/Users/UpdateCurrentUser.cs` (and note
**`Q-PROFILE-01`**, `blocking: yes`, is already open on that command's client-supplied `Id` — **an
adjacent, already-escalated defect on the same surface**; the story should check whether its fix
interacts), the `EmployeeInvoice` entity and its PDF generation, and `Core.Domain/Employee*`.

## Status log
- 2026-08-02 — **draft (created by pm from the partner-onboarding investigation).** Filed as a story
  panel ahead of six children because the investigation's own recommendation is a **scoped rewrite**
  and because four of the five headline findings share one root — *the flow collects information and
  keeps none of it* — which is a design question, not five bugs. **All findings marked RELAYED; the
  PM re-verified none of them** and AC9 requires the panel to say what it re-grounded. Two of the
  seven decisions (CZ/SK invoice contents, employee-vs-OSVČ) are **explicitly barred from being
  defaulted** — they are legal and they are the owner's.

## Review
