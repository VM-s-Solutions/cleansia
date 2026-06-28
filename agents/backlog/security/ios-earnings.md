# Security findings — iOS partner Earnings / Invoices / PeriodPay (T-0309: employeePayrollGetPeriodPays + getPagedInvoices + getInvoiceById + downloadInvoice→PDF)

## 2026-06-27 — T-0309 Earnings read-scoping gate (Gate-SEC, security reviewer) — PASS-the-design (binding iOS client rules) + ONE latent backend S5 note (rate-limit), NO backend read-scoping fix needed

**security_touching: YES.** **Verdict: PASS-the-design for the iOS client (own-data financial read,
carries bank/payment PII); the backend read-scoping is VERIFIED owner-pinned on all four handlers
(this is the read-side analogue of T-0339, but UNLIKE T-0339 the payroll handlers ALREADY pin to the
JWT caller — no T-0339-class over-read here).** One residual is a **latent S5 gap** (the payroll
controller carries no `[EnableRateLimiting]`), tracked separately, not a blocker for the design.
Scope = the read-scoping / PII security gate of T-0309 only — the ARCHITECT rules nav / PDF preview /
number-format in parallel; I stay out of those. Cross-ref: sprint-12 §7.11 sub-note.

T-0309 is **read-only over the existing `EmployeePayrollController`** — greenfield on the iOS side
(no committed iOS earnings code on `phase/ios-phase5` yet), so the rules below are what the developer
builds to and the reviewer enforces, not findings against shipped iOS code. The **backend payroll
surface was traced AND a unit gate run on this Mac** (`GetPeriodPaysOwnershipTests` — 4 passed).

### DECISION 1 — IS T-0309 security_touching? **YES.**
It reads the caller's OWN financial data over an authenticated partner API: `GetPeriodPays`
(per-period pay totals + per-order pay breakdown), `GetPagedInvoices` / `GetInvoiceById` (invoice
amounts + CZ/SK payment-routing identifiers), and `DownloadInvoice` → a **QuestPDF PDF that contains
bank/payment PII** (`VariableSymbol` / `SpecificSymbol` / `PaymentReference` / `BankTransferNote`).
Gate 3 applies (endpoint + resource-by-id + response DTO + financial PII + file download +
rate-limited-route surface). It is the **read-side analogue of the T-0339 `GetPagedOrders`
read-scoping gap** — the same "does the backend trust a client-supplied `employeeId` filter?" question.

### What was read (trace base — backend reachable on this Mac, gate run green)
- Controller: `Web.Mobile.Partner/Controllers/EmployeePayrollController.cs` (4 read routes:
  `GetPagedInvoices`, `GetInvoiceById/{invoiceId}`, `GetPeriodPays`, `DownloadInvoice/{invoiceId}`).
- Handlers: `Core.AppServices/Features/EmployeePayroll/{GetPeriodPays,GetPagedInvoices,GetInvoiceById,
  DownloadInvoice}.cs`.
- Authz seam: `Core.AppServices/Authentication/OrderAccessService.cs` (`GetCallerEmployeeIdAsync` →
  `ResolveCallerEmployeeIdAsync`: employee-id claim → fallback `GetByUserEmailAsync(jwt email)`).
- DTOs: `Features/EmployeePayroll/DTOs/{PeriodPaySummaryDto,EmployeeInvoiceDto,
  EmployeeInvoiceDetailDto,OrderEmployeePayDto}.cs`; filter `Filters/EmployeeInvoiceFilter.cs`.
- Entity: `Core.Domain/EmployeePayroll/EmployeeInvoice.cs` (`ITenantEntity`; bank-PII fields).
- Existing gate (run green here): `Cleansia.Tests/Features/EmployeePayroll/GetPeriodPaysOwnershipTests.cs`.
- Rate-limit shape: `Cleansia.Config/RateLimiting/RateLimitPolicies.cs` (the `auth`/`interactive`
  partitioned policies + the `GetNoLimiter("authed-global")` global bypass for authenticated callers).

### DECISION 2 — SERVER-SIDE OWN-SCOPING (the load-bearing check, the T-0339 analogue) — VERIFIED, ALL FOUR PINNED

