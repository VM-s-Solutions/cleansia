# Security findings — iOS partner Order work-loop (T-0307: Take/NotifyOnTheWay/Start/Complete + note/issue add·update·delete + GetPaged/GetById)

## 2026-06-27 — T-0307 Order-action gate (Gate-SEC, security reviewer) — CHANGES (one binding backend fix) + APPROVE-the-iOS-design (binding client rules)

**security_touching: YES.** **Verdict: CHANGES REQUESTED on ONE backend invariant (S3/S4 read-scoping
of `GetPaged`); APPROVE-the-design for the iOS client + the 10 state-changing/authorship-scoped
command paths (VERIFIED safe on the reachable backend).** Scope = the **order-action / ownership**
security gate of T-0307 (the architect rules nav/state/sheet/map in parallel — I stay out of those).
T-0307 is **greenfield** — `phase/ios-phase4` carries **zero** committed diff (`git diff master...HEAD`
= 0 files); no iOS order code exists on disk yet — so the client rules below are what the developer
builds to and the reviewer enforces, not findings against shipped iOS code. The **backend Order
surface was traced on this Mac** (same discipline as the Devices D8 ruling): the action handlers are
server-scoped + safe; the **one gap is the `GetPaged` read** (employee can over-read other employees'
assigned-order rows by passing a foreign `employeeId` filter). That gap is **pre-existing backend
behavior** T-0307 consumes — flagged as a backend fix, not an iOS regression. Cross-ref: sprint-12
§7.8 sub-note.

### DECISION 1 — IS T-0307 security_touching? **YES.**
It drives state-changing, ownership-scoped mutations on a **shared** resource (Order) over an
authenticated partner API: `orderTakeOrder` (self-assign), `NotifyOnTheWay`/`StartOrder`/`CompleteOrder`
(lifecycle transitions with money/email/loyalty/referral/pay fan-out on Complete), and note/issue
add·update·delete (author-scoped writes). It also surfaces customer PII + geo on the reads
(`GetById`→`OrderItem`, `GetPaged`→`OrderListItem`). This is the partner-mobile analogue of the
Devices revoke gate — Gate 3 applies (endpoint + resource-by-id + side-effecting commands + DTO + PII).

### What was read (trace base — backend reachable on this Mac)
- Controller: `Web.Mobile.Partner/Controllers/OrderController.cs` (all 14 routes: GetPaged, GetById,
  TakeOrder, StartOrder, NotifyOnTheWay, CompleteOrder, AddNote, UpdateNote, DeleteNote, ReportIssue,
  UpdateIssue, DeleteIssue, + photo routes).
- Action handlers: `Core.AppServices/Features/Orders/{TakeOrder,NotifyOnTheWay,StartOrder,CompleteOrder,
  AddOrderNote,UpdateOrderNote,DeleteOrderNote,ReportOrderIssue,UpdateOrderIssue,DeleteOrderIssue}.cs`.
- Reads: `GetPagedOrders.cs`, `GetOrderDetails.cs`; spec `Core.Domain/Specifications/OrderSpecification.cs`;
  filter `Features/Orders/Filters/OrderFilter.cs`.
- Authz seam: `Core.AppServices/Authentication/OrderAccessService.cs` (`GetCallerEmployeeIdAsync`,
  `CanAccessOrderAsync`, `CanBrowseOrderAsync`).
- DTOs/mapper: `Features/Orders/DTOs/{OrderItem,OrderListItem,OrderNoteDto,OrderIssueDto,
  AssignedEmployeeDto}.cs`; `Mappers/OrderMappers.cs`.
