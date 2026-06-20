# Mobile repository result contract — living decision notes

> Companion to the **immutable** `agents/backlog/adr/0011-mobile-apiresult-contract.md` (ADR-0011).
> The ADR is the frozen contract; this file is the *evolving* design notes, trade-off space, and current
> shape. Update this when the decision evolves; supersede the ADR for a real contract change.
> Cross-links: consistency.md **E5**/**E3**, `check-consistency.mjs` rule E5, and (dev/published view) the
> mobile sections of `docs/architecture/*` once iOS lands.

## Current shape (as of ADR-0011, accepted 2026-06-15)

The mobile repo→ViewModel error channel is **one contract across all clients**, deliberately a shared seam:

```
Retrofit Response<T>
      │  safeApiCall { … } / networkCall { … }   (constructs the result; re-throws CancellationException)
      ▼
ApiResult<T>           ◀── the ONE repository contract, lives in :core (cz.cleansia.core.network)
   Success(data)                              partner-app ─┐
   Error(ApiError)                            customer-app ─┼─ all import the SAME type from :core
        │                                     iOS (Result<T, ApiError>) ─┘  (Swift mirror, D4)
        ▼
ViewModel  ──onError { snackbar.showError( localizer.translate(it) ) }──▶  one snackbar per failure
                          ▲
                   app-LOCAL localizer (ApiErrorTranslator / ApiErrorParser), stays per-app — depends on R
```

Why the contract is the asset: a server error shape is interpreted **identically** by partner-app,
customer-app, and the incoming iOS app; the data layer is UI-free and unit-testable (no `Context`); and a new
client starts from the same type instead of re-deriving one. ADR-0011 fixes the contract's *shape*, its
*home* (`:core`), and the *iOS equivalent*; it does not change the partner repos that already use it.

### The split: shared contract vs. per-app localization

| Concern | Where | Why |
|---|---|---|
| `ApiResult<T>` (sealed `Success`/`Error(ApiError)` + `map`/`onSuccess`/`onError`) | **`:core`** `cz.cleansia.core.network` | pure (`kotlinx`/Retrofit only) — no `Context`/`R`; every client shares it |
| `ApiError` (sealed `Network`/`Server`/`Unauthorized`/`NotFound`/`BadRequest`/`Unknown`) + `ApiErrorResponse` wire DTO | **`:core`** `cz.cleansia.core.network` | serialization-only; the case set is the cross-client interpretation contract |
| `safeApiCall` (Retrofit `Response<T>` → `ApiResult<T>`, re-throws `CancellationException`) | **`:core`** `cz.cleansia.core.network` | pure; the canonical construction site |
| `ApiErrorTranslator` (partner) / `ApiErrorParser` (customer) — `ApiError`/keys → localized string | **per-app** | depends on each app's `Context` + `R.string.*`; E3 = localize at the surfacing layer |
| The snackbar | **ViewModel**, never the repo | the data layer stays UI-free; one snackbar per failure (D2) |

### The four invariants that hold the contract together

1. **One result shape.** A mobile repo returns `ApiResult<T>` (`ApiResult<Unit>` for fire-and-forget) —
   never a nullable/sentinel body (`T?`, `Boolean`-as-success, `empty`-as-failure). Failure is the `Error`
   branch carrying a typed `ApiError`, not a value collision. (`check-consistency.mjs` rule E5.)
2. **One definition.** Exactly **one** `ApiResult` and **one** `ApiError` exist across
   `src/cleansia_android/**`, in `:core`. A customer-local or iOS-local fork is a blocking finding — the
   whole point is a single contract.
3. **The repo never touches UI.** No `snackbar.showError(...)` inside a repo. The ViewModel surfaces exactly
   one snackbar per failure (`onError`/`errorOrNull()`); deliberately-silent paths (background page-load,
   empty-picker) map `Error` to a VM no-op — same observable behavior.
4. **Localization is per-app, at the surfacing layer (E3).** The `ApiError → string` mapping lives in the
   app (it depends on that app's resources); the *contract* is shared. Pulling a localizer into `:core` would
   drag `R` into the shared module — forbidden.

### The iOS shape (born canonical — no iOS code yet, D4)

iOS repos return `ApiResult<T> = Result<T, ApiError>` (Swift stdlib `Result` is the natural carrier;
`ApiResult<Void>` for fire-and-forget). The Swift `ApiError` enum mirrors the Kotlin `ApiError` subclasses
**one-to-one** so the case set is identical across clients. The iOS ViewModel surfaces the message via an
**iOS-app-local** localizer (the `Localizable.strings` analogue of `ApiErrorTranslator`/`ApiErrorParser`).
A bespoke `enum ApiResult` is an allowed equivalent only if a future iOS need demands extra combinators, and
must stay shape-compatible with the fixed `ApiError` case set (recorded against ADR-0011, not forked).

## Trade-off space (what was rejected and why — see ADR-0011 §Alternatives)

- **Canonicalize customer-app's `T?`-with-snackbar-in-repo** — rejected (the E5 judgment call): buries the
  snackbar in the data layer, discards the typed error (no retry / no 401-vs-404), collides failure with
  empty success.
- **Leave `ApiResult` in partner-app; each app/iOS defines its own** — rejected: the exact drift this
  prevents (three subtly-different result types, three `ApiError` sets, one error interpreted three ways).
- **New `:network` Gradle module** — rejected at this size: `:core` already hosts `NetworkCall.kt` + the
  network deps + the auth stack and both apps already depend on it; a new module is pure overhead. Documented
  future option only if `:core` must split for build-time reasons.
- **Move the localizers into `:core` too** — rejected: they depend on each app's `R`; E3 keeps localization
  per-app.
- **Defer the iOS contract** — rejected: deferring guarantees iOS copies the legacy `T?`; fixing the Swift
  shape now costs nothing (zero iOS code) and makes iOS born-canonical.

## Current rollout state

| Step | Ticket | State (2026-06-15) |
|---|---|---|
| ADR (contract + `:core` home + iOS shape) | T-0197 AC1 | **accepted** (ADR-0011) |
| Move `ApiResult`/`ApiError`/`safeApiCall` → `:core`; re-point partner's 38 imports | T-0197 AC2 | pending (next consumer ticket) |
| Migrate each customer-app repo + its VMs (serial, characterization-test-first) | T-0197 AC3–AC7 | pending — one serial child per repo (`orders`, `disputes`, address, `memberships`, `loyalty`, `referral`, `recurring`, `notifications`, `payments`, `catalog`, `user`, `auth`, `settings`) |
| iOS Swift repos | future | not started — first iOS repo cites ADR-0011 D4 |

**Out of scope of this decision (separate tickets):** E1/E2 sealed-UiState migration, E7 directory
unification, any behavior change (no new messages, no retry UX, no caching/pagination change). This decision
changes **only** the repo→VM error channel.

## Open questions / future evolution

- If `:core` ever needs to split for build-time reasons, the contract may move to a dedicated `:network`
  module — that is a living-doc note (the home changes, the contract doesn't) unless the contract shape
  itself changes (then: a new ADR superseding ADR-0011).
- When the first iOS repo is written, confirm the Swift `ApiError` case set still matches the Kotlin one
  one-to-one; if a server error shape forces a new case, add it to **both** clients in the same change (a
  per-client-only case is the drift invariant #2 forbids).
