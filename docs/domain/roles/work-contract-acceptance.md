# WorkContractAcceptance (+ WorkContractAcceptor, WorkContractFactsBuilder) (ADR-0068, accepted 2026-09-20)

**Responsibility (one sentence):** Be one cleaner's acceptance of the contract for work for **one seat
of one job** — which exact text row they read (hence the document, its version, the language and the
content hash by one join), when, from where, and the job facts as shown to them at that instant —
written only by the cleaner's own act, append-only, bound to the seat so a re-take is a second
contract and an admin re-add is none, pseudonymised on the cleaner's erasure and kept as long as the
order, so that a claim against a cleaner for a job is answered from a record and not an inference.

> Introduced by **[ADR-0068](/decisions/adr-0068)**. `Cleansia.Core.Domain/Contracts/WorkContractAcceptance.cs`
> (`: TenantAuditable` — stamped with the **order's** operator) with its read shape
> `WorkContractAcceptanceRow`; the writer `Services/WorkContractAcceptor.cs`; the facts
> `Features/Orders/WorkContractFacts.cs` built by `Services/WorkContractFactsBuilder.cs`. The order
> carries its half: **`Order.WorkContractDocumentId`** — the contract text this job was offered under,
> set once by `OrderFactory` at booking from the address's market, read by the preview, the acceptor
> and the take's last rule. Table `WorkContractAcceptances`: `UNIQUE (OrderEmployeeId)`, FKs `Orders`
> and `LegalDocumentTexts` (Restrict), `OrderEmployeeId` and `EmployeeId` bare scalars (the seat is
> hard-deleted by the next drop, the cleaner is anonymised on erasure — the row outlives both).

## Collaborators

- **`Order.WorkContractDocumentId`** — the text the customer booked under. `OrderFactory.CreateAsync`
  resolves `ILegalDocumentResolver.ResolveInForceAsync(LegalDocumentType.WorkContract, address.CountryId)`
  beside the pay-coverage gate and **throws** when nothing is in force (an order no contract can form
  on is not booked; the recurring materialiser reaches the factory without `CreateOrder`'s validator).
  Nullable in the schema because `Order.Create` has 153 callers, 152 in tests; a null is a fixture,
  not a state — the take refuses it, nothing resolves it lazily. Fixed per order at the offer: a new
  version of the text applies to orders booked from its date and changes nothing on an order already
  booked.
- **`LegalDocument` / `LegalDocumentText`** ([legal-document](./legal-document)) — the text is a
  **customer-audience** document, type `WorkContract`, seeded from
  `Seed/Legal/customer/work-contract/{ISO3|any}/{yyyy-MM-dd}/{cs,en,sk,uk,ru}.md` (today `any/2026-09-20`,
  a draft template). It may say *"the job identified by the order number, date, time window, price and
  location shown to you at acceptance"* and use `{{currency}}`; it may **not** carry a figure, a name,
  an address or a date (`check-booking-policy-parity.mjs` walks the folder). The row names the **text
  row** — a Czech reader and a Ukrainian reader of one version agreed to two different strings, and
  the record says which; `ContentHash` comes by join.
- **`TakeOrder`** — the inline act. `Command(OrderId, AcceptedWorkContractTextId)`: the text-row id
  **is** the tick (null is *not accepted*, a value is *"I read this text and I accept it"*). Two rules
  in the one ordered chain: blank → `contract.not_accepted` **before** existence (depends on nothing
  about the order, so it can leak neither existence nor the hold); the id names a text of the
  **order's** document → `contract.text_mismatch` **last** (every refusal ahead of it is a better
  answer, and an order with no document matches no text). Between `AddAssignedEmployee` and the
  handler's own commit the acceptor stages the row, so the seat, the status row, the acceptance and its
  audit row are one transaction and a seat-race loser leaves no row.
- **`AcceptWorkContract`** — the standalone act for a seat an administrator formed
  (`AdminReassignOrder` writes no acceptance: an admin cannot accept on the cleaner's behalf). One
  chain: blank → `contract.not_accepted` → `order.not_found` → the caller holds a seat →
  `order.employee_not_assigned` → not cancelled / not completed (`take_order.already_*` — **any
  not-over order**, so a cleaner placed on an in-progress job can accept before completing) → the
  text belongs to the order's document → `contract.text_mismatch`. A seat that already has its row
  answers success with that row and writes nothing; a concurrent double tap is arbitrated by the
  unique index at commit.
- **`StartOrder` and `CompleteOrder`** — the gates: `HasAcceptedWorkContractForSeatAsync` (a row whose
  `OrderEmployeeId` is the caller's **current** seat on this order) → `contract.acceptance_required`,
  placed right after `EmployeeIsAssignedToOrderAsync` on both (a non-assignee still answers
  `order.employee_not_assigned` and learns nothing). Both, because on a two-seat crew the second
  cleaner never starts (the order is already `InProgress`) but may complete. `NotifyOnTheWay` is not
  gated — travel is not the work. Two keys for one fact on purpose: `not_accepted` is a client bug
  shown as an error; `acceptance_required` is a product state the app answers by opening the sheet.
- **`WorkContractAcceptor.StageAsync(order, seat, textId)`** — the one writer, two callers. Loads the
  text with its document (`ILegalDocumentRepository.GetByTextIdWithTextsAsync`), **throws** on a text
  not of the order's document (the validators are the gate; a mismatch here is a programming error,
  never a row), builds the facts, reads `IRequestMetadataProvider.{IpAddress, DeviceLabel}`, the
  **session's signed `device_id` claim** for the device (never the `X-Device-Id` header — the client's
  word alone on a signed-in act), `IHostAudienceProvider.Audience`, creates the row and one
  `EmployeeActionAudit(ContractAccepted)`, pins both to `order.TenantId`, and does **not** commit.
- **`WorkContractFactsBuilder.BuildAsync(orderId)`** — one `AsNoTracking` projection shared by the
  preview and the acceptor so the screen and the row cannot differ: `orderNumber`,
  `cleaningDateTimeUtc` + `estimatedMinutes` (the window), `totalPrice` + `currencyCode` (the
  customer's price — Q-WC-01), `locationApproximate` (*"Praha · 120"*, the same
  `BuildApproximateAddress` the board shows), `countryId`, `rooms`, `bathrooms`, `services` and
  `packages` as `{ id, name }`, `extraSlugs`. Never off the take handler's tracked aggregate (its
  includes are the seat-race arbiter's). `WorkContractFactsPiiGuardTests` walks every member name
  against the archive guard's token list — a `customerName` is a red build.
- **`IWorkContractAcceptanceRepository`** — `Add`; `GetForSeatsAsync` (the order detail),
  `GetForOrdersAsync` (the customer's export, the incident file), `GetByEmployeeIdNoTrackingAsync`
  (the cleaner's export), `GetByIdIgnoringTenantAsync` (the read keyed on the acceptance) — all **past
  the tenant filter**, because the row is stamped with the order's operator and the order's customer
  may be booked across the border; each caller re-pins on an authorised order or the subject's own id.
  `AnyForSeatAsync` (the gates) and `PseudonymiseExpiredAsync` (the per-company sweep) read through
  the filter. `PseudonymiseForEmployeeAsync` is a **tracked** load past the filter riding the erasure's
  single commit. The inherited `Remove` / `Deactivate` are pinned unused
  (`WorkContractAcceptanceImmutabilityTests`). → [S8](/architecture/security-rules#s8-tenant-isolation-correctness)
- **`GetWorkContractPreview.Query(OrderId, Language)`** (partner hosts, `CanTakeOrder`) — the order's
  document in the requested language (or the fallback), `ContentHtml` rendered with the **order's**
  currency code, the facts from the builder, `Acceptance: null`; `legal.document_not_found` for an
  order with no document. Readable exactly as the board's floor and the browse gate define it
  (`OrderSpecification` — on the crew, or offerable with a takeable seat, and not held from the
  caller), so a held order is a missing order to everyone but its beneficiary and a cancelled,
  finished, full or unpaid-card job the caller is not on discloses nothing.
- **`GetWorkContract.Query(AcceptanceId, Language)`** (all five hosts) — the same `WorkContractDto`
  with `Acceptance` filled (`AcceptedOn`, `DocumentVersion`, `AcceptedLanguage`, `OrderEmployeeId`,
  `EmployeeId`), the facts **from the stored `FactsJson`**, the text of the accepted document in the
  requested language when it has one, else the accepted text. Keyed on the acceptance so the dropped
  contract of a re-take stays readable and no employee id travels on the wire. Access is derived from
  the row: its order must exist for the caller (owner-pinned for a customer, the company's for staff)
  **and**, for a cleaner, the row's `EmployeeId` must be theirs — an ex-crew cleaner keeps reading
  what they accepted; anyone else answers `order.not_found`.
- **`OrderItem.WorkContractAcceptances`** — one `WorkContractAcceptanceDto(Id, OrderEmployeeId,
  EmployeeId, AcceptedOn, DocumentVersion, Language)` per **current seat** that has a row; the client
  pairs it with the crew entry by `OrderEmployeeId == AssignedEmployeeDto.Id` and takes the name from
  there (already audience-masked — this DTO carries none). A crew entry with no match **is** the
  pending state. `OrderPiiRedaction.RedactForBrowsingCleaner` empties it.
- **The record readers** ([ADR-0068 D5](/decisions/adr-0068)) — `employee.order.contract_accepted`
  on the timeline and in the incident-file trail with **no new reader**; the incident file's
  *Contracts for work* section (seat, cleaner, instant, version, language, client, IP, device label,
  device id, the flattened facts); the cleaner's GDPR export (the rows in full) and the customer's
  (each order's `workContractDocumentVersion` and its acceptances' instant / version / language — no
  employee id); `GdprExportEvidence.workContractAcceptanceCount`; the archive's
  `books/work-contract-acceptances.jsonl` (no IP / device — the record guard forbids the tokens) and
  `workContractDocumentId` on the orders stream.
- **`DataRetentionBackgroundService`** — `retention.work_contract_metadata.years` (default **3**, min
  1, per company): blanks the IP address, device label and device id on rows accepted before the
  window, in batches, per company; **never deletes**. The row itself is books and is kept with the
  order.
- **The clients** — partner web `work-contract-dialog` (modes `take` / `accept` / `read`; a tick +
  *Accept and take the job*), partner Android / iOS `WorkContractSheet` (the same three modes; the
  text in a shared JavaScript-off HTML view under a *Swipe to accept the contract for work* slider);
  customer web `/work-contract` (the public text), `/orders/:orderId/contract/:acceptanceId` (the
  accepted text with the frozen facts), the customer Android `WorkContractScreen` / iOS
  `WorkContractView`; the admin order detail's per-crew *accepted {date}, v{version}* / *contract
  pending* line with **Read** (and, for an Administrator, the accepted text row's SHA-256 through
  `AdminGetLegalDocument`).

## Does NOT know

- **The customer.** No name, no street, no user id on the row: the customer resolves **through the
  order** at render time and reads as the anonymised marker after erasure. The coarse location
  (*"Praha · 120"*) is kept on purpose as a term of the contract — it is the pre-acceptance disclosure
  the board already makes (Q-WC-07 asks the lawyer/DPO to confirm).
- **The cleaner's name or company.** Ids only; the given name comes from the crew entry on the order
  detail (Q-WC-03).
- **Whether the seat still exists.** The seat row says; this row is history. A drop, a cover swap, a
  rejection or a reassignment leaves the row — *accepted at 09:00 / dropped at 09:40* is the record a
  claim is answered from. The cover swap writes no employee-side row of its own; the displacement is
  inferable from the successor's acceptance of the same seat ordinal.
- **Whether the document is still the one in force.** It points at the one the order was booked
  under; there is no stale-version case and no re-acceptance on a new version.
- **A cleaner an administrator placed.** Nothing is written for them — the gates, the job-detail
  banner and the admin's *contract pending* line are the nudges; a second crew member who neither
  starts nor completes works under no acceptance (the stated residual; the structural close is
  Q-WC-06).
- **Whether the sheet was really read.** The echo proves *which text*, not that this session fetched
  this order's preview; a per-preview HMAC is not built — the platform records the act, not the
  reading. No scroll-to-end gate.
- **A signature provider, a PDF, a guest read.** Signi is the upgrade path (`SignatureEnvelopeId` +
  `SignedOn`, nothing above changes); the PDF is a follow-up (a dispute bundle is the incident file's
  section plus `AdminGetLegalDocument`'s hash until then); `LookupOrder` has no crew and no contract.

## Invariants a reviewer checks

- **Private setters, `Create` + `Pseudonymise` only; `Pseudonymise` nulls exactly the trio**
  (`WorkContractAcceptanceTests`, `WorkContractAcceptanceImmutabilityTests`); no member of the facts
  record is named with an archive-guard token (`WorkContractFactsPiiGuardTests`);
  `IX_WorkContractAcceptances_OrderEmployeeId` is **unique** in the emitted DDL.
- **The stamp and the seed:** `OrderFactory` stamps the document from the address's market and throws
  when nothing is in force (`OrderFactoryWorkContractStampTests`); the seeder lands three customer
  documents on a real Postgres (`LegalDocumentSeedAndReadTests`); `GET api/Legal/GetDocument?type=WorkContract`
  answers `200` anonymous on the customer hosts.
- **The take:** no id → `contract.not_accepted` and no seat, the same for a held and a missing order;
  a text of another document → `contract.text_mismatch` after every other refusal (a full order still
  answers `no_available_spots`); a good id → one seat, one `Confirmed` row, one acceptance naming the
  echoed text and the new seat, one `ContractAccepted` audit row, all in the take's commit; the
  seat-race loser leaves zero rows (`TakeOrderWorkContractTests`, Postgres); a cover take leaves the
  displaced cleaner's row.
- **The gates:** an admin-placed cleaner is refused at Start and at Complete with
  `contract.acceptance_required` after `order.employee_not_assigned` would have fired; they can accept
  on an in-progress order; a second accept writes nothing; a take → drop → admin re-add is refused
  again (a new seat, no row) (`AcceptWorkContractHandlerTests`; `StartOrderWorkContractGateTests` and
  `CompleteOrderWorkContractGateTests`, which share the file `WorkContractGateTests.cs` — both gates,
  the two-seat case included).
- **The reads:** `OrderItem.workContractAcceptances` lists current seats only, is empty for a
  browsing cleaner (`OrderRedactionSurfaceTests`); `GetWorkContract`: the order's customer, the named
  cleaner (after dropping) and an admin → `200`; another customer or another cleaner →
  `order.not_found` (`GetWorkContractHandlerTests`); a stranger's preview of a held order →
  `order.not_found` (`GetWorkContractPreviewHandlerTests`).
- **Erasure, retention, export, archive:** the cleaner's erasure blanks the trio riding the single
  commit and a commit throw leaves it intact (`WorkContractAcceptanceErasureTests`, on an in-memory
  SQLite context); the sweep blanks
  one company's rows and not another's, never deletes, refuses a window below 1
  (`WorkContractAcceptanceRetentionTests`, `WorkContractAcceptanceMetadataSweepTests`); the exports
  and the bundle carry what D5 says and nothing more (`CompanyArchiveRecordGuardTests`,
  `CompanyArchiveBundleTests`).
- **Routes:** the two partner routes `401` / `403` / `200`; `GetWorkContract` `401` / `200` on all
  five hosts; every one in the `interactive` window (`WorkContractRouteTests`,
  `RateLimitCoverageGuardTests`).
- **Greps:** `AcceptedWorkContractTextId` in `Cleansia.Core.AppServices` → the two commands only
  (`TakeOrder.cs`, `AcceptWorkContract.cs`; the acceptor takes the id as `textId`);
  `WorkContractAcceptance.Create` → the acceptor only; `SetWorkContractDocument(` in
  AppServices → `OrderFactory` only; `CustomerName` / `Street` in `WorkContractFacts.cs` → empty;
  `requestMetadataProvider.DeviceId` in `WorkContractAcceptor.cs` → empty;
  `Seed/Legal/customer/work-contract/any/2026-09-20/` holds five files.