- Android parity (the legitimate "mine" convention): `partner-app/.../features/orders/
  OrdersListViewModel.kt:232-266` (the My tabs pass the client's OWN stored `employeeId`).

### DECISION 2 — ORDER-ACTION OWNERSHIP SCOPING (S2/S3, the load-bearing authz) — VERIFIED

- **S1 (actor is JWT-derived, never client `employeeId`) — PASS (VERIFIED).** Every action handler +
  validator derives the acting employee via `IOrderAccessService.GetCallerEmployeeIdAsync`, which
  resolves from the JWT (`OrderAccessService.ResolveCallerEmployeeIdAsync`: employee-id claim →
  fallback `GetByUserEmailAsync(jwt email)`). **No command carries a client `employeeId`** — the
  Take/Notify/Start/Complete commands are `record Command(string OrderId[, ...])` only; note/issue
  commands are `(OrderId, NoteId/IssueId, Content/Description)`. The author stamp on create is the
  server `employeeId` (`OrderNote.Create(orderId, employeeId, ...)` `AddOrderNote.cs:72`;
  `OrderIssue.Create(orderId, employeeId, ...)` `ReportOrderIssue.cs:72`).
- **S2 (authorization) — PASS (VERIFIED).** Every route has a `[Permission(Policy.CanXxx)]` attribute
  (`OrderController.cs`: CanViewPagedOrder, CanViewOrderDetail, CanTakeOrder, CanStartOrder [Notify
  reuses CanStartOrder], CanCompleteOrder, CanAddOrderNote, CanUpdateOrderNote, CanDeleteOrderNote,
  CanReportOrderIssue, CanUpdateOrderIssue, CanDeleteOrderIssue). No `[AllowAnonymous]`, no missing
  attribute.
- **S3 (Take = unassigned/takeable only) — PASS (VERIFIED).** `TakeOrder.Validator` requires the
  order to exist + `HasAvailableSpots` + caller not already assigned + Approved/profile/limit/no-conflict;
  the handler only flips New/Pending→Confirmed (`TakeOrder.cs:194-198`). A foreign/full order →
  `NoAvailableSpots`/`EmployeeAlreadyAssignedToOrder`. **(see also S7a — the take race below.)**
- **S3 (Notify/Start/Complete = assigned-to-caller only) — PASS (VERIFIED).** Each validator gates
  on `EmployeeIsAssignedToOrderAsync` (`order.AssignedEmployees.Any(oe => oe.EmployeeId == callerEmployeeId)`):
  `NotifyOnTheWay.cs:73-84`, `StartOrder.cs:89-100`, `CompleteOrder.cs:106-117` — plus the correct
  status precondition (Confirmed / Confirmed-or-OnTheWay / InProgress). Acting on a **foreign** order
  → `EmployeeNotAssignedToOrder`; **wrong-status** → `OrderNotConfirmed`/`OrderNotInProgress`.
  *Note (S3 existence-hiding):* these surface `EmployeeNotAssignedToOrder`, NOT NotFound — a partner
  who guesses an orderId learns "this order exists but isn't yours." This is **weaker** than the
  RevokeDevice 404-not-403 precedent, but **acceptable**: an assigned cleaner already legitimately
  sees the order (it can appear in their lists), and the message reveals only existence, not customer
  data. Logged as a low-severity hardening preference, not a blocker — the action is still denied.
- **S3 (note/issue update·delete scoped to the AUTHOR, not any assigned employee) — PASS (VERIFIED, the
  subtle one).** Both the assignment check AND an authorship check run: `UpdateOrderNote.cs:71-72`
  (`n.Id == NoteId && n.EmployeeId == employeeId`), `DeleteOrderNote.cs:59-60` (same),
  `UpdateOrderIssue.cs:68-69` (`i.Id == IssueId && i.ReportedByEmployeeId == employeeId`),
  `DeleteOrderIssue.cs:59-60` (same). A second cleaner on a shared job **cannot** edit/delete the
  first cleaner's note/issue — the author predicate makes it `note == null` → `NotFound`. (Today
  AssignedEmployees is size 1, so this is latent until shared jobs ship, but it is **correctly built
  now**.) GOOD.

### DECISION 2b — `GetPaged` employeeId read-scoping (S3/S4) — **CHANGES REQUESTED (the one real gap)**

