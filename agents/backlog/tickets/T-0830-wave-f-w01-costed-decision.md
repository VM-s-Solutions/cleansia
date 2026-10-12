---
id: T-0830
title: Wave F W01: put the evidence and a measured, costed decision in front of Mike
size: S
owner: pm
created: 2026-10-11
updated: 2026-10-12
depends_on: [T-0812]
blocks: []
stories: []
adrs: []
layers: [frontend, docs]
security_touching: false
---

## Context

The roadmap's W01, endpoint-scoped client loading. The ground truth refutes the audit's mechanism on all three web apps: esbuild keeps each generated client file whole in one chunk, and eager code imports from it, so the whole file is initial (customer 254 KB, partner 225 KB, admin 546 KB raw). Endpoint-scoped injection would move about 0 B. Per the roadmap, the levers that would move bytes (splitting the generated client per declaration, or letting the build drop unreferenced injectables) need a costed decision [W-customer, W-partner, W-admin].

## Doing

- Write up the evidence for each app.
- Measure the structural options in scratch only, with no tree change: a split-client counterfactual build for each app, and the `isolatedModules: false` build. Comparable before/after initial-JS bytes, with source, toolchain and counterresults recorded.
- A costed decision for Mike, with the measured numbers.

## NOT doing

- No application code change.
- No change to the generated-client layout, routing, SSR or a CI gate before Mike decides.

## Acceptance criteria (done looks like)

- [x] **AC1** — The report and measurements are recorded with receipts.
- [x] **AC2** — Mike has a decision line with costs and measured gains.

## Work record

- 2026-10-11 — Filed from the post-Wave E ground truth and Mike's rulings.
- 2026-10-12 — Measured, in scratch only. Three frontend lanes, one per app, ran twelve production builds of `git archive c39928c` (tar sha256 `a3286307…1b21`).
  - The build was `nx build <app> --configuration=production --stats-json --skip-nx-cache`, with no source maps, which is the shipping path.
  - Toolchain: Node 22.23.3, Nx 23.2.1, `@angular/build` 20.3.39 with its own esbuild 0.28.1, and TypeScript 5.9.3.
  - A network outage cut the first attempt short. The second attempt reused its receipts only after re-deriving their numbers from the build outputs.
  - The Architect wrote the costed decision, and the Optimizer then tried to refute it (Review, below).
  - Nothing in the tree changed: no worktree edit, NSwag regeneration, CI, DEV or PRO.
  - Writes outside scratch, disclosed:
    - two temp files, since deleted;
    - npm debug logs in `~/.npm/_logs`, from the first attempt's partner and admin `npm ci` and from admin's `npm ls`. Each run also pruned one older log there, and both installs read the shared `~/.npm/_cacache`.
