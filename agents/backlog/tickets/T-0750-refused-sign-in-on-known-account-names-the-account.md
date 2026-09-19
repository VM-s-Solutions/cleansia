---
id: T-0750
title: A refused sign-in or reset on a KNOWN account names the account, and its row is stamped with the account's operator (Q-AUD-O4)
status: done
size: S
owner: —
created: 2026-09-15
updated: 2026-09-15
depends_on: [T-0744]
blocks: []
stories: []
adrs: [ADR-0061, ADR-0062]
layers: [backend]
security_touching: true
manual_steps: []
sprint: 16
---

## Context

Owner ruling 2026-09-15 on **Q-AUD-O4** (filed by T-0744): *name it*. A wrong password, a lockout, a
bad reset or confirmation code, a password sign-in or reset asked for a Google/Apple account, and a
social token refused onto an account of another type all left a customer failure row with
`UserId = null` and `ResourceId = null`, stamped with the **default market's** operator — the refusal
short-circuits in the validator, which never named the subject — so an account-takeover trail was
attributable by IP only.

## Doing

- The validator that resolves the account names it **before** it refuses, through the seam the
  handlers already use — `IAuditContext.RecordEvidence("User", user.Id, payload: null,
  actorUserId: user.Id)` — from one `ResolveAsync` each rule reads through (`LoginValidator` for
  `Login`/`MobileLogin` and the unmarked partner/admin sign-ins; `ChangePassword`; `ConfirmUserEmail`)
  or at the one predicate that loads the account (`RequestPasswordChange`); `GoogleAuth` and
  `AppleAuth` name the account resolved from the verified claims before the account-type and active
  guards.
- Both failure arms drain the snapshot and hand it to `AuditEntryFactory.CreateCustomerFailure`,
  which reads the subject and the resource off it and never its payload — `PayloadJson` stays null on
  every failure row, the session still wins over the named subject (S1), nothing of the naming reaches
  the caller.
- An unknown address resolves nothing, so nothing is named: no user, no resource, no payload, no
  e-mail — unchanged.
- Tenancy stays out of the validators (ADR-0061 D3): `OutOfBandAuditFailureSink` stamps a customer
  row that names a subject with **that subject's `TenantId`**, read in the sink's own scope past the
  tenant filter; a row that names nobody keeps the ambient stamp (the default market's operator on an
  anonymous request). A second operator's customer refused a sign-in lands in the second operator's
  feed, where `GetActionTimeline` by user and the paged customer list find it and the default
  operator's does not.

## NOT

No change to what the caller is told (the same key). No change to the unknown-address row. No
session-subject flag on the sink (the doc reword was chosen over a new flag — proportionality). No
schema change.

## Status log

- 2026-09-15 — shipped in `e53346cf` (red first: 12 new unit cases against the plumbing alone;
  `OutOfBandAuditFailureSinkTenantTests` over SQLite — the subject's operator over the ambient
  tenant, the ambient stamp for a nameless row and an unknown subject, the subject's operator with no
  ambient tenant, the skip with neither, the admin row untouched; `SessionAuditTests` on Postgres —
  a wrong password on a second operator's account found by both admin reads under that operator and
  by neither under the default, a reset request for a known Google address, a bad reset code and a
  wrong confirmation code each naming the account, the unknown-address cases unchanged; Tests
  5445/5445, IntegrationTests Auditing+Auth+Gdpr+Users 143/143, HostTests 239/239) and `380896cf`
  (review: the five `IOperatorScopedRequest.CountryId` docs, `AuditContext`'s summary and the sink's
  doc now say what the sink does; no behaviour change). Recorded in ADR-0062 D3/D7 as amended
  2026-09-15; S2; the customer-action-audit card; `/flows/auth-and-identity#session-rows`.
