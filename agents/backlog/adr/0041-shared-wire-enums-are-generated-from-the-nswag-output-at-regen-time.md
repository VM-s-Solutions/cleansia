# ADR-0041 — The shared wire-enum declaration is **generated from the NSwag output, inside the owner's regen command**, and the hand-written mirror is deleted; the three per-host clients keep emitting their own copies, because that multiplicity is the per-host seam and not the defect

- **Status:** `proposed` — 2026-08-04, **author mode**. The **owner has ruled the WHAT** (see §0); that
  half is not open to challenge. **The HOW is the architect's and is open** — one challenger round is
  requested on §D1/§D2 (the placement) and §D3 (why the clients keep their copies). Ticket **T-0546**
  carries the round; **T-0547** carries the refactor and does not start until this ADR is `accepted`.
  *(Both ids are **proposed**, not allocated — the PM owns `backlog/tickets/`. `T-0545` was the highest on
  disk when this was written; if the PM allocates other ids, those bind and this line is the only place
  that needs correcting.)*
- **Date:** 2026-08-04
- **Answers:** **`Q-ENUM-01`** (`agents/backlog/questions/open.md`) — *"Three generated clients each emit
  their own `OrderStatus`/`PaymentStatus`. Which declaration is canonical, and should the per-app copies
  stop being emitted?"*
- **Supersedes:** — . **Adopts and extends ADR-0031 D1** (a regen-time check placed where the drift is
  *created*) onto a second surface. ADR-0031 is untouched and stays `accepted`.
- **Consumes:** ADR-0031 (the regen entry-point invariant M1, the derive-discovery-from-the-real-config
  rule M2, the anti-vacuity rule), ADR-0032 D2 (every constraining catalog entry names an enforcer and a
  tier).
- **Applies to:** `src/Cleansia.App` — `package.json` regen scripts · a new `tools/generate-wire-enums.mjs`
  · `libs/shared/models` · `libs/shared/pipes` · **the three generated clients are not edited and NSwag's
  configuration is not changed** (§D3) · **no backend change, no DTO change, no migration** · Android/iOS
  generated clients explicitly out of scope (§residue 3).
- **Catalog edit bound to acceptance:** `agents/knowledge/patterns-frontend.md` §"Module boundaries"
  currently states the rule this ADR replaces. Literal replacement text is in §7; it lands with T-0547,
  not before.

> **One decision:** *where the shared, cross-app declaration of a wire enum comes from.* It comes from a
> **generator that reads the NSwag output and runs inside `npm run generate-*-client`**, before the
> ADR-0031 typecheck. Nothing about the enum is typed by a human. The three per-host clients keep their
> own copies **on purpose** — collapsing them would delete the only evidence that two hosts' contracts
> have diverged, and would be a change to an owner-run command that no agent can test.

---

## 0 — The owner's ruling, quoted, and exactly what it settles

> *"I think that there is a need to refactor and better to use the one that is generated from nswag.
> Also consider using backend enums on frontend instead of generating your own"*

**Settled, not re-litigable:**

1. **The hand-written mirror goes.** `libs/shared/models/src/lib/models/order-status.models.ts` — the
   file a previous agent typed by hand — is deleted. No successor is hand-typed.
2. **The declaration a shared lib reads must come out of the NSwag pipeline**, and must be traceable to
   `Cleansia.Core.Domain.Enums` without a human retyping it.

**Not settled by the ruling, and therefore this ADR's actual work:** *how*, given that a `scope:shared`
lib may not import any `scope:<app>` client (`patterns-frontend.md` §"Module boundaries"), and the fix
that established that boundary retired 13 circular dependencies. "Just import the generated one" is the
obvious version and it is the thing that cannot be done. §D1–§D3 are that answer.

---

## 1 — Context, established against the tree (not against the ticket text)

### 1.1 — There are **five** declarations, not four, and the fifth has already drifted

`Q-ENUM-01` says four. Verified at HEAD, there are five:

| # | File | `OrderStatus` | `PaymentStatus` | Written by |
|---|---|---|---|---|
| 1 | `libs/core/partner-services/src/lib/client/partner-client.ts:10712` / `:11428` | `New=0 Pending=1 Confirmed=2 OnTheWay=3 InProgress=4 Completed=5 Cancelled=6` | `…PartiallyRefunded=6` | `nswag-partner.json` |
| 2 | `libs/core/admin-services/src/lib/client/admin-client.ts:23500` / `:25004` | identical | identical | `nswag-admin.json` |
| 3 | `libs/core/customer-services/src/lib/client/customer-client.ts:11233` / `:11771` | identical | identical | `nswag-customer.json` |
| 4 | `libs/shared/models/src/lib/models/order-status.models.ts:19` / `:29` | identical | identical | **a human** |
| 5 | **`libs/core/services/src/lib/client/admin-client.ts:7180` / `:7194`** | **`Pending=1 Confirmed=2 InProgress=3 Completed=4 Cancelled=5`** | **no `PartiallyRefunded`** | **nothing** |

