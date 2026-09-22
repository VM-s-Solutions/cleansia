---
id: T-0729
title: Docs — ADR-0061 accepted, ADR-0050 accepted as amended, the living note, S1/S8, CLAUDE.md landmine 1, model, roles, business rules, API pages, catalog, backlog, the owed DEV drop
status: done
size: M
owner: —
created: 2026-09-13
updated: 2026-09-13
depends_on: [T-0722, T-0723, T-0724, T-0725, T-0726]
blocks: []
stories: []
adrs: [ADR-0061, ADR-0050, ADR-0051, ADR-0017]
layers: [docs]
security_touching: false
manual_steps: []
sprint: 16
---

## Context

Docs are written at the end, once the feature is green. The tenancy activation shipped in `e5a45de3`
and `5a5f4681` (the clients and mobile specs regenerated in `9bc5da2a`); ADR-0061 was a draft with the
challenger round applied and no verdict; ADR-0050 was `proposed` with three decisions the ruling
reversed; and eighteen living pages said some form of *"single-tenant mode is `TenantId = null`"* —
including CLAUDE.md landmine 1, which stopped being true the day D8 landed.

## Doing

- `docs/decisions/adr-0061.md` as `accepted` (2026-09-13): the owner's words quoted, the challenge
  table kept, Defense and Verdict written, O-1..O-5 recorded as decided-with-default, an *"As shipped"*
  block naming every place the tree differs from the draft (the `OperatorResolution` record, the
  resolver calling the directory handler directly, the `SetDefaultMarket` operator gate, the
  `TokenService` non-empty guard, nine marker records, `OrderStatusTrack`'s mapping, the two absorbed
  fixes, the deleted dead method), the DDL counts re-verified (44 required / 23 nullable / 13
  `NullsDistinct=false`).
- `docs/decisions/adr-0050.md` accepted **as amended** — a banner and an appended amendment table
  (D1/D4/D5 superseded by ADR-0061 D5.1, D2 kept, D3 vacuous); the body untouched.
