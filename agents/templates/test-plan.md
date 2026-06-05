# Test Plan — T-NNNN <ticket title>

- **Author:** qa
- **Date:** YYYY-MM-DD
- **Story:** US-<persona>-NNNN
- **App(s) under test:** customer / partner / admin / mobile / api

## Cases
One row per AC item, plus edge & negative cases.

| # | Type | Given / When / Then | Expected | Result | Evidence |
|---|---|---|---|---|---|
| 1 | AC1 (happy) | ... | ... | PASS/FAIL | screenshot/log/test |
| 2 | edge | ... | ... | | |
| 3 | negative (authz) | cross-user access attempt | rejected (NotFound) | | |
| 4 | money | pay calc / rounding | exact value | | |

## Automated tests added
- `path/to/Test.cs` — what it covers.

## Regression spot-checks
- Adjacent features touched by the shared code, and their result.

## Defects found
- Repro steps, expected vs actual; raised to PM as <finding/ticket>.
