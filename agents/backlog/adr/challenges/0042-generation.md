# ADR-0042 — Challenge, generation lane (§D1 placement · §D2 contents · §D3 the three copies · §D4/§7 enforcement)

**Mode:** challenger. **Gate 0:** REFUTED-by-default — every claim below cites a `file:line` I opened in
the working tree on 2026-08-05, or a shipped artifact I read end-to-end. **No ADR was amended, no NSwag
client regenerated, no ticket allocated, no git write.** Nothing outside
`agents/backlog/adr/challenges/`. §0 (the owner's ruling) is out of scope and I do not touch it.

**Headline.** The *diagnosis* is excellent and I could not break it — §1.3 (the enum is a constant table,
not a type, so no compiler checks the integers) and §1.4 (the parity spec cannot run on the commit it
exists for) are both correct, independently verified, and they are the two best sentences in the
document. The `SortDirection` finding (§1.2) is real and unguarded. What does not survive is the
**mechanism**, in four places:

1. **The generator asks the wrong question.** It compares the three clients *to each other*. All three
   are rendered from **one** C# enum declaration in an assembly all five hosts share — the ADR says so
   itself in D3.1 — so at a single backend commit they **cannot** disagree, and the check is vacuous on
   the path the owner normally runs. The drift it cannot see is *all three equally stale*, which is the
   repo's normal state between owner-only regens, and it is the class that produced §1.1's fossil.
   (CH-G1)
2. **`T1-CI at the regen` is not a tier this catalog has.** `conventions.md:231` defines `T1-CI` as
   *"fails a CI job on the offending change"*. CI never runs the generator. And the ADR-0031 analogy
   breaks exactly here: ADR-0031's defect class is a **type error**, caught unconditionally by
   `frontend-ci.yml:91-101`; ADR-0042's is a **wrong integer**, caught by nothing in CI. (CH-G2)
3. **The backstop is hosted on a cached, affected-gated Nx jest target whose declared inputs exclude the
   files it reads** (`nx.json:33-35` + `:4-16`). Two committed workflow headers in this repo name that
   exact hazard as the reason not to do this, and neither is cited. D4's own second bullet is the commit
   shape §1.4 proves the spec skips. (CH-G3)
4. **D1's `exit 1` contradicts D3's own justification.** D3 keeps three copies because the hosts are
   *"regenerated independently"* and divergence must stay **observable**; D1 makes that same divergence
   **fatal** inside `generate-partner-client`. Observable ≠ fatal. (CH-G4)

Eight findings. **CH-G1, CH-G2, CH-G3 and CH-G4 I consider blocking**; CH-G5 is blocking as an
editorial matter because two of its false facts drive proposed work.

**Citation sampling (Gate 0).** I spot-checked eighteen of this ADR's citations. **Fifteen are exact**
and several are unusually good — `partner-client.ts:10712`/`:11428`, `admin-client.ts:23500`/`:25004`,
`customer-client.ts:11233`/`:11771` all land on the enum's first line; the enum **counts** (partner 15,
customer 19, admin 27) are right when re-counted independently; the 12-name intersection in §D2 is
exactly the set `rg '^export enum'` produces; `package.json:20-27`, `nswag-partner.json:39`,
`partner-client-formatter.sh:4`, `sort-types.models.ts:16-19`, `order-status-enum-parity.spec.ts:12-16`,
the three pipe imports of §2, `patterns-frontend.md:657-667` and `questions/open.md:1206` are all
verbatim. **Three do not hold, and two of those three are load-bearing for proposed work** — the fifth
client is already deleted, `CLAUDE.md` does not say what §1.1 says it says, and the living-doc
"correction" §1.5 offers has already landed. See CH-G5.

---

## CH-G1 — The generator compares the clients *to each other*, never to `Cleansia.Core.Domain.Enums`. At one backend commit the three clients **cannot** disagree, so the check is vacuous on the normal path — and it is blind to the only drift this repo has ever had. **BLOCKING**

**The hole.** D1.3 computes the intersection and *"asserts they agree, member name by member name and
integer by integer"*, and §4 sells the result as *"a cross-host disagreement becomes unshippable"*, D3
as *"a disagreement between them is impossible to ship."* But the three clients are not three
independent statements of the contract. **They are three renderings of one C# declaration.**

- D3.1 states the premise itself: *"Five API hosts share Core + Infra + Config."*
- All twelve intersecting enums are single `enum` declarations under `Cleansia.Core.Domain` /
  `Cleansia.Core.AppServices`, each carrying `[SwaggerEnumAsInt]` — I opened all twelve:
  `OrderStatus.cs:5-28`, `PaymentStatus.cs:5-14`, `PaymentType.cs:5-10`, `ConsentType.cs:5-12`,
  `ContractStatus.cs:5-13`, `EmployeeEntityType.cs:5-10`, `EmployeeInvoiceStatus.cs:5-14`,
  `PhotoType.cs:5-10`, `PayoutScheme.cs:11-29`, `PayoutDetailsStatus.cs:10-18`,
  `Sorting/Common/SortDirection.cs:5-10`, `AppServices/Shared/DTOs/Enums/AppliedDiscountSource.cs:29-37`.
  There is exactly one integer table per enum in the whole backend.