The headline: **unlike `GetPagedOrders` (T-0339), all four payroll handlers ignore/override the
client `employeeId`/`filterEmployeeId` and pin to the JWT caller for non-admins.** This is exactly
the fix T-0339 had to add — payroll already has it.

- **S1 (actor = JWT, never trusted from the request) — PASS (VERIFIED).** Every handler resolves the
  caller via `IUserSessionProvider` role + `IOrderAccessService.GetCallerEmployeeIdAsync` (JWT). No
  client-supplied id is trusted for a non-admin.
- **`GetPeriodPays` — PASS (VERIFIED + unit-gate green).** `GetPeriodPays.cs:52-61`: non-admin →
  `callerEmployeeId = GetCallerEmployeeIdAsync()`; if empty OR `!= query.EmployeeId` → fail
  `EmployeeNotFound` **before** any repo read. A foreign `employeeId` → `EmployeeNotFound` (matches
  the Android note). Admin path (`:53`) skips the check. Proven by `GetPeriodPaysOwnershipTests`
  (4 tests, ran here, all pass): foreign id rejected, unresolvable session rejected, own succeeds,
  admin broad.
- **`GetPagedInvoices` — PASS (VERIFIED) — the direct T-0339 contrast.** `GetPagedInvoices.cs:33-43`:
  for a non-admin the handler **overwrites** `employeeIdFilter` with the server-resolved caller id
  (`employeeIdFilter = await GetCallerEmployeeIdAsync()`) and **ignores `request.Filter?.EmployeeId`
  entirely**; if the caller has no resolvable employee id it returns an EMPTY page. So a partner who
  passes `Filter.EmployeeId=<victim>` gets their OWN invoices, never the victim's. This is precisely
  the pin T-0339's `GetPagedOrders` was MISSING — here it is already present. **No T-0339-class
  over-read of another employee's invoices / amounts / payment-refs.** (Admin keeps the broad
  client filter, `:34` + `:36`.)
- **`GetInvoiceById` — PASS (VERIFIED).** `GetInvoiceById.cs:57-66`: after loading by id, a non-admin
  whose `invoice.EmployeeId != callerEmployeeId` → `InvoiceNotFound` (NotFound, not Forbidden —
  S3-correct existence-hiding). A foreign `invoiceId` cannot return another employee's invoice detail.
- **`DownloadInvoice` — PASS (VERIFIED).** `DownloadInvoice.cs:49-58`: same owner check BEFORE the
  blob is fetched/streamed — a non-admin foreign `invoiceId` → `InvoiceNotFound`; the PDF bytes are
  never read from blob nor returned. The owner gate sits between the existence load and the blob
  download, so a foreign id can't even cause a blob fetch.

**Conclusion: NO backend read-scoping follow-up ticket required for T-0309.** (Contrast: T-0307's
gate had to file T-0339 because `GetPagedOrders` trusted the client filter; the payroll equivalents
do not.)

### DECISION 3 — INVOICE PDF PII HANDLING (iOS client side) — BINDING RULES

The PDF carries bank/payment PII (`VariableSymbol`/`SpecificSymbol`/`PaymentReference`/
`BankTransferNote`) and lands in the iOS caches dir for the QuickLook preview.

- **(a) Cache cleanup — REQUIRED (binding).** The downloaded PDF MUST be written to an app-private
  location and **explicitly deleted after the QuickLook/preview is dismissed** — do NOT leave the
  PII-bearing PDF sitting in `Caches/` for the OS to evict whenever it likes. Acceptable shapes:
  (i) write under `FileManager.default.temporaryDirectory` (or a dedicated `Caches/invoices/`
  subdir) and `try? FileManager.default.removeItem(at:)` in the preview controller's dismiss /
  `onDisappear`; or (ii) hold the bytes in memory + a short-lived temp file removed on dismiss.
  Rationale: an unencrypted backup, a shared-device snapshot, or a forensic dump should not retain
  another person's bank-routing data longer than the view that needed it. The architect owns the
  *preview mechanism* (QuickLook vs in-app PDFKit); SECURITY owns the *cleanup-is-mandatory* rule.
