# Sprint 15 — the owner's 15-remark batch + four completed investigations

**Baseline:** `master` at **`0e4ede1b`** (*"docs(backlog): record the owner's label ruling, unblock the
avatar chain"* — the T-0450 merges). Working tree carries the owner's uncommitted iOS files
(`Package.resolved`, `Info.plist` ×3). **The live Stripe key is in them; no agent opens those files.**
**Created:** 2026-08-02 — planning pass.
**Input:** the owner's **15 remarks** + **4 completed investigations** (Cleansia Plus, Azure cost,
cold start/deploy, partner onboarding).
**Output: 35 new tickets, `T-0476`…`T-0510`**, plus 14 sprint-14 carry-overs that this plan sequences.

> ## 🟥 READ THIS FIRST — three of the owner's remarks are wrong about *where* the defect is
>
> The PM ground-truthed all 15 against the code before ticketing. **Three would have sent a developer
> to the wrong file, and one would have sent them to fix something that already works.**
>
> | Remark | As reported | What the code says |
> |---|---|---|
> | **#4** *"iOS order detail has no progress bars, no mascot"* | customer app implied | **FALSE for customer.** `LiveProgressHero.swift` already has the mascot overlay (`:88-94`), a live `ProgressView` (`:73`) and a `StepIndicator` (`:121`). **TRUE for partner** — no mascot puck at all. → **T-0482** is partner-scoped |
> | **#8** *"hide the order panel to reveal the map, both apps"* | customer order detail implied | **There is no map on the customer order detail on either platform.** The map is on **partner**. And it is **not symmetric**: iOS already has a `.mapFocus` anchor at 30%; Android's sheet cannot go below 75%. → **T-0489** |
> | **#9** *"no back button on the invoice detail page"* | a missing button | **The button exists** (`InvoiceDetailScreen.kt:117-122`) and is correctly wired (`PartnerNavHost.kt:283`). It has **no status-bar inset** under `enableEdgeToEdge()`, so it renders **under the clock**. A developer told "add a back button" would have added a second one. → **T-0490** |
> | **#2** *"no translations for the recurring setup, both apps"* | missing translations | **All 62 Android `recurring_*` keys exist in all 5 locales, genuinely translated. All 46 iOS keys carry all 5.** The Android defect is that **catalog names bypass the `translations` map** (`CreateRecurringScreen.kt:977/980/998` use `.name.orEmpty()`) — iOS does this **correctly** and is the reference. → **T-0477**; the iOS half (**T-0478**) is filed as *reproduce-first* because the PM could not find it |

---

## 1. The 35 new tickets

### 1.1 From the owner's 15 remarks — `T-0476`…`T-0490`