**Therefore: `client_A ≠ client_B` ⟺ they were generated against *different backend commits*.** The
check is a **staleness-skew detector**, not an enum-correctness detector. Two consequences:

- **On `npm run generate-clients` it can never fire.** All three `_nswag:*` steps run in one command
  (`package.json:27`) against one backend, so the three renderings are identical by construction and the
  agreement assertion passes unconditionally. The ADR's flagship command carries a check that cannot
  fail.
- **The state it cannot see is the repo's normal state.** Regen is owner-only and rare
  (`CLAUDE.md` §"Manual Steps"). Between a backend enum change and the next regen, **all three clients
  are equally stale, they agree perfectly, D1 exits 0, and it writes a `wire-enums.generated.ts` that
  disagrees with `Cleansia.Core.Domain.Enums`.** Shared code — the three pipes, and every future shared
  chip/table-def §1.2 anticipates — then renders against a table no mechanism in this ADR can question.

**And it would not have caught §1.1's fossil.** The ADR's strongest evidentiary claim is a **C#
renumbering** (`New = 0` and `OnTheWay = 3` inserted) that one copy missed. D1.1 **definitionally
excludes** that copy: *"file #5 is written by no config, so it is definitionally not a client, and no
future reader has to litigate that again."* The generator would have read three agreeing clients and
exited 0 with the fossil in the tree. The ADR cites an incident as the argument for a mechanism that is
blind to it, and Alternative H (*"the incident already happened"*) inherits the same gap.

**Why the fix is cheap, and why Alternative C was rejected against a cost model this repo already
falsified.** C is dismissed as *"reflect over the assembly, or a 4th enum-only host endpoint … a new
tool, a new owner command, a backend build inside a frontend pipeline — a **fifth** pipeline to keep
alive."* Neither named option is the cheap one, and the cheap one is **shipped and running in CI
today**:

```js
// agents/tools/check-available-status-parity.mjs:134
const CANON_ENUM = join(REPO, "src/Cleansia.Core.Domain/Enums/OrderStatus.cs");
// :147-161 — a regex over the C# file
const m = /^\s*([A-Z]\w*)\s*=\s*(\d+)\s*,?\s*$/.exec(raw);
if (m) { const [, name, n] = m; byName.set(name, Number(n)); byOrdinal.set(Number(n), name); … }
// :162-165 — anti-vacuity
if (byName.size < 5) { add(CANON_ENUM, 0, "P0", `parsed only ${byName.size} enum members — the parser is stale`); return null; }
```

`byName` **is** the name→integer table D1 wants to emit. No assembly reflection, no enum-only endpoint,
no backend build, no `dotnet` at all, no new owner command — a dependency-free Node script reading a
`.cs` file, with the same anti-vacuity discipline D1.7 asks for, gated by
`.github/workflows/offerability-parity.yml` on every PR and every push to master.

C's *substantive* objection is real and I am not waving it away: `[SwaggerEnumAsInt]` means the wire
rendering is a decision separable from the C# declaration, and a domain enum the API does not expose
would give shared code a symbol no client has. **But that is a filter, not a blocker** — and D2 already
computes the filter. Split the two authorities:

- **Membership** (which enums are on the shared surface) — from the clients, exactly as D2 does today.
- **Values** (the integers, and the names bound to them) — from the C# declaration, requiring
  `[SwaggerEnumAsInt]`, with a hard failure when a client's rendering of an intersecting enum disagrees
  with the C# source.

That is one extra file read in the same tool and it converts a check that cannot fail on the normal
path into one that answers the question the owner actually asked — *"consider using backend enums on
frontend instead of generating your own."* It also subsumes the client-skew check for free: if any
client disagrees with C#, that client is stale, and it is named.

**What I want changed.** D1 gains the C# source as the value authority; §4's *"a cross-host disagreement
becomes unshippable"* is rewritten to say what the client-only comparison actually detects (*"three
clients generated against different backend commits"*); Alternative C is re-dispositioned against the
shipped technique rather than against reflection, or the ADR states why parsing
`Cleansia.Core.Domain/Enums/*.cs` — which this repo does in a blocking CI job — is unacceptable here
when it is acceptable there.

*Blocking?* **Yes.** The ADR's stated goal is *"a disagreement between them is impossible to ship."* As
designed, the disagreement that matters — shared table vs. domain — remains not just shippable but
undetectable, and the mechanism cannot fail on the command the owner runs most.

---

## CH-G2 — `**T1-CI at the regen**` is not a tier `conventions.md` defines, and the ADR-0031 analogy it rests on breaks precisely where this defect class differs from ADR-0031's. **BLOCKING**

**The hole.** §7's catalog entry declares:

> **Enforced by:** `tools/generate-wire-enums.mjs` (runs inside every regen entry point) — **T1-CI at the
> regen**, plus `wire-enums.generated.spec.ts` in `nx test models` as a committed-state backstop
> (**T2-ADVISORY on a regen-only commit** …)

