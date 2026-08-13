# ADR-0034 — Challenge (security lane: D6 / D8 / D9)

**Challenger:** security · **Mode:** challenger · **Date:** 2026-08-02
**Lane:** D6 (no column-level encryption in v1) · D8 (masked read contract + audited admin reveal) · D9 (no PAN column)
**Method:** every claim below is a read at a cited `file:line` in the working tree. No code executed.
Gate 0 applied: nothing is called a vulnerability until the exploit trace is complete and no existing
guard already stops it. Several of my starting hypotheses died at the guard check and are recorded at
the bottom, not dressed up as findings.

**Verdict from this lane: 3 BLOCKING (CH-S1, CH-S2, CH-S4), 3 must-fix-before-tickets (CH-S3, CH-S5,
CH-S6), 1 fatal out-of-lane (CH-S7), 1 spec gap (CH-S8).** D6's *conclusion* survives — and survives
better than the author argued it. D8's *conclusion* survives but its stated premise, its audit
mechanism, and its anonymization clause do not. D9's *security* half is right; its *scope* half is a
product decision taken without escalation.

---

### CH-S1 — The single factual claim that D6 and D8 both rest on is false: **no payout identifier has ever ridden a paged list DTO.** `EmployeeListItem.cs:52` is not `EmployeeListItem`.

**The hole.** The ADR states, in the context table, in D8.1, in D1's reason (i), and in Consequences,
that `Iban` ships in a **paged list** response, citing `EmployeeItem.cs:27` and `EmployeeListItem.cs:52`.
The *file:line* citations are accurate. The *inference drawn from them* is not. `EmployeeListItem.cs`
declares four records, and line 52 is inside the fourth:

- `EmployeeListItem.cs:6-16` — `EmployeeListItem`. **No `Iban`.**
- `EmployeeListItem.cs:18-30` — `AdminEmployeeListItem`. **No `Iban`.**
- `EmployeeListItem.cs:32-69` — **`AdminEmployeeDetail`**, and `:52` is *its* `string? Iban`.

Trace both `Iban`-carrying DTOs to their endpoints:

| DTO | Mapper | Handler | Endpoint | Shape |
|---|---|---|---|---|
| `EmployeeItem` (`EmployeeItem.cs:27`) | `EmployeeMappers.cs:41-74` (`Iban` at `:61`) | `GetCurrentEmployeeDetail.cs:32-36` — resolves the subject from `userSessionProvider.GetUserEmail()` at `:34` | `Web.Mobile.Partner/Controllers/EmployeeController.cs:29-33`, `Policy.CanGetCurrentEmployee` | **self-read, single resource** |
| `AdminEmployeeDetail` (`EmployeeListItem.cs:52`) | `EmployeeMappers.cs:93-139` (`Iban` at `:115`) | `GetEmployeeDetail.cs:33-46` | `Web.Admin/Controllers/AdminEmployeeController.cs:54-60`, `Policy.CanViewPagedEmployee` = `PhysicalPolicy.AdminOnly` (`PolicyBuilder.cs:68`) | **admin single-resource-by-id** |

And the actual paged list: `GetPagedEmployees.cs:22-47` returns `PagedData<AdminEmployeeListItem>` via
`MapToAdminDto` (`EmployeeMappers.cs:76-91`) — **which does not map `Iban` at all**. Confirmed on the
wire at `AdminEmployeeController.cs:18`, `ProducesResponseType(typeof(PagedData<AdminEmployeeListItem>))`.

**Why it matters — three separate consequences, all load-bearing:**

1. **D6's counterweight evaporates.** D6 concedes encryption and says (lines 309-311) *"What we do
   instead… is aimed at the exposure that is actually real (D8): the read contract, not the disk.
   Today's genuine leak is `EmployeeListItem.cs:52` shipping the account identifier in a paged list
   response — which no amount of at-rest encryption would have prevented."* That trade is
   "we decline control X because we are buying bigger control Y." **Control Y is buying back a leak
   that does not exist.** The ADR's own framing therefore does not establish that the alternative is
   "not weaker" — it establishes nothing, because the compared exposure was misread.
2. **D1's reason (i) for a child table is misstated.** D1 line 117-120 says the child entity is what
   makes "never on a list DTO" *structural* rather than remembered, because today `Iban` rides
   `EmployeeListItem` "*because* it is a property of `Employee` and the mapper flattens everything."
   The mappers do **not** flatten everything: `MapToDto` (`:26-39`) and `MapToAdminDto` (`:76-91`) are
   hand-written positional constructions that each *chose* to omit `Iban` while `MapToEmployeeItem`
   and `MapToAdminDetailDto` *chose* to include it. The existing code already honours the rule the
   child table is supposed to enforce. (That is the db lane's to adjudicate — but the evidence is mine
   and I am putting it on the record.)
3. **Reviewer-verification item 9 is a no-op.** It instructs a reviewer to confirm *"`Iban` is gone from
   `EmployeeListItem` and `EmployeeItem`."* The `EmployeeListItem` half passes on day zero without
   anyone doing anything, while `AdminEmployeeDetail.Iban` — the field that actually needs removing —
   is not named in the checklist. A reviewer following the ADR literally green-lights the leak.

