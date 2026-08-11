# Generated API clients — the drift contract (living decision notes)

> Companion to the ADRs on this topic:
> `agents/backlog/adr/0031-nswag-regen-drift-is-guarded-at-regen-time.md` (**ADR-0031**, accepted
> 2026-07-30 — *call sites* vs a regen) and
> `agents/backlog/adr/0042-shared-wire-enums-are-generated-from-the-nswag-output-at-regen-time.md`
> (**ADR-0042** — the *shared enum declaration*, answering `Q-ENUM-01`). An accepted ADR is immutable;
> this file is the *evolving* design notes, trade-off space, and current shape. Update this when the
> design evolves; supersede an accepted ADR for a real decision change.
>
> 🟠 **ADR-0042 is `proposed`. Round 1 was returned 2026-08-05; a REBUILT round-2 draft was authored
> 2026-08-06 and is awaiting a fresh challenger and lead — nothing on this page's §"The second surface"
> may be built against until it is `accepted`.** Round 1's mechanism (values derived from the NSwag output,
> gated inside the owner's regen command) was ruled vacuous on the command it lived inside and blind to the
> incident it cited. **Round 2 inverts the authority and the placement:** the `[SwaggerEnumAsInt]`-marked
> **C# declaration** is the authority, membership still comes from the `nswag-*.json` `output` keys, and
> the gate is a dependency-free repo-root checker triggered on **`src/**/*.cs` as well as
> `src/Cleansia.App/**`**. See ADR-0042 §D1–§D6, §8 (the RB-1…RB-7 answers) and `## Verdict — round 1`.
> **Only ADR-0031's half of this page is in force.** *(This header previously cited ADR-0042 under a
> `0041-…` filename — that path does not exist and `0041` is a different decision. Corrected 2026-08-05.
> The ADR's **filename** is still round 1's slug and is deliberately not renamed until acceptance, so this
> pointer keeps resolving.)*
> Cross-links: `agents/process/quality-gates.md` §"After an NSwag regen…" (the binding rule),
> `CLAUDE.md` §"NSwag Client Generation" + §"Manual Steps (owner does these)", ADR-0019 (the **iOS**
> generated client, a different pipeline governed separately).

## Scope