`conventions.md:229-235` gives the taxonomy, and there is no "at the regen" tier:

| Token | Means |
|---|---|
| `T1-CI` | **fails a CI job on the offending change** |
| `T2-ADVISORY` | runs on demand, reports, never sets the exit code |

The generator runs on the owner's machine, inside an owner-only command, **before a commit exists**.
It appears in no workflow file. It cannot fail a CI job on any change, offending or otherwise. My own
charter states the rule this trips: *a mechanism that cannot fail a build is `T2-ADVISORY` however it is
labelled.*

**And the catalog says T1-CI is *owed* here, not optional.** `conventions.md:237-242`: *"`T1-CI` is
required when — and only when — both hold: (a) the rule is mechanically expressible on that stack, and
(b) its baseline on that stack is **zero**."* Both hold. (a) — the `--check` mode is already in the
design. (b) — I verified the baseline is zero: all 36 generated declarations agree with each other **and**
with the C# source at HEAD (see "found sound"). So the entry is not merely mislabelled; it declares a
tier the design does not build while the conditions that mandate a real one are satisfied.

**Why the ADR-0031 analogy does not carry.** D1 closes with *"it is ADR-0031 D1's argument on a second
surface — put the check where the defect is created."* ADR-0031 has **two legs**
(`0031-…md:86-88`): D1 *prevents* at the regen, and there is a CI leg behind it. For ADR-0031's defect
class that CI leg genuinely works — a regen that breaks a call site is a **type error**, and
`frontend-ci.yml:91-101` runs three **unconditional, never affected-gated** production builds that
compile the regenerated client on every PR and every push to master. ADR-0031's residue list does not
need to claim a backstop it lacks.

ADR-0042's defect class is the opposite, and §1.3 says so beautifully: *"If that integer is ever wrong,
**no compiler anywhere will say so**."* The three unconditional builds are green over a wrong integer.
So ADR-0042 imports ADR-0031 D1's **placement** while silently dropping the leg that made that placement
safe, and then labels the survivor with the tier the dropped leg used to earn. **D1 without a working D2
is not ADR-0031's shape.**

**What I want changed.** Either (a) the entry's tier becomes honest — `T2-ADVISORY` for the regen-time
generator plus whatever the backstop truly is — or, far better, (b) the ADR builds the CI leg
(CH-G3) and *then* the `T1-CI` token is earned by the thing that actually sets an exit code in a
workflow. Per `conventions.md:237-242` with a zero baseline, (b) is the one the catalog asks for.

*Blocking?* **Yes.** This sprint's whole point (ADR-0032 D2, which this ADR *"consumes"*) is that a
declared enforcer must be able to fail. An ADR that establishes the rule for others and then declares an
unearned tier for itself is the worst possible precedent for the next author to copy.

---

## CH-G3 — The backstop is hosted on a **cached**, affected-gated Nx jest target whose declared inputs **exclude the files it reads**. Two committed workflow headers in this repo name that exact hazard as the reason not to do this — and D4's own second bullet is the commit shape §1.4 proves it skips. **BLOCKING**

**The hole.** D4 puts the committed-state check in `wire-enums.generated.spec.ts`, under `nx test
models`. §1.4 correctly works out that the spec is not Nx-*affected* by a client change. It stops one
step short of the worse half: **the task is cached, and the client files are not among its inputs.**

```jsonc
// src/Cleansia.App/nx.json:4-16
"namedInputs": { "default": ["{projectRoot}/**/*", "sharedGlobals"], "sharedGlobals": [] }
// :33-35
"@nx/jest:jest": { "cache": true, "inputs": ["default", "^production", "{workspaceRoot}/jest.preset.js"] }
```

`{projectRoot}` for `models` is `libs/shared/models`. `^production` is the *dependencies'* files — and
`libs/shared/models/project.json` declares `scope:shared`/`type:util` with **no** `implicitDependencies`
and no import edge to any `*-services` lib (deliberately; that is the scope break). So
`libs/core/*-services/src/lib/client/*-client.ts` are **not declared inputs to `nx test models`**, and Nx
may replay a cached PASS computed against different client bytes.

**This is not a novel observation — it is written down twice in this repo, in committed CI, and cited by
neither the ADR nor §1.4:**

```
# .github/workflows/offerability-parity.yml:8-14
#   * frontend-ci.yml  runs `nx affected -t test`. … `nx.json` gives @nx/jest inputs
#                      `{projectRoot}/**/*` with an EMPTY sharedGlobals … so the domain
#                      rule and both mobile literals are not declared inputs and Nx would replay a
#                      CACHED PASS over a drifted literal.
```

