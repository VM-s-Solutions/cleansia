# Execution Plan — Collision Check & Corrections (2026-06-01)

A 5-checker parallel collision audit of `AUDIT-2026-06-01-execution-plan.md` (dependency cycles, ADR
sequencing, same-file collisions, logical contradictions, TDD pairing). **39 candidates → 18 real
issues.** Run before execution, as the owner required.

## Verdict
**The wave plan was NOT sound as written — 3 blocking structural defects.** All now corrected (below);
the corrected sequencing is reflected in the plan and `INDEX.md`.

1. **ADR-vs-Wave-0 sequencing was self-contradictory.** Wave-0 PROD-gate items (F2/SEC-W1, F4, BSP-1,
   BSP-6, BSP-4) hard-depended on Wave-1 ADRs that are *defined as* those same items' fixes → Wave 0
   could not complete before Wave 1, breaking the "Wave 0 is the PROD gate" claim.
2. **The TDD claim was prose-only and structurally false.** Every `TC-*` test sat in Wave 4 with
   backward `depends-on` arrows at Wave-0 fixes — "tests land with the fix" was impossible under strict
   ordering, and the account-takeover + money-idempotency criticals had **zero** paired tests.
3. **Same-file authz collisions.** BSP-1/BSP-6/IDA-SEC-04/SEC-DSP-01 all mutate
   `PolicyBuilder.cs`/`Policy.cs` in parallel within Wave 0.

## Corrections applied

### A. ADR/Wave-0 sequencing (blocking)
- **Pre-Wave-0 ADR sprint:** ADR-AUTHZ, ADR-OUTBOX (contract), ADR-RATELIMIT are **decided and accepted
  before** the Wave-0 items that encode them begin coding. ADR-REFUND, ADR-INTEGRATION stay parallel and
  gate Wave 2.
- **Outbox split tactical/strategic:** Wave 0 gets the self-contained fixes that need **no** ADR —
  F2/SEC-W1 (move `queueClient.SendAsync` after `CommitAsync` + idempotent receipt consumer), F4
  (idempotent-on-OrderId, reserve receipt row pre-email, fiscal sequence once), F3 (poison/dead-letter +
  `host.json maxDequeue`). The **full transactional outbox** (table, dispatcher, post-commit drain across
  5 queues) becomes a Wave-1 ticket governed by ADR-OUTBOX. Edges: F4→F2, F3→F2 (shared dispatcher); drop
  F2/F3/F4 → ADR-OUTBOX hard deps from Wave 0.
- **Partitioned limiter ships in Wave 0** (BSP-4); ADR-RATELIMIT formalizes "shared across hosts" after.

### B. Same-file authz collisions (blocking)
- **Merge BSP-1 + BSP-6** into one `PolicyBuilder` ticket (fail-closed fallback + full Map + startup
  completeness assertion are one edit); keep the startup-assertion **guardrail test** as BSP-1's paired
  test. **Serialize within Wave 0:** merged BSP-1 → then IDA-SEC-04 → then SEC-DSP-01. No parallel edits
  to `PolicyBuilder.cs`/`Policy.cs`.
- **Severity relabels:** merged BSP-4/IDA-SEC-02 → **critical** (global DoS); BSP-6 → guardrail-test;
  F11 → correctness-minor (stays Wave 0).

### C. TDD structure — a real Wave-0 test slice (blocking; this is the owner's TDD instruction made real)
Tests now land **in the same merge** as their fix (Gate 6). New Wave-0 test tickets:
- **TC-PAY** (was TC-1) → Wave 0; **drop the `TC-1-sub` broken dep** (holiday-calendar is a separate
  backlog story).
- **TC-AUTHZ-0** — cross-tenant/cross-user write-path tests, paired with merged BSP-1 + SEC-DSP-01/02.
- **TC-IDEMP-0** — webhook + money idempotency ("safe to run twice") covering F2/F11/SEC-W2 **and** the
  three LG money fixes (incl. LG-SEC-02 direct-subscribe, which the webhook tests do **not** cover).
- **TC-AUTH-TAKEOVER** (new) — token-claim binding (IDA-SEC-01) + `(email, hashedToken)` reset-code
  lookup (IDA-SEC-03).
- TC-4 → drop the AUD-06 dep; write CreateOrder **characterization** tests first (TDD-before-refactor);
  AUD-06 rebases on them. Keep a TC-2/TC-3 regression that Stripe signature verification stays on (it is
  REFUTED as a gap, not removed).

### D. Triage / bundling (should-fix)
- Pull **LG-SEC-05** into the Wave-0 anonymous-tenant bundle alongside BSP-9 (more-severe sibling).
- Split **DA-2** (dispute transition-guard) out of the Wave-2 admin-UI bundle into its own critical
  correctness ticket; name ADR-REFUND + ADR-AUTHZ as that bundle's hard gates.
- Remove the "(folds T-0008/F8)" annotation from LG-SEC-02 (F8 stays its Wave-2 row; T-0008 phantom).
- Drop the `SEC-W3 → BSP-4` edge (BSP-4 keeps the webhook unlimited; SEC-W3 is an independent per-IP
  egress window). Serialize BSP-5 with BSP-4 (both own `CleansiaStartupBase.cs`).

### F. PM rebase notes (no merge, just sequence)
- AUD-06 (Wave 3) rebases on the post-F2 `CreateOrder.cs` handler.
- LG-01q/LG-03 (Wave 1) rebase on the post-LG-SEC-06 `LoyaltyService` idempotent path.

## Second pass — owner-requested full plan review (self-review)
A human-style end-to-end read of the corrected plan found 5 more issues; the two real backlog bugs are
fixed:
- **#1 (fixed): `LG-SEC-05` was duplicated** — listed in both Wave 0 (correct, pulled forward) and Wave
  2 (stale). Removed the Wave-2 occurrence.
- **#3 (fixed): two criticals had tests but no FIX ticket in Wave 0** — `SEC-EMP-01`/`EMP-SEC-1`
  (partner-analytics IDOR) and `EMP-GAP-01` (rejected cleaners can still work orders). `TC-AUTHZ-0`
  referenced SEC-EMP-01 but no fix row existed. Added both as Wave-0 rows.
- **#2 (noted): `IA-1`** (admin double-hash) has no paired test — add a one-line characterization test.
- **#4 (noted): `F2-FULL` supersedes the Wave-0 tactical F2/F3/F4** — this is deliberate throwaway
  scaffolding (ship-safe-now, do-it-right-after-ADR), not scope creep; documented so no one re-opens it.
- **#5 (open question): `TC-10` (fiscal-mode tests) has no paired fiscal *fix* ticket** — confirm the
  modes are already correct (then TC-10 is a pure characterization test) or add a fix. Flag for the
  fiscal owner.

## Net effect
After these edits the strict wave ordering holds, **Wave 0 is a true PROD gate**, and **Gate 6
(test-with-fix) is satisfiable** — TDD is structural, not prose. Dropped false positives are recorded in
the workflow result (`tasks/wd236g5si.output`).
