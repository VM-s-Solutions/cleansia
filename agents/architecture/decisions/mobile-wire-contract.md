# The mobile wire contract — mapping and refusing a generated DTO (living decision notes)

> Companion to **ADR-0048**
> (`agents/backlog/adr/0048-a-generated-dto-is-refused-at-the-repository-boundary-and-the-refusal-names-the-field.md`).
> An accepted ADR is immutable; this page is the *evolving* design notes, trade-off space and current
> shape. Update this when the design evolves; supersede the ADR for a real decision change.
>
> 🟢 **ADR-0048 is `accepted`** (that file, `:3`), ruled by a lead 2026-08-11 **with amendments B1–B6**.
> **Retires when:** its status line stops reading `accepted`.
>
> ### The three amendments that change what a lane does — read these before the ADR body
>
> **B1 — `.orEmpty()` on the response body is not absolutely forbidden.** The ADR said *"it never
> defaults the payload"*, and T-0588's own diff ships three deliberate exceptions
> (`OrderApi.kt:109`, `OrderRepository.kt:250`, `CatalogRepository.kt:87`). The discriminator is §D2's,
> applied to the payload: a collection **payload** may default to empty only when absence and empty are
> **the same product decision**, **nothing sums / counts / paginates it**, and **no affordance is derived
> from its emptiness that a user would read as a fact** — *and the mapper's doc comment says which*.
> `CatalogRepository.refresh` is the worked example because it **refuses** services and packages
> (`:82-83`) and **degrades** extras (`:87`) in one method.
>
> **B2 — the "359 of 359 strings are `nullable: true`" figure is a DATED MEASUREMENT, not a law.**
> Corroborated 2026-08-11 and **perishable**: any owner-run `mobile-spec-regen` can move it.
> What is normative carries no number — *a string's declared nullability in either mobile spec
> discriminates nothing, so for a `string` you read the C# property, always.* **Re-measure after a
> regen; never inherit the count from a page.**
>
> **B4 — `:core` carries FOUR pieces, not the one the ADR priced**, and the fourth is a contract change
> to a **shared** primitive: `networkCall` now rethrows `WireContractViolation`
> (`core/network/NetworkCall.kt:61-62`) instead of folding it to `null`. Without it every customer-side
> violation is reported as `ApiError.Network` — the attribution defect §D5 exists to close. **The iOS
> port inherits all four**, plus the reason: the customer's adapters map *inside* the Retrofit
> `Response`, so a refusal can only cross that boundary as a throw.
>
> **Deliberately a separate page from `generated-client-contract.md`.** That page is scoped to the
> **web** NSwag clients and says so — *"The Android/iOS generated clients come from the separate
> owner-only `mobile-spec-regen` and are not covered by anything on this page."* This is the mobile
> half, and the two pipelines share no mechanism.

## Scope

The **response** side of the Android/iOS generated clients: what a repository does with a generated
model, and what a caller sees when the payload breaks the contract. The **request** side is already
governed (`patterns-mobile.md` §*"Testing the request side of an Api adapter"*, T-0441) and is a
different failure: there, a dropped mapper line silently omits a field the user typed.

## Why the contract lands in the mapper

```
C# DTO property (non-nullable)
        │
        ▼   host swagger  →  owner-only `mobile-spec-regen`
openapi/*-mobile-api.json      "openapi": "3.0.4",  NO `required` array on any schema
        │                       so a property with no `nullable` key IS non-nullable by 3.0 default
        ▼   Kotlin generator
generated model               EVERY property optional-with-null, regardless
        │
        ▼
   the hand-written mapper    ← the ONLY place the contract can be re-asserted
```

Three consequences, and each one has bitten:

1. **`?: 0.0` is a fabrication, not a default.** It renders `0 Kč` to a cleaner who earned `4 800`, and
   nothing goes red.
2. **A bare `$ref` cannot carry `nullable` in OpenAPI 3.0**, so its absence carries no information —
   `"status": { "$ref": … }` at `src/cleansia_android/openapi/customer-mobile-api.json:8100-8102` is
   indistinguishable from the genuinely non-nullable `totalPrice` at `:8103-8106`. A `$ref` field can
   look required and be genuinely optional. **Read the C# property.**
3. **A `toDomain()` that coerces scores clean on a "does it have a mapper?" audit.** The customer app
   has mappers throughout *and the mappers coerce*.

## Current shape

**The mapper (ADR-0048 §D1).** Money and quantities refuse; booleans refuse; identity is refused **or
dropped**; collections `orEmpty()`; nullable-by-design stays nullable.

**The rollup ruling is per surface (§D2), and it is the part that is re-decided every time:**

| The rendered total comes from… | Ruling | Because |
|---|---|---|
| the **server** (a paged `total`, a supplied grand total) | **drop the row** | a lost row falsifies no figure, and refusing hides every row the server answered correctly |
| the **client summing the rows** | **refuse the page** | a dropped row is *"a smaller, plausible, unmarked number"* — and in booking it keeps its **id selected**, so the server still prices it on `Create` |
| the rows are **alternatives to each other** (plans) | **refuse** | a missing one is a different purchase, not a shorter list |

Both compose inside one mapper: identity drops, and a surviving row whose own money is broken refuses —
and because the row is an element of the page, that refuses the page
(`customer-app/…/core/orders/OrderApi.kt:133-137`).

**The refusal transport (§D5, answering T-0589).** Three idioms are live at HEAD:

| # | Idiom | A broken `nullable: false` field produces |
|---|---|---|
| 1 ✅ | partner `WireContract.required()` + `mapWire` (`WireContract.kt:12-15`, `:22-28`) | `ApiError.Server(200, "<field> is null but …")` — the name survives |
| 2 ✗ | customer `?: return null` → `networkError()` (`OrderRepository.kt:125`, `:138`) | `ApiError.Network` — attributed to the one thing that did not fail |
| 3 ✗ | customer `?: return null` → `emptyBodyError()` (`RecurringBookingRepository.kt:116-117`) | `ApiError.Unknown` — right channel, name lost |

…and a **fourth outcome** that decides it: on the paged path, `resp.body() ?: return
ApiResult.Success(Unit)` (`OrderRepository.kt:84`, `:110`). A refused page is reported as **Success**.
The refusal at `OrderApi.kt:122-126` exists because *"a defaulted zero silently ends pagination"* — and
one layer up it produces exactly that.

**Ruled: idiom 1, moved to `:core`.** It cannot degrade to Success (one function decides, not N
callers); it is the only one that carries the field name; it attributes correctly; and `required()`
composes on one expression where `?: return null` obliges every enclosing signature. `ApiError.Network`
is never available for a contract violation — that reasoning is adopted verbatim from idiom 3's own doc
comment.

> ⚠️ **The "reaches triage" half is OWED, not shipped.** `WireContract.kt:19-20` states the purpose as
> *"the offending field name reaches triage"*. `partner-app` contains **zero** `SentryAndroid` /
> `SENTRY_DSN` occurrences (swept 2026-08-11) and `:core`'s build file says so —
> *"Customer wires both up; partner doesn't run Sentry yet"* (`core/build.gradle.kts:129-131`). The name
> is preserved in a value that reaches no sink. Still better than losing it; not yet what the comment
> claims. The migration ticket picks **one**: a sink, or a corrected comment. `Q-OBS-01` governs which.

## The migration, priced

1. Move `WireContract.kt` → `:core` (`cz.cleansia.core.network`), `internal` → `public`. No new
   dependency — it already imports only `cz.cleansia.core.network.ApiError`/`ApiResult`
   (`WireContract.kt:3-4`).
2. Repoint three partner imports (`DashboardRepository.kt:16`, `OrdersRepository.kt:29`,
   `PeriodPayRepository.kt:8`).
3. **`:core` grows a `Response<T>` counterpart to `mapWire`.** This is the real cost: partner adapters
   already return `ApiResult` (`DashboardRepository.kt:204`); customer adapters return Retrofit
   `Response<T>` and build `ApiResult` in the repository (`OrderApi.kt:116-118`,
   `OrderRepository.kt:120-126`). **Ruled: add the counterpart** (smallest change that closes the
   attribution gap). Converging the customer adapters onto `safeApiCall`/`ApiResult` is the ADR-0011
   direction and is **not this ticket's job**.
4. Customer mappers become total: `toAppDto(): T?` → `toAppDto(): T` with `.required("field")`; the
   `?: ApiResult.Success(Unit)` / `?: networkError()` sites on 2xx bodies go.
5. iOS inherits the `:core` shape as a Swift equivalent when it ports — **with no decision left to
   make**, which is why T-0589 was filed before the port rather than after.
6. Narrow `mapWire`'s `runCatching` to `WireContractViolation` so a genuine mapper bug is not reported
   as a wire violation (ADR-0048 CH-2 concession).

## Invariants (what a reviewer enforces)

1. **Read what the mapper does, never whether one exists.**
2. **Check the spec, not the generated type** — and for a `$ref` field, the spec's silence means
   nothing; read the C# property.
3. **The rollup ruling is MADE for this surface**, in the mapper's doc comment. An inherited ruling with
   no sentence about where the number comes from is a finding even when the outcome is right.
4. **The refusal transport is the shared wrapper.** A 2xx body resolving to `ApiResult.Success` or to
   `ApiError.Network` is a finding.
5. **The wire test drives the mutation**: removing a `nullable: false` money key must fail the mapping,
   and the `@SerialName` set must equal the spec's property set.

## Known gaps (accepted, named)

| # | Gap | Bound |
|---|---|---|
| 1 | The enforcer is a **closed roster** of per-repository wire tests; a new repository with a coercing mapper is caught by nothing | inherent to the line-based checker — deciding it needs the spec's nullability for the schema the mapper targets. Widening = one wire test per repository. |
| 2 | The spec is regenerated by an **owner-only** step, so a committed spec can lag the running host | every wire test asserts against the committed spec; a stale spec makes the test agree with the wrong contract. |
| 3 | iOS has **neither** idiom today | closed by migration step 5; until then the iOS repositories are outside both the rule and its enforcer. |
| 4 | Partner has **no error-reporting sink**, so no refusal is observable off-device | §"reaches triage" above; `Q-OBS-01`. |

## Open questions / future evolution

- **The one change that would delete this whole class** is emitting `required` arrays in the mobile
  specs, so the generator types the fields correctly. It is an owner-only `mobile-spec-regen` change
  that re-types every generated model on two apps at once, and every mapper would still need writing to
  survive the interim — so it is rejected *here*, not foreclosed. **If the generated models ever become
  non-nullable, §D1's rules 1–3 collapse into the type system, and the right response is a superseding
  ADR rather than an amendment to ADR-0048.** *(Phrased this way deliberately: `ADR-0048 is superseded`
  would read to `check-catalog-claims.mjs` C1 as a status claim — id, connector `is`, status token —
  and disagree with the `accepted` on that ADR's own line 3. A conditional about a future event must
  not be written in the shape of a claim about today.)*
- **A hand-written `@Serializable` DTO with non-nullable members** (customer `OrderDtos.kt:43`) moves
  the refusal into `kotlinx.serialization`. It is kept where it already exists but is not the general
  answer: the thrown message is a serialization diagnostic rather than a domain one, and it cannot
  express the drop-the-row ruling at all.
