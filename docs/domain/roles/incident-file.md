# IncidentFile (ADR-0062 D6 as amended, owner ruling 2026-09-14)

**Responsibility (one sentence):** Assemble, from the database alone, the one document that prints a
customer's identity beside their orders, their disputes, their consents and the whole trail of who did
what on those orders — as a PDF whose data section is hashed, so that a printed copy can be matched to
the audited act that produced it — and refuse to print anyone else's data while doing it.

> Introduced by **[ADR-0062](/decisions/adr-0062)** D6 as amended (Q-AUD-L6 reversed: *"PDF would be a
> cleaner approach and would resolve a headache in the beginning"*). Three parts: **`IncidentFileService`**
> (`Cleansia.Core.AppServices/Services`) decides *what* is printed and builds `IncidentFilePdfData`;
> **`IncidentFileSections` + `IncidentFileDigest`** (`Cleansia.Infra.Services/Pdf/IncidentFile`) turn it
> into a section model and the SHA-256 over that model's canonical text; **`IncidentFileLayoutBuilder`**
> draws the model with QuestPDF behind the same process-wide render gate as the receipt and the invoice.
> Reached through **`ExportCustomerIncidentFile.Command(UserId, OrderId?)`** — `POST
> api/v1/AdminGdpr/incident-file/{userId}?orderId=`, `CanAdminExportUserData`, `auth` window, marked
> `gdpr.user.incident_file` (Sensitive, resource `User`) — which returns `application/pdf` named
> `incident-<userId>-<yyyyMMdd>.pdf`.

## Collaborators

- **`ExportCustomerIncidentFile.Validator`** — the subject exists and is not an Administrator
  (`CannotTargetAdminViaGdprTool`); an `orderId` must be **the subject's** by the service's own
  predicate (`IsSubjectOrderAsync`) or is refused `order.not_found` — never "not yours" (S3).
- **`GetActionTimeline.ProvenOrderIds`** — the shared rule for *whose orders*: the ones that name the
  subject now **or** the ones the subject's own *successful* customer acts named, grouped by order,
  newest act first, capped after the ordering. After an erasure `Order.UserId` is blanked, so the trail
  is the only link; a refused act proves nothing and names no order.
- **The nine repositories** it reads — user, order (address, lines, status history, assigned
  cleaners), refund, dispute (messages, evidence), consent (with the `LegalDocument` for the effective
  date), currency (ids → codes), and the three audit tables. Every read is `AsNoTracking`; the trail is
  loaded through the tenant filter of the admin's session.
- **`IncidentFileEvidenceFields`** — flattens each audit row's `PayloadJson` into a two-column
  evidence table in declaration order, currency ids read as codes and enums as the names the pipeline
  stored (they are serialised by name, ADR-0062 D1).
- **`IncidentFileDigest`** — `CanonicalText(sections)` (LF line ends, no generation timestamp) and
  `Sha256Hex(sections)`; the handler puts the hash on the audit snapshot and the layout prints it in the
  *Integrity* block.
- **`IPdfService.GenerateIncidentFilePdf(data) → IncidentFilePdf(Bytes, DataSha256)`** — the render;
  the footer of every page carries page *x of y* and the generating admin's e-mail (the one place it is
  printed).
- **`IAuditContext.RecordChange("User", userId, snapshot, snapshot)`** — `IncidentFileSnapshot(SubjectUserId,
  OrderId, OrderCount, DisputeCount, ConsentCount, TrailEntryCount, DataSha256)`: the row and the copy
  name the same content and never the content itself. A build or render that throws is recorded
  out-of-band by the pipeline.
- **The admin web** — `/customers/:userId` (*Incident file (PDF)* with an optional typed order-id
  scope) and the order detail (*Incident file*, which resolves the subject off the order's own trail:
  the first *successful* customer-source row, paging until one turns up), both through
  `FileDownloadService`.

## Does NOT know

- **How to sign anything.** One hash over the data section, printed and audited; no signature, no
  chain (ADR-0062 D5's rejection of a hash chain stands — the threat model is `psql`).
- **What a later build will print.** The hash proves that *a printed copy is the file the audit row
  of the same build describes*. It is **not** a promise that a re-generation matches: an **unscoped**
  file's trail carries every admin act on the account — the previous build's own
  `gdpr.user.incident_file` row included — so it differs whenever anything landed in between; an
  **order-scoped** file is stable until something on that order changes. Two builds over unchanged
  data print the same hash only for an order-scoped file.
- **A bystander.** Scoped to an order, the customer arm is the subject's own rows and the **guest**
  rows that name the order or its disputes. A stranger's refused probe at the order (`order.not_found`
  with the victim's order id, ADR-0062 D3) carries *their* user id, IP and device label, and a
  document built to leave the platform must not carry it (S6) — it is left out; widening that again is
  the owner's security call.
- **The subject's market.** The user row carries none; the *operator* printed in the identity section
  is the server-side image of the market the account was opened under.
- **Free text it did not store.** After an erasure the identity fields print the anonymised values
  (`[DELETED]`, `deleted_{id}@anonymized.local`) and the file is marked *erased*; the dispute text
  prints whatever the three-year window still holds; evidence file names print `[DELETED]` where the
  blobs are gone; the trail's IP and device label are blank. It fabricates nothing.
- **Whether the customer asked for it.** This is the admin's file; the customer's own Art. 15 export
  is JSON (`ExportUserData`) and carries no PDF.

## Invariants a reviewer checks

- **The route answers `401` anonymous, `403` to a Customer/Employee JWT, `200` with the PDF headers and
  `400` on a bad scope to an Admin JWT, and exactly one `gdpr.user.incident_file` admin row commits per
  build** (`AdminGdprIncidentFilePolicyTests`; `IncidentFileTests` on Postgres).
- **Scoped: the subject's rows and the guest rows only** — two customer rows, all the subject's, and
  the bystander's id nowhere in the canonical text (`IncidentFileTests`, `IncidentFileServiceTests`).
- **Unscoped: the subject's rows only on the customer arm; the admin arm covers the account, the
  orders and their disputes; the cleaner arm the orders** — newest first within each source, capped at
  the newest 2 000 per source with the cut said on the page (`IncidentFileServiceTests`).
- **The cap keeps the newest orders**: one more distinct order than the cap, enumerated oldest first —
  the newest is proven, the oldest is cut (`IncidentFileServiceTests`).
- **A refused act proves nothing** (`IncidentFileServiceTests`).
- **The render is deterministic once the wall-clock `CreationDate`/`ModDate` are zeroed**, a different
  `GeneratedBy` changes the bytes (the footer), and a different hash handed to the layout renders
  differently (the Integrity block) (`IncidentFileDocumentTests`).
- **After an erasure the file still builds**: identity marked erased, trail present with its request
  context blanked, the order reached through the subject's booking row (`IncidentFileTests`).
- **The route is suppressed from every host's request log** by the `gdpr/` rule (the path-suppression
  test names it), and the render logs the subject id only.