**Answering the question that was asked — "do the three generated copies currently agree?"** They do.
So do the other nine enums all three clients declare (§1.2). **But that is the wrong question to stop
at, and #5 is why.**

`libs/core/services/src/lib/client/admin-client.ts` is ADR-0031's residue #5(a) / CH-14 — written by no
`nswag-*.json`, exported by no barrel, imported by nothing, compiled by no app build. ADR-0031 recorded
it as **dead code**, a hygiene item. **It is not hygiene. It is a drifted wire contract**, and it drifted
in the most dangerous way available:

```
live contract:   New=0  Pending=1  Confirmed=2  OnTheWay=3  InProgress=4  Completed=5  Cancelled=6
stale copy #5:          Pending=1  Confirmed=2  InProgress=3  Completed=4  Cancelled=5
```

Every integer from 3 upward means something else. A consumer of #5 reading `status === 3` renders
*"In progress"* for a cleaner who is merely **on the way**; `4` renders *"Completed"* for a job that is
**in progress**. This is not a hypothetical: `Cleansia.Core.Domain.Enums.OrderStatus` was genuinely
**renumbered** at some point in this repository's history — `New = 0` and `OnTheWay = 3` were inserted —
and file #5 is the fossil that survived the renumber. Meanwhile `CLAUDE.md`'s repo map still advertises
`core/services/` as *"NSwag-generated API clients"*, so an agent following the map imports the fossil.

**So the honest finding is:** the multi-copy scheme has **already produced a silently wrong renumbering
that has been sitting in the tree**, and the parity spec added this sprint does **not** see it — its
`GENERATED_CLIENTS` list (`order-status-enum-parity.spec.ts:12-16`) names the three live clients and not
this one. *That* is the argument for the refactor, and it is stronger than "four copies is untidy."

### 1.2 — It is a class of problem, not two enums

Enum counts per client: partner **15**, customer **19**, admin **27**. Declared by **all three**:

`AppliedDiscountSource` · `ConsentType` · `ContractStatus` · `EmployeeEntityType` ·
`EmployeeInvoiceStatus` · **`OrderStatus`** · **`PaymentStatus`** · `PaymentType` ·
`PayoutDetailsStatus` · `PayoutScheme` · `PhotoType` · `SortDirection`

**12 backend enums × 3 clients = 36 generated declarations.** All 36 agree today (verified member-by-
member at HEAD). Every one of them is a candidate for the next shared pipe, shared table-definition or
shared status chip — `PhotoType`, `PaymentType` and `PayoutScheme` especially. Answering `Q-ENUM-01` for
two enums and leaving the other ten would guarantee this question is asked again.

**And it already was asked again, and answered by hand.** `SortDirection` is a **third** hand-written
mirror: `libs/shared/models/src/lib/models/sort-types.models.ts:16-19` declares
`SortDirection { Ascending = 0, Descending = 1 }`, matching all three clients, **with no parity spec of
any kind**. It predates the `OrderStatus` one. The pattern this ADR retires is not new — it is on its
third instance and its second one is unguarded.

Enums declared by **some but not all** hosts (`DocumentStatus`/`DocumentType`/`PayPeriodStatus` on
partner+admin; `LoyaltyTier`/`LoyaltyEarnSource`/`LoyaltyTransactionType`/`ReferralStatus` on
customer+admin; `RefundReason`/`FiscalErrorKind`/`GdprRequestStatus`/… admin-only) are **not** shared
surface and §D2 deliberately does not emit them.

### 1.3 — The four copies coexist only because of a TypeScript accident, and that accident is what hides drift

TypeScript numeric enums are **nominal**. `OrderStatus` from `@cleansia/models` is *not* assignable to
`OrderStatus` from `@cleansia/customer-services`, even member-for-member identical. The three shared
pipes compile only because their parameter admits `number`:

```ts
// libs/shared/pipes/src/lib/order-status/order-status-icon.pipe.ts:13
transform(status: OrderStatus | { value?: number } | number | null | undefined): string
```

and the call sites feed them a *client* enum through that arm —
`order-detail.component.html:85` `[severity]="order()!.orderStatus | orderStatusSeverity"`, and 13 more
across customer orders / track-order / order-lookup. `strictTemplates` is on, so these bindings **are**
type-checked; they pass because `customer-client`'s `OrderStatus` is assignable to `number`.

**Consequence, and it is the load-bearing one:** the shared enum is used as a **constant table**, never
as a type. `case OrderStatus.Completed:` inside the pipe compares against the *integer* `5`. If that
integer is ever wrong, **no compiler anywhere will say so** — the widening to `number` is precisely what
makes the mismatch invisible. This is why "declare it once by hand and add a parity spec" is a detector
and not a fix, and it is why the check has to be **generation**, not comparison.

### 1.4 — The existing parity spec cannot run in CI on the one commit it exists to police

`order-status-enum-parity.spec.ts` reads the three clients **off disk** (`readFileSync`), deliberately —
an import would be the scope break it exists to keep closed. That is the right idiom and it has a
consequence nobody wrote down:

