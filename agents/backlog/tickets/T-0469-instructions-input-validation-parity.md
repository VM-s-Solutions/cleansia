---
id: T-0469
title: Instructions-field input validation diverges across clients — Swift's grapheme count is more permissive than the backend, and the three platforms now disagree on capping specialInstructions
status: draft
size: S
owner: architect
created: 2026-07-30
updated: 2026-07-30
depends_on: [T-0440, T-0441]
blocks: []
stories: [US-customer-access-instructions]
adrs: []
layers: [architect, ios, android, docs]
security_touching: false
manual_steps: []
sprint: 14
---

## Context

Two findings from the **T-0440** and **T-0441** work that land on the same surface — the booking
confirm-step instruction fields — and are filed together deliberately: same files, same reviewers,
same wave. Splitting them would fragment one decision across two tickets.

### Finding A — Swift's `String.count` is grapheme-based, so the iOS cap is MORE PERMISSIVE than the backend

The backend caps at `MaximumLength(2000)` (`Features/Orders/CreateOrder.cs:136-138`), and
FluentValidation's `MaximumLength` counts **UTF-16 code units** — .NET's `string.Length`.

Swift's `String.count` counts **grapheme clusters**. One user-perceived character can be many UTF-16
units: an emoji with a skin-tone modifier, a ZWJ family sequence, a flag, a Devanagari cluster. So a
naive `prefix(2000)` / `count <= 2000` check on iOS **passes input the backend will reject** — the
client believes it is within the limit and the user gets a server-side 400 with no field-level
feedback.

**Android gets the right property for free:** Kotlin's `String.take(2000)` operates on UTF-16 units,
which is exactly what the backend counts.

**This is genuinely iOS-only** and is **not covered by T-0441's `patterns-mobile.md` hunk**. It was
**reported, not written** — the T-0440 developer was told not to touch `patterns-mobile.md` under the
lane and **complied** (`git diff --stat -- agents/` empty). That was correct; this ticket is where the
write gets sequenced.

### Finding B — the platforms now disagree on capping `specialInstructions`

**T-0440 capped `specialInstructions` on iOS. T-0441's reviewer explicitly ruled the opposite on
Android.** The reasons differ and both are defensible in isolation — iOS generalized the shared
component, so adding a cap there was **new behaviour** rather than a move of existing behaviour —
**but the platforms now diverge on the same field in the same flow.** Two reviewers ruled opposite
ways on the same question, which is precisely the signal that it needs a decision above either.

Web's behaviour must be established too before ruling — do not assume it matches either.

## Deliberation required — NOT `ready`

**Architect panel.** Finding A is a correctness bug with an obvious fix, but Finding B is a genuine
cross-platform consistency decision, and A's *catalog* form (what the law should say) is an
iOS-catalog question.

**Note for routing:** an architect is **already ruling on iOS catalog laws** — hand A's catalog write
to that same panel rather than opening a second one, and let it decide whether the rule is
"iOS-specific" or the more general *"client-side length caps must count the same units the server
counts"*, which would also cover a future Kotlin `codePointCount` mistake or a JS `.length`-vs-`[...str]`
mistake on web.

## Acceptance criteria

- [ ] **AC1 (Finding A — the defect)** — Given an iOS user pastes text whose grapheme count is ≤2000
      but whose UTF-16 length is >2000, When they submit, Then the client rejects or truncates it
      **before** the request, matching the backend's unit exactly. Evidence: a test using a real
      multi-unit cluster (ZWJ family, flag, or skin-tone emoji) — **not** an ASCII string, which
      cannot distinguish the two counts and would make the test vacuous (Gate 0.5 leg 1).
- [ ] **AC2 (Finding A — the catalog)** — The rule is written into `agents/knowledge/patterns-mobile.md`
      (or wherever the panel rules it belongs), stating the unit mismatch and naming the correct Swift
      idiom. **Sequenced under the `patterns-*.md` lane** — see Implementation notes.
- [ ] **AC3 (Finding B — the decision)** — A single ruling on whether `specialInstructions` is capped
      client-side, applied consistently to **iOS, Android and web**, with the reasoning recorded.
      "Each platform decides" is an acceptable outcome **only** if the panel writes down why the
      divergence is harmless — it must be a decision, not an accident.
- [ ] **AC4** — Whatever AC3 rules is **applied**, and the platform that has to change says so
      explicitly in its status log. If the ruling matches what already shipped on both, record that
      and close — do not manufacture a change.
- [ ] **AC5** — `accessInstructions` gets the same treatment as `specialInstructions` under AC3. The
      two fields sit side by side in the same step; a rule that covers one and not the other will
      diverge again within a sprint.

## Out of scope

- Changing the backend's 2000 limit or its validation unit. The backend is the reference; the clients
  conform to it.
- Any other field's validation. If the panel wants a general audit of client-vs-server validation
  units, **that is its own ticket** — note it, do not absorb it.
- The i18n wording of the fields — T-0440 / T-0441.

## Implementation notes

- **⚠️ SHARED-FILE LANE — `agents/knowledge/patterns-mobile.md`: T-0441 ✅ → T-0440 → T-0469.**
  This is the family lane ruled on 2026-07-30 (see `INDEX.md`; durable edit routed through T-0456).
  **AC2's write must not start while T-0440 is live in that file** — T-0440 was already told not to
  re-harvest, so the practical order is: T-0440 closes, then this writes.
- **Archetype:** T-0441's `patterns-mobile.md` hunk is the shape to mirror for AC2.
- Read `agents/knowledge/patterns-mobile.md` and `consistency.md` before proposing the rule's wording.

## Status log
- 2026-07-30 — draft (created by pm from the T-0440 developer's report and the T-0441 reviewer's ruling). Two findings filed as one ticket: same surface, same wave, and B cannot be decided without touching A's platform.
- 2026-07-30 — **not `ready`**: awaiting the architect panel; `depends_on: [T-0440, T-0441]` — both must land before the divergence in Finding B is settled against final code rather than in-flight code.

## Review
<!-- architect / reviewer verdicts here; AC1 must use a real multi-unit cluster -->
