# ADR-0011 — The mobile repository result contract: `ApiResult<T>` canonical, hoisted to shared `:core`, and the born-canonical iOS Swift equivalent

- **Status:** accepted   <!-- proposed | accepted | superseded | rejected -->
- **Date:** 2026-06-15
- **Supersedes:** —
- **Superseded by:** —
- **Applies to:** mobile (android) | ios | cross-cutting
- **Ratifies:** consistency rule **E5** (`agents/knowledge/consistency.md:161-166`) as the binding mobile repository contract, and clarifies **E3** (`:154-157`) — error is localized at the layer that surfaces the snackbar (the ViewModel for an `ApiResult` repo).
- **Ticket:** T-0197 (this ADR — AC1) · **Consumers:** the `:core` type move + partner import re-point (T-0197 AC2), then one **serial** child per customer-app repo + its ViewModels (T-0197 AC3–AC7), then the iOS contract note when iOS repos are first written.

> **One decision — "the mobile repository result contract."** A mobile repository **returns
> `ApiResult<T>`** (the sealed `Success`/`Error(ApiError)` type with `map`/`onSuccess`/`onError`) and the
> **ViewModel** surfaces the snackbar; that one type lives in the **shared `:core` module**
> (`cz.cleansia.core.network`) so partner-app, customer-app **and the incoming iOS app** consume a single
> contract; and the **iOS Swift equivalent** (a `Result`-like enum the future Swift repos return) is fixed
> here so iOS is *born* canonical instead of re-deriving customer-app's legacy `T?` form. This ADR ships
> **no production code** — it ratifies the E5 judgment call and fixes the type's home + the iOS shape; the
> `:core` move and per-repo migration are the consumer tickets. Once `accepted` it is immutable —
> supersede, never edit.

---

## Context

The mobile layer has the **same operation written two ways** — the exact threat the consistency catalog
exists to kill. The repository→ViewModel error channel disagrees between the two Android apps, and a
third client (iOS) is incoming with no contract yet. All facts below verified against the code on
2026-06-15.

