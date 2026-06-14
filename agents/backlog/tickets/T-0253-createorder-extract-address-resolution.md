---
id: T-0253
title: "AUD-06a — extract address-resolution + serviced-area collaborator out of CreateOrder.Handler"
status: done
size: M
owner: backend
created: 2026-06-13
updated: 2026-06-14
depends_on: [T-0118, T-0212]
blocks: [T-0254]
stories: []
adrs: [0002]
layers: [backend]
security_touching: false
manual_steps: []
sprint: 5
source: T-0199 split (Batch 5D sub-step a); AUD-06
---

## Context
Child of the **T-0199 (AUD-06)** CreateOrder god-handler decomposition (Batch **5D — RUNS ALONE on the
`CreateOrder.cs` cluster**). `src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs` injects 15
dependencies and braids four unrelated concerns inline. This first sub-step lifts out the **address-resolution +
serviced-area gating** concern:

- `ResolveAddressAsync` (saved-vs-inline address, ownership check, country `IsServicedAsync`, single-serviced-
  country fallback),
- the inline `serviceCityRepository.CityIsServicedAsync` city check,
- `addressGeocoder.PopulateCoordinatesAsync`.

These move into a named collaborator (e.g. an address-resolution / serviced-area service) behind an interface,
injected into the handler and registered in DI. **This is a refactor, NOT a behavior change** — the externally
observable behavior (success `Response`, every `BusinessResult.Failure` error code, side-effect ordering) stays
**identical**.

**CRITICAL ACCEPTANCE GATE:** **T-0212's CreateOrder characterization suite**
(`src/Cleansia.Tests/Features/Orders/CreateOrder*CharacterizationTests.cs`, 20 cases, Wave-4 4A) is the green
regression net. It MUST be green before the first decomposition commit and **stay green UNCHANGED** through this
sub-step (AC1/AC3). A diff that modifies T-0212's assertions to make them pass fails review.

**Sequencing:** depends on **T-0118✓** (the Cash-branch enqueue is its post-commit dispatch / outbox seam,
preserved by sub-step c) and **T-0212✓** (the net). `blocks: [T-0254]` — the three AUD-06 sub-steps land
**serially** on `CreateOrder.cs`; this is the first. Rebase target: current `master` (`ee95a57f`, post-Wave-4).

## Acceptance criteria
- [ ] **AC1 (T-0212 net green, unchanged)** — Before this sub-step's first commit, T-0212's CreateOrder
  characterization suite is green against the **unchanged** handler (status log records it). The suite file is
  **not modified** by this ticket.
- [ ] **AC2 (decomposition — address concern extracted)** — `ResolveAddressAsync`, the city-serviced check, and
  geocoding are extracted into a named collaborator behind an interface, injected into the handler and
  registered in DI. The handler's direct dependency count drops; no new collaborator reconstitutes the god-unit.
  The extracted unit has its own unit tests.
- [ ] **AC3 (behavior identical)** — T-0212 re-runs **unchanged** and is **still green** — same success shape,
  same error codes (`NotFound` saved-address missing/cross-user, `CountryNotServiced`, `CountryRequired`,
  `CityNotServiced`), same side-effect ordering (address resolve → city/country gate → geocode → …).
- [ ] **AC4 (consistency clean)** — `node agents/tools/check-consistency.mjs backend --paths=src/Cleansia.Core.AppServices/Features/Orders`
  reports zero new violations; the collaborator follows §B canon (B7 rich domain methods, B9 `entity.MapToDto()`
  — no inline projection re-introduced).
- [ ] **AC5** — `dotnet test src/Cleansia.Tests` green; Reviewer confirms refactor-only and no `Command`/`Response`
  contract change → **no nswag-regen, no migration**.

## Out of scope
- Promo preview/apply extraction (sub-step b, T-0254) and payment-side-effect/late-referral extraction +
  final handler slim-down (sub-step c, T-0255).
- Any `Command`/`Response` wire shape, endpoint, or validator-behavior change; adding the missing CreateOrder
  idempotency guard (B8 gap — behavior change, tracked elsewhere); re-architecting the post-commit dispatch /
  outbox (owned by T-0118).

## Implementation notes
- **TEST-FIRST / characterization net:** T-0212 is the net (already shipped) — do not advance until it is green
  against the pre-refactor handler; keep it green and unmodified through the refactor (Gate 6).
- **Canonical pattern:** `knowledge/consistency.md` §B — B7 rich domain methods (no direct entity property sets
  in the collaborator), B8 narrow provider-specific try/catch preserved, B9 `entity.MapToDto()`. The collaborator
  is a plain injected service behind an interface, registered in DI alongside the existing Orders services.
- **Serialization — LANE-ISOLATED:** `CreateOrder.cs` is a serialized shared-file cluster. No other ticket
  touching `CreateOrder.cs` runs concurrently. The three AUD-06 sub-steps run **serially** (a → b → c); the
  collaborator's DI registration touches the Orders-feature registration — serialize against any concurrent
  Orders-feature DI edit (none this wave). 5D can run in parallel with 5B/5C/5E (none edit `CreateOrder.cs`).

## Status log
- 2026-06-13 — ready (created by pm — split of T-0199 / AUD-06, Batch 5D sub-step a). DoR met: AC observable
  (T-0212 stays green), sized M, deps T-0118✓/T-0212✓ done, no migration/regen, refactor-only, lane-isolated on
  `CreateOrder.cs`, `blocks: [T-0254]` (serial a→b→c). Reviewer-per-developer.