The **web** generated clients only — the three NSwag-generated TypeScript clients under
`src/Cleansia.App/libs/core/*/src/lib/client/`, **and (once ADR-0042 lands) the shared wire-enum artifact
derived from them**. The Android/iOS generated clients come from the separate owner-only
`mobile-spec-regen` and are **not** covered by anything on this page (ADR-0031 residue #4).

> **Why mobile is out of scope is structural, not scheduling — corrected 2026-08-05 (ADR-0042 Verdict
> §V.6, verified first-hand).** The mobile generators emit **nameless ordinal** members —
> `src/cleansia_ios/CleansiaPartnerApi/Models/OrderStatus.swift:13-21` is `case _0 = 0 … case _6 = 6`, and
> `src/cleansia_android/partner-app/build/generated/openapi/…/model/OrderStatus.kt:28-49` is
> `@SerialName("0") _0(0) … _6(6)`. `_N = N` is an identity map with no semantic content, so **there is no
> name→integer table on mobile to compare** and the web fossil's failure mode (a *name* bound to the wrong
> *integer*) is structurally impossible there. The real mobile risk lives in the hand-written mapping code
> (`OrderStatusPresentation.kt`, `OrderPrimaryAction.swift`), and `agents/tools/check-available-status-parity.mjs`
> **already gates it for `OrderStatus`** across eight web/Android/iOS surfaces against the canonical C#,
> in its own repo-root workflow. The true residue is narrower and worse: **nothing gates the other eleven
> shared enums on any stack**, and the hand-written mirror class reaches mobile too — `RecurrenceFrequency`
> is typed by hand in C#, TypeScript, **Swift** (`CleansiaCustomer/…/RecurringModels.swift:3-7`) and
> **Kotlin** (`cz.cleansia.customer.core.recurring.RecurrenceFrequency`), with no client rendering it on
> any stack and no gate anywhere. ADR-0042 round 2 pins the C# and TypeScript halves; the Swift/Kotlin
> halves stay open and belong to `check-available-status-parity.mjs`, not to the wire-enum tool.
> *(The earlier text here — "the same 12 enums exist a third and fourth time on mobile with nothing
> comparing them to the web ones" — was wrong in both directions.)*

---

## Current shape (as of ADR-0031, 2026-07-30)

### The three clients and who writes them

| Client | NSwag config | Output (the ONLY generated file) | Barrel |
|---|---|---|---|
| Partner | `nswag-partner.json:39` | `libs/core/partner-services/src/lib/client/partner-client.ts` | `libs/core/partner-services/src/index.ts` |
| Admin | `nswag-admin.json:39` | `libs/core/admin-services/src/lib/client/admin-client.ts` | `libs/core/admin-services/src/index.ts:3` |
| Customer | `nswag-customer.json:39` | `libs/core/customer-services/src/lib/client/customer-client.ts` | `libs/core/customer-services/src/index.ts` |

**A generated client is never hand-edited.** Regeneration is an **owner-only** step
(`quality-gates.md` §"Owner-only steps"); agents flag it as a `manual_steps` entry and block dependent
work until the owner confirms.

> ✅ **Stale duplicate — CLOSED. The file is gone (verified 2026-08-05: `libs/core/services/**/client/**`
> matches nothing).** Kept here as history because the lesson outlives the file, and because two
> statements about it circulated as current and were false.
>
> `libs/core/services/src/lib/client/admin-client.ts` was written by **no** `nswag-*.json`, exported by no
> barrel, imported by nothing, and typechecked by neither the regen guard nor the three production
> builds. ADR-0031 recorded it as hygiene (residue #5a); it was worse than that. Its `OrderStatus` read
> `Pending=1 Confirmed=2 InProgress=3 Completed=4 Cancelled=5` — **a pre-renumber fossil** against a live
> contract of `New=0 Pending=1 Confirmed=2 OnTheWay=3 InProgress=4 Completed=5 Cancelled=6`, so **every
> integer from 3 up meant something else**: `3` rendered "In progress" for a cleaner merely *on the way*,
> `4` rendered "Completed" for a job *in progress*. Its `PaymentStatus` was missing `PartiallyRefunded=6`.
> The sprint's parity spec never covered it (`order-status-enum-parity.spec.ts:12-16` lists the three live
> clients).
>
> **Two corrections, 2026-08-05 (ADR-0042 Verdict §V.7):**
> - **It was deleted this session, not by a future ticket.** ADR-0042 §D5 (*"T-0547 deletes it"*) is a
>   **no-op** and the row was removed from this page's rollout table.
> - **`CLAUDE.md` never said what was quoted.** `CLAUDE.md:34-35` applies *"NSwag-generated API clients"*
>   to `core/{partner,admin,customer}-services/` and labels `core/services/` **"(hand-written)"** — i.e.
>   the map is correct and the owner-gated `MANUAL_STEP` proposed against it is **WITHDRAWN**. The claim
>   was inherited from ADR-0031 residue #5(a) and re-asserted under a "verified at HEAD" header:
>   **an inherited citation is not a verified one.**
>
> **The general lesson, worth more than the instance, and it survives untouched:** *"written by no
> `nswag-*.json` `output` key"* is the only sound definition of "not a generated client". Any tool that
> reasons about the client set must derive it from those keys, or this file re-appears as a fourth
> "client" to whoever is globbing. **How it was actually found matters too:** a human measured five
> declarations against the C# source and closed it with a `git rm` — no client-to-client comparison could
> have seen it, because that file is not a client by the definition above.

### The emission rule that makes a backend field a compile break

All three configs set **`markOptionalProperties: false`** (`nswag-*.json:31`). Consequence:

```
backend DTO member is NULLABLE   ──NSwag──▶   accessInstructions: string | undefined;
                                              ^^^^^^^^^^^^^^^^^^  REQUIRED key, OPTIONAL value
```

TypeScript requires the **key** to be present in every object literal. So adding one nullable field to a
backend command breaks **every existing** `new XCommand({ … })` call site at once — **122** of them
outside the generated clients today, and growing. This is the entire defect class.

### Where the check lives, and what each placement is worth

```
owner edits a backend DTO
        │
        ▼
npm run generate-{partner|admin|customer}-client       ← the ONLY regen entry points (ADR-0031 M1)
        │  nswag run  →  formatter  →  npm run typecheck
        │                               ngc --noEmit over EVERY apps/*/ compilation unit
        ▼                               (Angular compiler: TS + template diagnostics)
   ✅ PREVENTS a red master on this path — names file:line before a commit exists
        │
        │   …any other path (hand-run generator, hand-edited client, some future script)
        ▼
git push master  ──▶  frontend-ci.yml (push: master, paths-scoped)
        │              3 production builds — the AUTHORITY
        ▼
   ⚠️ ATTRIBUTES ONLY — master is already red; the red now lands on the offending
      commit within minutes instead of ambushing the next contributor's PR
        │
        ▼
   ❓ nothing prevents that red — the option that would (branch protection) is Q-CI-01
```

**Do not call these "primary and backstop."** ADR-0031 CH-3 was sustained precisely because that phrasing
implies the class is covered. One leg prevents on one path; the other prevents nothing anywhere.

### The guard's coverage, stated structurally

`tools/typecheck-apps.mjs` is handed each app's `tsconfig.app.json` — the same file the production build
target passes to `@angular/build:application` (`apps/cleansia.app/project.json:26`). Its file set is
therefore the build's compilation unit **by construction**:
`include: ["src/**/*.ts", "server.ts"]` plus everything those files transitively import, with `strict` +
`strictTemplates` inherited from the app's own `tsconfig.json:15-21`.

- ✅ **Covers:** TypeScript diagnostics and **Angular template** diagnostics over each app's unit —
  including generated types/enums reached only from a template, which plain `tsc --noEmit` cannot see
  (proven: `ngc` exited 1 with `TS2339` where `tsc` exited 0).
- ❌ **Does not cover:** bundling, budgets, SSR prerender, styles. The three production builds remain the
  authority; the prose rule "build all three before pushing" is **unchanged and still binding**.
- ❌ **Also outside:** `.spec.ts` files (excluded at `tsconfig.app.json:8-13`) and lib files unreachable
  from every app entry — but *outside by the same configuration that puts them outside a production
  build*, so the prose rule never covered them either.
- **State coverage structurally, never empirically.** "N call sites fall outside" decays with every
  commit; *the guard's tsconfig is the build's tsconfig* does not. Anything outside the guard is outside
  the three builds too. (The T-0439 `tsc --listFiles` enumeration that found exactly two such files is
  evidence *for* the identity, not a substitute for it.)

---

## The second surface: shared **wire enums** (ADR-0042 — `proposed`; round 1 returned 2026-08-05, **round 2 rebuilt 2026-08-06, awaiting a second panel**; answers `Q-ENUM-01`)

> 🟠 **Nothing in this section is in force.** The problem statement survived an adversarial panel; round
> 1's mechanism did not and has been rebuilt. Do not build against the diagram, the invariants or the
> rollout table until ADR-0042 clears its second panel. `Q-ENUM-01` therefore stays **open** — its
> owner-ruled half (the hand-written mirror goes; the shared declaration must be machine-written and
> traceable to `Cleansia.Core.Domain.Enums`) is settled; the *how* is proposed, not accepted.

ADR-0031 guards *call sites* against a regen. Nothing guarded *two generated clients against each
other*, or the hand-written copy a `scope:shared` lib was forced to keep. That is this section.

### Why a shared copy has to exist at all

`patterns-frontend.md` §"Module boundaries": a `scope:shared` lib may not import any `scope:<app>`
client. Three shared pipes (`order-status-severity`, `order-status-icon`, `payment-status-severity`)
need `OrderStatus`/`PaymentStatus`. So the symbol must exist somewhere shared code can read — and the
first answer was to **type it by hand** in `libs/shared/models`. The owner ruled that out (2026-08-04):
*"better to use the one that is generated from nswag… consider using backend enums on frontend instead
of generating your own."*

### Actual shape today (unchanged — the refactor has not landed)

```
Cleansia.Core.Domain.Enums.OrderStatus        ← the source of truth (C#, [SwaggerEnumAsInt])
        │  three hosts' /swagger/v1/swagger.json  (NOT committed; exist only while the hosts run)
        ▼
  nswag-{partner,admin,customer}.json  ──▶  three per-host clients   (each declares its own enums)
                                                  │
  libs/shared/models/.../order-status.models.ts  ←─┘  HAND-WRITTEN mirror  ⚠️ still on disk
  libs/shared/models/.../sort-types.models.ts    ←─┘  HAND-WRITTEN SortDirection, no spec at all
        │
        ▼   read by 3 pipes as a CONSTANT TABLE (no compiler checks the integers)
  order-status-enum-parity.spec.ts   ← detector only. It now RUNS and cannot cache-replay
                                       (the {workspaceRoot} client glob landed on models' test
                                       target) — but it compares clients to the shared table and
                                       NEVER to Cleansia.Core.Domain.Enums, over 2 of 12 enums.

  …and two more hand-typed mirrors no client-derived rule can reach:
  cleansia-customer-features/.../recurring-bookings.models.ts:6  RecurrenceFrequency
        └─ no client renders it (the C# enum has no [SwaggerEnumAsInt]); the same table is
           ALSO hand-typed in Swift and Kotlin — four stacks, zero gates
  cleansia-customer-features/.../disputes.models.ts:12           CustomerDisputeStatus
        └─ a renamed mirror of Enums/DisputeStatus.cs; only the ADMIN client declares it
```

### The shape ADR-0042 **round 2** proposes *(2026-08-06, NOT in force — awaiting a second panel)*

```
Cleansia.Core.Domain / Cleansia.Core.AppServices   ← THE AUTHORITY (one [SwaggerEnumAsInt] enum per name)
        │                                             12 names, each resolving to exactly ONE .cs declaration
        │  (three hosts' swagger, uncommitted)         — two of them OUTSIDE Core.Domain/Enums/
        ▼
  nswag-{partner,admin,customer}.json  ──▶  three per-host clients   ← MEMBERSHIP (which enums, which members)
        │                                                                the intersection of all three
        ▼
  agents/tools/check-wire-enum-parity.mjs
        │   compares C# × 3 clients × the shared table; grades by WHO CAN REPAIR IT
        │     CONFLICT / ORPHAN / AUTHORITY / SCOPE / VACUOUS  → P0, exit 1, refuse to write
        │     PENDING-REGEN (C# ahead of a client)             → P2, --baseline ratchet, never fatal
        ├──▶ --write  →  libs/shared/models/.../wire-enums.generated.ts   (chained into the regen, NO tier)
        └──▶ gate     →  .github/workflows/wire-enum-parity.yml           (T1-CI, self-test first)
                          triggers: src/**/*.cs  AND  src/Cleansia.App/**
```

Also in round 2: **§D6** pins the two hand-written mirrors a client-derived rule cannot reach —
`RecurrenceFrequency` (no client renders it; hand-typed on **four** stacks) and `CustomerDisputeStatus`
(renamed, admin-only on the wire) — by name-matched discovery plus one declared alias. That sweep is the
exact complement of the `output`-key definition of "client", which is the set the 2026-08-05 fossil
belonged to.

**Retired by round 2, and this is its most contestable move:** `order-status-enum-parity.spec.ts` and the
`{workspaceRoot}` client glob on `models`' `test` target go together — subsumed (2 enums vs 12, never the
authority), a second parser (RB-5), and its per-client `toEqual` is *unsatisfiable* whenever the three
clients differ, which is exactly what a single-host regen produces. The `inputs` **lesson** at
`patterns-frontend.md:793-827` stays; only the `**Enforced by:**` line moves.