| # | Ticket | Size | Layers | Panel? | Note |
|---|---|---|---|---|---|
| 1 | **T-0476** Android profile hero taller | S | android | short analyst | **Moves away from the parity T-0442 just built.** Couples to the `:172` overlap spacer |
| 2a | **T-0477** Android recurring wizard catalog names unlocalized | S | android | no-decision | **Confirmed**, 3 call sites |
| 2b | **T-0478** iOS recurring i18n — reproduce first | S | ios | no | **PM could not reproduce.** "Not reproducible" closes it successfully |
| 3a | **T-0479** Android bottom-nav labels wrap | S | android | no-decision | **Confirmed.** `MainShell.kt:445-449` has no `maxLines`/`overflow`; partner has the same duplicated `NavSlot` |
| 3b | **T-0480** iOS tab-bar overflow mechanism | S | architect, ios | **architect** | **The Android fix does not port** — `.tabItem` is UIKit-rendered and ignores `.lineLimit`. May close as "already correct" |
| 4b | **T-0481** 🔍 **DISCOVERY** — order-detail parity audit ×4 screens | M | analyst | no | **The owner's explicit "audit both apps" ask, filed as its own ticket** |
| 4a | **T-0482** iOS partner mascot + progress parity | M | ios | no | Confirmed gap. **Sequenced behind T-0489** (shared moving sheet edge) |
| 4c | **T-0483** entrance instructions behind a reveal | M | analyst, architect, ios, android | **analyst** | 🔒 `security_touching`. **Partner-scoped by default**; AC1 rules the scope |
| 5 | **T-0484** 🎨 **DESIGN-FIRST** customer order-detail HTML concept | M | analyst, architect | **analyst** | **No implementation ticket behind it. Owner decision point.** |
| 6 | **T-0485** 📋 **STORY** — edit a recurring booking | S | analyst | **analyst** | Blocks T-0486/T-0487 |
| 6 | **T-0486** Android recurring edit path | M | android | no | Android has `update()` at 3 layers with **zero callers** |
| 6 | **T-0487** iOS recurring edit path | M | ios | no | iOS has **nothing** below the generated client |
| 7 | **T-0488** 🎨 **DESIGN-FIRST** Live Activity HTML concept | M | analyst, architect | **analyst** | **No implementation ticket behind it. Owner decision point.** |
| 8 | **T-0489** partner order detail — reveal the full map | S | architect, android, ios | **architect** | Android needs the anchor; **iOS needs discoverability, not capability** |
| 9 | **T-0490** Android partner invoice header under the status bar | S | android | no-decision | **Cheapest ticket in the batch.** AC4's inset sweep is the valuable half |

### 1.2 Cleansia Plus — `T-0491`…`T-0498`