```
# .github/workflows/nx-project-registration.yml:14-21
#   * frontend-ci.yml's TEST step is `nx affected -t test`. … the guard would be excluded from CI by
#     exactly the defect it exists to catch. Hosting the spec inside some other project does not rescue
#     it either: nx.json gives @nx/jest:jest inputs `{projectRoot}/**/*` with an EMPTY sharedGlobals …
#     and Nx replays a CACHED PASS over the hole.
#   * agents/tools/check-consistency.mjs is the counter-example, not the model: it appears in ZERO
#     workflow files (ADR-0038 CH-P6) and can therefore never set an exit code at all.
```

Note what those two headers do to Alternative F. The ADR's only "CI rule" alternative is *"a
`check-consistency.mjs` line rule"* — the one mechanism this repo has already labelled **the
counter-example, not the model**. Rejecting it is correct and costs nothing; it is not *considering* a CI
gate. **The shape the sprint actually standardized is never considered:** a dependency-free Node checker
living **outside** the Nx workspace (uncacheable by construction), its own tiny repo-root workflow with
no `continue-on-error`, and a self-test that runs **first** so the guard cannot rot into scaffolding.
That shape is written twice (`nx-project-registration.yml`, `offerability-parity.yml`) and
`check-nx-project-registration.test.mjs` carries the 40-scenario self-test the brief names.

**D4 also contradicts itself and residue 2.** D4 claims the spec catches two things D1 cannot:

- *"someone **hand-edited** `wire-enums.generated.ts`"* — touches `models` ⇒ affected ⇒ the spec runs. ✅
- *"someone regenerated a client **without** the wrapper"* — touches only `libs/core/*-services/**` ⇒
  `models` **not** affected ⇒ **the spec does not run.** ❌ That is §1.4's mechanism applied to D4's own
  bullet, and residue 2 concedes it verbatim: *"A regen-only commit still does not run `nx test models`
  … the residue is real if a client is ever regenerated outside the wrapper."*

So D4's body claims the exact case residue 2 concedes is uncovered. The tier line
(*"T2-ADVISORY on a regen-only commit"*) is the honest half of the same paragraph; the body is the
overclaiming half.

**The cheapest possible repair is one line, and the ADR walks past it.** `frontend-ci.yml` already runs
an **unconditional** node step in the same job, at `:79-81`:

```yaml
      - name: Regen-drift guard self-test
        run: npm run typecheck:test
        working-directory: ./src/Cleansia.App
```

Adding `node tools/generate-wire-enums.mjs --check` beside it makes the drift check run on **every** PR
and **every** push to master, unconditionally, un-affected-gated, un-cached (it is a `run:` step, not an
Nx task) — and it catches all three of the cases D4 wants plus the regen commit itself. §3 step 5
already proposes adding the generator's *self-test* to that very spot; it adds the test of the tool and
not the run of the tool. Optional stronger version, matching the two precedents: a repo-root
`wire-enums.yml` triggered on `src/Cleansia.App/**` **and** `src/Cleansia.Core.Domain/**` (which CH-G1
makes necessary anyway).

**What I want changed.**

1. The committed-state check moves out of `nx test models` and into an **unconditional CI step** —
   minimum: one `run:` line in `frontend-ci.yml` beside `:79-81`; better: its own repo-root workflow in
   the shape of `nx-project-registration.yml`, with the self-test running first.
2. D4's second bullet is deleted or rewritten — as written it claims coverage residue 2 denies.
3. §1.4 gains the cache half. As written it teaches the next reader that "affected" is the whole
   problem, and the next guard gets hosted in an Nx project again.
4. The alternatives table gains the shape that was actually available: *"a dependency-free Node checker
   outside the Nx workspace + its own repo-root workflow + a self-test,"* with a real disposition. If it
   is rejected, it must be rejected on something other than `check-consistency.mjs`'s weakness.

*Blocking?* **Yes.** With CH-G2 this is the difference between an ADR that closes the defect class and
one that moves it into a command nobody but the owner ever runs.

---

## CH-G4 — D1's `exit 1` makes a **single-host** regen fail for a reason that has nothing to do with that host. That is the opposite of D3's own justification for keeping three copies, and the ADR states the reverse. **BLOCKING**

**The hole.** D3.1 keeps the per-client copies because the hosts *"are **deployed and regenerated
independently**"* and because divergence must remain **observable**: *"Today it is observable — which is
the only reason §1.1's fossil is legible at all. Collapsing to one declaration does not remove the drift;
it removes the evidence of it."*

D1.4 then makes that same divergence **fatal**: *"On any disagreement: exit 1."* Wired per D1 into every
entry point:

```
generate-partner-client  =  npm run _nswag:partner && npm run gen:wire-enums && npm run typecheck
```

**Observable and fatal are not the same property, and the ADR trades one for the other without
noticing.**