### Why round 1 was returned *(kept — these four rulings are what shaped round 2)*

Round 1 proposed: `tools/generate-wire-enums.mjs` runs **inside** every `npm run generate-*-client`,
between the formatter and the ADR-0031 typecheck; it derives the client set from each `nswag-*.json`
`output` key, keeps the enums all three clients declare, **fails the regen if the three disagree**, and
writes one `wire-enums.generated.ts` into `libs/shared/models`.

**The panel's ruling (2026-08-05), in four lines:**

1. **The authority cannot be wrong.** The three clients are three renderings of **one** C# declaration in
   an assembly every host shares, so at one backend commit they cannot disagree. `client_A ≠ client_B` is
   provable only when they were generated against **different backend commits** — the check is a
   *staleness-skew detector*, not an enum-correctness detector, and on `generate-clients` (all three, one
   backend, one command) it **cannot fire at all**.
2. **The placement guards the repair, not the creation.** ADR-0031's defect is created *by* the regen, so
   guarding the regen is prevention. A shared table that disagrees with the domain is created by a
   **backend commit**; the regen is when it is *repaired*. The window between the two — the repo's normal
   state, since regen is owner-only and rare — is unguarded by construction.
3. **The two paths are a pincer.** Vacuous on `generate-clients`; on a single-host regen it *does* fire —
   and then `exit 1` short-circuits the `&&` chain, leaving a fresh client plus a stale shared file and a
   command that cannot go green without booting the other two hosts. Cross-host coupling introduced
   through the build pipeline, by the decision whose stated rationale is host independence.
4. **The declared tier was not an available label**, and the proposed backstop's host cannot work
   (below).

**What survived and is reusable verbatim:** the diagnosis (three points below), discovery of the client
set from the `nswag-*.json` `output` keys, the intersection-not-union membership rule, "import, do not
re-export" for `SortDirection`, and the ruling that the three clients keep emitting their own copies.

### The four facts — three of which survived the panel intact

*(Fact 1 is restated in the past tense per ADR-0042 Verdict §V.7; facts 2–4 were attacked and held.)*

1. **There were five declarations, not four** — the three clients, the hand-written mirror, **and the
   stale `libs/core/services/.../admin-client.ts`, which had already drifted through a renumbering.** The
   defect class is not hypothetical; it shipped and sat unseen. **But note how it was closed:** a human
   compared five declarations against the C# source and deleted the file (this session). No
   client-to-client comparison could have found it — by the `output`-key definition that file was never a
   client — so this incident is evidence for the *problem*, not for the returned mechanism.
2. **It is a class: 12 enums, ×3 clients = 36 generated declarations** — `AppliedDiscountSource`,
   `ConsentType`, `ContractStatus`, `EmployeeEntityType`, `EmployeeInvoiceStatus`, `OrderStatus`,
   `PaymentStatus`, `PaymentType`, `PayoutDetailsStatus`, `PayoutScheme`, `PhotoType`, `SortDirection`
   (partner declares 15 enums, customer 19, admin 27; the intersection is those 12). **Each of the twelve
   names resolves to exactly ONE `public enum` across `src/**/*.cs` and each carries `[SwaggerEnumAsInt]`
   — verified 2026-08-06; two of them live outside `Core.Domain/Enums/`** (`Sorting/Common/SortDirection.cs`,
   `Core.AppServices/Shared/DTOs/Enums/AppliedDiscountSource.cs`), which is why any C#-reading tool must
   search `src/**/*.cs` rather than a folder.
   **And the hand-typed side is five, not three** (swept 2026-08-06): `OrderStatus` + `PaymentStatus`
   (guarded), `SortDirection` (`sort-types.models.ts:16-19`, **no** spec), plus two a client-derived rule
   cannot reach at all — `CustomerDisputeStatus` (`disputes.models.ts:12`, a *renamed* mirror of
   `Enums/DisputeStatus.cs`, which only the admin client declares) and `RecurrenceFrequency`
   (`recurring-bookings.models.ts:6`, mirroring `Bookings/RecurrenceFrequency.cs`, which **no** client
   renders, and which is *also* hand-typed in Swift and Kotlin). All five agree with their `.cs` source
   today. **The class is "a hand-typed name→integer table that must match the wire", not "the shared lib's
   copy".**