**FINDING (S3/S4 over-read, MEDIUM — latent-to-live).** In `GetPagedOrders.Handler`, for a non-admin
(employee) caller, the query is built **purely from the client-supplied filter** — the
client-controlled `request.Filter.EmployeeId` flows straight into `OrderSpecification`
(`GetPagedOrders.cs:70` → `OrderSpecification.cs:67-70`: `x.AssignedEmployees.Select(...).Contains(EmployeeId)`).
**There is no server-side override pinning `EmployeeId == callerEmployeeId`, and no "row must be mine
OR have available spots" predicate on the base query.** The only non-admin guard is "return empty if
the caller has no employeeId" (`GetPagedOrders.cs:51-54`). The per-row branch (`:171-186`) blanks only
`CustomerName/Email/Phone/CustomerAddress` (full street) for orders the caller isn't assigned to — it
does **NOT** blank `CustomerAddressApproximate`, `CustomerAddressLatitude`/`Longitude` (the EXACT
geocoded coordinates), `EstimatedCleanerPay`, `TotalPrice`, `ConfirmationCode`, status, or times.

- **Concrete exploit (reachable today):** an authenticated partner (any Approved employee) calls
  `POST/GET .../Order/GetPaged` with `Filter.EmployeeId = <victimEmployeeId>` and
  `Filter.OrderStatuses = [Confirmed, InProgress, Completed]` (not Unassigned, no available spots).
  The spec returns **the victim's assigned-order rows** — the attacker is not assigned, so full PII is
  blanked, **but the exact customer coordinates, approximate address, confirmation code, price and the
  victim's pay estimate are returned**. That is a cross-employee location + confirmation-code +
  earnings leak. The victim's employeeId is low-entropy/discoverable (it ships in `AssignedEmployeeDto.EmployeeId`
  on any shared/visible order, and the Android client stores its own id in plaintext prefs).
- **Why it's reachable as designed:** the legitimate client convention (Android `OrdersListViewModel.kt:232-266`,
  which the iOS T-0307 port mirrors) is "the client sends its OWN stored employeeId for the My tabs."
  The backend trusts that id. A modified client / direct API call substitutes a foreign id. **The
  client is not authority** (Devices D8 precedent) — the server must enforce "mine."
- **Severity honesty (Gate 0):** MEDIUM. Full PII (name/email/phone/street) IS blanked, so this is not
  a full customer-record dump. But exact lat/lng + confirmation code + another employee's pay is a real
  cross-tenant-of-employees disclosure, exploitable today on the single-tenant path with just a JWT and
  a guessed employeeId. **This blocks the GetPaged contract**, not the iOS UI work — the iOS client can
  proceed on the action paths.
- **REQUIRED backend fix (binding):** in `GetPagedOrders.Handler`, for non-admin callers, **ignore the
  client `EmployeeId` and force the server-derived `callerEmployeeId`** when the request is a "mine"
  view (My Active / My Completed), AND for the Available view constrain rows to `HasAvailableSpots ||
  assigned-to-caller` so a non-assigned, no-spots row of another employee is never returned. Equivalent
  to how `isAdmin ? request.Filter?.CustomerName : null` already strips admin-only filters for non-admins
  (`GetPagedOrders.cs:66-68`) — apply the same "client cannot widen scope" rule to `EmployeeId`. Until
  fixed, also stop emitting coords/approximate-address/confirmation-code/pay on non-assigned rows
  (blank them in the same `:179-186` branch). **Verify with an integration test: caller A passing
  `EmployeeId=B` gets zero of B's exclusive (assigned, no-spots) rows.**

### DECISION 3 — S4 (no leak) / S5 / S7 / S8 / S10

- **S4 (DTO leak) — PASS with the GetPaged carve-out above.** `OrderItem`/`OrderListItem` carry **no
  `UserId`, no `TenantId`, no Stripe ids, no hashes**. Customer PII (name/email/phone/street) is the
  documented job-need for the **assigned** cleaner and is **blanked for non-assigned rows** on the list
  (`GetPagedOrders.cs:179-186`) and gated on `CanBrowseOrderAsync` for detail (only assigned, or
  available-spot offers, or admin — `GetOrderDetails.cs:44-49`, `OrderAccessService.cs:68-86`).
  `AssignedEmployeeDto.EmployeeId` is a documented intentional disclosure (the partner computes
  "am I assigned?" from it; low-value internal id, not a token). `isAssignedToCurrentUser` /
  `EstimatedCleanerPay` are caller-scoped (false/null for non-employees). The ONLY S4 issue is the
  non-assigned-row over-exposure in DECISION 2b (coords/approx/confirmation-code/pay) — fix there.
