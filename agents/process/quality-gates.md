# Quality Gates

A change does not reach `done` until every **applicable** gate passes. The Reviewer enforces gates
1–2 and 6–7 on every ticket; Security (gate 3), Architecture (gate 4), and Optimizer (gate 5) are
conditional. The PM will not merge until the gates that apply are green.

These gates exist because this platform is going to production and is already large. A bug or a
leak that ships now is expensive to undo later. The bar is "would I let this run unattended in
production handling real customers and real money" — not "does it compile."

---

## The gates

### Gate 1 — Conventions self-check (always)
The change conforms to [`../knowledge/conventions.md`](../knowledge/conventions.md) and the
relevant stack catalog (`patterns-backend.md` / `patterns-frontend.md` / `patterns-mobile.md`).
Concretely:
- **No hardcoded user-facing strings.** Backend errors use `BusinessErrorMessage` codes; frontend
  uses `TranslatePipe` keys; Android/iOS use string resources. Every new error key exists in all
  5 locales (en, cs, sk, uk, ru).
- **No `any` in TypeScript. No `dynamic` in C#.** Proper types and enums.
- **Architecture obeyed**: CQRS structure intact, facades delegate, no business logic in
  components/composables, no raw HTML form controls.
- **No magic numbers/strings** — constants in the right home (Policy class, enum, theme).
- **Naming + file layout** match the canonical tables.

### Gate 2 — Acceptance criteria (always)
Every AC item in the ticket has **verifiable evidence**: an automated test, a screenshot from the
running app, a log line, or an explicit reviewer confirmation tied to a file:line. "Looks done" is
not evidence. An AC with no evidence fails the gate.

### Gate 3 — Security (mandatory iff `security_touching: true`)
The Security Reviewer walks [`../knowledge/security-rules.md`](../knowledge/security-rules.md)
(S1–S10) against the diff. A ticket is **security-touching** if it adds/changes any of: an
endpoint, auth/authorization, a resource-by-id operation, a response DTO, tenancy scoping, a
side-effecting command (payment, email, loyalty, referral, invoice), file upload, logging of user
data, or rate-limited routes. The verdict names the **specific risk**, not a category — e.g.
"customer A can read customer B's order because the handler doesn't check ownership at
`GetOrderById.cs:31`", not "missing authorization".

### Gate 4 — Architecture (mandatory iff a new pattern or extension point is touched)
The Architect confirms the change preserves the seams: no country branching in handlers (read
`CountryConfiguration`), no provider-specific code outside its adapter, no infra leaking into
`Core.Domain`/`Core.AppServices`, fiscal enforcement modes respected. If the change needed a new
pattern, an ADR exists and is cited.

### Gate 5 — Performance, cost & runtime readiness (for hot paths, external calls, jobs, heavy UI)
The Optimizer checks: no N+1 queries, `AsNoTracking()` on read paths, indexes for new
WHERE/ORDER/JOIN columns, no over-fetching DTOs, OnPush + trackBy on lists, no bundle bloat from a
heavy import, no needless re-renders/recompositions. **Plus runtime readiness** per
[`../knowledge/runtime-readiness.md`](../knowledge/runtime-readiness.md) when the change touches an
external service, a queue/Function, or a hot path: structured logging + correlation id, error
classification, **graceful degradation** (core action not blocked by a non-core dependency outage),
durable side effects, idempotency, and a visible dead-end for failures. Applied when the ticket
touches a list view, a paged query, a hot endpoint, an external integration, a background job, or
adds a dependency.