3. **The shared enum is a constant table, not a type.** TS numeric enums are *nominal*, so
   `@cleansia/models`'s `OrderStatus` is not assignable to `customer-client`'s. The pipes compile only
   because their parameter admits `number` (`order-status-icon.pipe.ts:13`), which is exactly what the 14
   template call sites feed them under `strictTemplates`. **So no compiler anywhere checks the
   integers** — which is why *"declare it by hand and add a parity spec"* is a detector, not a fix.
   *(Corrected 2026-08-05: the original clause read "which is why the check must be generation, not
   comparison." That inference does not follow — generating the file from artifacts that are all
   renderings of one declaration still leaves the integers unchecked. The premise stands; the conclusion
   was the returned mechanism.)*
4. **The parity spec had two independent holes — affected-gating and cache replay — and BOTH ARE CLOSED AT
   HEAD.** *(Rewritten 2026-08-06. The 2026-08-05 text below the line was correct when written and its
   conclusion — "no Nx-hosted variant works" — was reached against the wrong mechanism.)*
   - *What was true:* the spec reads the clients **off disk** (an import would be the scope break), so the
     Nx project graph has no `models → *-services` edge — and undeclared, those files were inputs to
     nothing. So `nx affected` did not select `models` on a regen-only commit, **and** any invocation that
     *did* select it could replay a cached PASS computed over different client bytes.
   - *What closed it, and it is one line:* `libs/shared/models/project.json:14-19` now declares
     `"{workspaceRoot}/libs/core/*-services/src/lib/client/*.ts"` as an input of the `test` target.
     **An Nx `inputs` entry is a hashing statement, not a graph edge** — `nx graph` is unchanged and no
     cycle forms. Measured (`patterns-frontend.md:801-805`): undeclared, a warm `nx test models` **replayed
     a green over renumbered bytes**; declared, the hash moves and the run is red, and a client-only diff
     selects `models` (12 test-target projects → 63).
   - *So the ADR-0042 Verdict §V.4 ruling "no Nx-hosted variant of this check works" is too broad and does
     not survive.* The limb that **does** survive, and that decides ADR-0042 round 2's placement: **an Nx
     input cannot name a path above the workspace root** (`src/Cleansia.App`), so a check that reads a
     `.cs` file, a Kotlin literal or a Swift literal cannot be hosted in an Nx task at any price
     (`offerability-parity.yml:8-14` states exactly this).
   - *The `implicitDependencies` escape hatch stays rejected, on a simpler ground:* it **is** a graph edge
     where an input is not, which makes it the wrong instrument regardless. **The cycle once cited for it
     (`models → partner-services → partner-stores → models`) no longer exists** — T-0455 removed the middle
     arrow and `libs/core/partner-services` imports zero `@cleansia/partner-stores` symbols at HEAD
     (verified 2026-08-06). `patterns-frontend.md:820-822` still asserts that chain and is stale on it;
     ADR-0042 §7a(ii) carries the correction.
   - *What the spec still cannot do, and this is now the whole remaining problem:* **it never reads the
     authority** (`patterns-frontend.md:852-856` says so in its own words), it covers 2 of 12 enums, and its
     per-client `toEqual` (`order-status-enum-parity.spec.ts:62-72`) is **unsatisfiable whenever the three
     clients differ** — which is exactly what a single-host regen produces. `SortDirection` has no spec at
     all.

   The three unconditional builds are green regardless, because a wrong integer is not a type error.

### What the clients do NOT do, and why (the deliberate part)

**The three clients keep emitting their own copies.** `Q-ENUM-01` asked whether they should stop; the
answer is no. Five API hosts are **deployed and regenerated independently**, so three clients from three
specs is a faithful rendering of three contracts. One shared symbol imported by all three would let a
client regenerated against a host that is a commit behind **claim** the current contract — the
divergence would become structurally unobservable. **Collapsing the copies does not remove the drift; it
removes the evidence of it**, which is the only reason the stale fossil above is legible at all. Two
supporting grounds: the `excludedTypeNames`/`extensionCode` path is unverified NSwag behaviour inside a
command **no agent can run**, and it would make `partner-client.ts` non-self-contained (the derived
artifact becoming an input to the artifact it derives from).

**Conceded plainly:** the refactor would leave four declarations, down from five. The count is not the
metric — *provenance* (none typed by a human) is. **The second half of that sentence used to read
"*gating* (disagreement is unshippable)"; struck 2026-08-05** — the returned mechanism does not deliver
it (see the pincer above), and no wording may claim it until a rebuilt mechanism earns it.

**The tension §V.5 named, and how round 2 resolves it.** The paragraph above treats a client-to-client
divergence as **evidence worth preserving**, while round 1's D1 treated the same divergence as an **error
worth halting on**. Round 2's answer, in one sentence: **a client-to-client divergence is evidence — it is
graded `PENDING-REGEN`, reported, ratcheted, and never fatal**; what is fatal is a *conflict with the
authority*, which is a different observation with a different cause. Round 1 could not draw that line
because it had no authority to be in conflict with.

### Invariants this WOULD add — **NOT IN FORCE** (bound to a rebuilt, accepted ADR-0042)

> Numbered 11–16 for continuity with the ADR-0031 list above. **A reviewer enforces 1–10 only.** 13 and
> 14 survived round 1's panel unchanged; 11, 12, 15 and 16 are restated against the round-2 draft.

11. *(pending)* **No wire-enum integer table anywhere in the web tree is hand-written and unchecked.** If
    all three clients render it, the tool *writes* it into `@cleansia/models`. If no client renders it, or
    it is deliberately renamed, it stays hand-written and is *pinned* to its `.cs` declaration. Grep
    `src/Cleansia.App` for `export enum` and cross-check each name against `src/**/*.cs` — an unpinned
    match is a violation.
12. *(pending)* **The gate can go red in CI on the commit that creates the drift, and that commit is a
    BACKEND commit.** The workflow's `paths` must cover `src/**/*.cs` as well as `src/Cleansia.App/**`, and
    must be a superset of the paths the tool reads. A regen-time step may run, but it is a *writer with no
    verdict* and carries **no tier**.
13. **The client set is derived from the `nswag-*.json` `output` keys** — never from a glob, never from a
    hardcoded list. A config whose output file is missing is a hard failure. *(ADR-0031 M2's property,
    applied to a second tool; it is also what makes "is this a generated client?" mechanically decidable.
    **Survived the panel; keep verbatim.**)*
14. **Anti-vacuity.** Zero clients read, zero enums found, or an empty intersection ⇒ exit 1. A tool that
    writes an empty file and exits 0 is a non-run, not a pass (ADR-0032 D3). **Survived, and extends:** a
    run in which the *authority itself* was not read — missing `.cs`, two declarations of one name, or a
    missing `[SwaggerEnumAsInt]` on an enum in the client intersection — is also `exit 1`.
