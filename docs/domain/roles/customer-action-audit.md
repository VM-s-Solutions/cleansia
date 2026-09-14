# CustomerActionAudit (ADR-0062, accepted 2026-09-13, amended 2026-09-14)

**Responsibility (one sentence):** Record that a customer did one money-relevant or account-relevant
thing — with the figures and versions they were shown, the request context it came from, and whether
it succeeded or was refused — as a row that outlives the account, the order and the erasure, so a
dispute or an account-takeover claim can be answered from the record rather than from memory.

> Introduced by **[ADR-0062](/decisions/adr-0062)** D1/D3/D5. `Cleansia.Core.Domain/Auditing/CustomerActionAudit.cs`,
> `: BaseEntity, ITenantEntity` — **not** `Auditable` (the ADR-0012 D6 reasoning: an audit row has no
> last-writer to stamp), its own table beside `AdminActionAudit` and `EmployeeActionAudit` (owner ruling
> 2026-09-06: actor kinds apart). `TenantId` **NOT NULL** (ADR-0061 D8). Written by the shipped
> ADR-0012 pipeline — the inner `AuditLogBehavior` (success rides the action's commit; a handler-returned
> failure goes out-of-band) and the outer `AuditFailureCaptureBehavior` (validation reject, thrown
> handler, commit-throw — out-of-band, exactly once) — through the customer arm of
> [`AuditGate`](./audit-gate). **25 commands are marked (21 labels, nine of them anonymous-capable)**:
> the sixteen money and entitlement acts of the first version, the session acts the owner ruled in on
> 2026-09-14 (`customer.session.login` on `Login`/`MobileLogin`/`GoogleAuth`/`AppleAuth`,
> `customer.session.logout`, `customer.password.reset_requested`/`.reset_completed`,
> `customer.account.email_confirmed`) and the customer's own export (`customer.gdpr.export`). The
> roster is pinned by `CustomerAuditActionRosterTests`, which also guards that every anonymous-capable
> marker sits on an `IOperatorScopedRequest`.

## Collaborators

- **The evidence record** — a nested `record … : ICustomerAuditPayload` beside the command
  (`OrderCancellationEvidence`, `OrderBookingEvidence`, `DisputeFilingEvidence`, `RegistrationEvidence`,
  `MembershipSubscribeEvidence`, `LogoutEvidence`, `EmailConfirmationEvidence`, `GdprExportEvidence`, … ;
  `ConsentEvidence` and `LoginEvidence` are top-level because several features share them). The
  handler emits it through `IAuditContext.RecordEvidence(resourceType, resourceId, payload, actorUserId?)`
  and `AuditEntryFactory` serialises it into `PayloadJson` (camelCase, **enums by name**). Success rows
  only; a failure row has no payload; a session act whose only evidence is that it happened and to whom
  (a completed password reset) passes `payload: null` and still names the subject through
  `actorUserId` — the anonymous session cannot. `CustomerAuditPayloadPiiGuardTests` walks every
  implementation by reflection: no contact-identity name, no free-text name (`*Reason`, `*Description`,
  `*Instructions`, `*Note`), no live-secret name (`ConfirmationCode`, `ResetCode`, …), every `string`
  on the allow-list (ids, codes, versions, a handful of labels — `ClientAudience` is a bounded label).
