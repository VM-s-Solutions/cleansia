# Board query — focused B010 optimisation

Mike approved the recommended 1–2 engineering day board-only scope on 2026-10-08
after Wave A PR #311 merged. [T-0804](backlog/tickets/T-0804-board-seat-predicate.md)
defines Doing, NOT doing and acceptance criteria. Original audit and Wave A timings
remain historical evidence, not this wave's fresh baseline.

Availability-true board server p95 falls from **637.367 to 140.797 ms (77.9%)**
in the comparable local corpus, clearing the 300 ms target. All 5000 scored
responses pass retained-byte/semantic checks; each arm has 2500 scored successes,
50 excluded warmups and 2550 unique same-host completion joins.
Full local verification passes: 8469 unit, 845 integration and 434 host tests
(9748 total), with no failures, skips or errors. Existing applicable PR CI and
Mike's merge decision remain the final gates.

Base: `6fa0492badf9ab5c9f30fee3beb680f7fcc1f265`.
Branch: `fix/board-seat-predicate`.
Scratch: `/Users/michael/.codex/scratchpads/cleansia-board-query-2026-10-08`.

## Change and correctness

The approved change removes the repeated inner takeable-seat aggregate only when
`HasAvailableSpots` is true and the outer predicate already requires it. Let `P`
mean non-cover-requested assignments are fewer than capacity, `A` mean assigned to
the caller, and `O` mean canonically offerable. The existing expression
`P AND (A OR (P AND O))` becomes `P AND (A OR O)`. The outer `P` remains. False/null
retain their original `A OR (P AND O)`, including the seat-first expression order.
The existing empty restriction check, offerability, cover seats, holds, cash,
currency, tenant boundary, redaction, ordering and financial calculations remain.

One existing relational test file gains 26 cases: six availability/restriction SQL
shapes, six exact count/ID visibility matrices and fourteen independent request
filter cases. The five existing current-status cases remain. Before the production
change, the final characterization run has 30 passes and one intended failure:
the restricted availability-true count and ordered-ID queries each contain two
seat aggregates rather than the asserted one. Afterward, all 31 pass. The executed
GREEN production bytes differ from the final candidate only by a terminal newline;
both versions and that qualification are retained. The exact final candidate is
also compiled by the successful Release solution build used for AFTER requests.

Independent source/security review accepts the algebra and the meaningful
regressions; the diff widens no S1–S12 boundary. Complete PostgreSQL, paired HTTP,
full-suite and CI evidence are separate gates below.

## Comparable local measurement

Both arms use one owned, disposable loopback PostgreSQL 16.15 fixture: 100000
orders, 20000 customers, 500 cleaners and 2000 recurring templates, with tenant
counts 80000/15000/5000 and status counts 75000 completed, 10000 cancelled, 10000
new and 5000 confirmed. Its newly seeded date anchor is 2026-10-12T12:00Z; the
earliest live cleaning is 2026-10-13T08:00Z. No live hold expires during the run.
This bulk corpus does not establish every edge's prevalence; the relational
regressions cover holds, recurring cash, cover seats and invalid legacy capacity.

A frozen physical database template, created after excluded initial setup, is
cloned separately into the same request database before each arm. This preserves
the same rows, relation OIDs, data/index pages and optimizer statistics. Recorded
non-clock qualification fields are identical between the two physical clones;
the before arm also retains them after scored requests. PostgreSQL planner,
autovacuum/analyze and default JIT settings are retained. A custom dump is retained
as evidence but is not used as proof that optimizer statistics match.

Each arm starts fresh Release hosts, authenticates the same 50 synthetic sessions,
executes the same excluded 50-request smoke prelude and excludes ten warmups per
cohort. The five cohorts run in fixed order, each with five runs of 100 physical
requests rotating ten identities, serial HTTP/1.1 keep-alive and an eight-starts/s
ceiling. Provider integrations are dormant. Hosting completion timing is enabled;
EF logging is disabled during scored windows. Pacing, checking and artifact I/O
are excluded from HTTP latency. No compiler, test suite, DB diagnostic or device
work overlaps a scored window.

This is a warm local before/after comparison, with no claim of identical OS/PG
buffer residency or Npgsql prepared-cache state, production incidence, concurrency
capacity or physical-device absolute performance. Original audit and Wave A
numbers remain historical and are not substituted for the fresh baseline.

## Before results

Every cohort has 500 successful scored requests, 500 unique same-host Hosting
completion joins, stable retained response bodies and matching DTO semantics.
The five cohorts total 2500 scored successes plus 50 excluded warmups. Zero
captured EF events here describes the logging profile, not zero database commands.

| Literal cohort | Server median ms | Server p90 ms | Server p95 ms | HTTP median ms | HTTP p95 ms |
|---|---:|---:|---:|---:|---:|
| Admin orders, page 1 / 20 | 42.517 | 47.053 | 47.973 | 43.537 | 49.078 |
| Partner preview | 93.927 | 95.886 | 96.919 | 94.627 | 97.626 |
| Partner board, availability=true | 619.508 | 629.561 | 637.367 | 620.285 | 637.938 |
| Partner board, availability=false | 994.418 | 1002.989 | 1013.405 | 995.229 | 1014.022 |
| Partner board, availability omitted | 995.373 | 1003.538 | 1009.946 | 996.214 | 1010.587 |

| Cohort | AFTER server median ms | AFTER server p90 ms | AFTER server p95 ms | AFTER HTTP median ms | AFTER HTTP p95 ms | Server p95 change |
|---|---:|---:|---:|---:|---:|---:|
| Admin orders | 42.342 | 46.986 | 47.547 | 43.316 | 48.623 | -0.889% |
| Partner preview | 94.480 | 96.380 | 97.774 | 95.248 | 98.592 | +0.883% |
| Board true | 137.196 | 139.684 | 140.797 | 138.009 | 141.694 | -77.910% |
| Board false | 993.166 | 1001.956 | 1011.485 | 993.961 | 1012.059 | -0.189% |
| Board omitted | 992.674 | 1000.211 | 1006.738 | 993.376 | 1007.377 | -0.318% |

The 50 stable host/cohort/identity body groups match actual before/after bytes and
semantics, covering every scored observation and warmup. The qualified 50-request
smoke comparison also passes; smoke timings are excluded. Controls move by at most
0.9% at server p95. False/omitted still exceed 300 ms; this focused approval does
not authorize changing those paths.
Every per-run server and HTTP median/p90/p95 appears in the appendix below;
the original full-precision values remain in both validated proofs and summaries.

## Separate SQL diagnostics

Full-bind tracing is temporarily enabled only for the owned local role/database,
after scored requests. No authentication occurs while tracing is enabled. A fresh
trace profile issues five observations per literal cohort (25 GETs), joins exact
same-host traces and successful EF commands, maps PG PIDs to existing host
ApplicationNames and retains original SQL and complete bind vectors privately.
Both temporary logging overrides are reset in `finally`, confirmed by a fresh
connection, and owned trace hosts stop before any plan window.

The completed BEFORE capture supplies 11 reviewed case/bind selections from eight
distinct original SQL shapes: board true/false/null count, ordered IDs and main
projection, plus admin ordered IDs and preview projection controls. Manual review
checks original SQL/literals, named-to-positional mappings, exact EF traces and
physical request intervals. Each case has five bounded read-only plan executions
using original PG SQL/binds, default planner/JIT settings and actual PREPARE
inferred types. String/ULID IDs are not UUIDs. EXPLAIN ANALYZE overhead is included;
these are plan diagnostics, not endpoint latency. Fresh first EXECUTE does not
reproduce pooled Npgsql AutoPrepare or a reused generic plan.
Both captures and all 110 bounded plan executions complete.
Actual PostgreSQL SQL confirms three true-board legs
reduce seat aggregates from two to one; all eight selected control SQLs remain
byte-identical. Both changing clock binds, `@nowUtc` and
`@CleaningDateFrom_Value` (request time minus the existing two-hour allowance), are
qualified against the future corpus and unchanged source. Every non-clock bind,
position map and EF parameter-type description matches.

Each trace arm records 320 successful EF commands across 25 physical GETs: seven
per admin request, nine per preview, and sixteen per true/false/omitted board
request, five observations each. Command count is unchanged; this change removes
repeated work inside SQL rather than round trips or existing projection/pay reads.

The physical-clone and post-scored-window statistics dictionaries match exactly.
A later pre-plan check finds one additional newly analyzed `RefreshTokens` entry
after excluded auth refresh. All 44 existing table statistic hashes, all 15
relevant relation metadata records and statistics for the eight selected SQL
relations remain identical. No selected plan references `RefreshTokens`, and no
statistics, planner or JIT setting is changed to force comparability.

## Plan comparison and mechanism

Each row has five executions per arm. Plan p90 is the maximum of five samples,
using the same nearest-rank rule as the endpoint evidence. These separate
EXPLAIN ANALYZE runs include diagnostic overhead and are not added together to
estimate endpoint latency.

| Selected original SQL | BEFORE median / p90 ms | AFTER median / p90 ms |
|---|---:|---:|
| admin-orders-ordered-ids | 11.232 / 18.072 | 10.553 / 12.839 |
| partner-preview-preview-projection | 42.381 / 43.759 | 40.804 / 41.083 |
| partner-board-count | 310.934 / 312.905 | 63.101 / 70.241 |
| partner-board-ordered-ids | 355.688 / 364.163 | 67.836 / 67.954 |
| partner-board-full-projection | 0.659 / 0.781 | 0.582 / 0.588 |
| partner-board-false-count | 505.009 / 507.078 | 563.785 / 648.715 |
| partner-board-false-ordered-ids | 557.852 / 559.988 | 716.623 / 841.477 |
| partner-board-false-full-projection | 0.572 / 0.581 | 0.615 / 0.652 |
| partner-board-null-count | 500.455 / 508.150 | 594.049 / 648.855 |
| partner-board-null-ordered-ids | 551.100 / 558.222 | 673.198 / 719.311 |
| partner-board-null-full-projection | 0.616 / 0.640 | 0.645 / 0.662 |

