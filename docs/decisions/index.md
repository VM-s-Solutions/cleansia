# Decisions

Every architecture and business decision on record, with the argument that settled it.

**67 records.** A decision keeps a stable id — `ADR-0037` — and roughly six hundred source
files cite ids in that form. The id is the reference, not the file name or the title, so a record can
be retitled without breaking a single citation.

```
ADR-0037   →   /decisions/adr-0037
```

## Supersession

An arrow means *replaces, in whole or in part*. A superseded record is kept rather than deleted: code
may still cite it, and the fact that a decision was reversed is part of the history.

```mermaid
flowchart LR
  A0009[ADR-0009] --> A0006[ADR-0006]
  A0014[ADR-0014] --> A0013[ADR-0013]
  A0023[ADR-0023] --> A0010[ADR-0010]
  A0024[ADR-0024] --> A0001[ADR-0001]
  A0025[ADR-0025] --> A0002[ADR-0002]
  A0026[ADR-0026] --> A0024[ADR-0024]
  A0027[ADR-0027] --> A0026[ADR-0026]
  A0028[ADR-0028] --> A0017[ADR-0017]
  A0029[ADR-0029] --> A0002[ADR-0002]
  A0030[ADR-0030] --> A0024[ADR-0024]
  A0034[ADR-0034] --> A0017[ADR-0017]
  A0037[ADR-0037] --> A0036[ADR-0036]
  A0040[ADR-0040] --> A0037[ADR-0037]
  A0042[ADR-0042] --> A0037[ADR-0037]
  A0049[ADR-0049] --> A0045[ADR-0045]
  A0053[ADR-0053] --> A0037[ADR-0037]
  A0057[ADR-0057] --> A0037[ADR-0037]
  A0061[ADR-0061] --> A0050[ADR-0050]
  A0061[ADR-0061] --> A0017[ADR-0017]
  A0061[ADR-0061] --> A0046[ADR-0046]
  A0063[ADR-0063] --> A0062[ADR-0062]
  A0066[ADR-0066] --> A0001[ADR-0001]
  A0067[ADR-0067] --> A0057[ADR-0057]
  classDef old fill:#e5e7eb,stroke:#6b7280,color:#374151
  class A0001,A0002,A0006,A0010,A0013,A0017,A0024,A0026,A0036,A0037,A0045,A0046,A0050,A0057,A0062 old
```

