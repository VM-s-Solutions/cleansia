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

> ## ⚠️ UPDATED 2026-08-01 by the T-0440 review — READ THIS BEFORE THE REST OF THE TICKET
>
> The T-0440 reviewer supplied three inputs that **change this ticket's framing**, and two of them
> contradict what the original draft below assumes. **Where they conflict, this block wins.**
>
> **(i) Part B's strongest argument is NOT the cross-platform divergence.** It is a **within-Android**
> inconsistency: Android caps **`accessInstructions`** and **not `specialInstructions`** — **on the
> same screen**. So two customers on one flow get **different treatment at 2001 characters**,
> depending only on which box they typed into. **A within-screen inconsistency is strictly worse than
> a cross-platform one** — no user ever compares two platforms, but a single user can hit both boxes
> in one session. This is the strongest argument for converging on **"cap both, everywhere"**, and it
> is now the lead argument for AC3/AC5.
>
> **(ii) Part A's premise is resolved the OTHER WAY — convergence runs Android → iOS, not iOS →
> Android.** The draft below treats Android's `.take(2000)` as the correct reference because it
> matches the backend's UTF-16 unit. **The reviewer upheld iOS's behaviour instead**, and the
> reasoning is sound:
> - **iOS** drops a character **whole**, wasting **≤10 units of a 2000-unit budget** — 0.5%,
>   invisible to any user.
> - **Android's `.take(2000)`** can leave a **lone high surrogate** — a **broken glyph at the end of a
>   field that can hold a gate code or key-box code.** That is a legibility failure on exactly the
>   content where legibility matters most.
>
> So **AC1 below is inverted**: the fix is not "make iOS count UTF-16 units like Android"; it is
> **"make Android stop severing clusters, like iOS"**, while both stay within the backend's UTF-16
> budget. The unit mismatch is still real and still must not let a client exceed the server's limit —
> but the *cluster-severing* behaviour is the defect, and it is **Android's**.
>
> **(iii) One leg is honestly open and belongs to this ticket:** **what Android's serializer actually
> emits for an orphan surrogate was traced, but NOT executed.** It may emit a replacement character,
> throw, or pass it through. **Execute it** — the fix's shape depends on the answer, and a traced
> behaviour is not a verified one.

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

- [ ] **AC1 (Finding A — REVISED 2026-08-01, direction inverted)** — Two properties must hold on
      **every** client: **(a)** no client may submit text exceeding the backend's **UTF-16** budget of
      2000 (the unit mismatch is real — Swift's grapheme count can be more permissive), **and (b)** no
      client may **sever a grapheme cluster** when truncating. **Android currently violates (b)** —
      `.take(2000)` can leave a lone high surrogate, a broken glyph at the end of a field that may hold
      a **gate or key-box code**. **iOS satisfies both**, wasting ≤10 units of 2000 (0.5%) to do it.
      **Converge Android onto iOS's behaviour**, not the reverse. Evidence: a test using a real
      multi-unit cluster (ZWJ family, flag, or skin-tone emoji) — **not** an ASCII string, which cannot
      distinguish the counts and would be vacuous (Gate 0.5 leg 1).
- [ ] **AC1a (the open leg — EXECUTE it, do not trace it)** — Determine **what Android's serializer
      actually emits for an orphan high surrogate**: a replacement character, a throw, or pass-through.
      This was **traced but never executed** during T-0440's review, and **the shape of AC1's fix
      depends on the answer.** Evidence: an executed test, with the emitted bytes shown. A traced
      behaviour is not a verified one — that distinction has already cost this sprint one false
      blocker (`status/sprint-14.md` §2.12).
- [ ] **AC2 (Finding A — the catalog)** — The rule is written into `agents/knowledge/patterns-mobile.md`
      (or wherever the panel rules it belongs), stating the unit mismatch and naming the correct Swift
      idiom. **Sequenced under the `patterns-*.md` lane** — see Implementation notes.
- [ ] **AC3 (Finding B — the decision)** — A single ruling on whether `specialInstructions` is capped
      client-side, applied consistently to **iOS, Android and web**, with the reasoning recorded.
      **Lead with the within-Android argument, not the cross-platform one:** Android caps
      `accessInstructions` and **not** `specialInstructions` **on the same screen**, so **two customers
      on one flow get different treatment at 2001 characters** depending only on which box they typed
      into. That is strictly worse than a cross-platform divergence — no user compares two platforms,
      but one user can hit both boxes in one session — and it is the strongest argument for **"cap
      both, everywhere"**. "Each platform decides" is an acceptable outcome **only** if the panel
      writes down why the divergence is harmless, and it must answer the within-screen case
      specifically, not just the cross-platform one.
- [ ] **AC3a (F-1 — two adjacent, visually identical, UNLABELED boxes)** — On iOS the hint
      **disappears once the field is non-empty** and there is **no `.accessibilityLabel` on the
      editor**, so a user who fills both fields and scrolls back sees **nothing on screen saying which
      is which**, and **VoiceOver reads two unnamed fields**. This diverges from **both** references:
      Android's float label persists, **and iOS's own Core text field keeps its label floating when
      non-empty** — so the component is inconsistent with its own platform's convention, not just with
      Android's. **T-0440's AC1 as literally worded is satisfied and the fix is one line** — this is
      not a T-0440 defect. It is a **label/parity question across three platforms**, which is why it
      sits with the panel. Rule on it and apply it consistently.
- [ ] **AC4** — Whatever AC3 rules is **applied**, and the platform that has to change says so
      explicitly in its status log. If the ruling matches what already shipped on both, record that
      and close — do not manufacture a change.
- [ ] **AC5** — `accessInstructions` gets the same treatment as `specialInstructions` under **AC3 and
      AC3a**. The two fields sit side by side in the same step; a rule that covers one and not the
      other will diverge again within a sprint — **which is exactly what already happened on Android**
      (see AC3). Both the **cap** ruling and the **label** ruling apply to both fields on all three
      platforms.

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
- 2026-08-01 — **materially updated by the T-0440 review** (see the block at the top). Three changes:
  **Finding A's direction is INVERTED** (converge **Android → iOS**; Android's `.take(2000)` severs
  clusters, iOS's costs 0.5% of the budget to avoid it); **Finding B gained its strongest argument**
  (a **within-Android** inconsistency on one screen, not a cross-platform one); and **F-1 was added as
  AC3a** (two adjacent unlabeled fields, diverging from Android's float label **and** from iOS's own
  Core text field). **AC1a is new and is the one honestly open leg** — Android's serializer behaviour
  on an orphan surrogate was **traced, not executed**. Both T-0440 and T-0441 are now `qa`, so the
  panel can run against near-final code.

## Review
<!-- architect / reviewer verdicts here; AC1 must use a real multi-unit cluster -->
