---
id: T-0817
title: Make backend tenancy comments, contracts and test guards say what the code does
size: S
owner: pm
created: 2026-10-11
updated: 2026-10-11
depends_on: [T-0808]
blocks: []
stories: []
adrs: []
layers: [backend, docs]
security_touching: true
---

## Context

Findings reported by the Wave D reviews, and still true on master, per the read-only post-Wave E ground truth of 2026-10-11 (off-tree: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/post-e-ground-truth-results.json`, finding ids in brackets).

## Doing

- [BE-01] The stale-checkout and recurring auto-cancel comments say which company actually stamps the feed, the credit and the track rows, with pointers to `/flows/cross-cutting`.
- [BE-02] `GetBusyEmployeeIdsInWindowAsync`'s contract states that it is tenant-ignoring on purpose, because a cleaner's commitment with any operator occupies the same person, and that every employee id must be server-derived. The hold resolver's role page agrees.
- [BE-03] `NotifyAsync`'s summary requires a server-read user id, as `NotifyEachAsync`'s already does.
- [BE-04] `PushSubjectNamesTheEventTests` requires every `NotifyEachAsync` caller to be on the deliberately-keyed allowlist, and walks the whole AppServices tree, not only `Features/`.
- [BE-05] `BootDatabaseIoTests`' eager-probe ceiling test asserts on the probed composition alone, so a cold first leg under parallel load cannot fail it.
- [BE-07] All five request-logging middleware copies prove the oversize-body 413 on real Kestrel, through one theory over the existing host levels.
- [IOS-13] The DTO comment on a missing currency code says what both apps do.

## NOT doing

- No behaviour change in any sweep or read.
- The failure-boundary findings BE-11 to BE-14 are listed for Mike.

## Acceptance criteria (done looks like)

- [ ] **AC1** — Each comment and contract matches the code it describes, with file:line in the review.
- [ ] **AC2** — The strengthened guards fail when their rule is broken (RED), and pass on master's code.
- [ ] **AC3** — The unit and host suites pass.

## Work record

- 2026-10-11 — Filed from the post-Wave E ground truth and Mike's rulings.

## Review

Pending.
