# ADR-0041 — Challenger (legal-sufficiency / obligation lane)

Role: CHALLENGER. Gate 0 discipline: every claim below is traced to `file:line` I opened, or to a
decision id in the ADR itself. Where the ADR was right I say so in the final section rather than
inflating it. I sampled the ADR's *"every citation verified in the working tree"* claim on eleven
citations; nine hold exactly, and the five that do not are listed at the end of the sound section —
two of them are not offsets drifting, they are **load-bearing facts that are false**.

**Headline.** The ADR's own §Challenge asks whether anyone will *watch* D6.3's number (CH-A2). That is
the wrong worry. **The number cannot be produced.** `PayPeriodBackgroundService` — the path that issues
the monthly self-billed document — **does not call `GenerateInvoice`**; it constructs the invoice inline
at `PayPeriodBackgroundService.cs:328`. D6.2 puts the stamp in `GenerateInvoice`'s handler, so on the
ordinary issuance path `SelfBillingAcceptanceId` is **null by construction, forever**, and D6.3's
detection query returns 100% of rows as unattributed. Every non-blocking argument in this ADR — D5's
"it stamps", A5's rejection, CH-A2's answer, CH-A5's answer — rests on that stamp existing. It does not.
And verification step #3 certifies the implementation as compliant.

Secondarily: the ADR is on a horn it does not name, and the horn is decidable from its own text (CH-L2);
and the legal trigger it exists to satisfy has an **open, blocking, unanswered owner question already in
`questions/open.md`** (`Q-PAYOUT-03`) that the ADR neither cites nor depends on (CH-L3).

---

### CH-L1 — D6.2's stamp is wired to a code path that does not issue invoices. The one control that makes D5's non-blocking posture defensible is null on every ordinary document, and verification #3 passes on it

F5 states: *"Invoices are generated from `PayPeriodBackgroundService.SendPeriodClosedEmailsAsync`"*, and
D9 spells the chain *"`PayPeriodBackgroundService` → `GenerateInvoice` → the D6.2 stamp."* **The arrow
does not exist.** There are two independent invoice-creation paths and they share no code:

| Path | Where the `EmployeeInvoice` row is born | Reached from |
|---|---|---|
| **Monthly close (the ordinary one)** | `/Users/michael/Desktop/Mike/Projects/cleansia/src/Cleansia.Core.AppServices/Services/PayPeriodBackgroundService.cs:328` — `EmployeeInvoice.CreateFromOrderPays(...)` **inline, no MediatR** | `Cleansia.Functions.Core/Handlers/PayPeriodTimerHandler.cs:13` → `CloseExpiredPeriodsAndOpenNewAsync` (`:107`) → `SendPeriodClosedEmailsAsync` (`:155`, `:199`) → `GenerateInvoiceForEmployeeAsync` (`:298`) |
| **`GenerateInvoice.Command`** | `/Users/michael/Desktop/Mike/Projects/cleansia/src/Cleansia.Core.AppServices/Features/EmployeePayroll/GenerateInvoice.cs:87` | only `Cleansia.Web.Admin/Controllers/AdminPayrollController.cs:61` (an admin button) and `Cleansia.Functions.Core/Handlers/GenerateInvoiceHandler.cs:64`, whose **sole** producer is `FiscalReconciliationService.cs:151-152` |

I checked for a producer that would join them: `rg 'GenerateInvoiceMessage'` over `src/**/*.cs` returns
`FiscalReconciliationService.cs:151-152` and nothing else in production. The sweep does not enqueue; it
duplicates. The duplication is visible and pre-existing — the sweep re-implements
`GenerateInvoice.Validator`'s "already invoiced" rule at `PayPeriodBackgroundService.cs:312-323` and its
"unpaid pays" rule at `:304-310`, and it resolves currency by a **different rule** (`:325` reads
`employee.PreferredCurrencyCode`; `GenerateInvoice.cs:81-85` calls
`ICurrencyResolutionService.ResolveCurrencyCodeForEmployeeAsync`).

**Consequences, all of which the ADR's own logic depends on being false:**

1. Every invoice a cleaner actually receives is stamped `null`. D6.3's *"invoices issued with a null
   stamp, by month"* is `COUNT(*)` of the month's invoices. A report whose value is always 100% is not
   a control; it is noise that trains an operator to ignore the row.
2. **Verification #3 passes green on this.** *"`GenerateInvoice`'s validator gains no rule. It gains a
   stamp in the handler."* — an implementer does exactly that, a reviewer checks exactly that, and no
   production invoice is stamped. That is the failure mode this panel format exists to catch: a
   compliance step that certifies a violating implementation.
3. D5's `GenerateInvoice` row (*"NEVER BLOCKS. It **stamps**"*) is the entire answer to A5. With no
   stamp on the real path, A5's rejection loses its compensating control and the choice becomes
   "block" versus "nothing", which is not the choice the ADR argued.

**Ask.** Either (a) the ADR names `PayPeriodBackgroundService.GenerateInvoiceForEmployeeAsync` as a
second stamp site and prices it, or — better, and the seam-preserving option — (b) it makes the sweep
*dispatch* `GenerateInvoice.Command` (or enqueue `GenerateInvoiceMessage`) so one issuance path exists
and one stamp site exists. (b) is a real refactor with its own risk and must be its own ticket sequenced
**before** ticket 2, not folded into it. Either way verification #3 must be rewritten to assert on **an
invoice produced by the timer path**, not on a handler's source.

