---
id: T-0832
title: Make the native apps' sort reach the server
size: M
owner: pm
created: 2026-10-12
updated: 2026-10-12
depends_on: []
blocks: []
stories: []
adrs: []
layers: [android, ios, backend]
security_touching: false
---

## Context

Found on 2026-10-12. Neither native app's sort parameter binds on the server, so every native sorted list gets the endpoint's default order instead of the one it asked for.
- Android serialises the generated model through `toString`: `Sort=SortDefinition(field=cleaningDateTime, direction=0)`, captured from the generated Retrofit client.
- iOS sends base64 JSON: `Sort=ewog…`.
- `DataRangeRequest.Sort` (`DataRangeRequest.cs:13`) has no binder or type converter, and nothing in `src/` adds a value provider.
- A throwaway ASP.NET Core 10 app with the same request shape bound both strings to an empty list, with ModelState valid. The web client's indexed form, `Sort[0].field=…&Sort[0].direction=0` (generated, `admin-client.ts:112`; the keys bind case-insensitively), bound one entry.
- An empty sort falls back to each endpoint's default. For orders that is `SortMapper.ForOrderPage`: newest created first, then `Id`.

Six native call sites send a sort:
- Android: `OrdersRepository.kt:300` (the Orders list's chosen field and direction), `DashboardRepository.kt:267` (upcoming, by cleaning date ascending) and `InvoicesRepository.kt:125` (created descending).
- iOS: `PartnerOrderClient.swift:107`, `PartnerDashboardClient.swift:27` and `PartnerPayrollClient.swift:51`.

Effects:
- The Orders list's sort choice does nothing on either app.
- The dashboard's upcoming page is the ten most recently created orders from today. So once a cleaner has more than ten orders from today on, the next job can fall off the page (whenever ten newer-created ones exist), and the today line can undercount (T-0831).
- The two created-descending calls get whatever their endpoint's default is, which this finding did not check.

## Doing (proposed, for Mike)

- Both native clients send the indexed form the server already binds (`Sort[0].Field`, `Sort[0].Direction`). This goes through each app's generator configuration or one query-encoding seam. A wire test on each app pins the form.
- The alternative is a server binder that accepts the native forms too. Choosing between the two is the Architect's call.

## NOT doing

- No change to the web client, which already binds.
- No change to `SortMapper`'s defaults.

## Acceptance criteria (done looks like)

- [ ] **AC1** — Every native sorted request carries a form the server binds. On each app, the wire tests go RED, then GREEN.
- [ ] **AC2** — On a device, the Orders list's sort choice changes the order.
- [ ] **AC3** — The full suites pass.

## Work record

- 2026-10-12 — Filed from the correctness re-review of the native PR (T-0831). Reported, not absorbed (CLAUDE.md §2): Mike decides whether it becomes work.

## Review

Pending.