- **S5 (rate limiting) — PASS (VERIFIED).** Every side-effecting route carries
  `[EnableRateLimiting("auth")]` — the partitioned shared window (TakeOrder/Start/Notify/Complete/
  AddNote/UpdateNote/DeleteNote/ReportIssue/UpdateIssue/DeleteIssue, `OrderController.cs:42,55,68,81,
  163,189,202,215,228` etc.). Reads (GetPaged/GetById) are not rate-limited — acceptable (no side
  effect; tenant/role scoped).
- **S6 (logging) — PASS.** The handlers log only `order.Id` on email-send warnings (`TakeOrder.cs:229`,
  `StartOrder.cs:157`, `CompleteOrder.cs:263`) — no customer email/phone/name/address above Debug. iOS
  adds none (ADR-0019 spine).
- **S7 (idempotency / stale-client safety) — PASS (VERIFIED).** The lifecycle is naturally idempotent
  via status-precondition validators: re-tapping a stale action is a **clean business rejection, not a
  crash or a double side-effect**. Take on an already-taken/full order → `NoAvailableSpots` /
  `EmployeeAlreadyAssignedToOrder` (no double-assign). Notify/Start/Complete re-tap from the wrong
  status → `OrderNotConfirmed`/`OrderNotInProgress`. **Complete's money fan-out is guarded
  downstream:** `LoyaltyService.GrantForCompletedOrderAsync` (ledger check) + `ReferralService`
  (status check) + per-cleaner `CalculateOrderPay` (deterministic `MessageKeys.Pay(orderId, employeeId)`
  idempotency key) — the S7 references in the law. A stale double-complete is blocked at the
  InProgress-status gate before the fan-out anyway. Receipt generation is keyed
  `MessageKeys.Receipt(order.Id)` + guarded by `order.Receipt is null` (`CompleteOrder.cs:222-232`).
- **S7a (TakeOrder check-then-act race) — LATENT, note for backend.** `TakeOrder` validates
  `HasAvailableSpots` in the validator and assigns in the handler with **no atomic conditional
  UPDATE / unique constraint** on `(OrderId, EmployeeId)` spot-count — two concurrent takes of the
  last spot on a shared job could both pass `HasAvailableSpots` and both assign (TOCTOU, S7a class).
  **Dormant today** (single-cleaner jobs, `MaxEmployees == 1` in practice, and a double-take by the
  SAME caller is blocked by `NotAlreadyAssignedToEmployeeAsync`). Becomes live when shared jobs
  (`MaxEmployees > 1`) ship. Not a T-0307 iOS regression; flag for the backend owner alongside the
  shared-jobs work. Low priority now.
- **S8 (tenant isolation) — PASS today.** Order is tenant-scoped via the global EF filter; the spec
  reads go through `Set<T>()` (filter applies). The push/email/pay envelopes carry `order.TenantId`
  explicitly. No raw SQL / leaked IQueryable on these paths. The `GetByUserEmailAsync` employee
  resolution rides the tenant filter. (The standing latent RefreshToken multi-tenant note in
  `auth-sessions.md` is unrelated to the order surface.)
- **S9 (migration/DTO contract) — N/A for the iOS client.** No schema change. The DECISION-2b backend
  fix changes only handler logic, not the DTO shape, so no nswag/spec regen is forced — but if the fix
  also blanks coords on non-assigned rows, that is a **value** change, not a contract change (no regen).
- **S10 (soft-delete) — PASS.** `IsActive` is an exposed filter on the order list (`OrderFilter.IsActive`
  → spec `:37-40`); deactivated orders are hidden when the caller doesn't ask for them. No deactivated-row
  leak on the action paths (they load by id and gate on status/assignment).

### Binding rules (the iOS developer builds to these; the reviewer enforces them)