- `docs/decisions/index.md` (61 records, two supersession arrows, ADR-0050 grey); the sidebar.
- Dated notes, not rewrites: ADR-0051 (the confirm read moved sides; O-2 closed), ADR-0003 (the
  renamed method), ADR-0017 (D3's granularity sentence and "null = single-tenant" superseded).
- `agents/architecture/decisions/multi-tenancy-and-region.md`: Axis (a) re-verified after activation,
  the D7 rule, *"What ACTIVATION changes"* → *"What activation changed"* in the past tense with the
  DDL claims corrected (13 indexes armed; `Users` global; two indexes gained a tenant term), the
  dormancy section replaced by *"active, one operator, the second one priced"*.
- `docs/architecture/security-rules.md`: S1 gains *"a request names a market, never a tenant"*; S8's
  opening states NOT NULL, the D7 sort and the `Users (Email)` exception, names the two standing
  guards, files the confirm read and the register pre-checks on the bypass side of the matrix,
  supersedes the "existence oracle" and "email is not a permitted pin" sentences, and corrects the
  anonymous-write trap and the sweep paragraph.
- `CLAUDE.md` landmine 1 rewritten to the activated truth.
- `docs/domain/model.md` (a Tenancy section, 84 entities, `User`/`Employee`/`OrderReceipt`/
  `EmployeePayConfig`/`CompanyInfo`/`CountryConfiguration`/`LoyaltyTierConfig`/`PromoCode` rows);
  `docs/domain/roles/` — `tenant.md`, `operator-tenant-resolver.md`,
  `operator-tenant-scope-behavior.md` (new), `tenant-provider.md` (three override writers, `null` is
  never a tenant), `membership-benefit-usage.md`, `employee-payout-details.md`, `index.md`.
- `docs/product/business-rules.md` `#market` (a market has an operating company; registration,
  orders and cleaners belong to it; one email = one identity; cross-company booking refused; gate 3);
  `docs/product/features.md`.
- `docs/architecture/platform-expandability.md` (§0 banner and row, §1 rewritten with the 46-entity
  three-bucket table, §8 step 11 = gate 3, §8 item 3 = the six second-company steps);
  `database.md`; `push-notifications.md` §4; `docs/flows/cross-cutting.md`.
- `docs/api/authentication.md` (the market on anonymous requests — six requests, hosts, the two
  refusals; register semantics; `tenant_id` claim), `orders.md` (`CreateOrder`'s operator rule and
  scope; `Lookup`'s secret pin and the corrected query example), `markets-and-memberships.md` (the
  operator predicate, the default-market gate, the three keys and `country.not_serviced`'s new reach).
- `agents/knowledge/consistency.md` §"Tenant-scoped unique indexes" (13-index roster, `Users` moved
  out with the reason, the two-layer guard, `TenantIdRequiredModelTests`); `patterns-backend.md` (the
  tenancy paragraph; the `42P08` note).
- `CHANGELOG.md` — five Added entries, including the operator's owed DEV drop.
- `agents/cleanup/MANUAL_STEPS.md` MS-2 names `20260913132759` as the id the one owed drop belongs to.
- Backlog: T-0722–T-0726 rows corrected to the final migration id and the regen commit; T-0727 and
  T-0728 rows added as `in-progress` (their lanes run in parallel); this row; `questions/open.md`
  gains Q-TENANCY-01..05 with the defaults applied.

## NOT

No code (the `ApplyTenantQueryFilters` comment that still says "single-tenant mode" is reported, not
edited — the filter body is pinned diff-empty by three ADRs and a comment fix is a backend lane's
one-liner); no `npm run build` (no shell in the lane — flagged for the orchestrator); the other
`agents/architecture/decisions/*.md` living notes that mention single-tenant mode historically
(`self-billing-agreement`, `payout-details`, `membership-benefits`, `promo-redemption-ordering`,
`payout-invoice-references`) not rewritten — each describes the state at its own decision's date and
is outside the acceptance grep; ADR-0028's frontmatter/body mismatch (PM-owned, reported twice now).

## Acceptance criteria

- `grep -rni "single-tenant" docs agents/knowledge CLAUDE.md` outside `docs/.vitepress/dist`: every hit
  is inside `docs/decisions/*` or a past-tense sentence naming what activation changed.
- S8 no longer says the DDL lacks `NULLS NOT DISTINCT`, no longer names `(TenantId, Email)` as the
  design, no longer forbids email as a pin; the register pre-checks are filed on the bypass side.
- CLAUDE.md landmine 1 describes the tenantless `Auditable` column only and states NOT NULL.
- ADR-0050 and ADR-0061 read `accepted`; 0050 carries the D1/D4/D5-superseded line; 0061 carries
  Challenge / Defense / Verdict.
- `questions/open.md` carries Q-TENANCY-01..05 with their defaults.
- MS-2 names the one owed DEV drop and the migration id it belongs to.
- The three new CRC cards each have a one-sentence responsibility, collaborators and a "does NOT
  know" list; the resolver's names both answers.
- Docs CI — the orchestrator's to run.

## Review

Every claim checked against the tree at `9bc5da2a`: `Tenancy/*` (the four files), `Tenant.cs`,
`CountryConfiguration.cs`, `EntityConfiguration.cs`, `Register.cs`, `TokenService.cs`, `GetMarkets.cs`,
`SetDefaultMarket.cs`, `CreateOrder.cs` and `ApproveEmployee.cs` (the two rules), `LookupOrder.cs`,
`FluentValidationExtensions.cs`, `NullsNotDistinctIndexModelTests.cs`, the eight guard test files (all
present), the four host `AuthController`s and the customer `PromoCode`/`Referral`/`Market` controllers,
`BusinessErrorMessage.cs`, the seed (`Tenants` first; CZE's operator; `cleansia-cz` on the re-homed
rows), the migration `20260913132759_Initial.cs` (`Tenants`; the FK; `IX_Users_Email`; the two
`NullsDistinct=false` indexes; `IX_LoyaltyTierConfigs_Tier`; 44 / 23 / 13). One count in the brief was
corrected from the DDL: 13 indexes carry `NullsDistinct=false`, not 14.