**What I want changed.**
- Rewrite the context table row, D6 lines 309-311, D1 reason (i), D8.1, Consequences, and
  Reviewer-verification item 9 to name **`AdminEmployeeDetail`** (`EmployeeListItem.cs:52`, mapper
  `EmployeeMappers.cs:115`, handler `GetEmployeeDetail.cs:45`, route `AdminEmployeeController.cs:54`)
  and **`EmployeeItem`** (`:27`, mapper `:61`, handler `GetCurrentEmployeeDetail.cs:35`).
- Delete every use of the phrase "paged list" in connection with the IBAN.
- **State the exposure that is actually there**, because it is real and D8 does close it, by accident:
  the *unmasked* account identifier is returned by an **enumerable resource-by-id route**
  (`GET admin/employee/details/{employeeId}`) gated by **the same policy that grants the id list**
  (`Policy.CanViewPagedEmployee` on both `:17` and `:55`), **with no masking and no reveal record**.
  That is the finding D8 should be justified by. Justify it by that, and D8 stands on its own feet.
- D6 must then re-argue its trade against the *corrected* exposure, or concede that the "we are buying
  something bigger instead" leg is gone and let D6 rest on its remaining legs (see CH-S6 — it can).

---

### CH-S2 — D8.4's audited admin reveal **cannot be built as specified**: `AdminMutationGate` audits only requests whose type name ends `Command`. A reveal is a read, and would produce **no audit row at all**.

**The hole.** D8.4: *"An admin viewing the unmasked value is an explicit reveal action that writes an
audit entry, per ADR-0012 D4.1 (ids, not the PII — `AdminUpdateEmployee.cs:101` is the precedent)."*
The generic capture engine is:

```csharp
// AdminMutationGate.cs:19-24
return descriptor.Audited
    && request.GetType().Name.EndsWith(CommandSuffix, StringComparison.Ordinal)   // CommandSuffix = "Command"
    && userSessionProvider.GetTypedUserClaim(ClaimTypes.Role)?.Value == UserProfile.Administrator.ToString();
```

`AuditLogBehavior.cs:18-19` states it in prose: *"Queries and non-admin mutations produce no row."*
The cited precedent works **only because it is a Command**: `AdminUpdateEmployee.Command`
(`AdminUpdateEmployee.cs:92-97`) ends in `Command`, so the gate fires; `:101-103` is just the comment
explaining the ids-only snapshot.

