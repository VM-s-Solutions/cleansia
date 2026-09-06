---
id: T-0684
title: A CreateDispute response with no id is an empty string on Android and a refusal on iOS
status: todo
size: S
owner: —
created: 2026-09-06
updated: 2026-09-06
depends_on: []
blocks: []
stories: []
adrs: []
layers: [android, ios]
security_touching: false
manual_steps: []
sprint: 16
---

## Context

`CreateDispute.Response` is `record Response(string DisputeId)` — non-nullable on the backend. The
OpenAPI schema declares no `required` array, so **both generators type the property as optional**,
and the two clients then disagree about what to do when it is absent:

| client | behaviour on `{}` or a null id |
|---|---|
| Android — `DisputeApi.create`, `.mapWire { it?.disputeId.orEmpty() }` | success carrying an empty string |
| iOS — `DisputeClient.create`, `guard let ... else { throw ApiError("dispute.malformed") }` | refusal |

Both behaviours are now **pinned by tests** — `DisputeWireTest`
`aCreateResponseWithNoIdCurrentlyYieldsAnEmptyIdRatherThanRefusing` and `DisputeWireFormatTests`
`testAResponseWithNoIdDecodesToNilSoTheClientCanRefuseIt` — so whichever way this is settled, the
test on the side that changes fails loudly rather than drifting.

**Why it matters even though the backend cannot currently send it.** The id addresses the evidence
upload that follows: `uploadEvidence(disputeId:file:)`. An empty id does not fail — it uploads the
customer's photo against an empty id. The iOS refusal is the safer of the two, which is why it was
written that way when the compile error was fixed.

## Acceptance criteria

- [ ] **AC1** — Given a CreateDispute response with no id, When either mobile client handles it,
      Then both behave the same way, and the pinned test on the platform that changes is updated.
- [ ] **AC2** — If the answer is "refuse", Then the refusal reaches the customer as a message rather
      than a silent no-op, and the dispute they just created is still reachable from the list.

## Out of scope

- **Marking `disputeId` required in the schema.** That is arguably the real fix — it removes the
  optionality from both generators at once — but it is a spec change carrying regeneration and
  parity work on every client, and it deserves deciding on its own terms rather than folded in here.

## Status log

- 2026-09-06 — filed while adding the wire tests that were missing on both platforms. Neither client
  can reach this against the real backend today; it is a divergence waiting for a schema change, a
  proxy, or a partial outage to expose it.
