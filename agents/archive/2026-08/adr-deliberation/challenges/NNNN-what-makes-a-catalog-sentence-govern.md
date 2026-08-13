# ADR-NNNN draft ("what makes a catalog sentence *govern*") — Challenger pass

Role: **CHALLENGER**. Fifth instance on this question — did not write ADR-0033, did not run its
challenger round, did not write its independent lead pass, did not author this draft. Target:
`agents/archive/2026-08/adr-deliberation/drafts/NNNN-what-makes-a-catalog-sentence-govern.md` @ `6fe38d0e`.

Gate 0 discipline: **REFUTED by default.** Every claim below cites a commit, a hunk, a file:line or a
ticket. Where something cannot be settled I say so in §"What I could not verify" rather than assert it.

> ## Method — and why this pass is not the two before it
>
> Both prior instances declared the same limitation: **no shell, so not one catalog edit was read as a
> diff.** I was handed the diffs. My evidence base is:
>
> - **the catalog-edit corpus** — every commit touching `agents/knowledge/*.md` (94), newest first,
>   with full diffs for `patterns-{mobile,frontend,backend}.md`, `consistency.md`, `conventions.md`;
> - the draft, ADR-0033 (including the appended independent lead pass), `challenges/0033-floor.md`,
>   `architecture/decisions/catalog-governance.md`;
> - the tickets behind the retro rows, and — new in this pass — **the shipped code the disputed catalog
>   sentences summarize** (Kotlin/Swift, read directly).
>
> **What that buys is one thing, and it is the thing that decides this round:** the draft located every
> candidate governing sentence by `Grep` over `agents/knowledge/` **as the tree stands today** — i.e.
> *after* each entry it is judging had already been applied. A diff shows what an entry **deleted**.
> That difference falsifies one of the ten rows outright (**CH-A**) and it is not a detail: an entry
> that deletes its governing sentence is the highest-signal instance of test 2 there is, and it is
> precisely the instance the draft's method cannot see.
>
> **I have no `Bash` either.** I did not re-run `git log`; I read the corpus the coordinator generated.
> Commit→date attributions below are read off that corpus's diff blocks.

**Seven findings. Three blocking (CH-A, CH-B, CH-C).** One limb — **D3** — I attacked with six
candidate triggers and **could not break; it is SUSTAINED**, and I say what I tried, because the last
round's credibility problem was manufactured disagreement.

---

### CH-A — Retro row 8 is **wrong**, and the method that produced it is blind in exactly one direction: the sentence an edit **deletes** — BLOCKING

Row 8 of §Retro-validation:

> | 8 | **T-0527** `:477-492` a server-charged price is never estimated client-side | nearest are `:468-475` … and `:505-515` … — neither reaches a price | none | **no** | inline | inline … | ✅ |

**There was a governing sentence. It named the withdrawn form. The same commit deleted it.**

`ab077504` (2026-08-04, *"fix(mobile): both cancel sheets quote the server instead of guessing"*) carries
**three** hunks in `patterns-mobile.md`, not the one the draft scored. The third is a replacement at
`@@ -1272,9 +1315,9 @@`, and it removes this:

```diff
-  Cancel is a modal `.sheet` previewing the fee/refund via a pure TDD'd
-  `CancellationFeePreview` (oops≤15m/free≥24h/half 4–24h/full<4h, the `CancelOrderSheet.kt` tiers; server recomputes
-  authoritatively).
+  Cancel is a modal `.sheet` rendering the **server's** quote
+  (`GET /api/Order/CancellationPreview`) — the client-side tier ladder both platforms shipped is deleted, see the fee-preview rule
+  above (T-0527).
```

Apply the draft's own D1 to it. Sentence `S` = the deleted clause; entry `E` = `:477-492`
(*"no fallback ladder — a fallback that disagrees IS the defect"*). Artifact both reach:
**`CleansiaCustomer/Sources/Features/Orders/CancellationFeePreview.swift`** — named by `S` verbatim,
and named by `E`'s ticket as the thing being deleted (`T-0527-…md:66-67`,
*"`CancellationFeePreview.swift` is **deleted** (not amended…)"*; `:180`, *"**`CancellationFeePreview.swift` is gone**"*).
`S` requires it; `E` forbids it. **Verdicts differ ⇒ `S` governs ⇒ Architect.**