So a `GetPayoutDetails.Query` / `RevealPayoutDetails.Query` is silently unaudited. The developer has
three ways out and **all three break something the ADR relies on**:
- **Name a read `…Command`.** It would then also acquire `UnitOfWorkPipelineBehavior` semantics and
  violate the house rule at `CLAUDE.md` ("Queries never modify data; Commands never return
  collections") — and, worse, would be a lie the next reader has to decode.
- **Hand-write an audit insert in the query handler.** Directly contrary to ADR-0012's stated
  mechanism ("captured generically by `AuditLogBehavior` — you write no audit code",
  `security-rules.md` S2) and to the atomicity property: `AuditLogBehavior.cs:10-16` gets atomicity by
  riding the UoW's single `SaveChangesAsync`. A **query pipeline has no commit to ride**, so a
  hand-rolled reveal-audit is a second, separate, non-atomic write — precisely the
  "best-effort *success*-audit" shape S2 names as an ADR-0012 violation.
- **Make the reveal a genuine mutation** (e.g. it stamps `PayoutDetails.LastRevealedAt`). That works
  and is atomic — but it is a design decision with a schema consequence, and the ADR does not make it.

**Why it matters.** D8.4 is the *entire* accountability control D6 leans on when it declines
encryption. An unaudited reveal on an unmasked financial identifier is strictly worse than today,
because today's `AdminEmployeeDetail` at least ships the value inside an action a reviewer knows
about; D8 as written creates a dedicated high-value read and attaches an audit obligation that the
engine will not honour. Nobody would notice: there is no test that asserts a row *appears*, only
`EmployeeUserAuditCoverageTests.cs:301` asserting the value does not appear *in* a row.

**What I want changed.** D8.4 must name the mechanism, not the intent. Pick one and write it into the
ADR (my order of preference):
1. **The reveal is a `Command`** that mutates (`RevealPayoutDetailsCommand` stamping
   `LastRevealedAt`/`RevealCount` on the record), returns the unmasked value, and is therefore audited
   atomically by the existing engine with zero new audit code. Add the field to D5's table. This also
   gives CH-S3 a natural counter.
2. Extend `AdminMutationGate` to a declared read-audit set — a **superseding amendment to ADR-0012**,
   not a side-effect of ADR-0034, and it still owes an answer on atomicity for a query pipeline.
3. Drop D8.4 and say plainly that admin reveals are not audited — which I would then block, because
   D6 cites it as compensating control.

Whichever is chosen, add a **Reviewer-verification item**: a test asserting an `AdminActionAudit` row
**exists** after an admin reveal and that the row's payload contains ids only.

---

### CH-S3 — The reveal endpoint is **unrate-limited by construction**, and no existing guard covers it. An audited-but-unlimited reveal *records* bulk exfiltration instead of *stopping* it. S5 is engaged and the ADR never names it.

**The hole.** The ADR walks D1-D10 and cites S3, S6, S7 (in D9), and S8. **S5 appears nowhere.**
Checked whether an existing guard saves it — it does not:

- `AdminEmployeeController.cs` carries **zero** `[EnableRateLimiting]` on any of its six actions
  (`:16, :28, :41, :54, :67, :80`) — including `GET details/{employeeId}` at `:54`, the route that
  ships the unmasked IBAN today.
- `RateLimitCoverageGuardTests.cs` is the repo's S5 structural guard, and it **excludes this surface
  twice**: `AdminEmployeeController` is not in `MoneyAndSideEffectControllers` (`:29-93`), and the
  test only asserts `MutatingMethods = { "POST", "PUT", "DELETE", "PATCH" }` (`:95`), with the
  explicit comment at `:26-28`: *"Read/list actions (GET) are deliberately NOT asserted — reads carry
  no window by convention."*

So the convention that protects the rest of the platform is exactly wrong for this one route. A
compromised or malicious admin session runs `GET admin/employee/get-paged` once for the id list
(`AdminEmployeeController.cs:16`, same `CanViewPagedEmployee` policy), then N reveals, and walks the
entire cleaner payout book at wire speed. The audit trail faithfully logs all N — after the fact.

**Why it matters more here than on a normal read.** D8's masking is what makes the bulk read
*possible to control at all*. Masking converts "one query returns everything" into "N deliberate
reveals" — which is only a control if N is bounded. Unbounded, masking is a speed bump that costs the
adversary one loop and costs us the illusion of a control.

**What I want changed.** D8 gains a numbered clause: *the reveal route carries a per-admin
`[EnableRateLimiting]` window* (the `"interactive"` policy is partitioned per JWT `sub` per
`security-rules.md` S5 / ADR-0003 — reuse it, do not hand-roll), and **`AdminEmployeeController` is
added to `RateLimitCoverageGuardTests.MoneyAndSideEffectControllers`**. If the reveal is modelled as a
Command per CH-S2 option 1, the existing guard covers it automatically and both findings close with
one decision — which is the strongest argument for that option.

---

### CH-S4 — **BLOCKING.** D8.5's anonymization clause is a silent no-op as designed. D1 converts a structurally-unmissable GDPR erasure into a load-order-dependent one, and the bank account survives the erasure request in plaintext.

This is the most serious thing in my lane. It is **latent, not live** — ADR-0034 is not implemented —
but it is a defect the ADR *creates*, and the ADR's own verification would not catch it.

**Today (safe by construction).** `Employee.Anonymize()` at `Employee.cs:257-267` sets
`IBAN = AnonymizationMarker.Value` on a **column of the row that is already loaded**. It cannot be
missed: if you have the `Employee`, you have the field.

**After D1 (unsafe by construction).** D8.5 says *"`Employee.Anonymize()` clears the whole payout
record (T-0518 AC6)."* `PayoutDetails` is now a **navigation property**. Trace the only production
caller:

```
GdprDeletionService.cs:43-45
    var user = await userRepository.GetQueryable()
        .Include(u => u.Employee).ThenInclude(e => e!.Address)     // ← Address only
        .Include(u => u.Cart)
        …
GdprDeletionService.cs:235
    user.Employee.Anonymize();                                     // ← PayoutDetails is null here
```

And there is **no lazy loading to save it**: `rg "UseLazyLoadingProxies|ILazyLoader"` across `src/`
returns **zero hits**, and `DbContextBindingExtensions.cs:63` registers the context with
`options.UseNpgsql(dataSource)` and nothing else. So `user.Employee.PayoutDetails` is `null`, the
domain method's `PayoutDetails?.Clear()` (or equivalent null-guard — a domain method has no other
option) does nothing, `SaveChanges` succeeds, and the erasure returns **success**.

**The exploit trace, end to end.** A cleaner exercises the right to erasure → `GdprDeletionService`
anonymizes `User`, `Employee`, `Address`, orders, disputes, devices, consents, documents → the
`EmployeePayoutDetails` row is **untouched**: `AccountNumber`, `BankCode`, `Iban`, `Swift`,
`HolderName` (the beneficiary's real legal name, D5 — *"the beneficiary name as the bank knows it"*,
i.e. possibly a name the platform anonymized nowhere else), and `LegacyRawValue`. All **plaintext**
(D6). Orphaned to an anonymized user, retained indefinitely, with no code path that will ever look at
it again — which is exactly why nobody will notice.

**Why D1 makes this worse than a normal missed-Include.** The child entity is chosen partly *for*
lifecycle separation (D1 reason 2). Lifecycle separation is precisely what makes "clear it when the
parent is anonymized" a **cross-aggregate obligation**, and the ADR discharges that obligation with a
method **on the parent aggregate** that cannot see the child unless someone remembered an `Include`
three layers away in a service the ADR never cites.

**The ADR's own verification would pass while production fails.** Item 10 asks for a test that
`Employee.Anonymize()` clears the record. A unit test constructs an `Employee` in memory with
`PayoutDetails` populated — nav present, method works, **green**. The failure only exists when the
object graph comes from `GdprDeletionService`'s query.

**What I want changed (non-negotiable for this lane):**
1. **Erasure must not depend on a navigation being loaded.** Delete/clear the payout row through a
   **set-based, id-keyed write** owned by `GdprDeletionService` — `IEmployeePayoutDetailsRepository`
   removing by `EmployeeId`, or an `ExecuteDeleteAsync` on `(TenantId, EmployeeId)` — so the operation
   is correct regardless of what the caller Included. Keep `Employee.Anonymize()` for the columns that
   remain on the row.
