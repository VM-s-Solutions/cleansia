---
id: T-0937
title: "Invoice and receipt PDFs keep every rendered version, API timestamps carry the stored precision, and the DEV publish script is tested against Postgres"
size: M
owner: Michael Chaban
created: 2026-11-19
updated: 2026-11-19
depends_on: [T-0934]
blocks: [T-0939]
stories: []
adrs: []
layers: [backend, db, qa, docs]
security_touching: false
---

## Context

Mike, 19 November 2026, on the phase-2 E2E report (T-0934, `e2e/runs/2026-11-18/findings.md`): "Fix all of them by
waves making the fixes that pays off in long term, not quick fixes". The findings are fixed in three waves, T-0937,
T-0938 and T-0939, one PR each. This is wave 1. His rulings (AskUserQuestion): a re-rendered invoice or receipt PDF
keeps every version (a new file per render, the row points at the newest, older files stay as issued, nothing is
overwritten); DEV is deployed after each merged wave and the wave's cases are re-run there.

Every row was ground-truthed against master c24378926 before filing.

## Doing

1. F-075: a second render of one document fails, because `IBlobContainerClient.UploadAsync` calls the SDK's
   create-only upload (`BlobContainerClient.cs:58-63`). Two callers render a document more than once:
   `RegenerateInvoicePdf` (Regenerate PDF, the re-render after Adjust Amounts, and the recovery after a lost night
   stamp) and the receipt's fiscal retry (`ReceiptService.cs:479-488`, which uploads again under the same
   `BlobName`). Each re-render now writes a new, numbered file, the invoice or receipt points at it in the same
   commit, and the earlier file stays as issued.
2. F-081: the API answers a timestamp with the 100 ns ticks .NET holds while Postgres stores microseconds, so a reply
   built before the commit differs from every later read (`MarkMyPartDone`: `…51.1991616Z`, then `…51.199161Z`). The
   JSON output the hosts share now writes timestamps at the stored precision.
3. F-076: `sql-scripts/dev-e2e-publish-cleaner-document.sql` fails at `CREATE FUNCTION` (an aggregate that belongs to
   the outer query), and its date pin expired with 19 November. The function is fixed, the pin becomes a guard that
   does not expire, and a test in the integration suite runs every `dev-e2e-*.sql` script against its real Postgres,
   so a broken script fails CI instead of a DEV run.

## Not doing

- Moving the 148 `DateTime.UtcNow` calls to `TimeProvider`: the precision is fixed at the API boundary.
- Azure storage-account blob versioning: it would also keep deleted photos and documents, against erasure.
- The E2E re-run of PAYOUT-057 and TAKE-025 on DEV: T-0939.
- PRO.

## Done

The three fixes are merged with CI green, each with a test that fails without it, and DEV is deployed with the wave.

## Work record

- 2026-11-19 — Filed with T-0938 and T-0939 on `fix/t0937-wave1-documents-and-data`, after Mike's rulings.
- 2026-11-19 — Built on `fix/t0937-wave1-documents-and-data`, one review and its in-scope rows fixed. F-075: each
  re-render is a new numbered file (`INV-2026-000017-2.pdf`, then `-3`) written through
  `DocumentVersions.UploadNewVersionAsync`, for Regenerate PDF, Adjust Amounts, the recovery after a lost night stamp
  and the receipt's fiscal retry; the row points at the newest, the earlier files stay, and retention deletes every
  render; ADR-0083 records the ruling. F-081: the API hosts write timestamps cut to the microsecond as Npgsql stores
  them. F-076: the publish script's ULID function is fixed, its date pin is replaced by a confirmation the dispatch
  must name, and `DevE2eScriptsTests` runs every dev-e2e script against Postgres in CI. No schema change.
- 2026-11-19 — Shipped in #388 (merge `c76007860`), CI green on every check; DEV deploy dispatched after the merge
  (run 44577499932). Reported to Mike, not fixed: the review's R9 (an upload fault after the fiscal stamp leaves a
  receipt that is never retried) and R10 (a failed first receipt upload is never repaired in fiscal mode None), the
  company archive copying only a document's newest render, and R14 (`dotnet ef` re-creates the literal `bin\Debug`
  folders on macOS).