- Nx's project graph therefore has **no edge** from `models` to any `*-services` lib (it must not; a
  `scope:shared → scope:partner` edge is the violation), and `libs/shared/models/project.json` declares
  no `implicitDependencies`.
- `frontend-ci.yml`'s blocking test step is `npx nx affected -t test --base=… --head=HEAD`.
- A regen commit touches only `libs/core/*-services/src/lib/client/*-client.ts`. `models` is **not**
  affected. **The parity spec does not run.**
- The three unconditional production builds *do* run, but a wrong **integer** is not a type error
  (§1.3), so they are green too.

So on a regen-only commit — the exact commit class this guard was written for — nothing in CI executes
it. The check has to move to where the regen happens. That is ADR-0031 D1's argument, arrived at
independently on a second surface.

### 1.5 — How generation actually works here (the constraint the design must fit)

```
owner runs:  npm run generate-partner-client
                └─ _nswag:partner  =  npx nswag run nswag-partner.json  &&  bash partner-client-formatter.sh
                └─ npm run typecheck   (ADR-0031 D1 — ngc --noEmit over every app compilation unit)

             npm run generate-clients  =  all three _nswag steps, then ONE typecheck
```

- **The owner runs generation. No agent does** (`CLAUDE.md` §"Manual Steps"). Any design must be
  something the owner can run **with the commands they already run**.
- The three `nswag-*.json` files each name their single output at `"output"`
  (`nswag-partner.json:39`, `nswag-admin.json:39`, `nswag-customer.json:39`). **That key is the only
  authoritative statement of what a generated client is** — and it is what proves file #5 is not one.
- **A post-generation step already exists and already rewrites the file**: the three
  `*-client-formatter.sh` scripts `sed` the output (`PagedData_1OfOf…` renames, snake→camel). So adding
  a derived artifact to this pipeline is an established shape, not a new one.
- *(Correction to the living doc, folded in with this ADR: all three formatters now carry
  `set -euo pipefail` at `:4`. `generated-client-contract.md` invariant 10 / gap 4b still says none of
  them do — that half is stale as of T-0439. The `nswag run` exit-code half remains unverified.)*

---

## Decision

### D1 — The shared declaration is **generated**, by a tool that runs inside `npm run generate-*-client`, before the ADR-0031 typecheck

A new `src/Cleansia.App/tools/generate-wire-enums.mjs`:

1. **Discovers the client set from `nswag-*.json`, never from a filesystem glob** (ADR-0031 M2, applied).
   It reads every `nswag-*.json` in `src/Cleansia.App/`, takes
   `codeGenerators.openApiToTypeScriptClient.output`, and treats **that set** as "the generated clients".
   A config whose `output` file is **absent** is a hard **exit 1**, never a silent skip.
   *This is also the mechanical answer to §1.1: file #5 is written by no config, so it is definitionally
   not a client, and no future reader has to litigate that again.*
2. **Parses every `export enum` block** from each discovered client.
3. **Computes the intersection** — enums declared by *all* clients — and **asserts they agree**, member
   name by member name and integer by integer.
4. **On any disagreement: exit 1, naming the enum, the member, and each client's value.** This is the
   whole point. It fires on the owner's machine, at the moment the divergence is created, before a
   commit exists.
5. **On agreement: writes one file**,
   `libs/shared/models/src/lib/models/wire-enums.generated.ts`, containing all 12 (currently) intersecting
   enums, with a header naming the producing command, the source clients, and
   `Cleansia.Core.Domain.Enums` as the origin of the values.
6. **Prints, but does not emit, the non-intersecting enums** — so the next person who wants
   `RefundReason` in a shared lib sees that it is admin-only *before* hand-typing a fourth mirror.