- 2026-10-12 — Shipped in [#324](https://github.com/VM-s-Solutions/cleansia/pull/324). Exact-head CI on `52d215b` passed all 5 check runs with no re-run: Frontend build and e2e-smoke, Android, Docs and Secret scan. Backend, iOS, iOS Symbols and booking-policy parity did not run, because their path filters select nothing in a backlog-only change. Merged normally with the SHA guard as `7fc7c39`. No deploy ran: deploy-dev, deploy-pro, deploy-docs and execute-sql have no run since the merge, and deploy-azure runs only through the first two. Evidence: `raw/ci/p3-pr324-ci-merge-closure-002.json` (sha256 `a1f52226…`) in the off-tree Wave F folder, which supersedes `-001`.
  - The decision itself is Mike's: one letter, A to E (+F), under *Recommendation and the decision* below. T-0830 stays in progress until he gives it, and nothing in the tree changes for W01 before then.

### Why W01 as written moves about 0 B

Two build facts keep each app's generated client in its initial bundle:
1. **esbuild places code per file.** Each source file goes whole into one chunk:
   - customer: 0 of 608 inputs are split across chunks;
   - partner: 0 of 769;
   - admin: 0 of 810.

   Each generated client is one file, and eager code imports from it: the base-URL token, the auth DTOs and the aggregate client. So the whole file is initial, however consumers inject.
2. **Injectables nothing references are still kept.** With `isolatedModules: true` and no source maps, `@angular/build` skips the pass that wraps `static ɵprov`/`ɵfac` for workspace TypeScript (`compiler-plugin.js:241-244, 354-359`). esbuild then keeps every `@Injectable` class in a reachable file.

W01 (endpoint-scoped injection plus route-scoped effects) changes neither fact. It was not built as its own arm. The "about 0 B" rests on three things:
- the controls:
  - customer's `DeviceClient` is built nowhere, yet it ships;
  - partner's per-endpoint `GdprClient` is used only on a lazy route, yet it is initial;
  - admin's 12 clients injected per endpoint are still initial;
- the ground truth's esbuild probes;
- the split builds below, where bytes moved only once both the file layout and the imports changed. A split that kept the barrel moved nothing: partner +2,591 B gzip-9, admin +2,804 B.

Moving the effects to their routes would also move about 1 KB gzip of hand-written effects (partner 4,183 B raw, customer 3,107 B, admin 308 B).

### Measured options

- **Initial JS:** the `index.html` entry scripts plus their static-import closure.
- **gzip-9:** Apple `gzip -9 -n` for customer and admin, and Node zlib level 9 for partner. Compare within an app, never across apps.
- **Generated:** esbuild `bytesInOutput` of the generated client.

**Customer (SSR)**

Baseline: 1,318,400 B raw / 314,045 B gzip-9. The generated client is 253,990 B, all initial.

| Option | Initial raw, B (Δ) | Initial gzip-9, B (Δ) | Lazy gzip-9 Δ | Generated: initial (lazy) | Tests |
|---|---|---|---|---|---|
| A. Do nothing | 1,318,400 | 314,045 | — | 253,990 (0) | not run (no import changed) |
| B. `isolatedModules: false` | 1,316,010 (−2,390) | 314,328 (**+283**) | −12,925 | 251,559 (0) | not run (no import changed) |
| C. Split client | 1,290,268 (−28,132) | 311,655 (−2,390) | +5,455 | 226,200 (23,773) | app: 277/279, failing only the baseline's 2 sandbox-loopback tests. 19 projects (17 rewritten libs, confirm-email and models): 1,692/1,694, the 2 new failures in a parity spec that reads the client file as text |
| D. Split + flag | 1,290,993 (−27,407) | 311,769 (−2,276) | −6,807 | 226,945 (25,322) | app 277/279 (0 new) |

- All browser JS, gzip-9: B −12,642, C **+3,065**, D −9,083.
- The SSR server bundle builds and evaluates offline in every arm.
- Under C, 221,802 of the 226,200 B still initial are reachable only through `CustomerClient`.

**Partner (client-only)**

Baseline: 1,709,834 B raw / 415,578 B gzip-9. The generated client is 225,457 B, all initial.

| Option | Initial raw, B (Δ) | Initial gzip-9, B (Δ) | Lazy gzip-9 Δ | Generated: initial (lazy) | Tests |
|---|---|---|---|---|---|
| A. Do nothing | 1,709,834 | 415,578 | — | 225,457 (0) | not run |
| B. `isolatedModules: false` | 1,638,235 (−71,599) | 399,794 (−15,784) | +595 | 224,692 (0) | (arm D: 814/814) |
| C. Split client | 1,676,834 (−33,000) | 414,216 (−1,362) | +5,258 | 192,571 (30,236) | 12 projects: 814/814; the models parity spec fails 2/7 |
| D. Split + flag | 1,606,496 (−103,338) | 398,157 (−17,421) | +6,578 | 193,196 (32,038) | 814/814; same parity 2/7 |

- All JS, gzip-9: B −15,189, C **+3,896**, D −10,843.
- B's gain is barely the generated client, only 765 B raw of it. The rest is exports of the `@cleansia/components` and `@cleansia/services` barrels that partner never references:
  - PrimeNG MultiSelect: 53,079 B;
  - its style: 7,731 B;
  - `CleansiaFileComponent`: 7,738 B;
  - `CleansiaMultiselectComponent`: 3,381 B.

  That is offset by +2,449 B of additions, 1,125 B of them enums no longer inlined, plus 110 B of chunk overhead.
- Under C, `base-client.ts` holds most of what stays initial: 188,358 B by shortest-chain attribution, or 187,723 B by reachability with it excluded. The latter is the method behind customer's 221,802.

**Admin (client-only)**

Shipping baseline: 1,982,286 B raw / 424,132 B gzip-9. The generated client is 540,549 B, all initial; the "546 KB" in this ticket came from a source-map build.

| Option | Initial raw, B (Δ) | Initial gzip-9, B (Δ) | Lazy gzip-9 Δ | Generated: initial (lazy) | Tests |
|---|---|---|---|---|---|
| A. Do nothing | 1,982,286 | 424,132 | — | 540,549 (0) | not run |
| B. `isolatedModules: false` | 1,978,313 (−3,973) | 423,067 (−1,065) | +2,025 | 546,098 (0) | not run |
| C. Split client | 1,842,179 (−140,107) | 415,377 (−8,755) | +28,212 | 400,422 (141,443) | 37 projects: 189/192 suites, 1,973/1,979 tests; the 3 failing suites are source-reading guards that pass on baseline |
| D. Split + flag | 1,833,160 (−149,126) | 412,777 (−11,355) | +32,203 | 401,190 (146,399) | same |

- All JS, gzip-9: B +960, C **+19,457**, D **+20,848**.
- Under C and D, initial chunks go from 7 to 13. Angular adds at most 10 modulepreload links, so `chunk-MWHR4T4R.js` (188,847 B: `@angular/core`, rxjs, `@angular/animations` and tslib) is imported but no longer preloaded. That is a likely extra round trip; it was not measured.
- Endpoint-scoped injection alone moves at most the `AdminClient` class itself, about 2,186 B raw in the shipping build.

**Budgets and targets.**
- Every app's `project.json` has an initial-bundle warning budget: 2.5 MB for customer, 2 MB for partner and 2 MB for admin.
- Admin's baseline is 192.94 kB over its budget, and no arm clears it: C is 52.83 kB over, D 43.81 kB.
- The audit proposed three ceilings, 300, 450 and 425 KiB, and none was adopted. Admin's baseline (414.2 KiB) already meets its 425. Partner's (405.8 KiB) meets its 450. No customer arm reaches 300; the best is C at 304.4 KiB.

**Not measured:**
- latency, or any browser run;
- CI build time;
- SSR rendering with data;
- the module-boundaries gate on the split trees.

The bundle-generation timings in the logs show no direction. The flag build was faster in 5 of 7 paired builds, and identical inputs varied by about 2 s.

### Cost for real

One engineer, including review and verification:

| Option | Customer | Partner | Admin |
|---|---|---|---|
| A. Do nothing | 0 d | 0 d | 0 d |
| B. Flag | 1.5–2 d | 0.5–1 d | about 0.5 d |
| C. Split | 4–5 d | 3–4 d | 4–5 d (7–9 d for all three; the splitter is shared) |
| D. Both | 5.5–7 d | 3.5–5 d | about 4.25–5.25 d |
| E. Full restructure (gains not measured) | 9–13 d | 8–12 d | 5–10 d |

**B. The flag**
- It changes the whole app's emit path. TypeScript emits and Babel wraps static members, instead of esbuild transpiling.
- The `development` configuration and every `--sourceMap` build already take that path, so the runtime risk is low.
- Enums stop being inlined.
- For customer, initial gzip gets worse.
- Set it in `apps/<app>/tsconfig.app.json`. Setting it in `tsconfig.json` would also type-check the app's specs under ts-jest. One admin run measured 63.8 s against 20.7 s, but at different machine loads, so it is not comparable.
- It barely touches the generated client: it drops only generated clients nothing references (customer `DeviceClient`, −2,431 B; partner `V1Client` and `ConsentsClient`, −765 B). Admin's generated client grows by 5,549 B.
- Wave E's T-0809 listed "No isolatedModules or type-check change" as out of scope. That was a scope line, not a ruling, so lifting it is Mike's call.

**C. The split** has four parts:
- a splitter step after each `<app>-client-formatter.sh` in the ADR-0031 regeneration chain, with its own tests;
- a one-time codemod per app: customer 142, partner 100 and admin 296 importer files, plus each lib's barrel;
- guard and tooling fixes: three source-reading specs, the Nx test-input globs, the ESLint ignore glob and docs;
- review of a first PR of about 575, 477 or 1,178 files.

Risks:
- regeneration gets more fragile;
- the three services libs change their public API;
- imports can drift back to the barrel, so a lint rule is needed;
- total JS grows in every app;
- admin's core chunk falls outside the preload cap.

For maintainers, each regeneration then writes about 430, 375 or 880 files, and the catalog import rule (`patterns-frontend.md:24-25`) narrows.

**E. Full restructure.** Split, then trim each aggregate to its eager clients, inject per endpoint in lazy features, and move effects behind their routes. It is the only path to a large cut, but every gain is derived from the metafile graph, not built:
- customer −90,735 or −189,325 B raw;
- partner −33.5 or −135.6 KB raw;
- admin's eager floor 17,881–55,931 B raw.

Each app needs a browser before/after first: Wave B's W08 made admin notifications 7.4% slower at the median while bytes fell.

### Recommendation and the decision

**Recommendation: A. Close W01 on this evidence and change nothing.**
- W01 as written moves about 0 B in all three apps.
- C on its own buys 0.33–2.06% of initial gzip-9 (1,362–8,755 B), for 3–5 days per app plus a permanent regeneration step. It also grows total JS everywhere.
- B's measured gain is a different defect: shared barrels ship code an app never references.
- No adopted target fails today.
- If you want bytes now anyway, B on partner is the cheapest measured win: 0.5–1 day for −15,784 B gzip-9 initial. Better still, cost the barrel leak at its cause (+F).

> **Decision for Mike. Reply with one letter:**
> - **A:** close W01, no change.
> - **B:** `isolatedModules: false` on partner, or on all three.
> - **C:** split the generated client on admin, or on all three.
> - **D:** B + C, on the apps you name.
> - **E:** full restructure, with a scratch prototype and browser cohorts first.
>
> You can add **+F**: one S, scratch-only ticket that costs the shared-barrel leak (narrow the barrels plus a guard, against the flag).

What each answer triggers:
- **A:** close T-0830, mark W01 closed as refuted, and add a dated note to `agents/architecture/decisions/generated-client-contract.md`.
- **B:** an ADR (build semantics, and T-0809's scope line), then one frontend ticket per app: the flag, a revert guard, a CI build-time measurement, and receipts. Customer also needs SSR, hydration and e2e checks.
- **C:** an ADR changing the ADR-0031 chain and the catalog import rule with its enforcer, then the splitter, the codemods, the guards and a regeneration. Admin also needs a browser check.
- **D:** B's and C's triggers together.
- **E:** a scratch-only prototype ticket with browser cohorts.

### Receipts (AC1)

Under `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/w01-measure.noindex/`:
- customer, in `customer/receipts/`:
  - `baseline.json`: `9223e05e…`
  - `isolatedModules-false.json`: `43ade2df…`
  - `split-client.json`: `80d12f67…`
  - `split-client+isolatedModules-false.json`: `0e88e9ed…`
- partner: `partner/r2/receipts/summary.json` (`0f0c491b…`), and `partner/r2/MANIFEST.sha256` (`64e90948…`, 191 entries; verify it from `partner/`, because its paths are relative to that folder);
- admin: `admin/receipts/summary-w01-admin.json` (`0881764a…`).

Two tool defects were found and corrected in the receipts:
- customer's first `measure.cjs` read 0 B of generated code in the server bundle (it is 253,990 B);
- the ground truth's `class_attribution.mjs` misses IIFE-wrapped classes.

### Reported, not absorbed

- The shared-barrel leak, which is what +F would cost.
- App spec suites run transpile-only under ts-jest, because each `tsconfig.spec.json` extends an `isolatedModules: true` `tsconfig.json`. Library suites are type-checked.
- `cleansia-status-badge.component.spec.ts` passes after the partner split only because it reads `admin-client.ts` first. An admin split breaks it (4 tests).
- `npm ls --all` flags the committed lockfile: an extraneous `@tybys/wasm-util@0.10.3` (optional), and an invalid peer range for `webpack-dev-middleware@7.4.6`.

## Review

- **Optimizer refutation (read-only): no byte figure refuted.** It recomputed every arm of all three apps from the build outputs on disk: its own initial-set closure, `gzip -9 -n` per file, and Node 22 zlib. That reproduced every initial, lazy, generated-client, landing, first-visit and server figure, and it confirmed each arm changed only what it claims.
- **Its eleven discrepancies are all applied above:**
  - the out-of-scratch npm logs;
  - the timing claim, which the paired builds do not support;
  - the budgets and the audit's third ceiling;
  - the customer test label;
  - the shipping-build AdminClient size;
  - partner's attribution method;
  - B's offsets and its effect on the generated client;
  - four count slips;
  - where the partner manifest verifies from;
  - the ts-jest comparison's load;
  - the flag's placement per arm (partner and admin set it in `tsconfig.json` and customer in `tsconfig.app.json`; the build options are identical either way).
- **AC1** is met by the receipts above. **AC2** is met by the decision line above, delivered in this ticket and its PR. Mike's letter will be recorded when he answers.
