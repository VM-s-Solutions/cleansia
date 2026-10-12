---
id: T-0816
title: Write the pay breakdown text the same way on every machine
size: XS
owner: pm
created: 2026-10-11
updated: 2026-10-12
depends_on: [T-0807]
blocks: []
stories: []
adrs: []
layers: [backend]
security_touching: false
---

## Context

Mike: "You can run against CI, but I guess it has to be adaptable" (Mike's rulings of 2026-10-11 (`/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/owner-decisions-2026-10-11.md`)). The ground truth reads that as: CI's text (`Collected fee: 300.00`) is right, and it must hold on every machine. No app displays `PayBreakdown`; its only durable reader is the company archive, which copies it verbatim, so the books text must not vary by host culture [BE-24, D5 lane].

## Doing

- Format `PayBreakdown` with `CultureInfo.InvariantCulture` at its two writers: `CalculateOrderPay.cs` (fee share) and `PayCalculatorExtensions.cs` (job and lockout rows).
- Add one RED-first test per writer that pins the host culture, using the house pattern (en-US and cs-CZ hosts). Change the one assertion that builds its expected value in the current culture.
- The integration suite passes on a decimal-comma Mac with no `LC_ALL` override.

## NOT doing

- No test-infrastructure, csproj or CI change.
- No change to how any reader formats figures.

## Acceptance criteria (done looks like)

- [x] **AC1** — `PayBreakdown` is identical under en-US and cs-CZ hosts; RED then GREEN.
- [x] **AC2** — The full integration suite passes under this Mac's own culture.
- [ ] **AC3** — CI stays green.

## Work record

- 2026-10-11 — Filed from the post-Wave E ground truth and Mike's rulings.
- 2026-10-11 — Implemented by the backend lane.
  - Both writers now write `PayBreakdown` with `value.ToString("F2", CultureInfo.InvariantCulture)`. That is the house's ToString-with-InvariantCulture idiom: `MoneyText.cs:19` uses it with its own format string.
    - the fee share, in `CalculateOrderPay.cs`;
    - the job and lockout rows, in `PayCalculatorExtensions.CalculateSeatPay`.
  - The one assertion that built its expected value in the host culture now builds it invariantly.
  - New: `CalculateSeatPay_Writes_Its_Breakdown_With_A_Point_Whatever_The_Hosts_Culture`, over en-US and cs-CZ hosts. The late-cancellation fee-share fact is now a theory over the same two hosts.
  - RED, in this Mac's own en-CZ culture (decimal comma):
    - unit: 4 of 48 fail. One is `Base: 500,00, …` against `Base: 500.00, …`; the other three are dirtiness rows.
    - integration: 3 of 6 fail.
  - GREEN:
    - the pay suites plus four culture-pinned house suites, in the machine culture: unit 148/148 and integration 15/15;
    - the pay suites alone, with `LANG=de_DE.UTF-8`: unit 83/83, and the same integration suites 15/15;
    - the full integration suite, with no `LANG` or `LC_ALL` override: 850/850 (AC2).
- 2026-10-12 — Committed as `d9a9fd8`, which builds on its own. AC3 waits for this PR's exact-head CI.

## Review

- **Correctness: approve. Security: approve.**
  - Both stored-row writers are covered:
    - every seat-pay row (both `CalculateSeatPay` overloads and `FrozenPay`, for Job and LockoutFeeShare rows) funnels into `PayCalculatorExtensions.cs:129-131`;
    - the fee-share row writes its own invariant text at `CalculateOrderPay.cs:276-277`.
  - No csproj, CI or test-infrastructure change.
- **Gate 7:** no CHANGELOG entry, because no client reads `PayBreakdown`. The lane's proposed `business-rules.md` note is outside the Doing list and was not applied.
- **Not absorbed:**
  - Rows already written on a decimal-comma host keep `300,00`. Nothing parses the text, and there is no backfill.
  - Some host-culture formatting never reaches a stored row:
    - the aggregated-pay breakdown, which is discarded;
    - `CalculatePay` and `BuildPayBreakdown`, which only tests call;
    - the Min > Max exception message;
    - the dates in `SendTestEmail` and `SendTestEmailByType`.
  - T-0808:129 says CI runs under `en_US.UTF-8`. That is unverified, because `backend-ci.yml` sets no locale.