**RULE O1 — JWT-derived actor (BINDING, mirrors S1).** The iOS client MUST NOT send any employeeId on
a state-changing command — the action commands carry `orderId` (+ Complete's optional minutes/notes)
ONLY; note/issue commands carry the server-issued `noteId`/`issueId`. The acting employee is the JWT.
*Reviewer grep:* no `employeeId` field is added to any Take/Notify/Start/Complete/note/issue request
body the iOS client builds.

**RULE O2 — no-client-id-echo (BINDING, the Devices D8 analogue).** The iOS client MUST never send an
`orderId`/`noteId`/`issueId` it did not receive from its OWN `orderGetPaged`/`orderGetById` response.
No synthesized, guessed, or cross-screen-carried id. Action buttons act only on the currently-loaded
`OrderItem` and on note/issue rows from that order's own `orderNotes`/`orderIssues`. *Reviewer grep:*
every id fed to an action call traces to a field of a loaded list/detail model, never a literal/UUID/
free-form input.

**RULE O3 — `GetPaged` "mine" is server-truth; the client filter is a hint, not authority (BINDING).**
The iOS My-tab list MAY pass the caller's own stored employeeId as a filter (Android parity,
`OrdersListViewModel.kt:244,249`) — but the client MUST NOT rely on it for isolation and MUST NOT
expose a way to filter by an arbitrary employeeId. The **server** enforces "mine" (DECISION 2b fix).
The client gates the Take/Notify/Start/Complete affordances by `IsAssignedToCurrentUser` + status, but
**that gating is UX only — the backend validators are the authority** (verified above). *Reviewer note:*
do not let the iOS UI ship a foreign-employee filter input; the only employeeId the client ever sends
is its own.

**RULE O4 — stale-action UX (BINDING-lite).** On a backend rejection (`NoAvailableSpots`,
`EmployeeAlreadyAssignedToOrder`, `OrderNotConfirmed`, `OrderNotInProgress`, `EmployeeNotAssignedToOrder`)
the client shows a clean message and **refreshes the order** (the server state moved under it) — never
crashes, never optimistically double-fires. The action button is re-entry-guarded while a call is in
flight (the Devices `.submitting` parity).

### Required test (Gate 6)
- **TC-IOS-ORDERS-OWNERSHIP (red-first, client VM):** (a) a Notify/Start/Complete attempt on an order
  whose `isAssignedToCurrentUser == false` is **not initiated** by the client (button hidden/disabled),
  AND if forced, the backend rejection surfaces as a clean `EmployeeNotAssignedToOrder` message +
  refresh — never a double-fire/crash. (b) the client builds every action command from a loaded
  `OrderItem.id` / `orderNotes[].id` / `orderIssues[].id` (RULE O2) — assert no synthesized id path.
  (c) the My-tab list never sends an employeeId other than the caller's own (RULE O3).
- **TC-BE-ORDERS-GETPAGED-SCOPE (red-first, backend integration — gates the DECISION-2b fix):** caller
  A calling `GetPaged` with `Filter.EmployeeId = B` returns **zero** of B's exclusive (assigned,
  no-available-spots) rows, and any incidentally-returned non-assigned row carries **no** exact
  coords / approximate-address / confirmation-code / pay. Owned by the backend dev; blocks the GetPaged
  contract for go-live.

### Open follow-ups for the backend owner
1. **GetPaged read-scoping (DECISION 2b) — REQUIRED before T-0307 ships against this endpoint.** Force
   server `callerEmployeeId` for non-admin "mine" views + constrain Available to
   `HasAvailableSpots || assigned`; blank coords/approx/confirmation-code/pay on non-assigned rows.
   File as a backend ticket; the iOS UI work can proceed in parallel but the GetPaged contract is the
   blocker. (Pre-existing backend behavior, not an iOS regression — surfaced by T-0307.)
2. **TakeOrder TOCTOU (S7a) — LATENT.** Make the spot-claim atomic (conditional UPDATE / unique index)
   before shared jobs (`MaxEmployees > 1`) ship. Low priority on the single-cleaner path.
3. **(Hardening, optional)** consider returning NotFound instead of `EmployeeNotAssignedToOrder` on the
   action paths to match the RevokeDevice existence-hiding convention. Low severity (action still denied).

---

## 2026-06-27 — T-0307 Slice D BUILD-TIME VERIFICATION (Gate-SEC, security reviewer) — PASS (verified against uncommitted code on `phase/ios-phase4`)

