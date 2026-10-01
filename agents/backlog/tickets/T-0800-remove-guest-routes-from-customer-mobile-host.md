---
id: T-0800
title: Remove the six guest routes from the customer mobile host, and refresh the mobile spec, once no supported app build calls them
status: todo
size: S
owner: —
created: 2026-10-02
updated: 2026-10-02
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend, android, ios, docs]
security_touching: true
manual_steps: []
sprint: —
---

## Context

On 2026-10-01 the owner removed every guest surface from both customer apps (remark C1, reversing the
2026-09-28 meeting default E-13): *Find a guest booking* and its lookup, preview and cancel are gone
from Android (`c45fb87c8`) and iOS (`a021c7a34`). A guest booking is now made, tracked and cancelled
on the web only, from the link in its e-mail.

**Owner decision D1, the same day:** keep the six token-keyed guest routes on the customer **mobile**
host, and the committed mobile spec unchanged, because an app build installed before the change still
calls `Lookup`, `GuestCancellationPreview` and `CancelGuest`. Remove them once no supported app build
calls them. This ticket is that removal. It is filed now so the routes do not outlive their reason
silently.

Ground-truthed 2026-10-02 against `feature/mobile-polish-2026-10-01`:

- `src/Cleansia.Web.Mobile.Customer/Controllers/OrderController.cs` still serves all six, each
  `[AllowAnonymous]`: `POST CancelGuest`, `POST GuestCancellationPreview`, `POST ReportGuestNoShow`,
  `POST Lookup`, `GET Lookup`, `POST LookupBatch`.
- `src/cleansia_android/openapi/customer-mobile-api.json` (the spec both platforms generate from) still
  carries the five paths (`/api/Order/Lookup` with both verbs).
- Neither app's anonymous allow-list names a guest path any more.

## Acceptance criteria

- [ ] **AC1** — Given the owner has confirmed that no supported customer app build calls a guest route
      (the trigger; see Out of scope), When the change ships, Then the customer mobile host serves none
      of the six routes and each answers `404`. The customer **web** host serves all six exactly as before.
- [ ] **AC2** — The committed `customer-mobile-api.json` is re-dumped from the host
      (`src/cleansia_ios/scripts/refresh-mobile-spec.sh customer`), carries none of the five paths, and
      both platforms' generated clients are regenerated from it in the same change; no generated type
      that only a guest route used survives.
- [ ] **AC3** — The host tests that build a mobile host for a guest route
      (`Cleansia.HostTests/GuestCancellationRouteTests.cs`, the `bool mobile` theories, and any other
      test that names the mobile `OrderController`'s guest actions) assert the routes are absent on the
      mobile host and keep proving them on the web host.
- [ ] **AC4** — docs/ no longer says the mobile host still serves them:
      `/flows/booking-and-pricing#guest-order-lookup`, `/flows/auth-and-identity#native-account-only`,
      `/mobile-app/api-integration#the-anonymous-allow-list`, and §3 of
      `src/cleansia_ios/docs/header-parity-contract.md`.

## Out of scope

- **Deciding when.** Whether an older build is still supported is the owner's call, from knowledge of
  the installed base; this ticket starts only on that confirmation.
- The customer web host's guest routes, guest booking and `/track-order`. Unchanged.
- Any change to the guest access token, its lifetime or its rate-limit window.

## Implementation notes

- Removing an anonymous route narrows the host's anonymous surface, so the Security gate looks at it
  (`security_touching: true`).
- The shared handlers (`CancelGuestOrder`, `GetGuestCancellationFeePreview`, `ReportGuestCleanerNoShow`,
  the lookup queries) stay: the web host still calls them. Only the mobile controller actions go.
- No migration, no DTO change.

## Status log

- 2026-10-02 — filed as `todo` from owner decision D1 (2026-10-01), on the mobile-polish branch.
