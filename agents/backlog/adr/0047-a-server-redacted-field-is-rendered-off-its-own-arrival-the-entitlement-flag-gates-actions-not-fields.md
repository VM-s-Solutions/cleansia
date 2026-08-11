# ADR-0047 — A server-redacted field is rendered off its **own arrival**; the entitlement flag gates **actions**, never fields

- **Status:** `accepted` — lead, 2026-08-11, **with amendments A1–A4** (`## Verdict (lead)`). Authored
  `proposed` 2026-08-11; an independent challenger+lead pass ran 2026-08-11 **after** the implementation
  landed (T-0590), and the amendments are what that pass found.
- **Date:** 2026-08-11
- **Mode:** **author**, with an author-run self-challenge (`## Challenge`), **then an independent
  challenger/lead pass** (`## Challenge (independent — post-implementation)` / `## Verdict (lead)`).
  Two lanes did the sweeps that this ADR rules on — recorded at `backlog/questions/open.md` **N24** —
  and this ADR does not re-derive their conclusions from that page: every code-state claim below was
  re-opened at HEAD and is cited at `file:line` (`conventions.md` §*"A claim about the tree cites the
  tree"*).
- **Number:** **0047**, allocated 2026-08-11. The highest on disk was 0046.
- **Supersedes:** nothing. **Narrows:** `patterns-mobile.md` §*"A number the server computes has no
  client-side twin, and the REASON travels with it (T-0527)"*, rule (1) — *"render the discriminator,
  never re-derive it from the rate"*.
- **Routing (ADR-0033):** **test 1 fires** (Android's sweep — shipped call sites become deviations)
  **and test 2 fires** (iOS's sweep — the sentence above governs the subject at a covering
  generality). Two platforms, two independent tests, one destination.
- **Living doc:** `agents/architecture/decisions/redacted-field-rendering.md`
- **Tickets it creates:** one canonicalization ticket (§D7), PM to allocate the id.

> ### ⚠️ Method declaration
> **No shell.** `Read` / `Glob` / `Grep` / `Write` / `Edit` only. Nothing was compiled, executed or
> measured. No test outcome, timing or build result is claimed anywhere below. Every `file:line` is a
> line this author opened at HEAD on 2026-08-11.

---

## Context

`b2a8cf62` gave the platform one redaction seam. `OrderPiiRedaction`
(`src/Cleansia.Core.AppServices/Features/Orders/OrderPiiRedaction.cs`) blanks the customer's identity,
home, free text, confirmation code and the crew's personal contacts for a cleaner admitted by the
**browse** gate alone (`:22-32` for the list shape, `:34-54` for the detail shape). The predicate that
decides it is `IOrderAccessService.CanAccessOrderAsync` — read from the same seam that granted access
(`GetOrderDetails.cs:58`, applied at `:137-139`).

The clients carry a **different** boolean on the same DTO. `isAssignedToCurrentUser` is computed from
the assignment list and nothing else (`GetOrderDetails.cs:81-82`). The two answers are not the same
answer, and the divergent case is the one the server's predicate was deliberately chosen for: **an
employee who books a cleaning for their own home arrives at that handler as the order's customer** —
`CanAccessOrderAsync` is true, so nothing is redacted, while `isAssignedToCurrentUser` is false.

A client that gates the *rendering* of a redacted field on that flag is therefore a **second
authorization implementation living beside the server's**, and it is wrong in exactly the case the
server got right.

Both platforms already ship the correct shape for the one field that has a coarse substitute — Android
`OrderLocationPresentation.kt:17-32` (built at `:34-40`), iOS `OrderLocationPresentation.swift:13-45`
(built at `:47-57`) — and both carry the reasoning in their doc comments (`.kt:6-16`, `.swift:5-12`).
What is missing is the **rule**, so the shape was applied to the address and to nothing else.

## Decision

### D1 — Scope, stated first because it is the load-bearing half

The rule governs **the rendering of a field the server redacts by caller class.** It does **not**
govern:

- an **action** gate — whether a button, slide or command is offered;
- a **request** gate — whether the client issues a call it expects to be refused.

Both of those legitimately read `isAssignedToCurrentUser`, and both fail **closed**, which is the safe
direction. Withdrawing them would offer "Slide to start" on a stranger's order
(`OrderPrimaryAction.kt:97`) and would fetch photographs of a home the caller is not entitled to
(the obligation iOS records at `OrderDetail.swift:119-124`).

**This scope line is not a caveat — it is the reason the entry could not be written as N24's raw
sentence.** *"The client never hides a field the server populated"* sweeps in every action arm; the
canonicalization ticket built from it would have deleted gates that must stay.

### D2 — The pair is ONE sealed value, discriminated by the precise field's arrival

Where the server ships a **coarse substitute** alongside the redacted field, the client models the
pair as a single sealed value with one case per disclosure level — `precise` / `approximate` / `none`
— and every surface reads that value. Neither the raw field nor the substitute is readable beside it.

`OrderLocation` is the reference implementation on both platforms and is unchanged by this ADR.

### D3 — Where there is no substitute, the gate is still a NAMED property

`CustomerPhone`, `AccessInstructions`, `Notes`, `OrderNotes`, `OrderIssues` have no coarse form: the
server blanks them and sends nothing in their place (`OrderPiiRedaction.cs:37-53`). The sealed value
collapses to two cases, and the gate is simply *"did the field arrive"*.

It must nevertheless be a **named property on the presentation model**, not an inline expression
inside the view — `OrderDetail.showsWorkSections` (`OrderDetail.swift:125-127`) is the shape. A gate
that exists only as a `if` condition in a `body`/`@Composable` cannot be driven at the
entitled-but-not-assigned shape without a UI harness, which is why the defect survived on both
platforms.

> #### AMENDMENT A1 (lead, 2026-08-11) — *"named" is not the obligation; WHOLE is*
> **A named property that is only partly named is not pinnable.** The sentence above is satisfied by
> two forms that both leave the defect live, and the Android lane hit the second one in its first pass:
>
> 1. a `val` **inside** the composable — it has a name, the test still cannot construct it;
> 2. a **partial** gate — the model exposes the arrival term (`showsAccessInstructions`) and the *view*
>    conjoins `&& isMine`. The behavioural test on the model stays **green while the entitlement term
>    is reinstated**, which is exactly the mutation this ADR exists to catch. The Android lane observed
>    that green and only moving the *whole* gate onto the model turned it red.
>
> **The obligation is therefore: every conjunct of the gate lives on the presentation model, and the
> view's expression is a single reference to it with no `&&`.** A lifecycle term (D5) does not escape
> this — it becomes a parameter or a property, not a conjunct at the call site:
> `OrderDisclosure.showsAccessCard(status)` (`OrderDisclosurePresentation.kt:45-46`) and
> `OrderDetail.showsAccessCard` (`OrderDetail.swift:140-142`) are the two shipped shapes.
>
> **And the enforcer must be shown to fail, not merely to exist:** reintroducing the entitlement term
> *anywhere in the gate* must redden a named test. A test that only asserts the current behaviour
> passes over form (2).

### D4 — Blank counts as absent

> #### AMENDMENT A2 (lead, 2026-08-11) — the original premise is FALSE at HEAD; the rule survives on a
> #### stronger one
> The paragraph below said *"the server redacts to `string.Empty` and `[]`, **not** to `null`"*. **That
> is wrong, and it is wrong inside the very line ranges it cites.** `OrderPiiRedaction`'s detail shape
> sets `Address = null`, `Notes = null`, `SpecialInstructions = null`, **`AccessInstructions = null`**,
> `CompletionNotes = null`, `Review = null`, `PreferredOffer = null`
> (`src/Cleansia.Core.AppServices/Features/Orders/OrderPiiRedaction.cs:40-53`), and the list shape nulls
> the coordinates (`:30-31`). Only the string scalars — `CustomerName`, `CustomerEmail`,
> `CustomerPhone`, `ConfirmationCode` — go to `string.Empty` (`:25-29`, `:37-41`), and only
> `OrderNotes` / `OrderIssues` go to `[]` (`:49-50`).
>
> **The redaction is MIXED, and that makes the rule stronger, not weaker.** Roster row 1 is blanked to
> `""` and roster row 2 to `null`, so *neither* `!= null` *nor* `!= ""` alone covers the roster. The
> arrival test must tolerate **null, empty and whitespace on the same field**. It also means `null` is
> **ambiguous** — "redacted" and "the customer never typed one" arrive identically — which is
> acceptable precisely because both render the same; a design that needed to tell them apart could not
> be built on this seam.
>
> ⚠️ **The false premise has already propagated verbatim into three shipped artifacts** and each needs
> a doc-comment correction (behaviour is correct in all three; only the stated reason is wrong):
> `src/cleansia_ios/CleansiaPartner/Sources/Features/Orders/OrderDetail.swift:133`,
> `src/cleansia_android/partner-app/src/main/java/cz/cleansia/partner/features/orders/OrderDisclosurePresentation.kt:21`,
> `src/cleansia_ios/CleansiaPartner/Tests/OrderDetailRedactionGateTests.swift:17`.

*(Original D4 text, retained so the amendment above has something to amend and the trail is legible —
**do not read the first sentence as current**.)* The server redacts to `string.Empty` and `[]`, **not**
to `null` (`OrderPiiRedaction.cs:25-31`, `:37-41`, `:49-50`). So the arrival test is `isNullOrBlank` /
`isEmpty` / a whitespace-trimmed check — never `!= null`. Both shipped `OrderLocation`s already encode
this for the substitute and say why (`.kt:37-38`, `.swift:51-52`: *"`BuildApproximateAddress` sends
`""` — not null — for an order with no city"*).

### D5 — A LIFECYCLE term survives; only the ENTITLEMENT term is withdrawn

`showAccessCard` is `isMine && <populated> && (OnTheWay || InProgress)`
(`OrderDetailScreen.kt:481-483`, `OrderDetailContent.swift:19-23`). The status conjunct answers *when
is this useful*, not *may this caller see it*, and it stays. **Only the `isMine` conjunct is deleted.**
Stated explicitly because a canonicalization ticket that deletes the wrong conjunct turns a door code
into permanently-visible content on a completed job.

### D6 — The change is a no-op for every caller class except the one where it is a bug

This is the defense, and it is checkable field by field. For a **browsing** cleaner the server has
already blanked the field, so both terms agree and the render is unchanged. For an **assignee** the
flag is true, so both terms agree. The terms differ **only** for the entitled non-assignee — the
employee-as-customer — and there the current code hides that person's own data from them.

So the migration is low-risk by construction: it cannot widen disclosure to anyone the server withheld
from, because the client is reading what the server sent.

### D7 — The deviating form, its roster, and the enforcer the canonicalization ticket ships

**Deviating form (the membership test — normative, it decides the next case):** *a conditional whose
body renders a field that `OrderPiiRedaction.RedactForBrowsingCleaner` blanks
(`OrderPiiRedaction.cs:25-31`, `:37-53`), and whose condition names `isAssignedToCurrentUser` or a
local aliasing it.*

**Roster (descriptive — read from the tree 2026-08-11; it decides nothing on its own).** If a call site
passes the test and is not here, the **roster** is stale — add it. If it is here and passes the test,
the **call site** is the defect.

| # | Field | Android | iOS |
|---|---|---|---|
| 1 | `CustomerPhone` (call + SMS chips) | `CustomerCard.kt:86-87` | `OrderDetailCards.swift:96-97` |
| 2 | `AccessInstructions` (access card) | `OrderDetailScreen.kt:481-483` | `OrderDetailContent.swift:19-23` |
| 3 | `OrderNotes` / `OrderIssues` (notes & issues section) | `OrderDetailScreen.kt:630` | `OrderDetailContent.swift:124` |

**Explicitly NOT on the roster, and this is a ruling rather than an omission** — each of these gates an
action or a request, per D1, and each stays exactly as written:

- `showWorkSections` / `showsWorkSections` — checklist + photo rails, and on iOS the photo **fetch**
  (`OrderDetailScreen.kt:607-608`, `OrderDetail.swift:119-127`).
- Every arm of the primary action (`OrderPrimaryAction.kt:59`, `:97`, `:113`) and the footer's
  `hasAction`.

**The enforcer** (shipped by the canonicalization ticket, not by this ADR): per roster row, a
**behavioural** test at the entitled-but-not-assigned shape — the field populated **and**
`isAssignedToCurrentUser = false` — asserting the render happens. That shape is already constructible
in both suites (`OrderDetailViewModelTests.swift:42` sets the flag; `OrderDetailMappingTests.swift:144`
asserts it false), and D3's named property is what makes the assertion possible without a UI harness.

**The behavioural test is the instrument; a source-scan is not a substitute for it.** A test asserting
the string `isAssignedToCurrentUser` is *absent* from a view file is an absence assertion, which
`patterns-mobile.md` §*"When the condition a tripwire guards gets fixed, REPOINT the tripwire"* already
rules against — it asserts nothing about the claim's content and goes green if the view is renamed
away. The call-site binding suite (`OrderDetailLocationCallSiteTests.swift:12-21`, reading source off
disk via the `#filePath` walk-out at `:56-62`) is the right instrument for *"the view renders through
the resolver"* and stays; it is not the right instrument for *"the view does not consult a flag"*.

> #### AMENDMENT A3 (lead, 2026-08-11) — what "not a source-scan" does and does not forbid
> The header sentence originally read *"Deliberately not a source-scan"*, which is broader than the
> ruling that follows it and broader than what shipped. Restated:
> - **Forbidden:** an absence assertion whose subject is *the entitlement flag* (*"the view does not
>   mention `isAssignedToCurrentUser`"*). It is the claim's content that matters and that form asserts
>   none of it.
> - **Permitted, and the established precedent:** an absence assertion whose subject is *reaching
>   around the resolver* — `OrderDetailCallSiteTest.kt:104-110` asserts no line reads
>   `order.accessInstructions` / `order.customerPhone`, which is the same shape
>   `OrderDetailLocationCallSiteTests.swift` already ships for `order.address`.
> - **Recorded cost, not a finding:** `OrderDetailCallSiteTest.kt:121` pins an exact source string
>   (`"val showWorkSections = isMine &&"`). It is a positive claim and legitimate, but it reddens on a
>   whitespace or extraction change to correct code. If it becomes noise, replace it with a behavioural
>   assertion on the action gate rather than deleting it.
>
> #### AMENDMENT A4 (lead, 2026-08-11) — the roster's citations are superseded by T-0590
> The roster table is descriptive and was read pre-implementation. **T-0590 (`7fdce902` Android,
> `327013db` iOS) moved all six cells**, so the line numbers no longer point at the gates. Current
> shape, for a reader arriving after the fact: Android
> `partner-app/…/features/orders/OrderDisclosurePresentation.kt` (`OrderDisclosure` +
> `showsAccessCard(status)` + `showsWorkRecordSection(canAddNotesOrIssues)`), iOS
> `CleansiaPartner/Sources/Features/Orders/OrderDetail.swift:134-154`
> (`showsCustomerContact` / `showsAccessCard` / `showsNotesAndIssues` / `canAddNotes`). The membership
> test in the paragraph above is what decides the next case; the table decides nothing.

**Tier:** `(gate pending: <canonicalization ticket>)` → **`T1-CI`** on landing. Both suites are already
CI gates — Android `android-ci.yml:79`, iOS `ios-ci.yml:185-187`. The baseline is **not** zero (three
roster rows × two platforms), which is why the token is `(gate pending:)` and not `T1-CI` today
(`conventions.md` §*"The price of a law"*, condition (b)).

## Alternatives considered

| Option | Disposition |
|---|---|
| **A — a new standalone law** *"a redacted field's replacement is rendered off the field's own arrival"* | **Rejected.** `patterns-mobile.md`'s T-0527 entry already says *"render the discriminator, never re-derive it"* about a server-computed value the client cannot reproduce, which is this subject at a covering generality. Two overlapping laws drift; iOS's routing test was right. Written as a **narrowing** of that entry instead. |
| **B — N24's raw wording** (*"never hides a field the server populated"*) | **Rejected.** It puts the action arms in violation (`OrderPrimaryAction.kt:59`, `:97`, `:113`) and its canonicalization ticket would delete them. D1 is the repair. |
| **C — keep the flag as defence-in-depth** | **Rejected.** `isAssignedToCurrentUser` is itself a **server field** (`GetOrderDetails.cs:81-82`), so it is not an independent check — it is the same server, asked a different question. Real defence-in-depth would be a *client-side* fact, and there is none. Meanwhile the server's redaction carries its own enforcer: `Cleansia.Tests/Features/Orders/OrderRedactionSurfaceTests.cs` asserts per DTO member both ways — blanked (`:166`, `:185`) and preserved (`:175`, `:194`). *(Whether its member set is exhaustive is a property of that suite, read at `:166-194` only; this ADR does not restate the T-0567 row's "every member" claim as its own.)* The belt is enforced; the braces are the bug. |
| **D — drop `isAssignedToCurrentUser` from the DTO** | **Rejected.** It is the correct input to the action gates (D1) and the partner apps also answer *"am I on this job"* from the assigned-employee list, whose `EmployeeId` the redaction deliberately preserves for exactly that reason (`OrderPiiRedaction.cs:56-66`). |
| **E — move the whole decision server-side** (ship a `canSeeCustomerContact` flag) | **Rejected, and it is the closest call.** It would work, but it adds a wire field per redacted group whose only content is *"did we blank the field next to it"* — derivable from the field itself. It also re-creates the exact drift this ADR closes the moment the server's redaction list changes and the flag's derivation does not. Revisit only if a field ever gains a *third* disclosure level with no observable difference on the wire. |

## Challenge (author-run — no independent challenger has run)

- **CH-1 — "the `isMine` term is redundant, so this is cosmetic; it does not earn an ADR."**
  Partly conceded and answered in D6: it *is* redundant for two of three caller classes. It is not
  redundant for the third, and the third is precisely the case that decided the server's predicate. The
  larger cost is structural — the term is a second authorization implementation that will diverge the
  next time `CanAccessOrderAsync` widens, and nothing connects the two.
- **CH-2 — "the roster will be stale in a week."**
  Sustained, and answered by shape rather than by diligence: D7 leads with a **membership test** and
  labels the roster descriptive, per `conventions.md` §*"never enumerate a COUNT of tree instances"*.
  No count appears anywhere in this ADR.
- **CH-3 — "you are asserting the two predicates differ; prove it rather than asserting it."**
  Answered from source: `isEntitledToCustomerData` is `CanAccessOrderAsync` (`GetOrderDetails.cs:58`),
  `isAssignedToCurrentUser` is `AssignedEmployees.Any(...)` (`:81-82`). Two expressions, computed
  independently, in one method.
- **CH-4 — "why not ship the enforcer in this ADR's change?"**
  Because the architect writes decisions, not application code, and because the enforcer needs D3's
  named property to exist first — which is a code change. Hence `(gate pending:)`, which is the exact
  token `conventions.md` provides for a specified-but-blocked gate.
- **CH-5 — "the iOS photo-fetch gate re-derives entitlement too."**
  Conceded as an observation, rejected as a finding under D1. It fails closed: the worst case is a
  request not made. If the entitled non-assignee should see photos, that is a *product* question about
  the partner app's order detail, not a rendering rule — and it would be answered on the server, which
  serves photos only to the strict gate.

## Verdict (author's ruling — pending a lead)

Decisions **D1–D7** stand as written. Nothing here is buildable until a lead rules and the PM stamps
`accepted`; the catalog entry landed alongside this ADR carries the `proposed` status token and its
retirement condition, so a reader cannot mistake its standing.

## Challenge (independent — post-implementation, 2026-08-11)

A challenger instance distinct from the author, running **after** T-0590 shipped, with the
implementation as evidence the pre-implementation panel could not have had. Method: `Read` / `Glob` /
`Grep` only — **no shell, nothing compiled, executed or measured; no test outcome or build result is
claimed anywhere in this section.**

- **IC-1 — "D4's premise is false, and it is false inside its own citations."** **Sustained.**
  `OrderPiiRedaction.cs:40-53` nulls `AccessInstructions` and every free-text field; `:30-31` nulls the
  coordinates. Roster row 2 is null-redacted and roster row 1 is `""`-redacted, so **the roster spans
  both forms** and no single-form test covers it. Author's conclusion (never `!= null`) is right; the
  stated reason is wrong. → **A2**.
- **IC-2 — "D3 does not say enough, and the implementation proved it."** **Sustained.** The Android
  lane's first pass satisfied D3 literally — a named gate — and the mutation that reinstates the
  entitlement flag **passed green**; only moving the whole gate onto the presentation model made it
  red. D3 as written admits a `val` in a composable and admits a model term the view conjoins
  `&& isMine` onto. → **A1**.
- **IC-3 — "D7's 'not a source-scan' is broader than what shipped."** **Sustained in part.**
  `OrderDetailCallSiteTest.kt` ships both permitted and forbidden-looking forms; the ADR's own next
  sentence already draws the line, the header sentence does not. → **A3**.
- **IC-4 — "the roster is stale."** **Sustained as expected, not as a defect** — the author labelled it
  descriptive and led with a membership test, which is exactly why staleness is survivable. Dated
  pointer added so nobody chases the old line numbers. → **A4**.
- **IC-5 — "D6 claims the change cannot widen disclosure; check it against what actually shipped."**
  **Not sustained — D6 holds.** Every shipped gate reads a field the server already decided:
  `showsCustomerContact` off the phone (`OrderDetail.swift:134-136`, `OrderDisclosure.kt:28`),
  `showsAccessCard` off the instructions **plus the retained lifecycle conjunct**
  (`OrderDetail.swift:140-142`, `OrderDisclosurePresentation.kt:45-46`), `showsNotesAndIssues` off the
  record **or** `canAddNotes` (`OrderDetail.swift:146-148`,
  `OrderDisclosurePresentation.kt:52-53`). For a browsing cleaner the server sent `""` / `null` / `[]`,
  so all three stay hidden. **No D5 violation:** the lifecycle conjunct survives on both platforms and
  is pinned (`OrderDetailRedactionGateTests.swift:30-43`).
- **IC-6 — "the action gates were withdrawn along with the entitlement term."** **Not sustained.**
  `canAddNotes` keeps `isAssignedToCurrentUser` (`OrderDetail.swift:152-154`), the Android composable
  keeps `isMine && (…)` for `canAddNotesOrIssues` (`OrderDetailScreen.kt:622-623`) and for
  `showWorkSections`, and `OrderPrimaryAction` still reads the flag (`OrderDetailScreen.kt:740`). D1's
  scope line did its job.

## Verdict (lead) — `accepted with amendments`, 2026-08-11

**D1, D2, D5, D6 stand unchanged. D3, D4 and D7 are amended — A1, A2, A3, A4 above — and the amendments
are part of the accepted text.** No challenge blocks. Consensus declared; nothing escalated to the
owner.

**Why it is accepted rather than returned, given the implementation shipped first.** Every finding is a
correction of *evidence or of scope*, not a reversal of a decision: the rule the ADR states is the rule
the tree implements on both platforms, and A1/A2 make the ADR say what the implementation already does.
Returning it would leave the shipped code governed by nothing while the ADR was rewritten.

**Provenance check (`deliberation.md` step 5).** Every load-bearing code-state claim was re-opened by
the lead rather than trusted. Divergences recorded: **D4's redaction-form claim (false — A2)** and the
**D7 roster's line numbers (superseded — A4)**. `OrderPiiRedaction.cs`, `OrderDetail.swift`,
`OrderDisclosurePresentation.kt`, `OrderDetailCallSiteTest.kt`, `OrderDetailRedactionGateTests.swift`,
`OrderDetailScreen.kt` were each opened at HEAD.

**Deliberately NOT ruled here, because it needs a command the panel does not have.** The tier token
stays `(gate pending: the ADR-0047 canonicalization ticket)` in both catalog entries. Flipping it to
`T1-CI` asserts a **zero baseline**, and a zero baseline is a claim about a green build — not
something a `Read`-only pass may assert. **What I would want run before that token moves:**
`./gradlew :partner-app:testDebugUnitTest --rerun-tasks --no-build-cache` (the call-site test reads
source off disk and Gradle does not track it as an input — `OrderDetailCallSiteTest.kt:16-17`) and the
`CleansiaPartner` scheme's test action; then the A1 mutation, twice — reinstate `&& isMine` inside
`OrderDisclosure.showsAccessCard` and inside `OrderDetail.showsAccessCard`, and confirm **both** go
red.

**Follow-ups filed by this verdict (PM to allocate ids):**
1. **Doc-comment correction** for the three artifacts named in A2 — the behaviour is right, the stated
   reason is false, and it is the reason that a future field will be reasoned from.
2. **A1 as a catalog sentence** — `patterns-mobile.md` §*"The redaction narrowing of rule (1)"* states
   the named-property obligation; it must carry the *whole-gate* form, or the catalog reproduces the
   hole the Android lane fell into.

## How a reviewer verifies compliance

1. **Run the membership test in D7 over the diff.** A conditional naming `isAssignedToCurrentUser`
   whose body renders a field listed in `OrderPiiRedaction.cs:37-53` is a finding.
2. **Check the conjunct that was deleted.** A lifecycle term removed alongside the entitlement term is
   a D5 violation and is worse than the defect.
3. **Check the gate is a named property**, not an inline `if` in a view body (D3) — otherwise the
   behavioural pin in D7 cannot be written and the reviewer is the only enforcer.
4. **Check the new test drives the divergent shape**: field populated **and** the flag false. A test
   with the flag true proves nothing; a test with the field blank proves the server's behaviour, not
   the client's.
