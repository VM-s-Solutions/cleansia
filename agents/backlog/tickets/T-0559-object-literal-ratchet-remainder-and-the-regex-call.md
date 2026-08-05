---
id: T-0559
title: Finish the generated-DTO literal sweep — 46 left in 9 admin libs, 3 with no specs at all — and rule on the ratchet's `(Command|Request|Dto|Query)$` blind spot
status: done
size: M
owner: frontend
created: 2026-08-05
updated: 2026-08-05
depends_on: [T-0535]
blocks: []
stories: []
adrs: [0031]
layers: [frontend, architect]
security_touching: false
manual_steps: []
sprint: 15
source: reported by the frontend lane while executing **T-0535** (`6bd3b0c6`, 51 literals converted).
  This is T-0535's **remainder plus its one architect question**, filed separately because T-0535 is a
  live lane and its ticket file must not be edited underneath it
---

## Context

**Dedup, stated first because this ticket looks like a duplicate and is not.** `T-0535` ("97 object
literals over generated command types remain, and the ratchet that would stop the next one is
advisory") is `ready`, `owner: frontend`, and **in flight** — `6bd3b0c6` converted 51 literals and wired
the ratchet into the root config plus a per-lib `eslint.config.mjs` across the admin feature libs. This
ticket carries what that lane reported as **left over**, so the remainder is visible without editing a
ticket another instance is holding.

### Half 1 — the remainder (reported by the lane, to be re-counted at dispatch)

**46 literals remain in 9 admin libs.** The real cost is not the conversion: **three of those libs have
no spec files at all**, so pinning their behaviour before changing it means writing the first specs
those libs have ever had. That is why this is `M` and not `S`, and why it did not simply continue
inside T-0535.

### Half 2 — the blind spot, which is an Architect call

The ratchet's selector is:

```js
"NewExpression[callee.name=/(Command|Request|Dto|Query)$/][arguments.0.type='ObjectExpression']"
```

(`src/Cleansia.App/eslint.generated-dto.config.mjs:24`.) It matches **four suffixes only**. Several
generated types are identically hazardous — the same all-optional generated shape, the same silent
drift when a property is renamed at regen time — and are **invisible to the rule** because their names
end differently.

Widening the regex is not a free win: a broader pattern starts matching **hand-written** classes, where
an object-literal constructor argument is ordinary and correct. So the choice is between a rule that
under-matches (today) and one that produces false positives on hand-written code — **a trade-off, and
therefore an Architect call**, not an implementer's tweak.

## Acceptance criteria

- [ ] **AC1 — the count is re-run and recorded, not inherited.** Given the reported "46 in 9 admin
      libs", When this ticket starts, Then the sweep is re-run against the tree at that moment and both
      numbers are recorded here with the command used. **The figure above is the reporting lane's, on
      its tree**; three reconciliation passes this sprint each found an inherited number to be wrong.
- [ ] **AC2 — the three spec-less libs are named before any conversion.** Given the 9 libs, When AC1
      runs, Then the libs with **zero** spec files are named explicitly with their paths, because they
      are the actual cost of this ticket and the reason for its size.
- [ ] **AC3 — behaviour is pinned before it is changed.** Given a lib with no specs, When its literals
      are converted, Then a spec exists first that fails if the conversion changes behaviour. A
      conversion in an untested lib with no new test is not evidence of anything.
- [ ] **AC4 — the remaining literals are converted.** Given the re-counted set, When the work lands,
      Then the count is **zero** in those libs and the ratchet is enabled for each of them.
- [ ] **AC5 — the regex question is ruled by the Architect, with evidence.** Given
      `eslint.generated-dto.config.mjs:24`, When the architect rules, Then the ruling names (a) which
      generated types the current four suffixes miss, with file paths; (b) what a widened pattern would
      start matching in hand-written code, measured rather than assumed; and (c) the decision — widen,
      keep and state the residual, or replace name-matching with a different discriminator (e.g. import
      provenance from the generated client). **A "we should widen it someday" note fails this AC.**
- [ ] **AC6 — whatever survives states its tier** (ADR-0032). Given the rule after AC5, When it is
      recorded in `agents/knowledge/patterns-frontend.md`, Then it carries
      `**Enforced by:** … — <tier>`, and the tier is honest: `frontend-ci.yml:72-74` runs lint with
      `continue-on-error: true`, so **any ESLint rule is `T2-ADVISORY` on this stack** however it is
      worded, with a statement of what would promote it. `patterns-frontend.md:462-465` is the house
      model.
- [ ] **AC7 — the catalog edit is routed, not self-ratified.** Given AC6 touches
      `agents/knowledge/patterns-frontend.md`, When the entry is written, Then its routing follows
      whatever ADR-0033's test resolves to at that time (see **T-0549** / **T-0551**), and the ticket's
      `## Review` records the catalog search that justifies the routing claim.

## Out of scope

- **The 51 literals already converted in `6bd3b0c6`**, and anything else T-0535 is currently holding.
  **Do not edit `agents/backlog/tickets/T-0535-…md`** — it belongs to a live lane.
- The four broken customer-lib tsconfigs — **T-0546**, a different defect with a different fix.
- Widening the ratchet's *glob scope* to stacks it does not read today; this ticket is about the
  **selector**, and any scope change is stated as its own decision under AC5.

## Implementation notes

**Files this ticket touches:**
- `src/Cleansia.App/eslint.generated-dto.config.mjs` — `:24`, the selector (AC5).
- `src/Cleansia.App/eslint.config.mjs` — the glob list that scopes the rule.
- `src/Cleansia.App/libs/cleansia-admin-features/*/eslint.config.mjs` — per-lib enablement for the
  remaining libs (AC4).
- `src/Cleansia.App/libs/cleansia-admin-features/*/src/**` — the conversions and the new specs
  (exact libs named by AC1/AC2).
- `agents/knowledge/patterns-frontend.md` — the entry + its enforcer/tier (AC6/AC7).

**Sequencing.** AC5 is an architect ruling and can run **in parallel** with AC1–AC4; the conversion does
not wait on it. Do not let the reverse happen — the sweep stalling behind a regex decision is how 97
literals became a standing number in the first place.

### Staleness detectability (sprint-15 §D3)

Names **product paths under `src/`** — `eslint.generated-dto.config.mjs`, `eslint.config.mjs` and the
admin lib trees — so the candidate-3 path rule covers this ticket, which matters because the same lane
is actively committing in those directories. `agents/knowledge/**` is excluded from that rule, so the
AC6/AC7 half is invisible to it and must be re-checked by hand.

## Status log
- 2026-08-05 — created **`draft`** by pm. Not `ready`: **AC5 is an architect ruling that does not exist
  yet** (Definition of Ready item 7 — the canonical form is not identified until the regex question is
  answered), and AC1/AC2 must re-derive the count and the spec-less lib list before anyone is dispatched
  into nine libraries. Filed as T-0535's remainder rather than as an edit to T-0535, whose file is held
  by a live lane.
- 2026-08-05 — **AC1–AC4, AC6, AC7 done** by frontend. All 9 libs converted, all 9 scopes opted in, count
  is **zero** across `libs/` and `apps/`. **AC5 remains open — it is the Architect's, and this lane did
  not touch the selector**; the measured evidence it asked for is in `## Review` below and in
  `patterns-frontend.md`.

## Review

### AC1 — the count, re-run rather than inherited

Re-derived on this tree with an AST scan running the rule's **exact** selector through the ESLint API
(a `grep` was run as a cross-check and under-counts by 11, because it misses multi-line
`new X(\n  {…})` forms — do not use grep for this):

```js
// ESLint 9 API, overrideConfig, @typescript-eslint/parser, filtered to ruleId === 'no-restricted-syntax'
"NewExpression[callee.name=/(Command|Request|Dto|Query)$/][arguments.0.type='ObjectExpression']"
```

**46 literals in 9 admin libs** — the reported figure reconciles exactly:
`employee-management` 10 · `invoice-management` 8 · `country-management` 5 · `order-management` 5 ·
`template-management` 5 · `pay-periods` 4 · `company-management` 3 · `disputes-management` 3 ·
`loyalty-tier-configs` 3. After the work: **0** in `libs/` and **0** in `apps/`.

### AC2 — the spec-less libs: the inherited figure was wrong

The ticket said **three** spec-less libs, 13 literals. The tree says **four**, 16 literals —
`loyalty-tier-configs` was miscounted as having specs and has none:

| Lib | Spec files before | Literals |
|---|---|---|
| `libs/cleansia-admin-features/template-management` | 0 | 5 |
| `libs/cleansia-admin-features/country-management` | 0 | 5 |
| `libs/cleansia-admin-features/loyalty-tier-configs` | **0** (not named in the ticket) | 3 |
| `libs/cleansia-admin-features/company-management` | 0 | 3 |

Two of those four could not have run a spec even if one had been written: **`company-management` and
`loyalty-tier-configs` had no `jest.config.ts`, no `tsconfig.json`/`.lib.json`/`.spec.json`, no
`src/test-setup.ts` and no `test` target** — the T-0546 family of defect, in admin rather than customer.
`loyalty-tier-configs` had `"targets": {}` outright; its `lint` target only existed because
`@nx/eslint/plugin` infers one. Both were scaffolded from `country-management`'s working configs and now
run in the suite.

### AC3/AC4 — pinned before changed, and mutation-proven

Sequence per lib: write/upgrade the spec → run it **against the literal** (green = the body is pinned,
and the run is what tells you the real body — the `updateCountry` case sends **no** `isoCode`, which a
guessed assertion would have got wrong) → convert → re-run → **drop one field assignment and confirm
RED** → restore byte-exact → confirm GREEN.

Per-field assertions were upgraded to whole-body `toJSON()` equality wherever they existed
(`disputes-management`, `pay-periods`, `order-management`, `invoice-management`, `employee-management`),
because a per-field check reads as coverage and passes when a *different* field is dropped. Two prior
assertions were of the shape `expect(Object.keys(command.toJSON())).toEqual([...])`, which pins the key
set but **not one value** — also replaced.

| Lib | Field dropped | Result | Restore |
|---|---|---|---|
| `country-management` | `command.countryId = countryId;` | **RED** 2/18 | byte-exact |
| `template-management` | `command.languageId = languageId;` | **RED** 1/17 | byte-exact |
| `company-management` | `command.companyInfoId = companyInfoId;` | **RED** 3/17 | byte-exact |
| `loyalty-tier-configs` | `command.discountPercent = t.discountPercent;` | **RED** 1/12 | byte-exact |
| `disputes-management` | `command.isStaffMessage = true;` | **RED** 1/27 | byte-exact |
| `pay-periods` | `command.notes = notes;` | **RED** 1/33 | byte-exact |
| `order-management` | `command.reason = reason;` | **RED** 4/43 | byte-exact |
| `invoice-management` | `command.bankTransferNote = bankTransferNote;` | **RED** 1/48 | byte-exact |
| `employee-management` | `command.maximumPay = data['maximumPay'] ?? 0;` | **RED** 1/56 | byte-exact |

All nine scopes joined the ratchet by spreading `generatedDtoLiteralRules()` into their own
`eslint.config.mjs`. **The root `src/Cleansia.App/eslint.config.mjs` was not edited** — every one of the
nine owns a local config, so no workspace-relative glob was needed and the shared file stayed out of the
diff entirely.

`company-management` built the same 16-field body in three places across two files, each with its own
copy of an identical `CompanyInfoFormData`. Per ADR-0031's several-call-sites guidance that became one
`company-info.models.ts` with `buildCreateCompanyInfoCommand` / `buildUpdateCompanyInfoCommand` and one
owner for the interface (both facades re-export the type, so no importer changed), pinned by a
TestBed-free `company-info.models.spec.ts`.

### AC5 — NOT done, and deliberately not attempted

The selector is unchanged. Evidence gathered for the Architect, measured rather than assumed:

- **`SortDefinition` is the largest invisible surface — 17 object-literal call sites.** 16 construct the
  **generated** class (11 admin, 5 partner). The 17th
  (`libs/shared/models/src/lib/models/sort.models.ts:244`) constructs a **hand-written** `SortDefinition`
  declared at `libs/shared/models/src/lib/models/sort-types.models.ts:6`. **The same identifier is both a
  generated DTO and a hand-written class in this workspace**, so no name-only discriminator can be both
  complete and false-positive-free — which is a direct argument for AC5 option (c), import provenance.
- **`OrderFilter`** (`libs/shared/models/src/lib/models/filter.models.ts:196`) — 4 literal call sites,
  **entirely hand-written**. A `Filter$` widening is 100 % false positives there.
- **`IssuePartialRefundRefundLineSelection`** — generated, one nested literal inside
  `admin-order-refund.facade.ts`. Converted anyway (it was in a file this ticket was editing) and now
  pinned by whole-body equality including the serialized `lines` array.
- **`SendSitewidePromoResponse`** — generated, one literal, in a `.spec.ts`. A `Response$` widening
  would catch it.

### AC6/AC7 — catalog edit routing: **inline**, by the ADR-0033 test

Two edits to `agents/knowledge/patterns-frontend.md` §"Building a generated DTO": the opt-in progress
list (now "every scope, count zero" instead of "17 of 26"), and four sentences of the AC5 evidence above
appended to the existing *"The selector's suffix set is narrower than the hazard"* paragraph, explicitly
labelled **evidence, not a rule**.

- **Test 1 — does it put existing code in violation?** No. **Sweep run:** the AST scan above over the
  whole of `libs/` and of `apps/` returns **0**, and `nx run-many -t lint --all` reports **0**
  `no-restricted-syntax` violations. Zero baseline by construction.
- **Test 2 — does it narrow latitude?** No. **Search run:** `patterns-frontend.md` for `opt-in`,
  `Cleared so far`, `unit of progress` → the governing sentences are `:466` (*"a scope may only be added
  once its own count is zero"*) and `:475` (*"the unit of progress is a lint scope"*); `consistency.md`
  for `construct-then-assign` / `object literal` / `ADR-0031` → `:153` (D1a, which delegates to this
  section); `conventions.md` for the same four terms → **no hits**. Both edits *obey* those sentences and
  record state under them; neither carves an exception, replaces them, or forbids a form they named. The
  blind-spot addition sits inside a paragraph that already governs the subject and already routes the
  widening to the Architect — it adds file:line facts and changes no obligation.
- **Test 3 — prescriptive about an unrun stack?** No. This ticket built and ran this stack: 64 Jest
  projects, `nx run-many -t lint --all`, and all three production builds.
- → **Test 4, inline.**

**Enforced by:** unchanged — `no-restricted-syntax` in `src/Cleansia.App/eslint.generated-dto.config.mjs`
— **T2-ADVISORY**, because `frontend-ci.yml:72-74` runs lint with `continue-on-error: true`; promotes to
`T1-CI` with the rest of the lint baseline. The edits do not alter the enforcer or the tier.

### Verification

- **Tests:** `npx nx run-many -t test --all` → **64 projects, all green**. The ticket brief's baseline of
  61 is stale by one: HEAD carries **62** projects with a `test` target (`git grep -l '"test"' HEAD --
  'src/Cleansia.App/**/project.json' | wc -l` → 62). 62 + the 2 targets this ticket created
  (`company-management`, `loyalty-tier-configs`) = 64.
- **Lint:** 24 failing projects before, 24 after, and the two sorted sets **diff clean — byte-identical**.
  **Zero** `no-restricted-syntax` violations in the whole run. Per-lib before/after comparisons show the
  same rules at the same counts throughout; only line numbers moved, in the files whose line counts the
  conversion changed.
- **Builds:** `build:cleansia-admin` (fresh, 40.5 s), `build:cleansia-partner`, `build:cleansia-customer`
  — all succeed. Partner and customer were Nx cache hits, which is itself the expected result: nothing
  outside the admin libs changed.
- **Not run:** `npm run generate-*-client` (owner-only). No generated client was read for anything but
  its type shapes, and none was edited.
