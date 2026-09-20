# The contract for work — living decision doc

**Topic:** what the platform writes down so that the *smlouva o dílo* the lawyer's model says forms
between the customer and the cleaner at the cleaner's acceptance is a **record and not an inference**
— the text an order is booked under, the cleaner's acceptance of that text per seat, the acts that
write it, what each party can read, and how long the record lives.
**ADR:** [ADR-0068](/decisions/adr-0068) (`accepted` 2026-09-20 by panel — author draft, architect
challenge C1–C17, lead verdict; **shipped the same day** as T-0777–T-0784; a §What shipped block
records where the tree departs from the panel text). Composes with [ADR-0063](/decisions/adr-0063)
(a legal text is a versioned, seeded, immutable `LegalDocument`; **amended by one sentence** — D9's
"no in-app rendering" is departed from for this text only), [ADR-0062](/decisions/adr-0062) D1/D4/D5
(the request trio and the signed device claim; the client asserts, the server stamps; append-only,
pseudonymise on erasure, a per-row window), [ADR-0041](/decisions/adr-0041) D2/D3 (the accepted,
never-built "echo the exact text row served" shape — this is its first instance in the tree),
[ADR-0064](/decisions/adr-0064) D3 (books, the archive bundle), [ADR-0067](/decisions/adr-0067)
(the crew is the fact; `Confirmed` says nothing about the contract either).
**Owner input:** *"I want to implement 'smlouva o dílo' (lawyer suggested it)"* (2026-09-20), on the
meeting notes of 2026-09-16 (*"Aby odpovědnost a případná reklamace byla na uklízečce … Uklízečka
potvrdí přejetím prstu (možná integrace něco jako Signi)"*) and the lawyer's framework agreement
art. 2.3 (the contract forms at the cleaner's acceptance, on the customer terms).
**Owner questions in force as defaults:** Q-WC-01…07 in `agents/backlog/questions/open.md` and on the
plate — every one shipped as its default.

---

## Current shape (the tree at `4cbd2c09`, 2026-09-20)

**The customer's half — the order is booked under a text.** `LegalDocumentType.WorkContract` is a
**customer-audience** document (the panel reversed the author's `Employee` audience: the contract's
terms are, by the lawyer's own sentence, the customer terms, and a text the customer could not read
before booking is a counter-offer, not an acceptance). Seeded from
`Seed/Legal/customer/work-contract/any/2026-09-20/{cs,en,sk,uk,ru}.md` — a **template** (no figure,
name, address or date; `{{currency}}` where a price is mentioned; the parity checker walks the folder).
`OrderFactory.CreateAsync` resolves the in-force document for the **address's market** and stamps
`Orders.WorkContractDocumentId` once, beside the pay-coverage gate and for the same reason; nothing in
force throws. The column is nullable (153 `Order.Create` callers, 152 in tests) and a null is a
fixture, not a state — the take refuses it, nothing resolves lazily. **Fixed at the offer:** a new
version applies to orders booked from its date; no re-acceptance, no stale-version race. Published at
`/work-contract` beside `/terms` and `/privacy`; the wizard's confirm step carries the lawyer's P074
sentence as an information line on every client (no checkbox).

**The cleaner's half — one row per seat, naming the exact text row.**
`WorkContractAcceptance : TenantAuditable` (`Core.Domain/Contracts`): `OrderId` (FK Restrict),
`OrderEmployeeId` (**the seat**, bare scalar, `UNIQUE`), `EmployeeId` (bare scalar),
`LegalDocumentTextId` (FK Restrict — document, version, language and `ContentHash` by one join),
`DocumentVersion`, `AcceptedOn`, `ClientAudience`, `IpAddress` / `DeviceLabel` / `DeviceId` (the
session's **signed claim**, never the `X-Device-Id` header), `FactsJson` (jsonb — `WorkContractFacts`:
number, window, price + currency, `locationApproximate`, market, rooms, bathrooms, services, packages,
extra slugs; never a name or a street, `WorkContractFactsPiiGuardTests` walks it). Private setters,
`Create` + `Pseudonymise` only. `WorkContractAcceptanceRow` is the read shape (adds the text's language
and the order number) every list reader uses. Bound to the seat because the seat id is the one fact
that names *this* assignment: take → drop → re-take is two rows; take → drop → admin re-add is a new
seat with **no** row — exactly the case the gates refuse.

**The acts.** `TakeOrder.Command(OrderId, AcceptedWorkContractTextId)` — the text-row id **is** the
tick; blank → `contract.not_accepted` **before** existence (leaks neither existence nor the hold),
a text not of the order's document → `contract.text_mismatch` **last**; the handler stages the row and
its `EmployeeActionAudit(ContractAccepted)` through `WorkContractAcceptor.StageAsync` between
`AddAssignedEmployee` and its own commit, so the seat-race loser leaves no row. `AcceptWorkContract`
is the same act for a seat an admin formed (`AdminReassignOrder` writes no acceptance — an admin
cannot accept on the cleaner's behalf), on any not-over order, idempotent per seat, a concurrent
double tap arbitrated by the unique index. `StartOrder` **and** `CompleteOrder` refuse a seat with no
row as `contract.acceptance_required` right after the assignment rule (Complete too: on a two-seat
crew the second cleaner never starts; and Complete is the act a *reklamace* turns on).
`NotifyOnTheWay` is not gated. `WorkContractFactsBuilder` is one `AsNoTracking` projection shared by
the preview and the acceptor so the screen and the row cannot differ, never off the take's tracked
aggregate.

**The reads.** `GetWorkContractPreview(OrderId, Language)` (partner hosts, `CanTakeOrder`) — the
order's document rendered with the order's currency + the facts; readable exactly where the board
would show the job (`OrderSpecification`, the board's floor — `1067d938`). `GetWorkContract(AcceptanceId,
Language)` (all five hosts, `CanViewOrderDetail` / `CanViewOrderDetailAdmin`) — the **stored** facts,
the accepted document's text in the requested language (else the accepted text, with
`acceptedLanguage` on the DTO), access derived from the row: its order exists for the caller
(owner-pinned for a customer) **and** for a cleaner the row's `EmployeeId` is theirs.
`OrderItem.WorkContractAcceptances` lists the current seats' rows (no name — paired with the crew entry
by seat id), emptied for a browsing cleaner.

**Tenancy of the row.** Stamped with the **order's** operator (the acceptor pins `TenantId =
order.TenantId` on both rows). The customer may be booked across the border (ADR-0061 D6), so the
acceptance-keyed, seat, order and subject reads bypass the filter and re-pin on the caller
(ADR-0051's top-right cell); the two gates and the per-company sweep stay filtered.

**The record.** `employee.order.contract_accepted` on the timeline, the incident file trail and the
archive's audit stream with no new reader; the incident file's *Contracts for work* section; the
cleaner's export (rows in full) and the customer's (the order's version + each acceptance's instant /
version / language, no employee id); the erasure blanks the trio riding the single commit (roster
verdict `AnonymizedInPlace`); `retention.work_contract_metadata.years` (default 3, min 1, per company)
blanks the trio per row and **never deletes** — the row is books, kept with the order;
`books/work-contract-acceptances.jsonl` in the archive bundle without the trio; `WorkContractDocumentId`
on the orders stream.

**The clients.** Partner: one component per platform with modes take / accept / read — web
`work-contract-dialog` (a tick + *Accept and take the job*), Android / iOS `WorkContractSheet` (the
text in a shared JavaScript-off HTML view — `:core`'s `HtmlContentView`, `CleansiaCore`'s
`HtmlContentView` — under a *Swipe to accept the contract for work* slider); every take path opens it
first and sends the previewed `legalDocumentTextId`; the detail's line / banner; Start and Complete
refusals open *accept* mode; a mismatch re-fetches, resets the gesture and says the contract was
updated. Customer: `/work-contract`, the wizard sentence, the per-crew-member line with **Read the
contract** → web `/orders/:orderId/contract/:acceptanceId`, Android `WorkContractScreen`, iOS
`WorkContractView`. Admin: the crew list's *accepted {date}, v{version}* / *contract pending*, **Read**
with the accepted text row's SHA-256 for an Administrator (`CanViewLegalDocuments`), the tenth
retention window's description, the `work_contract` type label.

---

## Trade-off space (why this shape and not the others — the ADR's alternatives table in one breath)

- **Customer audience, stamped at booking** beat an `Employee`-audience text resolved at the take:
  two texts is no consensus on terms; resolving at the take needed a stale-version protocol that one
  column on the order deletes; and `LegalDocumentResolver` could not even reach an `Employee` text.
- **The text-row echo** beat a `bool` (proves a tap, not a text) and `(documentId, language)` (weaker
  than ADR-0041's precedent, a second validator chain, and a text the seeder added after the preview).
- **Per seat with a unique index** beat `(OrderId, EmployeeId)` (cannot tell a re-take from an admin
  re-add; the dropped contract unreadable; a double accept writes two rows) and a stamp on
  `OrderEmployee` (hard-deleted by the first act a claim turns on).
- **Both gates** beat Start alone (two-seat crews and in-progress reassignments were ungated) and a
  `NotifyOnTheWay` gate (travel is not the work).
- **The trio with a per-row window** beat "claim + audience only" (a web acceptance would carry
  nothing but the session; every other legal-act record carries IP + UA and these are read) and
  "keep for the life of the account" (volume: one row per job, not two per account).
- **In-app rendering** beat ADR-0063 D9's web link for this one text: the gesture has to sit under
  the text it accepts, and the customer page is authenticated.
- **The admin placement stays a force** (Q-WC-06 default): turning it into an offer is structurally
  cleaner — it deletes the standalone act, both gates and a client key — but removes an operator
  capability and reshapes the hold for a partly crewed order; not the panel's to take.
- **No PDF, no HMAC, no Signi, no per-job text, no `CoverDisplaced`, no lazy stamp** — each stated in
  D7 with the cost of building it and what is lost by not.

## Follow-ups (filed on the owner plate, not built)

- **F24 — the PDF.** `IPdfService.GenerateWorkContractPdf(WorkContractDto)` → `(Bytes, DataSha256)`,
  a markdown-to-QuestPDF block renderer over Markdig's AST, one download route per audience beside
  `DownloadOrderReceipt`. Until then a dispute bundle is the incident file's contracts section plus
  the admin document read's hash (the admin dialog prints it).
- **F25 — the guest read.** `LookupOrder` has no crew and no contract; a read keyed on the confirmation
  secret is a new route.
- **F26 — `TermsDocumentId` on the order.** The gap this ADR closed for the contract still exists for
  the VOP (a version string in a three-year audit payload).
- **F27 — the `EmployeeActionAudit` export gap.** The cleaner's own acts are not in their JSON export.
- **F28 — `GetOrderDetails.Handler`'s eleventh collaborator.**
- **Q-WC-01…07** — the owner's and the lawyer's; an overruling is a dated amendment block on ADR-0068.

## What a change here must keep true

- The seat, the status row, the acceptance and its audit row are **one transaction** on the take; no
  acceptance exists for a seat that was never won.
- The row is written **only by the cleaner's own act** — never by an admin write, never by a job.
- The row names the **text row** (never the document + a language) and the **seat** (never the
  cleaner alone); `UNIQUE (OrderEmployeeId)` stays.
- The facts carry **no name and no street**; the guard test is the proof.
- The device id is the **claim or null**, never the header.
- Nothing deletes a row; erasure and the sweep blank exactly the trio.
- The take's validator stays **one chain** with the tick before existence and the echo last.
- A tenant-ignoring read on this table re-pins on the row's **order for the caller** (and the
  cleaner's own id); the gates and the sweep read through the filter.
