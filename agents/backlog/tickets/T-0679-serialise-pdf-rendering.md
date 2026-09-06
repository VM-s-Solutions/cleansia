---
id: T-0679
title: Serialise PDF rendering — concurrent renders corrupt the text layer of invoices and receipts
status: done
size: S
owner: backend
created: 2026-09-06
updated: 2026-09-06
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend]
security_touching: false
manual_steps: []
sprint: 16
---

## Context

**QuestPDF 2024.12.1's bundled native Skia is not thread-safe when it builds a subsetted font's
`/ToUnicode` CMap.** With two `GeneratePdf` calls in flight in one process, roughly 1–3% of renders
emit a CMap in which **every glyph maps to `U+0000`** instead of its real code point.

The document looks perfect. What breaks is the **text layer**: copy, paste, search and any text
extraction return nothing usable. On a payout invoice that means an accountant cannot select a figure
out of it; on a receipt, the same.

### The evidence

Measured in one process, by object-level diff of divergent PDF pairs:

| condition | corrupted renders |
|---|---|
| sequential | **0** / 300 |
| concurrent | **39** / 1200 |
| concurrent behind one lock | **0** / 1200 |

A divergent pair differs in **exactly one indirect object** — a `/ToUnicode` CMap whose every
`bfchar` entry is `<0000>`. Page content streams, the glyph ids actually drawn, the font programs and
the widths are byte-identical; the trailing xref offsets shift only because that one stream changed
length. It hit three different subsetted faces across runs (Lato-Bold, Lato-Regular, Lato-Italic).
The signature matches Skia's `sk_bzero` fallback in `SkTypeface_FreeType::onGetGlyphToUnicodeMap`
when the FT face cannot be acquired; QuestPDF caches typefaces process-wide.

### It is live, not latent

`RegenerateInvoicePdf` is a plain MVC action with no concurrency limiter, on **two hosts** —
`Cleansia.Web.Admin/Controllers/AdminInvoiceController.cs` and
`Cleansia.Web.Partner/Controllers/EmployeePayrollController.cs`. Two admins clicking *Regenerate* at
the same moment on the same host is two concurrent renders. It has a second, non-obvious caller:
`AssignInvoiceVariableSymbol` sends the same command, so every variable-symbol assignment renders on
a web thread too. Payroll admins working down a list on payroll day is exactly that workload.

In the Functions process the paths are individually serial (`host.json` pins the receipt queue at
`batchSize: 1`, and the pay-period job is a `foreach` with awaits between renders), but the cron table
schedules `CloseExpiredPayPeriods` (`0 0 2 * * *`) and `FiscalReconciliation` (`0 */5 * * * *`) in the
**same second**, and reconciliation re-enqueues `generate-receipt` unconditionally.

### The output is permanent

Both artefacts are uploaded to blob storage **and** emailed, so one corrupt render leaves **two
permanent copies**. `docs/architecture/infrastructure.md` records the retention for
`generated-receipts` and `generated-invoices` as **Indefinite**, and every later download is a pure
blob read with no re-render.

An **invoice** has a remedy — `RegenerateInvoicePdf` re-renders over the same blob name, so it is one
admin click from repaired. A **receipt has none**: nothing re-renders one outside a fiscal-retry path
that is dormant by default (`FiscalEnforcementMode.None` is the shipped default with no seed
overriding it).

### Why a lock, and why the throughput objection does not survive

The obvious objection is that serialising every PDF in the process is a throughput and thread-pool
risk. It is not, and the reason is a fact that was not known when the objection was raised:
**QuestPDF already blocks every `GeneratePdf()` call on a static process-wide semaphore admitting
two.** `QuestPDF.Drawing.DocumentGenerator.RenderDocumentSemaphore` is `static`, non-public, and
`CurrentCount == 2` — verified by reflection against the pinned package.

So the process already parks threads on a global render gate. This change moves it from two to one:
one more parked thread per burst, against a real demand near 0.01 renders/second. No customer request
pays any of it — a receipt download is a blob read, and the receipt queue is already pinned at one
message in flight.

## Acceptance criteria

- [x] **AC1** — Given eight threads calling `GenerateInvoicePdf` at once, When they run, Then the
      observed maximum concurrency inside the renderer is exactly 1.
- [x] **AC2** — Given the lock is removed, When the guard test runs, Then it FAILS. (A test that
      cannot detect the fix's absence is not a gate.)
- [x] **AC3** — Given the fix is in place, Then no existing test changes and no interface, DI
      registration or caller changes.

## Out of scope

- **Detect-and-retry** (render, inspect the `/ToUnicode` stream, re-render if it is all-zero). Better
  measured throughput (104/s vs 66/s) and rejected anyway: the saving is four orders of magnitude
  beyond demand, it is a PDF object scanner plus a zlib inflate plus a retry loop plus binary fixtures
  against one `lock`, and it is probabilistic where the lock is correct by construction. Its detector
  also fails OPEN — two separate agents each shipped a silently-broken version of it while
  investigating this ticket.
- **Upgrading QuestPDF.** Not an escape: 2026.2.2 measured *worse* (55/2500).
- **Disabling font subsetting or using a standard PDF font.** The copy is cs/sk/en/ru/uk — Cyrillic
  and Czech diacritics — so glyph coverage is not negotiable.
- **Pre-warming the typeface cache at startup.** It narrows the window rather than closing it, which
  is worse than a lock because it looks solved.
- **Repairing already-corrupted artefacts in blob storage.** Unknown quantity, and invoices have an
  operator remedy already. A separate decision if it turns out to matter.
- **Any async/`SemaphoreSlim` seam.** `IPdfService` is synchronous by design and changing it would
  touch every caller to solve a problem a `lock` solves in one file.

## Implementation notes

`Cleansia.Infra.Services/Pdf/QuestPdfService.cs` only:

- one `private static readonly object RenderGate = new();`
- `lock (RenderGate)` around each of the two `Document.Create(...).GeneratePdf()` expressions.

Static, not per-instance: `IPdfService` is registered **scoped**
(`Cleansia.Infra.Services/ServiceCollectionExtensions.cs`), so every request gets its own service and
an instance field would gate nothing. The state being protected is QuestPDF's process-wide typeface
cache.

The lock wraps only the render call, not the logging or the country-logic enrichment, so nothing else
is serialised.

## Status log

- 2026-09-06 — filed and worked in one pass, on the owner's instruction to investigate, plan, file
  and fix. Root cause proven empirically before any code was written.

## Review

<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
