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
- 2026-08-06 — **AC1 discharged by `backend` (matrix below). No code written; AC2–AC8 remain BLOCKED
  on T-0504 decision 7, which has never run.** The premise is confirmed but its wording is wrong in
  one load-bearing way: the two shapes are not two implementations of *one* flow, they are one flow
  and a **partial** second one. Correcting that changes what "delete the duplicate" can mean.

## Review

### AC1 — the two implementations, at file:line

| | all-or-nothing | granular |
|---|---|---|
| Command(s) | `UpdateEmployee` (`Features/Employees/UpdateEmployee.cs:203`) | `UpdatePersonalInfo` `:52` · `UpdateAddressInfo` `:66` · `UpdateIdentificationInfo` `:112` · `UpdateEmergencyContact` `:44` · `UpdateAvailability` `:80` · `UpdateBankDetails` `:89` |
| Exposed on partner web | `Web.Partner/Controllers/EmployeeController.cs:41` | **`UpdateBankDetails` only** (`:53`) |
| Exposed on partner mobile | `Web.Mobile.Partner/Controllers/EmployeeController.cs:40` | all six (`:52`,`:63`,`:74`,`:85`,`:107`,`:118`) |
| Actually called by web | **yes** — `profile.facade.ts:202` | `UpdateBankDetails` only |
| Actually called by mobile | **no caller in either app** | yes — `ProfileRepository.kt:192,265,291,305`, `PartnerProfileClient.swift:39-73` |

**Field-by-field.** `UpdateEmployee` is a superset of five of the six, **minus bank details**, plus
two fields no granular command has:

| Field | `UpdateEmployee` | granular | persisted by both? |
|---|---|---|---|
| FirstName/LastName/BirthDate/Phone | ✅ | `UpdatePersonalInfo` | yes |
| Street/City/ZipCode/CountryId/State | ✅ | `UpdateAddressInfo` | yes |
| **Latitude/Longitude** | ❌ | ✅ `UpdateAddressInfo:80-85` | **granular only** |
| Nationality/PassportId | ✅ | `UpdateIdentificationInfo` | yes |
| EntityType/RegistrationNumber/VatNumber/LegalEntityName | ✅ (format-checked against `CountryId`) | ✅ (format-checked against **`BusinessCountryId`**, a separate field, `:112`) | yes, **different scoping key** |
| EmergencyName/EmergencyPhone | ✅ | `UpdateEmergencyContact` | yes |
| Availability | ✅ | `UpdateAvailability` | yes |
| **Documents** (`List<BlobFileDto>`) | ✅ `:226` | ❌ — mobile uses `SaveMyDocuments` | different endpoint |
| **Consent** | ✅ `:225`, gated `:132-134`, **persisted `:263`** | ❌ **on all six** | **all-or-nothing only** |
| Bank/payout | ❌ | ✅ `UpdateBankDetails` | granular only |

### Is the shape difference itself a defect? No. Three of its consequences are.

An all-or-nothing write and an incremental one are both legitimate, and AC2's trade-off is real. What
is **not** legitimate is that the two disagree about *facts*, and each disagreement has its own
consequence, independent of which shape survives:

1. **Consent** — the only genuine behavioural divergence. → **T-0507**, verdict recorded there.
2. **Coordinates** — `UpdateEmployee` cannot carry a map pin and always re-geocodes
   (`UpdateEmployee.cs:253` calls `PopulateCoordinatesAsync` unconditionally); `UpdateAddressInfo`
   trusts client coords when present and geocodes only as fallback (`:127-129`). A web onboarding
   therefore stores a *re-geocoded approximation* where mobile stores the exact pin. **Not filed
   anywhere.** AC8 forbids fixing it here — **recorded for the PM to file.**
3. **Tax-id scoping key** — `UpdateEmployee` validates IČO/VAT format against `CountryId` (the
   **home address** country, `:101`/`:115`); `UpdateIdentificationInfo` validates against a dedicated
   `BusinessCountryId` (`:112`). For a cleaner living in one country and registered in another the
   two commands reach **opposite verdicts on the same number**. **Not filed anywhere.** Recorded for
   the PM to file.

### AC3 — "deleting the loser is safe" cannot be answered yet, and one leg of it is already decided

`UpdateEmployee` is exposed on **both** hosts and its `Consent` field is already in the shipped
generated clients (`cleansia_ios/CleansiaPartnerApi/Models/UpdateEmployeeCommand.swift:33`, and the
Android/partner equivalent). So deleting the **granular** side would strand two shipped mobile
binaries whose entire profile surface is those six calls — that is AC3's "broken app in the field",
not a compile error. Deleting the **all-or-nothing** side would strand the web profile page
(`profile.facade.ts:202`) and, today, the only path that records consent at all. **Neither deletion
is currently safe without a client release first**, which is the fact AC2 should be decided against.

### Verdict: BLOCKED, not executable

AC2 says the surviving shape is chosen "per T-0504 decision 7". **T-0504 is still `status: draft` and
its panel has not run**, so there is no ruling to implement and this ticket cannot proceed without
re-litigating a decision it explicitly declines to take. AC6's `nswag-regen` + `mobile-spec-redump`
are owner-gated. Recommend: run T-0504, then re-size this against the matrix above — the two
undeclared divergences (2) and (3) mean AC4's "behaviour is preserved" now has **three** rows to
resolve, not one, which is the `split before starting` trigger the `## Implementation notes` names.