**The canonical form already exists in partner-app.** `ApiResult.kt`
(`partner-app/src/main/java/cz/cleansia/partner/core/network/ApiResult.kt:7-32`) is a
`sealed class ApiResult<out T>` with `Success(data)` / `Error(ApiError)`, plus `isSuccess`/`isError`,
`getOrNull()`/`errorOrNull()`, and the inline `map`/`onSuccess`/`onError` combinators. `ApiError.kt`
(`:26-54`) is a sealed `Network`/`Server`/`Unauthorized`/`NotFound`/`BadRequest`/`Unknown` hierarchy plus
the `ApiErrorResponse` wire shape (ProblemDetails + the bespoke `{message,code,errors}` form). `safeApiCall`
(`SafeApiCall.kt:23-78`) wraps a Retrofit `Response<T>` into an `ApiResult<T>`, re-throwing
`CancellationException` (so a fast nav-away doesn't surface a phantom snackbar) and mapping
timeout/host/IO/HTTP into the right `ApiError`. **38 partner files** consume these from
`cz.cleansia.partner.core.network` (repos like `data/orders/OrdersRepository.kt:23-24` return
`ApiResult<…>`; VMs like `features/orders/viewmodels/OrderPhotosViewModel.kt:9-11,40-41` branch on
`Success`/`Error` and surface the snackbar via the injected `SnackbarController` + the app-local
`ApiErrorTranslator`).

**Crucially, all three moved types are pure** — `ApiResult`/`ApiError`/`safeApiCall` reference only
`kotlinx.serialization`, `kotlinx.coroutines`, and Retrofit's `Response`. **None touches `android.content.Context`,
`R`, or any partner-only symbol.** (`ApiErrorTranslator.kt:1-99` and the customer `ApiErrorParser.kt:36-97`
*do* take `Context`/`R` — they are the **app-specific localizers** and stay per-app; they are *not* part of
this move.)

**customer-app is the legacy form.** Every repo under
`customer-app/src/main/java/cz/cleansia/customer/core/**/*Repository.kt` returns a **nullable/sentinel**
(`OrderDetailDto?`, `CancelOrderResponse?`, `String?`, `Boolean`, `emptyList()`) and **surfaces the
snackbar inside the repo** — e.g. `core/orders/OrderRepository.kt:113-121` (`getById`),
`:131-140` (`cancel`), `:63-84` (`refresh`) each do `ApiErrorParser.parseToUserMessage(...)` →
`snackbar.showError(msg)` → return the failure sentinel. This buries a UI concern (the snackbar) in the
data layer, throws away the typed error (no retry, no "was it 401 vs 404"), and encodes failure as a
value collision (`null`/`false`/`empty` can also be a legitimate empty success). `check-consistency.mjs`
flags every such method with rule **E5** (`check-consistency.mjs:384-393`: a `suspend fun …): T?` that
isn't `ApiResult`/`Flow`/`Unit` → *"Repository returns a nullable body (legacy) — prefer ApiResult<T>
(tracked migration)"*).

**The shared module already exists and is wired.** `:core` (`core/build.gradle.kts`, namespace
`cz.cleansia.core`) already hosts `cz.cleansia.core.network.NetworkCall.kt` and already declares
Retrofit + `kotlinx-serialization-json` + `kotlinx-coroutines` (`:73-80`). **Both** apps already
`implementation(project(":core"))` (partner `build.gradle.kts:213`, customer `:292`). So the move target
is a real, depended-on home — no new module, no new dependency.

**E5 is an Architect-owned judgment call, not a majority pick.** consistency.md's "Judgment calls" section
(`:184`) records: *"E5 `ApiResult<T>`: the two apps disagree; we canonicalize on the **more explicit**
contract."* and `:191` fixes that **changing a judgment call is an ADR, not an ad-hoc reversal**. That is
why this ticket is ADR-first: the E5 rule is binding *as a rule*, but its platform-wide adoption (moving the
type cross-app + fixing the iOS shape) is a structural decision that must be ratified and defended, not
edited into a feature branch.

**iOS is incoming with no repos yet.** If iOS is left to start when its first repo is written, it will
look at whichever Android app it happens to read first — and customer-app's legacy `T?`-with-snackbar-in-repo
is the seductive-but-wrong shape (it looks simpler). Fixing the Swift contract *now*, in the same decision
that canonicalizes Android, is the cheapest moment to make iOS born-canonical — there is zero migration
cost because there is zero iOS code.

**This is one decision, not three.** "Canonicalize `ApiResult<T>`", "move it to `:core`", and "fix the iOS
equivalent" are inseparable facets of a single question — *what is the one mobile repository result
contract, and where does it live so every client shares it?* Splitting them would let the type be
canonical-but-partner-only (customer can't import it), or moved-but-not-canonical (no rule forcing the
migration), or Android-canonical-but-iOS-divergent (the exact drift this prevents). They ship as one ADR;
the *implementation* is split into serial consumer tickets (T-0197 AC2…N).

---

## Decision

> **Contract principle.** A mobile **repository returns `ApiResult<T>`** — the sealed
> `Success(data)` / `Error(ApiError)` type carrying the typed error — and **never** surfaces UI (no
> snackbar) from the data layer. The **ViewModel** branches on `Success`/`Error` and surfaces the single
> snackbar via the app-local localizer + `SnackbarController` (E3). That `ApiResult` / `ApiError` /
> `safeApiCall` type lives in **`:core` (`cz.cleansia.core.network`)** so partner-app, customer-app, and
> iOS consume **one** contract. iOS repos return the **Swift equivalent** (`Result`-like enum, D4) so the
> iOS app is born on this contract.

### D1 — `ApiResult<T>` is THE mobile repository contract (ratifies E5)

- A repository method that performs a network call **returns `ApiResult<T>`**, never a
  nullable/sentinel body (`T?`, `Boolean`-as-success, `emptyList()`-as-failure). The sealed
  `Success(data)` / `Error(ApiError)` shape is the *only* sanctioned repo result shape.
- **Fire-and-forget** operations (former `addMessage(): Boolean`, register/clear calls) return
  **`ApiResult<Unit>`** — `Success(Unit)` on the happy path, `Error(ApiError)` on failure. `Unit` is the
  payload, not `Boolean`; success/failure is the `Success`/`Error` branch, never a payload value.
- The repo **does not** call `snackbar.showError(...)`. On failure it returns
  `ApiResult.Error(ApiError(...))` built from the existing `ApiErrorParser.parseToUserMessage(...)`
  (customer) / the `safeApiCall` HTTP mapping (partner) — same message string as today, just carried in
  the `Error` instead of pushed to the snackbar.
- **`safeApiCall` is the construction site.** A repo builds its `ApiResult` from the existing
  `networkCall { … }` / `safeApiCall { … }` over the unchanged Retrofit `Response<T>`. **No wire change**:
  DTOs, the Retrofit interface, and JSON are untouched (T-0197 implementation note — no nswag-regen, no
  ef-migration).
- This **ratifies E5 verbatim** and makes it ADR-binding. customer-app's `T?` repos are the *legacy form
  to migrate*; the migration is a tracked, serial refactor (D5), not a same-day rewrite.

### D2 — The snackbar is surfaced by the ViewModel, not the repo (clarifies E3)

- The **ViewModel** consumes `ApiResult<T>` and surfaces exactly **one** snackbar per failure via
  `result.onError { snackbar.showError(localizer.translate(it)) }` (or `errorOrNull()` + an equivalent
  branch). The message is **the same single string** the repo used to show — behavior is observably
  identical; only *where* the error becomes a snackbar moves repo → VM.
- **Localization stays per-app, at the surfacing layer (E3).** `ApiErrorTranslator` (partner,
  `Context`/`R`-bound) and `ApiErrorParser` (customer, `Context`/`R`-bound) are **app-specific** and stay
  in their app. The *contract* (`ApiResult`/`ApiError`) is shared; the *localization of an `ApiError` into
  a user string* is app-local because it depends on that app's `R.string.*` resource set. This is the E3
  judgment call applied: localize at the layer that surfaces the snackbar — for an `ApiResult` repo, that
  layer is the ViewModel.
- **Silent/background paths stay silent.** Where a repo today swallows errors deliberately
  (`OrderRepository.loadNextPage` `:91-107`, `getMyServingCleaners` `:230-233` — background page-load /
  empty-picker), the migration maps that `Error` to a **no-op in the VM** (no snackbar), preserving the
  identical silent behavior. The contract change must not turn a deliberately-silent failure into a toast.

### D3 — `ApiResult` / `ApiError` / `safeApiCall` move to `:core` (`cz.cleansia.core.network`)

- The three **pure** types move from `partner-app/core/network` into the existing shared `:core` module,
  package **`cz.cleansia.core.network`** (alongside `NetworkCall.kt`). They take no Android `Context`/`R`
  dependency, so the move is mechanical (package rename + import re-point), behavior-preserving.
- **partner-app re-points its imports** (`cz.cleansia.partner.core.network.{ApiResult,ApiError,safeApiCall}`
  → `cz.cleansia.core.network.*`) across the 38 consuming files — **no behavior change** to partner repos
  or VMs. This is T-0197 **AC2**; both apps build green after it. The app-local `ApiErrorTranslator` /
  `ApiErrorParser` and the `ApiErrorResponse` wire DTO *if it needs `R`* stay per-app; the pure
  `ApiErrorResponse` (serialization-only) moves with `ApiError`.
- **customer-app then consumes the moved type** as it migrates each repo (D5) — it imports
  `cz.cleansia.core.network.ApiResult` rather than re-deriving a customer-local copy. **No second
  `ApiResult` is ever created.** A future "customer needs its own result type" is a smell to escalate, not
  to fork.
- **Why `:core`, not a new `:network` module:** `:core` already hosts `NetworkCall.kt` + all the network
  deps + the auth/token stack, and both apps already depend on it. A new module is pure overhead (extra
  Gradle wiring, a second seam to keep aligned) for zero benefit at this size — revisit only if `:core`
  later needs to split for build-time reasons (D6).

### D4 — The iOS Swift result contract (born canonical, no iOS code yet)

iOS has **no repos yet**; this fixes the Swift equivalent so the first iOS repo is written on the canonical
shape. The Swift contract mirrors `ApiResult`/`ApiError` one-to-one (it is the *same decision*, expressed
in Swift's idiom — Swift's stdlib `Result<Success, Failure>` is the natural carrier):

```swift
// cz.cleansia core (iOS) — the canonical repository result contract.
// Mirror of Kotlin ApiResult<T> / ApiError. iOS repos return this; the
// ViewModel surfaces the user-facing message (snackbar/alert), never the repo.

enum ApiError: Error {
    case network(message: String)
    case server(statusCode: Int, message: String)
    case unauthorized
    case notFound(message: String)
    case badRequest(message: String, code: String?, validationErrors: [String: [String]]?, errorKey: String?)
    case unknown(message: String)
}

// Repos return Swift's stdlib Result specialized to ApiError — the direct
// analogue of the sealed Success/Error(ApiError). Fire-and-forget returns
// ApiResult<Void> (the Unit analogue). map/flatMap/get() come from stdlib;
// `onError`/`onSuccess` are thin extensions mirroring the Kotlin combinators.
typealias ApiResult<T> = Result<T, ApiError>

extension Result where Failure == ApiError {
    @discardableResult func onSuccess(_ action: (Success) -> Void) -> Self { if case .success(let v) = self { action(v) }; return self }
    @discardableResult func onError(_ action: (ApiError) -> Void) -> Self { if case .failure(let e) = self { action(e) }; return self }
}
```

Rules that bind the future iOS app:
- **iOS repos return `ApiResult<T>` (= `Result<T, ApiError>`)**; fire-and-forget returns `ApiResult<Void>`.
  No iOS repo returns an optional-as-failure (`T?`) or throws raw — failure is `.failure(ApiError)`.
- **The iOS ViewModel surfaces the message** (snackbar/alert via the iOS localizer), exactly as D2 — the
  repo never touches UI. The `ApiError → localized String` mapping is the **iOS-app-local** analogue of
  `ApiErrorTranslator`/`ApiErrorParser` (it depends on iOS `Localizable.strings`), kept in the iOS app.
- The **`ApiError` cases match the Kotlin `ApiError` subclasses one-to-one** so a server error shape is
  interpreted identically across all three clients — no client invents a case the others lack.
- **No iOS source is written by this ADR or T-0197.** This is the *contract note* so iOS is born canonical;
  the first iOS repo ticket cites this D4. Choosing `enum ApiResult` (a bespoke type) over the stdlib
  `Result` typealias is an allowed equivalent if a future iOS need (e.g. extra combinators) demands it —
  but it must stay shape-compatible with this `ApiError` case set, and that choice is recorded against this
  ADR, not forked silently.

### D5 — Rollout: serial, characterization-test-first, behavior-preserving

- The `:core` move (AC2) lands first; partner re-points imports; both apps green.
- Then **one child ticket per customer-app repo + its ViewModels**, run **serially** — never two repo
  children in parallel (each repo file + its VM files is a distinct cluster, so serialization avoids merge
  collisions). The order is the T-0197 implementation-notes list (`orders`, `disputes`, `data`/address,
  `memberships`, `loyalty`, `referral`, `recurring`, `notifications`, `payments`, `catalog`, `user`,
  `auth`, `settings`).
- Each child is **characterization-test-first** (testing.md): pin current observable behavior (success
  returns the body; failure fires exactly one snackbar with the `ApiErrorParser`-derived message and
  yields the failure sentinel) → see it green → migrate the repo to `ApiResult<T>` + move the snackbar to
  the VM → the **same** test still asserts the **same** success/failure outcome and the **same single**
  message (now raised by the VM). Behavior is observably identical end-to-end.
- **Out of scope of every child:** the E1/E2 sealed-UiState migration (separate Wave-3 tickets — this
  changes the repo→VM *error channel*, not the UiState shape), E7 directory unification, and any behavior
  change (no new messages, no retry UX, no caching/pagination change). Same successes, same failures, same
  single snackbar.

### D6 — Scope guard

This ADR ratifies the contract + its `:core` home + the iOS shape. It does **not**: migrate any
customer-app repo (those are the serial children), refactor partner repo logic (partner is canonical;
touched only to re-point imports), do the E1/E2/E7 migrations, or write any iOS code. A future split of
`:core` into a dedicated `:network` module, or an iOS need for a bespoke `enum ApiResult`, are revisited
against this ADR (a new ADR if it changes the contract; a living-doc note if it's only the type's home).

---

## Alternatives considered

- **Canonicalize customer-app's `T?`-with-snackbar-in-repo instead (the "simpler" form).** Rejected — this
  is the core of the E5 judgment call. `T?` buries a UI concern (the snackbar) in the data layer (untestable
  without an Android `Context`, un-reusable from a non-snackbar caller), throws away the typed error (no
  retry, no 401-vs-404 branch), and collides failure with legitimate empty success (`null`/`false`/`empty`).
  `ApiResult<T>` carries the error explicitly, enables retry, and keeps the data layer UI-free. The "simpler"
  form is simpler only until you need to branch on *why* it failed — which every non-trivial screen does.
- **Leave `ApiResult` in partner-app; have customer-app/iOS each define their own.** Rejected — that is the
  drift this ADR exists to prevent: three subtly-different result types, three `ApiError` case sets, the same
  server error interpreted three ways. One contract in `:core` (which both apps already depend on) is the
  whole point of having a shared module.
- **Create a new `:network` Gradle module for the contract.** Rejected at this size — `:core` already hosts
  `NetworkCall.kt` + every network dependency + the auth/token stack, and both apps already depend on it. A
  new module is pure wiring overhead and a second seam to keep aligned for zero benefit. Left as a documented
  future option (D6) only if `:core` must split for build-time reasons.
- **Move the localizers (`ApiErrorTranslator`/`ApiErrorParser`) into `:core` too, for "one error→string
  path".** Rejected — they depend on each app's `Context`/`R.string.*` resource set (partner and customer
  have different resource ids and different validation-key joining), so they are legitimately app-specific.
  E3 says localize at the surfacing layer; the *contract* (`ApiResult`/`ApiError`) is shared, the
  *localization* is per-app. Forcing them into `:core` would drag `R` into the shared module — the opposite
  of clean.
- **Defer the iOS contract until the first iOS repo is written.** Rejected — that is precisely when iOS would
  copy whichever Android app it reads first, and customer-app's legacy `T?` is the seductive-wrong shape.
  Fixing the Swift shape now (D4) costs nothing — zero iOS code exists — and makes iOS born-canonical instead
  of needing its own migration later. The cheapest moment to set a contract is before any code depends on it.
- **Define iOS as a bespoke `enum ApiResult` rather than reusing Swift's stdlib `Result`.** Considered; left
  as an *allowed equivalent* (D4) rather than the default. Swift's stdlib `Result<Success, Failure>` already
  *is* the sealed Success/Error shape, with `map`/`flatMap`/`get()` for free — reusing it is idiomatic and
  less code. A bespoke enum is sanctioned only if a future iOS need demands extra combinators, and must stay
  shape-compatible with this `ApiError` case set.
- **Do the contract move and the customer migration in one big-bang ticket.** Rejected — it is `L`, spans
  every customer repo + VM, and the catalog mandates characterization-test-first behavior-preservation. A
  big-bang change can't be pinned green incrementally and risks merge collisions across repos. D5's serial,
  one-repo-per-child rollout is the safe shape (and is why this ADR exists before any code).

---

## Consequences

**Cheaper / safer:**
- **One contract, three clients.** partner-app, customer-app, and iOS share a single `ApiResult`/`ApiError`
  — a server error shape is interpreted identically everywhere; a new client (the next platform) starts from
  the same type instead of re-deriving one.
- **Typed errors unlock retry and branch-on-cause** without re-plumbing — the `Error(ApiError)` already
  carries 401-vs-404-vs-network, which a `T?` threw away.
- **The data layer is UI-free and testable** without an Android `Context` — repos can be unit-tested by
  asserting the returned `ApiResult`, not by mocking a snackbar.
- **iOS is born canonical** — zero migration cost paid now instead of a customer-app-style migration later.

**More expensive (new obligations):**
- Every new mobile repo method returns `ApiResult<T>` (`Unit` for fire-and-forget); a `T?`/sentinel return
  is an E5 finding (`check-consistency.mjs:384-393`).
- The consuming ViewModel must surface the snackbar (`onError`/`errorOrNull()`) — the repo no longer does it.
  A repo that still calls `snackbar.showError(...)` after migration is a finding.
- The customer-app migration is a **serial, characterization-test-first** refactor (D5) — one repo+VM cluster
  per child ticket, behavior pinned green before and after. This is slower than a big-bang but is the only
  safe shape for a behavior-preserving cross-app change.
- iOS repos (when written) must return `Result<T, ApiError>`/`ApiResult<Void>` and surface the message in the
  ViewModel; the `ApiError` case set is fixed by D4 and may not be silently extended per-client.
- **No `manual_step`** — mobile-only client refactor; no nswag-regen, no ef-migration, no wire change.

**Rollout (consumers, each test-first):**
- **T-0197 AC2:** move `ApiResult`/`ApiError`/`safeApiCall` to `cz.cleansia.core.network`; re-point
  partner's 38 imports; both apps green, no behavior change.
- **T-0197 AC3–AC7 (serial children):** one customer-app repo + its VMs per child, characterization-green →
  migrate → green; E5 clean for the touched area; clear the entry in
  `agents/backlog/audits/consistency-violations.md`.
- **iOS (future):** first iOS repo ticket cites D4 for the Swift contract.

---

## How a reviewer verifies compliance

**Mechanical (the gate — already partly in `check-consistency.mjs`):**
1. **No legacy `T?`/sentinel repo return.** `check-consistency.mjs` mobile pass rule **E5**
   (`:384-393`) flags `suspend fun …): T?` that isn't `ApiResult`/`Flow`/`Unit`. After each child,
   `node agents/tools/check-consistency.mjs mobile --paths=src/cleansia_android/customer-app` reports
   **zero E5** for the touched repo.
2. **`ApiResult` resolves from `:core`.** After AC2, grep for
   `import cz.cleansia.partner.core.network.{ApiResult|ApiError|safeApiCall}` → **zero matches** (all
   re-pointed to `cz.cleansia.core.network.*`); the type is defined **once**, under
   `core/src/main/java/cz/cleansia/core/network/`.
3. **No second `ApiResult`.** Grep `sealed class ApiResult` / `sealed (class|interface) ApiError` across
   `src/cleansia_android/**` → exactly **one** definition each, in `:core`. A customer-local or iOS-local
   redefinition is a blocking finding (D3/D4).
4. **Repos don't snackbar.** In a migrated customer repo, grep `snackbar.showError(` / any
   `SnackbarController` use inside `*Repository.kt` → **zero** (the snackbar moved to the VM, D2). The VM
   carries exactly one `onError { … showError … }` per failure path.
5. **Localizers stayed per-app.** `ApiErrorTranslator` (partner) / `ApiErrorParser` (customer) are **not**
   in `:core` (they depend on `R`) — assert they remain under their app's package (D2/D3).

**Test contract (consumer tickets, red first):**
6. **TC-APIRESULT-0 (characterization).** Before migrating a repo method: success returns the body; failure
   fires **exactly one** snackbar with the `ApiErrorParser`-derived message and the call yields the failure
   sentinel. Green before any production change.
7. **TC-APIRESULT-1 (behavior identical post-migration).** After migrating: the method returns
   `ApiResult.Success(body)` / `ApiResult.Error(ApiError(...))`; the **VM** raises the **same single** message
   on `Error`; a deliberately-silent path (background page-load) raises **no** snackbar. Same success, same
   failure, same one message as TC-APIRESULT-0.
8. **TC-UNIT-FIREFORGET.** A former `Boolean`/fire-and-forget method returns `ApiResult<Unit>` —
   `Success(Unit)` on the happy path, `Error` on failure; success is the branch, not a `true` payload.

**iOS (future, D4):** the first iOS repo returns `Result<T, ApiError>` / `ApiResult<Void>`; the iOS VM (not
the repo) surfaces the alert; the `ApiError` cases match this ADR's set one-to-one.

---

## Roles affected

Role files in `agents/knowledge/roles/` (CRC cards — added when the consumer tickets land so they reflect the
moved code):
- **`mobile-result-contract.md`** (new, cross-cutting CRC) — *responsibility:* define, once for all mobile
  clients, the single repository result type (`ApiResult<T>` = sealed `Success`/`Error(ApiError)`) and where
  it lives (`:core`). *Collaborators:* `safeApiCall`/`networkCall` (constructs it), the Retrofit
  `Response<T>` (its input), the consuming ViewModel (surfaces the snackbar). *Does NOT know:* how an
  `ApiError` is turned into a user-facing string (that is the app-local localizer's job), the snackbar
  channel (that is the ViewModel/`SnackbarController`), or any app's `R.string.*` set.
- **`mobile-error-localizer.md`** (clarified — partner `ApiErrorTranslator` / customer `ApiErrorParser`) —
  *responsibility:* map an `ApiError` (+ server validation keys) to a localized user string for **its app**.
  *Collaborators:* the app's `R.string.*`, the consuming ViewModel. *Does NOT know:* the network/Retrofit
  layer, the `ApiResult` construction, or the other app's resource set (it is deliberately per-app — this is
  why it stays out of `:core`).

Catalog edit (same change as this ADR, per the pattern-evolution loop): `agents/knowledge/consistency.md
§E5` gains "ratified by ADR-0011 (`ApiResult<T>` is THE contract; moved to `cz.cleansia.core.network`; iOS
Swift equivalent fixed)"; the living companion `agents/architecture/decisions/mobile-result-contract.md` is
created/updated in parallel.