**Why it matters — the concrete run.** A member is added to `OrderStatus`. (This is not hypothetical:
§1.1's own argument is that `New = 0` and `OnTheWay = 3` were inserted into this exact enum.) The owner
boots the Partner host and runs `npm run generate-partner-client`:

1. `_nswag:partner` succeeds and **writes the new `partner-client.ts` to disk**.
2. `gen:wire-enums` reads three clients, sees partner with 8 members and admin/customer with 7, and
   exits 1 naming the member.
3. `&&` short-circuits — `npm run typecheck` never runs, so ADR-0031 M1's guarantee does not execute
   either on this run.
4. The tree now holds a **fresh partner client**, a **stale `wire-enums.generated.ts`**, and a command
   that cannot be made green without booting Admin `:5001` **and** Customer `:5003` and regenerating both.

**Three of five hosts must be running to complete one host's regen.** That is exactly the coupling
`CLAUDE.md`'s per-audience-host seam exists to prevent, arriving through the build pipeline, and it is
introduced by the decision whose stated rationale is host independence.

D1's reassurance is therefore not accurate: *"The owner's command surface does not change. … Nothing new
to learn, nothing new to remember."* The *names* do not change; the *semantics* do. A previously
single-host, single-backend command acquires a cross-host precondition and a failure mode with **no
documented escape** — no `--allow-skew`, no "regenerate the others", not even a sentence telling the
owner what the message means.

**What I want changed.** The ADR states the graded response, and picks one:

- fail only in `generate-clients` (the all-three path), and in the single-host paths **report loudly and
  refuse to rewrite** the shared file — divergence stays observable without bricking the command; or
- fail only when the disagreement touches an enum a shared lib actually consumes (the intersection is
  already computed, so this is a filter, not new machinery); or
- adopt CH-G1 — with the C# source as the value authority, a single-host regen against a newer backend
  is **not a disagreement at all**: the shared file is rewritten from C#, and the two stale clients are
  reported as stale by name, which is the true statement.

Whichever is chosen, D1 must print what the owner is supposed to *do*, and §4 must price this under
"more expensive (accepted)" instead of claiming the command surface is unchanged.

*Blocking?* **Yes.** It is a live operational hazard in an owner-only command that no agent can test or
recover, and it is introduced in direct contradiction of D3's own reasoning.

---

## CH-G5 — Three citations do not hold at HEAD. Two of them drive proposed work: a delete that is already done, and an owner-gated `MANUAL_STEP` against a file that does not say what the ADR quotes.

**(a) The fifth client is already gone.** `src/Cleansia.App/libs/core/services/src/lib/client/` does not
exist; `libs/core/services/` now contains only `auth/`, `enums/`, `interceptors/`, `services/`,
`validators/` (glob of `libs/core/services/**/*.ts` — 40 files, no `client/`). `rg
'core/services/src/lib/client'` over `src/Cleansia.App` returns **zero** matches.

So the following describe a state that has ended: §1.1 table row 5, §D5 (*"**T-0547 deletes it**"*), §3
step 4's *"delete `libs/core/services/src/lib/client/admin-client.ts`"*, §V7's *"the only client-shaped
file that changes is … which is **deleted**"*, and Alternative H's *"has been sitting in the tree."*
**D5 is a no-op ticket line in a `proposed` ADR.**

The *argument* consequence matters more than the citation. The one real incident this ADR leans on was
**found by a human measuring five copies against the C# source, and closed by a `git rm`** — not by a
generator, not by anything D1 proposes, and (CH-G1) not by anything D1 could propose, since D1.1 excludes
that file by definition. §1.1 must be restated in the past tense, and Alternative H's disposition must
stop implying the mechanism under decision would have caught it.

**(b) `CLAUDE.md` does not say what §1.1 says it says.** §1.1 asserts: *"Meanwhile `CLAUDE.md`'s repo map
still advertises `core/services/` as 'NSwag-generated API clients', so an agent following the map imports
the fossil."* At HEAD:

```
CLAUDE.md:34   │   │       ├── core/{partner,admin,customer}-services/  # NSwag-generated API clients
CLAUDE.md:35   │   │       ├── core/services/               # Shared HTTP interceptors, snackbar, guards (hand-written)
```

The map labels `core/services/` **"(hand-written)"** and applies "NSwag-generated API clients" only to
the three `*-services` libs — i.e. it says the correct thing, and has for as long as this line has read
that way. The claim is inherited verbatim from ADR-0031 residue #5(a) (`0031-…md:264`) and re-asserted
here as verified at HEAD. **It is the entire basis of §D5's owner-gated `MANUAL_STEP`** (*"The `CLAUDE.md`
map correction stays an owner-gated `MANUAL_STEP` with proposed literal text"*) **and of §4 residue 5**
(*"`CLAUDE.md`'s repo map is still wrong until the owner edits it"*). A `MANUAL_STEP` that asks the owner
to correct a line that is already correct is worse than no step: it spends the owner's attention and
teaches the next agent that the map is untrustworthy. Delete the step and residue 5, or quote the text
being corrected.

**(c) The living-doc correction §1.5 offers has already landed.** §1.5's parenthetical says
`generated-client-contract.md` *"invariant 10 / gap 4b **still says** none of them do — that half is stale
as of T-0439."* At HEAD it says the opposite, dated the same day as this ADR:

```
generated-client-contract.md:310-315
  ~~Verified (2026-07-30): none of admin-/customer-/partner-client-formatter.sh sets set -e…~~
  **CORRECTED 2026-08-04 (architect, re-verified at HEAD): this half is now FIXED and the text above was stale.**
generated-client-contract.md:346
  | 4b | … **half-CLOSED 2026-08-04:** all three formatters now carry `set -euo pipefail` …
```

The underlying fact the ADR asserts is **correct** (`partner-client-formatter.sh:4` is `set -euo
pipefail`, `:7` is the output-exists check — verified). Only the "still says" is stale. Same class as
(a): work described as owed that is already done.

*One more, adjacent and not the ADR's fault but worth the lead's eye:* `questions/open.md:1231` records
the answer as living in **`adr/0041-shared-wire-enums-…`**. The file is `0042-…`; `0041` is the
partner-agreements ADR. A reader following that pointer lands on a different decision mid-panel.

*Blocking?* **Yes, editorially.** Two of the three produce proposed work that should not be done, and
this sprint has already had two ADRs whose "verified in the working tree" claims were false. An ADR that
is about to become immutable should not carry three.

---

## CH-G6 — `T-0546` is already someone else's ticket, and it is referenced from a committed workflow file. The header's justification for the number is also false at HEAD.

The header states: *"Ticket **T-0546** carries the round; **T-0547** carries the refactor … `T-0545` was
the highest on disk when this was written."*

- **`T-0546` is taken.** `agents/backlog/tickets/T-0546-four-customer-libs-cannot-run-jest.md` exists,
  and `.github/workflows/nx-project-registration.yml:6` attributes NX-6/NX-7 to T-0546 **in committed
  CI**: *"NX-6/NX-7 (T-0546) extend it one layer in."*
- **`T-0547` is genuinely reserved** — `INDEX.md:41` says *"ADR number allocated at write time; highest
  at HEAD is 0042, `T-0547` reserved."* That half is fine.
- **"`T-0545` was the highest on disk" is false at HEAD:** `agents/backlog/tickets/` also holds T-0546,
  T-0548 and T-0549.

Small, mechanical, and exactly the sort of line that gets copied into a `## Verdict` and then into two
status files. The PM owns the ids; the ADR should say "the PM allocates" and stop naming one that is
already spoken for.

*Blocking?* No.

---

## CH-G7 — Residue 3's Android/iOS sentence is wrong in both directions, and as written it would send the next reader to build the wrong thing.

**The hole.** Residue 3: *"the same 12 enums exist a third and fourth time in the mobile clients and
**nothing compares them to the web ones**. If that is ever wanted, it is its own ADR."*

**(i) "Nothing compares them" is too strong, and it hides the working model.**
`agents/tools/check-available-status-parity.mjs` already compares `OrderStatus` **ordinals** used at
eight surfaces — four web (`web.query.available`, `web.button.row-action`, `web.button.detail`,
`web.filter.vocabulary`), two Android (`OrdersListViewModel.kt`, `OrderPrimaryAction.kt`), two iOS
(`OrdersListLogic.swift`, `OrderPrimaryAction.swift`) — against the canonical
`src/Cleansia.Core.Domain/Enums/OrderStatus.cs`, with a `--baseline` ratchet, hard `P0` failures on any
stale anchor, and its own repo-root workflow triggered on all four trees
(`offerability-parity.yml:31-49`). It is not an enum-declaration check, but "nothing" is the wrong word
and it costs the reader the one shipped precedent for cross-stack enum work.

**(ii) "The same 12 enums exist a third and fourth time" is not true of the object in question.** The
mobile generators emit **nameless** members:

```swift
// src/cleansia_ios/CleansiaPartnerApi/Models/OrderStatus.swift:13-21
public enum OrderStatus: Int, Codable, CaseIterable { case _0 = 0 … case _6 = 6 }
```
```kotlin
// src/cleansia_android/partner-app/build/generated/openapi/…/model/OrderStatus.kt:28-49
enum class OrderStatus(val value: kotlin.Int) { @SerialName("0") _0(0) … @SerialName("6") _6(6); }
```

`_N = N` is an **identity map with no semantic content**. There is no name→integer table on mobile, so
the fossil's failure mode — a *name* bound to the wrong *integer* — is structurally impossible there.
The mobile drift risk lives in the hand-written mapping code (`OrderStatusPresentation.kt`,
`OrderPrimaryAction.swift`), which is precisely where the existing checker already looks.

**(iii) Two different artifacts under one sentence.** The iOS file is **committed source**
(`CleansiaPartnerApi/Models/`); the Android file is a **Gradle build output**
(`partner-app/build/generated/openapi/…`) re-derived from the committed spec on every build. Their drift
profiles are not the same and a single residue line hides that.

**What I want changed.** Rewrite residue 3 to say: mobile clients render these enums as ordinal-only
identity maps, so there is no shared *table* to compare; the cross-stack question that *is* real is
already carried by `check-available-status-parity.mjs` + `offerability-parity.yml`, and any extension
belongs there rather than in this generator. The ADR's *conclusion* (out of scope, not a widening of this
generator) is right — it is the reasoning that would misdirect the follow-up.

