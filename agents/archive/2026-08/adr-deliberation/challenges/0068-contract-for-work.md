# ADR-0068 — Architect challenge (batch 9, 2026-09-20)

Every file:line below was opened at `8a7710b0` on `fix/remove-membership-free-trial`. Severity: **H** = the
decision is wrong or unbuildable as written; **M** = a cheaper or more correct shape exists and the ADR did
not answer it; **L** = state it, do not build it. A challenge stands unless defended or conceded.

---

## C1 (H) — The record names a text only one party accepted; the customer's half of the contract has no durable per-order pointer

**Evidence.**
- The lawyer's model: the per-job contract forms between the *customer* and the *cleaner*
  (`cleansia_ramcova_smlouva.txt:46` P043; `Cleansia_VOP.txt:25` P022), and *"Podmínky každého konkrétního
  úklidového výkonu se podpůrně řídí Obchodními podmínkami Operátora pro Zákazníky"* (P043, second
  sentence) — the terms of the job are the **customer** terms.
- D1 creates an **`Employee`-audience** `WorkContract` text the cleaner accepts. D7 says the customer's side
  is "their VOP acceptance at booking" — a *different* document. Two parties, two texts, no row that says
  both assented to one instrument. If the cleaner-facing text differs in substance from the VOP there is no
  consensus on terms; if it does not, the lawyer maintains one text twice, in five languages.
- The customer's half is not even durable per order: `CreateOrder.cs:1076-1079` resolves the in-force
  terms and writes **only** `terms?.Version` into `OrderBookingEvidence.TermsVersionAccepted`
  (`CreateOrder.cs:823`) — a `CustomerActionAudit` payload that the retention sweep **deletes three years
  after the act** (ADR-0062 D5, `adr-0062.md:543-560`), and that is `null` for a signed-in customer who saw
  no box (`adr-0062.md:452-454`). `Order.cs` carries no `LegalDocumentId`/terms column at all
  (`grep -n "Terms\|LegalDocument" src/Cleansia.Core.Domain/Orders/Order.cs` → empty). `UserConsent`
  moves on re-acceptance (`AcceptVersion`). So D5's *"kept for as long as the order is kept, which is
  forever"* is true of the cleaner's row and false of the customer's.