- **`IAuditContext.DeclineSuccessRow()`** — a marked command whose handler took a branch the marker
  does not describe declines the success row: the social sign-ins on their **provisioning** branch (a
  registration's proof is the consent rows it writes, not a `customer.session.login`). The failure arms
  do not read it — a refusal on either branch is still recorded.
- **`IRequestMetadataProvider`** (through the factory) — the IP address and device label. The device
  id comes from the **session's signed `device_id` claim** on a signed-in row and from the `X-Device-Id`
  header only on an anonymous one (the header is the client's word alone; the claim is the device the
  token was minted for — ADR-0026).
- **`IHostAudienceProvider`** (through the factory) — `ClientAudience`, the JWT audience of the host
  that **served** the request, filled for an anonymous act too and never read off the token (which is
  null on exactly the rows that most need it). The customer web and the customer app share one
  audience, so it names the client family; `DeviceLabel` tells them apart.
- **`AuditErrorCode.Resolve`** — the `ErrorCode` on a failure row: the `BusinessErrorMessage` **key**
  (`Error.Message` of a handler failure, the first validation failure's message), never the field name
  `Error.Code` carries, never the `ValidationError` sentinel; the exception type name on a throw.
  Clamped to the column.
- **`AuditResourceResolver.ResolveExact`** — `ResourceId` on a failure row: the `{ResourceType}Id`
  property of the request, or the property the marker names in `ResourceIdProperty` (the three
  recurring-schedule acts carry `TemplateId`). Never the admin arm's `Id` / single-`*Id` fallbacks,
  which would label a country id as a membership. The success snapshot's resource wins when present
  (`CreateDispute` marks `Order`, its success row says `Dispute`).
- **`ITenantProvider`** (through `DbContextAuditWriter` / `OutOfBandAuditFailureSink`) — `TenantId ??=
  GetCurrentTenantId()` at write time: the `tenant_id` claim, or the market's operator that
  `OperatorTenantScopeBehavior` set for an anonymous act (the session acts name no market and carry an
  explicit `CountryId => null`, so their refusal row lands under the default market's operator), or
  the **account's own** operator that `TokenService` / the reset handlers adopt before a session act's
  success row is stamped (ADR-0061 D4 as amended). A refusal raised before any operator exists cannot
  be stamped and is skipped with one warning.
- **`ICustomerActionAuditRepository`** — `Add`, reads, **`PseudonymiseForSubjectAsync`** (the erasure:
  a tracked, tenant-ignoring load and `Pseudonymise()` on each row, riding the erasure's single commit —
  never `ExecuteUpdateAsync`, which would commit outside it) and **`DeleteExpiredAsync(cutoff)`** (the
  retention sweep: per row by its own `OccurredOn`, batched, tenant-agnostic).
- **`GdprDeletionService`** — the one caller of `PseudonymiseForSubjectAsync`; roster verdict
  `AnonymizedInPlace` with the ground stated (`SubjectDataErasureRosterTests`).
- **`DataRetentionBackgroundService`** — the one caller of `DeleteExpiredAsync`; window
  `retention.customer_audit.years`, default **3**, a value at or below zero refused with a warning.
- **`GdprExportService`** — reads every row of the subject into the export's `customerActions` section
  (both the self-export and the admin export).
- **`IncidentFileService`** ([`incident-file`](./incident-file)) — prints the subject's rows (and, scoped
  to an order, the guest rows on it) in the PDF's trail section, each payload flattened to a two-column
  table.
- **`GetPagedCustomerActionAudits` / `GetCustomerActionAuditById` / `GetActionTimeline`** — the admin
  readers, behind `CanViewAuditLog`; the list DTO has no payload, IP or device, the detail DTO has them
  and no tenant. `GetActionTimeline` is a canonical `PagedData<T>` query again (its validator runs
  because validators now run for every request type); its user arm unions the orders that still name
  the user with `ProvenOrderIds` — the orders the subject's own successful acts named — so an erased
  subject's timeline keeps the admin and cleaner rows on their orders.

## Does NOT know

- **The customer's name, email, phone, address text, or anything they typed.** The domain row
  (`Order`, `Dispute`, `UserConsent`) is the home of the customer's text; this row references it by id
  and records that it was submitted, when, from where and how long it was (`descriptionLength`,
  `reasonProvided`). No `ActorEmail` either: here the actor *is* the subject, and a copy would be the
  second uncontrolled copy ADR-0012 D4.1 forbids.
- **Who the customer is after erasure.** `UserId` is a bare scalar with no navigation and no FK; after
  erasure it resolves to a `User` row whose email is `deleted_{Id}@anonymized.local`. The row keeps
  the `UserId → ResourceId` link on purpose — it is the only link from an erased id to its orders —
  and the only route back to a natural person is outside the platform, through Stripe (ADR-0062 D5,
  Q-AUD-L1 ruling).
- **The before-state of anything.** No `BeforeJson`/`AfterJson` columns; the diff-shaped acts put
  `{ before, after }` inside the payload when it matters.
- **Anything the client asserted about money.** The cancel preview the customer saw is not recorded;
  the server's figures at the click are. The one client-asserted member on any row is `termsAccepted`,
  and it is the customer's assertion *against* themselves.
- **Whether it is still needed.** Retention decides (three years after its own act); the row does
  not carry a purpose or an expiry.
- **Which table a request belongs to.** That is [`AuditGate`](./audit-gate)'s answer; the entity is
  only ever built by `AuditEntryFactory.CreateCustomerSuccess/Failure`.

## Invariants a reviewer checks

- **Append-only, with one sanctioned mutator.** Private setters, one static `Create(...)`,
  `Pseudonymise()` blanks `IpAddress`, `DeviceLabel`, `DeviceId` and nothing else. `IRepository<T>`
  still exposes `Remove`/`RemoveRange`/`Deactivate`/`DeactivateRange` and `BaseEntity.IsActive` is a
  public setter, so the discipline is a test, not a type: `CustomerActionAuditImmutabilityTests` fails
  on any call site in `Cleansia.Core.AppServices` / `Cleansia.Infra.Database` that invokes one of the
  four on the type or touches `IsActive` on it.
- **A refusal is a row.** A handler-returned failure, a validation reject, a thrown handler and a
  commit-throw each leave exactly one out-of-band row with the refusal **key** (`AuditErrorCode`), and a
  rolled-back success leaves none (`CustomerAuditPipelinePostgresTests`).
- **An anonymous row is bounded, and lands only on a customer host.** Nine commands carry
  `AllowsAnonymousActor` (`Register`, guest `CreateOrder`, the four sign-ins, `ConfirmUserEmail`, the
  two password-reset commands); every customer-host action that dispatches a marked command is
  `[EnableRateLimiting]` (`RateLimitCoverageGuardTests`, anti-vacuous by label), so an unauthenticated
  caller writes at most the `auth` window's ten rows a minute per real IP; and the gate writes an
  anonymous act only when the serving host is a customer host, so a cleaner's confirmation on a partner
  host lands nowhere (`AuditGateTests`, host tests on the customer, partner and admin hosts).
- **A refusal for an unknown address names nobody and carries no address.** `UserId`, `ResourceId`
  and `PayloadJson` are all null on it (`SessionAuditTests`). A refused sign-in on a *known* account is
  `UserId = null` too — attributable by IP only, pending an owner/architect ruling.
- **The payload holds ids, money, enums and versions only** — the PII guard is the standing proof the
  erasure verdict relies on; a new member named `*Email`, `*Phone`, `*Name`, `*Reason`,
  `*Description`, `ConfirmationCode` or an unbounded `string` fails the build naming the record.
- **Erasure survives its own failure.** After `DeleteUserAccount` the rows exist with the three
  request-metadata columns null and `PayloadJson`/`UserId` unchanged; an erasure whose commit throws
  leaves them un-pseudonymised (`CustomerActionAuditErasureTests`).
- **Retention is per row and touches nothing else.** A row at cutoff − 1 day goes and a row at
  cutoff + 1 day stays for the same user; guest rows the same; the admin and employee tables' counts
  are unchanged (`CustomerActionAuditRetentionTests`, SQLite and Postgres).
- **The columns and indexes are the ADR's** — lengths 26/40/45/120/64/100/50/26/100/64, `PayloadJson`
  `jsonb`, the four indexes (`CustomerActionAuditModelMetadataTests`).
- **No log line carries it.** Every `/customeraudit/` path is suppressed wholesale by the admin
  request-log middleware (`RequestLogCustomerAuditPathSuppressionTests`).