2. The ADR states this explicitly in **D8.5**, not as an AC buried in T-0518.
3. **Reviewer-verification item 10 is rewritten to require an integration test** that loads the user
   through `GdprDeletionService`'s **actual** query shape (`Cleansia.IntegrationTests` — the repo
   already runs Postgres-backed tests, e.g. `AuditLogBehaviorPostgresTests.cs`) and asserts **zero**
   `EmployeePayoutDetails` rows for the erased employee. An in-memory unit test does not discharge
   this and the ADR must say so.
4. Same treatment for `LegacyRawValue`: D7 makes it *"write-once by the backfill script, never written
   by application code"* — it must also be **erased by the erasure path**, and its scheduled drop
   ticket is not a substitute (a cleaner may be erased before the campaign closes).

---

### CH-S5 — D9's "no PAN. Ever." is an **unenforced invariant against D9's own migration path**, and D9 also settles a product-scope question it simultaneously calls the owner's.

**Part A — the invariant is unenforced, and the migration is the vector.**

D9 is correct that no *field* accepts a PAN. But a PAN can already be **in the data**, and D7 copies it
forward into a new, longer-lived, plaintext column:

- Today's server validator is `ValidationExtensions.cs:122-130`:
  `Cascade(Stop).NotEmpty().Length(15, 34)`. **A 16-digit Visa/Mastercard PAN is 16 characters and
  passes. A 15-digit Amex passes.**
- The Android client actively *helps* it through:
  `BankSectionViewModel.kt:74-76` — `it.copy(iban = v.uppercase().filter { ch -> ch.isLetterOrDigit() })`.
  A PAN typed as `4111 1111 1111 1111` is normalized to `4111111111111111` (16 chars) and sent.
- The owner's own phrasing — *"Bank Account, **Card number** and what else needed to make a payment"* —
  is direct evidence that **a cleaner may reasonably believe a card number belongs in this field**.
  This is not a contrived adversary; it is the product's own language.
- D7's classifier then does exactly the wrong thing: a PAN fails mod-97 (class 1), is not
  CZ/SK-shaped (class 2), so it lands in **class 3 → `LegacyRawValue` = the original string
  verbatim**, `varchar(50)`, **plaintext per D6**, in a table whose whole purpose is payout data.
- D7's dry-run reports **class counts**, not content classes (D7 "Operational shape" / T-0518 AC9), so
  the migration would move card numbers into the new table and report only *"class 3: N"*.
- And D7's reconfirmation prompt would then **echo it back**: *"we have `4111111111111111` on file —
  please re-enter it."*

**Why it matters.** D9's stated reason 1 is that a PAN drags the platform, its **backups** and its
**logs** into PCI scope. If a PAN is already sitting in `Employee.IBAN`, that consequence is *already
running*, and the ADR — which is the artifact that discovered it — would knowingly propagate it into a
new column while asserting "no PAN, ever."

**What I want changed.**
- **D7's backfill classifier gains a class -1: a PAN-shaped value** (13-19 digits after stripping
  separators **and** passing the **Luhn** check — a two-line, standard, unambiguous test). Class -1 is
  **not copied to `LegacyRawValue`**; the payout record is created with `Status = NeedsReconfirmation`
  and **no legacy value**, and the prompt says *"we cannot use what we have on file"* without echoing
  it. The dry-run reports the class -1 count separately so the owner learns whether this is real.
- **T-0518 gains an AC to null the source column** for class -1 rows (do not leave the PAN in
  `Employee.IBAN` behind the migration).
- **D9 gains an enforcement clause, not just a prohibition:** `IPayoutDetailsValidator` (D4) **rejects
  a Luhn-valid 13-19-digit value on every write path** with a distinct key
  (`validation.payout.looks_like_card`), so the invariant is a runtime guard rather than a sentence in
  an ADR. Add it to Reviewer-verification item 11, which today only inspects *names and shapes* of
  fields — a check that cannot catch data.
- **T-0521 (mobile)**: the CZ form's numeric fields must not silently accept 16 digits into
  `AccountNumber` (D5's `varchar(10)` + mod-11 will reject it server-side — good — but the client must
  say *why*, or the cleaner retries into `LegacyRawValue` territory).

**Part B — the scope decision is the owner's, and the ADR says so and then makes it anyway.**

Owner, verbatim: *"…like Bank Account, **Card number** and what else needed to make a payment to the
employee."* D9 answers a **storage** question ("no PAN column") and then, in the same decision, answers
a **product** question: card payout is *"a separate epic… explicitly out of scope."*

- The storage half **needs no owner sign-off and I back it unconditionally.** Security outranks
  product convenience (`security-rules.md` preamble: *security > correctness > cleanliness >
  consistency*), and D9's reason 2 is decisive on its own: you do not push a payout to a PAN; a card
  payout is a network payout to a tokenised destination and what you store is an **id**. Even a
  "yes, cards" answer produces `ProviderAccountRef`, not a PAN column. **This part of D9 is right and
  should not be softened.**
- The scope half is not the architect's. The ADR itself concedes it at line 402-403: *"a business
  decision for the owner, several orders of magnitude larger than a payout field."* Naming a decision
  as the owner's and then taking it in the same paragraph is the failure mode `deliberation.md` step 3
  calls **ESCALATE**. And `questions/open.md` shows the discipline is live in this exact domain —
  Q-PAYOUT-01/02/03 are open, two of them `blocking: yes` — with **no card/payout-rail question among
  them**.