**Fix (cheaper and correct).**
1. The job's governing text is **stamped on the order at booking**: `Orders.WorkContractLegalDocumentId
   varchar(26) NOT NULL FK LegalDocuments (Restrict)` (or `TermsLegalDocumentId` if the lawyer says the VOP
   *is* the instrument), resolved in `CreateOrder` where `:1076` already resolves it — one column, no new
   command, the customer's side of every order is a row forever.
2. The cleaner's acceptance echoes and is compared against **the order's document**, not "the document in
   force today". Consequences that fall out: the midnight-UTC version race D3 spends a paragraph on
   disappears (the text is fixed per order); `contract.version_stale` becomes a lying-client case only; a
   new lawyer version applies to orders booked from its date — which is the legally right behaviour (a
   contract's terms are fixed when the offer is made, not when it is accepted).
3. The text is **customer-audience** (published at `/work-contract` beside `/terms`, referenced by the
   customer's existing tick sentence — the P074 line the ADR defers becomes part of this design, one
   wording change) — which deletes the ADR-0063 D9 departure, the `Employee`-folder widening of the parity
   checker, and C2 below entirely.
4. If the lawyer wants a separate cleaner-only instrument, the same fix applies: the customer must accept
   *that* instrument at booking too. Either way the row must name a text **both** parties accepted.

Overrulable by the lawyer (Q-WC-05 is the same question from the other side); the ADR must ask it, not
default around it.

---

## C2 (H) — The resolver the ADR and the ticket name cannot resolve an `Employee`-audience document

**Evidence.** `LegalDocumentResolver.ResolveInForceAsync(type, countryId)` hard-codes
`LegalDocumentAudience.Customer` (`LegalDocumentResolver.cs:19-20`); `ILegalDocumentResolver.cs:14` has no
audience parameter. D1 (*"`ILegalDocumentResolver.ResolveInForceAsync(WorkContract, order.CustomerAddress.CountryId)`"*)
and T-0777 §3 as written resolve `(Customer, WorkContract)`, find nothing, and every take answers
`legal.document_not_found`. An ADR that claims file:line verification must name the signature change
(an `audience` parameter — five callers: `Register`, `GoogleAuth`, `AppleAuth`, `GrantConsent`,
`GetLegalDocument`, `CreateOrder`) or the overload. With C1 the change is unnecessary.

---

## C3 (H) — The gate is per-order; the admin path it exists for is mostly ungated, and the ADR's own accept command dead-ends on it

**Evidence.**
- `StartOrder` admits the **order** at `Confirmed | OnTheWay` (`StartOrder.cs:90-101`). Two-seat orders
  exist today (`OrderEmployee.cs:24-28`: crew = `ceil(EstimatedTime/120)`). On a two-seat crew, cleaner A
  starts; cleaner B — admin-reassigned, no acceptance — never calls `StartOrder` (the order is already
  `InProgress` → `order.not_confirmed`), and `CompleteOrder` has no contract rule (`CompleteOrder.cs:83-90`).
  B does the work under no contract. The admin swap on a multi-seat crew is the everyday shape of
  `AdminReassignOrder`, not the residual D3 names (take → drop → re-add of the *same* cleaner).
- `InProgress` is offerable (`OrderAvailability.cs:53-54`), so an admin can reassign onto an in-progress job
  (the mid-clean swap). The reassigned cleaner cannot `StartOrder` (status) — ungated — and the ADR's
  `AcceptWorkContract` validator refuses them too: D3 restricts the standalone act to
  *"`Confirmed | OnTheWay` (`StartOrder.cs:101`; `order.not_confirmed`)"*. Banner → sheet → swipe →
  `order.not_confirmed`. A dead end the clients (T-0778 AC3, T-0779 AC3) cannot map.
- `NotifyOnTheWay` is ungated by decision; fine. `CompleteOrder` ungated is not a decision, it is an
  omission.

**Fix — two shapes, the ADR must pick and answer the other.**
- **(a) Keep the admin force, close the holes:** `AcceptWorkContract` admits every
  `OrderAvailability.OfferableStatuses` status (the statuses a seat can be formed at); the same
  `HasAcceptedWorkContractAsync` rule goes on **`CompleteOrder`'s employee chain** after
  `EmployeeIsAssignedToOrderAsync` (`CompleteOrder.cs:85-86`) — the act that ends the work is the last act
  that can still have a contract behind it, and it is the one the reklamace turns on; state the residual
  that remains (a second cleaner who neither starts nor completes). AC7 gains the two-seat case.
- **(b) The cheaper correct shape the ADR never considered:** an admin reassignment becomes an **offer**,
  not a seat — the ADR-0036 preferred hold (`Order.GrantPreferredHold`, the pending-offers surface both
  mobile apps already have, the push, the decline). Every seat is then formed by `TakeOrder`; the ADR's own
  sentence *"an administrator cannot accept a contract on the cleaner's behalf"* becomes structural instead
  of gated. Deleted with it: `AcceptWorkContract`, the `StartOrder` rule, `contract.acceptance_required`
  on three clients, the banner and the `accept` mode of the sheet, `WorkContractAcceptor` (one caller →
  inline per CLAUDE.md §4), three ACs. Trade-off: the admin can no longer force a crew member; the seat sits
  offered until the cleaner acts (the hold's 12-hour lapse and its `NotHeldFrom … || AssignedEmployees.Any()`
  term at `OrderVisibility.cs:79` would need a look for a partially-crewed order). **Owner question**, not a
  default — the ADR must at least put it in the alternatives table with this trade-off.

---

## C4 (M) — Echo the served text row, not `(document id, language)`; the `Language` member breaks the one-chain invariant

**Evidence.**
- The precedent the ADR cites for the echo, ADR-0041 D3 (`adr-0041.md:1050-1053`), echoes the **text row**
  (`agreementVersionTextId`), because *"a Czech cleaner who read the Czech body and a Ukrainian cleaner who
  read the Ukrainian body of the same version agreed to two different strings"* (`:1003-1005`). The draft
  echoes the document id plus a client `Language` and lets the server pick the text again
  (`TextForOrFallback`) — a weaker record than the one it cites. `LegalDocumentText` already has an id and a
  `ContentHash` (`LegalDocumentText.cs:15-33`): one FK gives document, version, language **and** hash by
  join; ADR-0041 D3.3's "silent edit is a one-query detection" comes free.
- `Language` validated by `LanguageValidator` (`LanguageValidator.cs:6-18`) is a **second `RuleFor`** beside
  the one ordered chain that `TakeOrder.cs:40-47` says must stay alone (ADR-0037 D6; a held and a missing
  order must answer with the same error count). TC-TAKE-ONE-ERROR breaks on a bad language code.
- The seeder can add a missing language to an in-force document at a host start (ADR-0063 D3); a preview
  that served `en` as the fallback and a take after the deploy would store `Language = uk` for a cleaner who
  read English.

**Fix.** `TakeOrder.Command(string OrderId, string? AcceptedWorkContractTextId)`; the row stores
`LegalDocumentTextId` (FK Restrict) — drop `LegalDocumentId`, `DocumentVersion`, `Language` from the row
(or keep `DocumentVersion` for the `UserConsent` twin argument, but say so); the preview returns the text
id; the validator's last rule compares it with the text of the order's document (C1) in that language.
One member, no second chain, exact-row provenance.

---

## C5 (M) — Bind the acceptance to the seat, and key the read on the acceptance

**Evidence.** D3's residual (*"the seat has no timestamp to compare against"*) and D2's *"two rows are two
contracts"* both exist because the row carries `(OrderId, EmployeeId)` and nothing that names the
assignment. Every notifier already captures the assignment id before the hard delete —
`AdminReassignOrder.cs:105`, `DropOrder.cs:120-121`, `RequestCover.cs:132`, `TakeOrder.cs:306`. And
`GetWorkContract.Query(OrderId, EmployeeId, …)` can only ever render the **newest** of the two rows the ADR
says both exist; the dropped contract is unreadable by either party. On the partner host the query also
puts an `EmployeeId` on the wire that the server then ignores or checks — S1's shape.

**Fix.** `OrderEmployeeId varchar(26) NOT NULL` on the row (bare scalar, no FK — it outlives the seat, the
`EmployeeId` argument at `EmployeeActionAudit.cs:34-40`); the gate asks *"an acceptance for the caller's
current seat id"* — exact, and the residual is gone; `GetWorkContract.Query(AcceptanceId)` with access
derived from the row (customer host: the row's order is the caller's; partner host: the row's employee is
the caller); `WorkContractAcceptanceDto` carries `Id`. The `AcceptWorkContract` idempotency check becomes
*"a row for this seat"*.

---

## C6 (M) — A factual claim in D3 is false: the cover swap writes no employee audit row

**Evidence.** D3: *"`DropOrder`, the cover swap (`TakeOrder.cs:295-309`) and `RejectEmployee` delete the
seat and write their own `EmployeeActionAudit` rows"*. `grep -rn "EmployeeActionAudit.Create(" src/Cleansia.Core.AppServices`
→ `DropOrder.cs:150`, `RequestCover.cs:123` only. `TakeOrder` has no audit repository; `RejectEmployee` and
`AdminReassignOrder` are pipeline-audited into `AdminActionAudit`. A displaced cleaner's record is
*accepted at 09:00 / cover requested at 09:20* and the displacement — the end of their contract — is
recorded on nothing employee-side.

**Fix.** Correct the sentence. Optionally (one enum member, one `Add` before `TakeOrder.cs:331`):
`EmployeeAuditAction.CoverDisplaced` for `coveredEmployeeId` — the taker's commit already carries the audit
repository the ADR adds. With C5 the displaced seat id is on the taker's row's predecessor anyway.

---

## C7 (M) — The ticket records the wrong device id

**Evidence.** T-0777 §3: *"`IRequestMetadataProvider` (`IpAddress`, `DeviceLabel`, `DeviceId` — the signed
claim on a mobile session, per ADR-0062 D1)"*. `IRequestMetadataProvider.DeviceId` is the **`X-Device-Id`
header** (`IRequestMetadataProvider.cs:20-26`); the claim is
`userSessionProvider.GetTypedUserClaim(AuthExtensions.DeviceIdClaimType)` (`AuditEntryFactory.cs:113-117`).
A signed-in act recording the header is exactly what ADR-0062 D1 forbids (`adr-0062.md:154-157`).

**Fix.** The acceptor takes `IUserSessionProvider` (add it to its CRC collaborators) and records the claim
or nothing; the ticket names `AuditEntryFactory.cs:115-117` as the idiom.

---

## C8 (M) — The trio and its retention machinery are not proportional to what they prove

**Evidence.** For three nullable columns the ADR adds: a 10th per-company catalogue key, a
`DataRetentionBackgroundService` task, `PseudonymiseExpiredAsync`, a `(TenantId, AcceptedOn)` index, a
two-company Postgres test, an erasure walk and a commit-throw test. `EmployeeActionAudit` refuses exactly
these columns (*"nothing reads them and ADR-0045 D13 refuses collection just in case"*,
`EmployeeActionAudit.cs:27-30`) and the ADR does not say why this row is different. What corroborates
*"the cleaner's own device did this"* is the signed `DeviceId` claim (token-bound, revocation-bound —
ADR-0062 D1) plus `ClientAudience`; an IP and a UA string add little a court would weigh against a JWT-bound
device id.

**Fix.** Store `DeviceId` (claim) + `ClientAudience` only. Then: no catalogue key, no sweep, no index; the
cleaner's erasure blanks one column in the walk it already has. If the lawyer wants the IP, keep the trio
and the sweep — but then say why ADR-0045 D13 does not apply, and reuse the §629 reasoning by reference
rather than a second knob for the same legal period.

---

## C9 (M) — The admin, whose act creates the pending state, gets no surface for it

**Evidence.** T-0781 NOT list: *"No admin web work … a line there is a finding"*. The only path to a
crew member without an acceptance is `AdminReassignOrder`; the job cannot start until that cleaner accepts;
the admin learns it from a phone call. `OrderItem.WorkContractAcceptances` is already on the DTO the admin
detail reads.

**Fix.** One line per crew member on the admin order detail — *accepted {date}* / *pending* — in T-0781
or a T-0783. The customer's "no pending line" reasoning is right; it does not transfer to the operator.

---

## C10 (L) — Guest orders

`LookupOrder` is anonymous (`Cleansia.Web.Customer/Controllers/OrderController.cs:43-64`); if it returns
`OrderItem` the guest sees the acceptance line and has no route to the text (D4's customer read requires
`OrderExistsForCallerAsync`). State the guest behaviour — consistent with how a guest gets a receipt — or
route the read through the guest access path.

## C11 (L) — The facts builder must not widen the seat-race aggregate

`TakeOrder.Handler` loads `AssignedEmployees, OrderStatusHistory, Currency, CustomerAddress, User`
(`TakeOrder.cs:261-268`) — no `SelectedServices/Packages/Extras` with names. The acceptor loads its own
`AsNoTracking` projection for the facts; the ticket should say so, or a developer adds four includes to
the handler whose commit is the seat arbiter.

## C12 (L) — Two things the snapshot should state

- `Address.Anonymize()` blanks `City` and `ZipCode` at the source (`Address.cs:57-59`);
  `locationApproximate` is a permanent coarse copy of a field the platform's own erasure treats as
  personal. Defensible as the *místo plnění* term in coarse form — say it, and name the lawyer/DPO
  acceptance, rather than "none of them is PII".
- `{{currency}}` is filled "from the order's market"; the facts carry `Order.Currency.Code`. Fill the
  placeholder from the order's currency so the text and the facts cannot disagree.

## C13 (L) — Concurrent double accept writes two rows

No unique index; two concurrent `AcceptWorkContract` requests both read "no row" and both insert.
ADR-0041 (`adr-0041.md:726`) accepted the same; state it rather than claim the handler closes the
double-tap. With C5 the arbiter, if wanted, is `UNIQUE (OrderEmployeeId)` — one contract per seat — which
is also the legal truth.

## C14 (L) — The residual the echo does not close

The echo proves *which text*, not that the preview for **this** job was fetched by **this** session; a
client that caches the id from job A and takes job B without opening the sheet records a perfect row.
A per-preview HMAC over `(orderId, employeeId, textId, issuedAt)` echoed on the take would prove the fetch
with no storage (~30 lines). Proportionality says state the limitation, not build it — but state it.

## C15 (L) — T-0777 is not an L, it is the batch's critical path

Items 10-14 (incident file, export, erasure, retention, archive) touch no wire and can run beside the client
lanes; only the document + row + acts + gate + regen + re-dump must run "first and alone". Split, or every
client lane waits on the archive stream.

## C16 (L) — PDF deferral stands; make the "nothing is missing" claim checkable

The incident file prints version/id, not the text; the text with its SHA-256 is on
`AdminGetLegalDocument`. Say in D4 that a dispute bundle = incident file + that admin read, so the
deferral is a stated procedure and not an assertion.

## C17 (L) — AC testability

- T-0778 AC1 *"the lists reload as today"* — pin *"the facade's reload is called once after success"*.
- T-0777 AC7 must add the two-seat crew and the `InProgress` reassignment (C3) or it tests the easy case only.
- T-0777 AC9 as written cannot assert the dropped contract of a re-take (C5).
- T-0779/T-0780 AC2 *"the slider resets"* — pin the view-model state, not the composable.

---

## Summary for the lead

C1–C3 must be answered before the ADR is accepted: C1 changes the document's audience and adds one column
to `Order`; C2 is a build-breaking citation; C3 is the admin path's correctness. C4, C5, C7 are one-member
changes that make the record stronger and the code smaller. C8 and C9 are proportionality calls. The rest
is wording.
