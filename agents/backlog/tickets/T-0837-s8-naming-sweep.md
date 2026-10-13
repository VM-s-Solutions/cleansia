---
id: T-0837
title: Name every company-wide read both worlds reach — rename IsFirstBookingAsync, sweep the remaining repositories, settle GetEmployeeIdsCurrentlyOnAJobAsync and the unfiled T-0606 (S8)
size: S
owner: pm
created: 2026-10-12
updated: 2026-10-13
depends_on: [T-0833]
blocks: []
stories: []
adrs: [0051]
layers: [backend, docs]
security_touching: true
---

## Context

This comes from item 2 of the 2026-10-12 native design ruling, the item on T-0833. The ruling is off-tree, in the Wave F folder: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/p5/phase0/architect_native-lead.json`.
- That ruling narrows S8's third form to its own subject: a method reached from both a request path and a background job.
- It also adds a line to S8 naming one open instance, to be closed by a ticket.

Lines are as of `1768d58`.

- **`IsFirstBookingAsync` reads every company under a name that sounds scoped.**
  - Its contract says it reads every company (`IOrderRepository.cs:10-18`), and its body does: `GetQueryableIgnoringTenant()` at `OrderRepository.cs:20`.
  - Request paths reach it through `CancellationPolicyResolver.cs:43`. A Function reaches it through `SendEmailHandler.cs:290`, which calls the same resolver.
  - Under S8's literal reading (`security-rules.md:741-743`), it is a violation today. The narrowed form covers it as a new case. Either way, the fix is a rename.
- **S8 already names this ticket.** Its open-instance line for `IsFirstBookingAsync` (`security-rules.md:756-759`, T-0833's change) and ADR-0074 cite T-0837. S8 must never cite a ticket that does not say what S8 claims; T-0606, below, is that defect.
- **The sweep is unfinished.**
  - The ruling swept all 11 bypass sites in `OrderRepository.cs`.
  - `UserRepository` and `EmployeeRepository` are confined by their roster tests (`security-rules.md:674-680`).
  - A grep for `IgnoringTenant|IgnoreQueryFilters` under `src/Cleansia.Infra.Database/Repositories/` lists 39 files. Less those three, 36 are unswept. The ruling counted 34.
- **`GetEmployeeIdsCurrentlyOnAJobAsync` is the same trap.** It reads every company under a name that sounds scoped (`OrderRepository.cs:378-398`; `IOrderRepository.cs:122`). Its only caller is a sweep (`SendCleanerJobReminders.cs:131`), so the narrowed rule does not reach it.
- **T-0606 is cited as a gate but was never filed.**
  - `security-rules.md:681` and `adr-0051.md:418`, `:434` cite "(gate pending: T-0606)" for the repository-wide roster check.
  - T-0606 is the highest id the old backlog mentions, and no ticket file exists for it (`agents/backlog/README.md:26-28`).

**If not built:** the next caller of `IsFirstBookingAsync` cannot tell from its name that it reads every company. That is the autocomplete trap T-0815 deleted a method to close.

## Doing (proposed, for Mike)

1. **Rename `IsFirstBookingAsync` to `IsFirstBookingIgnoringTenantAsync`.**
   - Rename it in the interface, the implementation, the caller and the tests, and in the comments at `OrderEntityConfiguration.cs:297` and `BookingPolicy.cs:171`.
   - Remove S8's open-instance line.
2. **Sweep the 36 files.** Look for a company-wide read that both worlds reach, under a name without `IgnoringTenant`. Rename each one found, or record why it is outside S8's subject. The list, with a verdict for each file, goes on this ticket.
3. **`GetEmployeeIdsCurrentlyOnAJobAsync`:** rename it to say `IgnoringTenant`, or leave it, since S8 does not require the rename. Mike's call.
4. **T-0606:** either
   - file the repository-wide roster gate as a ticket and cite that ticket, or
   - change `security-rules.md:681` to "(guidance — no gate)".

   ADR-0051's two citations are dated history. At most, ADR-0051 gets a dated pointer at its head, in the shape of `adr-0057.md:17-21`.

## NOT doing

- No behaviour change: renames and records only.
- T-0833's own rename of the busy read.
- ADR-0039's architecture test, which is not built.

## Acceptance criteria (done looks like)

- [ ] **AC1** — `rg -n 'IsFirstBookingAsync' src` prints nothing, S8 has no open-instance line, and the full suites pass.
- [ ] **AC2** — Each unswept repository file has a verdict recorded here.
- [ ] **AC3** — No page cites T-0606 as a gate that exists.

## Work record

- 2026-10-12 — Filed from the native design ruling on T-0833, its open questions 1 to 3. Reported, not absorbed (CLAUDE.md §2): Mike decides whether it becomes work.
- 2026-10-13 — Citations re-pointed from `207d2f319` to `1768d58`. The backend branch's later edits moved the cited lines of `security-rules.md` and `OrderEntityConfiguration.cs`, and S8's open-instance line, which T-0833 added, did not exist at `207d2f319`. `OrderRepository.cs:13`, the method's signature, became `:20`, where it reads every company.

## Review

Pending.
