---
id: T-0732
title: T-AUD-3 — Terms and privacy get a version; registration and consent are recorded server-side (ADR-0062 D4; absorbs T-0686)
status: done
size: M
owner: —
created: 2026-09-13
updated: 2026-09-13
depends_on: [T-0730]
blocks: []
stories: []
adrs: [ADR-0062, ADR-0041]
layers: [backend, frontend]
security_touching: true
manual_steps: []
sprint: 16
---

## Context

ADR-0062 D4, absorbing T-0686 (*nothing records that a customer accepted the terms, or which
version*). `Register` persisted nothing; Google/Apple validated a `TermsAccepted` bool and discarded
it; the web parked the tick in localStorage and flushed two consent rows best-effort at the next
session; no version existed anywhere; `UserConsent` overwrote itself on regrant.

## Doing

- `LegalDocumentVersions` constants (contract §1.5); `UserConsent.Grant/Regrant(documentVersion)` +
  `AcceptVersion(version, ip, ua)` for a re-acceptance under a **different** version; `ConsentService`
  passes the version only for a Customer subject (`GrantConsent` is routed on both Partner hosts);
  `UpdateEmployee` passes `null`.
- `Register.Command.TermsAccepted: bool?`; `Register` (marker `customer.account.register`,
  `AllowsAnonymousActor = true`), and the provisioning branches of `GoogleAuth` / `AppleAuth`
  (**no marker** — sign-in-or-register commands) grant `TermsOfService` + `PrivacyPolicy` when `true`
  (IP/UA from the provider, version stamped); `Register` records `RegistrationEvidence` with
  `actorUserId`.
- `GrantConsent` / `WithdrawConsent` markers + `ConsentEvidence`.
- Customer web: register form sends `termsAccepted`; `signup-consent.service.ts` retired (customer
  side) and its callers; `terms_page.version` / `privacy_page.version` keys rendered on the legal pages
  in five locales.
- `check-booking-policy-parity.mjs`: pin the two constants to the five locale values (+ self-test).
- T-0686: status log entry "absorbed by ADR-0062 / T-AUD-3", INDEX row → done.

## NOT

- No `AgreementVersion` tables (ADR-0041 stays unbuilt for customers). No refusal of registration or
  checkout on a missing/false tick (Q-AUD-L4). No backfill or re-prompt of existing customers
  (Q-AUD-L2). No mobile client changes (they keep sending nothing → `null`, recorded as not asserted).
  No append-only `UserConsent`. No change to the marketing/data-processing consent types.

## Acceptance criteria

- [x] **AC1** — Given `Register.Command` with `TermsAccepted = true`, when it succeeds, then two
      `UserConsent` rows (`TermsOfService`, `PrivacyPolicy`) exist with `DocumentVersion =
      LegalDocumentVersions.CustomerTerms` / `.CustomerPrivacy`, `IpAddress` and `UserAgent` from the
      request, and one `customer.account.register` row with `UserId` = the new user,
      `termsAccepted = true`, both versions, and no name/email member.
- [x] **AC2** — Given `Register.Command` with `TermsAccepted = null` (an old mobile client), when it
      succeeds, then no consent row is written, the row records `termsAccepted = null`, and
      registration is NOT refused.
- [x] **AC3** — Given an existing Google user, when `GoogleAuth` signs them in, then no
      `CustomerActionAudits` row is written; given a new verified Google identity with
      `TermsAccepted = true`, when it provisions, then the two consent rows carry `DocumentVersion` and
      there is still no audit row.
- [x] **AC4** — Given an Employee JWT on a Partner host, when it runs `GrantConsent(TermsOfService)`,
      then the `UserConsent` row's `DocumentVersion` is null and no customer audit row is written.
- [x] **AC5** — Given a customer whose `TermsOfService` row is granted under version V1, when the
      constant is V2 and they grant again, then the row's `DocumentVersion` is V2 and a
      `customer.consent.grant` row with `documentVersion = V2` exists; when the constant is still V1,
      then the row is unchanged and a `customer.consent.grant` row is still written.
- [x] **AC6** — Given the five customer-web locales, when any `terms_page.version` value differs from
      the C# constant, then `check-booking-policy-parity.mjs` exits non-zero naming the locale.
- [x] **AC7** — Given the web register form, when the terms box is ticked, then the request body
      carries `termsAccepted: true`, and the customer-side `signup-consent.service.ts` no longer exists.
      *(The order wizard's half waits on the client regen — see T-0731's status log.)*

## Status log

- 2026-09-13 — backend half landed (033f13ad). Test-first attestation: the implementing lane's single
  commit carries production and tests together; the fix lane cannot attest to the order in which
  `ConsentServiceTests` / `UserConsentDocumentVersionTests` were written relative to `ConsentService`.
  The tests are non-vacuous (they fail on the pre-D4 `existing.IsGranted -> return false` shape). PM
  decides whether to accept the deviation.
- 2026-09-13 — review fixes. Enum serialisation ruled under the binding contract §3 ("enums serialised
  by name"): `AuditContext.JsonOptions` gains `JsonStringEnumConverter(JsonNamingPolicy.CamelCase)`, so
  BOTH arms write enums by camelCase name; no production data exists, the admin int-pinning tests moved
  to names. `Grant`/`Regrant` no longer default `documentVersion`. `AcceptVersion` accepts a
  **different** version (what the code does — versions are opaque strings); the contract §1.4 and the
  ADR D4 wording "NEWER" corrected to "different" by the docs lane.
- 2026-09-13 — `generate-customer-client` + `refresh-mobile-spec.sh customer` for
  `Register.Command.TermsAccepted` batched into the orchestrator's regen after T-0731/T-0732 landed;
  the customer client and the mobile spec carry it on `Register`, `GoogleAuth`, `AppleAuth`. The
  customer-web register form sends it.
- 2026-09-13 — review fix (fa2d5672): the two consent-type refusals on `GrantConsent`/`WithdrawConsent`
  carry the `invalid_enum_value` key instead of an English sentence with the submitted integer.

## Review

- `ConsentEvidence` is a top-level record (`Features/Gdpr/ConsentEvidence.cs`), not nested, because two
  features (`GrantConsent`, `WithdrawConsent`) share it. Accepted deviation from contract §3; the
  T-0731 PII guard walks `ICustomerAuditPayload` implementations by reflection, not by nested-type
  position.
- Roster: `CustomerAuditActionRosterTests` pins sixteen commands (thirteen + `Register`, `GrantConsent`,
  `WithdrawConsent`), `GoogleAuth`/`AppleAuth` asserted absent, `AllowsAnonymousActor` on exactly
  `Register` and `CreateOrder`.