7. **Anti-vacuity (ADR-0032 D3 / ADR-0038's rule):** exit 1 if it discovers fewer configs than exist,
   if any discovered client yields **zero** enums, or if the intersection is **empty**. A generator that
   writes an empty file and exits 0 is a non-run, not a pass.

**Wiring** — the ADR-0031 M1 invariant is extended, not bypassed:

```
_nswag:partner  =  npx nswag run nswag-partner.json && bash partner-client-formatter.sh
generate-partner-client  =  npm run _nswag:partner && npm run gen:wire-enums && npm run typecheck
generate-clients         =  _nswag:partner && _nswag:admin && _nswag:customer && npm run gen:wire-enums && npm run typecheck
```

**Order is load-bearing and is not negotiable:** the generator runs **after** NSwag+formatter and
**before** `typecheck`. It must see the freshly formatted clients, and the typecheck must see the
freshly written shared file — so a regen that removes a member the pipes still `case` on fails the
owner's own command with a `file:line`, not a reviewer's PR three days later.

**The owner's command surface does not change.** They run `npm run generate-partner-client` /
`generate-admin-client` / `generate-customer-client` / `generate-clients`, exactly as today. Nothing new
to learn, nothing new to remember. **`npm run gen:wire-enums` is a composition step and is `_`-prefixed
in practice per ADR-0031 M1's convention** — it is listed above unprefixed only for readability; the
implementing ticket names it `_gen:wire-enums` and adds a directly-runnable `check` alias (§D4).

**Why this is the right placement, in one line:** it is ADR-0031 D1's argument on a second surface — put
the check where the defect is *created*, because §1.4 proves the place it currently lives cannot run on
the commit that creates it.

### D2 — What the generated file contains, and what it replaces

- **Location:** `libs/shared/models/src/lib/models/wire-enums.generated.ts` — inside the existing
  `scope:shared` / `type:util` lib (`libs/shared/models/project.json`), which already has `test` and
  `lint` targets. A new lib for one file would be worse.
- **Content:** the intersection set. Today: `AppliedDiscountSource`, `ConsentType`, `ContractStatus`,
  `EmployeeEntityType`, `EmployeeInvoiceStatus`, `OrderStatus`, `PaymentStatus`, `PaymentType`,
  `PayoutDetailsStatus`, `PayoutScheme`, `PhotoType`, `SortDirection`. Not a hand-picked list — whatever
  the three hosts all expose.
- **Emitted as `export enum`**, matching the clients, so the pipes' `switch` bodies are unchanged.
- **Committed**, like the clients themselves, with a `DO NOT EDIT — regenerate with …` header.
- **Deleted by this change:** `order-status.models.ts` (the whole file — its `OrderStatus` and
  `PaymentStatus` are now generated), and the `export enum SortDirection` block in
  `sort-types.models.ts:16-19`.
- **`sort-types.models.ts` keeps `SortDefinition`/`ISortDefinition`** (hand-written convenience shapes
  shared code constructs) and **imports** `SortDirection` from the generated file for its default. It
  must **not re-export** it — `models/index.ts` exports both files, and a re-export would be a duplicate
  export.
- **`models/index.ts`** gains `export * from './wire-enums.generated';` and loses `'./order-status.models'`.

**Why the intersection and not the union:** an enum only one host exposes is that host's contract. A
shared lib with a claim on it is either not really shared, or the enum should be exposed by the other
hosts — both of which are decisions, and neither should be made silently by a generator. If a host stops
exposing an enum a shared lib uses, it drops out of the file and the shared consumer **fails to compile
during the owner's regen**, with a name. That is the correct, loud failure.

### D3 — The three per-host clients **keep emitting their own copies**. NSwag's configuration is not changed. This is the deliberate part

`Q-ENUM-01` asked whether the clients should stop emitting. **No**, on three independent grounds:

1. **The multiplicity is the per-host seam, not the defect.** Five API hosts share Core + Infra + Config
   but are **deployed and regenerated independently** (Partner :5000, Admin :5001, Customer :5003, plus
   the two mobile hosts). Three clients from three specs is a *faithful* rendering of three contracts. If
   all three imported one shared symbol, a client regenerated against a host that is a commit behind
   would **claim** the current contract, and the divergence would become **structurally unobservable**.
   Today it is observable — which is the only reason §1.1's fossil is legible at all. **Collapsing to one
   declaration does not remove the drift; it removes the evidence of it.**
2. **It is a change to an owner-run command that no agent can test.** `excludedTypeNames` +
   `extensionCode` import-hoisting is plausible NSwag behaviour and it is **unverified in this repo**, and
   unverifiable by anyone who is not the owner. Shipping an untested config change into the one command
   the owner runs, whose failure mode is "your regen is broken and the agent that wrote it cannot
   reproduce it", is a bad trade for an aesthetic gain.
3. **It would make the generated client non-self-contained.** `partner-client.ts` would stop compiling
   without `@cleansia/models`. The `scope:partner → scope:shared` edge is *legal*, but it inverts the
   pipeline's shape: the derived artifact would become an input to the artifact it is derived from.

**What is conceded, plainly:** this leaves **four** declarations (3 generated per-host + 1 generated
shared), down from five. The count is not the goal. **The goal is that none of them is typed by a human
and that a disagreement between them is impossible to ship** — which D1 delivers and which no reduction
in count would deliver on its own.

**And it stays revisitable.** If a future NSwag upgrade makes `excludedTypeNames` + shared-import
emission a supported, testable path, and if the per-host observability argument is answered, this is a
superseding ADR — not a quiet config edit.

### D4 — The parity spec is **not deleted**; it is re-based onto the committed state, where it still has a job

`order-status-enum-parity.spec.ts` becomes `wire-enums.generated.spec.ts` and answers a different,
still-real question: **does the file committed to the tree match what the generator would produce from
the clients committed to the tree, right now?**

It runs the generator in a `--check` mode (compute, compare, never write) and fails on any difference.
That catches the two things D1 cannot:

- someone **hand-edited** `wire-enums.generated.ts`;
- someone regenerated a client **without** the wrapper — e.g. by invoking `npx nswag run` directly, which
  ADR-0031 M1 discourages but cannot make impossible.

It keeps the off-disk read idiom (an import is still the scope break) and it keeps `nx test models`
meaningful.

**Its tier, stated honestly per ADR-0032 D2:** the spec runs under `nx affected -t test`, so — per
§1.4 — it is **T2-ADVISORY on a regen-only commit** (models is not affected and it does not run) and
**T1-CI on any commit that touches `libs/shared/models`**. That asymmetry is exactly why D1 exists and
why the spec is the *backstop*, not the guard. **Do not describe the spec as the gate.**

*Optional, named, not mandated:* adding `libs/core/*-services/src/lib/client/*-client.ts` to
`models`'s `implicitDependencies` would make the spec affected by a regen. It is **not** adopted here
because an `implicitDependencies` entry is a project-graph edge with `enforce-module-boundaries`
implications that need their own verification, and because D1 already covers the case earlier. Recorded
so the next reader does not think it was missed.

### D5 — The stale fifth client is deleted **by this refactor**, not by a someday-ticket

ADR-0031 routed `libs/core/services/src/lib/client/admin-client.ts` to a follow-up as dead code.
§1.1 reclassifies it: it is a **wrong wire contract in the tree, advertised by `CLAUDE.md`'s repo map**.
D1's claim ("the clients all agree") is false comfort while a fourth generated-*looking* client sits
unread by every gate. **T-0547 deletes it.** The `CLAUDE.md` map correction stays an **owner-gated
`MANUAL_STEP`** with proposed literal text (the file is owner/orchestrator-gated —
`shared-file-lanes.md`); the ticket proposes, the owner edits. This is ADR-0031's M6 shape.

---

## 2 — What breaks the moment the hand-written copy is deleted (named, all of them)

Exactly **four** files import it. Verified at HEAD by grep for
`import {… OrderStatus | PaymentStatus …} from '@cleansia/models'`:

| Consumer | Import | Fix |
|---|---|---|
| `libs/shared/pipes/src/lib/order-status/order-status-severity.pipe.ts:2` | `OrderStatus` | import path only — the symbol keeps its name and its integers |
| `libs/shared/pipes/src/lib/order-status/order-status-icon.pipe.ts:2` | `OrderStatus` | same |
| `libs/shared/pipes/src/lib/order-status/payment-status-severity.pipe.ts:2` | `PaymentStatus` | same |
| `libs/shared/models/src/lib/models/order-status-enum-parity.spec.ts:3` | both | rewritten per §D4 |

**Not affected** (they map integers to i18n keys and import nothing):
`order-status-label.pipe.ts`, `payment-status-label.pipe.ts`.

**Do not confuse this with T-0533**, which is in flight on a superficially similar finding —
`customer-auth.service.ts` importing four *generated DTO classes* from `@cleansia/partner-services`.
That one is `scope:customer → scope:partner` and its fix is trivial and correct: **use the customer
client's own equivalents**, which exist. The enum case has **no such fallback** — a `scope:shared` lib
has no client of its own to fall back to, which is precisely why the hand-written mirror was created and
why it needs a generator rather than a re-point. The two tickets must not be merged, and T-0533's fix is
**not** a precedent for this one.

**Because `models/index.ts` re-exports the generated file under the same symbol names, every consumer
outside `libs/shared/models` sees no change at all** — including the 14 template call sites in §1.3.
This is deliberate: the refactor must be a *provenance* change, not an API change, or it cannot be
reviewed for what it actually is.

**One further sweep the compiler will not name:** `SortDirection`'s move (§D2). It is exported from
`@cleansia/models` today and imported by feature and store code; after the change it is exported from
the same barrel under the same name, so call sites are untouched — but a duplicate-export mistake in
`sort-types.models.ts` would be a build break, which is why §D2 spells out "import, do not re-export".

---

## 3 — The refactor, specified (this is T-0547's spec; **no production code is written by this ADR**)

**Order matters. Steps 1–4 need no regeneration and can land in one PR. Step 5 is the owner's.**

1. **Add `src/Cleansia.App/tools/generate-wire-enums.mjs`** per §D1. Discovery from `nswag-*.json`
   `output`; intersection; agreement assertion; anti-vacuity exits; `--check` mode for §D4.
2. **Add its own suite, `tools/generate-wire-enums.test.mjs`, and make it mutation-provable** (Gate 6.5,
   the ADR-0031 V3 shape): fixtures under `os.tmpdir()` (**never** in-repo — ADR-0031's dated closure §B),
   covering at minimum → *disagreeing integer between two clients → exit 1 naming both*; *an enum in only
   two of three clients → reported, not emitted*; *a config whose `output` is missing → exit 1*; *zero
   enums found → exit 1*; *agreement → the expected file bytes*. Stubbing the generator body to
   `process.exit(0)` must redden this suite.
3. **Run it once, commit the produced `wire-enums.generated.ts`.** It is derived from the clients
   **already committed**, so this step needs no NSwag run and no backend running.
4. **Retire the hand-written declarations and re-point the consumers:**
   delete `order-status.models.ts`; delete the `SortDirection` enum block from `sort-types.models.ts` and
   import it instead; update `models/index.ts`; re-point the three pipes (§2); rewrite the parity spec
   per §D4; **delete `libs/core/services/src/lib/client/admin-client.ts`** (§D5).
5. **Wire the generator into `package.json`** per §D1 and add its suite to `frontend-ci.yml` beside the
   existing **"Regen-drift guard self-test"** step (cite the step *name*, not a line —
   `generated-client-contract.md` §"cite stable anchors").
6. **`manual_steps`:** `nswag-regen` is **NOT** required for this refactor — that is the point of step 3.
   The **first** regen after this lands is the ADR's real proof and should be reported. Plus the
   owner-gated `CLAUDE.md` correction from §D5.
7. **`agents/knowledge/patterns-frontend.md` edit** — §7 below, bound to this ADR reaching `accepted`.
8. **A CRC card is not required** — the generator is a build tool, not a role. Nothing new knows anything.

**Explicitly out of scope of T-0547:** any NSwag config change (§D3); the Android/iOS clients; the
non-intersecting enums; `implicitDependencies` (§D4); the `| number` widening in the pipes (§residue 1).

---

## 4 — Consequences

**Cheaper / safer**

- **Nothing about a wire enum is typed by a human.** The owner's ruling is satisfied by construction, not
  by discipline.
- **A cross-host disagreement becomes unshippable**, named with the enum, the member and each host's
  value, on the owner's machine, before a commit exists.
- **The class is closed, not two instances of it.** All 12 shared enums land at once; the 13th arrives
  free the day a host exposes it to all three.
- **`SortDirection`'s unguarded hand-mirror is retired in the same change** — the instance nobody had
  noticed.
- **A fourth Angular app / a fourth host is covered the day its `nswag-*.json` exists** — discovery is
  derived from the configs, the ADR-0031 M2 property.
- **The per-host seam is preserved.** A customer feature still cannot compile against the partner
  contract; no host is coupled to another.

**More expensive (accepted)**

- One more step in the owner's regen (a file read + a parse + one small file write — negligible beside
  `ngc`; **speed is not an argument here either**, per ADR-0031 CH-6).
- A committed `*.generated.ts` inside a hand-written lib: a new shape in this repo, mitigated by the
  DO-NOT-EDIT header and by §D4's committed-state check.
- One more invariant to keep alive: **every regen entry point ends in the generator *and* the typecheck**
  — ADR-0031 M1 with a second clause.

**Accepted residues (named, not closed)**

1. **The pipes keep `| number` in their signatures.** TypeScript numeric enums are nominal across
   declarations (§1.3), so the shared symbol will never be *assignable* to a client DTO's field. Removing
   the widening would require every call site to cast, which is worse. **This is exactly why the
   generator's agreement check is load-bearing: the compiler will never do this job.** Do not "fix" the
   widening without reading §1.3 first.
2. **A regen-only commit still does not run `nx test models`** (§1.4). D1 makes that acceptable — the
   check ran earlier — but the residue is real if a client is ever regenerated outside the wrapper.
3. **Android + iOS are out of scope.** They come from committed specs under `src/cleansia_android/openapi/`
   via the owner-only `mobile-spec-regen` and are governed by ADR-0019 — a parallel drift surface with the
   same shape, deliberately not folded in (ADR-0031 residue #4, restated). **Named consequence:** the same
   12 enums exist a third and fourth time in the mobile clients and **nothing compares them to the web
   ones**. If that is ever wanted, it is its own ADR, not a widening of this generator.
4. **The generator reads generated output**, one hop further from the backend than the clients are.
   Deliberate: the three swagger documents are not committed for the web and exist only while the three
   hosts are running, whereas the emitted clients are files on disk and re-derivable at any time. Same
   trade the parity spec already made.
5. **`CLAUDE.md`'s repo map is still wrong** until the owner edits it (§D5). Owner-gated.

---

## 5 — Verification (how a reviewer verifies compliance)

- **V1 — no regen entry point skips the generator.** Every `package.json` script that invokes NSwag
  (directly or by composition) ends in the wire-enum generator **and then** `npm run typecheck`, in that
  order. Grep the scripts block; a regen-shaped script name without both is a fail. *(ADR-0031 V1, second
  clause.)*
- **V2 — discovery equals the NSwag configs, not a glob.** `tools/generate-wire-enums.mjs` derives its
  client list from each `nswag-*.json`'s `codeGenerators.openApiToTypeScriptClient.output`. Confirm by
  deleting one client file in a scratch tree: the generator must **exit 1**, not report "2 clients read".
  *(ADR-0031 M2, applied.)*
- **V3 — the generator is mutation-provable.** Replace its body with `process.exit(0)` and its suite goes
  red on at least the disagreement case, the missing-output case and the empty-intersection case. The
  suite runs in `frontend-ci.yml` beside the "Regen-drift guard self-test" step.
- **V4 — the shared file is generated, and nothing else is hand-typed.** `libs/shared/models/src/lib/models/`
  contains **no** hand-written `export enum` that also appears in any generated client. Grep the lib for
  `export enum` and cross-check each name against the three clients. `order-status.models.ts` is **gone**;
  `sort-types.models.ts` **imports** `SortDirection` and does not declare or re-export it.
- **V5 — the committed state is self-consistent.** `npx nx test models` passes, and its
  `wire-enums.generated.spec.ts` runs the generator in `--check` mode against the committed clients.
  Mutation-check it by editing one integer in `wire-enums.generated.ts` — the suite must go red.
- **V6 — nothing outside `libs/shared/models` changed shape.** The diff touches import *paths* in exactly
  the three pipes of §2 and no `.html` file. If a template changed, the refactor did more than it should.
- **V7 — no generated client was edited and no NSwag config was changed.** `nswag-{partner,admin,customer}.json`
  are byte-identical; the three `*-client.ts` under `libs/core/*-services/` are byte-identical. The only
  client-shaped file that changes is `libs/core/services/src/lib/client/admin-client.ts`, which is
  **deleted** (§D5).
- **V8 — the anti-vacuity exits exist and are tested.** A generator that reads zero clients, finds zero
  enums, or emits an empty intersection must exit **1**. Verified by V3's suite, not by reading the code.
  *(ADR-0032 D3.)*

---

## 6 — Alternatives considered

| # | Option | Disposition |
|---|---|---|
| **A** | **Generate the shared declaration from the NSwag output, inside the regen command; clients unchanged** | **CHOSEN (D1/D2/D3).** Satisfies the owner's ruling; zero NSwag-config risk; owner's commands unchanged; closes the whole 12-enum class; preserves the per-host seam; places the check where the defect is created (ADR-0031 D1). |
| **B** | **NSwag `excludedTypeNames` + `extensionCode`: clients import one shared enum** | **REJECTED (D3).** One nominal type workspace-wide is a genuine gain, but it (i) deletes the per-host drift signal — the only reason §1.1's fossil is legible, (ii) rests on unverified NSwag behaviour in a command **no agent can run**, (iii) makes the generated client non-self-contained. Revisitable by a superseding ADR if the emission path is ever verified. |
| **C** | **A fourth generated artifact straight from `Cleansia.Core.Domain.Enums`** (reflect over the assembly, or a 4th enum-only host endpoint) | **REJECTED.** The most literal reading of *"use backend enums on frontend"*, and it points at the **domain**, not the **contract**. `OrderStatus.cs:5` carries `[SwaggerEnumAsInt]` — the wire rendering is a *decision*, separable from the C# declaration; a domain enum the API does not expose, or one rendered differently, would give shared code a symbol that disagrees with all three clients. Also: a new tool, a new owner command, a backend build inside a frontend pipeline — a **fifth** pipeline to keep alive. |
| **D** | **Keep the hand-written mirror; keep the parity spec** (the status quo `Q-ENUM-01` shipped as its default) | **REJECTED — and the owner ruled it out.** Independently: it is a detector, not a fix (§1.3 — no compiler checks the integers), and §1.4 shows the detector **does not run in CI on a regen commit**. Also its `GENERATED_CLIENTS` list missed the one copy that had actually drifted (§1.1). |
| **E** | **Add the clients to `models`'s `implicitDependencies` so the spec is affected by a regen** | **REJECTED as the mechanism, RECORDED as available (D4).** Cheap, but it adds a project-graph edge into `scope:partner` territory with `enforce-module-boundaries` implications needing their own verification — and it still fires *after* the regen, where D1 fires during it. |
| **F** | **A `check-consistency.mjs` line rule comparing the enum blocks** | **REJECTED.** It would be a second, weaker implementation of the generator's own comparison, in a tool whose header calls its checks "heuristic, line-based… necessary, not sufficient", and it could only ever *detect*. If you are going to parse the enums, write the file. |
| **G** | **Emit the shared enums as `const` objects + union types instead of `enum`** | **REJECTED, and the tempting reason for it is wrong.** It would not buy assignability — an enum member type is not assignable to a numeric literal type either — so it would not remove the `| number` widening (residue 1). It would only churn the three pipes' `switch` bodies. |
| **H** | **Do nothing until a real drift incident** | **REJECTED — the incident already happened** (§1.1) and has been sitting in the tree, unseen by the guard added to catch exactly this. |

---

## 7 — The catalog edit, bound to acceptance (literal text; lands with T-0547, not before)

`agents/knowledge/patterns-frontend.md`, §"Module boundaries…", currently ends with a paragraph
beginning **"The fix, and the general rule: a wire enum a shared lib needs is declared in
`@cleansia/models`…"** and ending **"…is an Architect call on owner-run generation: `Q-ENUM-01`."**
That paragraph states the rule this ADR replaces. It is **replaced in full** by:

> **The fix, and the general rule: a wire enum a shared lib needs is GENERATED into `@cleansia/models`
> by the regen command — never hand-written (ADR-0041, answering `Q-ENUM-01`).** Shared code may not
> import any `*-services` client, so the symbol has to exist somewhere a `scope:shared` lib can read.
> It is **not** re-typed by a human. `tools/generate-wire-enums.mjs` runs inside every
> `npm run generate-*-client`, reads the client files named by the `nswag-*.json` `output` keys, keeps
> the enums **all** clients declare, **fails the owner's regen** if any of them disagree, and writes
> `libs/shared/models/src/lib/models/wire-enums.generated.ts`. Import wire enums from
> `@cleansia/models` in shared code exactly as before; **never** add a hand-written `export enum` to that
> lib for something a client already emits — the three-copies-plus-a-mirror scheme produced a silently
> renumbered `OrderStatus` that sat in the tree undetected (ADR-0041 §1.1). The three per-host clients
> keep emitting their own copies **deliberately**: they are three contracts, and one shared symbol would
> hide a host that had fallen behind (ADR-0041 D3). Note the shared symbol is a **constant table, not a
> type** — TS numeric enums are nominal across declarations, so a pipe takes `… | number` and the
> generator's agreement check is the only thing verifying the integers.
>
> **Enforced by:** `tools/generate-wire-enums.mjs` (runs inside every regen entry point) — **T1-CI at the
> regen**, plus `wire-enums.generated.spec.ts` in `nx test models` as a committed-state backstop
> (**T2-ADVISORY on a regen-only commit** — `models` is not Nx-affected by a client change; see
> ADR-0041 §1.4).

---

## Challenges pre-answered (author's anticipation — the panel writes in `## Challenge`)

*(Precedent label: ADR-0024/0025/0026/0027/0031.)*

| # | Expected challenge | Author's position |
|---|---|---|
| C1 | "The owner said *use the one generated from nswag*. You are generating a **fourth** file, not using one of the three." | The three cannot be *used* by a `scope:shared` lib — that is the entire premise of `Q-ENUM-01` and the boundary whose repair retired 13 cycles. What the ruling forbids is **hand-maintenance**, and D1 removes it: every value in the shared file comes out of the NSwag output by machine, traceable to `Cleansia.Core.Domain.Enums`. If the panel reads the ruling as *"exactly one declaration in the workspace"*, that is Option B and D3 answers it on three grounds — the strongest being that it deletes the drift signal. |
| C2 | "You are keeping four copies. That is barely better than five." | Count is not the metric; **provenance and gating** are. Four machine-written copies with a hard failure on disagreement is categorically different from four copies where one is typed by hand and the comparison runs in a CI step that (§1.4) does not execute on the relevant commit. |
| C3 | "The three clients agree today — where is the actual problem?" | §1.1. A **fifth** copy exists and has already drifted through a **renumbering** (`OnTheWay=3` vs `InProgress=3`), the guard added this sprint does not cover it, and `CLAUDE.md` points agents at it. |
| C4 | "The parity spec already solves this — just add the fifth file to its list." | It would not run (§1.4), and it cannot check what matters (§1.3 — the integers are compared by no compiler; the spec compares them only when Nx decides to run it). Also `SortDirection` has no spec at all. Detection placed where it cannot execute is the ADR-0031 defect verbatim. |
| C5 | "Reading generated output is two hops from the backend. Read the swagger documents." | The web swagger documents are **not committed** and exist only while three hosts are running; the emitted clients are on disk and re-derivable any time. Same trade the existing parity spec already made, and the reason step 3 of §3 needs no backend at all. |
| C6 | "Emit the union, not the intersection — a shared lib may want `RefundReason`." | An admin-only enum in a `scope:shared` lib means the code is not shared, or the enum should be on all three hosts. Both are decisions. D1.6 *prints* the non-intersecting names so the next person sees them instead of hand-typing a mirror. |
| C7 | "A `*.generated.ts` inside a hand-written lib is a smell." | Conceded as a new shape; mitigated by the header, by V4, and by D4's committed-state check. The alternative — a whole new lib for one file — buys nothing and costs a `project.json`, tags, a jest config and a barrel. |
| C8 | "This should just be part of the formatter scripts." | The formatters are **per client** and `sed`-based; this check is inherently **cross-client** (it compares three files) and must run once after all of them. Putting a cross-client comparison in a per-client script would run it against a half-regenerated tree in the `generate-partner-client` path. |
| C9 | "You did not verify NSwag's `excludedTypeNames` behaviour, so B is dismissed on ignorance." | Partly fair, and stated as such in D3(ii) — but the *inability to verify* is itself the argument: an agent cannot run the owner's regen, so shipping an untested change into it is the risk, not the ignorance. D3(i) is the ground that does not depend on verification at all, and it is the one I would defend if `excludedTypeNames` turned out to work perfectly. |

---

## Challenge

*(Panel challenger — one round requested on §D1/§D2 placement and §D3. **§0 is owner-ruled and is not in
scope.** T-0546 carries the round.)*

## Defense

*(Author.)*

## Verdict

*(Panel lead — a different instance from the author. Not yet convened; this ADR is `proposed`.)*