Grey nodes are superseded in whole or in part. Not every later record replaces an earlier one:
[ADR-0062](./adr-0062) **extends** [ADR-0012](./adr-0012) — the admin audit gate gains a second,
opt-in arm for customer acts and every sentence about the admin table stays true — so there is no
arrow between them. [ADR-0063](./adr-0063) supersedes **one paragraph** of ADR-0062 (D4's version
constant became a stored, dated document); the rest of ADR-0062 stands and was **amended in place on
2026-09-14** with the owner's rulings, each decision carrying a dated block. [ADR-0061](./adr-0061)
supersedes **one section** of [ADR-0046](./adr-0046) (§D3's global namespace — each operating company
numbers its own payout invoices since 2026-09-15, and ADR-0046 carries the superseding note at its
head); ADR-0061 itself was **amended in place on 2026-09-15** with the owner's rulings on its five open
items and carries a §Rulings table. [ADR-0064](./adr-0064) **amends ADR-0061 by reference** (its O-3
default and D1's "seed-only writer" for the `Tenants` row) and adds a fourth market predicate to
[ADR-0058](./adr-0058) D1 — every other sentence of both stands, so there is no arrow. **Three records
were proposed on 2026-09-19** from the owner's rulings of that day and are `proposed` until their tickets
ship: [ADR-0065](./adr-0065) (administrators are told — an in-app feed and an e-mail per event) supersedes
nothing and composes with ADR-0002, ADR-0025, ADR-0061 and ADR-0064; [ADR-0066](./adr-0066) (four
administrator roles) supersedes **ADR-0001 D2's table** for every admin-host row — the arrow — and keeps
D1, D3, D4 and D5; [ADR-0067](./adr-0067) (`Confirmed → New` when the last cleaner leaves) supersedes
**ADR-0057's one open consequence** — the arrow — and leaves its status ruling standing, with a dated
pointer at ADR-0057's head.

## All records

| | Decision | Status |
|---|---|---|
| **[ADR-0001](./adr-0001)** | Authorization model ⟲ | `accepted` |
| **[ADR-0002](./adr-0002)** | Outbox dispatch contract ⟲ | `accepted` |
| **[ADR-0003](./adr-0003)** | Partitioned rate limiting | `accepted` |
| **[ADR-0004](./adr-0004)** | Fiscal receipt idempotency boundary | `accepted` |
| **[ADR-0005](./adr-0005)** | Integration resilience contract | `accepted` |
| **[ADR-0006](./adr-0006)** | Refund dispute money path ⟲ | `accepted` |
| **[ADR-0007](./adr-0007)** | Soft delete policy | `accepted` |
| **[ADR-0008](./adr-0008)** | Outbox table and drainer | `accepted` |
| **[ADR-0009](./adr-0009)** | Refund policy | `accepted` |
| **[ADR-0010](./adr-0010)** | Durable consumer idempotency ⟲ | `accepted` |
| **[ADR-0011](./adr-0011)** | Mobile apiresult contract | `accepted` |
| **[ADR-0012](./adr-0012)** | Admin action audit log | `accepted` |
| **[ADR-0013](./adr-0013)** | Ios app architecture and port strategy ⟲ | `accepted` |
| **[ADR-0014](./adr-0014)** | Ios deployment target ios16 and state mechanism | `accepted` |
| **[ADR-0015](./adr-0015)** | Azure dev deployment bicep and github environments | `accepted` |
| **[ADR-0016](./adr-0016)** | Apple app review compliance and ios quality bar | `accepted` |
| **[ADR-0017](./adr-0017)** | Multi region expansion seam and its composition… ⟲ | `accepted` |
| **[ADR-0018](./adr-0018)** | Ios design parity principle | `accepted` |
| **[ADR-0019](./adr-0019)** | Ios generated client authenticates via the core… | `accepted` |
| **[ADR-0020](./adr-0020)** | Ios partner router is a flat enum root switch gated… | `accepted` |
| **[ADR-0021](./adr-0021)** | Ios non modal 3 snap map sheet on the ios16 floor | `accepted` |
| **[ADR-0022](./adr-0022)** | Ios shell single navigation stack pager and pill bar | `accepted` |
| **[ADR-0023](./adr-0023)** | Per consumer claim ordering email claims after… | `accepted` |
| **[ADR-0024](./adr-0024)** | Mobile access token ttl is the device revocation… ⟲ | `accepted` |
| **[ADR-0025](./adr-0025)** | Ios push display per platform apns alert with loc… | `accepted` |
| **[ADR-0026](./adr-0026)** | Immediate device revocation via device id claim and… ⟲ | `accepted` |
| **[ADR-0027](./adr-0027)** | Immediate user session cutoff on password reset via… | `accepted` |
| **[ADR-0028](./adr-0028)** | Multi tenant activation pack | `accepted` |
| **[ADR-0029](./adr-0029)** | Ios live activity for in progress clean | `accepted` |
| **[ADR-0030](./adr-0030)** | Web admin access token ttl 15 min | `accepted` |
| **[ADR-0031](./adr-0031)** | Nswag regen drift is guarded at regen time | `accepted` |
| **[ADR-0032](./adr-0032)** | Catalog law declarations require a named ci gate | `accepted` |
| **[ADR-0033](./adr-0033)** | Catalog edit authority the routing test and cross… | `accepted` |
| **[ADR-0034](./adr-0034)** | Partner payout details shape | `accepted` |
| **[ADR-0035](./adr-0035)** | Metered membership benefit usage | `accepted` |
| **[ADR-0036](./adr-0036)** | Preferred cleaner first refusal hold ⟲ | `accepted` |
| **[ADR-0037](./adr-0037)** | Order offerability is a payment qualified status… ⟲ | `accepted` |
| **[ADR-0038](./adr-0038)** | Promo redemption reservation runs after the uow… | `accepted` |
| **[ADR-0039](./adr-0039)** | Preferred cleaner slot availability is checked at… | `accepted` |
| **[ADR-0040](./adr-0040)** | Order currentstatus is non nullable the pre… | `proposed` |
| **[ADR-0041](./adr-0041)** | Self billing agreement is a versioned append only… | `accepted` |
| **[ADR-0042](./adr-0042)** | Shared wire enums are generated from the nswag… | `proposed` |
| **[ADR-0043](./adr-0043)** | User artifact metadata is scrubbed at intake by… | `accepted` |
| **[ADR-0044](./adr-0044)** | Stored content type is byte derived on every intake | `accepted` |
| **[ADR-0045](./adr-0045)** | Favourite cleaner is a reservation the cleaner must… ⟲ | `accepted` |
| **[ADR-0046](./adr-0046)** | Payout invoice variable symbol is a claimed number… ⟲ (§D3 superseded 2026-09-15: per company) | `accepted` |
| **[ADR-0047](./adr-0047)** | A server redacted field is rendered off its own… | `accepted` |
| **[ADR-0048](./adr-0048)** | A generated dto is refused at the repository… | `accepted` |
| **[ADR-0049](./adr-0049)** | A disclosure block is withheld by the server when… | `accepted` |
| **[ADR-0050](./adr-0050)** | A dormant tenant column arbitrates nothing the… ⟲ | `accepted` (as amended) |
| **[ADR-0051](./adr-0051)** | A reads tenancy posture is decided by the write… | `proposed` |
| **[ADR-0052](./adr-0052)** | A cleaners own deletion files a request; only an admin… | `proposed` |
| **[ADR-0053](./adr-0053)** | The live-commitment cap is one admins decision about one… | `accepted` |
| **[ADR-0054](./adr-0054)** | Cleaner job reminders dedupe on a stamp per recipient… (Q-PUSH-01 ruled 2026-09-15: the digest stays non-silenceable) | `accepted` |
| **[ADR-0055](./adr-0055)** | A cleaner may set off or start only inside a 60-minute… | `accepted` |
| **[ADR-0056](./adr-0056)** | Property size is two integers; the label is per-country… | `proposed` |
| **[ADR-0057](./adr-0057)** | Confirmed means a cleaner took the job, and nothing else ⟲ (its open consequence taken by ADR-0067, 2026-09-19) | `accepted` |
| **[ADR-0058](./adr-0058)** | A customer's market is chosen, remembered, and overridden by the address | `accepted` |
| **[ADR-0059](./adr-0059)** | Cleansia Plus is priced per market | `accepted` |
| **[ADR-0060](./adr-0060)** | Money figures in copy come from the market, not the translation | `accepted` |
| **[ADR-0061](./adr-0061)** | Tenancy is active from day one: one tenant per operating company (amended 2026-09-14 and 2026-09-15 — the FK, `TenantAuditable`, per-company payout numbering and settings) | `accepted` |
| **[ADR-0062](./adr-0062)** | A customer's actions are recorded for incident defence, and support reads them in the admin panel (extends ADR-0012; amended 2026-09-14 and 2026-09-15) | `accepted` |
| **[ADR-0063](./adr-0063)** | Legal documents are versioned by effective date, stored per market, and a consent stamps the version | `accepted` |
| **[ADR-0064](./adr-0064)** | A company's lifecycle: deactivation, wind-down, archive (amends ADR-0061 O-3/D1 and ADR-0058 D1 by reference) | `accepted` |
| **[ADR-0065](./adr-0065)** | Administrators are told: an in-app feed and an e-mail per event (owner ruling D5, 2026-09-19; T-0768 / T-0769 / T-0774 / T-0775) | `proposed` |
| **[ADR-0066](./adr-0066)** | Four administrator roles: Administrator, Manager, Support, Accountant (owner ruling D8, 2026-09-19; supersedes ADR-0001 D2's admin rows; T-0748 / T-0773) | `proposed` |
| **[ADR-0067](./adr-0067)** | Confirmed → New when the last cleaner leaves; the administrators are told (owner ruling D2, 2026-09-19; supersedes ADR-0057's open consequence; T-0770) | `proposed` |

⟲ = superseded in whole or in part by a later record.

## Where the deliberation lives

Several of these were argued by an author→challenger→lead panel before being accepted. The challenge
and draft documents are **not published here** — they are the argument, not the decision — and are
archived in the repository under `agents/archive/2026-08/adr-deliberation/`.