**What I want changed.** D9 splits into two paragraphs and one escalation:
- *D9a (decided, security):* no PAN column, ever; a card destination is an **id** in
  `ProviderAccountRef`; enforced by the validator per Part A. **Not negotiable, no escalation needed.**
- *D9b (escalated):* a new **`Q-PAYOUT-04`** in `agents/archive/2026-08/backlog/questions/open.md` — *"Did 'Card number'
  mean (a) cleaners are paid **to a card**, or (b) it was one example of 'the identifiers needed to pay
  someone'? (a) requires a PSP payout rail (Stripe Connect Express or equivalent), per-cleaner KYC and
  a webhook-driven account lifecycle — a separate epic. (b) is what ADR-0034 builds. **This does not
  block T-0518-T-0521** (D9's `ProviderAccountRef` + `Scheme = ProviderPayoutToken` costs zero
  migrations either way — D10), but it decides whether the payout epic is v1 or later."*
  Marking it `blocking: no` is honest and costs the ADR nothing — which is exactly why there is no
  excuse for not filing it.

---

### CH-S6 — D6's threat model is incomplete in a way that engages its own reversal trigger (iii): a **direct `psql` reader that is not the application is provisioned into the infrastructure today**. Separately, I can narrow the verification burden the ADR hands T-0518.

**The hole.** D6 reason 1: *"Application-level encryption defends against stolen database files and
backups… It does not defend against the application, which is the component that reads and renders the
value."* Both sentences are true and neither is complete. The threat column encryption *uniquely*
addresses is **a principal holding a live DB connection who is not the application** — and the Bicep
provisions exactly that:

```
deploy/bicep/modules/postgres.bicep:153-161
// Firewall: the owner/admin public IP, for the EF-bundle migration apply + manual psql access.
resource allowAdminIp '…/firewallRules@2024-08-01' = if (publicNetworkAccess == 'Enabled') { … }
```

active whenever `publicNetworkAccess == 'Enabled'`, which `main.bicep:347` sets as
`privateNetworkingEnabled ? 'Disabled' : 'Enabled'`. D6's reversal trigger (iii) is *"an external
processor or BI tool gains direct DB access."* A named human with `psql` and the admin credential is
the same access class, and it is not a future trigger — it is the current provisioning intent.

**I am NOT asking for encryption.** Gate 0: I traced this and it does not overturn D6. The decisive
argument stands and I state it in full under "found sound" below. But the ADR's threat-model sentence
is doing work it cannot support, and the reversal-trigger list is calibrated against a future that has
partly already happened.

**What I want changed (small, cheap, and it makes D6 more defensible, not less):**
- Rewrite D6 reason 1 to name the threat column encryption *does* cover — *"a principal with a direct
  DB session who is not the application"* — and then answer it honestly: *today that principal is the
  owner, who is also the person executing the transfers, so the control would gate them from data they
  need anyway.*
- Reword trigger (iii) to *"**a non-owner** principal (external processor, BI tool, contractor,
  support vendor) gains direct DB access, or `publicNetworkAccess` remains `Enabled` once a second
  operator exists."*
- Trigger (ii) — *"a second tenant/franchise goes live"* — should be marked **latent multi-tenant
  risk**, and D8's read contract should be the thing that carries it, since the tenancy filter already
  scopes reads (`Employee : Auditable, ITenantEntity`, `Employee.cs:11`) and the new record inherits it
  (D1). That is a correct posture; it is just not written down as such.

**Verification narrowed (a gift to T-0518, not a challenge).** The ADR flags *"Azure Database for
PostgreSQL Flexible Server encrypts at rest… **read of product documentation; not verified against this
repo's Bicep**"* as a T-0518 duty. I verified the Bicep half: `postgres.bicep:81` declares
`Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01` with **no `dataEncryption` block** — i.e. the
platform default (service-managed keys), **not** customer-managed keys, and there is no ARM property
on this resource type that disables encryption at rest. So T-0518's remaining duty is narrower and
should be restated as: *"confirm DEV/PROD were provisioned from this Bicep (not hand-created), and
record that the posture is **service-managed, not CMK** — so key custody sits with Microsoft, which is
itself a fact the reversal triggers should reference."*

---

### CH-S7 — **OUT OF LANE, FATAL.** D7's completeness-gate rewiring, implemented exactly as written, returns **403 to every cleaner on the entire partner surface** — the precise outage D7 exists to prevent. Same root cause as CH-S4.

I am not the D7 challenger. I found this while tracing CH-S4's missing-`Include` class and my brief
says to raise a fatal finding outside my lane. **This should block on its own.**

D7: *"`Employee.cs:283` changes from `!string.IsNullOrEmpty(IBAN)` to **`PayoutDetails is not null`** —
satisfied by *every* migrated row, including parked ones. **No cleaner loses job-board access on
migration day.** This is the single most important property of the migration."*

That property holds **only if the navigation is loaded**, and the gate's query does not load it:

```
Cleansia.Config/Filters/RequireCompleteProfileAttribute.cs:25
    var employee = await employeeRepo.GetByUserEmailAsync(email);
Cleansia.Config/Filters/RequireCompleteProfileAttribute.cs:32-35
    var isRegistrationComplete = employee.IsProfileComplete() && employee.Documents.Any(d => d.IsActive) && …
Cleansia.Config/Filters/RequireCompleteProfileAttribute.cs:37-49
    if (!isRegistrationComplete) → 403 "Registration incomplete"
```

```
Cleansia.Infra.Database/Repositories/EmployeeRepository.cs:9-17
    GetDbSet().Include(e => e.User).Include(e => e.Address)
              .Include(e => e.Nationality).Include(e => e.Documents)
              .FirstOrDefaultAsync(…)                                  // ← no payout navigation
```

No lazy loading exists (verified: zero `UseLazyLoadingProxies` in `src/`). So
`PayoutDetails is not null` → **`false` for every cleaner**, → `IsProfileComplete()` false → **403**.

**Blast radius:** `[RequireCompleteProfile]` is applied **class-level** on
`Cleansia.Web.Partner/Controllers/OrderController.cs`, `EmployeePayrollController.cs`,
`DashboardController.cs`, and `DisputeController.cs` — the job board, invoices, dashboard, disputes.
That is the partner web application.

**And the ADR's verification cannot catch it.** Reviewer-verification item 6 specifies *"A test
constructs an employee whose payout record has `Status = NeedsReconfirmation` and asserts
`IsProfileComplete() == true`."* A hand-constructed `Employee` has the nav set; the test is **green**
while every real request 403s.

**Secondary, same cause:** `GetPagedEmployees.cs:37-41` Includes `User`/`Nationality`/`Address`/
`Documents` and projects through `MapToAdminDto` (`EmployeeMappers.cs:89` → `IsEmployeeProfileComplete`
→ `IsProfileComplete()`), so the admin grid's `IsProfileComplete` column silently reads **false for
every cleaner**. And when T-0519/T-0520 fix it by adding `.Include(e => e.PayoutDetails)` to the paged
query, **the full unmasked payout record materializes on the paged list path** — the exact path D1
reason (i) and D8.1 claim the child entity makes structurally unreachable. The protection reduces to
"the next mapper author does not add one positional line", which is what D1 says it eliminates.

**What I want changed.**
- D7 states the load requirement as a **decision**: either `IsProfileComplete()` stops depending on a
  navigation (take a `bool hasPayoutDetails` parameter, or project the flag), or **every** loader
  feeding it is enumerated in the ADR — `EmployeeRepository.GetByUserEmailAsync` (`:9-17`),
  `GetPagedEmployees` (`:37-41`), `GetEmployeeDetail` (`:35-43`), `ApproveEmployee` (`:46`, `:114`) —
  with the Include added. Enumerate them here or a developer will find them one 403 at a time.
- Reviewer-verification item 6 is rewritten to require a **host/route test** (`Cleansia.HostTests`
  already carries `Ac8RejectedCleanerCannotWorkTests` on this exact gate) asserting a cleaner with a
  `NeedsReconfirmation` record gets **200**, not 403, from a `[RequireCompleteProfile]` route.
- D1's reason (i) is downgraded from *"structural"* to *"one extra hop, plus an enforcement test"* —
  and the enforcement test is **specified**, since this repo already has the idiom
  (`FrozenPermissionMapTests`, `RateLimitCoverageGuardTests`, `AuthWireContractTests`,
  `HandleFailureErrorsContractTests`): a **frozen DTO-surface test** asserting that no type in
  `…Features.*.DTOs` other than the single named payout DTO declares a payout-identifier property.
  Without that, "never rides a list DTO" is a rule a future author must remember — which is the thing
  the ADR set out not to build.

---

### CH-S8 — D8.2 cites an **owner-only** ownership check as the precedent for an **owner-or-admin** read, and D8.3 makes one route return different content by role without saying how.

**Part A — the cited precedent excludes admins.** D8.2: the read is *"authorized to the owner of the
record or an admin, following S3… exactly as `UpdateBankDetails.Validator.AllowedToUpdateEmployee`
(`:39-44`) does for writes."* That method is:

```csharp
// UpdateBankDetails.cs:39-44
private async Task<bool> AllowedToUpdateEmployee(Command command, CancellationToken cancellationToken)
{
    var currentUserEmail = _userSessionProvider.GetUserEmail();
    var employee = await _employeeRepository.GetByUserEmailAsync(currentUserEmail ?? string.Empty, cancellationToken);
    return employee?.Id == command.EmployeeId;
}
```

Owner-only. It has **no admin arm** and would reject an administrator, making D8.4's reveal
unreachable. A developer told to follow it "exactly" ships a read no admin can call — or, patching in
a hurry, widens it wrongly. The correct in-repo precedent for a two-arm owner-or-admin check is
`DownloadInvoice.Handler` (`DownloadInvoice.cs:49-58`): role claim checked first, otherwise
caller-employee-id equality, **`NotFound` (not `Forbidden`) on mismatch** per S3. Cite that instead.

**Part B — role-dependent response content on one route is unspecified.** D8.3 masks *"everywhere
except (a) the owner's own edit form"*. So `GET .../employees/{id}/payout-details` returns
**unmasked to the owner** and **masked to an admin (until reveal)** — one route, one NSwag DTO, two
contents. The ADR does not say how the client is told which it got. Left unstated, a frontend that
renders `dto.iban` will render whatever arrived, and "masked by default" becomes a property of five
clients' rendering code instead of a server guarantee — with the two mobile clients app-store-gated
(the ADR makes this argument itself in D7 about `"profile.fields.iban"`).

