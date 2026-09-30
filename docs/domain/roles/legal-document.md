# LegalDocument (ADR-0063, accepted 2026-09-14)

**Responsibility (one sentence):** Be one version of one legal text — the terms, the privacy policy,
the contract for work, the complaints procedure or one of a cleaner's own documents, for one audience,
for one market or for the whole platform — identified by the date it started applying, immutable once
that date has passed, and readable in any of its languages, so that a consent row can point at exactly
the text the customer or the cleaner read and still find it years later.

> **Types added 2026-09-28** (owner ruling: a cleaner accepts their documents in the app, versioned).
> `LegalDocumentType` gained, appended, `CleanerFrameworkContract = 3`, `SelfBillingAgreement = 4`,
> `CleanerDataProcessingAgreement = 5` (employee audience) and `ComplaintsProcedure = 6` (customer);
> `ConsentType` gained `CleanerFrameworkContract = 4`, `SelfBillingAgreement = 5`,
> `CleanerDataProcessingAgreement = 6`. `LegalDocument.CleanerAcceptedTypes` and `CleanerConsentTypeFor`
> map them; `TypeFor` maps each document-backed consent to its type. The seeder reads
> `Seed/Legal/employee/{framework-contract|self-billing-agreement|data-processing-agreement}/…` and
> `Seed/Legal/customer/complaints-procedure/…`, and refuses a cleaner document placed under `customer/`.
> No text is seeded yet, so nothing is in force and nothing is gated.
> → [Business rules — a cleaner's own documents](/product/business-rules#cleaner-documents)

> Introduced by **[ADR-0063](/decisions/adr-0063)**. `Cleansia.Core.Domain/Legal/LegalDocument.cs`
> (`: BaseEntity`, **tenantless** — platform copy per market, the `CountryConfiguration` axis) with its
> child `LegalDocumentText` (one per language, `ContentMarkdown` + SHA-256 `ContentHash`).
> `Version` is derived, never assigned: `LegalDocument.VersionFor(effectiveFrom)` = `yyyy-MM-dd`, and
> `(Audience, Type, CountryId, Version)` is unique. The only writer is the seeder; there is no delete
> path and no admin authoring — **a new version is a seed folder with a new date plus a deploy.**

## Collaborators

- **`LegalSeedResource`** — one embedded file at
  `Seed/Legal/{customer|employee}/{terms-of-service|privacy-policy}/{ISO3|any}/{yyyy-MM-dd}/{xx}.md`
  with a `title:` front-matter line. The path is the identity, the file is the text; a path that does
  not fit the shape throws at read. Line endings are normalised to LF before hashing so a CRLF checkout
  seeds the same hash as an LF one.
- **`LegalDocumentSeeder`** — the one writer. Upserts by (audience, type, market, effective date,
  language) plus the content hash: a new five-tuple becomes a document, a missing language is added,
  a text of a document **not yet in force** is replaced, and a text of a document **already in force**
  whose hash differs is **logged at Warning and skipped** (`LegalSeedOutcome.SkippedImmutable`). A
  country the catalogue does not know is skipped; a unique violation from a second host booting at the
  same moment is caught as "already seeded".
- **`LegalDocumentSeedHostedService`** — runs the seeder at **every host start, in every
  environment**, registered after `NpgsqlTypeCatalogInitializer` so it waits behind an in-flight
  migration; five bounded attempts; a host that cannot seed logs at Error and starts anyway. Pinned as
  a plain `IHostedService` awaited inline (`BootDatabaseIoTests`).
- **`ILegalDocumentRepository.GetInForceAsync(audience, type, countryId, today)`** — the reader:
  `EffectiveFrom <= today` and (`CountryId IS NULL` or `= countryId`), ordered **market-specific
  first, then newest** — a market's own copy beats a newer platform-wide one.
- **`LegalDocumentResolver`** — `ResolveInForceAsync(type, countryId)`: the caller's market, else the
  **default market** (`CountryConfiguration.IsDefaultMarket`); logs a Warning and returns `null` when
  nothing is in force. An audience-aware overload serves the employee documents and does not warn about
  a missing employee text.
- **`TextForOrFallback(language)`** (on the entity) — the primary subtag (`cs-CZ` → `cs`), else `en`,
  else the first text by ordinal. A legal page in the wrong language beats no legal page.
- **`LegalMarkdownRenderer`** — `{{name}}` placeholders substituted **before** parsing (so a market's
  figure is escaped like any text), then Markdig with `DisableHtml()`. The one placeholder is
  `{{currency}}`, filled from the market's `DefaultCurrencyCode` (ADR-0060 D3).
- **`ConsentService.TryGrantAsync(userId, type, LegalDocument? document)`** — the consumer that
  matters: stamps `UserConsent.DocumentVersion` **and** `LegalDocumentId`; a regrant compares the
  **document's identity**, not the version string (a market copy can share the platform-wide date).
  `Register`, `GoogleAuth`/`AppleAuth` (provisioning), the customer hosts' `GrantConsent` and a booking
  with the tick (`CustomerLegalConsents.RecordAsync`) resolve the document; `AcceptLegalDocument` resolves
  the cleaner's; `RegisterEmployee` grants nothing any more.
- **`UserConsent.Covers(inForce)`** — the re-acceptance question (2026-09-28): granted, not withdrawn,
  and `LegalDocumentId` equal to the document in force (nothing in force is covered). `CreateOrder` and
  `CreateRecurringBooking` ask it for the terms and the privacy policy of the booking's market;
  `ApproveEmployee`, `TakeOrder` and `AdminReassignOrder` ask it for every cleaner document in force.
- **`GetLegalDocument`** (customer hosts, anonymous, `interactive` window) and **`AdminGetLegalVersions`
  / `AdminGetLegalDocument`** (`CanViewLegalDocuments`, Administrator only — ADR-0066) — the reads; the customer `/terms` and
  `/privacy` pages and the admin `/legal-documents` page render them. **The admin reads mark one version
  per (audience, type, market) in force** — the newest whose `EffectiveFrom` is on or before today. An
  older version that has been superseded and a version dated in the future both read *no*.
  `AdminGetLegalVersions` computes it over the fetched list (its filters narrow on that same key, so every
  group arrives whole); `AdminGetLegalDocument` asks `GetInForceAsync` for the document's own group and
  compares ids.
- **`agents/tools/check-booking-policy-parity.mjs`** — reads the newest seed version per type for the
  no-baked-figures rule (a `1 000 000 Kč` in a legal text is a red build).

## Does NOT know

- **What a customer accepted.** That is `UserConsent`'s row (and the `customer.consent.*` audit
  history). The document only has to still exist and still read the same.