*Blocking?* No. But this is the residue a future reader turns into a ticket, and as written the ticket
would be "compare mobile enums to web enums", which has nothing to compare.

---

## CH-G8 — The backstop shares one parser with the generator, and D4 proposes running an `.mjs` tool from a jest spec — a module boundary the existing precedent deliberately avoided.

Two smaller shape problems that both resolve the same way as CH-G3:

- **Shared parser.** D4's `--check` mode *is* the generator, so a bug in its `export enum` parse fails
  identically in both directions and the "backstop" agrees with the thing it is backstopping. V3's
  mutation test (stub the body to `process.exit(0)`) catches a *missing* generator, not a *wrong* parse.
  The existing spec's regex is the likely ancestor (`order-status-enum-parity.spec.ts:36`:
  ``new RegExp(`export enum ${enumName} \\{([^}]*)\\}`)`` — unanchored, first-match, `[^}]*`). Under
  CH-G1's split (values from C#, membership from clients) the two sides stop being one parse and the
  backstop regains independence.
- **Jest cannot naturally host it.** The precedent for a tool self-test in this repo is plain node from
  an npm script — `package.json:19` `"typecheck:test": "node tools/typecheck-apps.test.mjs"`, run at
  `frontend-ci.yml:79-81`. `models` is a `jest-preset-angular` project; running an ESM `.mjs` from that
  spec means either transform config or a `child_process` shell-out inside a unit test. Both work; both
  are worse than the one `run:` line CH-G3 asks for, and the shell-out variant is a subprocess whose
  verdict Nx will still cache (CH-G3).