15. *(pending)* **A divergence is graded by who can repair it.** A name bound to two integers, or an
    integer bound to two names, anywhere among {C#, the three clients, the shared table} ⇒ hard failure.
    A member C# declares that a client has not picked up ⇒ a **pending owner-only regen**, carried on an
    exact-match `--baseline` ratchet, never a hard failure. Confusing the two is what turns a
    `manual_step` into a merge blocker.
16. *(pending)* **`scope:shared` code reads `@cleansia/models`; app code reads its own client.** The
    integers are identical, which is exactly why the wrong import spreads silently. Baseline zero
    (2026-08-06).

### Known gaps on this surface (accepted, named)

| # | Gap | Bound |
|---|---|---|
| E1 | The pipes keep `\| number` in their signatures — nominal enums make assignability impossible across declarations | inherent to TS; it is *why* an integer-level check is load-bearing (no compiler does it). Do not "fix" the widening without reading ADR-0042 §1.3 |
| E2 | A client regenerated **outside** the wrapper skips any regen-time tool | ADR-0031 M1's residue, inherited — **and it stops mattering under round 2**, because the regen-time step is only a *writer* and the CI gate runs on `src/Cleansia.App/**` regardless of how the client got there |
| E3 | An enum only *some* hosts expose is not emitted; if a host stops exposing one that shared code uses, it silently leaves the file | the shared consumer then fails to compile **when the tool next runs**, by name — the correct loud failure. The tool *prints* the non-intersecting names so nobody hand-types a mirror |
| E4 | ~~Mobile clients declare the same 12 enums a third and fourth time; nothing compares them to the web ones~~ **FALSE IN BOTH DIRECTIONS — corrected 2026-08-05, see §Scope.** Mobile emits nameless ordinal identity maps (no name→integer table to compare), and `check-available-status-parity.mjs` already gates `OrderStatus` across web/Android/iOS against the canonical C# | the true residue: **nothing gates the other eleven shared enums on any stack.** Round 2 closes the **web declaration** half; the named open instance is `RecurrenceFrequency`'s **Swift + Kotlin** halves, which belong to `check-available-status-parity.mjs`, not to the wire-enum tool. ADR-0019 / `mobile-spec-regen` still own the mobile pipeline |
| E5 | The **deployed** contract is unobservable from this repository | every artifact here is at HEAD or older. A `--baseline` records the gap between HEAD and the last regen; nothing records the gap between the last regen and production. This is why the invariant is *"no two of them may disagree"*, never *"they are right"* |
| E6 | A hand-written mirror under a **new** name is invisible until someone adds an alias entry | the alias map is a **closed roster** (one entry today) and must be declared as one wherever it is cited — `conventions.md:246-250` |

### Rollout state (ADR-0042) — **everything below is BLOCKED on the second panel**

| Step | Where | State (2026-08-06) |
|---|---|---|
| ADR (the mechanism decision + rejected options) | ADR-0042 | **`proposed` — round 1 returned 2026-08-05 (four blocking findings); round 2 rebuilt 2026-08-06, awaiting a fresh challenger + lead** |
| shared `agents/tools/lib/cs-enum.mjs` extracted; `check-available-status-parity.mjs` re-pointed at it | `agents/tools/` | **blocked on the ADR.** RB-5's answer, and it fixes a latent defect: that parser never decomments, so `Confirmed = 2, // note` is silently dropped |
| `check-wire-enum-parity.mjs` + its `os.tmpdir()` self-test | `agents/tools/` | **blocked on the ADR.** Ticket id is the PM's to allocate |
| `wire-enum-parity.yml` (self-test first, gate second, triggers on `src/**/*.cs` **and** `src/Cleansia.App/**`) | `.github/workflows/` | blocked on the ADR — this is the leg that earns the `T1-CI` token |
| shared generated file committed; `order-status.models.ts` deleted | `libs/shared/models` | **blocked on the ADR.** Still true and reusable: needs **no regen** — derived from the already-committed clients and `.cs` files |
| `SortDirection` folded in (`sort-types.models.ts` imports, does **not** re-export; `sort.models.ts:2` splits its import) | `libs/shared/models` | blocked on the ADR |
| `order-status-enum-parity.spec.ts` **and** the `{workspaceRoot}` client glob on `models`' `test` target **retired together** | `libs/shared/models` | blocked on the ADR — **the most contestable step**; reasoning at ADR-0042 §D4 |
| the three pipes | `libs/shared/pipes` | **no change** — they import the barrel, and the barrel keeps the symbol (round 1's table was wrong about this) |
| ~~Stale `libs/core/services/.../admin-client.ts` deleted~~ | ~~`libs/core/services`~~ | ✅ **DONE** (`2d913b8b`) — the file no longer exists. Round 1's §D5 was a **no-op**; ADR-0031 residue #5(a) closes |
| `patterns-frontend.md` §"Module boundaries" — 3 surgical edits (opening paragraph, the stale cycle sentence at `:820-822`, the `**Enforced by:**` + closing paragraph) | `agents/knowledge/` | **does not land** until acceptance. *(The previously-routed correction — the paragraph claiming a renumbering regen "fails `nx test models`" — has since become **true**: the input glob landed. The outstanding factual error is now the `implicitDependencies` cycle sentence.)* |
| `consistency.md` "Judgment calls" deviation entry | `agents/knowledge/` | **does not land** until acceptance. Required by `conventions.md:143-146` test 1 — five shipped declarations become deviations |
| `quality-gates.md` Gate 4 named item **WE-1** (a new `export enum` under `src/Cleansia.App/` is declared a wire mirror or not) | `agents/process/` | **does not land** until acceptance. It is the `T3-HUMAN` half: a *renamed* mirror has no mechanical witness, so the alias roster is closed by construction and the catalog must say so |
| ~~`CLAUDE.md` repo-map correction~~ | ~~`CLAUDE.md`~~ | **WITHDRAWN** — `CLAUDE.md:34-35` already says the right thing; no owner step is owed |
| `Q-ENUM-01` in `questions/open.md` | `agents/backlog/questions/` | ✅ **already corrected and correct at HEAD** — it records the owner's half as settled, the mechanism as returned, and stays open. No edit owed until the second panel rules |
| ADR filename still carries round 1's slug | `agents/backlog/adr/` | **deliberate.** Rename only in the commit that accepts it, together with the three pointers that name it (ADR-0042 §9) |

### Trade-off space — the axes, and what round 2 chose on each *(chosen ≠ accepted; the panel has not ruled)*

Two axes, and round 1 conflated them. The 2026-08-05 lead recorded the space without choosing; the
2026-08-06 author chose. Both are marked below.

**Axis 1 — what is the authority for the *integers*?**

| Option | Disposition |
|---|---|
| The three clients, compared to each other | **RETURNED (round 1).** Three renderings of one C# declaration; cannot fire on `generate-clients`; blind to all-three-stale, which is the normal state between owner-only regens |
| **The `[SwaggerEnumAsInt]`-marked C# declaration, with membership still from the clients** | ✅ **CHOSEN by round 2 (ADR-0042 §D1), pending the panel.** The technique is shipped and blocking today — `check-available-status-parity.mjs:134-165` parses a `.cs` file into a name→integer table with dependency-free Node, `offerability-parity.yml` gates it. It answers the owner's second sentence and subsumes client-staleness for free. **The cost the lead priced for the absent author, and how round 2 answers it:** the shared table must match what the **deployed** API emits and C# HEAD is not that, so a *blanket* hard failure on "clients disagree with HEAD" would turn an owner-only regen into a merge blocker. Round 2 therefore **grades by who can repair it** — hard-fail a conflict (one name, two integers), **ratchet** the pending-regen case on the house `--baseline`. The emitted bytes stay the clients' member-wise intersection, so a pending window changes nothing on disk |
| A committed swagger document per host | **Rejected (round 2).** The web swagger docs are **not** committed and exist only while the hosts run; committing them is a separate decision and would insert a *second* derived artifact between the domain and the clients |
| NSwag `excludedTypeNames` + `extensionCode` (one symbol, imported by all three clients) | **Rejected and it held** — it deletes the per-host drift signal, rests on unverified NSwag behaviour inside a command no agent can run, and makes the generated client non-self-contained. Revisitable only by a superseding ADR |

**Axis 2 — where does the check run, and how hard does it fail?**

| Option | Disposition |
|---|---|
| Only inside `npm run generate-*-client` | **RETURNED (round 1).** The regen is when this drift is *repaired*; it is created by a **backend commit**. A gate only at the repair moment leaves the whole creation→repair window unguarded, and `conventions.md:231`'s `T1-CI` (*"fails a CI job on the offending change"*) is unavailable to it — the tool appears in no workflow. **Round 2 keeps a regen-time step as a *writer with no verdict and no tier*,** which is why its pincer dissolves |
| An Nx jest spec (`nx test models`) | **Rejected (round 2) — but on ONE ground, not three.** The 2026-08-05 ruling *"no variant works"* is too broad: the `inputs` glob shipped and closes both affected-gating and cache-replay without a graph edge (fact 4). What decides it is that **an Nx input cannot name a path above the workspace root**, and this authority is a `.cs` file. Secondary: RB-5, a second parser over the clients |
| **A dependency-free Node checker outside the Nx workspace + its own repo-root workflow + a self-test that runs first** | ✅ **CHOSEN by round 2 (ADR-0042 §D4), pending the panel.** The shape standardized twice (`offerability-parity.yml`, `nx-project-registration.yml`), uncacheable by construction rather than by configuration. Trigger: **`src/**/*.cs`** (not a folder list — two of twelve authorities live outside `Core.Domain/Enums/`) **and** `src/Cleansia.App/**`. **The one-parser-per-source sub-question is answered by extraction:** `agents/tools/lib/cs-enum.mjs`, used by both tools |
| One unconditional `run:` line in `frontend-ci.yml` beside the "Regen-drift guard self-test" step | **Rejected (round 2) as insufficient, not wrong.** Un-affected-gated and un-cached, but that workflow's `push` trigger is paths-scoped to `src/Cleansia.App/**`, so it cannot see the **backend** commit — which is the commit that creates this drift |
| A `check-consistency.mjs` line rule | Rejected, correctly — that tool appears in **zero** workflow files and can never set an exit code. Rejecting it is not *considering* a CI gate |
| Regen-time step **plus** a CI leg | ✅ **What round 2 ships.** They compose: the CI leg is the gate and carries the `T1-CI` token; the regen-time step derives the file and carries no tier |

---

## Trade-off space (what was considered and why it landed where it did)

| Option | Axis | Verdict | Why |
|---|---|---|---|
| **A — typecheck chained to the regen command** | placement of a check | **CHOSEN** | Fires where the defect is *created*, before a commit exists. Mutation-provable (Gate 6.5) — three chained builds are not. **Not faster** than the builds (measured: 3 builds ≈ 120 s cold / 58.7 s warm; typecheck 28.5–69.4 s) — speed is *not* the argument. |
| B — chain the three production builds | placement of a check | rejected as mechanism, **retained as the rule** | Byte-identical to CI, but short-circuits at the first failing app (T-0438 broke all three), swings with cache state, writes `dist/`, untestable. |
| C — pre-push git hook | placement of a check | rejected | Not checked out by default, `--no-verify`-able, repo-wide tax for a one-command failure mode. |
| D — `markOptionalProperties: true` | remove the sharp edge | **rejected, BOUNDED** | Trades a loud compile error for a silent runtime bug — proven by the `accessInstructions` data-loss find. See "The open D question" below. |
| **E — branch protection / merge-via-PR** | *who may redden `master`* | **ESCALATED — Q-CI-01** | The only option on this axis; would have caught **both** incidents with zero new machinery. Constrains the **owner's** workflow → owner decides, not the panel. Composes with A; replaces nothing. |
| F — a `check-consistency.mjs` line rule | placement of a check | rejected | Required-key satisfaction is not line-local (spreads, conditional keys, variables, inheritance, generics). A type defect gets a type checker. |
| G — Nx-cache / `--incremental` the typecheck | cost | rejected | A mis-specified `inputs:` caches a green across a client change — the precise false green the guard exists to prevent. |
| H — a dedicated client-drift CI job | placement of a check | rejected (standing position **upheld**) | No job was added; the existing build gate was pointed at the branch where the damage lands. |

### The open D question (the one live thread)

D is rejected **today**, on a decisive counter-example: the `accessInstructions` compile error was the
only reason anyone discovered the booking wizard had been collecting entry instructions, rendering them
back on the summary step, and **discarding them at submit** since the field shipped
(`order-wizard.facade.ts:551`). Under D that literal compiles and the data loss continues.

But the rejection is **bounded**, for two recorded reasons:

1. **Measured cost of the current posture.** T-0438: three broken call sites, **one** semantic catch, two
   noise (wired `undefined`/`false`). Signal:noise **1:2**. *Today that is a good trade* — the signal was a
   shipped data-loss bug; the noise was two one-line compile-time edits.
2. **The ratio scales badly.** Signal is ~1 per added field (the place that should wire it); noise scales
   with call-site count (122 and rising). So the trade degrades monotonically with codebase growth.

- **Revisit trigger:** one regen breaking **more than 10** call sites for a *single* added optional field
  → D gets its own ADR, not a footnote.
- **Unresolved premise:** whether ASP.NET marks non-nullable value types `required` in the emitted schema
  — i.e. whether `removePhoto: boolean` would even become optional under D. **Never observed.**
- **The free experiment:** at the next owner regen (T-0446 is imminent), one extra run with
  `markOptionalProperties: true` into a scratch output, diffed and discarded, settles it. **Record the
  result here**, whether or not D is ever adopted.

| D question | State |
|---|---|
| Does `markOptionalProperties: true` make nullable *reference* members optional? | expected yes — unverified |
| Does it change `removePhoto: boolean` (non-nullable value type)? | **UNKNOWN — the blocking unknown** |
| Diff size across all three clients | unknown (owner-only regen) |
| Result of the scratch experiment | _(fill in after the next regen)_ |

---

## Invariants (what a reviewer enforces)

1. **A generated client is never hand-edited.** The regen is owner-only; agents flag `manual_steps`.
2. **No `package.json` script invokes NSwag without ending in `npm run typecheck`** (ADR-0031 M1). A
   publicly-named script that regenerates without the guard reopens the hole silently.
3. **The guard's unit set is derived from the build target, never hardcoded and never merely non-empty**
   (ADR-0031 M2). A declared build target whose `tsConfig` is missing is a hard failure — "2 of 3 apps
   checked, green" is the T-0438 topology.
4. **The guard is a typecheck, not a build.** The three production builds stay CI's authority and the
   prose pre-push rule stays binding verbatim. Never narrow the rule to match the guard.
5. **`ngc`, not `tsc`** — pinned by a template-diagnostic fixture so the cheap compiler cannot be swapped
   back in silently.
6. **The guard can go red.** Stubbing its body to `process.exit(0)` must fail its suite, and that suite
   runs in CI (`frontend-ci.yml:69-71`).
7. **The guard's own failures must be actionable.** It runs *inside* the owner's regen, so an
   infrastructure fault of its own (a missing/moved Angular compiler) must not read as "your regen
   failed". Resolve the `ngc` bin from `@angular/compiler-cli/package.json` rather than hardcoding
   `bundles/src/bin/ngc.js` — the path has moved across Angular majors (ADR-0031 M4).
8. **Every command a human is told about is guarded, and every guarded command is one a human is told
   about.** Two sides of one surface: no discoverable script regenerates without the typecheck (M1), and
   the preferred all-three command is documented rather than folklore (M6). Cost is *not* the argument for
   either — ADR-0031 struck speed as a justification.

9. **The guard's test fixtures live outside the repository.** `tools/typecheck-apps.test.mjs` builds its
   throwaway workspace under `os.tmpdir()` and reaches the workspace's `node_modules` via `baseUrl` +
   `paths`. This is **structural**, not configurational: since invariant 3 (M2) made every fixture carry an
   `apps/<app>/project.json`, an in-repo fixture that survived a cancelled run would be inferable by **Nx
   as a real project**. Do not "fix" a future leak with a `.gitignore` line — move the fixtures back out.
   (ADR-0031 M5 was withdrawn for exactly this reason; the ignore entry it mandated was struck as dead
   configuration that read as protective.)

10. **A green `generate-*-client` proves the tree COMPILES — not that the client regenerated correctly.**
    State the guarantee at its true width. The chain is
    `npx nswag run <config> && bash <x>-client-formatter.sh && npm run typecheck`, and it can report
    success over a bad client in **two independent ways**:
    - ~~**Verified (2026-07-30):** none of `admin-`/`customer-`/`partner-client-formatter.sh` sets
      `set -e`…~~ **CORRECTED 2026-08-04 (architect, re-verified at HEAD): this half is now FIXED and the
      text above was stale.** All three formatters carry `set -euo pipefail` at `:4` **plus** an explicit
      `[ -f "$file" ] || { …; exit 1; }` output-exists check at `:7`, each with a comment naming T-0439.
      So a failed `sed` or a missing output **does** break the `&&` chain today. *The cheap fix this
      invariant proposed has landed; do not re-file it.*
    - **Still unverified:** what `npx nswag run` returns on a partial or failed generation. Agents cannot
      run it (owner-only), so this remains an open unknown, not a claim. The next owner regen can observe
      it for free alongside the ADR-0031 Option-D experiment.
    - **Unchanged and still true:** a green regen chain proves *the tree compiles*, not *the client
      regenerated correctly*. The typecheck, the three builds **and** ADR-0042's proposed wire-enum writer
      all only see what is on disk. *(Which is why round 2 does not put the verdict there: the CI gate
      re-derives the same comparison from committed bytes, and does so on the backend commit too.)*

    What the typecheck *does* prove is exact and still worth having: whatever client file is on disk now
    compiles against every app compilation unit. What it cannot prove is that the file on disk is the
    freshly generated one, or that the renames applied. The three production builds share the blind spot —
    they also only see what is on disk. **Out of scope for T-0439; do not let the chain be described as
    stronger than this.** Cheap future fix: `set -euo pipefail` in the three formatters. Free observation:
    the next owner regen (T-0446) is already carrying the Option-D experiment — record `nswag run`'s
    exit-code behaviour on the same run.

**Two things that are NOT holes, written down so they are not re-derived as such:**
- A leaked fixture was never a *coverage* hole even when fixtures lived in-repo: the guard reads
  `src/Cleansia.App/apps`, and a fixture root sits one directory deeper. The hazard was Nx inference and
  committable junk, not guard blindness.
- The guard covering less than the three production builds is deliberate and exact for this defect class
  (invariant 4) — not an oversight to be closed by widening it.

## Known gaps (accepted, named, not closed)

| # | Gap | Bound / named fix |
|---|---|---|
| 1 | Nothing **prevents** a red `master` on non-regen-script paths | attribution speed only, until **Q-CI-01** is answered |
| 2 | Guard covers TS + templates, not bundling/budgets/SSR/styles | exact for this defect class; the three builds remain the authority |
| 3 | `strictTemplates` can be flipped off silently in an app's `tsconfig.json` | weakens the guard **and** the production build together → a `check-consistency.mjs` rule (ADR-0031 M3) |
| 4 | Android/iOS generated clients are a parallel, separately-governed drift surface | `mobile-spec-regen` + ADR-0019; deliberately out of scope |
| 4b | **The regen chain can report success over a bad client.** ~~formatters always exit 0~~ **half-CLOSED 2026-08-04:** all three formatters now carry `set -euo pipefail` + an output-exists check, so a failed `sed`/missing output breaks the chain. `nswag run`'s failure exit code **remains unverified** | invariant 10; the `nswag` half rides free on the next owner regen |
| 5 | **Code no gate can see** — (a) ~~the stale duplicate `libs/core/services/src/lib/client/admin-client.ts` + a `CLAUDE.md` map that points at it~~; (b) app-unreachable lib files (e.g. `libs/cleansia-admin-features/template-management/.../email-template-form.facade.ts`) | (a) ✅ **CLOSED 2026-08-05 — the file is deleted** (ADR-0031 residue #5a closes; ADR-0042 §D5 is a no-op). **And the `CLAUDE.md` half never existed:** `:34-35` labels `core/services/` "(hand-written)" and applies "NSwag-generated API clients" to the three `*-services` libs. No owner step is owed. (b) still a dead-export sweep, not a wider guard |
| 6 | **A wire enum a `scope:shared` lib needs has no importable home**, so a copy must exist there — **and today that copy is still HAND-WRITTEN, and neither it nor any of the four other hand-typed mirrors is ever compared to `Cleansia.Core.Domain.Enums`** | **OPEN.** ADR-0042 round 1 was returned 2026-08-05; **round 2 (2026-08-06) is `proposed` and awaiting a second panel.** See §"The second surface", the trade-off space, and gaps E1–E6. **Nothing may be built against it yet.** The detector half has improved independently: the `{workspaceRoot}` client glob on `models`' `test` target landed, so the spec now really runs and cannot cache-replay — but it still compares only generated artifacts to the shared table, over 2 of 12 enums |

## Rollout state

| Step | Where | State (2026-07-30) |
|---|---|---|
| ADR (placement decision + rejected options) | ADR-0031 | **accepted** (panel verdict, M1–M6 mandated) |
| `tools/typecheck-apps.mjs` + its suite | `src/Cleansia.App/tools/` | shipped in T-0439 (under review) |
| `generate-*-client` chained to the typecheck | `package.json:23-26` | shipped in T-0439 |
| **M1** — no unguarded `nswag:*` entry point | `package.json:20-22` | **required before T-0439 merges** |
| **M2** — discovery from the build target's `tsConfig` | `tools/typecheck-apps.mjs` | **required before T-0439 merges** |
| `frontend-ci.yml` `push: master` + guard self-test | `.github/workflows/` | shipped in T-0439; **no CI run has exercised the push trigger yet** |
| `quality-gates.md` pointer paragraph | `:297-306` | shipped (additive: 11 insertions, 0 deletions) |
| **M4** — guard resolves `bin.ngc` from the package manifest (or splits its "not installed" vs "path moved" message) | `tools/typecheck-apps.mjs:30-43` | **required before T-0439 merges** |
| ~~**M5** — `.typecheck-fixture-*` ignored~~ | ~~`src/Cleansia.App/.gitignore`~~ | **WITHDRAWN 2026-07-30** — fixtures moved to `os.tmpdir()`, which removes the condition; the ignore line is struck as dead config (ADR-0031 dated closure §B) |
| **M6** — `generate-clients` documented + the guard sentence | `CLAUDE.md:93-96` | **owner MANUAL_STEP** — proposed text lives in the T-0439 `## Review` |
| **M3** — `strictTemplates` consistency rule | `agents/tools/check-consistency.mjs` | follow-up ticket (PM) |
| ~~CH-14 — stale client + `CLAUDE.md` map~~ | ~~`libs/core/services/`~~ | ✅ **CLOSED 2026-08-05** — the client is deleted; the `CLAUDE.md` half was a false claim inherited from residue #5(a) and no owner edit is owed (see the box above) |
| **Q-CI-01** — branch protection for `master` | `questions/open.md` | **open — owner** |
| D experiment (`markOptionalProperties: true` scratch run) | next owner regen (T-0446) | recommended, not gating |

## Documenting this topic: cite stable anchors, not line numbers

Scoped rule for this page and for anything citing into `.github/workflows/**`, `package.json` scripts or
`project.json` targets: **cite the named thing — a step name, a job id, a YAML key, a script name — not a
line range.** ADR-0031 was bitten twice inside one ticket: a comment block added mid-file moved four of its
citations, and the *careful* remapping offered as the fix contained three off-by-ones in five entries. A
citation format whose correct maintenance is that error-prone is the wrong instrument for config files
that accrete comments.

The one case where a line range is still right: when you are pinning **what a reader ruled on at a
date** — an ADR's `## Challenge`/`## Defense`/`## Verdict` citations describe the artifact as reviewed and
must keep pointing there even after the code moves. Re-anchor *navigational* citations; leave *historical*
ones.

*(This is stated here in its topic-scoped form. Its general form — "ADRs citing CI/config files cite stable
anchors" — is a `agents/knowledge/conventions.md` candidate and is routed to the PM as a catalog-edit
proposal rather than smuggled into a topic doc where nobody would find it.)*

## Open questions / future evolution

- **Q-CI-01 (owner)** — require PRs for `master`? If yes, the `master` push build becomes largely
  redundant (kept as a paths-scoped net) and the whole "attribution vs prevention" asymmetry collapses in
  our favour. If no, gap #1 is permanent and the guard is the only prevention we have.
- **A fourth Angular app** needs no change here — coverage is derived. Under M2 that is provable rather
  than conventional; verify by checking the guard reports the new app the first time it runs.
- **If D is ever adopted**, this page changes shape rather than disappearing: the defect class moves from
  compile-time noise to a silent-under-wiring risk, and the compensating control (a wiring checklist? a
  runtime assertion? a diff review of new optional fields?) becomes the thing that needs a decision.
- **If a new generated client is added** (a fourth audience, a public API SDK), it inherits invariants
  1–6 or it is a deviation needing its own ADR.
- **The shared wire-enum authority (`Q-ENUM-01`) is the live thread on this page.** ADR-0042 round 1 was
  returned 2026-08-05; **round 2 was authored 2026-08-06 and is awaiting a fresh challenger and lead.** The
  owner's half is settled (no hand-typed mirror). The two structural facts any mechanism must answer —
  **no compiler anywhere checks these integers**, and **the three clients cannot disagree at one backend
  commit** — are what drove round 2 to make the C# declaration the authority. **The three places to press
  when the panel convenes:** (a) the graded failure posture — is `PENDING-REGEN`-as-ratchet the right line,
  or does it let a real gap sit; (b) §D6's scope — is pinning the two client-less mirrors a second
  decision; (c) retiring `order-status-enum-parity.spec.ts` and its input glob days after they landed.
- **When ADR-0042 is accepted, this page's §"The second surface" becomes the current shape rather than a
  proposal**, invariants 11–16 move into the enforced list, and the `Q-ENUM-01` entry moves to
  `answered.md`. Until then, only ADR-0031's half of this page is in force.
- **A process lesson worth more than this ADR** (2026-08-05, from three false citations in one document,
  two of which commissioned work): **an inherited citation is not a verified one.** Two of the three came
  from an earlier ADR's residue list and were re-asserted under a "verified against the working tree"
  header. When a draft carries that header, the claims that most need re-opening are the ones copied from
  a prior artifact — they are the ones nobody re-reads.
