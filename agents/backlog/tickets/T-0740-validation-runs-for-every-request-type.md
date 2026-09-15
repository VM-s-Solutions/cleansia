---
id: T-0740
title: Validation runs for every request type (A2)
status: done
size: S
owner: —
created: 2026-09-14
updated: 2026-09-14
depends_on: []
blocks: []
stories: []
adrs: [ADR-0062, ADR-0002]
layers: [backend]
security_touching: false
manual_steps: []
sprint: 16
---

## Context

Owner ruling 2026-09-14 (A2): *"Option b"* — lift `ValidationPipelineBehavior`'s `BusinessResult`
constraint. Validators on `IRequest<PagedData<T>>` never ran (`GetAllGdprRequests.Validator` was
dead); `GetActionTimeline` had been made `BusinessResult<PagedData<T>>` to get a validator, declared
as an A2 deviation in `agents/cleanup/consistency-baseline.md`.

## Doing

- `ValidationPipelineBehavior` validates every request that has a validator: a `BusinessResult`
  response keeps the typed failure; any other response throws `RequestValidationException` carrying
  the errors, mapped to the **same 400 ProblemDetails** the `IValidationResult` arm produces (HostTests
  pin the wire shape byte for byte).
- `GetAllGdprRequests.Validator` runs: `limit=1000` → 400 `validation.page_size_exceeded`.
- `GetActionTimeline` returns `PagedData<T>` directly again; the A2 baseline row and the
  `check-consistency.mjs` allowance removed with their self-test cases.
- A validation reject on a marked command still writes exactly one out-of-band failure row with the
  error key — the pipeline tests extended to the throwing arm.

## NOT

No change to any validator's rules. No change to the ProblemDetails shape.

## Status log

- 2026-09-14 — shipped in `6f874295` (the constraint lifted, `RequestValidationException` +
  `RequestValidationExceptionFilterAttribute`, `GetAllGdprRequests` keys `validation.page_size_exceeded`,
  `GetActionTimeline` canonical, baseline row and checker widening withdrawn, the thrown arm's failure
  row) and `7004ada4` (`EveryValidatorIsReachedByThePipelineTests` resolves the behaviour from the
  container for every validated request type; `AdminGdprController.GetAllGdprRequests` declares the
  400; the registration-order comment names the throwing arm). Admin client regenerated in `f433f5ff`
  (`AdminGdprClient.requests` gained the status parameter first, which T-0739's facade then took
  positionally). Recorded in ADR-0062 D6 as amended; `agents/knowledge/consistency.md` A2 no longer
  declares an exception.