The true-board count and ordered-ID plans reduce estimated total costs from
501986.19 / 502035.22 to 392551.36 / 392604.68, and shared buffer hits from
71591 / 71597 to 34205 / 34211; all five runs have zero shared reads. JIT remains
on. The retained defaults are `jit_above_cost=100000`,
`jit_inline_above_cost=500000` and `jit_optimize_above_cost=500000`. Before,
inlining and optimization are enabled; afterward the lower estimates cause both
to be disabled automatically. Median JIT diagnostic time falls from
217.443 / 266.754 to 15.341 / 18.852 ms for count / IDs. PostgreSQL documents
these cost decisions at planning time in its
[PostgreSQL 16 JIT decision documentation](https://www.postgresql.org/docs/16/jit-decision.html).
This supports the observed mechanism; it does not establish an exact decomposition
of JIT time inside the scored HTTP requests. No JIT or planner setting is tuned.

Identical false/null control count/ID SQL has noisier plan medians, increasing
11.64–28.46%, despite matching selected-relation statistics, estimated costs,
parameter types, settings and buffer blocks. Their measured HTTP/server controls
remain within 0.9% at server p95. These diagnostic increases are retained, not
rerun away or represented as endpoint regressions caused by the change.

## Verification and remaining gates

- Successful unchanged baseline and exact final-candidate Release builds.
- Final relational RED: 30 passed / 1 intended failure; GREEN: 31 passed.
- Latest master fetched and merged before candidate build; it remains base `6fa0492`.
- Source guard checks 7443 tracked source/workflow inputs: only the approved
  specification and existing test file change. All six native string catalogs,
  committed specs, three web SDKs, 453 retained Swift and 429 retained compiled/
  freshly generated Kotlin files match Wave A's verified inputs. The tracked
  measurement export's absent ignored SDK packages are explicitly distinguished.
- Full backend suites pass: 8469 unit, 845 integration and 434 host (9748 total),
  all executed/passed with no failure, error, skip, timeout or abort. Fresh serial
  Release `--no-build --no-restore` runs execute the exact AFTER build; pre/post
  source and binary hashes close unchanged. Unit / integration / host elapsed
  times are 115.937 / 449.035 / 244.433 seconds. Integration declares
  `postgres:latest`, host declares `postgres:16`; actual runtime versions were
  not observed and are not inferred from tags. These test databases are owned
  by the existing Testcontainers fixtures, separate from the fixed PG16.15
  measurement fixture.
- All nine applicable existing repository checkers pass: available-status-parity,
  module-boundaries, nx-project-registration, booking-policy-parity, catalog-claims,
  backlog-consistency, consistency, docs-refs and ios-symbols. All eight existing
  applicable checker self-test files pass (eight TAP subtests, no skips/failures).
  The existing VitePress docs build passes using retained dependencies and locked
  Node22.23.3. The deploy-only legal-draft checker is not applicable; its historical
  Wave A result remains 35 drafts / 105 hashes, rather than a newly claimed pass.
- graphifyy0.9.20 is already installed, correcting the original tooling assumption.
  The required AST graph rebuild runs with its existing Python runtime and supported
  single-worker setting. The first scratch runner omitted a Python main guard:
  worker re-entry failed despite an outer exit0, so that result is rejected. The
  corrected rebuild completes extraction of all 1635 uncached files and writes
  91392 nodes / 209361 edges / 3400 communities. It reports 112 zero-node files,
  23 SQL files lacking the optional SQL parser, and omits HTML above its size limit.
  Both changed C# files are present with manifest content hashes matching current
  source (4 specification nodes /22 test nodes). These qualifications do not become
  authority to install packages or edit maintained tooling.
- Independent paired evidence review rehashes all 5100 response bodies
  (249876540 bytes), recomputes every aggregate and per-run server/HTTP statistic,
  validates 5100 same-host completion joins and all 50 stable body groups, and
  checks source-product identity. Log-prefix hashes match the timing captures;
  later suffixes contain exactly ten excluded auth refresh POSTs per host.
- Only backend source and existing tests change. Web/native inputs and contracts
  remain identical to the verified Wave A inputs, so their local suites are not
  repeated. Existing PR workflow applicability is still evaluated independently.
- Existing applicable CI must pass on the exact final PR head. Its links/results
  and subsequent GitHub command receipts will be attached to the PR and retained
  in this scratchpad, avoiding a documentation-only commit/CI recursion.
- Merge requires Mike's approval of the concrete verified PR.

## Boundaries

No schema, API, DTO, client, planner, CI or deployment change is approved. The 300 ms
board p95 budget is a target, not a promised result or authority to expand scope.
Only disposable loopback synthetic services and simulator evidence are applicable.

## Command ledger

Commands, working directories, UTC starts, elapsed times, exits and raw logs are in
the scratchpad's `commands.jsonl`. Initial fetch and branch creation precede that
ledger and remain in Wave A's ledger as `root-board-wave-initial-fetch` and
`root-board-wave-new-branch`.

## Per-run measurements

Each row contains 100 scored HTTP200 requests and 100 unique same-host
server completion joins. Values are milliseconds, rounded only here.
Ten warmups per cohort and all smoke/auth/trace requests are excluded.

### BEFORE

| Cohort | Run | Server median | Server p90 | Server p95 | HTTP median | HTTP p90 | HTTP p95 |
|---|---:|---:|---:|---:|---:|---:|---:|
| admin-orders | 1 | 41.629 | 46.537 | 48.044 | 42.532 | 47.909 | 49.021 |
| admin-orders | 2 | 43.485 | 46.937 | 47.607 | 44.330 | 48.169 | 48.728 |
| admin-orders | 3 | 42.623 | 46.773 | 47.953 | 43.607 | 47.911 | 49.078 |
| admin-orders | 4 | 42.897 | 46.985 | 47.973 | 43.951 | 48.137 | 49.015 |
| admin-orders | 5 | 42.408 | 47.242 | 48.075 | 43.271 | 48.367 | 49.271 |
| partner-preview | 1 | 93.723 | 95.607 | 96.711 | 94.471 | 96.372 | 97.534 |
| partner-preview | 2 | 93.542 | 95.573 | 97.021 | 94.308 | 96.227 | 97.845 |
| partner-preview | 3 | 94.144 | 96.401 | 96.919 | 94.959 | 97.244 | 97.626 |
| partner-preview | 4 | 94.053 | 95.650 | 96.963 | 94.791 | 96.636 | 97.980 |
| partner-preview | 5 | 94.002 | 95.886 | 96.718 | 94.702 | 96.591 | 97.119 |
| partner-board | 1 | 619.912 | 632.085 | 636.555 | 620.604 | 632.770 | 637.228 |
| partner-board | 2 | 617.535 | 628.707 | 633.238 | 618.568 | 629.532 | 634.026 |
| partner-board | 3 | 620.029 | 626.822 | 633.878 | 620.767 | 627.706 | 634.474 |
| partner-board | 4 | 618.411 | 626.676 | 640.444 | 619.206 | 627.563 | 641.291 |
| partner-board | 5 | 620.616 | 631.551 | 638.796 | 621.370 | 632.361 | 639.496 |
| partner-board-false | 1 | 994.506 | 1001.944 | 1009.395 | 995.319 | 1002.870 | 1010.203 |
| partner-board-false | 2 | 994.533 | 1002.865 | 1013.612 | 995.521 | 1003.731 | 1014.398 |
| partner-board-false | 3 | 994.421 | 1000.786 | 1004.851 | 995.070 | 1001.510 | 1005.693 |
| partner-board-false | 4 | 995.406 | 1006.289 | 1011.942 | 996.125 | 1006.956 | 1012.550 |
| partner-board-false | 5 | 993.394 | 1006.611 | 1018.419 | 994.142 | 1007.398 | 1019.292 |
| partner-board-null | 1 | 995.836 | 1005.632 | 1015.362 | 996.676 | 1006.277 | 1016.284 |
| partner-board-null | 2 | 997.662 | 1004.423 | 1007.682 | 998.435 | 1005.212 | 1008.406 |
| partner-board-null | 3 | 995.395 | 1001.839 | 1008.600 | 996.245 | 1002.433 | 1009.561 |
| partner-board-null | 4 | 994.790 | 1002.651 | 1004.472 | 995.502 | 1003.169 | 1005.358 |
| partner-board-null | 5 | 994.909 | 1002.843 | 1016.303 | 995.693 | 1003.601 | 1017.118 |

### AFTER

| Cohort | Run | Server median | Server p90 | Server p95 | HTTP median | HTTP p90 | HTTP p95 |
|---|---:|---:|---:|---:|---:|---:|---:|
| admin-orders | 1 | 41.931 | 47.356 | 47.856 | 42.740 | 48.304 | 48.934 |
| admin-orders | 2 | 43.135 | 47.357 | 47.513 | 44.083 | 48.336 | 48.623 |
| admin-orders | 3 | 42.518 | 46.713 | 47.120 | 43.418 | 47.762 | 48.276 |
| admin-orders | 4 | 42.104 | 46.369 | 47.585 | 43.115 | 47.585 | 48.679 |
| admin-orders | 5 | 42.360 | 45.925 | 47.004 | 43.251 | 46.934 | 48.133 |
| partner-preview | 1 | 94.604 | 97.206 | 98.269 | 95.191 | 98.118 | 99.210 |
| partner-preview | 2 | 94.067 | 95.811 | 96.942 | 94.927 | 96.642 | 97.772 |
| partner-preview | 3 | 94.575 | 95.941 | 98.206 | 95.386 | 96.542 | 98.879 |
| partner-preview | 4 | 94.582 | 96.901 | 97.979 | 95.385 | 97.723 | 98.488 |
| partner-preview | 5 | 94.428 | 95.846 | 96.525 | 95.298 | 96.663 | 97.281 |
| partner-board | 1 | 137.645 | 140.162 | 141.750 | 138.412 | 141.015 | 142.614 |
| partner-board | 2 | 137.099 | 139.444 | 140.302 | 137.911 | 140.381 | 141.146 |
| partner-board | 3 | 137.620 | 139.820 | 140.668 | 138.559 | 140.708 | 141.525 |
| partner-board | 4 | 136.810 | 139.085 | 140.077 | 137.514 | 139.825 | 140.860 |
| partner-board | 5 | 137.450 | 139.853 | 140.882 | 138.277 | 140.594 | 141.840 |
| partner-board-false | 1 | 993.082 | 1000.776 | 1017.254 | 993.768 | 1001.603 | 1018.093 |
| partner-board-false | 2 | 993.214 | 1004.162 | 1012.362 | 994.015 | 1004.945 | 1013.108 |
| partner-board-false | 3 | 993.652 | 1001.789 | 1005.174 | 994.385 | 1002.563 | 1005.937 |
| partner-board-false | 4 | 992.914 | 1000.734 | 1004.413 | 993.705 | 1001.655 | 1005.184 |
| partner-board-false | 5 | 993.341 | 1002.979 | 1013.287 | 994.239 | 1003.841 | 1013.895 |
| partner-board-null | 1 | 991.621 | 999.236 | 1004.278 | 992.280 | 1000.264 | 1005.387 |
| partner-board-null | 2 | 992.295 | 999.602 | 1002.685 | 993.004 | 1000.523 | 1003.669 |
| partner-board-null | 3 | 992.804 | 1000.595 | 1009.904 | 993.538 | 1001.509 | 1010.536 |
| partner-board-null | 4 | 992.972 | 1001.159 | 1006.738 | 993.775 | 1002.048 | 1007.377 |
| partner-board-null | 5 | 992.851 | 999.610 | 1003.294 | 993.656 | 1000.572 | 1004.129 |

## Evidence identities and lifecycle

Hashes identify retained evidence, not a replacement for reading it. Original
SQL, complete bind vectors, synthetic auth material and actual response bodies
remain private scratch artifacts rather than copied into the repository.
The source-build lineage receipt pins every tracked input in both arms, both
Release build records/logs and the compiled API/Core.Domain binaries. Only the
two approved C# files differ. The bulk privacy context's assignment-ID map is
empty by a separate bounded SELECT proof; redacted zero assignment IDs are not
guessed into employee IDs. Security/own-assignment edges are established by the
relational and full existing suites, rather than claimed from the bulk corpus.

| Evidence relative to scratch | SHA256 |
|---|---|
| `raw/backend/seat-predicate-red-003-receipt.json` | `d7a5457a988d22d19a532a888f5449a3d98747917d769f7962bd9520a9f10160` |
| `raw/backend/seat-predicate-green-receipt.json` | `ea1c4c41f4b74fed31623d83c007afc42f8a7eed336289ae22330e934c5b10fb` |
| `raw/backend/arm-source-build-lineage.json` | `73efa48dbe0dfcd66509c635cdfda8047c47ba8587ed7f5ee32d0e049e7eea39` |
| `raw/backend/board-before-main-001-validated-proof.json` | `ac48f0b9c9dd8e5398990c73b70c261dd5e4ecda0b29a01058b3fc3928b22e7b` |
| `raw/backend/board-after-main-001-validated-proof.json` | `56992bdd27fe03c48c53c86620a1ddc2612123ed2b24cb3ee03c4743e0bf9894` |
| `raw/backend/board-main-paired-proof.json` | `efdbf0d659b5c75036e1beecf1db3c1662d369dd2e114302cecda45ac30870f7` |
| `raw/backend/board-select-paired-shapes.json` | `4c75e8e9090112234371d2bf882e9aab43af152d0a41367256b55cd90c2a716b` |
| `raw/backend/paired-plan-mechanism.json` | `2cf8269303d0fe29cc9031ddbe64875432cafcc64e10050e7e789d9dcbe6e795` |
| `raw/backend/tests/final-001/receipt.json` | `e2c53995dcf71096a87cb96e55011a6440b3ecf1f45db09307693c45de7f24ce` |
| `raw/backend/tests/final-001/runtime-closure.json` | `2fae1dc0d8d2aad653523e049bc71402d1cb0780e1e9b275f8984b509f893d92` |
| `raw/backend/tests/final-001/unit/unit.trx` | `742d884a4f4d03e96bc4f2d3cf976e3d3088731659be9a3dd6ec900a77f8a17d` |
| `raw/backend/tests/final-001/integration/integration.trx` | `6f78b76dab38c1413bdd06967874b3c375feaf7ca07ffb1bf09787d387738c90` |
| `raw/backend/tests/final-001/host/host.trx` | `69fff1e1e4ee326322ce01da8033fa5e72a558b2abf49d5332a8cf8f161900ea` |
| `raw/review-paired-evidence-web.json` | `c0b1667fbdd059d376ffce7bb86670fa8047cd6904c29d93966b180e79d0fced` |
| `raw/mobile/final-source-client-invariance.json` | `53630b4bf27143d9808c9c53a70b650dcacdc5bb142bcbc0a0b6bd1666e5499a` |
| `raw/review-board-corrected-graph-mobile.json` | `e9d9e10ec8ed1876734120292ed0e3bed0d12b96344697ed8baa0fe6a9c0bc9a` |

The local role/database tracing overrides are reset and all owned HTTP/trace
hosts have stopped. The frozen physical template is dropped after both arm
captures and plans. The guarded stop shuts down only the labelled
`cleansia-audit-pg-20261007` container at 16:43:20 UTC, retaining its named volume,
dumps and raw evidence. Docker Desktop itself remains available to the separate
functional Testcontainers suites. The temporary repo-root dependency symlink is
removed after the checkers; existing docs dependencies remain. No simulator or
device work, generator, DEV/PRO command, Azure command, provider, distribution or
deployment is executed in this wave.

## Local command appendix

This local snapshot contains 321 command records. Complete exact argv,
cwd, UTC start, elapsed seconds, exit and raw log path are retained in
`raw/report-command-ledger-local.json` (SHA256 `df5239de967617841f9fb67f601aa9d1ce9e2e104b62f17a1d8b734ea29f02d1`).
Below, `W` means the managed worktree, `B` this wave's scratchpad, `A` the
original audit scratchpad and `V` the Wave A scratchpad. Paths abbreviate only
those four prefixes. A Python `-c` body longer than 500 characters is named by
its SHA256 and exact record in the retained snapshot; the body is not a new
maintained script. Other argv are shown with shell quoting. Logs are retained
under each record's exact `log` path. Scratch helper invocations additionally
retain the helper source and its inner DB/container commands; inner operations
without individual timestamps are not assigned invented times. Backend full
suite group records are also in `raw/backend/tests/final-001/runs.json`.

Nonzero setup/review attempts and the three intentionally failing RED runs
remain visible. They precede corrected closed receipts and are not scored
HTTP failures. The first graph attempt's worker failures remain visible even
though its outer wrapper exited0. Optional SQL parsing and graph HTML remain
qualified as above. Subsequent push/PR/CI/merge-decision commands continue in
the live ledger and the PR's final verification receipt.

- `root-board-wave-initial-fetch` — 2026-10-08T13:44:59.905654+00:00; 0.917 s; exit 0; cwd `W`.

  ```text
  git fetch origin master
  ```

- `root-board-wave-new-branch` — 2026-10-08T13:46:39.638080+00:00; 0.023 s; exit 0; cwd `W`.

  ```text
  git switch -c fix/board-seat-predicate origin/master
  ```

- `root-board-baseline-archive` — 2026-10-08T13:49:21.776686+00:00; 0.879 s; exit 0; cwd `W`.

  ```text
  git archive --format=tar 6fa0492badf9ab5c9f30fee3beb680f7fcc1f265 --output B/artifacts/merged-baseline.tar
  ```

- `root-board-baseline-extract` — 2026-10-08T13:49:22.753372+00:00; 0.817 s; exit 0; cwd `B`.

  ```text
  tar -xf B/artifacts/merged-baseline.tar -C B/source-before
  ```

- `root-board-before-release-build` — 2026-10-08T13:49:23.640180+00:00; 24.579 s; exit 0; cwd `B/source-before/src`.

  ```text
  /opt/homebrew/bin/dotnet build Cleansia.Api.sln -c Release --nologo
  ```

- `mobile-board-client-config-read` — 2026-10-08T13:56:55.726985+00:00; 0.017 s; exit 0; cwd `W`.

  ```text
  python3 -c 'import json,pathlib; r=pathlib.Path("W/src/Cleansia.App");
  for p in sorted(r.glob("nswag-*.json")):
   d=json.loads(p.read_text()); print(p.name,json.dumps({"output":d.get("codeGenerators",{}).get("openApiToTypeScriptClient",{}).get("output")}))'
  ```

- `root-board-local-tools-preparation` — 2026-10-08T13:59:25.675451+00:00; 0.044 s; exit 1; cwd `B`.

  ```text
  python3 tools/prepare-local-tools.py
  ```

- `mobile-board-baseline-source-invariance` — 2026-10-08T13:59:26.291666+00:00; 1.259 s; exit 1; cwd `W`.

  ```text
  python3 B/tools/mobile-source-invariance.py baseline
  ```

- `root-board-local-tools-preparation-v2` — 2026-10-08T13:59:40.789460+00:00; 0.028 s; exit 0; cwd `B`.

  ```text
  python3 tools/prepare-local-tools.py
  ```

- `root-board-helper-static-check` — 2026-10-08T13:59:52.518857+00:00; 0.033 s; exit 0; cwd `B`.

  ```text
  python3 -m py_compile tools/local-db.py tools/backend-hosts.py tools/prepare_backend_fixture.py tools/start-owned-postgres.py
  ```

- `board-owned-postgres-start` — 2026-10-08T13:59:52.755001+00:00; 1.221 s; exit 0; cwd `B`.

  ```text
  docker --context desktop-linux start cleansia-audit-pg-20261007
  ```

- `root-board-pg-owned-start` — 2026-10-08T13:59:52.639155+00:00; 1.357 s; exit 0; cwd `B`.

  ```text
  python3 tools/start-owned-postgres.py
  ```

- `root-board-fresh-fixture-generator` — 2026-10-08T13:59:54.066948+00:00; 0.037 s; exit 0; cwd `B`.

  ```text
  python3 tools/prepare_backend_fixture.py --size primary --anchor 2026-10-12T12:00:00Z
  ```

- `board-seat-test-source-clone` — 2026-10-08T14:00:11.033261+00:00; 1.548 s; exit 0; cwd `B`.

  ```text
  cp -cR B/source-before B/test-source
  ```

- `mobile-board-baseline-source-invariance-staged-test` — 2026-10-08T14:00:16.167217+00:00; 0.654 s; exit 0; cwd `W`.

  ```text
  python3 B/tools/mobile-source-invariance.py baseline --allowed-change src/Cleansia.Tests/Features/Orders/OrderSpecificationCurrentStatusTests.cs
  ```

- `board-reference-before-postgres-local-drop-for-restore` — 2026-10-08T14:00:29.535817+00:00; 0.080 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec cleansia-audit-pg-20261007 dropdb -U cleansia_audit --force --if-exists CleansiaAudit
  ```

- `board-reference-before-postgres-local-create-for-restore` — 2026-10-08T14:00:29.615825+00:00; 0.117 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec cleansia-audit-pg-20261007 createdb -U cleansia_audit CleansiaAudit
  ```

- `board-reference-before-reference-before` — 2026-10-08T14:00:29.732735+00:00; 0.451 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 pg_restore -U cleansia_audit --dbname CleansiaAudit --exit-on-error
  ```

- `root-board-reference-restore` — 2026-10-08T14:00:29.492495+00:00; 0.707 s; exit 0; cwd `B`.

  ```text
  python3 tools/local-db.py restore --file B/private/backend-reference.dump --label reference-before
  ```

- `board-seat-test-overlay-001` — 2026-10-08T14:00:46.509604+00:00; 0.003 s; exit 0; cwd `B`.

  ```text
  cp W/src/Cleansia.Tests/Features/Orders/OrderSpecificationCurrentStatusTests.cs B/test-source/src/Cleansia.Tests/Features/Orders/OrderSpecificationCurrentStatusTests.cs
  ```

- `board-http-requester-copy` — 2026-10-08T14:00:50.935620+00:00; 0.003 s; exit 0; cwd `B`.

  ```text
  /bin/cp V/tools/backend-requests.py B/tools/backend-requests.py
  ```

- `board-seat-spec-red-001` — 2026-10-08T14:00:53.029740+00:00; 32.628 s; exit 1; cwd `B/test-source/src`.

  ```text
  dotnet test Cleansia.Tests/Cleansia.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~OrderSpecificationCurrentStatusTests' --logger trx --results-directory B/raw/backend/spec-red-001
  ```

- `board-http-requester-preimage` — 2026-10-08T14:01:45.636579+00:00; 0.003 s; exit 0; cwd `B`.

  ```text
  /bin/cp B/tools/backend-requests.py B/tools/backend-requests-wave-preimage.py
  ```

- `board-seat-test-overlay-002` — 2026-10-08T14:02:05.211552+00:00; 0.002 s; exit 0; cwd `B`.

  ```text
  cp W/src/Cleansia.Tests/Features/Orders/OrderSpecificationCurrentStatusTests.cs B/test-source/src/Cleansia.Tests/Features/Orders/OrderSpecificationCurrentStatusTests.cs
  ```

- `board-fresh-primary-fresh-primary` — 2026-10-08T14:00:30.315124+00:00; 122.869 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1 -v audit_local_fixture=1
  ```

- `root-board-fresh-fixture-load` — 2026-10-08T14:00:30.274455+00:00; 122.916 s; exit 0; cwd `B`.

  ```text
  python3 tools/local-db.py sql --file B/tools/backend-fixture-primary.sql --label fresh-primary
  ```

- `board-seat-spec-red-002` — 2026-10-08T14:02:11.254701+00:00; 24.943 s; exit 1; cwd `B/test-source/src`.

  ```text
  dotnet test Cleansia.Tests/Cleansia.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~OrderSpecificationCurrentStatusTests' --logger trx --results-directory B/raw/backend/spec-red-002
  ```

- `board-fixture-before-fixture-before` — 2026-10-08T14:02:52.279324+00:00; 3.753 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1 -v audit_local_fixture=1
  ```

- `root-board-fixture-qualification-before` — 2026-10-08T14:02:52.222071+00:00; 3.825 s; exit 0; cwd `B`.

  ```text
  python3 tools/local-db.py sql --file B/tools/board-fixture-qualification.sql --output B/raw/backend/fixture-qualified-before.json --label fixture-before
  ```

- `board-frozen-primary-frozen-primary` — 2026-10-08T14:03:12.692585+00:00; 0.931 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec cleansia-audit-pg-20261007 pg_dump -U cleansia_audit -d CleansiaAudit --format=custom
  ```

- `root-board-frozen-fixture-dump` — 2026-10-08T14:03:12.644860+00:00; 0.994 s; exit 0; cwd `B`.

  ```text
  python3 tools/local-db.py dump --file B/private/board-primary-frozen.dump --label frozen-primary
  ```

- `board-context-context` — 2026-10-08T14:03:13.758275+00:00; 0.071 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1 -v audit_local_fixture=1
  ```

- `root-board-fresh-context` — 2026-10-08T14:03:13.716424+00:00; 0.118 s; exit 0; cwd `B`.

  ```text
  python3 tools/local-db.py sql --file B/tools/backend-context.sql --output B/raw/backend/scenario-context.json --label context
  ```

- `root-board-auth-helper-copy` — 2026-10-08T14:03:32.095907+00:00; 0.003 s; exit 0; cwd `B`.

  ```text
  cp V/tools/backend-requests.py tools/auth-requests.py
  ```

- `root-board-before-host-start` — 2026-10-08T14:03:32.188099+00:00; 2.493 s; exit 0; cwd `B`.

  ```text
  python3 tools/backend-hosts.py start --arm before --hosting-timing --local-baseline --log-prefix board-before-main
  ```

- `root-board-before-synthetic-bootstrap` — 2026-10-08T14:04:01.207302+00:00; 7.848 s; exit 0; cwd `B`.

  ```text
  python3 tools/auth-requests.py bootstrap --rps 8 --label board-before-auth
  ```

- `root-board-before-profile-freeze` — 2026-10-08T14:05:28.806825+00:00; 0.129 s; exit 0; cwd `B`.

  ```text
  python3 tools/freeze-before-profile.py
  ```

- `board-requester-static-compile` — 2026-10-08T14:08:30.041710+00:00; 0.038 s; exit 0; cwd `B`.

  ```text
  python3 -m py_compile tools/backend-requests.py tools/backend-request-proof.py
  ```

- `board-requester-help` — 2026-10-08T14:08:30.041715+00:00; 0.053 s; exit 0; cwd `B`.

  ```text
  python3 tools/backend-requests.py --help
  ```

- `board-request-proof-help` — 2026-10-08T14:08:30.041695+00:00; 0.056 s; exit 0; cwd `B`.

  ```text
  python3 tools/backend-request-proof.py --help
  ```

- `board-seat-test-overlay-003` — 2026-10-08T14:09:21.260103+00:00; 0.005 s; exit 0; cwd `B`.

  ```text
  cp W/src/Cleansia.Tests/Features/Orders/OrderSpecificationCurrentStatusTests.cs B/test-source/src/Cleansia.Tests/Features/Orders/OrderSpecificationCurrentStatusTests.cs
  ```

- `root-board-before-smoke-001` — 2026-10-08T14:09:12.473430+00:00; 35.162 s; exit 0; cwd `B`.

  ```text
  python3 tools/backend-requests.py smoke --arm before --phase diagnostic --label board-before-smoke-001
  ```

- `board-seat-spec-red-003` — 2026-10-08T14:09:29.932282+00:00; 25.714 s; exit 1; cwd `B/test-source/src`.

  ```text
  dotnet test Cleansia.Tests/Cleansia.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~OrderSpecificationCurrentStatusTests' --logger trx --results-directory B/raw/backend/spec-red-003
  ```

- `mobile-board-select-tool-preimage-copy` — 2026-10-08T14:11:44.811911+00:00; 0.020 s; exit 0; cwd `B`.

  ```text
  python3 -c '<inline Python; SHA256 f57e68788d400bed35b63d2d7bca2659934cbc8464ca20e0b3deec7b1f895621; exact argv in retained record>'
  ```

- `board-request-proof-static-002` — 2026-10-08T14:12:35.934339+00:00; 0.031 s; exit 0; cwd `B`.

  ```text
  python3 -m py_compile tools/backend-request-proof.py
  ```

- `root-board-assignment-map-prepare` — 2026-10-08T14:14:08.154478+00:00; 0.030 s; exit 1; cwd `B`.

  ```text
  python3 tools/prepare-assignment-map.py
  ```

- `root-board-assignment-map-read` — 2026-10-08T14:14:08.268805+00:00; 0.104 s; exit 1; cwd `B`.

  ```text
  python3 tools/local-db.py sql --file B/tools/board-assignment-map.sql --output B/raw/backend/assignment-map.json --label assignment-map
  ```

- `root-board-assignment-context-freeze` — 2026-10-08T14:14:08.433436+00:00; 0.026 s; exit 1; cwd `B`.

  ```text
  python3 tools/freeze-assignment-context.py
  ```

- `root-board-assignment-map-prepare-v2` — 2026-10-08T14:15:15.162976+00:00; 0.022 s; exit 0; cwd `B`.

  ```text
  python3 tools/prepare-assignment-map.py
  ```

- `board-assignment-map-v2-assignment-map-v2` — 2026-10-08T14:15:44.006391+00:00; 0.055 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1 -v audit_local_fixture=1
  ```

- `root-board-assignment-map-read-v2` — 2026-10-08T14:15:43.956058+00:00; 0.111 s; exit 0; cwd `B`.

  ```text
  python3 tools/local-db.py sql --file B/tools/board-assignment-map.sql --output B/raw/backend/assignment-map.json --label assignment-map-v2
  ```

- `root-board-assignment-context-freeze-v2` — 2026-10-08T14:15:44.142704+00:00; 0.020 s; exit 0; cwd `B`.

  ```text
  python3 tools/freeze-assignment-context.py
  ```

- `root-board-setup-host-stop` — 2026-10-08T14:15:44.249880+00:00; 0.071 s; exit 0; cwd `B`.

  ```text
  python3 tools/backend-hosts.py stop --arm before
  ```

- `board-physical-freeze-container-guard` — 2026-10-08T14:15:44.437153+00:00; 0.018 s; exit 0; cwd `B`.

  ```text
  docker --context desktop-linux inspect --format '{{json .Name}}
  {{json .Config.Labels}}
  {{json .HostConfig.PortBindings}}
  {{json .Mounts}}' cleansia-audit-pg-20261007
  ```

- `board-physical-freeze-clone-sql` — 2026-10-08T14:15:44.455932+00:00; 0.734 s; exit 0; cwd `B`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d postgres -v ON_ERROR_STOP=1
  ```

- `root-board-physical-fixture-freeze` — 2026-10-08T14:15:44.409865+00:00; 0.801 s; exit 0; cwd `B`.

  ```text
  python3 tools/physical-fixture.py freeze --label board-physical-freeze
  ```

- `board-physical-before-container-guard` — 2026-10-08T14:16:30.668287+00:00; 0.017 s; exit 0; cwd `B`.

  ```text
  docker --context desktop-linux inspect --format '{{json .Name}}
  {{json .Config.Labels}}
  {{json .HostConfig.PortBindings}}
  {{json .Mounts}}' cleansia-audit-pg-20261007
  ```

- `board-physical-before-clone-sql` — 2026-10-08T14:16:30.685698+00:00; 0.940 s; exit 0; cwd `B`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d postgres -v ON_ERROR_STOP=1
  ```

- `root-board-before-physical-restore` — 2026-10-08T14:16:30.640475+00:00; 1.003 s; exit 0; cwd `B`.

  ```text
  python3 tools/physical-fixture.py restore --label board-physical-before
  ```

- `board-clone-before-clone-before` — 2026-10-08T14:16:31.757213+00:00; 3.732 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1 -v audit_local_fixture=1
  ```

- `root-board-before-clone-qualification` — 2026-10-08T14:16:31.717734+00:00; 3.787 s; exit 0; cwd `B`.

  ```text
  python3 tools/local-db.py sql --file B/tools/board-fixture-qualification.sql --output B/raw/backend/fixture-clone-before.json --label clone-before
  ```

- `root-board-before-qualified-host-start` — 2026-10-08T14:16:47.189349+00:00; 2.165 s; exit 0; cwd `B`.

  ```text
  python3 tools/backend-hosts.py start --arm before --hosting-timing --local-baseline --log-prefix board-before-qualified
  ```

- `root-board-before-qualified-auth` — 2026-10-08T14:17:00.056707+00:00; 7.733 s; exit 0; cwd `B`.

  ```text
  python3 tools/auth-requests.py bootstrap --rps 8 --label board-before-qualified-auth
  ```

- `root-board-before-qualified-smoke-context` — 2026-10-08T14:17:30.436754+00:00; 0.003 s; exit 0; cwd `B`.

  ```text
  cp raw/backend/scenario-context.json raw/backend/board-before-smoke-002-context-snapshot.json
  ```

- `board-request-proof-static-003` — 2026-10-08T14:17:33.447108+00:00; 0.035 s; exit 0; cwd `B`.

  ```text
  python3 -m py_compile tools/backend-request-proof.py
  ```

- `root-board-before-smoke-002` — 2026-10-08T14:17:30.521271+00:00; 29.572 s; exit 0; cwd `B`.

  ```text
  python3 tools/backend-requests.py smoke --arm before --phase diagnostic --label board-before-smoke-002
  ```

- `root-board-before-smoke-001-proof` — 2026-10-08T14:19:03.823975+00:00; 0.122 s; exit 0; cwd `B`.

  ```text
  python3 tools/backend-request-proof.py join --label board-before-smoke-001
  ```

- `root-board-before-smoke-002-proof` — 2026-10-08T14:19:04.004706+00:00; 0.114 s; exit 0; cwd `B`.

  ```text
  python3 tools/backend-request-proof.py join --label board-before-smoke-002
  ```

- `root-board-before-main-context` — 2026-10-08T14:19:48.088113+00:00; 0.004 s; exit 0; cwd `B`.

  ```text
  cp raw/backend/scenario-context.json raw/backend/board-before-main-001-context-snapshot.json
  ```

- `mobile-board-select-tools-static-001` — 2026-10-08T14:21:03.789662+00:00; 0.030 s; exit 0; cwd `B`.

  ```text
  python3 -c '<inline Python; SHA256 b52bff58c0349d4012d87a88111b95dcd4ca2c2828f327ae67a54b1e3fd748de; exact argv in retained record>'
  ```

- `mobile-board-select-tools-static-002` — 2026-10-08T14:24:02.793344+00:00; 0.030 s; exit 0; cwd `B`.

  ```text
  python3 -c '<inline Python; SHA256 5914b6e45d319fa852e776ec3af57a5da9c03ff3026c101662f515da44b2fa5e; exact argv in retained record>'
  ```

- `mobile-board-select-controller-static-003` — 2026-10-08T14:36:41.225574+00:00; 0.026 s; exit 0; cwd `B`.

  ```text
  python3 -c '<inline Python; SHA256 18b703bbb519f7087854c1a56153b5c64e2b65a7db2f8e2ef3f187420b9ce4b1; exact argv in retained record>'
  ```

- `root-board-before-main-measure` — 2026-10-08T14:19:48.177547+00:00; 1476.045 s; exit 0; cwd `B`.

  ```text
  python3 tools/backend-requests.py measure --arm before --phase timing --label board-before-main-001 --runs 5 --samples 100 --warmup 10 --rps 8
  ```

- `root-board-before-main-proof` — 2026-10-08T14:48:34.147714+00:00; 3.488 s; exit 0; cwd `B`.

  ```text
  python3 tools/backend-request-proof.py join --label board-before-main-001
  ```

- `board-seat-spec-green-overlay-001` — 2026-10-08T14:48:55.283743+00:00; 0.004 s; exit 0; cwd `B`.

  ```text
  cp W/src/Cleansia.Core.Domain/Specifications/OrderSpecification.cs B/test-source/src/Cleansia.Core.Domain/Specifications/OrderSpecification.cs
  ```

- `board-board-before-post-timing-fixture-board-before-post-timing-fixture` — 2026-10-08T14:48:58.390172+00:00; 3.770 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1 -v audit_local_fixture=1
  ```

- `root-board-before-post-timing-fixture` — 2026-10-08T14:48:58.288765+00:00; 3.886 s; exit 0; cwd `B`.

  ```text
  python3 tools/local-db.py sql --file B/tools/board-fixture-qualification.sql --output B/raw/backend/fixture-post-timing-before.json --label board-before-post-timing-fixture
  ```

- `board-seat-test-green-overlay-001` — 2026-10-08T14:49:07.504078+00:00; 0.004 s; exit 0; cwd `B`.

  ```text
  cp W/src/Cleansia.Tests/Features/Orders/OrderSpecificationCurrentStatusTests.cs B/test-source/src/Cleansia.Tests/Features/Orders/OrderSpecificationCurrentStatusTests.cs
  ```

- `root-board-before-trace-auth-refresh` — 2026-10-08T14:49:10.742042+00:00; 6.180 s; exit 0; cwd `B`.

  ```text
  python3 tools/auth-requests.py refresh --rps 8 --label board-before-trace-auth
  ```

- `board-seat-spec-green-001` — 2026-10-08T14:49:07.574488+00:00; 25.655 s; exit 0; cwd `B/test-source/src`.

  ```text
  dotnet test Cleansia.Tests/Cleansia.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~OrderSpecificationCurrentStatusTests' --logger trx --results-directory B/raw/backend/spec-green-001
  ```

- `board-before-select-01-container-label` — 2026-10-08T14:49:33.349364+00:00; 0.089 s; exit 0; cwd `B`.

  ```text
  docker --context desktop-linux inspect --format '{{ index .Config.Labels "cleansia.audit" }}' cleansia-audit-pg-20261007
  ```

- `board-before-select-01-container-ports` — 2026-10-08T14:49:33.438235+00:00; 0.045 s; exit 0; cwd `B`.

  ```text
  docker --context desktop-linux inspect --format '{{json .NetworkSettings.Ports}}' cleansia-audit-pg-20261007
  ```

- `board-board-before-select-01-settings-before-sql-board-before-select-01-settings-before-sql` — 2026-10-08T14:49:33.532617+00:00; 0.077 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1 -v audit_local_fixture=1
  ```

- `board-before-select-01-settings-before` — 2026-10-08T14:49:33.483375+00:00; 0.176 s; exit 0; cwd `B`.

  ```text
  /opt/homebrew/opt/python@3.14/bin/python3.14 B/tools/local-db.py sql --file B/private/board-before-select-01-settings-before.sql --label board-before-select-01-settings-before-sql --output B/private/board-before-select-01-settings-before-result.log
  ```

- `board-before-select-01-normal-stop` — 2026-10-08T14:49:33.673065+00:00; 0.135 s; exit 0; cwd `B`.

  ```text
  /opt/homebrew/opt/python@3.14/bin/python3.14 B/tools/backend-hosts.py stop
  ```

- `board-board-before-select-01-enable-sql-board-before-select-01-enable-sql` — 2026-10-08T14:49:33.868723+00:00; 0.061 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1 -v audit_local_fixture=1
  ```

- `board-before-select-01-enable` — 2026-10-08T14:49:33.824105+00:00; 0.127 s; exit 0; cwd `B`.

  ```text
  /opt/homebrew/opt/python@3.14/bin/python3.14 B/tools/local-db.py sql --file B/tools/backend-pg-trace-on.sql --label board-before-select-01-enable-sql
  ```

- `board-board-before-select-01-settings-enabled-sql-board-before-select-01-settings-enabled-sql` — 2026-10-08T14:49:33.997556+00:00; 0.051 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1 -v audit_local_fixture=1
  ```

- `board-before-select-01-settings-enabled` — 2026-10-08T14:49:33.951939+00:00; 0.138 s; exit 0; cwd `B`.

  ```text
  /opt/homebrew/opt/python@3.14/bin/python3.14 B/tools/local-db.py sql --file B/private/board-before-select-01-settings-enabled.sql --label board-before-select-01-settings-enabled-sql --output B/private/board-before-select-01-settings-enabled-result.log
  ```

- `board-before-select-01-trace-start` — 2026-10-08T14:49:34.089977+00:00; 2.218 s; exit 0; cwd `B`.

  ```text
  /opt/homebrew/opt/python@3.14/bin/python3.14 B/tools/backend-hosts.py start --arm before --local-baseline --trace --log-prefix board-before-select-01-host
  ```

- `board-before-select-01-requests` — 2026-10-08T14:49:36.308918+00:00; 15.777 s; exit 0; cwd `B`.

  ```text
  /opt/homebrew/opt/python@3.14/bin/python3.14 B/tools/backend-requests.py measure --arm before --phase ef --runs 5 --samples 1 --warmup 0 --rps 8 --label board-before-select-01
  ```

- `board-board-before-select-01-settings-after-requests-sql-board-before-select-01-settings-after-requests-sql` — 2026-10-08T14:49:52.258517+00:00; 0.056 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1 -v audit_local_fixture=1
  ```

- `board-before-select-01-settings-after-requests` — 2026-10-08T14:49:52.086948+00:00; 0.241 s; exit 0; cwd `B`.

  ```text
  /opt/homebrew/opt/python@3.14/bin/python3.14 B/tools/local-db.py sql --file B/private/board-before-select-01-settings-after-requests.sql --label board-before-select-01-settings-after-requests-sql --output B/private/board-before-select-01-settings-after-requests-result.log
  ```

- `board-before-select-01-proof` — 2026-10-08T14:49:52.328731+00:00; 0.203 s; exit 0; cwd `B`.

  ```text
  /opt/homebrew/opt/python@3.14/bin/python3.14 B/tools/backend-request-proof.py join --label board-before-select-01
  ```

- `board-before-select-01-trace-stop` — 2026-10-08T14:49:52.543792+00:00; 0.142 s; exit 0; cwd `B`.

  ```text
  /opt/homebrew/opt/python@3.14/bin/python3.14 B/tools/backend-hosts.py stop
  ```

- `board-before-select-01-docker-log-capture` — 2026-10-08T14:49:52.704661+00:00; 0.127 s; exit 0; cwd `B`.

  ```text
  docker --context desktop-linux logs --since 2026-10-08T14:49:33.824057+00:00 --until 2026-10-08T14:49:52.704624+00:00 cleansia-audit-pg-20261007
  ```

- `board-board-before-select-01-disable-sql-board-before-select-01-disable-sql` — 2026-10-08T14:49:52.878548+00:00; 0.046 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1 -v audit_local_fixture=1
  ```

- `board-before-select-01-disable` — 2026-10-08T14:49:52.833381+00:00; 0.132 s; exit 0; cwd `B`.

  ```text
  /opt/homebrew/opt/python@3.14/bin/python3.14 B/tools/local-db.py sql --file B/tools/backend-pg-trace-off.sql --label board-before-select-01-disable-sql
  ```

- `board-board-before-select-01-settings-after-sql-board-before-select-01-settings-after-sql` — 2026-10-08T14:49:53.011755+00:00; 0.045 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1 -v audit_local_fixture=1
  ```

- `board-before-select-01-settings-after` — 2026-10-08T14:49:52.966195+00:00; 0.125 s; exit 0; cwd `B`.

  ```text
  /opt/homebrew/opt/python@3.14/bin/python3.14 B/tools/local-db.py sql --file B/private/board-before-select-01-settings-after.sql --label board-before-select-01-settings-after-sql --output B/private/board-before-select-01-settings-after-result.log
  ```

- `board-before-select-01-extract` — 2026-10-08T14:49:53.103402+00:00; 0.147 s; exit 0; cwd `B`.

  ```text
  /opt/homebrew/opt/python@3.14/bin/python3.14 B/tools/backend-pg-select-extract.py --arm before --capture-manifest B/raw/backend/board-before-select-01-capture-manifest.json --local-synthetic-log --full-bind-values-confirmed --input B/private/board-before-select-01-postgres.log --output B/private/board-before-select-01-sql-candidates.json
  ```

- `root-board-before-select-controller-01` — 2026-10-08T14:49:33.305219+00:00; 19.951 s; exit 0; cwd `B`.

  ```text
  python3 tools/backend-select-capture.py --arm before --local-isolated-window --label board-before-select-01
  ```

- `board-seat-spec-final-byte-overlay-001` — 2026-10-08T14:50:29.080568+00:00; 0.003 s; exit 0; cwd `B`.

  ```text
  cp W/src/Cleansia.Core.Domain/Specifications/OrderSpecification.cs B/test-source/src/Cleansia.Core.Domain/Specifications/OrderSpecification.cs
  ```

- `root-board-final-master-fetch` — 2026-10-08T14:50:52.415256+00:00; 0.575 s; exit 0; cwd `W`.

  ```text
  git fetch origin master
  ```

- `root-board-clone-after` — 2026-10-08T14:50:53.063301+00:00; 1.629 s; exit 0; cwd `B`.

  ```text
  cp -cR source-before source-after
  ```

- `mobile-board-before-select-offline-inventory-01` — 2026-10-08T14:51:02.961082+00:00; 0.024 s; exit 0; cwd `B`.

  ```text
  python3 -c '<inline Python; SHA256 7a926609b3e96706b17aada97c106bee89c3ebcbe7326d63c071fb64c7c91b13; exact argv in retained record>'
  ```

- `root-board-merge-latest-master` — 2026-10-08T14:51:03.116515+00:00; 0.012 s; exit 0; cwd `W`.

  ```text
  git merge --no-edit origin/master
  ```

- `root-board-after-overlay-spec` — 2026-10-08T14:51:14.488440+00:00; 0.003 s; exit 0; cwd `B`.

  ```text
  cp W/src/Cleansia.Core.Domain/Specifications/OrderSpecification.cs B/source-after/src/Cleansia.Core.Domain/Specifications/OrderSpecification.cs
  ```

- `root-board-after-overlay-tests` — 2026-10-08T14:51:14.575937+00:00; 0.002 s; exit 0; cwd `B`.

  ```text
  cp W/src/Cleansia.Tests/Features/Orders/OrderSpecificationCurrentStatusTests.cs B/source-after/src/Cleansia.Tests/Features/Orders/OrderSpecificationCurrentStatusTests.cs
  ```

- `root-board-after-release-build` — 2026-10-08T14:51:14.660505+00:00; 24.762 s; exit 0; cwd `B/source-after/src`.

  ```text
  /opt/homebrew/bin/dotnet build Cleansia.Api.sln -c Release
  ```

- `mobile-board-before-selected-shapes-inspect-01` — 2026-10-08T14:51:49.805275+00:00; 0.069 s; exit 0; cwd `B`.

  ```text
  python3 -c '<inline Python; SHA256 d682a2debd73bcb4f37271719c75fb4008e0edaa925d8dccef791ae2f7d910a5; exact argv in retained record>'
  ```

- `root-board-final-client-invariance` — 2026-10-08T14:53:35.094061+00:00; 1.655 s; exit 0; cwd `B`.

  ```text
  python3 tools/mobile-source-invariance.py final --allowed-change src/Cleansia.Core.Domain/Specifications/OrderSpecification.cs --allowed-change src/Cleansia.Tests/Features/Orders/OrderSpecificationCurrentStatusTests.cs
  ```

- `mobile-board-before-select-proposal-01` — 2026-10-08T14:54:43.652148+00:00; 0.298 s; exit 0; cwd `B`.

  ```text
  python3 tools/backend-select-propose.py --arm before --capture-label board-before-select-01 --output B/private/board-before-select-01-proposed-selection.json
  ```

- `mobile-board-before-select-type-inspection-01` — 2026-10-08T14:55:33.591116+00:00; 0.018 s; exit 0; cwd `B`.

  ```text
  python3 -c '<inline Python; SHA256 768e12ed2f1dfd59b4abfdf6cbdde1991c76f127784280d221c76195f06231b0; exact argv in retained record>'
  ```

- `board-before-plans-01-container-label` — 2026-10-08T14:55:49.299759+00:00; 0.140 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux inspect --format '{{ index .Config.Labels "cleansia.audit" }}' cleansia-audit-pg-20261007
  ```

- `board-before-plans-01-container-ports` — 2026-10-08T14:55:49.440559+00:00; 0.023 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux inspect --format '{{json .NetworkSettings.Ports}}' cleansia-audit-pg-20261007
  ```

- `board-before-plans-01-plan-admin-orders-ordered-ids-r1` — 2026-10-08T14:55:49.464252+00:00; 0.126 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-admin-orders-ordered-ids-r2` — 2026-10-08T14:55:49.591377+00:00; 0.083 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-admin-orders-ordered-ids-r3` — 2026-10-08T14:55:49.675239+00:00; 0.079 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-admin-orders-ordered-ids-r4` — 2026-10-08T14:55:49.754965+00:00; 0.074 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-admin-orders-ordered-ids-r5` — 2026-10-08T14:55:49.830404+00:00; 0.077 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-preview-preview-projection-r1` — 2026-10-08T14:55:49.908572+00:00; 0.141 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-preview-preview-projection-r2` — 2026-10-08T14:55:50.052283+00:00; 0.126 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-preview-preview-projection-r3` — 2026-10-08T14:55:50.180183+00:00; 0.140 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-preview-preview-projection-r4` — 2026-10-08T14:55:50.322132+00:00; 0.135 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-preview-preview-projection-r5` — 2026-10-08T14:55:50.458888+00:00; 0.143 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-board-count-r1` — 2026-10-08T14:55:50.604041+00:00; 0.409 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-board-count-r2` — 2026-10-08T14:55:51.016125+00:00; 0.383 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-board-count-r3` — 2026-10-08T14:55:51.401853+00:00; 0.421 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-board-count-r4` — 2026-10-08T14:55:51.824857+00:00; 0.413 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-board-count-r5` — 2026-10-08T14:55:52.239311+00:00; 0.424 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-board-ordered-ids-r1` — 2026-10-08T14:55:52.667857+00:00; 0.428 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-board-ordered-ids-r2` — 2026-10-08T14:55:53.098741+00:00; 0.422 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-board-ordered-ids-r3` — 2026-10-08T14:55:53.522482+00:00; 0.416 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-board-ordered-ids-r4` — 2026-10-08T14:55:53.940694+00:00; 0.465 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-board-ordered-ids-r5` — 2026-10-08T14:55:54.408515+00:00; 0.464 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-board-full-projection-r1` — 2026-10-08T14:55:54.875363+00:00; 0.078 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-board-full-projection-r2` — 2026-10-08T14:55:54.954310+00:00; 0.074 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-board-full-projection-r3` — 2026-10-08T14:55:55.029646+00:00; 0.075 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-board-full-projection-r4` — 2026-10-08T14:55:55.106446+00:00; 0.071 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-board-full-projection-r5` — 2026-10-08T14:55:55.178868+00:00; 0.072 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-board-false-count-r1` — 2026-10-08T14:55:55.253789+00:00; 0.597 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-board-false-count-r2` — 2026-10-08T14:55:55.852278+00:00; 0.581 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-board-false-count-r3` — 2026-10-08T14:55:56.434493+00:00; 0.589 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-board-false-count-r4` — 2026-10-08T14:55:57.025382+00:00; 0.586 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-board-false-count-r5` — 2026-10-08T14:55:57.613493+00:00; 0.601 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-board-false-ordered-ids-r1` — 2026-10-08T14:55:58.219064+00:00; 0.650 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-board-false-ordered-ids-r2` — 2026-10-08T14:55:58.870925+00:00; 0.644 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-board-false-ordered-ids-r3` — 2026-10-08T14:55:59.517345+00:00; 0.635 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-board-false-ordered-ids-r4` — 2026-10-08T14:56:00.154183+00:00; 0.646 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-board-false-ordered-ids-r5` — 2026-10-08T14:56:00.802436+00:00; 0.650 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-board-false-full-projection-r1` — 2026-10-08T14:56:01.455116+00:00; 0.078 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-board-false-full-projection-r2` — 2026-10-08T14:56:01.534888+00:00; 0.079 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-board-false-full-projection-r3` — 2026-10-08T14:56:01.615875+00:00; 0.082 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-board-false-full-projection-r4` — 2026-10-08T14:56:01.699880+00:00; 0.089 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-board-false-full-projection-r5` — 2026-10-08T14:56:01.789914+00:00; 0.074 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-board-null-count-r1` — 2026-10-08T14:56:01.867993+00:00; 0.570 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-board-null-count-r2` — 2026-10-08T14:56:02.439102+00:00; 0.609 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-board-null-count-r3` — 2026-10-08T14:56:03.049778+00:00; 0.594 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-board-null-count-r4` — 2026-10-08T14:56:03.645108+00:00; 0.593 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-board-null-count-r5` — 2026-10-08T14:56:04.239151+00:00; 0.575 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-board-null-ordered-ids-r1` — 2026-10-08T14:56:04.818366+00:00; 0.652 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-board-null-ordered-ids-r2` — 2026-10-08T14:56:05.472000+00:00; 0.668 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-board-null-ordered-ids-r3` — 2026-10-08T14:56:06.142000+00:00; 0.658 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-board-null-ordered-ids-r4` — 2026-10-08T14:56:06.802724+00:00; 0.667 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-board-null-ordered-ids-r5` — 2026-10-08T14:56:07.471192+00:00; 0.643 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-board-null-full-projection-r1` — 2026-10-08T14:56:08.119295+00:00; 0.081 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-board-null-full-projection-r2` — 2026-10-08T14:56:08.201790+00:00; 0.082 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-board-null-full-projection-r3` — 2026-10-08T14:56:08.286092+00:00; 0.076 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-board-null-full-projection-r4` — 2026-10-08T14:56:08.362401+00:00; 0.077 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-before-plans-01-plan-partner-board-null-full-projection-r5` — 2026-10-08T14:56:08.440058+00:00; 0.067 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `root-board-before-plans-controller-01` — 2026-10-08T14:55:49.165830+00:00; 19.350 s; exit 0; cwd `B`.

  ```text
  python3 tools/backend-read-plans.py --arm before --label board-before-plans-01 --selection B/private/board-before-select-01-selection.json --approval B/private/board-before-select-01-approval.json --runs 5 --local-isolated-window
  ```

- `board-after-physical-restore-container-guard` — 2026-10-08T14:56:39.901973+00:00; 0.020 s; exit 0; cwd `B`.

  ```text
  docker --context desktop-linux inspect --format '{{json .Name}}
  {{json .Config.Labels}}
  {{json .HostConfig.PortBindings}}
  {{json .Mounts}}' cleansia-audit-pg-20261007
  ```

- `board-after-physical-restore-clone-sql` — 2026-10-08T14:56:39.922539+00:00; 0.850 s; exit 0; cwd `B`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d postgres -v ON_ERROR_STOP=1
  ```

- `root-board-after-physical-restore` — 2026-10-08T14:56:39.874423+00:00; 0.916 s; exit 0; cwd `B`.

  ```text
  python3 tools/physical-fixture.py restore --label board-after-physical-restore
  ```

- `board-board-after-clone-qualification-board-after-clone-qualification` — 2026-10-08T14:56:48.649554+00:00; 3.773 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1 -v audit_local_fixture=1
  ```

- `root-board-after-clone-qualification` — 2026-10-08T14:56:48.601011+00:00; 3.837 s; exit 0; cwd `B`.

  ```text
  python3 tools/local-db.py sql --file B/tools/board-fixture-qualification.sql --output B/raw/backend/fixture-clone-after.json --label board-after-clone-qualification
  ```

- `root-board-after-qualified-host-start` — 2026-10-08T14:57:03.190832+00:00; 2.477 s; exit 0; cwd `B`.

  ```text
  python3 tools/backend-hosts.py start --arm after --hosting-timing --local-baseline --log-prefix board-after-qualified
  ```

- `root-board-after-qualified-auth` — 2026-10-08T14:57:36.676328+00:00; 7.882 s; exit 0; cwd `B`.

  ```text
  python3 tools/auth-requests.py bootstrap --rps 8 --label board-after-qualified-auth
  ```

- `mobile-board-before-selection-freeze-01` — 2026-10-08T14:57:48.158901+00:00; 0.021 s; exit 0; cwd `B`.

  ```text
  python3 -c '<inline Python; SHA256 aafc6e622d888d40956d1deb021315b43a5b007de0d801086490d00d421c51cb; exact argv in retained record>'
  ```

- `root-board-after-qualified-smoke-context` — 2026-10-08T14:57:49.043235+00:00; 0.004 s; exit 0; cwd `B`.

  ```text
  cp raw/backend/scenario-context.json raw/backend/board-after-smoke-001-context-snapshot.json
  ```

- `root-board-after-smoke-001` — 2026-10-08T14:57:49.130427+00:00; 24.753 s; exit 0; cwd `B`.

  ```text
  python3 tools/backend-requests.py smoke --arm after --phase diagnostic --label board-after-smoke-001
  ```

- `root-board-after-smoke-001-proof` — 2026-10-08T14:58:25.668058+00:00; 0.153 s; exit 0; cwd `B`.

  ```text
  python3 tools/backend-request-proof.py join --label board-after-smoke-001
  ```

- `root-board-smoke-paired-proof` — 2026-10-08T14:58:35.682628+00:00; 0.076 s; exit 0; cwd `B`.

  ```text
  python3 tools/backend-request-proof.py compare --before board-before-smoke-002 --after board-after-smoke-001 --output board-smoke-paired-proof
  ```

- `root-board-after-main-context` — 2026-10-08T14:58:35.844510+00:00; 0.003 s; exit 0; cwd `B`.

  ```text
  cp raw/backend/scenario-context.json raw/backend/board-after-main-001-context-snapshot.json
  ```

- `root-board-after-main-measure` — 2026-10-08T14:58:35.930780+00:00; 1227.732 s; exit 0; cwd `B`.

  ```text
  python3 tools/backend-requests.py measure --arm after --phase timing --label board-after-main-001 --runs 5 --samples 100 --warmup 10 --rps 8
  ```

- `root-board-after-main-proof` — 2026-10-08T15:19:17.754658+00:00; 3.682 s; exit 0; cwd `B`.

  ```text
  python3 tools/backend-request-proof.py join --label board-after-main-001
  ```

- `board-board-after-post-timing-fixture-board-after-post-timing-fixture` — 2026-10-08T15:19:17.890755+00:00; 3.856 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1 -v audit_local_fixture=1
  ```

- `root-board-after-post-timing-fixture` — 2026-10-08T15:19:17.754658+00:00; 4.011 s; exit 0; cwd `B`.

  ```text
  python3 tools/local-db.py sql --file B/tools/board-fixture-qualification.sql --output B/raw/backend/fixture-post-timing-after.json --label board-after-post-timing-fixture
  ```

- `root-board-main-paired-proof` — 2026-10-08T15:19:31.301817+00:00; 0.118 s; exit 0; cwd `B`.

  ```text
  python3 tools/backend-request-proof.py compare --before board-before-main-001 --after board-after-main-001 --output board-main-paired-proof
  ```

- `root-board-after-trace-auth-refresh` — 2026-10-08T15:19:31.487278+00:00; 6.135 s; exit 0; cwd `B`.

  ```text
  python3 tools/auth-requests.py refresh --rps 8 --label board-after-trace-auth
  ```

- `board-after-select-01-container-label` — 2026-10-08T15:20:30.256973+00:00; 0.045 s; exit 0; cwd `B`.

  ```text
  docker --context desktop-linux inspect --format '{{ index .Config.Labels "cleansia.audit" }}' cleansia-audit-pg-20261007
  ```

- `board-after-select-01-container-ports` — 2026-10-08T15:20:30.302437+00:00; 0.022 s; exit 0; cwd `B`.

  ```text
  docker --context desktop-linux inspect --format '{{json .NetworkSettings.Ports}}' cleansia-audit-pg-20261007
  ```

- `board-board-after-select-01-settings-before-sql-board-after-select-01-settings-before-sql` — 2026-10-08T15:20:30.371872+00:00; 0.068 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1 -v audit_local_fixture=1
  ```

- `board-after-select-01-settings-before` — 2026-10-08T15:20:30.324700+00:00; 0.127 s; exit 0; cwd `B`.

  ```text
  /opt/homebrew/opt/python@3.14/bin/python3.14 B/tools/local-db.py sql --file B/private/board-after-select-01-settings-before.sql --label board-after-select-01-settings-before-sql --output B/private/board-after-select-01-settings-before-result.log
  ```

- `board-after-select-01-normal-stop` — 2026-10-08T15:20:30.462303+00:00; 0.089 s; exit 0; cwd `B`.

  ```text
  /opt/homebrew/opt/python@3.14/bin/python3.14 B/tools/backend-hosts.py stop
  ```

- `board-board-after-select-01-enable-sql-board-after-select-01-enable-sql` — 2026-10-08T15:20:30.613912+00:00; 0.061 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1 -v audit_local_fixture=1
  ```

- `board-after-select-01-enable` — 2026-10-08T15:20:30.566676+00:00; 0.124 s; exit 0; cwd `B`.

  ```text
  /opt/homebrew/opt/python@3.14/bin/python3.14 B/tools/local-db.py sql --file B/tools/backend-pg-trace-on.sql --label board-after-select-01-enable-sql
  ```

- `board-board-after-select-01-settings-enabled-sql-board-after-select-01-settings-enabled-sql` — 2026-10-08T15:20:30.734788+00:00; 0.050 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1 -v audit_local_fixture=1
  ```

- `board-after-select-01-settings-enabled` — 2026-10-08T15:20:30.690839+00:00; 0.123 s; exit 0; cwd `B`.

  ```text
  /opt/homebrew/opt/python@3.14/bin/python3.14 B/tools/local-db.py sql --file B/private/board-after-select-01-settings-enabled.sql --label board-after-select-01-settings-enabled-sql --output B/private/board-after-select-01-settings-enabled-result.log
  ```

- `board-after-select-01-trace-start` — 2026-10-08T15:20:30.814539+00:00; 2.220 s; exit 0; cwd `B`.

  ```text
  /opt/homebrew/opt/python@3.14/bin/python3.14 B/tools/backend-hosts.py start --arm after --local-baseline --trace --log-prefix board-after-select-01-host
  ```

- `board-after-select-01-requests` — 2026-10-08T15:20:33.035508+00:00; 13.195 s; exit 0; cwd `B`.

  ```text
  /opt/homebrew/opt/python@3.14/bin/python3.14 B/tools/backend-requests.py measure --arm after --phase ef --runs 5 --samples 1 --warmup 0 --rps 8 --label board-after-select-01
  ```

- `board-board-after-select-01-settings-after-requests-sql-board-after-select-01-settings-after-requests-sql` — 2026-10-08T15:20:46.286825+00:00; 0.050 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1 -v audit_local_fixture=1
  ```

- `board-after-select-01-settings-after-requests` — 2026-10-08T15:20:46.231506+00:00; 0.131 s; exit 0; cwd `B`.

  ```text
  /opt/homebrew/opt/python@3.14/bin/python3.14 B/tools/local-db.py sql --file B/private/board-after-select-01-settings-after-requests.sql --label board-after-select-01-settings-after-requests-sql --output B/private/board-after-select-01-settings-after-requests-result.log
  ```

- `board-after-select-01-proof` — 2026-10-08T15:20:46.362936+00:00; 0.141 s; exit 0; cwd `B`.

  ```text
  /opt/homebrew/opt/python@3.14/bin/python3.14 B/tools/backend-request-proof.py join --label board-after-select-01
  ```

- `board-after-select-01-trace-stop` — 2026-10-08T15:20:46.514033+00:00; 0.082 s; exit 0; cwd `B`.

  ```text
  /opt/homebrew/opt/python@3.14/bin/python3.14 B/tools/backend-hosts.py stop
  ```

- `board-after-select-01-docker-log-capture` — 2026-10-08T15:20:46.611924+00:00; 0.145 s; exit 0; cwd `B`.

  ```text
  docker --context desktop-linux logs --since 2026-10-08T15:20:30.566623+00:00 --until 2026-10-08T15:20:46.611893+00:00 cleansia-audit-pg-20261007
  ```

- `board-board-after-select-01-disable-sql-board-after-select-01-disable-sql` — 2026-10-08T15:20:46.803376+00:00; 0.048 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1 -v audit_local_fixture=1
  ```

- `board-after-select-01-disable` — 2026-10-08T15:20:46.758092+00:00; 0.126 s; exit 0; cwd `B`.

  ```text
  /opt/homebrew/opt/python@3.14/bin/python3.14 B/tools/local-db.py sql --file B/tools/backend-pg-trace-off.sql --label board-after-select-01-disable-sql
  ```

- `board-board-after-select-01-settings-after-sql-board-after-select-01-settings-after-sql` — 2026-10-08T15:20:46.934807+00:00; 0.050 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1 -v audit_local_fixture=1
  ```

- `board-after-select-01-settings-after` — 2026-10-08T15:20:46.885058+00:00; 0.129 s; exit 0; cwd `B`.

  ```text
  /opt/homebrew/opt/python@3.14/bin/python3.14 B/tools/local-db.py sql --file B/private/board-after-select-01-settings-after.sql --label board-after-select-01-settings-after-sql --output B/private/board-after-select-01-settings-after-result.log
  ```

- `board-after-select-01-extract` — 2026-10-08T15:20:47.026113+00:00; 0.145 s; exit 0; cwd `B`.

  ```text
  /opt/homebrew/opt/python@3.14/bin/python3.14 B/tools/backend-pg-select-extract.py --arm after --capture-manifest B/raw/backend/board-after-select-01-capture-manifest.json --local-synthetic-log --full-bind-values-confirmed --input B/private/board-after-select-01-postgres.log --output B/private/board-after-select-01-sql-candidates.json
  ```

- `root-board-after-select-controller-01` — 2026-10-08T15:20:30.223253+00:00; 16.954 s; exit 0; cwd `B`.

  ```text
  python3 tools/backend-select-capture.py --arm after --local-isolated-window --label board-after-select-01
  ```

- `root-board-verification-master-fetch` — 2026-10-08T15:21:41.444098+00:00; 0.507 s; exit 0; cwd `W`.

  ```text
  git fetch origin master
  ```

- `root-board-verification-master-merge` — 2026-10-08T16:32:22.787385+00:00; 0.049 s; exit 0; cwd `W`.

  ```text
  git merge --no-edit origin/master
  ```

- `mobile-board-after-selected-shapes-inspect-01` — 2026-10-08T16:32:23.809550+00:00; 0.064 s; exit 0; cwd `B`.

  ```text
  python3 -c '<inline Python; SHA256 15c12bf3db95798a0ffd64355bf3e058c07c6430116bf82aa9f5736f63ab791e; exact argv in retained record>'
  ```

- `mobile-board-after-select-proposal-01` — 2026-10-08T16:32:40.431190+00:00; 0.308 s; exit 0; cwd `B`.

  ```text
  python3 tools/backend-select-propose.py --arm after --capture-label board-after-select-01 --output B/private/board-after-select-01-proposed-selection.json
  ```

- `root-board-verification-current-master-fetch` — 2026-10-08T16:33:22.737818+00:00; 0.734 s; exit 0; cwd `W`.

  ```text
  git fetch origin master
  ```

- `board-board-after-pre-plan-fixture-board-after-pre-plan-fixture` — 2026-10-08T16:33:23.753728+00:00; 3.687 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1 -v audit_local_fixture=1
  ```

- `root-board-after-pre-plan-fixture` — 2026-10-08T16:33:23.610753+00:00; 3.835 s; exit 0; cwd `B`.

  ```text
  python3 tools/local-db.py sql --file B/tools/board-fixture-qualification.sql --output B/raw/backend/fixture-pre-plan-after.json --label board-after-pre-plan-fixture
  ```

- `mobile-board-select-paired-shape-review-01` — 2026-10-08T16:34:10.978109+00:00; 0.219 s; exit 1; cwd `B`.

  ```text
  python3 tools/backend-select-pair-summary.py
  ```

- `mobile-board-after-paired-bind-diff-inspect-01` — 2026-10-08T16:34:44.239836+00:00; 0.017 s; exit 0; cwd `B`.

  ```text
  python3 -c '<inline Python; SHA256 c0f84225d962b5114b4935b956434c66279657f4ad1722d6c98a59884877da08; exact argv in retained record>'
  ```

- `mobile-board-select-paired-shape-review-02` — 2026-10-08T16:35:33.861411+00:00; 0.216 s; exit 0; cwd `B`.

  ```text
  python3 tools/backend-select-pair-summary.py
  ```

- `board-web-paired-evidence-read-001` — 2026-10-08T16:36:37.513555+00:00; 0.082 s; exit 1; cwd `B`.

  ```text
  python3 -c '<inline Python; SHA256 a62f233289f865a5de5c8b3b4c9a77943b105902d93aab360d0a7b59444dd4d5; exact argv in retained record>'
  ```

- `board-web-paired-evidence-read-002` — 2026-10-08T16:37:26.392993+00:00; 0.680 s; exit 1; cwd `B`.

  ```text
  python3 -c '<inline Python; SHA256 8082f603927d40635588adfe2f97cfc395cc8af658960c674d4715e5bd1a3d57; exact argv in retained record>'
  ```

- `board-web-paired-evidence-read-003` — 2026-10-08T16:37:49.427629+00:00; 2.098 s; exit 1; cwd `B`.

  ```text
  python3 -c '<inline Python; SHA256 d12bcfcd614d9410d787bc68de2945ea3b98b5c2c34c30b75c12a3c81be6216b; exact argv in retained record>'
  ```

- `root-board-arm-source-build-lineage` — 2026-10-08T16:39:46.832527+00:00; 1.941 s; exit 0; cwd `B`.

  ```text
  python3 tools/arm-source-identity.py
  ```

- `board-web-paired-evidence-read-004` — 2026-10-08T16:39:51.908245+00:00; 4.724 s; exit 0; cwd `B`.

  ```text
  python3 -c '<inline Python; SHA256 4f7176d2cdceb40f5a76e917aead75725d81b6f736001c2aa474fc1e1e56d313; exact argv in retained record>'
  ```

- `board-after-plans-01-container-label` — 2026-10-08T16:40:35.391580+00:00; 0.137 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux inspect --format '{{ index .Config.Labels "cleansia.audit" }}' cleansia-audit-pg-20261007
  ```

- `board-after-plans-01-container-ports` — 2026-10-08T16:40:35.529331+00:00; 0.024 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux inspect --format '{{json .NetworkSettings.Ports}}' cleansia-audit-pg-20261007
  ```

- `board-after-plans-01-plan-admin-orders-ordered-ids-r1` — 2026-10-08T16:40:35.554236+00:00; 0.129 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-admin-orders-ordered-ids-r2` — 2026-10-08T16:40:35.683631+00:00; 0.073 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-admin-orders-ordered-ids-r3` — 2026-10-08T16:40:35.757287+00:00; 0.073 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-admin-orders-ordered-ids-r4` — 2026-10-08T16:40:35.831436+00:00; 0.073 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-admin-orders-ordered-ids-r5` — 2026-10-08T16:40:35.905422+00:00; 0.074 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-preview-preview-projection-r1` — 2026-10-08T16:40:35.983303+00:00; 0.134 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-preview-preview-projection-r2` — 2026-10-08T16:40:36.119388+00:00; 0.128 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-preview-preview-projection-r3` — 2026-10-08T16:40:36.248806+00:00; 0.141 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-preview-preview-projection-r4` — 2026-10-08T16:40:36.392044+00:00; 0.126 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-preview-preview-projection-r5` — 2026-10-08T16:40:36.519850+00:00; 0.132 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-board-count-r1` — 2026-10-08T16:40:36.655649+00:00; 0.142 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-board-count-r2` — 2026-10-08T16:40:36.800295+00:00; 0.139 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-board-count-r3` — 2026-10-08T16:40:36.941923+00:00; 0.143 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-board-count-r4` — 2026-10-08T16:40:37.086266+00:00; 0.128 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-board-count-r5` — 2026-10-08T16:40:37.215846+00:00; 0.136 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-board-ordered-ids-r1` — 2026-10-08T16:40:37.354916+00:00; 0.146 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-board-ordered-ids-r2` — 2026-10-08T16:40:37.502104+00:00; 0.143 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-board-ordered-ids-r3` — 2026-10-08T16:40:37.647201+00:00; 0.137 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-board-ordered-ids-r4` — 2026-10-08T16:40:37.786712+00:00; 0.140 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-board-ordered-ids-r5` — 2026-10-08T16:40:37.929087+00:00; 0.134 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-board-full-projection-r1` — 2026-10-08T16:40:38.066860+00:00; 0.091 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-board-full-projection-r2` — 2026-10-08T16:40:38.159768+00:00; 0.086 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-board-full-projection-r3` — 2026-10-08T16:40:38.248064+00:00; 0.074 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-board-full-projection-r4` — 2026-10-08T16:40:38.323610+00:00; 0.075 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-board-full-projection-r5` — 2026-10-08T16:40:38.400417+00:00; 0.075 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-board-false-count-r1` — 2026-10-08T16:40:38.478684+00:00; 0.603 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-board-false-count-r2` — 2026-10-08T16:40:39.083032+00:00; 0.606 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-board-false-count-r3` — 2026-10-08T16:40:39.691105+00:00; 0.651 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-board-false-count-r4` — 2026-10-08T16:40:40.342612+00:00; 0.698 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-board-false-count-r5` — 2026-10-08T16:40:41.042865+00:00; 0.769 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-board-false-ordered-ids-r1` — 2026-10-08T16:40:41.817876+00:00; 0.831 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-board-false-ordered-ids-r2` — 2026-10-08T16:40:42.650766+00:00; 0.934 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-board-false-ordered-ids-r3` — 2026-10-08T16:40:43.586943+00:00; 0.868 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-board-false-ordered-ids-r4` — 2026-10-08T16:40:44.456665+00:00; 0.817 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-board-false-ordered-ids-r5` — 2026-10-08T16:40:45.274707+00:00; 0.789 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-board-false-full-projection-r1` — 2026-10-08T16:40:46.067469+00:00; 0.086 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-board-false-full-projection-r2` — 2026-10-08T16:40:46.155266+00:00; 0.078 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-board-false-full-projection-r3` — 2026-10-08T16:40:46.234577+00:00; 0.081 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-board-false-full-projection-r4` — 2026-10-08T16:40:46.317440+00:00; 0.079 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-board-false-full-projection-r5` — 2026-10-08T16:40:46.398320+00:00; 0.081 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-board-null-count-r1` — 2026-10-08T16:40:46.483321+00:00; 0.644 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-board-null-count-r2` — 2026-10-08T16:40:47.129096+00:00; 0.710 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-board-null-count-r3` — 2026-10-08T16:40:47.841032+00:00; 0.707 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-board-null-count-r4` — 2026-10-08T16:40:48.549531+00:00; 0.708 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-board-null-count-r5` — 2026-10-08T16:40:49.258936+00:00; 0.755 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-board-null-ordered-ids-r1` — 2026-10-08T16:40:50.018265+00:00; 0.812 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-board-null-ordered-ids-r2` — 2026-10-08T16:40:50.832122+00:00; 0.801 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-board-null-ordered-ids-r3` — 2026-10-08T16:40:51.635444+00:00; 0.745 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-board-null-ordered-ids-r4` — 2026-10-08T16:40:52.382993+00:00; 0.761 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-board-null-ordered-ids-r5` — 2026-10-08T16:40:53.145718+00:00; 0.750 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-board-null-full-projection-r1` — 2026-10-08T16:40:53.901459+00:00; 0.077 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-board-null-full-projection-r2` — 2026-10-08T16:40:53.979640+00:00; 0.071 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-board-null-full-projection-r3` — 2026-10-08T16:40:54.052552+00:00; 0.077 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-board-null-full-projection-r4` — 2026-10-08T16:40:54.131684+00:00; 0.087 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `board-after-plans-01-plan-partner-board-null-full-projection-r5` — 2026-10-08T16:40:54.220390+00:00; 0.078 s; exit 0; cwd `unspecified in retained record`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d CleansiaAudit -v ON_ERROR_STOP=1
  ```

- `root-board-after-plans-controller-01` — 2026-10-08T16:40:35.260111+00:00; 19.056 s; exit 0; cwd `B`.

  ```text
  python3 tools/backend-read-plans.py --arm after --label board-after-plans-01 --selection B/private/board-after-select-01-selection.json --approval B/private/board-after-select-01-approval.json --runs 5 --local-isolated-window
  ```

- `root-board-current-master-final-merge` — 2026-10-08T16:42:37.184325+00:00; 0.021 s; exit 0; cwd `W`.

  ```text
  git merge --no-edit origin/master
  ```

- `board-cleanup-physical-template-container-guard` — 2026-10-08T16:42:37.376959+00:00; 0.034 s; exit 0; cwd `B`.

  ```text
  docker --context desktop-linux inspect --format '{{json .Name}}
  {{json .Config.Labels}}
  {{json .HostConfig.PortBindings}}
  {{json .Mounts}}' cleansia-audit-pg-20261007
  ```

- `board-cleanup-physical-template-clone-sql` — 2026-10-08T16:42:37.412334+00:00; 0.093 s; exit 0; cwd `B`.

  ```text
  docker --context desktop-linux exec -i cleansia-audit-pg-20261007 psql -X -U cleansia_audit -d postgres -v ON_ERROR_STOP=1
  ```

- `root-board-cleanup-physical-template` — 2026-10-08T16:42:37.349125+00:00; 0.160 s; exit 0; cwd `B`.

  ```text
  python3 tools/physical-fixture.py cleanup --label board-cleanup-physical-template
  ```

- `root-board-stop-owned-postgres` — 2026-10-08T16:43:19.368812+00:00; 0.726 s; exit 0; cwd `B`.

  ```text
  python3 tools/stop-owned-postgres.py
  ```

- `root-board-checker-dependency-link` — 2026-10-08T16:44:01.929438+00:00; 0.014 s; exit 0; cwd `W`.

  ```text
  ln -s V/source/src/Cleansia.App/node_modules node_modules
  ```

- `root-board-check-available-status-parity` — 2026-10-08T16:44:02.372178+00:00; 0.261 s; exit 0; cwd `W`.

  ```text
  A/tools/web-tooling/node_modules/node/bin/node agents/tools/check-available-status-parity.mjs
  ```

- `root-board-check-module-boundaries` — 2026-10-08T16:44:02.749207+00:00; 19.241 s; exit 0; cwd `W`.

  ```text
  A/tools/web-tooling/node_modules/node/bin/node agents/tools/check-module-boundaries.mjs
  ```

- `root-board-check-nx-project-registration` — 2026-10-08T16:44:22.078561+00:00; 0.153 s; exit 0; cwd `W`.

  ```text
  A/tools/web-tooling/node_modules/node/bin/node agents/tools/check-nx-project-registration.mjs
  ```

- `root-board-check-booking-policy-parity` — 2026-10-08T16:44:22.301264+00:00; 0.265 s; exit 0; cwd `W`.

  ```text
  A/tools/web-tooling/node_modules/node/bin/node agents/tools/check-booking-policy-parity.mjs
  ```

- `root-board-check-catalog-claims` — 2026-10-08T16:44:22.635264+00:00; 0.539 s; exit 0; cwd `W`.

  ```text
  A/tools/web-tooling/node_modules/node/bin/node agents/tools/check-catalog-claims.mjs
  ```

- `root-board-check-backlog-consistency` — 2026-10-08T16:44:23.233422+00:00; 0.062 s; exit 0; cwd `W`.

  ```text
  A/tools/web-tooling/node_modules/node/bin/node agents/tools/check-backlog-consistency.mjs
  ```

- `root-board-check-consistency` — 2026-10-08T16:44:23.353534+00:00; 0.683 s; exit 0; cwd `W`.

  ```text
  A/tools/web-tooling/node_modules/node/bin/node agents/tools/check-consistency.mjs
  ```

- `root-board-check-docs-refs` — 2026-10-08T16:44:24.094946+00:00; 0.806 s; exit 0; cwd `W`.

  ```text
  A/tools/web-tooling/node_modules/node/bin/node agents/tools/check-docs-refs.mjs
  ```

- `root-board-check-ios-symbols` — 2026-10-08T16:44:24.959889+00:00; 1.368 s; exit 0; cwd `W`.

  ```text
  A/tools/web-tooling/node_modules/node/bin/node agents/tools/check-ios-symbols.mjs
  ```

- `root-board-checker-self-tests` — 2026-10-08T16:44:26.395720+00:00; 11.816 s; exit 0; cwd `W`.

  ```text
  A/tools/web-tooling/node_modules/node/bin/node --test agents/tools/check-available-status-parity.test.mjs agents/tools/check-module-boundaries.test.mjs agents/tools/check-nx-project-registration.test.mjs agents/tools/check-booking-policy-parity.test.mjs agents/tools/check-catalog-claims.test.mjs agents/tools/check-consistency.test.mjs agents/tools/check-docs-refs.test.mjs agents/tools/check-ios-symbols.test.mjs
  ```

- `board-backend-verification-final-001-unit` — 2026-10-08T16:42:48.184466+00:00; 115.937 s; exit 0; cwd `B/source-after/src`.

  ```text
  dotnet test Cleansia.Tests/Cleansia.Tests.csproj -c Release --no-build --no-restore --logger 'trx;LogFileName=unit.trx' --results-directory B/raw/backend/tests/final-001/unit -- xUnit.parallelizeTestCollections=false
  ```

- `root-board-docs-build` — 2026-10-08T16:44:38.247724+00:00; 26.335 s; exit 0; cwd `W/docs`.

  ```text
  A/tools/web-tooling/node_modules/node/bin/node node_modules/vitepress/bin/vitepress.js build
  ```

- `root-board-repo-verification` — 2026-10-08T16:44:02.226633+00:00; 62.377 s; exit 0; cwd `B`.

  ```text
  python3 tools/verify-repo.py
  ```

- `root-board-paired-plan-mechanism` — 2026-10-08T16:46:33.254010+00:00; 0.106 s; exit 0; cwd `B`.

  ```text
  python3 tools/plan-mechanism.py
  ```

- `board-web-final-evidence-receipt-001` — 2026-10-08T16:46:33.666155+00:00; 0.054 s; exit 0; cwd `B`.

  ```text
  python3 -c '<inline Python; SHA256 b6f33fae708e9652bf2b72bf310f9d81f1fa6eb590e820cb742735136c8402e5; exact argv in retained record>'
  ```

- `board-web-final-evidence-receipt-polish-001` — 2026-10-08T16:47:42.419459+00:00; 0.030 s; exit 0; cwd `B`.

  ```text
  python3 -c '<inline Python; SHA256 0114cc85b79ea664fa46b52c3f42797a31cd79b60b05c682e6ff1eb7d2b34d4a; exact argv in retained record>'
  ```

- `root-board-graph-rebuild` — 2026-10-08T16:44:52.068222+00:00; 235.513 s; exit 0; cwd `W`.

  ```text
  '/Users/michael/Library/Application Support/pipx/venvs/graphifyy/bin/python' B/tools/rebuild-repo-graph.py
  ```

- `root-board-graph-runner-preimage` — 2026-10-08T16:49:21.197244+00:00; 0.005 s; exit 0; cwd `B`.

  ```text
  cp tools/rebuild-repo-graph.py raw/graph-runner-first.py
  ```

- `root-board-checker-dependency-unlink` — 2026-10-08T16:50:02.535611+00:00; 0.005 s; exit 0; cwd `W`.

  ```text
  unlink node_modules
  ```

- `board-testcontainers-identity-inventory-001` — 2026-10-08T16:50:45.732449+00:00; 0.110 s; exit 0; cwd `B`.

  ```text
  docker --host unix:///Users/michael/.docker/run/docker.sock ps --filter label=org.testcontainers=true --format '{{.ID}}\t{{.Image}}\t{{.CreatedAt}}\t{{.Label "org.testcontainers.session-id"}}\t{{.Status}}'
  ```

- `board-backend-verification-final-001-integration` — 2026-10-08T16:44:44.275406+00:00; 449.035 s; exit 0; cwd `B/source-after/src`.

  ```text
  dotnet test Cleansia.IntegrationTests/Cleansia.IntegrationTests.csproj -c Release --no-build --no-restore --logger 'trx;LogFileName=integration.trx' --results-directory B/raw/backend/tests/final-001/integration -- xUnit.parallelizeTestCollections=false
  ```

- `root-board-graph-rebuild-corrected` — 2026-10-08T16:50:02.656776+00:00; 278.110 s; exit 0; cwd `W`.

  ```text
  env GRAPHIFY_MAX_WORKERS=1 '/Users/michael/Library/Application Support/pipx/venvs/graphifyy/bin/python' B/tools/rebuild-repo-graph.py
  ```

- `board-backend-verification-final-001-host` — 2026-10-08T16:52:13.456231+00:00; 244.433 s; exit 0; cwd `B/source-after/src`.

  ```text
  dotnet test Cleansia.HostTests/Cleansia.HostTests.csproj -c Release --no-build --no-restore --logger 'trx;LogFileName=host.trx' --results-directory B/raw/backend/tests/final-001/host -- xUnit.parallelizeTestCollections=false
  ```

- `board-backend-full-verification-final-001` — 2026-10-08T16:42:48.109431+00:00; 809.860 s; exit 0; cwd `B`.

  ```text
  python3 B/tools/backend-full-verification.py --id final-001
  ```

- `root-board-complete-local-report` — 2026-10-08T16:59:35.813708+00:00; 0.113 s; exit 1; cwd `W`.

  ```text
  python3 B/tools/complete-report.py
  ```

- `root-board-final-fetch-existing-credential` — 2026-10-08T16:59:35.974326+00:00; 0.033 s; exit 0; cwd `W`.

  ```text
  git -C W credential fill
  ```

- `mobile-board-corrected-graph-validation-01` — 2026-10-08T16:59:35.427043+00:00; 0.862 s; exit 0; cwd `W`.

  ```text
  python3 B/tools/validate-corrected-board-graph.py
  ```

- `root-board-final-fetch` — 2026-10-08T16:59:36.036726+00:00; 0.565 s; exit 0; cwd `W`.

  ```text
  gh api repos/VM-s-Solutions/cleansia/branches/master --jq .commit.sha
  ```

- `root-board-complete-local-report-02` — 2026-10-08T16:59:55.210626+00:00; 0.090 s; exit 1; cwd `W`.

  ```text
  python3 B/tools/complete-report.py
  ```


## Private command input provenance

The110 read-only plan commands consume the original SQL/bind input through stdin;
its private filename and digest are recorded below. Other retained stdin references
are preserved here as well. Values are kept outside the repository.

| Command label | Private stdin relative to scratch | SHA256 |
|---|---|---|
| `board-reference-before-reference-before` | `private/backend-reference.dump` | `2ad912b05bcfc4e40fcc22d6a1ede41a75fb1b94336b0aeb20fff267408b7d03` |
| `board-fresh-primary-fresh-primary` | `tools/backend-fixture-primary.sql` | `5cd4f21571f18d78676e749270715d597b6672458755a8ac240aced36c5e23dc` |
| `board-fixture-before-fixture-before` | `tools/board-fixture-qualification.sql` | `d88133d9b19b9d38bb558ea27d5a51581beb16127928be46175c2084df349758` |
| `board-context-context` | `tools/backend-context.sql` | `5693ecdbd23d55f167e5532cba4c83e9e5d02c8fd89c539329ed1ef765b4bd7a` |
| `board-assignment-map-v2-assignment-map-v2` | `tools/board-assignment-map.sql` | `a68f0bb925dac7d0ca8d15434d3e2b0f6cced9eb78654987b982a92df9955a7c` |
| `board-clone-before-clone-before` | `tools/board-fixture-qualification.sql` | `d88133d9b19b9d38bb558ea27d5a51581beb16127928be46175c2084df349758` |
| `board-board-before-post-timing-fixture-board-before-post-timing-fixture` | `tools/board-fixture-qualification.sql` | `d88133d9b19b9d38bb558ea27d5a51581beb16127928be46175c2084df349758` |
| `board-board-before-select-01-settings-before-sql-board-before-select-01-settings-before-sql` | `private/board-before-select-01-settings-before.sql` | `2ba5e6311fe526b88f9e0835d7187616d014118d2ce31a3e1912f223ceefd929` |
| `board-board-before-select-01-enable-sql-board-before-select-01-enable-sql` | `tools/backend-pg-trace-on.sql` | `01c4b7b53b23e9b0216d425e0a6901ccc37b1822556ff360faf1bc75b7509f64` |
| `board-board-before-select-01-settings-enabled-sql-board-before-select-01-settings-enabled-sql` | `private/board-before-select-01-settings-enabled.sql` | `2ba5e6311fe526b88f9e0835d7187616d014118d2ce31a3e1912f223ceefd929` |
| `board-board-before-select-01-settings-after-requests-sql-board-before-select-01-settings-after-requests-sql` | `private/board-before-select-01-settings-after-requests.sql` | `2ba5e6311fe526b88f9e0835d7187616d014118d2ce31a3e1912f223ceefd929` |
| `board-board-before-select-01-disable-sql-board-before-select-01-disable-sql` | `tools/backend-pg-trace-off.sql` | `a563513cae548ac7f54e3b7ddf83440393d6234e464cad68c650661c6ea5bf98` |
| `board-board-before-select-01-settings-after-sql-board-before-select-01-settings-after-sql` | `private/board-before-select-01-settings-after.sql` | `2ba5e6311fe526b88f9e0835d7187616d014118d2ce31a3e1912f223ceefd929` |
| `board-before-plans-01-plan-admin-orders-ordered-ids-r1` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-admin-orders-ordered-ids-r1.sql` | `c290696bdfef956a6d9cfb16cd4bdfa8b47b996ded585bb10ffc6dfbf78c37da` |
| `board-before-plans-01-plan-admin-orders-ordered-ids-r2` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-admin-orders-ordered-ids-r2.sql` | `c290696bdfef956a6d9cfb16cd4bdfa8b47b996ded585bb10ffc6dfbf78c37da` |
| `board-before-plans-01-plan-admin-orders-ordered-ids-r3` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-admin-orders-ordered-ids-r3.sql` | `c290696bdfef956a6d9cfb16cd4bdfa8b47b996ded585bb10ffc6dfbf78c37da` |
| `board-before-plans-01-plan-admin-orders-ordered-ids-r4` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-admin-orders-ordered-ids-r4.sql` | `c290696bdfef956a6d9cfb16cd4bdfa8b47b996ded585bb10ffc6dfbf78c37da` |
| `board-before-plans-01-plan-admin-orders-ordered-ids-r5` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-admin-orders-ordered-ids-r5.sql` | `c290696bdfef956a6d9cfb16cd4bdfa8b47b996ded585bb10ffc6dfbf78c37da` |
| `board-before-plans-01-plan-partner-preview-preview-projection-r1` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-preview-preview-projection-r1.sql` | `e828a2405ab07da79050a57cdc3b11b6a694c07fdecbe85abeabf942436b8101` |
| `board-before-plans-01-plan-partner-preview-preview-projection-r2` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-preview-preview-projection-r2.sql` | `e828a2405ab07da79050a57cdc3b11b6a694c07fdecbe85abeabf942436b8101` |
| `board-before-plans-01-plan-partner-preview-preview-projection-r3` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-preview-preview-projection-r3.sql` | `e828a2405ab07da79050a57cdc3b11b6a694c07fdecbe85abeabf942436b8101` |
| `board-before-plans-01-plan-partner-preview-preview-projection-r4` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-preview-preview-projection-r4.sql` | `e828a2405ab07da79050a57cdc3b11b6a694c07fdecbe85abeabf942436b8101` |
| `board-before-plans-01-plan-partner-preview-preview-projection-r5` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-preview-preview-projection-r5.sql` | `e828a2405ab07da79050a57cdc3b11b6a694c07fdecbe85abeabf942436b8101` |
| `board-before-plans-01-plan-partner-board-count-r1` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-board-count-r1.sql` | `d783178163ff1bfad732f9d2c7bc1888ee546c480c9b5ca14a597687689a173a` |
| `board-before-plans-01-plan-partner-board-count-r2` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-board-count-r2.sql` | `d783178163ff1bfad732f9d2c7bc1888ee546c480c9b5ca14a597687689a173a` |
| `board-before-plans-01-plan-partner-board-count-r3` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-board-count-r3.sql` | `d783178163ff1bfad732f9d2c7bc1888ee546c480c9b5ca14a597687689a173a` |
| `board-before-plans-01-plan-partner-board-count-r4` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-board-count-r4.sql` | `d783178163ff1bfad732f9d2c7bc1888ee546c480c9b5ca14a597687689a173a` |
| `board-before-plans-01-plan-partner-board-count-r5` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-board-count-r5.sql` | `d783178163ff1bfad732f9d2c7bc1888ee546c480c9b5ca14a597687689a173a` |
| `board-before-plans-01-plan-partner-board-ordered-ids-r1` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-board-ordered-ids-r1.sql` | `4752d82475c7538f230e412263405fa93d3969e7d313847129b144f5880639f2` |
| `board-before-plans-01-plan-partner-board-ordered-ids-r2` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-board-ordered-ids-r2.sql` | `4752d82475c7538f230e412263405fa93d3969e7d313847129b144f5880639f2` |
| `board-before-plans-01-plan-partner-board-ordered-ids-r3` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-board-ordered-ids-r3.sql` | `4752d82475c7538f230e412263405fa93d3969e7d313847129b144f5880639f2` |
| `board-before-plans-01-plan-partner-board-ordered-ids-r4` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-board-ordered-ids-r4.sql` | `4752d82475c7538f230e412263405fa93d3969e7d313847129b144f5880639f2` |
| `board-before-plans-01-plan-partner-board-ordered-ids-r5` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-board-ordered-ids-r5.sql` | `4752d82475c7538f230e412263405fa93d3969e7d313847129b144f5880639f2` |
| `board-before-plans-01-plan-partner-board-full-projection-r1` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-board-full-projection-r1.sql` | `f9bce7a8696884bd911084220a80e5e750d04f3a0be8c8b9e118483e8483dab7` |
| `board-before-plans-01-plan-partner-board-full-projection-r2` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-board-full-projection-r2.sql` | `f9bce7a8696884bd911084220a80e5e750d04f3a0be8c8b9e118483e8483dab7` |
| `board-before-plans-01-plan-partner-board-full-projection-r3` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-board-full-projection-r3.sql` | `f9bce7a8696884bd911084220a80e5e750d04f3a0be8c8b9e118483e8483dab7` |
| `board-before-plans-01-plan-partner-board-full-projection-r4` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-board-full-projection-r4.sql` | `f9bce7a8696884bd911084220a80e5e750d04f3a0be8c8b9e118483e8483dab7` |
| `board-before-plans-01-plan-partner-board-full-projection-r5` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-board-full-projection-r5.sql` | `f9bce7a8696884bd911084220a80e5e750d04f3a0be8c8b9e118483e8483dab7` |
| `board-before-plans-01-plan-partner-board-false-count-r1` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-board-false-count-r1.sql` | `c2d6f15afb15a24e745ecfff52d39a74909477d09bcbc9dfd0c0b90cd5019d03` |
| `board-before-plans-01-plan-partner-board-false-count-r2` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-board-false-count-r2.sql` | `c2d6f15afb15a24e745ecfff52d39a74909477d09bcbc9dfd0c0b90cd5019d03` |
| `board-before-plans-01-plan-partner-board-false-count-r3` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-board-false-count-r3.sql` | `c2d6f15afb15a24e745ecfff52d39a74909477d09bcbc9dfd0c0b90cd5019d03` |
| `board-before-plans-01-plan-partner-board-false-count-r4` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-board-false-count-r4.sql` | `c2d6f15afb15a24e745ecfff52d39a74909477d09bcbc9dfd0c0b90cd5019d03` |
| `board-before-plans-01-plan-partner-board-false-count-r5` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-board-false-count-r5.sql` | `c2d6f15afb15a24e745ecfff52d39a74909477d09bcbc9dfd0c0b90cd5019d03` |
| `board-before-plans-01-plan-partner-board-false-ordered-ids-r1` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-board-false-ordered-ids-r1.sql` | `4624628ff3dd082b08c7d12110825338fcfb9ef57c76d180281ff6a60d5bde4c` |
| `board-before-plans-01-plan-partner-board-false-ordered-ids-r2` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-board-false-ordered-ids-r2.sql` | `4624628ff3dd082b08c7d12110825338fcfb9ef57c76d180281ff6a60d5bde4c` |
| `board-before-plans-01-plan-partner-board-false-ordered-ids-r3` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-board-false-ordered-ids-r3.sql` | `4624628ff3dd082b08c7d12110825338fcfb9ef57c76d180281ff6a60d5bde4c` |
| `board-before-plans-01-plan-partner-board-false-ordered-ids-r4` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-board-false-ordered-ids-r4.sql` | `4624628ff3dd082b08c7d12110825338fcfb9ef57c76d180281ff6a60d5bde4c` |
| `board-before-plans-01-plan-partner-board-false-ordered-ids-r5` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-board-false-ordered-ids-r5.sql` | `4624628ff3dd082b08c7d12110825338fcfb9ef57c76d180281ff6a60d5bde4c` |
| `board-before-plans-01-plan-partner-board-false-full-projection-r1` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-board-false-full-projection-r1.sql` | `8a3804704ff5927515dd0522b7524c80e0884db70935e03d274004d77a0b922d` |
| `board-before-plans-01-plan-partner-board-false-full-projection-r2` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-board-false-full-projection-r2.sql` | `8a3804704ff5927515dd0522b7524c80e0884db70935e03d274004d77a0b922d` |
| `board-before-plans-01-plan-partner-board-false-full-projection-r3` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-board-false-full-projection-r3.sql` | `8a3804704ff5927515dd0522b7524c80e0884db70935e03d274004d77a0b922d` |
| `board-before-plans-01-plan-partner-board-false-full-projection-r4` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-board-false-full-projection-r4.sql` | `8a3804704ff5927515dd0522b7524c80e0884db70935e03d274004d77a0b922d` |
| `board-before-plans-01-plan-partner-board-false-full-projection-r5` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-board-false-full-projection-r5.sql` | `8a3804704ff5927515dd0522b7524c80e0884db70935e03d274004d77a0b922d` |
| `board-before-plans-01-plan-partner-board-null-count-r1` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-board-null-count-r1.sql` | `84a4c90ac5aafba9ad487c8a7f529ebe167338a9735f0ba19d2ef7022b133e46` |
| `board-before-plans-01-plan-partner-board-null-count-r2` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-board-null-count-r2.sql` | `84a4c90ac5aafba9ad487c8a7f529ebe167338a9735f0ba19d2ef7022b133e46` |
| `board-before-plans-01-plan-partner-board-null-count-r3` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-board-null-count-r3.sql` | `84a4c90ac5aafba9ad487c8a7f529ebe167338a9735f0ba19d2ef7022b133e46` |
| `board-before-plans-01-plan-partner-board-null-count-r4` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-board-null-count-r4.sql` | `84a4c90ac5aafba9ad487c8a7f529ebe167338a9735f0ba19d2ef7022b133e46` |
| `board-before-plans-01-plan-partner-board-null-count-r5` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-board-null-count-r5.sql` | `84a4c90ac5aafba9ad487c8a7f529ebe167338a9735f0ba19d2ef7022b133e46` |
| `board-before-plans-01-plan-partner-board-null-ordered-ids-r1` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-board-null-ordered-ids-r1.sql` | `13b1d5a48ade1f629fdb4907aaf492e2a95f586d5d6ed6b1681f70f28e0324ed` |
| `board-before-plans-01-plan-partner-board-null-ordered-ids-r2` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-board-null-ordered-ids-r2.sql` | `13b1d5a48ade1f629fdb4907aaf492e2a95f586d5d6ed6b1681f70f28e0324ed` |
| `board-before-plans-01-plan-partner-board-null-ordered-ids-r3` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-board-null-ordered-ids-r3.sql` | `13b1d5a48ade1f629fdb4907aaf492e2a95f586d5d6ed6b1681f70f28e0324ed` |
| `board-before-plans-01-plan-partner-board-null-ordered-ids-r4` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-board-null-ordered-ids-r4.sql` | `13b1d5a48ade1f629fdb4907aaf492e2a95f586d5d6ed6b1681f70f28e0324ed` |
| `board-before-plans-01-plan-partner-board-null-ordered-ids-r5` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-board-null-ordered-ids-r5.sql` | `13b1d5a48ade1f629fdb4907aaf492e2a95f586d5d6ed6b1681f70f28e0324ed` |
| `board-before-plans-01-plan-partner-board-null-full-projection-r1` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-board-null-full-projection-r1.sql` | `38165700cf784cfd32a75c8b353366a671129838e00f8ec3e6c7e00ef9e5dae6` |
| `board-before-plans-01-plan-partner-board-null-full-projection-r2` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-board-null-full-projection-r2.sql` | `38165700cf784cfd32a75c8b353366a671129838e00f8ec3e6c7e00ef9e5dae6` |
| `board-before-plans-01-plan-partner-board-null-full-projection-r3` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-board-null-full-projection-r3.sql` | `38165700cf784cfd32a75c8b353366a671129838e00f8ec3e6c7e00ef9e5dae6` |
| `board-before-plans-01-plan-partner-board-null-full-projection-r4` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-board-null-full-projection-r4.sql` | `38165700cf784cfd32a75c8b353366a671129838e00f8ec3e6c7e00ef9e5dae6` |
| `board-before-plans-01-plan-partner-board-null-full-projection-r5` | `private/board-before-plans-01-select-plans/board-before-plans-01-plan-partner-board-null-full-projection-r5.sql` | `38165700cf784cfd32a75c8b353366a671129838e00f8ec3e6c7e00ef9e5dae6` |
| `board-board-after-clone-qualification-board-after-clone-qualification` | `tools/board-fixture-qualification.sql` | `d88133d9b19b9d38bb558ea27d5a51581beb16127928be46175c2084df349758` |
| `board-board-after-post-timing-fixture-board-after-post-timing-fixture` | `tools/board-fixture-qualification.sql` | `d88133d9b19b9d38bb558ea27d5a51581beb16127928be46175c2084df349758` |
| `board-board-after-select-01-settings-before-sql-board-after-select-01-settings-before-sql` | `private/board-after-select-01-settings-before.sql` | `2ba5e6311fe526b88f9e0835d7187616d014118d2ce31a3e1912f223ceefd929` |
| `board-board-after-select-01-enable-sql-board-after-select-01-enable-sql` | `tools/backend-pg-trace-on.sql` | `01c4b7b53b23e9b0216d425e0a6901ccc37b1822556ff360faf1bc75b7509f64` |
| `board-board-after-select-01-settings-enabled-sql-board-after-select-01-settings-enabled-sql` | `private/board-after-select-01-settings-enabled.sql` | `2ba5e6311fe526b88f9e0835d7187616d014118d2ce31a3e1912f223ceefd929` |
| `board-board-after-select-01-settings-after-requests-sql-board-after-select-01-settings-after-requests-sql` | `private/board-after-select-01-settings-after-requests.sql` | `2ba5e6311fe526b88f9e0835d7187616d014118d2ce31a3e1912f223ceefd929` |
| `board-board-after-select-01-disable-sql-board-after-select-01-disable-sql` | `tools/backend-pg-trace-off.sql` | `a563513cae548ac7f54e3b7ddf83440393d6234e464cad68c650661c6ea5bf98` |
| `board-board-after-select-01-settings-after-sql-board-after-select-01-settings-after-sql` | `private/board-after-select-01-settings-after.sql` | `2ba5e6311fe526b88f9e0835d7187616d014118d2ce31a3e1912f223ceefd929` |
| `board-board-after-pre-plan-fixture-board-after-pre-plan-fixture` | `tools/board-fixture-qualification.sql` | `d88133d9b19b9d38bb558ea27d5a51581beb16127928be46175c2084df349758` |
| `board-after-plans-01-plan-admin-orders-ordered-ids-r1` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-admin-orders-ordered-ids-r1.sql` | `c290696bdfef956a6d9cfb16cd4bdfa8b47b996ded585bb10ffc6dfbf78c37da` |
| `board-after-plans-01-plan-admin-orders-ordered-ids-r2` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-admin-orders-ordered-ids-r2.sql` | `c290696bdfef956a6d9cfb16cd4bdfa8b47b996ded585bb10ffc6dfbf78c37da` |
| `board-after-plans-01-plan-admin-orders-ordered-ids-r3` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-admin-orders-ordered-ids-r3.sql` | `c290696bdfef956a6d9cfb16cd4bdfa8b47b996ded585bb10ffc6dfbf78c37da` |
| `board-after-plans-01-plan-admin-orders-ordered-ids-r4` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-admin-orders-ordered-ids-r4.sql` | `c290696bdfef956a6d9cfb16cd4bdfa8b47b996ded585bb10ffc6dfbf78c37da` |
| `board-after-plans-01-plan-admin-orders-ordered-ids-r5` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-admin-orders-ordered-ids-r5.sql` | `c290696bdfef956a6d9cfb16cd4bdfa8b47b996ded585bb10ffc6dfbf78c37da` |
| `board-after-plans-01-plan-partner-preview-preview-projection-r1` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-preview-preview-projection-r1.sql` | `21ea054f88c5a8411d7ec7ec5d92ffca308f8dc087f1d2584e819002605e90df` |
| `board-after-plans-01-plan-partner-preview-preview-projection-r2` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-preview-preview-projection-r2.sql` | `21ea054f88c5a8411d7ec7ec5d92ffca308f8dc087f1d2584e819002605e90df` |
| `board-after-plans-01-plan-partner-preview-preview-projection-r3` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-preview-preview-projection-r3.sql` | `21ea054f88c5a8411d7ec7ec5d92ffca308f8dc087f1d2584e819002605e90df` |
| `board-after-plans-01-plan-partner-preview-preview-projection-r4` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-preview-preview-projection-r4.sql` | `21ea054f88c5a8411d7ec7ec5d92ffca308f8dc087f1d2584e819002605e90df` |
| `board-after-plans-01-plan-partner-preview-preview-projection-r5` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-preview-preview-projection-r5.sql` | `21ea054f88c5a8411d7ec7ec5d92ffca308f8dc087f1d2584e819002605e90df` |
| `board-after-plans-01-plan-partner-board-count-r1` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-board-count-r1.sql` | `5691333eeea56095e56ae37767ce0cde59d01895d30e6a112c1fce900dd98632` |
| `board-after-plans-01-plan-partner-board-count-r2` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-board-count-r2.sql` | `5691333eeea56095e56ae37767ce0cde59d01895d30e6a112c1fce900dd98632` |
| `board-after-plans-01-plan-partner-board-count-r3` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-board-count-r3.sql` | `5691333eeea56095e56ae37767ce0cde59d01895d30e6a112c1fce900dd98632` |
| `board-after-plans-01-plan-partner-board-count-r4` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-board-count-r4.sql` | `5691333eeea56095e56ae37767ce0cde59d01895d30e6a112c1fce900dd98632` |
| `board-after-plans-01-plan-partner-board-count-r5` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-board-count-r5.sql` | `5691333eeea56095e56ae37767ce0cde59d01895d30e6a112c1fce900dd98632` |
| `board-after-plans-01-plan-partner-board-ordered-ids-r1` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-board-ordered-ids-r1.sql` | `a7342581b94c545cd664af5fba2054d338615f1b50c66a1a4b8b3525472fc9b4` |
| `board-after-plans-01-plan-partner-board-ordered-ids-r2` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-board-ordered-ids-r2.sql` | `a7342581b94c545cd664af5fba2054d338615f1b50c66a1a4b8b3525472fc9b4` |
| `board-after-plans-01-plan-partner-board-ordered-ids-r3` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-board-ordered-ids-r3.sql` | `a7342581b94c545cd664af5fba2054d338615f1b50c66a1a4b8b3525472fc9b4` |
| `board-after-plans-01-plan-partner-board-ordered-ids-r4` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-board-ordered-ids-r4.sql` | `a7342581b94c545cd664af5fba2054d338615f1b50c66a1a4b8b3525472fc9b4` |
| `board-after-plans-01-plan-partner-board-ordered-ids-r5` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-board-ordered-ids-r5.sql` | `a7342581b94c545cd664af5fba2054d338615f1b50c66a1a4b8b3525472fc9b4` |
| `board-after-plans-01-plan-partner-board-full-projection-r1` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-board-full-projection-r1.sql` | `56157a46538926d1ccd0415a477a7baf2179fcb5a47ea87cf9b9b0990c67b867` |
| `board-after-plans-01-plan-partner-board-full-projection-r2` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-board-full-projection-r2.sql` | `56157a46538926d1ccd0415a477a7baf2179fcb5a47ea87cf9b9b0990c67b867` |
| `board-after-plans-01-plan-partner-board-full-projection-r3` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-board-full-projection-r3.sql` | `56157a46538926d1ccd0415a477a7baf2179fcb5a47ea87cf9b9b0990c67b867` |
| `board-after-plans-01-plan-partner-board-full-projection-r4` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-board-full-projection-r4.sql` | `56157a46538926d1ccd0415a477a7baf2179fcb5a47ea87cf9b9b0990c67b867` |
| `board-after-plans-01-plan-partner-board-full-projection-r5` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-board-full-projection-r5.sql` | `56157a46538926d1ccd0415a477a7baf2179fcb5a47ea87cf9b9b0990c67b867` |
| `board-after-plans-01-plan-partner-board-false-count-r1` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-board-false-count-r1.sql` | `46fa3187d20f2d953a22d364f6f72de3d4ea37850b413557fb4c086127cffcb5` |
| `board-after-plans-01-plan-partner-board-false-count-r2` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-board-false-count-r2.sql` | `46fa3187d20f2d953a22d364f6f72de3d4ea37850b413557fb4c086127cffcb5` |
| `board-after-plans-01-plan-partner-board-false-count-r3` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-board-false-count-r3.sql` | `46fa3187d20f2d953a22d364f6f72de3d4ea37850b413557fb4c086127cffcb5` |
| `board-after-plans-01-plan-partner-board-false-count-r4` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-board-false-count-r4.sql` | `46fa3187d20f2d953a22d364f6f72de3d4ea37850b413557fb4c086127cffcb5` |
| `board-after-plans-01-plan-partner-board-false-count-r5` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-board-false-count-r5.sql` | `46fa3187d20f2d953a22d364f6f72de3d4ea37850b413557fb4c086127cffcb5` |
| `board-after-plans-01-plan-partner-board-false-ordered-ids-r1` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-board-false-ordered-ids-r1.sql` | `6711a3be4a7a9e8d5d98b553ca74f17aeaad581a70bca10bfd96ac4ad3e0ebec` |
| `board-after-plans-01-plan-partner-board-false-ordered-ids-r2` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-board-false-ordered-ids-r2.sql` | `6711a3be4a7a9e8d5d98b553ca74f17aeaad581a70bca10bfd96ac4ad3e0ebec` |
| `board-after-plans-01-plan-partner-board-false-ordered-ids-r3` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-board-false-ordered-ids-r3.sql` | `6711a3be4a7a9e8d5d98b553ca74f17aeaad581a70bca10bfd96ac4ad3e0ebec` |
| `board-after-plans-01-plan-partner-board-false-ordered-ids-r4` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-board-false-ordered-ids-r4.sql` | `6711a3be4a7a9e8d5d98b553ca74f17aeaad581a70bca10bfd96ac4ad3e0ebec` |
| `board-after-plans-01-plan-partner-board-false-ordered-ids-r5` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-board-false-ordered-ids-r5.sql` | `6711a3be4a7a9e8d5d98b553ca74f17aeaad581a70bca10bfd96ac4ad3e0ebec` |
| `board-after-plans-01-plan-partner-board-false-full-projection-r1` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-board-false-full-projection-r1.sql` | `8e34914e924d73cb6e75dde92db7e7f1eb946f40cf336da1d55e2536df6a8dd8` |
| `board-after-plans-01-plan-partner-board-false-full-projection-r2` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-board-false-full-projection-r2.sql` | `8e34914e924d73cb6e75dde92db7e7f1eb946f40cf336da1d55e2536df6a8dd8` |
| `board-after-plans-01-plan-partner-board-false-full-projection-r3` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-board-false-full-projection-r3.sql` | `8e34914e924d73cb6e75dde92db7e7f1eb946f40cf336da1d55e2536df6a8dd8` |
| `board-after-plans-01-plan-partner-board-false-full-projection-r4` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-board-false-full-projection-r4.sql` | `8e34914e924d73cb6e75dde92db7e7f1eb946f40cf336da1d55e2536df6a8dd8` |
| `board-after-plans-01-plan-partner-board-false-full-projection-r5` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-board-false-full-projection-r5.sql` | `8e34914e924d73cb6e75dde92db7e7f1eb946f40cf336da1d55e2536df6a8dd8` |
| `board-after-plans-01-plan-partner-board-null-count-r1` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-board-null-count-r1.sql` | `241ef677a008891de0750b7223054bf992cf0fc1e4dfed02a88573a230b1b143` |
| `board-after-plans-01-plan-partner-board-null-count-r2` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-board-null-count-r2.sql` | `241ef677a008891de0750b7223054bf992cf0fc1e4dfed02a88573a230b1b143` |
| `board-after-plans-01-plan-partner-board-null-count-r3` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-board-null-count-r3.sql` | `241ef677a008891de0750b7223054bf992cf0fc1e4dfed02a88573a230b1b143` |
| `board-after-plans-01-plan-partner-board-null-count-r4` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-board-null-count-r4.sql` | `241ef677a008891de0750b7223054bf992cf0fc1e4dfed02a88573a230b1b143` |
| `board-after-plans-01-plan-partner-board-null-count-r5` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-board-null-count-r5.sql` | `241ef677a008891de0750b7223054bf992cf0fc1e4dfed02a88573a230b1b143` |
| `board-after-plans-01-plan-partner-board-null-ordered-ids-r1` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-board-null-ordered-ids-r1.sql` | `ef555b9bb6d58b4d41e5e84bc72e4cf2f5f3d436f81b68c084b9cf5f5a6812ab` |
| `board-after-plans-01-plan-partner-board-null-ordered-ids-r2` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-board-null-ordered-ids-r2.sql` | `ef555b9bb6d58b4d41e5e84bc72e4cf2f5f3d436f81b68c084b9cf5f5a6812ab` |
| `board-after-plans-01-plan-partner-board-null-ordered-ids-r3` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-board-null-ordered-ids-r3.sql` | `ef555b9bb6d58b4d41e5e84bc72e4cf2f5f3d436f81b68c084b9cf5f5a6812ab` |
| `board-after-plans-01-plan-partner-board-null-ordered-ids-r4` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-board-null-ordered-ids-r4.sql` | `ef555b9bb6d58b4d41e5e84bc72e4cf2f5f3d436f81b68c084b9cf5f5a6812ab` |
| `board-after-plans-01-plan-partner-board-null-ordered-ids-r5` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-board-null-ordered-ids-r5.sql` | `ef555b9bb6d58b4d41e5e84bc72e4cf2f5f3d436f81b68c084b9cf5f5a6812ab` |
| `board-after-plans-01-plan-partner-board-null-full-projection-r1` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-board-null-full-projection-r1.sql` | `4794ce6d534544ea08cd2e640b7fcfdf1cece5c924dbff86e609c727c5281a70` |
| `board-after-plans-01-plan-partner-board-null-full-projection-r2` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-board-null-full-projection-r2.sql` | `4794ce6d534544ea08cd2e640b7fcfdf1cece5c924dbff86e609c727c5281a70` |
| `board-after-plans-01-plan-partner-board-null-full-projection-r3` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-board-null-full-projection-r3.sql` | `4794ce6d534544ea08cd2e640b7fcfdf1cece5c924dbff86e609c727c5281a70` |
| `board-after-plans-01-plan-partner-board-null-full-projection-r4` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-board-null-full-projection-r4.sql` | `4794ce6d534544ea08cd2e640b7fcfdf1cece5c924dbff86e609c727c5281a70` |
| `board-after-plans-01-plan-partner-board-null-full-projection-r5` | `private/board-after-plans-01-select-plans/board-after-plans-01-plan-partner-board-null-full-projection-r5.sql` | `4794ce6d534544ea08cd2e640b7fcfdf1cece5c924dbff86e609c727c5281a70` |
