# Knowledge catalogues — how we build

What is left here is **instruction for whoever is writing the code**: the shapes to follow, the rules
a checker enforces, and how to test.

| File | |
|---|---|
| `patterns-backend.md` · `patterns-frontend.md` · `patterns-mobile.md` | the shapes each stack uses |
| `consistency.md` | the A/B/C/D/E rules `check-consistency.mjs` enforces |
| `conventions.md` | naming, and the discipline for claims that decay |
| `testing.md` | what to test and where |
| `runtime-readiness.md` | the observability / degradation gate before a feature is done |

## What moved out, 2026-08-13 (CL-034)

The other half of this folder was never build instruction — it was **domain truth**, describing what
the platform *does* rather than how to write it. That belongs where everyone can read it, so it was
published:

| Was | Now |
|---|---|
| `security-rules.md` | [`docs/architecture/security-rules.md`](../../docs/architecture/security-rules.md) — the S1–S12 laws |
| `platform-expandability.md` | [`docs/architecture/platform-expandability.md`](../../docs/architecture/platform-expandability.md) |
| `roles/**` (18 files) | [`docs/domain/roles/`](../../docs/domain/roles/) — the per-component contracts |

The filenames did not change, so a reference by name still reads correctly.

**Their claims are still checked.** `check-catalog-claims.mjs` names the new paths explicitly rather
than globbing `docs/**` — widening the corpus to the 51 migrated ADRs is a decision to take on its own,
not a side effect of a move. The checker reads the same 36 files it always did.