**What I want changed.** D8.2 cites `DownloadInvoice.cs:49-58` and spells out both arms.
D8.3 states the wire shape: **the masked value and the full value are different fields or different
DTOs** (e.g. `MaskedAccount: "****3003"` always; `Iban` populated only when the caller is the owner or
has revealed), so a client cannot accidentally render an unmasked value it was never sent. Add to
Reviewer-verification item 9: *a test asserting the admin (pre-reveal) response body contains no
substring of the stored account identifier* — the same assertion style already proven at
`EmployeeUserAuditCoverageTests.cs:301`.

---

## What I checked and found sound

Named explicitly, because silence is not assent (`deliberation.md`).

1. **D6's core argument — "we print it anyway" — LANDS, and on far firmer ground than the ADR claims
   for itself. The author's own nominated strongest counter (challenge #3) FAILS.** The counter was
   *"'we print it anyway' is an argument for fixing the printing, not for leaving the column bare."*
   You cannot fix the printing: the payee account **is** the payment instruction on a supplier invoice.
   And it is not a future T-0522 concern — **it is live and test-pinned today**:
   - `FileExtensions.cs:87-108` — `CreateSupplierData(this Employee employee)` sets `Iban = employee.IBAN` at `:107`.
   - `InvoicePdfData.cs:36-59` — `InvoiceSupplierData`, documented *"The SUPPLIER of a payout invoice — the cleaner. Bank details are the cleaner's own; sourcing them from `CompanyInfoData` would print an account that tells the cleaner to pay us."*
   - `DefaultInvoiceLayoutBuilder.cs:166-188` — `PaymentFields` reads `data.Supplier`, rendering `supplier.Iban` at `:175`.
   - `PayoutInvoiceLayoutTests.cs:56` — an existing test named `Payment_Block_Carries_The_Suppliers_Bank_Details_And_The_Variable_Symbol`, with the owner's specimen `"5885638003/5500"` already in the fixture at `:186`, and `:11` noting `Supplier_Block_Is_The_Cleaner_Not_Cleansia` fails if the blocks are swapped.

   **This corrects the ADR in D6's favour**: the context-table row *"The bank block on today's PDF is
   Cleansia's, not the cleaner's"* is wrong (it describes the customer receipt, not the payout
   invoice). The value is already rendered, blob-stored and downloadable. A `ValueConverter` decrypting
   on every read for a value the app prints on a PDF it emails is, as D6 says, theatre. **I do not
   contest D6's conclusion. I contest only its premise (CH-S1) and its threat model (CH-S6).**
   The second leg of the counter — *"a reversal later means re-encrypting live data"* — is real but
   small: it is a one-table, one-tenant, few-thousand-row backfill, and D6 already writes the reversal
   triggers down, which is more than most ADRs do. It does not outweigh the printing argument.

2. **The PDF path — the second reader of the unmasked value, and the one D8.3 exempts — is properly
   ownership-gated.** `DownloadInvoice.Handler` (`DownloadInvoice.cs:39-58`) checks the role claim,
   and for non-admins requires `invoice.EmployeeId == callerEmployeeId`, returning
   `BusinessErrorMessage.InvoiceNotFound` (**NotFound, not Forbidden** — S3's don't-confirm-existence
   convention) on mismatch. The PDF is streamed through the app from the `GeneratedInvoices` container
   (`:60-65`), not handed out as a SAS URL. **S3 PASS.** My hypothesis that D8 masks the JSON while
   leaving the PDF open **died at the guard check.**

3. **S6 is clean today and the ADR's requirement is achievable.** `rg` over every
   `LogInformation`/`LogWarning`/`LogError` in `src/` intersected with `iban|bank`: **zero hits.** No
   payout identifier is logged at any level anywhere.

4. **S5 on the existing write path is already satisfied.**
   `Web.Mobile.Partner/Controllers/EmployeeController.cs:85-90` — `UpdateBankDetails` carries
   `[Permission(Policy.CanUpdateCurrentEmployee)]` **and** `[EnableRateLimiting("auth")]`. My gap
   (CH-S3) is the new *read*, not the write.

5. **S8 — the tenancy archetype the ADR copies is correct.** `Employee : Auditable, ITenantEntity`
   (`Employee.cs:11`). D1's `(TenantId, EmployeeId)` unique index and `ITenantEntity` on the child match
   the house pattern, so the child is scoped by the same global filter as its parent and there is no
   one-sided-join asymmetry between them. I found no `FromSqlRaw`/`ExecuteSqlRaw` on any employee path.
   The one `IgnoreQueryFilters` neighbour, `EmployeeRepository.GetByUserEmailIgnoringTenantAsync`
   (`:19-26`), carries a justifying comment naming T-0361 and Includes only `User` — it would not drag
   payout data across a tenant boundary.

6. **The ADR's "zero owned types" claim (A5, D1) is accurate.** `rg "OwnsOne|OwnsMany|ComplexProperty"`
   across `src/Cleansia.Infra.Database` returns **zero**. Rejecting an unfamiliar persistence pattern
   on this table is right.