- 2026-06-13 — review (backend). Behavior-preserving extraction of the address-resolution + serviced-area +
  geocode concern out of `CreateOrder.Handler` into a named collaborator behind an interface, DI-registered.
  - New: `Features/Orders/IOrderAddressResolver.cs` (interface + `OrderAddressResolution` result record),
    `Features/Orders/OrderAddressResolver.cs` (sealed impl, mirrors the existing `IOrderFactory`/`OrderFactory`
    idiom in the same folder). Logic lifted verbatim — same step order (resolve → city gate → geocode) and same
    error codes (`NotFound`, `CountryNotServiced`, `CountryRequired`, `CityNotServiced`).
  - `CreateOrder.Handler` ctor deps dropped 15 → 11 (removed `IAddressRepository`, `ISavedAddressRepository`,
    `ICountryRepository`, `IServiceCityRepository`, `IAddressGeocoder`; added `IOrderAddressResolver`). The
    private `ResolveAddressAsync` + `AddressResolution` record were removed (moved to the collaborator).
    `Command`/`Response`/`ICommandHandler<Command,Response>` contract UNCHANGED.
  - DI: `services.AddScoped<IOrderAddressResolver, OrderAddressResolver>()` added in
    `Cleansia.Config/Services/ServiceExtensions.cs` directly after the `IOrderFactory` registration.
  - **AC4 consistency:** `check-consistency.mjs backend --paths=…/Features/Orders` → `OK (50 files scanned)`,
    zero new violations.
  - **TEST EVIDENCE:**
    - AC1/AC3 — T-0212 net stays green and assertions UNMODIFIED. The only edit to
      `CreateOrderHandlerCharacterizationTests.cs` is its private `CreateHandler()` construction factory, which
      now wires the REAL `OrderAddressResolver` from the same five mocks the test already owned (mechanically
      required by the ctor-arity change; no `[Fact]`, no Arrange, no Assert touched). This makes the net STRONGER
      — every behavioral assertion now runs through the extracted code, not a stub. `CreateOrderTestData.cs` and
      `CreateOrderValidatorCharacterizationTests.cs` untouched.
    - AC2 — new `Features/Orders/OrderAddressResolverTests.cs` (10 cases): saved not-found/cross-user →NotFound,
      saved country de-flagged →CountryNotServiced, inline country not serviced →CountryNotServiced + Code,
      no-country multi-serviced →CountryRequired, no-country single-serviced →fallback success, city not
      serviced →CityNotServiced + `nameof(Address.City)`, missing coords →geocode once, present coords →no
      geocode.
    - Runs (project-scoped, VS-lock-safe): CreateOrder handler+validator characterization + new resolver tests
      = 29 passed / 0 failed. Full `Features/Orders` folder = 156 passed / 0 failed.
    - Full `Cleansia.Tests` (non-parallel) = 1371 passed, 7 failed — all 7 in the **untracked** `Logging/`
      `S6LoggingHygieneCharacterizationTests` (EmailService/SendGrid PII-redaction, a DIFFERENT concurrent lane's
      in-progress work: `Logging/` is `??` and `SendGridClientFactory.cs` is `M` by that lane). NOT caused by
      this ticket — my edits touch only Orders + DI; zero overlap with email/logging paths.
  - **DEVIATIONS:** one — `CreateOrderHandlerCharacterizationTests.CreateHandler()` factory edited (wiring only,
    no assertions) because a ctor-arity reduction makes a directly-`new()`-ed handler test impossible to compile
    otherwise; the gate's red line ("modify assertions to make them pass") is not crossed.
  - **MANUAL_STEPs:** none. Refactor-only — no `Command`/`Response`/DTO/endpoint change → no nswag-regen; no
    schema change → no ef-migration.
  - **Follow-up (not fixed here, out of scope):** the 7 `S6LoggingHygiene` failures are a real red suite owned by
    another lane — flag to PM so that lane's email-redaction ticket lands before the wave PR.
- 2026-06-13 — review fix (backend). Blocking comment-discipline finding resolved: removed the audit-tracker ID
  `(AUD-06a)` from the 3 XML `<summary>` doc comments where it was introduced
  (`Features/Orders/IOrderAddressResolver.cs`, `Features/Orders/OrderAddressResolver.cs`,
  `Tests/Features/Orders/OrderAddressResolverTests.cs`). The reason for each comment (extracted
  address-resolution + serviced-area concern, same ordering, same error codes) already stands on its own; only
  the dangling-pointer tag was dropped, matching the neighboring Orders files' convention of citing only
  load-bearing IDs (ADR-0002/0006, S1, LOY-003). `grep AUD-06a src/` → no matches remain.
  - **Comment-only edits** — no production logic, no `[Fact]`/Arrange/Act/Assert touched; behavior identical.
  - **TEST EVIDENCE:** `Cleansia.Core.AppServices` (both edited production files) fresh build = 0 errors,
    0 warnings. Targeted run of the owned suites (`OrderAddressResolverTests` + `CreateOrderHandlerCharacterization`
    [T-0212 net, unchanged] + `CreateOrderValidatorCharacterization`) = 29 passed / 0 failed. A full
    `Cleansia.Tests` fresh build is currently blocked by 4 UNRELATED files owned by other concurrent lanes
    (`GetPagedPayConfigs/PromoCodes/Referrals/Services` — all `M`/`??` by those lanes; CS0122 `Handler`
    protection-level), zero overlap with this ticket; orchestrator's clean run will resolve once those lanes land.
  - **DEVIATIONS:** none for this fix. **MANUAL_STEPs:** none (comment-only; no contract/DTO/schema change).

## Review
<!-- reviewer / optimizer write verdicts here; PM reconciles before advancing state -->
