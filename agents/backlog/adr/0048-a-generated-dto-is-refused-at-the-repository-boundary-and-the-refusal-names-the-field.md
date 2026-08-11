# ADR-0048 — A generated DTO is **refused** at the repository boundary, never defaulted; and the refusal **names the field**

- **Status:** `proposed` — architect, 2026-08-11. **The author does not accept their own ADR**
  (`adr/README.md`); a lead rules and the PM stamps.
- **Date:** 2026-08-11
- **Mode:** **author**, with an author-run self-challenge (`## Challenge`). No independent challenger
  has run. The sweeps this ADR rules on were done by the T-0576 / T-0582 lanes and recorded on
  `backlog/INDEX.md` (T-0582, T-0588, T-0589) and `questions/open.md` **N29**. **No code-state claim
  is inherited from those rows** — every one below was re-opened at HEAD and is cited at `file:line`,
  and §D7 records where a ticket's own stated membership test is **falsified** by the tree.
- **Number:** **0048**, allocated 2026-08-11. 0047 was allocated the same day to the redaction-
  rendering lane.
- **Supersedes:** nothing. **Extends:** `patterns-mobile.md` §*"Networking & Repository — exact idiom"*
  and its T-0441 note (*"every field on an OpenAPI-generated command is optional with a `= null`
  default"* — the request side; this ADR is the response side).
- **Routing (ADR-0033):** **test 1 fires, hard.** The rule puts shipped repositories in violation, so
  it needs a `consistency.md` deviation entry and a canonicalization ticket — neither of which a
  feature lane may file for itself.
- **Living doc:** `agents/architecture/decisions/mobile-wire-contract.md`
- **Answers:** **T-0589** (*which refusal idiom wins*) — §D5, §D6.
- **Consumes:** ADR-0011 (`ApiResult<T>` is THE mobile repository contract, and the type lives in
  `:core`), ADR-0032 (a constraining entry names an enforcer and declares a tier), ADR-0033.

> ### ⚠️ Method declaration
> **No shell.** `Read` / `Glob` / `Grep` / `Write` / `Edit` only. Nothing was compiled, executed or
> measured. **No count of tree instances appears in the normative text** (`conventions.md`
> §*"never enumerate a COUNT"*): §D7 is a membership test plus a roster.

---

## Context — why the generator hands the contract to the mapper

Neither mobile spec declares a `required` array. The customer spec is `"openapi": "3.0.4"`
(`src/cleansia_android/openapi/customer-mobile-api.json:2`), and OpenAPI 3.0's default is
`nullable: false` — so a property with no `nullable` key is a property the **server never sends null
for**. The Kotlin generator, seeing no `required` array, emits every property optional-with-null
anyway. The contract therefore survives only in the spec, and the only place a client can re-assert it
is the hand-written mapper.

`?: 0.0` on such a field is not a default — it is a **fabrication**. It renders `0 Kč` to a cleaner who
earned `4 800`, and nothing anywhere goes red.

## Decision

### D1 — Five rules, and each exists because the naive default breaks it

A repository maps generated DTOs through a `toDomain()` / `toAppDto()` mapper. That mapper:

1. **Money and quantities are never coerced.** A null in a field the spec marks `nullable: false` is a
   renamed or broken wire field, not a zero. Refuse it.
2. **Booleans follow the money rule.** `false` is a real state — an unpaid order, a period with no
   invoice — so defaulting to it is a claim, not a fallback.
3. **Identity is refused or dropped, never synthesized.** A row with no id is already dead (every card
   navigates by id). Which of *refuse* and *drop* applies is §D2's question, and it is a **per-surface
   ruling**.
4. **Collections DO default** — `orEmpty()`. An absent list and an empty list are the same fact to
   every renderer, and there is no arithmetic to falsify. *(Not the body: see §D4 fact 4.)*
5. **Nullable-by-design stays nullable.** A field the spec marks `nullable: true` is carrying a real
   "unknown", and forcing it to a value destroys the distinction the server drew.

The shipped reference is partner `PeriodPayRepository.kt:77-84` / `:95-97` and
`DashboardRepository.kt:243-252`.

### D2 — The rollup ruling is **per surface**, and it is decided by where the total comes from

> **Refuse the page where the list IS the addends. Drop the row where a total is supplied
> independently.**

Getting it backwards produces *"a smaller, plausible, unmarked number"* — the failure mode with no
symptom.

- **Drop the row** when the rendered total comes from the **server**, not from the rows. Customer
  `OrderApi.kt:141-152` is the worked case and states it: the paged `total` is the server's own count,
  the badges count rows actually shown, so a lost row cannot falsify a figure — while refusing the page
  would hide every order the server answered correctly.
- **Refuse the page** when the client **sums the rows**. The catalog case is the sharpest: a dropped
  services/packages row keeps its **id selected** in booking state, so the server still prices it on
  `Create` while it has vanished from the pre-quote subtotal — the customer pays a price they were
  never shown.
- **Refuse when the rows are alternatives to each other** (membership plans): a missing one is a
  different purchase, not a shorter list.
- **The two rulings compose within one mapper.** Customer `OrderApi.kt:133-137` is the shape: identity
  is dropped in the page mapper, and a surviving row whose own money is broken refuses — and because
  the row is an element of the page, that refusal refuses the page.

**The ruling is made per surface and never inherited.** A lane porting this rule to a new repository
answers *where does the number on screen come from* before it chooses, and records the answer in the
mapper's doc comment, as `OrderApi.kt:122-126` and `:141-152` do.

### D3 — The pin is a wire test over a captured payload, asserting the `@SerialName` set

Per repository: decode a captured payload with **every** member non-default; assert that removing a
`nullable: false` money key **fails** the mapping; and assert that the mapper's `@SerialName` set
**equals the spec's property set** for that schema. The last one is the field-name contract the mapper
owns implicitly and would otherwise lose silently on a rename.

Named instances at HEAD: `PeriodPayWireTest.kt`, `InvoicesWireTest.kt`, `DashboardWireTest.kt`,
`PendingOffersWireTest.kt` (partner); `OrderWireTest.kt`, `CatalogWireTest.kt`,
`BookingQuoteWireTest.kt`, `MembershipWireTest.kt`, `LoyaltyWireTest.kt`, `ProfileWireTest.kt`
(customer).

### D4 — Four facts this class turns on, each counter-intuitive, each written down because it was learned the hard way

1. **A bare `$ref` cannot carry `nullable` in OpenAPI 3.0, so its absence carries no information.**
   Sibling keywords beside `$ref` are ignored, and the emitted schema shows it: in
   `GdprExportOrderDto`, `"status": { "$ref": "#/components/schemas/OrderStatus" }`
   (`customer-mobile-api.json:8100-8102`) sits beside `"totalPrice": { "type": "number", "format":
   "double" }` (`:8103-8106`), which *is* non-nullable by omission. **A `$ref` field can look required
   and be genuinely optional.** Never read a `$ref`'s missing `nullable` as a contract; read the C#
   property.
2. **A `toDomain()` that coerces scores clean on a "does it have a mapper?" audit.** The customer app
   has mappers throughout — *and the mappers coerce*. Any audit of this class must read what the mapper
   **does**. §D7's membership test is written on the mapper's null-handling for exactly this reason,
   and it is why **T-0588's stated tell is wrong** (§D7).
3. **The rollup ruling is surface-dependent** (§D2). There is no rule of the form "always refuse" or
   "always drop"; there is a rule about *where the number comes from*.
4. **`.orEmpty()` on the RESPONSE BODY is not rule 4 — it is the worst outcome of the set.**
   `CatalogRepository` called it on the body, so a **refused price list surfaced as an empty catalog
   reported as Success** — "nothing is bookable today", strictly worse than the coercion it looked
   like. Rule 4 defaults a **collection member**; it never defaults the payload.

### D5 — T-0589: the `WireContract` idiom wins

**Three idioms are live at HEAD, not two.** T-0589's framing names two; the tree carries a third, and
the third is the one whose *reasoning* is right.

| # | Idiom | Where | What a broken `nullable: false` field produces |
|---|---|---|---|
| 1 | `required(field)` throws → `mapWire` catches | partner `WireContract.kt:12-15`, `:22-28` | `ApiResult.Error(ApiError.Server(200, "<field> is null but the mobile API contract declares it non-nullable"))` — **the field name survives in the value** |
| 2 | `?: return null` → `networkError()` | customer `OrderApi.kt:195`, `OrderRepository.kt:125`, `:138` | `ApiResult.Error(ApiError.Network(…))` — loud, but **attributed to the network**, which is the one thing that did not fail |
| 3 | `?: return null` → `emptyBodyError()` | customer `RecurringBookingRepository.kt:116-117` | `ApiResult.Error(ApiError.Unknown(…))` — correctly channelled, field name lost |

**And idiom 2 has a fourth outcome the ticket does not name, which decides this on its own.** On the
customer's paged path a refused body is not an error at all:
`val body = resp.body() ?: return ApiResult.Success(Unit)` — `OrderRepository.kt:84` (`refresh`) and
`:110` (`loadNextPage`). `mapBody` rewraps a refusal as a **200 with a null body**
(`OrderApi.kt:116-118`), `isSuccessful` is true, and the repository reports **Success** with no rows
added.

That is the coercion defect wearing a different costume. The refusal at `OrderApi.kt:122-126` exists,
in its own words, because *"a defaulted zero silently ends pagination, so the customer's older orders
stop existing rather than fail to load"* — and one layer up, the refusal produces exactly that.

**Ruling: idiom 1 is canonical.** Four grounds, in order of weight:

1. **It cannot degrade to Success.** `mapWire` maps `ApiResult.Success` → `ApiResult.Error` and there
   is no other path (`WireContract.kt:22-28`). Idioms 2 and 3 decide the outcome **at every caller**,
   and at HEAD that produced three different answers including `Success(Unit)`. *The transport is
   decided once, in one function, or it is decided N times.*
2. **It is the only one that carries the field name** — the cost T-0589 was filed over.
3. **It attributes correctly.** `ApiError.Server(200, …)` says *the server answered and the answer was
   wrong*. `ApiError.Network` says the opposite of what happened, and sends a user to check their
   connection and an investigator to the wrong subsystem.
4. **It composes.** `required()` is a call-site obligation on one expression; `?: return null` obliges
   every enclosing function to be nullable and every caller to handle it.

**What is adopted from the losing side, not discarded:** `RecurringBookingRepository.kt:110-115`'s
reasoning — *"deliberately not `ApiError.Network`: that channel is the silent one … reusing it here
turns a failed write into a no-op the user never sees."* That is correct and idiom 1 satisfies it.
`ApiError.Network` is not an available channel for a contract violation on any surface.

### D6 — "…reaches triage" is **owed, not shipped**, and the migration ticket owes it

`WireContract.kt:19-20` states the idiom's purpose as *"the offending field name reaches triage rather
than the cleaner."* At HEAD **nothing on the partner app records it.** `partner-app` contains zero
occurrences of `SentryAndroid` or `SENTRY_DSN` (swept 2026-08-11), and `:core`'s own build file says so
in as many words: *"Customer wires both up; partner doesn't run Sentry yet"*
(`core/build.gradle.kts:129-131`). `ApiError.Server` renders as the generic line, so the cleaner does
not see it either.

So the field name is **preserved in a value that reaches no sink**. The idiom is still right — it is
the only one that keeps the name at all, and a name preserved can be routed later while a name lost
cannot — but the ADR will not repeat an unbacked claim. This is the
`patterns-backend.md` §*"⭐ If you write down an invariant, write down the thing that goes RED"* disease
one layer up: **prose asserting a property nothing delivers.**

**The migration ticket owes one of two closures, and must pick one rather than leaving the sentence
standing:** either a sink for `ApiError.Server` on partner (the customer app already initialises Sentry
— `CleansiaApp.kt:61-62`), or a correction to `WireContract.kt`'s doc comment stating that the name is
carried for the day a sink exists. **Q-OBS-01 is the open owner question that governs which**
(`questions/open.md`); this ADR does not pre-empt it.

### D7 — The deviating form, its roster, the enforcer — and a correction to T-0588's stated tell

**Deviating form (the membership test — normative):** *a mapper from a generated model that supplies a
value for a field the spec marks `nullable: false` — `?: 0.0`, `?: 0`, `?: false`, `?: ""`, `?: <n>`,
or `.orEmpty()` on the response **body*** — **or** a refusal whose transport is decided at the call
site rather than by one shared wrapper (§D5).

**⚠️ T-0588's row says the tell is *"the return type, still a generated `*Dto`"*. That is false at
HEAD and a lane sweeping on it will read the wrong files.** `ReferralApi.getMy()` returns a
**hand-written** `ReferralAccountDto` (`ReferralApi.kt:20-23`) through a mapper that coerces
(`:44-50`, `:59-68`); `DisputeApi` likewise returns hand-written `DisputeListResponseDto` /
`DisputeDetailsDto` (`DisputeApi.kt:30-38`). This is fact 2 of §D4 landing on the ticket that was
written to close it. **Sweep on the mapper's null-handling, never on the return type.**

**Roster (descriptive — read 2026-08-11; it decides nothing on its own).** Customer app, under
`core/`:

| Repository | File |
|---|---|
| Referral | `customer-app/…/core/referral/ReferralApi.kt` |
| Dispute | `customer-app/…/core/disputes/DisputeApi.kt` |
| Recurring booking | `customer-app/…/core/recurring/RecurringBookingApi.kt` |
| Saved address | `customer-app/…/core/user/SavedAddressApi.kt` |
| Promo code | `customer-app/…/core/promo/PromoCodeApi.kt` |
| Notification preferences | `customer-app/…/core/notifications/NotificationPreferencesApi.kt` |

Plus, on the **transport** limb of the form and independent of the six above: customer
`OrderRepository.kt:84` and `:110` (a refusal reported as `Success(Unit)`), and every
`?: networkError()` on a 2xx body — `OrderRepository.kt:125`, `:138`.

**Enforcer:** the per-repository wire test of §D3, run by `:customer-app:testDebugUnitTest` /
`:partner-app:testDebugUnitTest` in `android-ci.yml:79`.
**Scope, stated because it is narrower than the rule:** the wire tests are a **closed roster** — they
gate the repositories that have one, and a *seventh* repository added tomorrow with a coercing mapper
is caught by nothing. The general form is not mechanically expressible by the line-based
`check-consistency.mjs` (it needs the spec's nullability for the schema the mapper targets), so
widening the roster means adding a wire test per repository, and the canonicalization ticket says so.

**Tier:** `(gate pending: T-0588)` → **`T1-CI`** when the roster is complete and the baseline is zero
(`conventions.md` §*"The price of a law"*, condition (b)).

## Alternatives considered

| Option | Disposition |
|---|---|
| **Coerce (`?: 0.0`) and move on** | **Rejected** — it is the defect. A fabricated number is indistinguishable from a correct one on screen, and the customer/cleaner acts on it. |
| **Always refuse the page** | **Rejected.** It hides every row the server answered correctly for the sake of one it did not, and on the orders list the total is the server's own count so nothing is falsified by a drop (§D2). |
| **Always drop the row** | **Rejected.** On a surface that sums its own rows this silently understates the number, which is the *"smaller, plausible, unmarked"* failure. |
| **Declare the DTOs non-nullable and let `kotlinx.serialization` throw** | **Rejected as the general answer, though it is what customer `OrderDtos.kt:43` does for `totalPrice`.** It moves the refusal into the deserializer, where the thrown message is a serialization diagnostic rather than a domain one, it cannot express the drop-the-row ruling at all, and it puts the contract in a second place that must be kept in step with the spec by hand. Keep it only where a hand-written DTO already exists. |
| **Fix the spec** (emit `required` arrays so the generator types the fields correctly) | **Rejected here, and it is the closest call — it would delete this whole class.** It is an owner-only `mobile-spec-regen` change to the emitted contract, it re-types every generated model on two apps at once, and every mapper would still need writing to survive the interim. **Not foreclosed:** if the generated models ever become non-nullable, §D1's rules 1–3 collapse into the type system and this ADR is superseded rather than amended. |
| **Duplicate `WireContract` into `customer-app`** | **Rejected.** Two copies of the refusal transport is the divergence T-0589 exists to close, at half the migration cost and all of the future cost. |
| **Move `WireContract` into `:core`** | ✅ **Chosen** — §D5, migration in the ticket spec below. It depends only on `cz.cleansia.core.network.ApiError`/`ApiResult`, which are already `:core` (`WireContract.kt:3-4`), so it is a package move plus a visibility change, not a new dependency. |

### The migration, priced — because a ruling without its cost is a preference

1. **Move `WireContract.kt` to `:core` (`cz.cleansia.core.network`), `internal` → `public`.** One file;
   no new dependency (`WireContract.kt:3-4`).
2. **Repoint the partner imports** — `DashboardRepository.kt:16`, `OrdersRepository.kt:29`,
   `PeriodPayRepository.kt:8`. Mechanical.
3. **`:core` grows a `Response<T>` counterpart to `mapWire`.** This is the real cost and it is **not**
   a drop-in: partner adapters already return `ApiResult` (`DashboardRepository.kt:204`), while the
   customer's adapters return Retrofit `Response<T>` and build `ApiResult` in the repository
   (`OrderApi.kt:116-118`, `OrderRepository.kt:120-126`). Two shapes must meet. **Ruled: add the
   `Response<T>` counterpart** — smallest change that closes the attribution gap — and record that
   converging the customer adapters onto `safeApiCall`/`ApiResult` is the ADR-0011 direction, **not
   this ticket's job**.
4. **Customer mappers become total**: `toAppDto(): T?` → `toAppDto(): T` with `.required("field")`, and
   the three `?: return ApiResult.Success(Unit)` / `?: networkError()` sites on 2xx bodies go
   (`OrderRepository.kt:84`, `:110`, `:125`, `:138`).
5. **iOS has neither idiom and inherits the `:core` shape as a Swift equivalent when it ports** —
   which is the reason T-0589 was filed before the port rather than after.

**Who owes it:** the Android lane, as the T-0588 canonicalization (steps 1–4); the iOS lane inherits
step 5 with no decision left to make. §D6's closure is owed by whichever lane lands step 1.

## Challenge (author-run — no independent challenger has run)

- **CH-1 — "one ADR, two decisions: the mapper contract and the refusal transport. Split it."**
  **Rebutted.** They are not separable. §D2's ruling *decides whether a field name exists to
  transport* — a dropped row has no refusal to attribute — and T-0589 exists precisely because the
  *rule* was ruled without the *channel*, so two lanes implemented one rule three ways. Ruling the rule
  again without the channel would reproduce the defect. Recorded because the split is the defensible
  alternative reading.
- **CH-2 — "idiom 1 throws for control flow, which the backend catalog forbids."**
  **Rebutted on scope.** `consistency.md` B8 forbids a broad `catch (Exception)` for control flow
  around a **provider call**. `mapWire` catches a **private, purpose-built** exception type
  (`WireContractViolation`, `WireContract.kt:12-13`) inside a `runCatching` whose whole body is a pure
  mapping function with no IO. Conceded partially: it does catch `Throwable`, so a genuine bug in the
  mapper is reported as a wire violation. That is a narrowing worth making in the migration — catch
  `WireContractViolation` specifically and let anything else crash — and it is written into the ticket
  spec rather than left as taste.
- **CH-3 — "you claim idiom 2 reports Success; prove it, don't assert it."**
  Answered from three lines read at HEAD: `mapBody` returns `Response.success(transform(body()),
  raw())` (`OrderApi.kt:116-118`), `refresh` reads `resp.body() ?: return ApiResult.Success(Unit)`
  (`OrderRepository.kt:84`), and `toAppDto` returns null on refusal (`:127-139`, `:130-137`).
- **CH-4 — "the six-repository roster will be stale before the ticket lands."**
  Sustained and answered by shape: the roster is labelled descriptive, the membership test is
  normative, and no count appears in the normative text.
- **CH-5 — "`.orEmpty()` on collections contradicts rule 1: an absent list of pay lines is data loss
  too."**
  **Rebutted with a boundary.** An absent collection and an empty one render identically and falsify no
  arithmetic — *unless* the collection is the addends of a rendered total, which is §D2, where the
  ruling is refuse. The two rules meet there and D2 wins. Fact 4 of §D4 carves out the case that
  actually bit (`.orEmpty()` on the **body**), which is not rule 4 at all.

## Verdict (author's ruling — pending a lead)

**D1–D7 stand.** T-0589 is **answered**: idiom 1 (`WireContract`) is canonical, it moves to `:core`,
and the "reaches triage" half is owed and named. T-0588's stated membership test is **corrected** in
§D7 and the ticket needs re-wording before a lane picks it up.

## How a reviewer verifies compliance

1. **Read what the mapper does, never whether one exists** (§D4 fact 2). A `toDomain()` in the diff is
   not evidence; a `?:` on a `nullable: false` field is a finding.
2. **Check the spec, not the generated type.** The generated property is nullable regardless. For a
   `$ref` field the spec's silence means nothing (§D4 fact 1) — read the C# property.
3. **Check that the rollup ruling was MADE, in the mapper's doc comment, and for this surface**
   (§D2). An inherited ruling with no sentence about where the number comes from is a finding even
   when the outcome happens to be right.
4. **Check the refusal transport is `mapWire`**, not a per-call-site `?:`. A 2xx body that resolves to
   `ApiResult.Success` or to `ApiError.Network` is a finding (§D5).
5. **Check the wire test drives the mutation**: removing a `nullable: false` money key from the
   captured payload must **fail** the mapping, and the `@SerialName` set must be asserted equal to the
   spec's property set (§D3).