| Ticket | Size | Dep | Note |
|---|---|---|---|
| **T-0491** 📋 **STORY** — what Plus actually promises | M | — | Blocks 4. Produces the **9 owner questions** as one block |
| **T-0492** discount worth 0 Kč, and a promo erases it | M | T-0491 | 🔍 **PM-verified**: `OrderFactory.cs:39` cap = `0.12m`; `:202-210` sets `MembershipAmount: 0m`. **Two findings, one 35-line method, one ruling → one ticket** |
| **T-0493** express upgrade has no enforcing code | M | T-0491 | **RELAYED.** "Express" has 3 meanings spanning `S`→new product; AC2 forces a re-file |
| **T-0494** 🔒 **recurring gate is client-side only** | S | **none** | 🔍 **PM-verified**: `CreateRecurringBooking.cs`'s validator has **no membership rule of any kind**. **Deliberately no dependency on T-0491** |
| **T-0495** favourite cleaner — no matching, no gate | M | T-0491 | **AC2 explicitly permits "withdraw the claim"** as the recommendation |
| **T-0496** express surcharge currency bug | S | **none** | **The only Plus ticket dispatchable today.** Wrong money is wrong under every ruling |
| **T-0497** trial re-offered to resubscribers | S | 🚫 **BLOCKED** | **Two candidate defects with OPPOSITE fixes.** `Q-PLUS-01`. AC1 carved out as dispatchable now |
| **T-0498** Plus card parity (iOS view + Android's 2 bugs) | S | T-0491 | **One ticket because porting Android's card copies hardcoded English + an unconditional pill** |

### 1.3 Azure cost + observability + cold start — `T-0499`…`T-0503`

| Ticket | Size | Note |
|---|---|---|
| **T-0499** `host.json` polling + sampling | S | 🔍 **PM-verified all five values.** 5s poll vs a **60s default**; sampling on with `Request` **excluded from it**. **Highest euro-per-line in the backlog** |
| **T-0500** 🔴 **the only live environment has no error tracking** | S | 🔍 **PM-verified both halves.** **Corrects the investigation:** Sentry is not "silently" disabled — it is documented at `AZURE-DEV-RUNBOOK.md:239`. **The conclusion gets worse:** prod never deployed, so the "prod" that was to have Sentry doesn't exist |
| **T-0501** docs claim telemetry that does not exist | S | 🔍 **PM-verified**: zero `ApplicationInsights`/`AddAzureMonitor` refs in all five APIs |
| **T-0502** dev cold start — Always On, warm loop, double restart | M | 🔍 **4 of 5 PM-verified.** **AC5 records a refusal: slots on dev = +€35–45/mo for HALF the RAM** |
| **T-0503** boot cost — blocking DB call in DI, cold EF model | M | **RELAYED.** *DI composes the graph; it must not do I/O* |

### 1.4 Partner onboarding — `T-0504`…`T-0510`

| Ticket | Size | Dep | Note |
|---|---|---|---|
| **T-0504** 📋 **STORY** — capture, persistence, legal minimum | M | — | Blocks 5. **Decisions 4/5/6 explicitly barred from being defaulted** |
| **T-0505** email validated → discarded → **success toast** | M | T-0504 | 🔒 **The toast is the worst half** — it stopped anyone noticing. If email is an identity credential this is an **auth epic** and AC2 forces a re-file |
| **T-0506** language unreachable / unpersisted / absent from the API | M | T-0504 | **4 failures on one field.** AC6 sweeps for other **`EmptyView()` routes** — invisible to compiler, tests and lint |
| **T-0507** 🔴 **consent required, never persisted, never asked on mobile** | M | T-0504 | 🔒 ⚠️ `ef-migration`. **Two cohorts — "asked, not recorded" and "never asked" — and the platform cannot tell which is which. Already-given consents cannot be reconstructed** |
| **T-0508** 🔴 invoice is not a valid CZ/SK supplier document | M | 🚫 **BLOCKED** | ⚠️ `ef-migration`. **`Q-PAYOUT-01` + `Q-PAYOUT-02`, both legal.** AC1 (render the current doc) dispatchable now |
| **T-0509** IBAN has no downstream consumer | S | T-0508 | 🔒 The **only** one of the four "collected and unused" fields that is **retained** → a data-**minimisation** problem. **"Delete it" is a legitimate outcome** |
| **T-0510** two onboarding implementations — delete the duplicate | M | T-0504 | **The root cause, filed LAST**, with the reasoning written down. AC8 forbids fixing any defect in the diff |

---

## 2. PR batches, in order

**The rule that drives the grouping:** two tickets share a PR when they share a *reason* a reviewer
would want to see them together, and they share a **lane** when they touch the same file. A shared
lane forces serialization whether or not they share a PR.

### 🟦 PR-A — the toolchain. **Everything else waits behind this.**
> **`T-0475` → `T-0474`** *(sprint-14, already filed, sequencing already ruled)*

**Why first, and it is not close.** **Seven** sprint-15 iOS tickets open with *"run
`generate-api-clients.sh` + `xcodegen generate`."* **Today that instruction wipes the owner's Stripe
key**, because `xcodegen generate` rewrites `Info.plist` from `project.yml`. Prescribing it before
T-0475 lands converts an occasional loss into one on **every single pull**.

**The regen trap has cost a broken build three times** and has cost one reviewer a false conclusion
(it read a stale client, declared T-0440 owner-regen-blocked, and **contradicted that ticket's own
warning at its lines 34-39**). The owner asked for this near the front. It is the front.

**⚠️ Order is not interchangeable:** `T-0475` (xcconfig) → owner drops in their values → `T-0474`'s
xcodegen leg is safe. T-0474's `generate-api-clients` leg is safe today and may ship first.

---

### 🟦 PR-B — "screens you cannot get out of". Two confirmed Android defects, zero panels.
> **`T-0490`** (invoice header under the status bar) + **`T-0479`** (nav labels wrap)

Both mechanical, both PM-verified, both no-decision, **disjoint files**, no dependencies. Both are
"broken in a way you see in two seconds". T-0490's AC4 sweep — **`statusBarsPadding` appears in
**zero** partner-app files** — is likely worth more than the fix.

**Runs alongside:** **`T-0480`** (iOS tab bar) as its own small PR, because AC1 may close it as
"already correct" and the Android remedy provably does not port.

---

### 🟦 PR-C — observability. **This changes what "green on DEV" means for every PR after it.**
> **`T-0500`** → **`T-0499`** → **`T-0501`**

One PR because they are one question asked three ways: *what can we see?* T-0500 decides whether DEV
gets error tracking; **T-0499 lowers Functions log levels to save money and must know the visibility
floor first** — cutting observability on a platform that has none is a worse trade than it looks;
T-0501 documents the final state once instead of twice.

**⚠️ If T-0500 rules "turn Sentry on", `T-0457` (sprint-14, `ready`, P1) should land first** — it is
the ticket that stops `GET /api/User/GetCurrent` writing every caller's email, name, phone and birth
date into Information-level logs on all five hosts. An error tracker that ships log context would
carry that to a third party.

---

### 🟦 PR-D — cold start and deploy. Everything in it is **€0**.
> **`T-0502`** (Always On + warm loop + stop the double restart) + **`T-0503`** (DI I/O + EF warm)

One investigation, one user-visible outcome — *DEV stops taking 20 seconds to answer* — two layers.
They **compose**: T-0502 keeps the host up, T-0503 makes it come up faster. Neither substitutes.

**Sequenced after PR-C** only because both touch `main.bicep` (different hunks — own-hunks-only rule).
**AC5 of T-0502 records a refusal in the repo: slots on dev cost +€35–45/month for half the RAM.**
That finding is worth more than the fix, because "add slots" is the intuitive answer and it is wrong.

---

### 🟦 PR-E — Plus, the half that needs no panel.
> **`T-0494`** (server-side recurring gate — 🔒 SECURITY) + **`T-0496`** (express surcharge currency)

**The only two Plus items with no dependency on the T-0491 panel**, and both are true under every
possible product ruling: a paid capability obtainable without paying, and a wrong amount of money.
Batched because a reviewer looking at "Plus is not enforced" wants both in view.

---

### 🟦 PR-F1 / F2 — Plus, after the panel.
> **F1: `T-0492`** (discount math) + **`T-0493`** (express enforcement) — both backend, both money
> **F2: `T-0498`** (the card, on both clients)

F1 is one reviewer's context: what a Plus subscriber is entitled to and what they are charged. **F2
comes after F1 deliberately** — the card advertises five perks, three currently unenforced, and
shipping a *better-looking* card for them makes the misrepresentation more prominent, not less.

**`T-0495`** (favourite cleaner) produces a **specification, not a PR**. Its recommendation may be
*"withdraw the claim"*, which would then be a copy change across three clients × five locales.

---

### 🟦 PR-G1 / G2 — the recurring feature, made real.
> **G1: `T-0477`** (Android catalog names) + **`T-0478`** (iOS reproduce)
> **G2: `T-0486`** (Android edit) + **`T-0487`** (iOS edit) — behind the **`T-0485`** story

G1 is both i18n halves of remark #2 in one PR — and it **clears the `CreateRecurringScreen.kt` /
`Features/Recurring/**` lanes** that G2 needs. **G1 → G2 is a lane order, not a preference.**

---

### 🟦 PR-H1 / H2 / H3 — partner order detail. **Strictly serialized: same two files per platform.**
> **H1: `T-0489`** — reveal the map (Android gains the anchor; iOS gains the affordance)
> **H2: `T-0482`** — the iOS mascot puck
> **H3: `T-0483`** — entrance instructions behind a reveal 🔒

**Getting this order wrong guarantees a rebase.** T-0482 anchors a puck to the sheet's **moving top
edge**; building it against a sheet whose anchor set is about to change is wasted work. H3 last
because it needs a panel and is the only `security_touching` one.

---

### 🟦 PR-I1…I4 — the profile-hero pile-up. **The most contested file in the backlog.**
> **I1: `T-0450`** (`ready` today) → **I2: `T-0447` + `T-0448` + `T-0449`** (the avatar trio) →
> **I3: `T-0476` + `T-0453`** (taller + edge-to-edge) → **I4: `T-0472`** (Poppins)

`ProfileTab.kt` has **five** claimants and `ProfileTab.swift` has three. **This lane is the reason
sprint-14 split T-0450.** I3 pairs T-0476 and T-0453 because both are hero *geometry* and one
developer should do both against one final layout rather than restructure twice. I4 last, per
sprint-14 §9.3.1.

**⚠️ `T-0447` carries an open `blocking: yes` question — `Q-PROFILE-01`.** It can ship its UI, but its
round-trip evidence cannot be produced until a backend decision lands.

---

### 🟦 PR-J1…J4 — partner onboarding, behind the **`T-0504`** story.
> **J1: `T-0505`** (email) + **`T-0506`** (language) — the two "collected and dropped" fields
> **J2: `T-0507`** (consent) — ⚠️ its own PR: `ef-migration`, owner-gated
> **J3: `T-0508`** + **`T-0509`** (invoice + IBAN) — 🚫 blocked on legal
> **J4: `T-0510`** (delete the duplicate) — last, and AC8 forbids mixing any defect into it

**Why the root cause ships last:** a consolidation landing before the field rulings gets redone
(J1/J2/J3 each add fields to whichever command survives), and a consolidation bundled with five
defect fixes cannot be reviewed. **T-0504 AC6 owns that trade-off and may overrule this order** — if
it does, every affected `depends_on` is updated to match rather than left to drift.

---

### 🟦 PR-K — the two design concepts. **Docs only. Then a hard stop.**
> **`T-0484`** (customer order detail) and **`T-0488`** (Live Activities)

HTML under `agents/backlog/attachments/`. `git diff --stat -- src/` **must be empty** for both.
**No implementation ticket exists behind either and none will be written until the owner picks a
concept.** Each concept carries a **per-platform S/M/L estimate**, because the owner is choosing a
budget as much as a picture.

---

### 🟦 PR-L — the parity audit. **Read-only, runs in parallel with everything.**
> **`T-0481`**

Four analyst instances (one per side of each screen) + a lead. Produces a ranked shortlist of **at
most 8** ticket candidates. **It files no tickets** — the PM does.

---

### Suggested wave ordering

```
WAVE 0   PR-A                                       ← nothing else starts iOS work first
WAVE 1   PR-B · PR-C · PR-E · PR-I1
         panels start: T-0491 · T-0504 · T-0485 · T-0484 · T-0488 · T-0480
         discovery starts: T-0481
WAVE 2   PR-D · PR-G1 · PR-H1 · PR-I2 · PR-L lands
WAVE 3   PR-F1 · PR-G2 · PR-H2 · PR-I3 · PR-J1 · PR-K lands → OWNER DECISION POINT
WAVE 4   PR-F2 · PR-H3 · PR-I4 · PR-J2 · PR-J4
ANYTIME  T-0497 (on Q-PLUS-01) · PR-J3 (on Q-PAYOUT-01/02)
```

---

## 3. Shared-file lanes — validated before dispatch

| Lane | Order | Why |
|---|---|---|
| **`ProfileTab.kt`** (Android customer) | T-0450 → T-0448 → **T-0476** → T-0453 → T-0472 | Five claimants on one 60-line composable |
| **`ProfileTab.swift`** (iOS customer) | T-0450 → T-0449 | |
| **`Localizable.xcstrings`** (customer) | T-0450 → T-0449 → **T-0487** → **T-0498** | Serialized i18n bundle **+ in the owner's uncommitted set** |
| **`values*/strings.xml`** ×5 (customer) | T-0450 → **T-0498** | T-0477/T-0479 add **zero** keys by AC |
| **`CreateRecurringScreen.kt`** | **T-0477** → **T-0486** | T-0477 is `S` and mechanical; it goes first |
| **`Features/Recurring/**`** (iOS) | **T-0478** → **T-0487** | T-0478 may close with an empty diff |
| **partner `OrderDetailScreen.kt`** | **T-0489** → **T-0483** | |
| **partner iOS `OrderDetailView.swift` + `SnapSheet.swift`** | **T-0489** → **T-0482** → **T-0483** | T-0482 anchors to a **moving** sheet edge |
| **`main.bicep`** | **T-0500** (one comment) → **T-0502** (`alwaysOn`) | Different hunks; own-hunks-only rule 2 applies |
| **onboarding command(s)** | T-0505/0506 → T-0507 → T-0508/0509 → **T-0510** | T-0510 AC8 forbids mixing |
| **`OrderFactory.cs`** | **T-0492** sole writer | T-0496 is a different code path |
| **`agents/knowledge/patterns-*.md`** | per-file, serialized | Sprint-14 PM ruling, effective now |

**Uncontended:** `MainShell.kt`, `FloatingIslandBottomBar.kt`, `InvoiceDetailScreen.kt`, `host.json`,
`Cleansia.Config` (check before T-0503), `CreateRecurringBooking.cs`.

---

## 4. The consolidated owner-decision list — ranked by what it unblocks

> Everything on this page that needs you, in one place. **Ranked by tickets unblocked per minute of
> your time**, not by importance.

| # | What | Blocks | Cost to you |
|---|---|---|---|
| **1** | **Supply the xcconfig values** once `T-0475` lands (Stripe key + `DEVELOPMENT_TEAM`, **both** app dirs) | **Every iOS ticket in the sprint — 7 of them** | 2 min |
| **2** | **`Q-PLUS-01` — the Stripe trial check.** In **test mode**, on a customer who has already trialled: does a second subscription land in `trialing` or `active`? | `T-0497`. **The two candidate defects have opposite fixes** | **1 min** |
| **3** | **`Q-OBS-01` — does DEV get error tracking?** And: **is `secrets.SENTRY_DSN` populated in GitHub at all?** | `T-0500`, `T-0501`; **changes what "it works on DEV" means for every PR** | 5 min + a decision |
| **4** | **`Q-PAYOUT-01` + `Q-PAYOUT-02`** — what a CZ/SK supplier invoice must contain, and **is a cleaner an employee or an OSVČ?** | `T-0508` → `T-0509`. **Your cleaners cannot be legally paid today** | an accountant call |
| **5** | **`Q-AZURE-01` — two cost queries** (cost by resource+meter; Log Analytics ingestion by table) | Sizes `T-0499`'s win; does **not** gate the fix | 5 min in the portal |
| **6** | **Approve or reject an HTML concept — `T-0484`** (customer order detail) | **No implementation ticket exists until you do** | when it arrives |
| **7** | **Approve or reject an HTML concept — `T-0488`** (Live Activities) | same | when it arrives |
| **8** | **The 9 Cleansia Plus product questions** — produced by the `T-0491` panel as one block | `T-0492`, `T-0493`, `T-0495`, `T-0498` | after the panel |
| **9** | **The 7 partner-onboarding decisions** — produced by the `T-0504` panel as one block | `T-0505`…`T-0510` | after the panel |
| **10** | **Carried from sprint-14:** `Q-PROFILE-01` (`blocking: yes`) · two `CLAUDE.md` lines (`T-0462` AC5b owns the text) · `Q-BRAND-01` · `Q-CI-01` · `Q-DESIGN-01` | `T-0447`'s round-trip evidence | — |

### The two questions nobody can guess for you, stated plainly

**Which bank schemes, in which countries.** IBAN is European. `CLAUDE.md` keeps `Address.State` for
*"US/CA when we launch there"*, so non-IBAN markets are on your roadmap. This decides whether the
stored field is *an IBAN* or *bank details with a scheme discriminator* — a column shape, not a
preference.

**What a CZ/SK invoice must legally contain, and who issues it.** The plausible list is easy to write
and that is exactly the danger. And the second half is worse than the first: **employee vs OSVČ
decides which *document* this is.** If a cleaner is an employee, the artifact is a **payslip**, not an
invoice — different content, different law, and `T-0508` is not an `M`. The entity is currently called
`EmployeeInvoice`, which is the two models' names collided into one.

### One thing I deliberately did NOT put on this list

**The nine Plus questions and the seven onboarding decisions are not pre-filed as owner questions.**
They come out of the `T-0491` and `T-0504` panels, each of which is required to file them as **one
consolidated block with options and a stated default**. Handing you sixteen raw questions now would be
handing you the panel's work.

---

## 5. Urgency split — an honest one

### 🔴 A — legal / consumer-protection exposure, **regardless of any demo**

These do not get better by waiting, and two of them get **worse**.

| Ticket | Why it is here |
|---|---|
| **T-0507** consent | **The un-provable cohort grows every day and cannot be repaired retroactively.** You cannot generate a consent record for a consent never captured. GDPR Art. 7(1) requires you to *demonstrate* consent; a checkbox that gates a form and writes nothing demonstrates nothing. And a second cohort (all mobile onboardings) was **never asked** — and the platform **cannot tell which partner is in which group** |
| **T-0494** recurring gate | A **paid** capability obtainable **without paying**. Exploit: subscribe, capture the request, cancel, replay. Live on DEV |
| **T-0492 / T-0493 / T-0495** | **Three of five advertised perks are unenforced and a fourth is worth 0 Kč to your best customers.** People are paying for this |
| **T-0497** trial | Either a **false advertised price** or an **unlimited free-trial loop**. One of the two is live right now |
| **T-0508** invoice | **Cleaners cannot be legally paid** against the document the platform generates |
| **T-0509** IBAN | Financial account identifiers held for **every** cleaner with **no consumer** — data-minimisation with no purpose to justify it |
| **T-0505** email | A **success toast on a discarded write**, and **no email-change path anywhere including admin.** A cleaner who changes address is unreachable and unfixable except by a direct DB edit |

**Stated plainly, because it was asked for: the unenforced Plus promises and the unpersisted consent
are not post-demo polish.** Everything in this bucket is true of a live system with real users and
real money, and a demo neither creates nor cures any of it.

### 🟠 B — must land before a demo

| Ticket | Why |
|---|---|
| **T-0475 → T-0474** (PR-A) | **The build has broken three times on this.** Nothing iOS is safe until it lands |
| **T-0490**, **T-0479** (PR-B) | Visible in two seconds. A back arrow under the clock and a nav label on two lines are what a demo audience notices first |
| **T-0502**, **T-0503** (PR-D) | **Always On is off on dev.** A demo that starts on an idle B2 opens with a 20-second white screen. €0 to fix |
| **T-0450 → T-0447/0448/0449** (PR-I1/I2) | **The owner ruled the avatar is demo scope** (sprint-14 §0(b)). It is currently **one-third shipped** — read path only, no client can upload |
| **T-0489**, **T-0482** (PR-H1/H2) | The two partner-app remarks that are pure visual polish on the screen the owner is proudest of |
| **T-0500** (PR-C) | Arguable, and I will argue it: **demoing on an environment where failures are invisible means a failure mid-demo has no diagnosis.** `S`, and option (a) is a secret paste |

### 🟡 C — cost and hygiene

`T-0499` (€35–42/month, but it is money not risk) · `T-0501` (docs) · `T-0481` (discovery — high value,
no urgency) · `T-0476` · `T-0453` · `T-0472` · `T-0480` (may close as already-correct) · `T-0477` /
`T-0478` · `T-0486` / `T-0487` · `T-0483` · `T-0498` · `T-0510` · `T-0484` / `T-0488` (concepts).

**One inversion worth naming:** `T-0499` saves real money and sits in bucket C, while `T-0502` saves
none and sits in bucket B. That is deliberate — **cost is recoverable and a bad demo is not.**

---

## 6. What this pass deliberately did NOT do (Gate 0.5 leg 3, applied to the PM's own work)

- **No specialist agent was dispatched and no code was written.** Every edit is under `agents/`.
  Nothing committed, staged or pushed; no `git stash`; **`CLAUDE.md` untouched**;
  `src/cleansia_ios/**/Info.plist` and `**/project.yml` **never opened**.
- **No panel was convened.** Eight tickets go to `draft` **needing** one (T-0480, T-0483, T-0484,
  T-0485, T-0488, T-0489, T-0491, T-0504). That is DoR item 2, not a dependency — all eight are
  dispatchable today with the panel as step 1.
- **No build, suite, Gradle task or iOS build was run.** No numbers in this document are measurements
  the PM took of a running system.
- **No implementation ticket was written behind T-0484 or T-0488.** Deliberate, and per instruction.
  **Writing acceptance criteria for an unapproved design is how a redesign becomes three rewrites.**
- **ADR-0032's FT-5 still has no `T-*` id.** Carried from sprint-14 §9.4, still not filed. Named again
  so it is not lost a second time.
- **The `T-0481` audit's findings were NOT pre-guessed.** One gap found while grounding (the recurring
  wizard is a 3-step wizard on Android and a single-page form on iOS, **19 string keys apart**) is
  handed to it as a row rather than ticketed, because ticketing an audit's output before the audit is
  how you get the audit you already expected.