**Verdict: PASS on the order-action security gate (O1/O2/O4 + TC-IOS-ORDERS-OWNERSHIP).** This is the
build-time verification of the order-action design ruled in sprint-12 §7.8 / the binding rules above.
The four lifecycle commands (take / notifyOnTheWay / start / complete) are now wired (detail
`StickyActionFooter` + Available `TakeButton` + Active row `SlideToConfirm`). Verified against the
**actual uncommitted source** (read, not assumed) and the test suite was **built and run on the
booted iPhone-17 simulator — 71/71 selected order tests passed, including all named ownership/guard
tests** (`xcodebuild test`, `** TEST SUCCEEDED **`, 2026-06-27 16:03).

### O1 — NO CLIENT employeeId on any lifecycle command — PASS (VERIFIED, wire-level)
- Generated command DTOs carry **only `orderId`** (Complete also `actualCompletionTimeMinutes` /
  `completionNotes`): `CleansiaPartnerApi/Models/TakeOrderCommand.swift:13-19`,
  `NotifyOnTheWayCommand.swift:13-19`, `StartOrderCommand.swift:13-19`,
  `CompleteOrderCommand.swift:13-23`. `CodingKeys` on each confirm **no `employeeId` is serialized** —
  there is no client actor field on the body at all.
- Construction sites pass orderId only: `Data/PartnerOrderClient.swift:79` (`TakeOrderCommand(orderId:)`),
  `:86-87` (`NotifyOnTheWayCommand(orderId:)`), `:93` (`StartOrderCommand(orderId:)`),
  `:99-104` (`CompleteOrderCommand(orderId:, actualCompletionTimeMinutes:nil, completionNotes:nil)`).
