---
id: T-0523
title: QR Platba (SPD) payment code and the invoice barcode
status: rejected
size: M
owner: backend
created: 2026-08-02
updated: 2026-08-03
depends_on: [T-0522]
blocks: []
stories: []
adrs: []
layers: [backend]
security_touching: false
manual_steps: []
sprint: 15
---

## Owner ruling — REJECTED 2026-08-03

The owner declined this outright: **"Nope, I don't need a QR code payment."**

Both halves are dropped, not deferred:

- **QR Platba (SPD).** Declined by the owner. It would have required a QR encoder — QuestPDF 2024.12.1
  has none (verified by reflection over its public API), and hand-rolling Reed-Solomon over GF(256)
  plus mask-penalty scoring is not defensible. Candidate dependencies were QRCoder (MIT) or ZXing.Net
  (Apache-2.0). **No dependency was added.**
- **Code 128 barcode.** Dropped with it. This one needed no package — a width table, a mod-103
  checksum and filled rectangles, roughly 80 lines against QuestPDF's existing primitives. It was
  carried only because the owner's specimen shows one. It is decoration on a document the cleaner
  files, so it goes with the QR rather than surviving alone as scope the owner did not ask for.

**Do not resurrect either from the specimen photograph.** The specimen is the reference for the
invoice's *content and layout*, and matching it pixel-for-pixel was never the goal. If a payment code
is ever wanted, note that the fixture's Cleansia IBAN `CZ1101000000001234567890` **fails mod-97** —
harmless today because it is never printed, and a live defect the moment anything encodes the company
account into a payment instruction.

The SPAYD payload research and the ISDOC sizing from this investigation are preserved below for
whoever revisits it.

## Context

The owner's specimen invoice carries a **QR Platba +F** code and a **barcode**. Neither exists on the
platform's document — PM-verified: no QR, no barcode, no encoder dependency anywhere in
`Cleansia.Infra.Services/Pdf`.

**Filed separately from T-0522 on purpose.** It needs a **new dependency** (a QR/barcode encoder) and a
**format specification** (Czech SPD — *Short Payment Descriptor* — and whatever symbology the
specimen's barcode uses). A correct invoice that is missing a convenience code is still a correct
invoice; a document held up because a QR library was being evaluated is a cleaner not being paid.

**It is also the highest-value-per-line item on the whole invoice chain for the human on the other
end.** A cleaner scans it and the payment is keyed for them — no transposed account number, no wrong
variable symbol. That is the same class of defect T-0519's real validation is guarding against, caught
at the other end of the pipe.

## Acceptance criteria

- [ ] **AC1 — the QR payload conforms to the Czech SPD standard**, at the version the specimen uses,
      with the fields the specimen carries (account/IBAN, amount, currency, variable symbol, constant
      symbol, due date, message). **Attribute the standard; do not paraphrase it from memory.**
      Evidence: the payload string for a real invoice plus the spec citation.
- [ ] **AC2 — the QR code SCANS, and this is proved by an actual scan.** A rendered square that no
      banking app accepts is worse than no code, because it looks like it works. **Evidence must be a
      scan result, not a rendered image** — and if no agent can scan it, that is stated plainly and
      handed to the owner as a one-minute check with their phone.
- [ ] **AC3 — the amount and the variable symbol in the QR equal the ones printed on the document.**
      A test asserts the equality. **A QR that disagrees with the printed figures is a payment sent to
      the wrong place with our own code on it.** Evidence: the test.
- [ ] **AC4 — the barcode's symbology is identified from the specimen before anything is encoded.**
      Code 128 of the invoice number is the common case; ISDOC documents also carry other conventions.
      **If it cannot be identified with confidence, say so and ask the owner** rather than shipping a
      plausible barcode. Evidence: the identification, or the question.
- [ ] **AC5 — the new dependency is justified and vetted.** Licence, maintenance status, transitive
      footprint, and whether QuestPDF (already in use) covers it natively. **`optimizer` reviews it**
      per `process/routing.md` (new dependency). Evidence: the comparison plus the verdict.
- [ ] **AC6 — generation failure degrades gracefully.** An encoder exception must not fail the invoice
      run for an entire pay period. Evidence: the test that forces a failure and still produces the
      PDF.
- [ ] **AC7 — no personal data beyond what is already printed goes into the code.** The QR is scannable
      by anyone who sees the document. Evidence: the payload field list, checked against the printed
      document.
- [ ] **AC8 — the placement matches the specimen.** Evidence: the rendered PDF beside it.
- [ ] **AC9 (Gate 0.5)** — `Cleansia.Tests` / `Cleansia.IntegrationTests` / `Cleansia.HostTests`
      **locally**, baselines **2295 / 108 / 75**. **Gate 0.5 leg 1 applies to AC3** (the equality
      assertion is executable); AC2's scan is not executable and says so under leg 3.

## Out of scope

- **The document itself** — **T-0522**. This adds two marks to a document that is already correct.
- **Payment initiation.** A QR code is a convenience for a human keying a transfer, not a payment rail.
- **The customer receipt.** If a QR belongs there too, that is a separate ticket — **name it, do not
  build it.**
- **SK's equivalent (PAY by square).** CZ first.

## Implementation notes

**`backend` + `optimizer`** (AC5, new dependency) with a `reviewer` in parallel.

**AC2 is the AC that decides whether this ticket is real.** Everything else can be verified by an
agent; a scan cannot. Plan for the owner to do it, and make that a 60-second ask with the PDF attached.

**Read first:** T-0522's implementation, the owner's specimen annotation from T-0508 AC1, the QuestPDF
capabilities already in `Cleansia.Infra.Services/Pdf`, and `agents/knowledge/patterns-backend.md` on
adding dependencies.

## Status log
- 2026-08-02 — **draft (created by pm from the owner's 2026-08-02 invoice specimen).** Split from
  T-0522 because it needs a **new dependency** and an **external format standard**, and neither should
  delay a legally-correct document. **AC2 is deliberately unforgiving:** a rendered QR nobody scanned
  is not evidence, and the failure mode (a code that looks right and pays the wrong account) is the
  worst one on this whole chain.

## Review