### Gate 6 — Tests, written test-first (always, proportional to risk) — per [`../knowledge/testing.md`](../knowledge/testing.md)
- Development is **TDD by default**: the test is written **before** the implementation (red → green →
  refactor). For **pure logic** (pricing, pay calc + override precedence, validators, state machines,
  fiscal-mode selection, numbering, refunds) this is **strict and mandatory** — the Reviewer expects
  the test to predate the code (commit order / status-log "red→green"), and **rejects after-the-fact
  tests on pure logic** (they miss the branches the author didn't think of). Money math and state
  transitions are non-negotiable.
- Handlers: the unit test (mocked repos, asserting `IsSuccess` + each `Error.Code`) and the route
  integration test (incl. the auth/ownership rejection) are written against the intended contract
  first. UI: the **facade/ViewModel** test is written first; the view follows.
- Changing existing **untested** code: write a **characterization test** pinning current behavior
  first, then TDD the change.
- New **endpoints** have an integration test covering the happy path and the key failure
  (auth/ownership rejection — a real test, not just review).
- The change covers its slice of the **must-cover list** in `testing.md` (pay calc, order lifecycle,
  money/refunds, fiscal modes, authz boundaries, idempotency, every `BusinessErrorMessage` path).
- Cross-layer behavior has an integration test where the project's harness supports it.
- The QA test plan exists and was executed; results recorded.

### Gate 7 — Contract & docs parity (when the surface changed)
- If a backend DTO/endpoint changed, the ticket carries a `MANUAL_STEP: nswag-regen` flag for the
  owner. The agents do **not** regenerate clients.
- If a schema changed, the ticket carries a `MANUAL_STEP: ef-migration` flag. The agents do **not**
  run migrations.
- If shipped behavior changed, the Docs agent updates the relevant `docs/**` page and the changelog
  in the same ticket (or a linked docs ticket).

### Gate 8 — Mechanical checks pass (always; this is what makes the rules real)
Deterministic beats diligent. Before a ticket reaches `done`, the **mechanical** checks for the
touched stacks pass, and the Reviewer/PM record the result on the ticket as evidence:
- **Backend touched:** `dotnet build` + `dotnet test` succeed (the same as CI `backend-ci.yml`).
- **Frontend touched:** the affected app(s) `nx build` (and `nx lint`/`nx test` where wired) succeed.
- **Any stack:** `node agents/tools/check-consistency.mjs --paths=<changed dirs>` reports **no new**
  violation. A pre-existing violation the change merely sits near is noted, not blocking — unless the
  ticket *is* its canonicalization ticket. See [`enforcement.md`](./enforcement.md) for the tool, the
  ~187-item baseline, and the rollout to fully-automatic CI gating.

A ticket whose mechanical checks fail cannot be `done`, regardless of how good the review reads. If a
check is failing for a reason genuinely unrelated to the change (a flaky test, a pre-existing
baseline item), the Reviewer says so explicitly with evidence — it is never waved through silently.

---

## Owner-only steps (the agents never do these)

Per `CLAUDE.md`, two steps are **owner-only**. Agents detect the need, flag it as a `manual_steps`
entry on the ticket, and **block the dependent work** until the owner confirms it's done:

- **EF Core migrations** — `dotnet ef migrations add` / `database update`. Agents describe the
  schema delta; the owner creates & applies the migration.
- **NSwag client regeneration** — `npm run generate-*-client`. Agents flag it; the owner regenerates
  the TypeScript clients before dependent frontend/mobile work begins.

A ticket that needs either and hasn't had it confirmed cannot reach `done`.

---

## How a reviewer writes a verdict

In the ticket's `## Review` section, for each gate that applies:

```markdown
## Review — reviewer (2026-06-01)

- Gate 1 Conventions: PASS
- Gate 2 AC: FAIL — AC#3 ("admin sees override badge") has no evidence; the badge component
  isn't wired in `pay-config.component.html`. Add it + screenshot.
- Gate 6 Tests: FAIL — PayCalculationService override precedence has no unit test. Add one
  covering employee-override-wins and fallback-to-service-config.

Verdict: CHANGES REQUESTED. Re-invoke me after fixes.
```

Be specific (file:line + the fix expected), be kind (reject the code, not the author), and never
approve under time pressure. "It's a small change" is not a reason to skip a gate.