- Tree-wide grep: **zero** `employeeId` on any Take/Notify/Start/Complete path. The only order-surface
  `employeeId` is the `GetPaged` read filter (O3, caller's OWN id) — `PartnerOrderClient.swift:58`,
  resolved via `currentEmployeeId()` (`:41-49`, JWT-truth surrogate). The acting employee is the JWT,
  server-side. CONFIRMED.

### O2 — NO ID-ECHO (acts only on the client's OWN list/detail id) — PASS (VERIFIED)
- Detail VM acts on the constructor `orderId` only (the id the detail route was opened with, itself a
  list-row id): `OrderDetailViewModel.swift:71,75,79,84` all use `self.orderId`; the comment at `:88-90`
  documents the O2 intent and `getById` at `:91` reuses the same id. The footer callback passes an
  **enum action, no id** (`OrderDetailView.swift:54`, `dispatch(action)`).
- List VM inline action acts on `order.id` from the loaded row only:
  `OrdersListViewModel.swift:113-118` (`runInlineAction(_:on:)` → `let orderId = order.id`),
  `:135-143` (`command(for:orderId:)`). The row `order` is bound from `ForEach(orders)` /
  `ForEach(rows, id:\.id)` over the loaded pane (`OrdersListContent.swift:78-83` Available,
  `:222-230` Active). Navigation to detail passes a row id (`OrdersListView.swift:34-40`).
- Grep confirms **no** `UUID()`/`uuidString`/free-form-id-`TextField`/`generateId` on any order action
  path — no synthesized, guessed, or cross-screen-carried id reaches a command. CONFIRMED.

### O3 (carried over, client side) — PASS. The "mine" panes pass ONLY the caller's own id
- `OrdersQueryBuilder` (`OrdersListLogic.swift:77-107`): Available → `employeeId: nil, isUnassigned:true`;
  Active/History → `employeeId: ownEmployeeId` (the caller's own). Resolved only via
  `resolveOwnEmployeeIdIfNeeded` → `currentEmployeeId()` (`OrdersListViewModel.swift:197-207`). **No
  foreign-employee filter input exists anywhere in the list UI.** (Server-truth enforcement remains the
  DECISION-2b backend fix — see follow-up #1 / T-0339; unchanged & unaffected by this slice — it is a
  read-scoping fix, not an action.)

### O4 — CLEAN REJECT + REFRESH + RE-ENTRY GUARD — PASS (VERIFIED)
- **Clean reject + refresh, screen kept:** detail `run(...)` failure branch surfaces the API error,
  sets `actionState=.error`, invalidates + re-fetches the detail so a stale "takeable" state
  self-corrects (`OrderDetailViewModel.swift:124-131`); list failure branch snackbars + background-
  refetches the current pane (`OrdersListViewModel.swift:126-132`). No crash, no client-side
  double-assign.
- **Exactly one mutation in flight:** detail guards on `actionState.isSubmitting`
  (`OrderDetailViewModel.swift:107`); list guards on `inFlightActionOrderId == nil`
  (`OrdersListViewModel.swift:114`). The `FakePartnerOrderClient` suspension gate
  (`Tests/FakePartnerOrderClient.swift:24-25,48-54`) proves the guard **actually blocks** a concurrent
  second action — `testReentryGuardDropsASecondActionWhileSubmitting` (detail) and
  `testInlineActionReentryGuardDropsSecond` (list) hold one mutation mid-flight, fire a second, and
  assert `commands.count == 1`. Both PASS.

### REQUIRED TEST — TC-IOS-ORDERS-OWNERSHIP — EXISTS + PASSES
- Detail: `OrderDetailViewModelTests.swift:206-219` `testCommandsCarryOnlyTheLoadedOrderIdNoEmployeeId`
  (O1 + O2: command carries only the loaded order id). PASS.
- List: `OrdersListViewModelTests.swift:303-313` `testInlineActionActsOnlyOnRowIdNoEmployeeId`
  (O1 + O2: command carries only the row's own id). PASS.
- Supporting O3 coverage: `testAvailablePaneSendsNoEmployeeIdUnassignedTrue` (`:50-57`),
  `testActivePaneSendsOnlyOwnEmployeeId` (`:59-66`), `testHistoryPaneSendsOnlyOwnEmployeeId` (`:68-73`).
  All PASS.
- O4 coverage: detail `testActionFailureSurfacesErrorAndKeepsScreen` (`:156-167`), list
  `testInlineActionFailureSnackbarsAndRefreshes` (`:289-299`). All PASS.

### completeBlocked gate — confirmed CLIENT-UX-ONLY; the SERVER is authority
- `OrderPrimaryAction.action(...):48-49` resolves InProgress&mine → `.complete` if `hasAfterPhotos`
  else `.completeBlocked`; `completeBlocked.orderAction == nil` (`OrderPrimaryAction.swift:26`) so it
  **dispatches nothing** (`OrderDetailViewModel.swift:66`, `OrdersListViewModel.swift:141`). It only
  renders a disabled hint (`StickyActionFooter.swift:55-56,75-88`). A bypassing client that forced
  `.complete` still hits the backend CompleteOrder ownership+status+after-photos check (verified in the
  §7.8 backend gate above, DECISION 2 / S3). The list inline path resolves InProgress with
  `hasAfterPhotos: true` by design (the field isn't on the list DTO) and **relies on the server guard**
  as the safety net (`OrdersListViewModel.swift:104-109`). Acceptable — server is authority.

### Spine / contract
- **No new token/header/401/Authorization path** on the order surface (grep clean) — the commands ride
  the shared spine (ADR-0019). CONFIRMED.
- **S9** — no DTO contract change in this slice (commands were already generated); no nswag regen forced.

### New gaps from this slice: NONE.
The standing DECISION-2b `GetPaged` backend over-read (follow-up #1, ticket **T-0339**) is **unchanged
and unaffected** by Slice D — it is a read-scoping fix on a different (read) path; this slice adds only
action wiring, which is server-scoped and safe. The TakeOrder TOCTOU (S7a, follow-up #2) and the
NotFound-vs-EmployeeNotAssigned hardening (follow-up #3) are likewise unchanged.

**Build-time evidence:** `xcodebuild test -scheme CleansiaPartner` (iPhone 17 sim, id
04753F32-…) — `Executed 71 tests, with 0 failures`; the 5 named ownership/guard/reject tests re-run
in isolation → `** TEST SUCCEEDED **`. Verdict: **PASS — Slice D order-action gate clears O1/O2/O4 +
TC-IOS-ORDERS-OWNERSHIP.** No code edits made (audit-only).