- **Whether a new version needs re-acceptance.** That is `UserConsent.Covers` and its callers: since
  2026-09-28 every new version is accepted before the next booking, and a cleaner accepts each new
  version of their documents before the next take. The document only has to be the one in force.
- **Who accepted a cleaner document, and when.** `UserConsent` is the *now*; the history is
  `CleanerLegalDocumentAcceptance`, one append-only row per acceptance.
- **The tenant.** Tenantless on purpose: a text is the brand's, shown identically by every operating
  company; only the *copy* is per market.
- **Today's date.** `IsInForceOn(today)` takes it; the seeder and the resolver pass UTC. The entity
  cannot refuse an edit by itself — `LegalDocumentText.Replace` exists for a not-yet-in-force document
  and the guard lives in the seeder. `IsInForceOn` means *has come into force*, which is what
  immutability needs; it stays true after a newer version supersedes the document, so it is **not** what
  the admin catalogue's *in force* reads.
- **HTML.** The stored text is markdown; the HTML is a rendering with a market's figure in it, produced
  per request and never stored.

## Invariants a reviewer checks

- **`Version == EffectiveFrom.ToString("yyyy-MM-dd")`** on every row; there is no other way to
  construct one (`LegalDocumentTests`).
- **An in-force text is never changed by the seed**: a second run over the same files changes nothing;
  a changed file under an in-force date is skipped with a warning; a not-yet-in-force text is replaced
  (`LegalDocumentSeederTests`); the embedded files parse and round-trip through a real Postgres
  (`LegalDocumentSeedAndReadTests`).
- **Resolution order**: a future date is invisible, an older date loses to a newer one, a
  market-specific copy wins over the platform-wide one, the language falls back to `en`
  (`LegalDocumentRepositoryInForceTests`, `LegalDocumentResolverTests`, `GetLegalDocumentHandlerTests`).
- **The seed runs after the type-catalog initializer** and is a plain hosted service
  (`BootDatabaseIoTests`).
- **A consent stamps the in-force document's version and id; a regrant under a different document
  identity moves the row; an employee subject stamps neither** (`RegisterConsentAndEvidenceTests`,
  `RegisterEmployeeConsentTests`, `RegistrationConsentAndAuditTests`).
- **Routes**: anonymous `200` on the two customer hosts, `404` on the partner hosts, admin
  `401`/`403`/`200` (`LegalDocumentRouteTests`).
- **`grep -r LegalDocumentVersions src` is empty**; the five customer locale files carry no
  `terms_page.section*` / `privacy_page.section*` key; `Seed/Legal/customer/*/any/2026-09-14/` holds
  five files per type.