### What the PM DID verify first-hand, on `0e4ede1b`

`MainShell.kt:371-460` · `FloatingIslandBottomBar.kt:44-90` · all five `values*/strings.xml` nav
labels · `ProfileTab.kt:255-300` + `:141-143` + `:172` · `ProfileTab.swift:285-310` ·
`OrderDetailView.swift` + `OrderDetailContent.swift` + `LiveProgressHero.swift` (customer iOS, in
full) · `OrderDetailScreen.kt` + `LiveProgressHero.kt` (customer Android) · partner
`OrderDetailScreen.kt:225-340` + `:440-560` · partner `OrderDetailView.swift` (in full) ·
`CleansiaCore/Components/SnapSheet.swift:1-60` · `InvoiceDetailScreen.kt:60-140` +
`InvoicesListScreen.kt:96` + `PartnerNavHost.kt:278-292` + `MainActivity.kt:76` ·
`CreateRecurringScreen.kt` + `.swift` + both ViewModels + both repositories + both API layers ·
a five-locale key-count diff of `recurring_*` (**62 Android / 46 iOS, 19 Android-only, 3 iOS-only**) ·
`Localizable.xcstrings` locale coverage (**all 46 recurring keys carry all 5**) ·
`OrderFactory.cs:29-45` + `:160-215` · `CreateRecurringBooking.cs:27-70` ·
`src/Cleansia.Functions/host.json` (in full) · `Cleansia.ServiceDefaults/Extensions.cs:80-115` ·
ten `appsettings*.json` `Dsn` values · `deploy/bicep/main.bicep:163`, `:426-470`, `:571-572`, `:673` ·
`deploy/bicep/modules/appService.bicep:33-34`, `:106-110` · `AZURE-DEV-RUNBOOK.md:239`, `:520` ·
`deploy-azure.yml:433`, `:481` · greps establishing **zero** `ApplicationInsights`/`AddAzureMonitor`/
`UseAzureMonitor` in all five APIs, **zero** `statusBarsPadding` in the partner app, **zero** callers
of Android's recurring `update()`, and **zero** `update` anywhere in iOS's Recurring feature.

### What is RELAYED and labelled as such on the tickets

Every finding from the four investigations that the PM did **not** re-derive: T-0493, T-0495, T-0496,
T-0497 (Plus); the double-restart in T-0502; both findings in T-0503; and **all** of T-0504…T-0510
(partner onboarding — **the PM re-verified none of the onboarding findings**). Each of those tickets
carries an **AC1 that re-establishes the finding before anything is changed**, and several explicitly
permit *"it did not reproduce"* as a successful close.

**Sprint-14 §2.12 is why.** The PM stamped a relayed claim "PM-verified", was accurate about the file
and wrong about the repo, and filed a false blocker. Reading an artifact is not verifying a claim.
