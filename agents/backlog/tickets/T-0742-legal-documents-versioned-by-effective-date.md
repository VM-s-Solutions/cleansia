---
id: T-0742
title: Legal documents are versioned by effective date and stored, and consent stamps the version (L2)
status: done
size: L
owner: —
created: 2026-09-14
updated: 2026-09-14
depends_on: []
blocks: [T-0743, T-0745]
stories: []
adrs: [ADR-0063, ADR-0062, ADR-0041, ADR-0060]
layers: [backend, db, frontend]
security_touching: false
manual_steps: []
sprint: 16
---

## Context

Owner ruling 2026-09-14 (Q-AUD-L2): *"I'd pick an identifier as an effective date from the beginning.
That would be beneficial for us to keep track of all of the consents and documents that we ever had.
I'd maybe have a separate storage for all of the documents. No real customers exist, so no reason to
worry about someone who didn't accept a document without a date."* Until then the version was the
constant `LegalDocumentVersions.CustomerTerms/CustomerPrivacy = "2026-09-draft"` pinned to five locale
`*.version` keys by the parity checker §4, and the text was five static locale sections.

## Doing (backend + db)

- `LegalDocument` (tenantless `BaseEntity`: `Audience`, `Type`, `CountryId?` null = platform-wide,
  `EffectiveFrom`, `Version` = the date as `yyyy-MM-dd`, `Notes`; unique with audience+type+country)
  and `LegalDocumentText` (`Language`, `Title`, `ContentMarkdown`, `ContentHash`; unique per document
  and language).
- **Immutability:** a document with `EffectiveFrom <= today` is immutable — the seeder logs and skips
  a changed text; a wording change is a new document with a new date.
- Seed: embedded markdown under `Seed/Legal/<audience>/<type>/<country-or-any>/<yyyy-MM-dd>/<lang>.md`
  with `title:` front-matter; the first version dated today for customer terms + privacy, `any`, five
  languages, the current locale copy moved verbatim; idempotent by the five-tuple + hash.
- `GET api/Legal/GetDocument?type=&countryId=&language=` anonymous on the two customer hosts: the
  document in force (`EffectiveFrom <= today`, market-specific over platform-wide, latest), language
  falling back to `en`, `LegalDocumentDto(Type, CountryId, Version, EffectiveFrom, Language, Title,
  ContentHtml, ContentHash)` — Markdig, safe pipeline, placeholders interpolated from the market.
- `GET api/AdminLegal/GetVersions` and `GET api/AdminLegal/GetDocument/{id}` under an existing policy.
- `ConsentService` resolves the document in force for (Customer, type, the subject's market) and
  stamps `DocumentVersion` **and** a new nullable FK `UserConsent.LegalDocumentId`; employee subjects
  unchanged (null).
- Delete `LegalDocumentVersions` and every reader; regenerate `Initial`; no DEV drop.

## Doing (customer web)

- `legal-pages` renders `GET Legal/GetDocument` for the chosen market and language (title,
  effective-from, version), the locale section keys deleted; the parity checker's §4 deleted and its
  placeholder-shape rule reads the seed files.

## Doing (admin web)

- A read-only **Legal documents** page: versions per audience/type/market with dates and languages, a
  content preview per language, one translated sentence that a new version is a seed file + deploy.

## NOT

No admin authoring/CMS. No employee-audience documents (ADR-0041). No re-prompt on a new version. No
mobile in-app rendering. No change to the consent types.

## Status log

- 2026-09-14 — backend/db in `87711f25` and `4a93f125` (the boot-order test pins the seed as an inline
  hosted service after the type-catalog initializer; a regrant compares the **document's identity**
  so a market copy sharing the platform-wide date is a new acceptance; the guest booking stamps the
  address's market). Clients regenerated in `0ff0dacf` (`LegalClient`, `AdminLegalClient`, the customer
  mobile spec). Customer web in `0c0de368` + `599df89c` (fetch-and-render `legal-pages`, parity checker
  on the seed files, component spec). Admin web in `290761c4` + `4c855fa7` (`/legal-documents` under
  `CanViewCountryConfigurations`, preview loader kept up across a language switch). As shipped: the one
  placeholder is `{{currency}}` (no other figure lives in the texts); the admin endpoints are
  `get-versions` / `get-document/{id}`; the seed runs at every host start. `Initial` regenerated (then
  again by T-0738 — final id `20260914115922`). Recorded as ADR-0063; ADR-0062 D4 amended in place.