- **(b) PrivacyInfo.xcprivacy — NO new collected-data-type entry required for this download.**
  Per AR-PRIV-1 / ADR-0016, `PrivacyInfo.xcprivacy` declares (i) **required-reason API** reason
  codes and (ii) the **App-Privacy nutrition-label** data-collection/tracking categories.
  Downloading and displaying the user's OWN invoice to that same user is **not** "data collection"
  in the App-Store-privacy sense (no collect-and-transmit to us or a third party; no tracking) — it
  is a server round-trip of the caller's own record, the same class as fetching their own profile.
  So **no `NSPrivacyCollectedDataType` entry is added for the invoice PDF** (contrast T-0308, which
  added `NS*UsageDescription` purpose strings because the camera/library are device-capability
  prompts — a download is neither). **Required-reason caveat (carry-forward, not new):** if the
  iOS code reads file timestamps / disk space while managing the cached PDF, the relevant
  required-reason API code (e.g. `NSPrivacyAccessedAPICategoryFileTimestamp` /
  `...DiskSpace` / `...SystemBootTime`) must already be in the manifest — that is the standing
  AR-PRIV-1 manifest obligation, audited once for `CleansiaCore`, NOT a T-0309-specific addition.
  Net: T-0309 adds **no** manifest entry; it inherits the existing AR-PRIV-1 audit.

### DECISION 4 — S4 (DTO leak) / S5 (rate limit) / enumeration

- **S4 (DTO leak) — PASS.** The surfaced DTOs carry only the caller's OWN fields. `EmployeeInvoice`
  is `ITenantEntity` and carries **no IBAN / raw bank-account number** — the only payment fields are
  `VariableSymbol`/`SpecificSymbol`/`PaymentReference`/`BankTransferNote` (CZ/SK payment-routing
  identifiers), which ARE the caller's own and reach the client only behind the per-handler owner
  gate (S3). `EmployeeName` is the caller's own. There is no `TenantId`, no `UserId`, no other
  employee's name/email/phone, no Stripe id, no hash. `PdfBlobName`/`PdfGenerationError` are
  operational (own invoice), acceptable. **No cross-employee field leaks.**