It does not even need D1. The **accepted** floor's *decidable* disjunct — *"replacing a named canonical
form"*, the half `challenges/0033-floor.md` CH-1 explicitly cleared as checkable — fires on it directly.

**What actually happened: inline.** `T-0527-…md:4,6,13` — `status: in_review`, **`owner: qa`**,
`layers: [android, ios]`, `adrs: []`; `:286-288` *"### Harvested back into the catalog"*. No Architect.
And it is not a trivial one: it deleted a shipped file on one platform, a shipped ladder on the other
(`CancelOrderSheet.kt:344-404`), **and rewrote a committed test suite that pinned the old schedule**
(`T-0527-…md:51-54`, `CancellationFeePreviewTests`, *"It is in scope: it is deleted or rewritten … in
the same change"*). That is retro row 3's shape — the edit converts its own violators, so **test 1 does
not fire and the floor is the only thing standing** — which the ADR itself names as the floor's
load-bearing case.

**Three consequences, in ascending order of damage:**

1. **Row 8's verdict flips from `inline ✅ match` to `Architect ❌ divergence`.** The headline score
   "10 determinate, **7 agree with history**" is wrong as printed.
2. **The one divergence the draft claims to have *found* (row 9) is a false positive (CH-B), and the
   one it did not find is real.** The pass's net discovery is negative.
3. **The method is structurally blind, not unlucky.** §Gate 0.5 leg 3 item 5 of the draft says it:
   *"Negative claims ('none nameable', rows 6/7/8/10) are searched, not proven. I searched today's
   `agents/knowledge/`… A sentence deleted since would not appear."* It was declared and then not
   weighted. **Four of the ten rows rest on that negative. One is now falsified. The other three were
   produced by the identical procedure.** A corpus in which the negatives are generated by a procedure
   with a known one-sided error, and one of them is shown false, does not support §Ruling 1's
   evidentiary claim — and D1's whole appeal is that its verdicts are checkable by opening a file.

**And note which direction the blindness runs.** Grepping the post-edit tree can only ever *miss*
governing sentences; it can never invent one. So every "none nameable ⇒ inline" in the table is an
under-estimate of test 2's firing rate, and the draft's §Consequences worry (*"row 9's class may be a
rate"*) is understated in the direction that matters for the inline lane, not overstated.

---

### CH-B — Row 9, D1's only claimed catch, is a **false positive** — and the reason generalizes into C5's reductio returning through the artifact door — BLOCKING

Row 9 names the artifact as *"an Android `ProfileViewModel` whose retry watermark is set once and
**never cleared**: `:562-565` rules it compliant, the entry rules it 'drops the avatar to initials
permanently'."*

**That artifact is not merely absent. It was shipped against, and tested against, by the very ticket
that wrote the sentence D1 says governs.**

- `src/cleansia_android/customer-app/…/features/profile/ProfileViewModel.kt:178-181`:
  `/** Hands the retry budget back… */ fun onAvatarLoadSucceeded() { avatarRetriedFor = null }`
- wired at **three** surfaces: `CleansiaNavHost.kt:430`, `MainShell.kt:302`,
  `ProfileTab.kt:298` (via `EditProfileScreen.kt:148`)
- pinned by a test: `ProfileViewModelTest.kt:642`
- and **T-0448's own ticket** — the ticket that harvested `:562-565` — lists it in scope at `:264`
  (`onAvatarLoadFailed` / **`onAvatarLoadSucceeded`**) and records at `:332-337`: *"A **successful**
  load hands the budget back… Four tests: … `a successful load restores the retry budget`."*
- T-0449's own ticket says so too, at `:307`: *"**Android clears it in `onAvatarLoadSucceeded`**; iOS
  had no success path at all."*

The catalog entry at `:325` **cites Android's method as the model** — `ProfileViewModel.avatarLoadSucceeded`
/ Android `onAvatarLoadSucceeded`. **Nothing was withdrawn from Android.** `:562-565` was an *incomplete
summary* of behaviour Android had already shipped and tested; the 2026-08-05 append **completed the
description**. Test 2's operative phrase is *"narrow latitude the catalog previously left open"* — and
there was no latitude. It was never open, in the code or in the ticket that produced the sentence.

**Now the generalization, which is the blocking part.** D1 computes *"`S`'s verdict on the artifact"*
from **`S`'s literal text, read in isolation from the ticket, the code and the tests it summarizes**.
Catalog sentences are summaries; summaries under-specify by construction. So for essentially **any**
summary sentence `S` and **any** later entry `E` that adds precision on the same subject, an artifact
satisfying `S`'s literal text and violating `E` is nameable in one line. Therefore:

> **D1 fires on every precision-adding refinement of an existing entry.**

Those are exactly the edits `conventions.md:122-124` sends inline — *"a better example, a sharper 'why',
a newly observed footgun"*. This is C5's reductio in a new costume: not *"every entry forbids the
less-consistent alternative"* but *"every clarification of an under-specified sentence has a nameable
conflicting artifact."* And the inline lane dies the same death.

**The draft has no answer available in its own vocabulary.** D2 gives the author exactly two moves:
show the two prescriptions **compose** on the named artifact, or concede. T-0449's author can do
neither — the two literal texts *do* rule the hypothetical VM differently. The correct answer —
*"that artifact is already excluded by the test the earlier sentence summarizes
(`ProfileViewModelTest.kt:642`)"* — has **no slot in D1 or D2**. That is a gap in the decision, and it
is the gap the only worked "new catch" fell into. (I am not writing the repair; the third instance rules.)

**Two smaller corrections that ride with this row:**

- **Chronology CONFIRMED, against my expectation.** `:562-565` (T-0448) and the T-0449 entry landed in
  the **same commit**, `0e4ede1b` 2026-08-01 — written together, one hand, one commit message
  (*"unblock the avatar chain"*). The tested hunk (`:319-329`, *"the guard is released by a successful
  render"*) is a **later append**, `4f81dce7` 2026-08-05, `@@ -316,6 +316,17 @@`. The draft's `Actual:
  inline (T-0449-…md:316)` citation **is** for the later hunk (the ticket was updated 2026-08-05 and
  `:316-317` describes exactly it). So the row is not misattributed; it is simply wrong on substance.
- **The draft's own §Consequences names this and then scores it as a win.** *"Row 9 is D1 working, not
  D1 failing"* is the one claim in the draft I can falsify from the tree.

---

### CH-C — D1's **"reach"** re-imports the subject-granularity problem Alternative A was rejected for. Demonstrated on the draft's own row 10 — BLOCKING

This is the soft edge the author named (attack surface 2: *"show me a real entry where the conflicting
artifact is easy to imagine for one reviewer and invisible to another"*). Here it is, and it is **inside
the validation table**.

Row 10: *"T-0432 `:255-263` iOS `CleansiaDangerButton` | `:249-253` (Android) 'never duplicate a `:core`
component' | none — `:249-253` reaches `cz.cleansia.core.ui.components`, the entry reaches
`CleansiaCore/Components`; no artifact is inside both | **no** | inline"*.

**I can name the artifact, and the entry names it too.**

- `agents/knowledge/patterns-mobile.md:247` is the heading `## Shared UI & theme`. `:249-253` is its
  **opening paragraph**, ending *"Never style raw components one-off; **never duplicate a `:core`
  component**."*
- The section runs to `:455` (`## Navigation — typed routes`). The **iOS** section does not begin until
  **`:569`** (`## iOS — SwiftUI/MVVM parity port`). So `:255-263` (T-0432, iOS), `:265-276` (T-0473,
  iOS), `:292-304` (T-0451, iOS) and `:306-329` (T-0449, iOS) all sit **inside a section whose
  preamble is the Android component rule**. The corpus confirms the placement was deliberate:
  `4d8b3978` (2026-07-22) inserted the T-0432 blockquote with the context line
  `raw components one-off; never duplicate a `:core` component.` **immediately above it**.
- The artifact: **`src/cleansia_ios/CleansiaPartner/Sources/Features/Profile/ProfileHubContent.swift:298`** —
  `private struct LogoutRow: View`, used at `:30`. It is a hand-rolled duplicate of the Core danger
  affordance, and `catalog-governance.md:264` already books it as such (`(gate pending: FT-5)`).
- Verdicts: under `:253` read as the **section's** rule, duplicating a Core component is a flat defect.
  Under the T-0432 entry (`:262-263`) it is an acknowledged, tolerated *"remaining convergence target"*
  with a pending gate. **Same artifact, different verdicts.**

So: reviewer A reads `:249-253` as scoped to `cz.cleansia.core.ui.components` (the draft's reading) and
names nothing ⇒ **inline**. Reviewer B reads it as the section preamble it is positioned as, names
`ProfileHubContent.swift:298` ⇒ **Architect**. **That is Case β, one level down, reproduced inside the
table that is supposed to retire Case β.**

**Why this is not a quibble about one row.** D1's rejection of Alternative A is:

> *"Any definition phrased as 'applied to this entry's subject…' relocates the indeterminacy from 'what
> does the sentence mean' to 'what is the subject'… **An artifact has no granularity problem.**"*

An artifact does not. **"Reach" does.** D1 defines it: *"the artifact falls inside **the scope `S`
prescribes for**"*. The scope `S` prescribes for **is** `S`'s subject. So D1 does not remove the
subject question; it moves it one clause to the right and stops arguing about it. The draft's central
claim of superiority over Alternative A does not survive its own row 10.

**A second instance, weaker but the same shape, on row 8's own neighbourhood.** `:468-475` prescribes
that a conditional presentation branch is *"a pure resolver"* returning **semantic cases**, with *"the
composable resolv[ing] `stringResource` per case"*. T-0527's `:481` prescribes a resolver
`tier → (titleRes, amountRes, args, severity)` — **resource ids out of the resolver**. On the artifact
`CancellationFeeCallout.kt` the two rule differently *if* `:468-475` reaches a callout, and not if its
subject is narrowly *"a conditional list of chips/pills/rows"*. Nameable or not-nameable, by choice of
scope, on today's text. I flag this as **arguable, not established** — unlike row 10, where the entry
itself names the artifact.

---

### CH-D — The corpus is not ten independent cases. It is about six routing events, and both "unmoved divergences" come from one architect sitting

Verified from the corpus and from in-entry signatures:

| Rows | Actually one event |
|---|---|
| **5, 6, 7** (T-0397 ×2, T-0379) | all three carry, or trace to, ratifications dated **2026-07-19**: `patterns-mobile.md:1000` *"(Architect-ratified T-0397, 2026-07-19…)"*, `:635` *"(Architect-ratified T-0397, 2026-07-19…)"*, `:602` *"(Architect-ratified T-0379, 2026-07-19…)"*. `challenges/0033-floor.md` CH-5 already says rows 5 and 6 are *"the same harvest ticket … ratified in the same sitting"* |
| **3, 9** (+ row 9's governing sentence) | **one commit** — `0e4ede1b` 2026-08-01 introduced the T-0473 entry, the T-0449 entry **and** the T-0448 section that supplies `:562-565` |

So ten rows compress to roughly **six independent routing decisions**, and **both** of the "divergences
D1 does not move and does not claim to" (rows 6 and 7) come from **the same day's architect sitting** as
row 5. The draft offers rows 6 and 7 as "the prize" for beating D3. They are one event, not two.

Chronology I **confirmed sound**, because silence is not assent:

- **Row 1 (T-0441)** — entry `1d85b35f` 2026-08-01, hunk `@@ -168,6 +168,18 @@`, i.e. inserted *after*
  the repository-error paragraph that supplies `:167-175`. Predates. ✅
- **Row 2 (T-0451)** — governing sentence (the `CleansiaColors` / `Color.dynamic(light:dark:)` mapping
  row) introduced in **`c1009c63` 2026-06-25**; entry `1c8fdd00` 2026-08-01. Predates by five weeks. ✅
- **Row 4 (T-0349)** — `:990` (*"feature/VM import no MapKit"*) introduced **`76fc48ab` 2026-06-27**;
  the T-0349 entry introduced **`04f98937` 2026-06-30**. Predates by three days. ✅ This remains the
  strongest row in the corpus and I did not dent it.
- **Row 5 (T-0397 `.medium`)** — I did **not** establish when `:1241`'s grant was introduced. Declared.

---

### CH-E — Row 3, the case the whole ADR is built on, is determinate because the **catalog says so**, not because of D1 — and that is visible without a shell

The coordinator settled the chronology: `:520-522` was introduced by **`2012b014`, 2026-08-02**; the
T-0473 entry by **`0e4ede1b`, 2026-08-01**. Taken as given. But the diff shows something the chronology
alone does not, and it is worse for §Ruling 1 than the date is:

`2012b014`'s added paragraph reads, in one breath:

> *"So a screen with no test seam gets a **source-text scan scoped to the file** … — **plus a call-site
> pin, because a resolver test does not cover the call site (the T-0473 rule)**: assert the card still
> calls `orderStatusLabel(…)` … **`OrderDetailCardStringsTest` is the model**."*

So the candidate governing sentence and the entry it allegedly conflicts with were **written by one
author, in one paragraph, with the earlier rule cited by name as a rule the new sentence composes
with**, and the composing artifact named in the same sentence.

Two things follow:

1. **Case β's "two reviewers reach opposite verdicts" is weaker than filed**, and not for want of a
   shell. Reviewer A must quote `:520-522` as governing while ignoring the clause two lines later that
   names T-0473 as a rule it is *adding to*. Reading the **entry** rather than the **sentence** answers
   it. That is worth knowing before anyone prices an ADR against it.
2. **Row 3 does not discriminate between D1 and Alternative A.** The draft's worked example concludes
   *"the obvious candidate is the model test `:524` itself names — `OrderDetailCardStringsTest` — and on
   that artifact the two rules **compose**"*. It reaches that by **reading the composition the catalog
   author wrote down**. The lead's nominee (*"does `S`, applied to `E`'s subject, yield a prescription
   `E` contradicts?"*) returns the same answer for the same reason. **The one case D1 claims to convert
   from indeterminate to determinate is the one case where every candidate definition agrees.**

D1's headline consequence — *"the one recorded indeterminacy becomes determinate"* — is therefore
carried by a case that is (a) not historical, (b) self-answering on its own text, and (c) not a
discriminator between the two definitions on the table.

---

### CH-F — N1 / N-E is **mis-characterized** (correction, not blocking)

The draft files N1: *"`patterns-mobile.md:320-329` (T-0449) carries a **prescriptive** cross-stack claim
with no file:line … `Android onAvatarLoadSucceeded` is cited with no file:line … evidence that ADR-0033
D2 is as unenforced today as ADR-0032's label was."*

Under ADR-0033 D2 the line between descriptive and prescriptive is *"can the next reader verify this by
reading what is in the repo?"* The `Android onAvatarLoadSucceeded` citation is **verifiable by
reading**: `ProfileViewModel.kt:178-181`, wired at `CleansiaNavHost.kt:430`, `MainShell.kt:302`,
`EditProfileScreen.kt:148`, pinned by `ProfileViewModelTest.kt:642`. That is a **structural** claim
missing its file:line — i.e. **Block B's shape exactly**, a two-line repair, not a routing failure.

What **does** survive as prescriptive is the narrower clause *"Both platforms plumb the pair through
**every** surface that draws the disc"* — a forward obligation on Android written from an iOS ticket.
N1 should be re-scoped to that clause. As filed it overstates, and it overstates in the same direction
as row 9: by reading a sentence that summarizes shipped code as if the code were not there.

---

### CH-G — Block C′ step 3 is unguarded: a developer whose edit **routes** is still told to write it into the catalog (minor)

`conventions.md:120-130` today puts the *action* inside each branch: bullet 1 says *"the developer edits
the relevant `patterns-*.md`… entry in the same change"*; bullet 2 says *"Raise it via the ticket;
**don't unilaterally redefine the standard**."* Block C′ hoists the action out of the branch:

```
2. **Decide who ratifies it** — the routing test below. Most edits are yours to make; some are not.
3. **Write it into the catalog** so it becomes the canonical form everyone follows next time, …
```

Read 1→2→3, a developer whose edit fires test 1, 2 or 3 at step 2 still arrives at an unconditional
step 3. The routing test's own preamble carries the guard (*"the first one that fires routes the edit to
the Architect"*), so this is recoverable — but a page that teaches two things at once, in the numbered
list a developer follows, is the L3 defect Block C′ exists to remove, at one level of nesting down.

---

### D3 (no fourth test) — **SUSTAINED.** Six triggers built and killed; here is every one

The author set an explicit target: *"build a trigger that fires on retro rows 6 and 7 but NOT on
`patterns-mobile.md:559-561`, and you beat D3 outright."* I read all three texts and tried six.

The three texts, so the reader can check my work:

- **Row 6** = `patterns-mobile.md:635` — a cell in the Android→iOS parity table. Both rejected forms are
  **defects** (*"COLLAPSES `proxy.safeAreaInsets.top` to 0 … — a defect"*; *"the failed round-5
  approach"*). Signed *"(Architect-ratified T-0397, 2026-07-19 …)"*.
- **Row 7** = `:614` — likewise a parity-table cell. Rejected form is a **defect**
  (*"strict backend `DateOnly` binding 400s — the T-0370 profile-save bug … a `format: date` field
  ridden as plain `Date` is a defect"*).
- **Negative control** = `:559-561` — *"memory-only removes the … question rather than answering it, **at
  the cost of** one small refetch per cold start."* (T-0448, `owner: android`, harvested inline.)

| # | Candidate trigger | Fires 6? | Fires 7? | Fires `:559-561`? | Verdict |
|---|---|---|---|---|---|
| **T-α** | *the rejected form is correct on its own terms and rejected for a reason outside the entry's subject* | **no** — both rejects are broken | no | no | **dead** — misses both prizes |
| **T-β** | *the entry changes a build/tooling configuration rather than a call-site idiom* | **no** | yes (`useCustomDateWithoutTime` in the generator configs) | no | **dead** — misses row 6 |
| **T-γ** | *the entry records ≥2 failed attempts of its own (a fix-round history)* | yes (*"failed round-5"*, *"fix-round 6"*, *"fix-round 8"*) | marginal (one: the T-0370 bug) | no | **dead** — measures the author's attempts, not the codebase's cost, and is trivially laundered by deleting the history. That is Alternative D's failure mode, closed by accepted ADR-0033 |
| **T-δ** | *the entry pins a **closed, enumerated** set of ≥2 shipped call sites that must stay in lockstep* | yes (3 named) | yes (*"Both app configs carry the flag"*) | no (one) | **dead** — over-fires on row 10 (*"the customer profile delete-row + the delete-account confirm both consume it"*), which D1 **and** history send inline |
| **T-ε** | *the chosen form deviates from the other platform's shipped form on the same surface* | yes (*"owner-directed edge-to-edge deviation from Android's breathing-room treatment"*) | **no** — parity of the wire form is preserved; only the mechanism differs | no | **dead** — misses row 7, and duplicates **ADR-0018**, which already owns parity deviations |
| **T-ζ** | *the form's correctness depends on an environment CI never exercises (on-simulator layout, generator output)* | yes | yes | marginal | **dead** — over-fires nowhere useful and **under**-fires on row 2 (T-0451, pinned by `FixedWhiteContrastTests`, which D1 and history both route to the Architect). It is also ADR-0032's mechanizability question wearing a hat |
| **T-η** | *the edit adds a cell **stating an iOS obligation** to the Android→iOS parity table* | **yes** | **yes** | **no** | **hits the stated target — and is still dead.** (i) It is a **location** trigger, i.e. Alternative D with extra steps: move the same sentence into a blockquote and it stops firing, constraint unchanged. (ii) The table runs `:578`–`:715+` and most of its cells state an iOS form (`:602`, `:616`, `:617`, `:634`, `:636`…), so it routes most of the largest structure in the file to the Architect — C5's reductio, localized. (iii) It is not F4's question at all; it is a **parity** trigger, and ADR-0018 already owns parity |

**Result: D3(b) holds.** *"The entry states a cost"* and every alternative I could construct either
misses one of the two prizes, over-fires on a row both D1 and history send inline, or reduces to the
wording trigger accepted ADR-0033 already closed. **I could not beat D3, and I am saying so rather than
manufacturing one.**

**Two things about D3 I do dispute, neither of which rescues F4:**

1. **Ground (a) is an inference from a censored sample.** *"`T-0397-…md:70` shows the **Architect**
   asking the question **after** routing … it does not establish a ground on which a developer routes."*
   The only place the question can be *observed* being asked is inside an Architect ruling, because
   Architect rulings are where routed decisions get written down at all. You cannot infer "a developer
   cannot ask X" from "the only surviving record of X is in Architect files". The conclusion may still
   be right — (b) is what carries it — but (a) proves nothing and should not be listed as evidence.
2. **Rows 6 and 7 are the wrong prize** (CH-D): they are one architect sitting, both ratified
   substantively unchanged, and neither is a case where anything went wrong. **The prize the draft
   should be chasing is CH-A** — a live divergence in which a `owner: qa` ticket deleted a named
   canonical form from the catalog, deleted a shipped file on one platform, and rewrote a committed
   test suite, inline. A fourth test does not touch it; **the accepted floor already routes it** and
   nothing ran.

---

## What survived — attacked and not broken

Named, not omitted (`deliberation.md`: *"a challenger that finds nothing says so explicitly and names
what they checked"*).

1. **D2 — the reviewer's symmetric burden — holds, and it is the best thing in the draft.** I looked
   for a way to make *"quote the sentence **and** name the artifact"* cost more than it buys and could
   not. It is M2 completed on the other side, and it converts a plausible-but-adjacent assertion into
   something an author can answer. **One rider, from CH-A:** the search it obliges must be run against
   the catalog **as it stood before the edit**, not after — the draft's own corpus is the proof that the
   post-edit tree hides the highest-signal case.
2. **Alternative D's rejection (the artifact need not exist) is right.** I tried to argue the artifact
   must be in the tree, and the draft's reasoning survives: row 8's artifact
   (`CancellationFeePreview.swift`) was converted **in the same change**, so an exists-in-tree
   requirement would make test 2 a strict subset of test 1 and miss the very case I am using to attack
   the draft.
3. **D4 / Block C′ is right on the facts, and I verified each one independently.**
   `conventions.md:120-130` are items 1–3; `:122-124` is bullet 1 and scopes the inline lane to *"a
   small clarification/addition to an **existing** rule"*; `:125-127` is the disjunction whose first
   limb the floor reverses; `:128-130` is the supersession step and is consistent with test 1;
   `:132-134` is the "earns its place" bar. **Replace, not append, is correct**, and the third finding
   (ADR-0033 D1 test 4 mis-cites itself as *"unchanged from step 2, first bullet"*) is real. My only
   objection is CH-G, which is a drafting nit inside the replacement.
4. **The erratum-lane ruling in D4 is right.** *"Refines … does not reverse"* is an **argued** claim
   (ADR-0033 §Consequences argues it), and `adr/README.md:16-26`'s lane is for transcription errors. A
   dated appended section is the correct vehicle. I pressed this, as the draft invited, and concede it.
5. **The rejection of the topic-level reading (Alternative C) still fails on re-test.** Row 2 (T-0451)
   escapes as *"theme-invariant surfaces"* under any lexical reading, and its governing sentence really
   does predate it by five weeks (`c1009c63`, 2026-06-25). Not re-litigated.

---

## Findings filed for the PM (not part of this round's verdict)

| # | Finding | Why it is not in the round |
|---|---|---|
| **G1** | **T-0527's catalog harvest deleted a named canonical form from `patterns-mobile.md` inline, under `owner: qa`, with no Architect** (`ab077504`, `@@ -1272,9 +1315,9 @@`). Under the **accepted** floor this fired test 2's decidable disjunct. It also deleted `CancellationFeePreview.swift` and rewrote `CancellationFeePreviewTests`. **Recorded, not re-opened** — T-0274/T-0473 precedent; the substance is right, only the routing was not taken. It is the sharpest available evidence for **L2** (ADR-0033 binds nothing until FT-11 lands) | a mis-routed edit is recorded, not re-opened; and it post-dates ADR-0033's acceptance by a day, which is the point |
| **G2** | **`patterns-mobile.md:562-565` (Android, T-0448) is a stale summary of its own shipped code.** It states the guarded retry and omits the release that `ProfileViewModel.kt:178-181` implements and `ProfileViewModelTest.kt:642` pins. `4f81dce7` corrected the **iOS** paragraph (`:319-329`) and left the Android one standing — F5's disease (two forms on one page), current | a one-sentence catalog fix in the `patterns-mobile.md` lane; this round writes no catalog entry |
| **G3** | **`## Shared UI & theme` (`:247-455`) hosts four iOS entries under an Android-worded preamble** (`:249-253`). Whatever the panel decides about D1, this is a live scoping ambiguity in the file the routing test is applied to most, and CH-C turns on it. A heading or a one-clause scope statement retires it | structural catalog edit, `patterns-mobile.md` lane |
| **G4** | **N1 / N-E should be re-scoped** to the *"every surface that draws the disc"* clause; the `Android onAvatarLoadSucceeded` citation is descriptive-and-verifiable (CH-F) and needs a file:line, i.e. Block B's repair | correction to a filed follow-up, not a decision |

---

## What I could not verify (Gate 0.5 leg 3)

1. **No `Bash`.** I read a coordinator-generated corpus, not `git` directly. Commit→date attributions
   are read off that corpus's diff blocks; I did not re-derive them.
2. **Row 5's chronology is unestablished.** I did not find when `patterns-mobile.md:1241`'s `.medium`
   grant was introduced relative to the T-0397 withdrawal at `:996-1001`. It is the one row of the ten
   whose date relation I left open.
3. **I did not measure the rate** (attack surface 4). The corpus holds **41** commits touching
   `patterns-mobile.md`; I classified the **August window by inspection only** and did not run all 41.
   What CH-A establishes is a **direction**, not a number: the draft's negatives are one-sided
   under-estimates, so the true firing rate of test 2 under D1 is **at least** what the table reports
   and probably higher. **N-F is still the measurement, and it must be run over the *pre-edit* catalog
   at each hunk, which is what the draft's method got wrong.**
4. **Row 10's counter-reading is mine.** Reviewer B's reading of `:249-253` as the section preamble is
   the one the file's structure and `4d8b3978`'s insertion point support, but the draft's narrower
   reading is not absurd. **That two competent readings exist on today's text is the finding**, not
   which one wins — which is exactly the standard §Ruling 1 used to establish L1 against ADR-0033.
5. **I did not open every iOS/Android surface that draws an avatar disc**, so CH-B's claim is that
   Android shipped and tested the *release*, not that all Android surfaces were wired on 2026-08-01.
   The narrower claim is enough: the artifact D1 names is excluded by a named test in the governing
   sentence's own ticket.
6. **Line numbers are this worktree's**, and `patterns-mobile.md` is a live shared-file lane. Every
   load-bearing citation quotes its text.

---

## Summary for the lead

| # | Claim | Ask |
|---|---|---|
| **CH-A** | **Retro row 8 is wrong.** `ab077504`'s third hunk deletes the governing sentence (`CancellationFeePreview` + the tier ladder) that the draft's grep of the *post-edit* tree could not see. Row 8 is an **Architect ⇄ inline divergence**, not a match, and it fires the accepted floor's decidable disjunct without needing D1. Four of ten rows rest on negatives produced by that same one-sidedly-erring procedure | **BLOCKING** — §Retro-validation's score and §Ruling 1's evidentiary claim do not stand as printed; the method must search the **pre-edit** catalog |
| **CH-B** | **Row 9 — D1's only claimed catch — is a false positive.** The artifact it names is excluded by `ProfileViewModel.kt:178-181` + `ProfileViewModelTest.kt:642`, shipped by the governing sentence's **own ticket** (`T-0448-…md:264, :332-337`). Generalized: D1 reads `S`'s verdict off `S`'s literal text in isolation, so it fires on every precision-adding refinement — C5's reductio through the artifact door. D2 offers the author only "compose or concede"; the right answer has no slot | **BLOCKING** — either the reductio is answered or D1 does not close the inline lane it claims to preserve |
| **CH-C** | **"Reach" is the subject question.** D1 rejects Alternative A for relocating indeterminacy to subject granularity, then defines reach as *"inside the scope `S` prescribes for"*. Demonstrated on the draft's **own row 10**: `ProfileHubContent.swift:298` (`LogoutRow`) is named by the entry itself and ruled differently by `:249-253` read as the section preamble it is positioned as | **BLOCKING** — D1's central claim of superiority over Alternative A fails inside its own table |
| **CH-D** | The ten cases are ~six routing events. Rows 5/6/7 trace to one architect sitting (**2026-07-19**, signatures at `:602`, `:635`, `:1000`); rows 3/9 and row 9's governing sentence are **one commit** (`0e4ede1b`). Rows 1, 2, 4 chronologies **confirmed sound** | state the clustering; stop calling rows 6 and 7 two divergences |
| **CH-E** | Row 3 is not historical (`:517-525` post-dates by a day) **and** is self-answering: `2012b014`'s paragraph cites *"(the T-0473 rule)"* and names `OrderDetailCardStringsTest` in the same sentence. So it does not discriminate D1 from Alternative A | the draft's headline consequence is carried by a non-discriminating case |
| **CH-F** | N1/N-E mis-characterizes a **descriptive** claim as prescriptive; re-scope to the *"every surface"* clause | correction |
| **CH-G** | Block C′ step 3 is unguarded — a routed edit still reaches *"Write it into the catalog"* | drafting nit inside the replacement |
| **D3** | **SUSTAINED.** Six triggers built and killed (T-α…T-η), each with its failure named. T-η hits the stated target and is still a location/wording trigger closed by accepted ADR-0033. Ground **(a)** is a censored-sample inference and should be dropped as evidence; ground **(b)** carries the limb | **no ask** — record the six as tried, so the next round does not re-derive them |