---

## Challenge / Defense / Verdict trail (condensed)

Author drafted; challengers (pragmatic, seam/coupling, cross-platform) attacked; the Lead re-verified every
citation against the real code and adjudicated. **Verdict: all challenges RESOLVED; zero blocking; consensus
reached.**

| # | Challenge (severity) | Disposition | Where |
|---|---|---|---|
| CH-1 (pragmatic) | customer-app's `T?` is simpler and already works — is `ApiResult` over-engineering for a small client? (MODERATE) | REBUT | Alternatives §1 + Context: `T?` buries the snackbar in the data layer (untestable w/o `Context`), discards the typed error (no retry / no 401-vs-404), and collides failure with empty success. The explicit contract is the E5 judgment call. |
| CH-2 (coupling) | Moving `ApiResult` to `:core` risks dragging `R`/`Context` into the shared module → coupling (MAJOR) | REBUT + REVISE | D3 + Context: the three moved types are **pure** (`kotlinx`/Retrofit only — verified `ApiResult.kt`/`ApiError.kt`/`SafeApiCall.kt`). The `Context`/`R`-bound localizers (`ApiErrorTranslator`/`ApiErrorParser`) explicitly **stay per-app**. Alternatives §4 records why. |
| CH-3 (scope) | A new `:network` module would be cleaner than overloading `:core` (MODERATE) | DEFEND | Alternatives §3 + D3: `:core` already hosts `NetworkCall.kt` + every network dep + the auth stack, and both apps already depend on it. A new module is pure overhead at this size; left as a documented future option (D6). |
| CH-4 (behavior) | Moving the snackbar repo→VM could turn a deliberately-silent failure (background page-load) into a toast → behavior change (MAJOR) | CONCEDE + REVISE | D2 silent-path rule: `loadNextPage`/`getMyServingCleaners` map `Error` to a **no-op in the VM**; characterization test (TC-APIRESULT-1) asserts **no** snackbar on silent paths. Behavior observably identical. |
| CH-5 (cross-platform) | Why fix the iOS contract now with zero iOS code — isn't that speculative? (MODERATE) | REBUT | Alternatives §5 + D4: this is the *cheapest* moment (zero migration cost). Deferring guarantees iOS copies customer-app's legacy `T?`. Fixing the Swift shape now makes iOS born-canonical; no iOS code is written. |
| CH-6 (cross-platform) | A bespoke Swift `enum ApiResult` vs stdlib `Result` — which binds iOS? (MODERATE) | DEFEND + REVISE | D4: stdlib `Result<T, ApiError>` is the default (it *is* the sealed shape, free combinators); a bespoke enum is an allowed equivalent only if a future need demands it, must stay shape-compatible with the fixed `ApiError` case set, and is recorded against this ADR. |
| CH-7 (drift) | What stops customer-app or iOS from forking a second `ApiResult` later? (MAJOR) | CONCEDE + REVISE | Verification #3: `check-consistency`/grep asserts **exactly one** `ApiResult`/`ApiError` definition across `cleansia_android/**`; a second is a blocking finding. D3/D4 forbid a per-client fork. |