*Blocking?* No — but it disappears entirely if CH-G3 is adopted, which is the point.

---

## What I checked and found sound

Silence is not assent. This is what I attacked and could not break.

**The diagnosis (§1.2, §1.3, §1.4) — I tried to falsify all three and could not.**

- **§1.3 is correct and load-bearing.** TS numeric enums are nominal across declarations; the pipes
  compile only through the `| number` arm. Verified: `order-status-icon.pipe.ts:2` imports from
  `@cleansia/models` and `:13` takes `OrderStatus | { value?: number } | number | null | undefined`;
  `order-status-severity.pipe.ts:2`/`:27` and `payment-status-severity.pipe.ts:2`/`:13` are the same
  shape. §2's "not affected" row is right — `order-status-label.pipe.ts:1-3` and
  `payment-status-label.pipe.ts:1-3` import `TranslateService` + `toSnakeCase` and no enum. The
  conclusion — *the shared enum is used as a constant table, never as a type, so no compiler checks the
  integers* — is exactly right, and it is why Alternative G is correctly rejected (an enum member type is
  not assignable to a numeric literal type either, so `const` objects buy nothing).
- **§1.4's affected-gating is correct.** `libs/shared/models/project.json` declares `scope:shared` /
  `type:util`, `test` + `lint` targets, and **no** `implicitDependencies`; `order-status-enum-parity.spec.ts:1,35`
  reads the clients with `readFileSync`, so there is no graph edge; `frontend-ci.yml:86` is
  `npx nx affected -t test`. A client-only commit does not select `models`. Correct — and incomplete
  (CH-G3).
- **§1.2's `SortDirection` finding is real and is the best un-argued reason for this refactor.**
  `sort-types.models.ts:16-19` declares a third hand-mirror, `order-status-enum-parity.spec.ts:12-16`
  covers only `OrderStatus`/`PaymentStatus`, and all three clients declare `SortDirection`
  (`partner-client.ts:12830`, `admin-client.ts:27269`, `customer-client.ts:12932`) matching
  `Sorting/Common/SortDirection.cs:5-10`. Unguarded, and older than the guarded one.
- **§D2's "import, do not re-export" caution is not pedantry.** `sort-types.models.ts:1-14` uses
  `SortDirection` in both `ISortDefinition` and `SortDefinition`'s constructor default, so the import is
  required and the duplicate-export hazard through `models/index.ts` is real.

**The premises, re-derived independently.**

- **The counts.** `rg '^export enum'` over `libs/core`: partner **15**, customer **19**, admin **27** —
  all three match §1.2. The 12-name intersection in §D2 is exactly right, and the "some but not all"
  list is right too (`DocumentStatus`/`DocumentType`/`PayPeriodStatus` partner+admin;
  `LoyaltyTier`/`LoyaltyEarnSource`/`LoyaltyTransactionType`/`ReferralStatus` customer+admin;
  `RefundReason`/`FiscalErrorKind`/`GdprRequestStatus`/`BillingInterval`/`EmailType`/`DisputeStatus`/
  `PromoCodeType`/`UserProfile` admin-only; `CancellationFeeTier`/`DisputeReason`/`MembershipStatus`
  customer-only).