---

### CH-L2 — D6 is on a horn and the ADR can be pushed off it from its own text: D6.1 issues *"on the contract's basis"* for exactly the cohort F4 defines as having no contract clause — and D6.3's invoice-side number is, by verification #10, incapable of ever going down

The panel brief asked which horn this ADR is on. It is decidable without counsel.

**The two claims, quoted:**

- *"The owner's contract clause is the legal basis; the checkbox is corroborating evidence"* (D5,
  `GenerateInvoice` row) — repeated at D6.1 (*"on the contract's basis"*) and A5 (*"our own
  corroboration of a term the contract already carries"*).
- *"Cleaners who already signed signed a contract **without** the clause. So for that cohort **neither
  leg exists**."* (F4) — repeated at A6 (*"the pre-clause cohort is covered by neither instrument"*).

D6.1 governs **precisely the cohort F4 defines by the absence of the contract clause**, and justifies
continuing on the basis of that clause. That is not a tension between two sections; it is one section
citing as its authority the thing another section says the population lacks. `Q-SELFBILL-02`'s stated
default reproduces the same sentence (*"**Yes**, on the contract's basis"*), so the escalation carries
the defect into the owner's inbox.

**Which horn.** If the contract clause suffices, then D1's three tables, D3's echo protocol and D3.3's
SHA-256 are an *evidence-quality* investment, not a compliance control — a legitimate thing to build,
but then D5's blocking rule gates on corroboration and could be dropped tomorrow with zero legal
consequence, and A6 was rejected for the wrong reason. If it does not suffice — which is the only
reading on which D1 earns its place, and the reading the owner's *"it's gonna be mandatory"* implies —
then D6.1 is issuing documents **with no basis at all** for the cohort it names, and saying so plainly
is the honest form of the escalation.

**And the compensating control cannot close.** D6.3 claims the exposure is *"bounded, visible and
closable"*, and CH-A2's answer to "barely mandatory" is that the report *"converts mandatory from a gate
into a **closable number**"*. On the cleaner side, yes. On the **invoice** side — the side that measures
actual exposure — verification #10 requires the opposite: *"asserts the first invoice's stamp is still
null (never back-filled by a later read)."* So *"invoices issued with a null stamp, by month"* is
**monotonically non-decreasing by design**. Nothing anyone does ever reduces it.

That freeze is a **misapplication of the pattern the ADR cites for it**. `patterns-backend.md` §B8 /
ADR-0009 D2 freeze a *computed output* against later config drift — the pay rate that was applied, the
price that was charged. An acceptance recorded later under D6.4 with the **contract's own signature
date** as `OccurredAt` (D2.3 makes that explicit and correct) is not config drift; it is **new evidence
about the state of the world at issuance**. Freezing it out means the ADR's schema can express *"the
agreement was in force on 3 June"* and its stamp rule forbids that fact from reaching the June invoice.
D6.4 is presented as the closure mechanism for D6's cohort and D6.2 structurally prevents it from
closing anything already issued.

**Ask.** (1) Q-SELFBILL-02 must be re-framed: not *"may we keep issuing on the contract's basis"* — the
cohort has no clause — but *"for a supplier with no self-billing agreement of any kind, is the document
valid, and if not is the remedy reissue or a retroactive acceptance?"*, with the count from D6.3
attached so the owner accepts a **number**. (2) D6.2 must distinguish *freeze* from *never-resolve*: a
stamp that is null at issuance and is later resolved **once**, by an `AdminRecordedContract` acceptance
whose `OccurredAt` precedes `GeneratedAt`, is not a B8 violation — it is the only repair the design
admits. If the panel keeps verification #10 as written, D6.3 must stop calling the number closable and
CH-A2's answer must be withdrawn.

---

### CH-L3 — The obligation attaches to **VAT-registered** suppliers, the platform derives that bit live and mutably, `Q-PAYOUT-03` is open + blocking + unanswered on exactly this, and the ADR neither cites it nor gives the version key an axis for it

The ADR never states the legal rule it is discharging. It cites the owner's instruction and
`Q-PAYOUT-02`'s ruling and proceeds. That is defensible for an architect — but it means the ADR cannot
say *when* the requirement bites, and the population it bites on is **already modelled in this repo, as
a mutable per-cleaner field**:

- `/Users/michael/Desktop/Mike/Projects/cleansia/src/Cleansia.Core.AppServices/Extensions/FileExtensions.cs:93-108`
  — the self-billed document's supplier block sets `IsVatPayer = vatNumber != null`, derived **live at
  PDF-build time** from `Employee.VatNumber`, with the comment *"A registered cleaner is rare rather
  than impossible, so the document expresses both variants and the presence of a validated DIČ is what
  selects between them."* `DefaultInvoiceLayoutBuilder.cs:137-143` prints DIČ or "not VAT registered"
  accordingly.
- `Employee.VatNumber` is **self-service editable**: `UpdateEmployee.Command.VatNumber`
  (`/Users/michael/Desktop/Mike/Projects/cleansia/src/Cleansia.Core.AppServices/Features/Employees/UpdateEmployee.cs:221`),
  validated by `ITaxIdValidator.ValidateVatNumberAsync` at `:111-119`, on the partner web **and** partner
  mobile hosts.
- The owner's *"realistically never a VAT payer"* is contradicted by the platform's own reference data:
  `Infra.Scripts/SeedData/insert_users_employees.sql:73-111` seeds **three of five** cleaners with a
  `VatNumber` and labels them *"OSVČ, VAT-registered"* and *"Legal entity (s.r.o.), VAT-registered"*.
  The launch cohort the ADR's F3 counts is 60% VAT-registered.
- **`Q-PAYOUT-03` is open, `blocking: YES`, unanswered** (`agents/archive/2026-08/backlog/questions/open.md:873-889`)
  and asks the missing question verbatim: *"is 'null means not registered' sufficient, or must a cleaner
  positively declare their status **(and can it change mid-pay-period)**?"* `INDEX.md:391` records
  T-0522 — the ticket that shipped the very document this ADR governs — as *"blocked only on
  Q-PAYOUT-02/03"*. Q-PAYOUT-02 was answered 2026-08-04; **Q-PAYOUT-03 was not.**

**Why this is architectural and not counsel's.** D1 freezes a version key —
`UNIQUE (TenantId, Kind, CountryId, Version)` — in an owner-run migration the ADR argues must happen
**now** ("the window is open today"). That key has **no axis for the supplier's VAT status**, and D5
binds acceptance at onboarding submit, which is the single moment at which the answer is most reliably
"not registered". A cleaner who registers for VAT in year two carries an acceptance recorded when the
document was not a `daňový doklad`. If the answer to Q-PAYOUT-03 is that status is declared and can
change, then either the agreement text is the same for both (an owner/counsel finding the ADR is
entitled to *record* but has not obtained) or the version key needs a fourth dimension — and adding a
dimension to a unique index **after** append-only acceptance rows exist is exactly the expensive change
D1 exists to prevent.

**Ask.** Add `Q-SELFBILL-00` (or fold into Q-PAYOUT-03): *does the self-billing agreement's required
wording differ by the supplier's VAT status, and is the platform's `VatNumber != null` derivation the
authoritative determination?* Mark it **blocking on the migration**, not on activation. And state
explicitly in D1 whether `AgreementVersion` is keyed on jurisdiction alone by decision or by omission.

---

### CH-L4 — D4.5 says *"a missing locale degrades honestly; it never blocks"*. Once D5 blocks, that is false — and the expected launch case is a cleaner hard-blocked on affirming a legal text in a language they do not read

D4.2's activation gate is *"at least one `AgreementVersionText` at `BusinessSupplied` or above"* — **any
one language**. D4.4 then serves the caller's language *"if that `(version, language)` row is
`BusinessSupplied`+; otherwise the version's authored language, with `bodyLanguageIsFallback: true`."*
D5 then **blocks the onboarding submit** on a current acceptance.

Compose them. The owner supplies a reviewed **Czech** body (the realistic first delivery — `Q-SELFBILL-01`
asks for *"as many of the five locales as you can supply"*). The feature switches **on** for CZ. A
Ukrainian- or Russian-speaking cleaner — a demographic this platform ships five locales for, and whose
presence is the reason `uk`/`ru` exist in `apps/*/src/assets/i18n/` — now cannot complete onboarding
until they tick a box affirming a Czech legal text, flagged as a fallback by a boolean their client may
or may not surface.

D4.5's sentence and D5's rule cannot both be true. And this lands directly on D3's premise: the ADR
spends a server-computed SHA-256 to prove *which bytes were displayed* — then permits those bytes to be
in a language the signer cannot read and records the result as proof of agreement. `BodyHash` makes the
record precise; it does not make it true. The ADR's own words for A3 apply to itself here: *"a record
that is false and looks perfect."*

I checked whether the shipped precedent covers this and it does not. `patterns-backend.md`'s rule that
the ADR cites — *"the notice is printed in the language it was reviewed in, while the heading follows
the reader"* — governs a **notice printed on a document** (`CountryInvoiceContext.ReviewedLegalNotice`),
where the reader is not asked to *assent* to anything. Assent is a different act; the precedent does not
transfer, and D4.4 transfers it without noticing.

**Ask (cheap, one predicate).** Split the gate: a version is **renderable** when any language is
`BusinessSupplied`+ (D4.2 as written), but it is **demandable** — i.e. D5's validator fires — only when
the *caller's* language has a `BusinessSupplied`+ body. A cleaner without a reviewed body in their
language gets the D5 prompt, not the block. That preserves D4.5's promise literally, costs nothing, and
makes "we shipped Czech first" a partial rollout instead of a language barrier at the door.

---

### CH-L5 — D5's "onboarding submit" is neither onboarding-only nor reachable on mobile; the table contradicts itself; and the one gate that fits every criterion D5 states — `ApproveEmployee` — is never considered

**(a) `UpdateEmployee` is the general profile-save, not an onboarding submit.**
`/Users/michael/Desktop/Mike/Projects/cleansia/src/Cleansia.Web.Partner/Controllers/EmployeeController.cs:41-51`
— `PUT api/Employee/UpdateEmployee`, `Policy.CanUpdateCurrentEmployee`, no onboarding qualifier — and
the partner web **profile page** posts it:
`libs/cleansia-partner-features/profile/src/lib/profile/profile.facade.ts:202`. So D5's rule fires
whenever a cleaner in year two saves a phone-number change. D5's own justification (*"They are on the
form anyway; the cost is one tick. This is where 'mandatory' is free"*) does not hold for that request.

Worse, it makes D5's table self-contradictory. Row 6 rules that *"the cleaner whose accepted version is
stale — **Prompted, not blocked**"*. Row 1 blocks `UpdateEmployee` on `HasCurrentAcceptanceAsync`. A
stale cleaner editing their address hits **both rows on one request**, and row 1 wins. The ADR has no
statement of which governs.

**(b) There is no mobile equivalent, and the ADR's own ticket 9 says so.** I verified the ADR's claim
that mobile never sends `UpdateEmployee` — `rg 'UpdateEmployee|updateEmployee'` over
`src/cleansia_android` returns only the OpenAPI spec and an unrelated local `updateEmployeeId` store;
over `src/cleansia_ios` it returns **nothing**. Correct. But the corollary the ADR draws is wrong. D5
says the rule attaches to *"the mobile granular equivalent"*. There is no submit event to attach to:
`Web.Mobile.Partner/Controllers/EmployeeController.cs:52-94` exposes four independent section PUTs
(`UpdatePersonalInfo` / `UpdateIdentificationInfo` / `UpdateAddressInfo` / `UpdateBankDetails`). Attach
to all four and a cleaner cannot fix a typo in their address without accepting; attach to none and
**mandatory is web-only** and every mobile cleaner joins D6's un-asked cohort at registration, by
construction. The ADR's **ticket 9** already records that this exact structural gap
(*"Mobile partner onboarding grants no `DataProcessing` consent at all"*, T-0504 finding 3) is **unsolved
and out of scope**. D5 assumes solved what ticket 9 defers.

**(c) The alternative that is never named.**
`/Users/michael/Desktop/Mike/Projects/cleansia/src/Cleansia.Core.AppServices/Features/Employees/ApproveEmployee.cs`
already: is a **write path** (D5's own stated criterion — *"the gate must be a write-path rule"*);
already validates `IsProfileComplete()` **twice**, in the validator at `:36-49` and again in the handler
at `:114-119`, so a completeness-shaped precondition there is the shipped idiom; is the transition
`AssignWorkCountry` + `Approve` (`:124-125`) after which a cleaner can take work and accrue pay — which
is exactly the moment **F3** names (*"the moment an admin approves them they can work, accrue pay, and
be issued a self-billed invoice"*) and then does nothing about.

Its properties against D5's own constraints: it does **not** touch `IsProfileComplete()` or
`[RequireCompleteProfile]`, so ADR-0034 §F2's platform-wide 403 is not re-created; it **cannot** lock
out a working cleaner, because it only governs the `Pending → Approved` edge; it is **host-agnostic**,
so it covers web and mobile with one rule and closes (b); it hits **exactly** F3's five seeded cleaners,
every one of whom is `ContractStatus = 1` (Pending — verified,
`insert_users_employees.sql:64,74,84,94,104`) and therefore has nothing to be locked out of; and the
operator's release valve is **already specified by this ADR** — D6.4's `RecordPartnerAgreementCommand`.
Its real cost is back-office friction (an admin cannot approve a cleaner who has not accepted), which is
a different and much smaller harm than the payment outage A5 was rejected for, and D4.3 keeps it inert
where no text exists.

I am not asserting it is the right answer. I am asserting the ADR frames the choice as
{onboarding submit, platform-wide 403, payment block} and there is a **fourth** option that satisfies
every criterion D5 itself lays down. A decision with a real trade-off must answer its alternatives; this
one is not in the record.

**Ask.** D5 must (1) state which of row 1 / row 6 wins on a profile edit, (2) either name the concrete
mobile attachment point or state plainly that v1's mandatory is web-only and size the resulting cohort,
and (3) evaluate and either adopt or reject-with-reasons the `ApproveEmployee` gate.

---

### CH-L6 — D3's stale rejection is, as ticketed, rendered to the cleaner as *"An error occurred. Please try again."* and is an unrecoverable loop; and the accept/submit pair is never sequenced

The brief asks whether a rejection is distinguishable from a bug. **As specified it is literally
rendered as the generic bug message.**

- Ticket 7 prescribes *"the two error keys under **`errors.agreement.*`**"*.
- The shared interceptor resolves `` `api.${dotValue}` ``
  (`libs/core/services/src/lib/interceptors/http-error.interceptor.ts:14-20`, root `CLAUDE.md` §i18n).
  A key present only under `errors.*` resolves to itself, the interceptor substitutes
  `api.common.error_occurred`, and the cleaner sees *"An error occurred. Please try again."*
  Partner and customer locales carry `api` only; `errors.*` is admin-legacy.
- **The guard will not catch it.** `apps/cleansia-partner.app/src/app/i18n/error-contract-parity.spec.ts`
  asserts against a **hand-maintained array**, `PARTNER_SURFACE_ERROR_KEYS` (`:95-155`), not against
  `BusinessErrorMessage.cs` at runtime. A key that is never added to the array is never checked, so the
  spec stays green while `agreement.version_stale` renders as the generic error.

**Now the flow.** D4.6 makes rollout an owner `INSERT` with `EffectiveFrom = now` — there is no staging
and no grace window, and D10 row 2 sells that as the feature (*"effective immediately on every installed
app"*). So:

1. 09:00 — cleaner opens onboarding; `GET .../me/self-billing-agreement` returns `v1`.
2. 09:05 — owner inserts `v2`.
3. 09:07 — cleaner posts `accept { agreementVersionId: v1 }` → `agreement.version_stale` → *"An error
   occurred."* The client still holds `v1`; **no client ticket (4/5/6) specifies a refetch-on-stale**,
   and D3.2's *"so the client can refetch and re-render"* is an assumption about a client that does not
   exist yet. Retry produces the identical failure. The flow is dead until the app is reloaded.

**And a second, worse interleaving the ADR never sequences.** The acceptance is a *separate* endpoint
(D5, deliberately) and the mandatory rule lives on `UpdateEmployee`. If the version rolls **between** a
successful accept and the submit, `HasCurrentAcceptanceAsync` resolves `v2`, finds a `v1` acceptance,
and the submit fails with `agreement.self_billing_required` — telling a cleaner who *just ticked the
box* that they must tick the box. Two commands, two error keys, one unmodelled window.

**Ask.** (1) Both keys go under `api.agreement.*` and into `PARTNER_SURFACE_ERROR_KEYS` in the same
change, in all five locales, on partner web — ticket 7 is wrong as written. (2) D3.2 must carry a client
contract: on `agreement.version_stale`, the client re-runs the GET, re-renders the body, **unticks the
box**, and shows a specific string ("the agreement was updated — please review it again"). (3) The
onboarding validator must accept an acceptance of the version that was current *at the time of the
acceptance* within a short window, or D3 must state that a mid-flow roll costs the cleaner a re-read and
the clients must implement it — but silence here ships the loop.

---

### CH-L7 — D9 and F5 mis-diagnose the invoice paths' tenancy and prescribe the **opposite** of the correct call; verification #6 would enforce an S8 regression

F5: *"the invoice sweep runs with no tenant claim."* D9: *"`GetCurrentAcceptanceIgnoringTenantAsync` —
the invoice sweep; names its world."* Both invoice paths **establish the tenant before any handler
runs**:

- `PayPeriodBackgroundService.cs:119-122` reads periods with `GetQueryableIgnoringTenant()`, then
  `:133-142` groups by `TenantId` and calls `ClearTenantOverride()` / `SetTenantOverride(tenantGroup.Key)`,
  and **only then** `:155` `SendPeriodClosedEmailsAsync`. Inside it, the sibling reads are **scoped** —
  `_employeeRepository.GetQueryable()` at `:203-209`, `_employeeInvoiceRepository.GetQueryable()` at
  `:312-314`. Commit is per tenant group at `:187`. This is precisely the reference shape root
  `CLAUDE.md` describes, and it is already correct.
- `Cleansia.Functions.Core/Handlers/GenerateInvoiceHandler.cs:48-61` — `GetByIdIgnoringTenantAsync` to
  find the employee by trusted id, then `SetTenantOverride(employee.TenantId)`, **then** `mediator.Send`
  at `:63`. Its own doc comment (`:16-18`) states the rule.

So an `...IgnoringTenantAsync` acceptance read inside `GenerateInvoice.Handler` or inside the sweep's
inline path would read acceptances **across all tenants** while the ambient tenant is already set, and
could stamp tenant A's invoice with tenant B's acceptance row. That is a cross-tenant read the code does
not have today, introduced by the ADR, and **verification #6 mandates it**: *"if a request path and the
invoice sweep both reach it, there must be two names."*

**Ask.** D9 must be rewritten from evidence: the two-variant naming is right as a *rule*, but the
invoice paths take the **scoped** variant because they have already named their tenant. The
`IgnoringTenant` variant, if it exists at all, needs a caller — and I could not find one. Verification #6
must be inverted: assert that **no** agreement repository method with `IgnoringTenant` in its name is
referenced from `GenerateInvoice`, `PayPeriodBackgroundService` or the accept/status handlers. D9's
pinning-test point (seed a **non-null** `TenantId`) is correct and should survive.

---

### CH-L8 — D6.2 binds an **immutable** acceptance to a **mutable** document. The ADR spends a SHA-256 pinning the agreement's bytes and leaves the invoice's bytes rewritable by the supplier

D6.2's promise: *"Which text authorized this specific document? is a two-hop join, forever."* True for
the text. **"This specific document" is not fixed.**

- `RegenerateInvoicePdf` exists on **two** hosts —
  `Cleansia.Web.Partner/Controllers/EmployeePayrollController.cs:68-79` and
  `Cleansia.Web.Admin/Controllers/AdminInvoiceController.cs:80-90` — and its handler rebuilds the entire
  supplier block **live** from the current `Employee` row
  (`RegenerateInvoicePdf.cs:60-98` → `FileExtensions.CreatePdfData` → `CreateSupplierData`), then
  overwrites the blob at the same name (`:100`, `:131-144`) and re-points the invoice row (`:102`).
- The supplier's **VAT character** is among the recomputed values (`FileExtensions.cs:95-108`,
  `IsVatPayer = vatNumber != null`), and `Employee.Anonymize()`
  (`/Users/michael/Desktop/Mike/Projects/cleansia/src/Cleansia.Core.Domain/Users/Employee.cs:294-307`)
  sets `VatNumber = null`, `RegistrationNumber`/`IBAN` to the anonymization marker. So a regeneration
  after a GDPR erasure flips an issued document from "VAT payer, DIČ printed" to "not VAT registered",
  with a redacted IČ.

So: D7 argues the acceptance facts must be retained because *"they are the authority for invoices that
are themselves retained"*, while the retained invoice's own content is neither retained nor stable. The
ADR is scrupulous about the immutability of the *agreement* and indifferent to the immutability of the
*document the agreement authorizes*. For a legal-sufficiency argument that is the wrong way round: what
a supplier pre-agrees to is the issuance of documents in their name, and the platform cannot say what
those documents said.

I am not asking this ADR to fix invoice immutability — that is a genuinely separate decision. I am
asking that **D6.2 stop claiming more than it delivers**, and that the finding be routed. The cheap
version that belongs here: the stamp is accompanied by nothing, so consider whether `EmployeeInvoice`
should also freeze the supplier's `IsVatPayer` at issuance (it is one bool, it is the field
`Q-PAYOUT-03` is about, and CH-L3 shows it is the axis the obligation turns on).

**Collateral, outside my lane, flagged not adjudicated:** `RegenerateInvoicePdf.Command(InvoiceId,
LanguageCode)` has **no ownership term** — the validator checks only that the invoice exists
(`:28-33`) and the handler loads it by id. It is gated `Policy.CanGenerateInvoice`, which resolves
`PhysicalPolicy.AdminOnly` (`PolicyBuilder.cs:92`, frozen at `FrozenPermissionMapTests.cs:82`), so it is
**not** cleaner-reachable today despite sitting on the partner host. Recording it because it is one
policy-map edit away from being an IDOR over other cleaners' invoices, and because D6.2 leans on that
document. Route to the security lane; it is not ADR-0041's to decide.

---

### CH-L9 — D4.3 is fail-**open** and silent, and it reuses an enum whose documented failure posture is fail-**soft**

The brief asks which it is. It is **fail-open**: where no reviewed text exists, the platform keeps
issuing self-billed documents with **no consent capture, no gate, and no record** — which is exactly the
state ADR-0041 exists to end — and it does so *by decision*, indefinitely, for as long as
`Q-SELFBILL-01` is unanswered.

That posture is defensible (it is the same judgement as D5's) but the ADR presents it as if it were the
safe direction — *"The platform never demands agreement to text nobody wrote"* — and never states the
other half: *and therefore issues without agreement to nobody's knowledge*. Two things make it worse
than the ADR's framing:

1. **No operator sees it.** D4.3's *"first-class runtime state"* is first-class in the API **response**
   (`required: false`). It is invisible everywhere else: D6.3's report has no *"jurisdictions with no
   `BusinessSupplied`+ agreement text"* row, there is no startup log, no admin banner. In the report a
   country with no text and a country where nobody has ticked yet look **identical** — 100% "never
   accepted" in both.
2. **The reused enum carries the wrong connotation.** `LegalNoticeReviewStatus`'s own doc comment
   (`/Users/michael/Desktop/Mike/Projects/cleansia/src/Cleansia.Core.Domain/Enums/LegalNoticeReviewStatus.cs:3-8`)
   defines its behaviour as *"only a notice above `NotReviewed` is printed … and **the platform's
   generic fallback prints in its place**."* On the invoice, `NotReviewed` still yields a document with
   *a* notice — fail-soft. On the agreement, `NotReviewed` yields **nothing at all** — fail-open. Same
   type, inverted safety posture. CH-A6 asked whether `BusinessSupplied` means the same thing; the
   sharper question is whether `NotReviewed` *behaves* the same way, and it does not. That is a comment
   the reuse must carry, or a sibling enum, as CH-A6 already concedes is cheap.

**Ask.** D6.3 gains one row: *jurisdictions with no `BusinessSupplied`+ agreement text, with the count
of active cleaners and of invoices issued in each*. That is the number that tells the owner
`Q-SELFBILL-01` is costing something, and without it D4.3 is a silent permanent off-switch.

---

### CH-L10 — D8's conclusion is right and its **evidence sentence is false**; and D7 retains an operator free-text field through erasure that verification #12 does not cover

**D8, checked against the tree rather than accepted.** Its ruling — *do not read `ContractStatus`, do
not treat it as evidence of a contract* — **stands, and I sustain it.** The landmine is live:
`Auditable.Deactivated()`
(`/Users/michael/Desktop/Mike/Projects/cleansia/src/Cleansia.Core.Domain/Common/Auditable.cs:35-42`)
does not touch `ContractStatus`; `GdprDeletionService.cs:242-244` calls `Anonymize()` then
`Deactivated()`; and `TakeOrder.cs:183-190`'s gate is `employee?.ContractStatus == ContractStatus.Approved`
with **no `IsActive` conjunct** — so an erased cleaner still reads `Approved` there. (`PreferredCleanerHoldResolver.cs:55-59`
already conjoins `IsActive` and documents why, and `PreferredCleanerHoldResolverTests.cs:111` pins it —
so the mitigation exists in exactly one of the readers.)

**But D8's supporting sentence is wrong**, and it is the kind of wrong an ADR makes permanent:

> *"Every one of its nine production readers … **all of them spelling it `Approved or Active`** — two
> values used interchangeably, which is itself the evidence."*

Three distinct predicates, not one:

| Predicate | Readers |
|---|---|
| `Approved \|\| Active` | `RequireCompleteProfileAttribute.cs:35`, `NewJobsDigestService.cs:96-97`, `PreferredCleanerHoldResolver.cs:59` |
| `== Approved` **exactly** | `TakeOrder.cs:189`, `StartOrder.cs:69`, `CompleteOrder.cs:153`, `MarkCashCollected.cs:98` |
| `!= Terminated` | `PeriodReminderBackgroundService.cs:83`, `EmployeeRepository.cs:40` |

The true fact is more interesting than the claimed one and points the same way: `ContractStatus.Active`
is accepted by the profile filter and by **no order-lifecycle gate**, so an `Active` cleaner passes the
403 filter and then fails every action with `employee.not_approved`. The value is also on the wire to
three generated clients (`RegistrationCompletionStatus.cs:10`, `EmployeeMappers.cs:22,83,116`,
`GdprExportDto.cs:49`, iOS `RegistrationCompletion.swift:4-9`). An ADR that tells future readers the
nine call sites agree licenses the next author to write `Approved or Active` in a new gate. Fix the
sentence; keep the ruling; ticket 8 is correctly scoped as a separate decision.

**D7's gap.** D7 redacts `IpAddress` and `DeviceLabel` on erasure and retains everything else. It says
nothing about **`ContractReference varchar(200)`** — an operator-typed free-text field (`Q-SELFBILL-05`
asks whether it carries a contract number or a scan id, i.e. it is undefined and free). D6.4 is
careful that the *audit snapshot* is ids-only *"because the subject's data is the subject's PII"*, and
then the persisted column takes whatever an operator types about that subject and survives erasure
untouched. Verification #12 lists `IpAddress`/`DeviceLabel`/`RecordedByUserId` as never-on-a-DTO and
omits `ContractReference`. Either constrain the field (an id into a document store, not free text) or
add it to both the redaction set and verification #12.

---

## What I checked and found sound

Silence is not assent. Each of these I tried to break and could not.

1. **F1 is exactly right.** `UserConsentEntityConfiguration.cs:31-32` is
   `HasIndex(new { UserId, ConsentType }).IsUnique()` — one mutable row per (user, type), no version
   column, and `Regrant` overwrites. The existing table genuinely cannot hold a versioned acceptance.
   Verified at the line cited.
2. **F2 is exactly right and I could not find a mitigating guard.**
   `Features/Gdpr/WithdrawConsent.cs:12` takes a bare `ConsentType`; the only validator rule is
   `IsInEnum()` (`:18`); the handler withdraws whatever type the caller names (`:31-38`). The route is
   live on the partner host at `Web.Partner/Controllers/GdprController.cs:55-63` under
   `Policy.CanWithdrawConsent`. Adding `ConsentType.SelfBilling` really would put a commercial term
   behind a one-field POST. **A1's rejection is sound.**
3. **D2.5's grant mechanics are correctly copied.** `ConsentService.cs:12-34` reads IP and device from
   `IRequestMetadataProvider` with the quoted comment at `:16-17`, and returns `false` when already
   granted (`:27-30`). `IRequestMetadataProvider` is registered in the **shared** config
   (`Cleansia.Config/Repositories/RepositoryExtensions.cs:18`), so it is available on the partner
   mobile host too — the ADR's server-side-metadata requirement is implementable on both surfaces
   without new plumbing.
4. **F3's population claim is verified precisely.** `insert_users_employees.sql:53-111` seeds five
   employees; the `ContractStatus` column value is `1` (Pending) in all five rows
   (`:66,76,86,96,106`), each with a populated `IBAN`. "The database is being dropped, so there is no
   cohort" is indeed false.
5. **The ADR is right that mobile never sends `UpdateEmployee`** — I checked both native trees rather
   than taking it. `rg` over `src/cleansia_android` returns only `partner-mobile-api.json` and an
   unrelated local `UserProfileStore.updateEmployeeId`; over `src/cleansia_ios`, nothing. D5's
   *"dead on mobile"* argument against putting a bool on `UpdateEmployee` is correct **as far as it
   goes** — see CH-L5(b) for what it implies that the ADR does not draw.
6. **Monthly pay periods, hence ~31 days of withholding under A5** — verified,
   `PayPeriodBackgroundService.cs:164-165` (`newStartDate.AddMonths(1).AddDays(-1)`). A5's harm
   argument is quantitatively right.
7. **The `IsProfileComplete()` / `[RequireCompleteProfile]` danger is real and the line numbers are
   exact.** `Employee.cs:323-349`; `RequireCompleteProfileAttribute.cs:32-49` returns a bare 403 with
   no field detail. D5's "never read" rule and verification #1 are the right defence, and I could not
   construct a way for the acceptance to leak into that gate if D5 is followed.
8. **"Nothing records a self-billing agreement today"** — confirmed. The only hit for
   `samofaktur|self.?bill` across `src/` is `MembershipModels.swift:60` (`self.billingInterval`), a
   regex artefact. No column, no enum value, no PDF marking.
9. **D1's "no unique index on the acceptance is a security property"** — I tried to find a
   check-then-act the append-only shape reintroduces and could not. Two concurrent accepts of the same
   version produce two true rows; every read is "latest for (employee, kind)". The reasoning holds.

**Citation drifts found while sampling** (the ADR claims *"every citation verified in the working tree,
2026-08-04"*):

| ADR says | Tree says |
|---|---|
| `UpdateEmployee` validator requires `Consent == true` at `:125-127` | `:132-134`. (Handler grant is `:263`, not `:239-242` — `:239` is the ctor parameter.) |
| `rg -i 'samofaktur\|self.?bill\|…'` over `src/` returns **zero** hits | one hit; conclusion unaffected (item 8) |
| T-0522 *"AC0–AC12 checked"* | `INDEX.md:117` — **AC0–AC15** |
| T-0522 as settled context for this ADR | `INDEX.md:391` records it *"blocked only on Q-PAYOUT-02/03"*; **Q-PAYOUT-03 is still open and `blocking: YES`** — see CH-L3 |
| D8: *"all of them spelling it `Approved or Active`"* | three distinct predicates — see CH-L10 |
| F5/D9: *"`PayPeriodBackgroundService` → `GenerateInvoice`"* | no such call — see CH-L1 |

---

## Summary for the lead

| # | Challenge | Class | Blocking? |
|---|---|---|---|
| CH-L1 | D6.2's stamp is in `GenerateInvoice.Handler`; the monthly issuance path builds the invoice inline at `PayPeriodBackgroundService.cs:328` and never calls it. Every ordinary invoice is stamped null forever; verification #3 certifies it. | **Correctness — the whole control** | **BLOCK** |
| CH-L2 | D6.1 issues *"on the contract's basis"* for the cohort F4 defines as lacking the clause; and verification #10 makes D6.3's invoice number monotonically non-decreasing, so the "closable number" cannot close. B8 freeze misapplied to later-discovered evidence. | Coherence + escalation scope | **BLOCK** (re-frame Q-SELFBILL-02; amend D6.2) |
| CH-L3 | The obligation turns on VAT-payer status; `Q-PAYOUT-03` is open + blocking + unanswered on exactly it; seed data is 60% VAT-registered; `IsVatPayer` is derived live and mutably; D1's version key has no axis for it and a migration freezes the key **now**. | Missing escalation before an irreversible step | **BLOCK the migration**, not the design |
| CH-L4 | D4.5 (*"never blocks"*) is false once D5 blocks: a cleaner is hard-gated on affirming a legal text in a fallback language they may not read. Expected launch case, not an edge. | Correctness + the record's truthfulness | **BLOCK** — split renderable from demandable |
| CH-L5 | D5's gate is on the general profile-save (fires on routine edits, contradicts D5 row 6), has no mobile attachment point (ticket 9 says so), and the `ApproveEmployee` alternative — which meets every criterion D5 states — is never considered. | Alternatives not answered | **BLOCK** |
| CH-L6 | `agreement.version_stale` ticketed under `errors.*` renders as the generic error on partner web and the parity guard is a hand-maintained array; no refetch contract; the accept/submit pair is unsequenced across a version roll. | Correctness + i18n contract | **BLOCK** ticket 7; D3.2 needs a client contract |
| CH-L7 | F5/D9 mis-diagnose tenancy: both invoice paths set the tenant override before the handler runs. D9 prescribes an `IgnoringTenant` read and verification #6 would enforce it — an S8 regression the code does not have today. | **S8 — introduced by the ADR** | **BLOCK** |
| CH-L8 | The stamp binds an immutable acceptance to a document `RegenerateInvoicePdf` rebuilds live from mutable `Employee` fields (incl. VAT status, nulled by `Anonymize()`). D6.2 claims more than it delivers. | Framing + a real finding to route | Non-blocking; narrow D6.2, consider freezing `IsVatPayer` |
| CH-L9 | D4.3 is fail-**open** and invisible to any operator; the reused `LegalNoticeReviewStatus` documents a fail-**soft** posture, so the type carries the wrong connotation. | Operability | Non-blocking; add the D6.3 row |
| CH-L10 | D8's ruling **sustained** (landmine live at `TakeOrder.cs:189`); its evidence sentence is false (three predicates, `Active` accepted by no order gate). D7 retains operator free-text `ContractReference` through erasure; verification #12 omits it. | Accuracy + PII | Non-blocking; fix the sentence, cover the field |

**Bottom line.** The ADR's *shape* survives my lane: a versioned, append-only, entity-not-`ConsentType`
record with a server-owned text is the right object, and F1/F2 are verified exactly — I tried to make
A1 work and it does not. What does not survive is the ADR's account of **where the obligation attaches
and where the design touches the running system**. Three of its structural claims about this codebase
are false — the invoice path (CH-L1), the sweep's tenancy (CH-L7), and the `ContractStatus` readers
(CH-L10) — and two of the three are load-bearing: the first deletes the only control that makes D5's
non-blocking posture defensible, and the second turns a correctness rule into an S8 regression. On top
of that, the ADR settles the *legal-sufficiency* question in two mutually exclusive ways without
noticing (CH-L2) and never asks the question the obligation actually turns on, which is already sitting
in `open.md` marked blocking (CH-L3). Fix CH-L1, CH-L3 and CH-L7 and this becomes a good ADR; ship it as
drafted and its own compliance checklist will certify a design that stamps nothing, reads across
tenants, and calls a permanently-rising number closable.