**Affirmed unchallenged:** E5's `ApiResult`-over-`T?` judgment call (already Architect-owned, ratified here);
the moved types are pure and the move is mechanical/behavior-preserving; partner is canonical and touched only
to re-point imports; the migration is serial + characterization-test-first; no wire/`manual_step` impact.

**Lead re-verification (against current code, 2026-06-15):**
`partner-app/core/network/ApiResult.kt:7-32` (sealed `Success`/`Error(ApiError)` + `map`/`onSuccess`/`onError`);
`ApiError.kt:26-54` (sealed `ApiError` + `ApiErrorResponse`, serialization-only — no `Context`/`R`);
`SafeApiCall.kt:23-78` (Retrofit `Response<T>` → `ApiResult<T>`, re-throws `CancellationException`);
`core/network/NetworkCall.kt:1-61` already in `:core` (`cz.cleansia.core.network`); `core/build.gradle.kts`
already declares Retrofit + serialization + coroutines; partner `build.gradle.kts:213` + customer `:292`
already `implementation(project(":core"))`; 38 partner files import the type from
`cz.cleansia.partner.core.network`; `customer-app/core/orders/OrderRepository.kt:113-121,131-140,63-84`
legacy `T?` + in-repo `snackbar.showError`; `customer/core/auth/ApiErrorParser.kt:36-97` + partner
`ApiErrorTranslator.kt:1-99` are the `Context`/`R`-bound app-local localizers; `check-consistency.mjs:384-393`
is the E5 rule; consistency.md `:161-166` (E5), `:154-157` (E3), `:184,191` (judgment-call ownership).

**Escalations to the owner:** none. The mobile result contract, its `:core` home, and the iOS shape are
architecture-pattern decisions within the Architect's mandate (E5 is already an Architect-owned judgment
call). No product/business window is invented; iOS *sequencing* (when iOS repos are written) is an existing
owner roadmap item, not a decision this ADR makes.