- **All 36 declarations agree — and they agree with the C# source too.** I went looking for a live
  falsification of the "the three clients agree today" premise and **did not find one**. `OrderStatus` is
  byte-identical at `partner-client.ts:10712-10720`, `admin-client.ts:23500-23508`,
  `customer-client.ts:11233-11241` and matches `OrderStatus.cs:8-28`. I also checked `PaymentStatus`,
  `PaymentType`, `PayoutDetailsStatus`, `PayoutScheme`, `ConsentType`, `ContractStatus`,
  `EmployeeInvoiceStatus`, `EmployeeEntityType`, `AppliedDiscountSource`, `SortDirection` against their
  C# declarations — all exact. **CH-G1 is about what the mechanism can see, not about a live defect.**
  It also means CH-G2's zero-baseline condition holds.
- **§1.5's pipeline description is verbatim.** `package.json:20-27` matches including the `//`
  documentation key and the `_nswag:*` convention; `nswag-partner.json:39` is the `output` key;
  `partner-client-formatter.sh:4` is `set -euo pipefail` and `:7` is the output-exists guard, both with
  the T-0439 comment.
- **§D1's discovery rule (ADR-0031 M2 applied) is the right instinct** and it is the one part of D1 I
  would defend hardest: deriving the client set from each config's `output` key rather than a glob, with
  a hard `exit 1` on a missing output, is correct, is ADR-0031's own mandated shape
  (`0031-…md:104-113`), and it is what makes the *"is this file a client?"* question mechanical rather
  than a matter of opinion. Keep it exactly as written — it survives CH-G1 unchanged (it decides
  membership; only the value authority moves).
- **D1.7's anti-vacuity exits** are the right list and match the house discipline
  (`check-available-status-parity.mjs:162-165`, `:190-193`, `:416-419`).
- **The catalog paragraph §7 replaces exists verbatim** at `patterns-frontend.md:657-667`, beginning
  *"The fix, and the general rule: a wire enum a shared lib needs is declared in `@cleansia/models`…"*
  and ending *"…is an Architect call on owner-run generation: `Q-ENUM-01`."* `Q-ENUM-01` is at
  `questions/open.md:1206-1258` and does say "four".
- **Alternative B's ground (i) is the right ground, and I could not break it.** I tried: if all three
  clients imported one symbol, a client regenerated against a host one commit behind would *claim* the
  current contract, and the skew would become structurally unobservable. That is true, it is the
  strongest sentence in D3, and D3 is right to lead with it rather than with the unverifiable NSwag
  behaviour. (It is also why CH-G4 is a *contradiction* finding and not an argument for collapsing.)
- **Alternative D's rejection is correct** on both stated grounds, and §1.1's observation that
  `GENERATED_CLIENTS` missed the one copy that had drifted is accurate against
  `order-status-enum-parity.spec.ts:12-16`.
- **"A CRC card is not required"** (§3.8) is right — a build tool is not a role, nothing new knows
  anything.

**One process note, not a finding.** `generated-client-contract.md:121-177` already carries a
*"The second surface"* section describing this ADR's design, while the ADR is `proposed` and had no
challenge on it. The living doc is the architect's to keep current and it does mark the ADR `proposed`,
so this is not a rule break — but if the panel changes D1's value authority (CH-G1) or D4's home
(CH-G3), that section and gap row 6 (`:348`) move with the Verdict, not after it.

---

## Bottom line

**The problem statement survives; the mechanism does not, and one of its four load-bearing parts is
vacuous on the path it will most often run.** Nothing here argues for Alternative D or for collapsing to
one declaration — D3's ground (i) is sound and the hand-written mirror should still go.

Ordered:

1. **CH-G1 — before `accepted`.** Make the C# enum the value authority and the clients the membership
   authority. As designed, the check compares three renderings of one declaration, cannot fail on
   `generate-clients`, is blind to all-clients-stale, and would not have caught the incident the ADR
   cites. The shipped technique for reading `Cleansia.Core.Domain/Enums/*.cs` from dependency-free Node
   is `check-available-status-parity.mjs:134-165`, and it is the answer to the owner's second sentence.
2. **CH-G2 + CH-G3 — before `accepted`, together.** Build a CI leg that can go red, then the `T1-CI`
   token in §7 is earned. Minimum: one unconditional `run:` line in `frontend-ci.yml` beside the
   "Regen-drift guard self-test" step at `:79-81`. Better: a repo-root workflow in the shape of
   `nx-project-registration.yml` / `offerability-parity.yml`. Until then §7's tier is `T2-ADVISORY` by
   `conventions.md:231`, and D4's second bullet must be deleted because residue 2 already concedes it.
3. **CH-G4 — before `accepted`.** State what happens when a single-host regen hits a skew, or the owner
   discovers it at 2 a.m. with a half-updated tree and three hosts to boot.
4. **CH-G5 — before `accepted`, editorial but two items drive work.** The fifth client is already
   deleted (D5 is a no-op); `CLAUDE.md:34-35` already says the right thing (the `MANUAL_STEP` and residue
   5 should go); `generated-client-contract.md:310-315` already carries the correction §1.5 offers.
5. **CH-G6, CH-G7, CH-G8 — same pass.** A taken ticket id, a residue that would misdirect the mobile
   follow-up, and two shape notes that dissolve once CH-G3 lands.