- **S5 (rate limit) — LATENT GAP (not a T-0309 blocker; tracked).** `EmployeePayrollController`'s
  four routes carry **NO** `[EnableRateLimiting]`, while every sibling controller on the same host
  (`OrderController`, `EmployeeController`, `DeviceController`, `GdprController`) carries
  `[EnableRateLimiting("auth")]`. And the global limiter (`RateLimitPolicies.cs:152-156`) returns
  `GetNoLimiter("authed-global")` for authenticated requests — so an authenticated partner hits
  these payroll reads with **zero** throttle. This matches the S5 note ("the Partner payroll
  controllers" verified-uncovered, tracked as **BSP-4d**). It is a read-only enumeration/abuse
  surface, NOT a money/email side-effect, and the owner gate already blocks cross-employee reads,
  so severity is LOW/latent — but it should get the `interactive` (or `auth`) window when BSP-4d is
  worked. **iOS cannot fix this; it is a backend hardening item.** Recorded here, folded into BSP-4d
  (no new ticket spun for T-0309).
- **Enumeration (invoiceId / payPeriodId guessing) — MITIGATED.** Because every by-id handler
  re-checks `invoice.EmployeeId == callerEmployeeId` (or `query.EmployeeId == caller`) before
  returning, guessing a foreign `invoiceId` / `payPeriodId` yields `InvoiceNotFound` /
  `EmployeeNotFound` — no data, no existence-confirmation beyond "not yours." Invoice ids are GUID
  suffixes (`INV-yyyyMM-<5 hex>` is the human number; the entity Id is a GUID), so they are not
  trivially enumerable; the owner gate is the real guarantee, not the id shape. The missing
  rate-limit (S5/BSP-4d) is the only thing that makes brute enumeration cheap — another reason to
  close BSP-4d for this host.

### BINDING iOS CLIENT RULES (what the developer builds to / the reviewer enforces) — E1–E4
- **E1 — own-server-derived id ONLY.** The earnings/invoices screens pass the caller's OWN
  `employeeId` resolved via `currentEmployeeId()` / `employeeGetCurrentEmployee` (the T-0307 O3
  precedent) into `getPeriodPays(employeeId, ...)` / `getPagedInvoices(filterEmployeeId, ...)`.
  NEVER take the employeeId from screen input, a deep link, a list row of someone else, or a
  remembered foreign value. (The backend would reject a foreign id anyway — E1 keeps the client
  honest and avoids needless `EmployeeNotFound` round-trips.)
- **E2 — no foreign-id echo.** Never echo back an `employeeId` / `payPeriodId` / `invoiceId` that
  did not originate from the caller's OWN resolved id or the caller's OWN returned list. The client
  must not construct a request from an id it received out-of-band.
- **E3 — download own invoices only.** `getInvoiceById` / `downloadInvoice` are called ONLY with an
  `invoiceId` taken from the caller's OWN `getPagedInvoices` result (or the `PeriodPaySummary.InvoiceId`
  of the caller's own period). Never from a typed-in or guessed id.
- **E4 — PDF PII cleanup (mirrors DECISION 3a).** The downloaded invoice PDF is deleted from the
  cache/temp location after the preview is dismissed; it is never left resident in `Caches/`.

### REQUIRED TEST — TC-IOS-EARNINGS-OWNERSHIP
A client-side test proving the earnings/invoices surface NEVER sends a foreign id:
- Given a signed-in partner whose `currentEmployeeId()` resolves to `emp-A`, the facade/viewmodel
  building `getPeriodPays` / `getPagedInvoices` requests asserts the outgoing `employeeId` /
  `filterEmployeeId` equals `emp-A` even if a foreign id is injected into screen state (E1/E2).
- `downloadInvoice` is invoked only with an `invoiceId` present in the caller's own fetched invoice
  list; a synthetic foreign id is never passed (E3).
- (PDF cleanup E4 covered by a unit test asserting the temp file is removed on preview-dismiss, OR
  folded into the QuickLook-coordinator test the architect's PDF ruling defines.)
- **Backend ownership is already proven** by `GetPeriodPaysOwnershipTests` (ran green here, 4/4).
  A matching backend integration proof for the invoice by-id/download/paged owner gates
  (TC-BE-INVOICE-OWNERSHIP) is **nice-to-have** (the unit-level gate + the verified handler code
  cover it); fold into BSP-4d if/when that host is hardened.

### Verdict
**PASS-the-design.** Backend read-scoping VERIFIED owner-pinned on all four handlers (NO T-0339-class
follow-up). Binding iOS rules E1–E4 + TC-IOS-EARNINGS-OWNERSHIP. PDF-PII cleanup mandatory (D3a);
no PrivacyInfo collected-data entry for the download (D3b). One latent S5 rate-limit gap recorded
into BSP-4d (backend, not a blocker, iOS cannot fix). Re-verify on the actual iOS diff at review time.

---

## 2026-06-28 — T-0309 Slice B BUILD-TIME VERIFICATION (Gate-SEC, security reviewer) — verified-against-code on `phase/ios-phase5` (uncommitted) — VERDICT: PASS

**security_touching: YES.** This is the build-time re-verify the 2026-06-27 design ruling demanded
("Re-verify on the actual iOS diff at review time"). Verified against the ACTUAL uncommitted Slice B
code (`git status` clean except this slice; `git diff`/untracked read), not the design. All four
binding iOS client rules (E1/E3/E4 + the standing E2), TC-IOS-EARNINGS-OWNERSHIP, and S4 hold as
written. The reviewer owns Gate-DP in parallel; this entry is the read-scoping / PII gate only.

### Files verified (file:line evidence)
- `CleansiaPartner/Sources/Features/Earnings/InvoicesListViewModel.swift`
- `CleansiaPartner/Sources/Features/Earnings/InvoiceDetailViewModel.swift`
- `CleansiaPartner/Sources/Features/Earnings/PeriodPayViewModel.swift`
- `CleansiaPartner/Sources/Features/Earnings/{InvoicesListView,InvoicesListContent,InvoiceDetailView}.swift`
- `CleansiaPartner/Sources/Data/PartnerPayrollClient.swift`
- `CleansiaCore/Sources/CleansiaCore/Components/QuickLookPreview.swift`
- `CleansiaPartnerApi/URLSessionImplementations.swift` (generated download file-sink — load-bearing for E4)
- Tests: `EarningsOwnershipTests.swift`, `InvoicesListViewModelTests.swift`,
  `InvoiceDetailViewModelTests.swift`, `QuickLookPreviewTests.swift`, `FakePayrollClient.swift`

### E1 — own-server-derived id ONLY — PASS (VERIFIED)
- `InvoicesListViewModel.fetch()` (`InvoicesListViewModel.swift:31-49`): the ONLY id passed to
  `getPagedInvoices` is `client.currentEmployeeId()` (→ generated `employeeGetCurrentEmployee`, see
  `PartnerPayrollClient.swift:18-26,37-47`). No screen/route value reaches it. A `.failure` (nil/
  unresolvable id) → `state = .loaded([])` with a `return` BEFORE the `getPagedInvoices` call → ZERO
  network call (`:35-38`).
- `PeriodPayViewModel.load()` (`PeriodPayViewModel.swift:33-38`): same pattern — own id only;
  unresolvable → `.error` with NO `getPeriodPays` call.
- Grep of the whole Earnings feature for a screen/route-supplied employeeId reaching
  `getPagedInvoices`/`getPeriodPays`: NONE. `getPagedInvoices(employeeId:)` / `getPeriodPays(employeeId:)`
  are only ever called with the `currentEmployeeId()` result. The client protocol intentionally exposes
  NO setter for a foreign id (`PartnerPayrollClient.swift:5-15`).
- Tests: `testInvoicesListSendsOnlyOwnServerDerivedEmployeeId` (`EarningsOwnershipTests.swift:30-39`,
  asserts outgoing id == `emp-own`), `testInvoicesListNeverQueriesWhenIdUnresolvable` (`:42-49`,
  `invoicesCallCount == 0`), `testMissingEmployeeIdMapsToEmptyWithoutNetworkCall`
  (`InvoicesListViewModelTests.swift:52-60`), `testPeriodPaySendsOnlyOwnServerDerivedEmployeeId`
  (`EarningsOwnershipTests.swift:52-65`).

### E2 — no foreign-id echo — PASS (VERIFIED, by construction)
The invoiceId carried into detail/download originates ONLY from the caller's own server-fetched list:
`InvoicesListContent`/`InvoiceCard.open()` (`InvoicesListContent.swift:88-90`) maps `invoice.id` (a
field of an `EmployeeInvoiceDto` returned by the own-scoped `getPagedInvoices`) into `onOpenInvoice`
→ `EarningsView.swift` `path.append(.invoiceDetail(id:$0))` → `InvoiceDetailView(invoiceId:)`. No
text field, deep link, or out-of-band id is constructed into any request anywhere in the feature.

### E3 — download own invoice only — PASS (VERIFIED)
- `InvoiceDetailViewModel.openPdf()` (`InvoiceDetailViewModel.swift:56-69`) and `.load()` (`:41-54`)
  act ONLY on `self.invoiceId` — the immutable `let` set at construction (`:24,28-32`) from the
  caller's own list row (E2 chain above). No synthesized/echoed/foreign id reaches
  `downloadInvoicePdf(id:)` or `getInvoice(id:)`; the VM has no API to mutate `invoiceId`.
- Test: `testDownloadActsOnlyOnTheLoadedInvoiceId` (`EarningsOwnershipTests.swift:69-79`, asserts
  `client.lastDownloadId == "inv-own"`); also `testDownloadSuccessEmitsPresentEventAndReturnsToIdle`
  (`InvoiceDetailViewModelTests.swift:63-79`, `lastDownloadId == "inv-1"`).

### E4 — delete the PDF after preview (load-bearing PII rule) — PASS (VERIFIED), single resident copy
- The generated downloader writes the PDF to ONE deterministic file under the Caches dir via
  `data.write(to: filePath, options: .atomic)` (`URLSessionImplementations.swift:314-332`); the
  returned `URL` IS that file. `.atomic` consumes its temp sibling on rename → NO second copy. There
  is no in-app re-copy/move in `LivePartnerPayrollClient` (`PartnerPayrollClient.swift:55-59`) or the
  VM — the Caches file is the only PII-at-rest copy.
- That exact URL flows VM → `presentPdf` (`InvoiceDetailViewModel.swift:64`) → view
  `.onReceive` → `.sheet(item:)` (`InvoiceDetailView.swift:42-45`) → `QuickLookPreview(url:url, …)`
  (`:52`). `deleteOnDismiss` defaults to `true` (`QuickLookPreview.swift:21`), and the coordinator
  deletes the file in `previewControllerWillDismiss` (`:61-66` → `removeFile(at:)`). The delete is
  file-URL-gated: `removeFile` early-returns unless `url.isFileURL`, then best-effort `try?`
  (`:72-75`) — never touches a remote URL.
- `previewControllerWillDismiss` is the QLPreviewController delegate hook; within the `.sheet` the
  QLPreviewController is the sheet root, so Done / swipe-down / programmatic dismissal all route
  through it before the sheet tears down — no dismissal path leaves the file resident.
- Tests: `testRemoveFileDeletesAnExistingFile` (`QuickLookPreviewTests.swift:6-15`),
  `testRemoveFileIsNoOpForMissingFile` (`:17-23`), `testRemoveFileIgnoresNonFileURL` (`:25-31`,
  proves the remote-URL guard).

### TC-IOS-EARNINGS-OWNERSHIP — EXISTS — PASS (E1 own-id + E3 own-invoice)
Present at `CleansiaPartner/Tests/EarningsOwnershipTests.swift` (4 cases; E1 list+period, E1
no-network-on-unresolvable, E3 download-own). E4 cleanup covered by `QuickLookPreviewTests.swift`.
NOTE: PASS is asserted at the code/logic level (the fakes in `FakePayrollClient.swift` record the
outgoing ids and the assertions are correct); the iOS suite is run on the Mac toolchain by the dev/
reviewer — flag a green `swift test`/Xcode run as the build proof at merge (no other-platform run here).

### S4 (DTO leak) — PASS (VERIFIED, unchanged from §7.11)
`EmployeeInvoiceDto` / `EmployeeInvoiceDetailDto` (`CleansiaPartnerApi/Models/*`) carry only:
`variableSymbol`/`specificSymbol`/`paymentReference`/`bankTransferNote` (CZ/SK payment-routing refs the
cleaner needs) + amounts + own `employeeId`/`employeeName`. NO IBAN / raw bank-account number, NO
`tenantId`, NO `userId`, NO Stripe id, NO hash, NO other-employee fields. These are NSwag-generated from
the same backend DTOs already passed at S4 in §7.11; the server pins all fields to the caller (owner gate
on all four handlers). The PDF is the only PII-at-rest and E4 cleans it.

### ADR-0019 spine / auth — PASS (no new token/header/401 path)
`LivePartnerPayrollClient` calls the generated `PartnerEmployeePayrollAPI.*` via the same
`URLSessionRequestBuilderFactory` / global session and `currentEmployeeId()` via `PartnerEmployeeAPI`
already used by shipped Order/Employee clients — all ride the existing `PartnerAuthSpine` HeaderAdapter
(`PartnerClients.swift:13-34`, wired once in `PartnerAppContainer.swift`). The generated builder's
`requiresAuthentication: false` is the stock NSwag default; Authorization/X-Device-Id are stamped at the
spine layer for every partner call, not per-route. No new KeychainTokenStore, 401 handler, or header path
in this slice.

### S5 (rate limit) — UNCHANGED LATENT (BSP-4d), not a blocker
`EmployeePayrollController`'s four routes still carry no `[EnableRateLimiting]`; the authed-global limiter
is a no-op. iOS cannot fix; folded into BSP-4d. Read-only enumeration is blocked by the owner gate, so
LOW/latent. This iOS slice does not change it.

### Verdict — PASS (build-time, verified-against-code)
E1 PASS · E2 PASS · E3 PASS · E4 PASS · TC-IOS-EARNINGS-OWNERSHIP exists & logically correct ·
S4 PASS · ADR-0019 no-new-auth-path PASS. NO new gap found. NO code edits (audit-only). Remaining:
S5/BSP-4d (backend, standing). Merge-proof to capture at PR: a green iOS test run on the Mac toolchain.