7. **The "no column encryption anywhere" claim is accurate.** The only sensitive converter is
   `PasswordConverter` (a one-way hash), structurally unusable for a printable value. D6's
   "current posture" paragraph checks out.

8. **The audit-hygiene precedent D8.5 extends is real and already enforced.**
   `EmployeeUserAuditCoverageTests.cs:37` defines `SubjectIban = "CZ6508000000192000145399"`, `:279`
   feeds it through an admin edit, `:301` asserts `Assert.DoesNotContain(SubjectIban, json)`. And
   `AdminUpdateEmployee.cs:101-103` is a genuine ADR-0012 D4.1 precedent (ids-only snapshot, comment
   explicitly naming the IBAN as PII the audit must never copy). D8.5's extension is well-founded —
   my only ask is that the extension use a **distinct sentinel per new field**, since a single
   `DoesNotContain` across ten fields passes if nine are checked and one is not.

9. **The GDPR *export* is self-scoped and correctly out of this ADR's scope.**
   `GdprExportService.cs:20-40` filters `u.Id == userId` and puts `emp.IBAN` (`:38`) in **the subject's
   own** export — S4-compliant (self-data), and D6 is right to leave it to T-0509.

10. **Two ADR citations I spot-checked are exact.** `ValidationExtensions.cs:122-130` is
    `Cascade(Stop).NotEmpty().Length(15, 34)` with no checksum and no country prefix, as stated; and
    `Employee.Anonymize()` (`Employee.cs:257-267`) does set `IBAN = AnonymizationMarker.Value`
    (`AnonymizationMarker.cs:5` = `"[DELETED]"`), a 9-char value its own validator would reject — the
    ADR's observation is correct and is a good catch.

11. **D9's security core is right and I back it against any pressure.** No PAN column, encrypted or
    otherwise; a card destination is an id in `ProviderAccountRef`. Reason 2 (*you do not push a payout
    to a PAN*) is decisive independently of the PCI-scope argument, exactly as the ADR says at line
    404-405. My objection (CH-S5) is that the invariant needs a **runtime guard** and that the
    *scope* half needs an **escalation** — not that the decision is wrong.

12. **Infrastructure fact that supports D6 and narrows T-0518:** `postgres.bicep:81` declares the
    Flexible Server with **no `dataEncryption` block** → platform default (service-managed keys), not
    CMK, and not disable-able on this resource type. Also confirmed `requireSecureTransport`
    (`:115`) is configured, and the `AllowAllAzureServicesAndResources` rule (`:144-152`) is the
    Azure-internal sentinel — correctly commented at `:140-143`, **not** an open-to-internet rule. I
    checked whether it was and it is not; only `allowAdminIp` (`:154-161`) admits a human, which is
    CH-S6's narrow point.

---

## Summary for the lead

| # | Finding | Lane | Severity | Type |
|---|---|---|---|---|
| CH-S1 | The "IBAN rides a paged list" premise under D6/D8/D1 is factually wrong; `EmployeeListItem.cs:52` is `AdminEmployeeDetail` | D6/D8 | **BLOCK** | wrong premise → wrong justification + a no-op reviewer check |
| CH-S2 | D8.4's audited reveal produces no audit row — `AdminMutationGate.cs:22` gates on `…Command` | D8 | **BLOCK** | unbuildable as specified |
| CH-S3 | The reveal route is unrate-limited and uncovered by `RateLimitCoverageGuardTests`; S5 never named | D8 | must-fix | missing control |
| CH-S4 | D8.5's anonymization is a silent no-op: `GdprDeletionService.cs:43-45` never loads the nav, no lazy loading | D8 | **BLOCK** | latent GDPR-erasure failure the ADR creates |
| CH-S5 | "No PAN ever" unenforced — `Length(15,34)` accepts a PAN today and D7 class 3 copies it to plaintext; scope half needs `Q-PAYOUT-04` | D9 | must-fix | unenforced invariant + missing escalation |
| CH-S6 | D6's threat model omits the direct-`psql` reader `postgres.bicep:154-161` provisions; trigger (iii) miscalibrated | D6 | must-fix (wording) | incomplete reasoning, conclusion survives |
| CH-S7 | D7's gate change 403s the whole partner surface (`RequireCompleteProfileAttribute.cs:25/33` + `EmployeeRepository.cs:9-17`); the ADR's own test would pass | **out of lane** | **BLOCK** | fatal, raised per brief |
| CH-S8 | D8.2 cites an owner-only check for an owner-or-admin read; D8.3's role-dependent body is unspecified | D8 | spec gap | ambiguity |

**Bottom line.** D6 is **right for a reason it did not use** and wrong for the reason it did — fix the
premise (CH-S1) and the threat model (CH-S6) and I will not contest the decision. D9a is **right and I
back it**; D9b belongs in `questions/open.md`. **D8 is the part of this ADR that needs real work**: its
justification (CH-S1), its audit mechanism (CH-S2), its missing rate limit (CH-S3), its erasure clause
(CH-S4) and its authorization precedent (CH-S8). And CH-S7 must be answered before T-0518 starts,
whoever owns it — three of these findings (CH-S4, CH-S7, and the paged-`Include` corollary) are the
**same root cause**: this repo has **no lazy loading**, so *every* invariant the ADR relocates onto a
navigation property becomes load-order-dependent. That single fact should be written into the ADR as a
constraint, not rediscovered in production.
