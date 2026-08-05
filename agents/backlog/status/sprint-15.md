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

---
---

# ADDENDUM — the owner's four product answers (2026-08-02)

**Input:** the owner answered the four open product decisions and four housekeeping items.
**Output: 14 new tickets `T-0511`…`T-0524`, 4 rewritten (`T-0493`, `T-0495`, `T-0508`, `T-0509`),
2 closed `done` (`T-0474`, `T-0475`), 5 new owner questions, 1 downgraded.**
**Baseline: `master` at `dceed4f1`** — **not** the `0e4ede1b` this document was written against.

> ## 🟥 READ THIS FIRST — the backlog above is stale, and by more than a little
>
> **PR #189 (`2012b014`, 2026-08-02) merged 236 files / +10,728 lines and updated NOT ONE ticket
> file.** Its message names *"order detail redesign, Plus enforcement, onboarding, cost and cold
> start"* — i.e. it shipped work belonging to a large slice of `T-0476`…`T-0510`. **#186, #187 and
> #188 did the same for T-0479/T-0490, T-0475 and T-0480.** Every `draft`/`blocked` row above must be
> treated as **unverified** until someone reconciles it against the code.
>
> **A full reconciliation of sprint-15 against `dceed4f1` is NOT in this addendum and is the single
> highest-value backlog job outstanding.** What this pass did instead is narrower and deliberate: it
> re-verified, first-hand and post-#189, **only** the premises of the four decisions it was asked to
> ticket. **All four still hold.** The new tickets are not duplicating shipped work.
>
> | Re-verified at `dceed4f1` (post-#189) | Result |
> |---|---|
> | `MembershipPlan.AllowsExpressUpgrade` read by any pricing code | **still zero readers** |
> | `Order.PreferredEmployeeId` read by any dispatch code | **still zero readers**; `TakeOrder.cs` never mentions it |
> | `ValidateIban` | **still `NotEmpty()` + `Length(15,34)`** |
> | The payout invoice's parties | **still Cleansia-as-issuer / cleaner-as-"Billed To"** |
> | T-0494 (recurring gate) | **SHIPPED in #189** — `CreateRecurringBooking.cs:84-92` |

---

## A1. Answer 1 — express upgrade: **BUILD IT**

> Owner: *"You can upgrade."*

**This closes T-0493's product question and opens an engineering one.** Reading A (a price benefit) is
the ruling — but the advertised copy is *"one free same-day booking per month"*, which needs a **usage
counter that does not exist**. `MembershipPlan.cs:99-104` defers exactly this to a *"future membership
benefit usage tracker"*. So the perk is **pricing change + usage tracking + a monthly reset rule**, and
that is an `L` as one ticket. **Split before ready, per the standing rule.**

| Ticket | Size | Status | What it is |
|---|---|---|---|
| **T-0511** 📋 | M | **`ready`** | **ARCHITECT PANEL + ADR** — how a metered benefit is counted, consumed, reset and **reversed on cancellation** |
| **T-0512** | S | `draft` | the entity + ⚠️ `ef-migration` — **separate so a migration is not in the same PR as money math** |
| **T-0493** ✏️ | M | `draft` | **REWRITTEN** — waive server-side across the **three** call sites; consume one unit |
| **T-0513** 🆕 | M | `draft` | **the copy says three different things** — see below. **No dependency; dispatchable today** |
| **T-0514** | M | `draft` | show the waived line + the remaining count. ⚠️ `nswag-regen` |

### 🔴 A finding this pass produced that was on no ticket and in no audit

**The three clients advertise three different express perks, and none of them matches the code.**

| Surface | Promise |
|---|---|
| Android `strings.xml:844` + iOS `membership_perk_express_desc` | *"One free same-day booking per month, no surcharge."* → **metered, 1/month** |
| Web `en.json:1095` | *"Pay less for last-minute bookings inside the express window."* → **unmetered discount, no cap** |
| `BookingPolicy.cs:18-30` | express = **2–4 h lead time**, +20% → **not "same-day" at all** |

**"Same-day" is the wrong word, and it fails in the customer's favour in a way that will generate
support tickets.** A booking made at 09:00 for 18:00 today is same-day and **already free of surcharge
for everyone** — the perk as worded promises to waive a charge that would never have applied. The
customer who reads it and books at 09:00 for 12:00 (which *is* express) is the one who gets a surprise.
**T-0513 AC2 puts the ruling to the owner: change the word, or change the mechanic.**

**One correction to this document's own §1.2:** T-0493's row read *"'Express' has 3 meanings spanning
`S`→new product; AC2 forces a re-file."* The owner picked the `S`-shaped meaning — **and it is still
not `S`**, because the *quota* is the part nobody costed. That is why it is five tickets and not one.

---

## A2. Answer 2 — favourite cleaner: **MAKE IT WORK**

> Owner: *"It exists, you can select in the app but I think it doesn't work fully. And I'd like to have
> it working fully."*

**They are right, and it is worse than "doesn't work fully" — it does nothing at all.** All
PM-verified first-hand:

- **written** (`CreateOrder.cs:140-154` validates it → `OrderFactory.cs:124` → `Order.cs:349`)
- **read by nothing.** The only other references are `Order.cs:621` (anonymization nulls it) and a
  comment. **`TakeOrder.cs` — the entire dispatch path — never mentions it.**
- `Order.cs:217-224` describes *"the matching algorithm boosts this employee's score"*. **There is no
  matching algorithm.** The same comment also ends *"today the field exists but no UI sets it"* — which
  is **also** false now; three clients set it. **The comment is stale in both directions.**
- `MaterializeRecurringBookings.cs:138` hardcodes `PreferredEmployeeId: null` — **the recurring
  customer, the single strongest case for "the same cleaner every time", is wired to null.**

| Ticket | Size | Status | What it is |
|---|---|---|---|
| **T-0495** ✏️📋 | M | **`ready`** | **REWRITTEN → ARCHITECT PANEL + ADR** — how a **pull-model** board honours a preference, **and the fallback** |
| **T-0515** 🆕 | M | `draft` | the build: dispatch rule + fallback + fix the stale comment |
| **T-0516** 🆕 | S | 🚫 `blocked` | the **Plus gate** — `Q-PLUS-03` |

**The hard half is the fallback, not the prioritisation.** This is a pull model — cleaners take work
off a board plus a 30-minute digest push. **A pull model has no assignment step to bias.** Every
mechanism that actually honours a preference (notify-first, exclusive hold) buys it with **latency**:
the order sits unclaimed while we wait for one person who may be asleep. **A booking that goes
unclaimed because we were waiting is a worse outcome than not honouring the preference** — and the
customer paid for the perk that caused it. T-0495 AC2 is where that gets decided.

**And gating is a genuinely separate question.** iOS's own string says *"**Plus benefit** · choose
someone who's cleaned for you before"*, while the server checks only that the customer previously
**completed** an order with that cleaner. **No membership rule of any kind.** → `Q-PLUS-03`.

---

## A3. Answer 3 — bank details: **CZ FIRST, BUILT TO EXTEND**

> Owner: *"Let's start with CZ but in a way that's easy to expand in the future — like Bank Account,
> Card number, and whatever else is needed to make a payment to the employee."*

**The owner asked for the architect panel explicitly, and it is the right call.**

| Ticket | Size | Status | What it is |
|---|---|---|---|
| **T-0517** 📋 | M | **`ready`** | **ARCHITECT PANEL + ADR** — shape, governing country, `CountryConfiguration`, encryption, migration, **"no PAN column"** |
| **T-0518** | M | `draft` | schema + ⚠️ `ef-migration` + backfill of live DEV rows |
| **T-0519** | M | `draft` | capture + **real** validation. ⚠️ `nswag-regen` |
| **T-0520** | M | `draft` | partner web + admin UI |
| **T-0521** | M | `draft` | partner Android + iOS UI |
| **T-0509** ✏️ | S | **`ready`** | **REWRITTEN** — the exposure sweep. **No dependency; dispatchable today** |

### What the grounding changed, in both directions

**Better than assumed — the platform is further along than sprint-15 §4 thought:**
- **`Employee` already has the identity fields.** `RegistrationNumber` (IČO), `VatNumber` (DIČ),
  `LegalEntityName` (`Employee.cs:15-23`), captured via `UpdateIdentificationInfo.cs` with a **real
  per-country validator service** (`ITaxIdValidator` → `ValidateRegistrationNumberAsync` /
  `ValidateVatNumberAsync`). **That is the archetype for a country-aware bank validator — do not invent
  a new one.**
- **`CountryConfiguration` already does per-country label/format/required** for registration and VAT
  numbers (`:43-57`). A bank equivalent is a well-trodden path in this codebase, not a new idea.
- **`CompanyInfo` already models the richer shape** (`bankName` / `bankAccountNumber` / `iban` /
  `swift`) and renders it on the receipt.

**Worse than assumed:**
- 🔴 **The entire server-side validation of a cleaner's bank account is `NotEmpty()` + `Length(15, 34)`**
  (`ValidationExtensions.cs:122-130`), shared by **all three** write paths (`UpdateBankDetails.cs:36`,
  `UpdateEmployee.cs:128`, `AdminUpdateEmployee.cs:73`). **No checksum, no country rule.
  `"totally not an iban!!"` is 21 characters and passes.**
- 🔴 **The IBAN is NOT "read by nothing"** — the sprint-15 filing of T-0509 was wrong about that, and
  the correction matters. It has **four** couplings: the **profile-completeness gate** that decides
  whether a cleaner may take orders (`Employee.cs:283`, `:313` → `"profile.fields.iban"`), the **GDPR
  export** (`GdprExportDto.cs:41`), an **admin paged LIST DTO** (`EmployeeListItem.cs:52` — every admin
  list page ships every cleaner's account number), and an audit-log test asserting it never reaches
  audit JSON. **"Just delete it" was never as free as it looked, and "add columns" will break the gate
  that lets cleaners work.** → T-0518 AC6/AC7, T-0509.

**Closed and not reopened: card numbers are not a column.** A PAN is a tokenised PSP object. Storing
one puts this platform in PCI-DSS scope — a business decision orders of magnitude larger than a payout
field. T-0517 AC10 records it as a constraint with the mechanism named for the day card payouts are
wanted (a payout-token **reference**, not a number).

---

## A4. Answer 4 — the invoice: **the owner supplied the specification**

> The owner sent a photo of a Czech ISDOC invoice they issued themselves.

**This is the single most valuable thing in the batch.** T-0508's own AC said *"what would help most:
the field list, plus **one real example** of an invoice your accountant accepts."* **`Q-PAYOUT-01` is
answered for CZ. T-0508 moves `blocked` → `ready`.**

| Ticket | Size | Status | What it is |
|---|---|---|---|
| **T-0508** ✏️ | M | **`ready`** | **REWRITTEN** — map the specimen onto the model, field by field. **Builds nothing** |
| **T-0522** 🆕 | M | 🚫 `blocked` | rebuild the document. `Q-PAYOUT-02` + `Q-PAYOUT-03`. ⚠️ `ef-migration` |
| **T-0523** 🆕 | M | `draft` | **QR Platba (SPD)** + the barcode — new dependency, own ticket |

### 🔴 The finding that changes the shape of this work

**The current document runs in the OPPOSITE DIRECTION from the owner's specimen.**
`DefaultInvoiceLayoutBuilder.cs:29-31` puts **CLEANSIA** in the header as issuer; `:73-81` puts the
**cleaner under "Billed To"**. The specimen has the **cleaner as *Dodavatel*** and **Cleansia as
*Odběratel***. **The two parties are the wrong way round.** That is not a missing-fields problem — it
is a document of a different kind, and **no amount of adding fields repairs it.** It is precisely why
`Q-PAYOUT-02` (employee vs OSVČ; who issues) stays `blocking: yes` even though the field list is now
known — and the question is now concrete enough to answer in one line: *whose name is in the header?*

### Two claims in the sprint-15 filing that are WRONG and are corrected on the tickets

1. **"No variable symbol" — false.** `EmployeeInvoice.VariableSymbol` (`:72`),
   `GenerateVariableSymbol(employeeId, payPeriodId)` (`:331`), rendered at
   `DefaultInvoiceLayoutBuilder.cs:38-39`. `PaymentReference` defaults to the invoice number (`:126`).
   **The VS exists and prints.** *(The specimen wants VS **= the invoice number**; whether the existing
   generator satisfies that is T-0522 AC4.)*
2. **"No IČ, no VAT" — half false.** The **columns exist and are validated**; they are simply **not on
   the PDF**. The identity half is a **rendering** job, not a capture job.

**The real gaps, restated accurately:** wrong parties · the wrong party's bank block (the only bank
details on any of these documents are **Cleansia's own**) · **no due date** anywhere in the model ·
no konstantní symbol · no payment method · **the line items are a pay breakdown, not invoice lines**
(no quantity, no unit, no unit price) · no QR · no barcode · `VatAmount = 0` **hardcoded**
(`FileExtensions.cs:48`) — right for a non-payer *by accident*.

**T-0508 AC6 is the sleeper.** Reshaping `OrderLineItem` from base/extras/expenses into
description/quantity/unit/unit-price is the largest piece of design in the whole invoice change and the
one most likely to be waved through as "mapping".

---

## A5. Housekeeping — all four

| Item | Ruling | Action taken |
|---|---|---|
| Branches merged | ✅ | noted; `master` is at `dceed4f1` |
| **xcconfig created, values not entered** | *"mark done, they will fill it before an iOS review"* | **T-0475 → `done`** (merged `1262b8cb` #187; `Config/{Base,Local}.xcconfig` verified on disk, `Local` gitignored at `.gitignore:26`). **The residual is recorded on the ticket and in T-0521 AC1: any iOS ticket that runs `xcodegen generate` must confirm the values are in first** |
| Clients regenerated | ✅ | **T-0474 → `done`.** The PM checked the *deliverable* (the prescribed step) not just the act: `README.md:84-85`, `:105`, `:54`, `:72` document both legs. **Stated on the ticket: no script was run and no iOS build was made** |
| Legal text later, **DEV URLs for now** | recorded as a **gate**, not a blocker | **T-0524** filed `blocked` + **`AR-PRIV-5` added to `ios-app-review-checklist.md`** (a gate living only in a blocked ticket is a gate that gets missed) + `Q-IOS-LEGAL-01` `resolve-by: pre-submission`. **PM-verified: both apps link to `cleansia.cz/terms` + `/privacy` and production has never been deployed** |

---

## A6. Dispatch order

### 🟩 Dispatchable **today** — no dependency, no owner answer

| Ticket | Why now |
|---|---|
| **T-0511** 📋 · **T-0495** 📋 · **T-0517** 📋 | the three panels. **Panel = step 1**; all three pass DoR |
| **T-0509** 🔒 | **~1 hour, and its value decays.** A sweep of one column is cheaper than a sweep of five — run it **before** T-0518 widens the field set |
| **T-0508** | the spec. Its AC1 renders the current document and **makes `Q-PAYOUT-02` concrete**: *"here is what your cleaners receive, and the parties are the wrong way round"* |
| **T-0513** | reduces a live misrepresentation with **no backend and no dependency** |

### Sequencing

```
WAVE A   T-0511 · T-0495 · T-0517  (three architect panels, parallel — different domains, no shared files)
         T-0509 · T-0508 · T-0513  (parallel, independent)
WAVE B   T-0512 · T-0518        ⚠️ BOTH ef-migration → OWNER GATE, hold everything behind them
WAVE C   T-0493 · T-0515 · T-0519
WAVE D   T-0514 · T-0520 · T-0521 · T-0522   ⚠️ nswag-regen → OWNER GATE before the three client tickets
WAVE E   T-0523
ANYTIME  T-0516 (on Q-PLUS-03) · T-0522 (on Q-PAYOUT-02 + Q-PAYOUT-03)
GATE     T-0524 — pre-submission only. Do not dispatch.
```

### New shared-file lanes this addendum adds

| Lane | Order | Why |
|---|---|---|
| `BookingPolicy.cs` + `OrderFactory.cs` + `QuoteOrder.cs` + `OrderPricingCalculator.cs` | **T-0496** (already filed) → **T-0493** | One surcharge, **three** call sites that must not drift — `BookingPolicy.cs:80-85` documents the ONE-ordering rule in the code itself |
| `ValidationExtensions.cs` + the three employee update commands | **T-0519** vs the onboarding chain **T-0505…T-0510** | **Shared with partner onboarding. Serialize before dispatch** |
| `Employee.cs` | **T-0518** sole writer | the completeness gate and anonymization both live there |
| `DefaultInvoiceLayoutBuilder.cs` + `InvoicePdfData.cs` + `FileExtensions.cs` | **T-0522** → **T-0523** | |
| `Localizable.xcstrings` + `values*/strings.xml` (customer) | append **T-0513** → **T-0514** to the existing sprint-15 lane | |
| `CreateOrder.cs` | **T-0515** → **T-0516** | the validator, twice |

---

## A7. The owner-decision list — shortest form, ranked by tickets unblocked per minute

| # | What | Blocks | Cost |
|---|---|---|---|
| **1** | **`Q-PLUS-02`** — three numbers: 1/month or unlimited? rollover? billing-date or calendar-month? **Plus the "same-day" ruling** | T-0512, T-0493 (**not** the T-0511 panel — it designs for both) | **2 min** |
| **2** | **`Q-PLUS-03`** — favourite cleaner: universal or Plus-only? | T-0516 | **1 min** |
| **3** | **`Q-PAYOUT-02`** — *whose name is in the invoice header, yours or the cleaner's?* | **T-0522** | 1 min once T-0508 AC1 shows you the current PDF |
| **4** | **`Q-PAYOUT-03`** — VAT-registered vs not: how do we know, what prints? **A second photo from a VAT-registered supplier settles half of it** | T-0522 | an accountant note |
| **5** | Enter the xcconfig values (both app dirs) | every iOS ticket | 2 min |
| **6** | **`Q-PLUS-01`** (carried) — the Stripe trial check, in test mode | T-0497 | 1 min |
| **7** | `Q-IOS-LEGAL-01` — which origin ships, and when real legal text exists | **nothing today**; gates submission | at submission |
| — | *Carried, unchanged:* `Q-PROFILE-01` · `Q-OBS-01` · `Q-AZURE-01` · `Q-PAYOUT-01`-for-**SK** | | |

**Nothing above was defaulted.** Two were deliberately left with no default: `Q-PLUS-03` (defaulting to
"gate it" silently removes a working capability from real users) and `Q-PAYOUT-02`/`-03` (legal).

---

## A8. What this pass deliberately did NOT do (Gate 0.5 leg 3, on the PM's own work)

- **No specialist agent was dispatched. No code was written.** Every edit is under `agents/`. Nothing
  committed, staged or pushed; no `git stash`; **`CLAUDE.md` untouched**; `src/cleansia_ios/**/Info.plist`
  and `**/project.yml` **never opened** (the live Stripe key is in them).
- **No panel was convened.** Three tickets go out **needing** one (T-0511, T-0495, T-0517) — that is DoR
  item 2 with the panel as step 1, not a dependency. **All three are `ready` today.**
- **No build, no test suite, no Gradle task, no iOS build was run.** Every "2295 / 108 / 75" in the new
  tickets is a **baseline quoted from sprint-15**, not a measurement this pass took.
- 🔴 **Sprint-15 was NOT reconciled against PR #189.** 236 files shipped without a single ticket update
  and this pass verified only the four decisions' premises. **Every other row above is unverified.**
  Named here so it is not lost — **it is the highest-value backlog job outstanding.**
- **No ticket was written for the web wizard's missing preferred-cleaner picker.** Web customers cannot
  select one at all (`order-wizard.facade.ts:580` sends `undefined`). **Named on T-0495 as an
  out-of-scope output, deliberately not ticketed until the ADR says whether the feature survives in a
  shape worth building a picker for.**
- **No implementation ticket behind `Q-PAYOUT-01`-for-SK.** CZ first, per the owner.

### What the PM verified first-hand, on `dceed4f1`

`MembershipPlan.cs:85-116` · every `AllowsExpressUpgrade` reference repo-wide (**zero** in pricing) ·
`BookingPolicy.cs` **in full** · its three call sites `OrderFactory.cs:100-102`, `QuoteOrder.cs:168`,
`OrderPricingCalculator.cs:65` · `CancellationPolicyResolver.cs` (the archetype) · every
`PreferredEmployeeId` reference on all four platforms · `TakeOrder.cs` **in full** (**zero**
references) · `CreateOrder.cs:130-165` · `Order.cs:210-234` + `:605-630` ·
`MaterializeRecurringBookings.cs:138` · `CreateRecurringBooking.cs:78-95` (**T-0494 shipped**) ·
`IUserMembershipRepository.cs:21-29` · `Employee.cs:15-35` · `ValidationExtensions.cs:122-130` ·
all three `ValidateIban()` call sites · `UpdateBankDetails.cs` **in full** ·
`UpdateIdentificationInfo.cs:60-150` · `CompanyInfo.cs:60-110` · `CountryConfiguration.cs` **in full** ·
`FileExtensions.cs:28-90` · `InvoicePdfData.cs` **in full** · `DefaultInvoiceLayoutBuilder.cs:20-115` ·
`DefaultReceiptLayoutBuilder.cs:167-168` · `EmployeeInvoice.cs` (VS/number/payment-reference members) ·
the express + favourite copy across **all three** clients (Android `strings.xml`, iOS
`Localizable.xcstrings` decoded from JSON, web `en.json`) · `CleansiaWeb.swift` + `CleansiaWeb.kt` +
`app.routes.ts:140-147` · `src/cleansia_ios/Config/` + `.gitignore:26` + `README.md:54/72/84-85/105` ·
`git log` and `git show --stat 2012b014`.

### What is RELAYED and labelled as such

The **owner's** four answers and their four housekeeping statements (relayed, acted on, and the two
`done` transitions say on the ticket exactly what was and was not corroborated). Everything else in
this addendum is a first-hand read at `dceed4f1`.

---

# ADDENDUM B — challenger-round fallout: 7 defects that belong to no ADR (`T-0525`…`T-0531`)

**Filed:** 2026-08-02, during the ADR-0034 / 0035 / 0036 challenger round.
**Input:** `agents/backlog/adr/challenges/*.md` — 8 lanes (0034-db, 0034-security, 0035-A/B/C,
0036-A/B/C), all read; plus PM grounding against the working tree.
**Output: 7 new tickets + 2 owner questions.** No ADR was read for *its own* content and
**nothing under `agents/backlog/adr/**` was written** — three architects are live in that directory.

## Why these exist as tickets at all

The challengers were attacking three *designs*. Underneath the designs they hit **shipped defects that
no ADR owns and that no ADR's adjudication will fix**. If they had stayed in the challenge files they
would have been archived with the round. They are filed now, ADR-free, so they survive whichever way
the three ADRs land.

## The one to move first

**T-0525 — the cancellation fee charges customers for a cleaner who never existed.** It is live, it moves
real money on the card cohort (`CancelOrder.cs:137-145` issues the refund), and the fix is a one-line
predicate behind a one-item architect ruling. Every paid order is treated as "a cleaner accepted it"
because `OrderStatus.Confirmed` is written by the Stripe webhook, by cash auto-confirm and by the admin
override as well as by `TakeOrder`. A customer who books tomorrow, pays by card and changes their mind
20 minutes later is charged **25%**; inside 4 hours, **50%**.

**PM recommendation on the ticket's one open decision** (the architect may overturn it): use the
assignment row (`order.AssignedEmployees.Count > 0`), not a status-model change. It is already loaded in
the same query, it costs nothing, and it is *strictly more correct* than any status predicate — because
`TakeOrder.cs:188` adds the assignment unconditionally while the `Confirmed` track at `:194` is written
only from `New`/`Pending`, so a cleaner taking an order the webhook already confirmed writes **no status
track at all**. Splitting `Confirmed` into two statuses would touch a persisted enum on every order row,
an index, five APIs, three web apps, two Android apps, iOS and the Live Activity payload — an `L` with a
migration and two client regens, for the same customer-visible outcome.

## What the scoping pass found that the challenges did not

1. **iOS has the same cancel-fee defect as Android**, and the challenge named Android only.
   `CancellationFeePreview.swift` mirrors `CancelOrderSheet.kt:344-404` faithfully — the parity is real;
   what was mirrored was wrong. **And a committed iOS suite pins the wrong ladder**
   (`OrderStatusLogicTests.swift:175-225`), so it goes red on the fix. It is in T-0527's scope explicitly,
   because a developer hitting it blind fixes the test instead of the code (the same trap
   `MembershipExpressClaimTest.kt` set for T-0513).
2. **The web is correct and is out of scope.** It has no cancel action at all, and its wizard policy block
   (`en.json:807-815`) already reads 25% / 50% with a Plus-aware tier. Recorded so nobody "fixes" it.
3. **The digest's status-set divergence is three-way, not two-way.** `TakeOrder`'s validator has **no
   status rule at all**, so a `New` order is pushed by the digest, absent from the board, and takeable.
   That turned T-0530 from a comment fix into a ticket carrying a one-item ruling.
4. **Two of the challengers' own premises are wrong** — see below.

## 🔴 Two corrections the three live panels need, and no agent may deliver

Found while verifying counts for T-0531. Neither changes a challenger's *conclusion*; both change what a
panel should conclude *from* it. **These must reach the architects through you, not through an ADR edit.**

1. **`0034-db.md` CH-D2** states *"all ~40 `.IsUnique()` sites … not one includes `TenantId`;
   `(TenantId, EmployeeId)` would be the first."* → **Refuted. Nine do** — `PromoCode:63`,
   `LoyaltyTransaction:91`, `UserMembership:112`, `PromoCodeRedemption:66`, `LoyaltyTierConfig:33`,
   `ReferralCode:38`, `User:106`, `TenantConfiguration:27`, `FiscalCounter:26`. The proposed index would be
   the **tenth**. CH-D2's conclusion (such an index enforces nothing while `TenantId` is null) stands; its
   "no precedent exists" premise, which its recommendation leans on, does not.
2. **`UserMembershipEntityConfiguration.cs:106-109`** says adopting `NULLS NOT DISTINCT` would *"introduce
   a one-off"* — and both `0034-db.md` CH-D2 and `0035-C-concurrency.md` CH-C1 reason from that sentence.
   → **False. It ships twice already**, in the committed Initial migration, against real PostgreSQL:
   `FiscalCounterEntityConfiguration.cs:28` → `Initial.cs:2649-2653`, and
   `LiveActivityTokenConfiguration.cs:28` → `Initial.cs:2680-2685`. **ADR-0035 CH-C1's option 1 is
   precedented, not novel.** The novelty argument is unavailable to either side of that debate.

## Dispatch

| Order | Ticket | Why here |
|---|---|---|
| 1 | **T-0525** ruling → build | Money, live, one item, no dependency |
| 2 | **T-0529** | `S`, no-decision, but it holds the `NewJobsDigestService.cs` lane — take it first and it clears in one run |
| 3 | **T-0530** ruling → build | Same file; a constant and a comment |
| 4 | **T-0528** ruling → build | Same file; the mechanism. Never concurrent with 2 or 3 |
| 5 | **T-0526** | Needs T-0525's predicate to exist or it ships a second wrong surface |
| 6 | **T-0527** | Needs T-0526's contract **and** the owner's `mobile-spec-redump` |
| — | **T-0531** | Independent, any time. A note; **AC5 forbids fixing anything** |

**Shared-file lane:** `NewJobsDigestService.cs` has three claimants (**T-0529 → T-0530 → T-0528**).
**Never two instances in that file at once.**

## ⚠️ Owner-only steps this addendum creates

- **`nswag-regen` (customer client) + `mobile-spec-redump`** — created by **T-0526**'s new preview
  endpoint. **T-0527 is held** until you confirm both. No agent runs either.
- **No EF migration is created by any of these seven.** T-0531 AC5 forbids one explicitly; if the
  architect concludes an index genuinely needs `AreNullsDistinct(false)` today, that is a separate ticket
  with an `ef-migration` step, filed rather than absorbed.

## Two questions for you (`questions/open.md`, both `blocking: no`, both `pre-prod`)

- **Q-PROMISE-01** — both mobile clients tell every customer *"Cleaner being assigned · Within 1 hour"*,
  unconditionally, in five languages (`values/strings.xml:741-742`, iOS `Localizable.xcstrings:4799`
  and `:4834`). **Nothing enforces it**: assignment is a pull model and the only proactive nudge is a
  30-minute digest that currently drops jobs (T-0528). *Is it true in practice on DEV?* One rough number —
  median and worst-case order-created→first-assigned — settles it. If it is not true, it is the same class
  as the express claim just removed, and the sentence comes off ten locale files.
- **Q-PROMISE-02** — on the **Plus checkout page**, **cs/sk/ru** promise the favourite cleaner *"will be
  preferentially assigned"* (`<locale>.json:1095`) where **en/uk** promise only priority. Three locales
  sell a stronger product than the design delivers — and the dispatch model is **pull**, so nothing is ever
  assigned to a chosen cleaner. **No copy ticket is filed**: the promise has to be chosen before it can be
  written in five languages.

## What this pass deliberately did NOT do

- **Did not read the three ADRs' own text**, and did not write one byte under `agents/backlog/adr/**`.
  Three architects are live in there. Every ADR fact above is quoted from a challenge file or verified
  directly against source.
- **Did not touch git** — no add, commit, branch, stash or checkout.
- **Did not open** `.env`, `.p8`, `Info.plist` or `project.yml`.
- **Did not file** the challengers' *cost* findings (the digest's per-cleaner country re-scan,
  `HasOverlappingOrderAsync` scanning a cleaner's lifetime assignment history, the O(C²) per-cleaner
  commit). They are real, they are the optimizer lane's, and folding them into T-0528 would have made the
  correctness fix unreviewable. They are named in T-0528's `## Out of scope` so they are findable.
- **Did not file** ADR-owned findings. Everything a challenger raised *about* a design stays with that
  design's panel.

### What the PM verified first-hand for this addendum

`BookingPolicy.cs` in full · `CancelOrder.cs:55-179` · `TakeOrder.cs:30-205` ·
the three literal `OrderStatusTrack.Create(OrderStatus.Confirmed` writers + `AdminOverrideOrderStatus`'s
`Lifecycle` array · `NewJobsDigestService.cs:40-228` · `DashboardSpecifications.cs:15-30` ·
`EmployeeRepository.cs:40-58` · `BaseRepository.cs:153-158` · `CancelOrderSheet.kt:68-90` and `:340-404` ·
`CancellationFeePreview.swift` in full · the iOS `CancellationFeePreviewTests` call sites ·
Android `order_cancel_fee_*` strings · `booking_success_t2_*` in all five Android locales + the two iOS
xcstrings keys · web `cancel_policy_tier*` (`en.json:807-815`) · `benefit_favorite_body` in all five web
locales + its render site · all nine `TenantId` unique indexes · `UserMembershipEntityConfiguration.cs:85-114`
· `FiscalCounterEntityConfiguration.cs:26-29` · `LiveActivityTokenConfiguration.cs:28` ·
`Initial.cs:2649-2653` and `:2680-2685` · the web customer feature libs (no cancel action) ·
`agents/backlog/tickets/` for dedup (T-0211, T-0242 `done`, T-0511) and `agents/backlog/audits/`.

---

# ADDENDUM C — the sprint-15 reconciliation (2026-08-04)

## Why this exists

The sprint moved faster than the backlog, and the backlog stopped being true. `master..HEAD` is **56
commits**; the INDEX and the ticket files described a repository that no longer existed. That is not a
cosmetic problem: **agents read the backlog as ground truth**, so a stale row does not sit inertly — it
routes work at a file that has already changed, or holds a ticket for a blocker that cleared two days
ago. The previous docs sweep found `CLAUDE.md` was wrong in **seven separate ways** for exactly this
reason, and a wrong line there loads into every agent's context.

**Method, stated so it can be audited.** Every state below was established from `git log master..HEAD`
first, and then **re-verified against the tree at HEAD**. Nothing was closed on a commit message alone —
a ticket closed on report is the same defect class as a ticket left open on report. Where verification
produced a different answer from the brief, the verification won and the difference is written down.

## What the verification changed about the brief I was given

Three things, all in the direction of *less* owner work, and each one is the reason to verify rather than
transcribe:

1. **The NSwag regens are not pending — they are done.** Three surfaces were owed (`3092abc1`). The
   owner regenerated **all three web clients and both mobile OpenAPI documents** in `37440bbc`, with the
   `isAvailableForRequestedSlot` leg at `53f887b6`. Verified field-by-field at HEAD:
   `updateBankDetails` / `getMyPayoutDetails` in the partner client, 40 `PayoutDetails` hits in the admin
   client, `expressSurchargeWaivedByMembership` + `expressUpgradesRemaining` +
   `expressWaiverForfeitedOnCancel` in the customer client, and all **three** `MyServingCleaners` query
   parameters in both the customer client and `customer-mobile-api.json`. **So the payout UI ticket
   (T-0520) is not blocked — it is `ready`, and it is a live regression.**
2. **T-0532 is not blocked on its own panel.** ADR-0038 was **accepted** in `f7828fb8` with zero blocking
   challenges, and CH-2 — the challenge flagged as able to delete the ticket's premise — was ruled and
   did not. AC0 is cleared; the ticket is `ready`, carrying one new binding condition (the one-call-site
   tripwire must land inside its own PR, because ADR-0032 D3 makes it *unwritable* before the seam
   exists and D2 forbids "later").
3. **`.AreNullsDistinct(false)` on the promo per-user index came off the owner list.** It was folded into
   the regenerated `Initial` (`7e1cf7f5`); the committed migration now carries `NULLS NOT DISTINCT` five
   times.

And one in the other direction: **a defect nobody had recorded.** `077b7e8a` — the last backend commit on
the branch — added `order.take.already_cancelled` and `order.take.already_completed`, correctly splitting
the take gate's refusals off the customer keys. **Android has both in all five locales; partner web and
iOS have neither.** Both sprint i18n sweeps (`8ff9dfb4` web, `befbb7af` Android) had already run by then.
So a cleaner on web or iOS who taps a job that was just cancelled reads *"An error occurred. Please try
again"*, and tries again. → **T-0543**.

## Closed: 15 tickets

`T-0525` · `T-0528` · `T-0529` · `T-0530` · `T-0513` · `T-0517` · `T-0511` · `T-0495` · `T-0512` ·
`T-0518` · `T-0519` · `T-0521` · `T-0493` · `T-0515` · `T-0516`. Each carries, in its own status log, the
commit that shipped it and the file-and-line that was read at HEAD to confirm it, plus a **MANUAL-GATE
(PM reconciliation)** block in `## Review` — because for most of these the in-workflow reviewer lane left
no verdict in the ticket file, and `ticket-lifecycle.md` requires that a hand-gated ticket say so rather
than pass for one whose gate ran. `T-0523` moved `rejected` → **`retired`**: same meaning, but `rejected`
is not a state in the lifecycle and `retired` is.

The one worth reading twice is **T-0519**. The payout validation was **unreachable in its own home
market** until `077b7e8a`: `CountryConfiguration.PayoutScheme` had no writer anywhere, so it was null for
every country and scheme selection fell through to IBAN self-description — **a Czech cleaner entering
prefix, account number and bank code with no IBAN was rejected with "country not supported", for their
own country**, and every stored home-market record was `SepaIban` with the domestic account number
persisted null. The unit suite could not see it because those tests set the scheme *by reflection,
precisely because nothing else can.*

## Re-opened or unblocked: 5 tickets

**`T-0520` (`ready`) is the one to move first, and it is a live regression rather than a feature gap.**
`c968cbf9` was right to delete partner web's `iban` form control — it was `Validators.required` against a
field the DTO no longer carries, so `onSubmit`'s `if (!formGroup.valid) return` meant **every cleaner
would have been permanently unable to save their profile, with only a "fill required fields" toast, on a
green build.** But it left partner web with **no bank capture at all**, while Android and iOS both have
the section. It also inherits the copy defect `9c13b2c7` raised and correctly declined to fix alone: the
completeness key still renders as **"IBAN"** in all five locales, so a cleaner is told "IBAN" is missing
and lands on a form whose IBAN helper says they may leave it empty. ADR-0034 freezes the **wire key**, not
its translation.

Then **`T-0514`** (a Plus member's express surcharge is being waived today and **no client says so** —
zero consumers of the two fields), **`T-0526`** (the last thing between the corrected server fee rule and
two mobile clients that still show 50% where the backend charges 25%), **`T-0531`** (verified *not* done —
the rule is absent from `multi-tenancy-and-region.md` at HEAD), and **`T-0532`**.

**`T-0509`** stays `ready` but was **re-aimed** — its headline target moved out from under it.
**`T-0522`** stays `blocked`, but it was **partly shipped and its row said nothing**: `8ca77412` inverted
the invoice parties (a wrong legal *category*, not a field gap) and `946200c1` added the late-payment
interest clause.

## The seven ADRs, now recorded in the INDEX

**0034 · 0035 · 0036 · 0037 · 0038 · 0039 accepted; 0040 proposed and challenged.** Every ticket that
depends on one now cites it in its `adrs:` frontmatter — that was missing across the board and is the
reason a reader could not tell, from a ticket, which decision governed it.

**ADR-0040 deserves a sentence of its own.** Its code has already shipped (`7e1cf7f5`) while the ADR is
still `proposed`, deliberately: the change was time-boxed to the `Initial` regeneration window. The
challenger hunted for a reachable production path that persists a status-less `Order` and **did not find
one** — the write-time guarantee stands. But it raised CH-W3, which is the single most operationally
important finding of the sprint, and it is in the owner list below.

## 12 findings filed that existed only in commit messages

`T-0533` (a live cross-app client import) · `T-0534` (the module-boundary guard is mostly off) ·
`T-0535` (97 generated-DTO object literals) · `T-0536` (the 25-project lint baseline) · `T-0537` (a
library invisible to Nx) · `T-0538` (four Web SDK hosts still armed) · `T-0539` (the recurring
materializer, and the `Rollback()` trap) · `T-0540` (two `Contains` shapes, unpinned) · `T-0541`
(`docs/mobile-app/**`) · `T-0542` (no changelog) · `T-0543` (the missing take-refusal keys) · `T-0545`
(the promo counter-repair script). Plus **`T-0544`** — a gap *created* by closing T-0493.

Two are worth calling out because the finding is more valuable than the fix:

- **T-0539 records a trap, not just a defect.** `MaterializeRecurringBookings` has no per-template
  `try`/`catch`, and the obvious fix is unsafe: `CleansiaDbContext.Rollback()` sets every tracked entry
  to `Unchanged`, and **`Added` → `Unchanged` is not `Detached`** — the half-built order stops being an
  insert and **stays in the tracker as a phantom existing row** for every later iteration. Anyone
  implementing catch-and-continue through `Rollback()` would ship the half-built-order bug believing they
  had detached it. The durable answer is one DI scope per template. That sentence is now in a ticket
  rather than in a commit message, which is the only place the next implementer will look.
- **T-0536 is filed as `L` and is explicitly forbidden from running.** It measures the baseline and
  splits itself. It is also the "lint-cleanup ticket" that `frontend-ci.yml:71` has promised in a comment
  since the day lint was made `continue-on-error`, and that never existed.

**T-0537's sweep is done, not deferred.** The commit recommended checking for other libraries in the same
Nx-invisible state; the PM ran it — 64 lib roots, **zero** others. The dashboard lib was the only one, so
the ticket narrowed from "find them" to "make the state unreachable", which is `S` rather than `M`.

### One methodological point that changed three of these tickets

Every finding was established against **HEAD (committed)** and then re-checked against the **working
tree** — and for three of them the two disagree, because a web lane is live in this tree right now. The
T-0533 import is already removed; `eslint.base.config.mjs` no longer holds the allow-everything
constraint (the real `scope:`/`type:` rules now sit in an **untracked**
`eslint.module-boundaries.config.mjs`, with tags spread across the lib graph); and the web and iOS-core
halves of T-0543 are already written. **So those three are filed `in_progress`, not `ready`** — filing
them `ready` would have sent a second instance into files another agent is holding, which is the
collision `shared-file-lanes.md` exists to prevent.

The general rule this produced, and it is worth keeping: **a citation is only true against the tree state
you name.** *"Verified"* without *"at HEAD"* or *"in the working tree"* is the same shape of claim as the
false "mirrors X" comments this sprint spent itself deleting.

## What this pass deliberately did NOT do

- **Did not touch git.** No add, commit, branch, stash or checkout. (The repo-global stash hazard across
  worktrees makes that rule load-bearing, not ceremonial.)
- **Did not write outside `agents/backlog/**`.** Not `agents/knowledge/**`, not `agents/backlog/adr/**`
  (other lanes are live in both), not production code, not tests, not `CLAUDE.md`, not `docs/`.
- **Did not open** `.env`, `.p8`, `Info.plist`, `project.yml` or anything under `src/cleansia_ios/Config/`.
- **Did not reconcile `T-0476`…`T-0492` / `T-0494` / `T-0496`…`T-0508` / `T-0510`** — the PR #189
  population. Those rows remain unverified and the INDEX now says so explicitly. Implying coverage this
  pass does not have would reproduce the defect it was called to fix.
- **Did not re-litigate any ADR.** Where a challenge changed what a ticket should do, the ticket records
  it; the decision documents belong to their panels.
- **Did not move `Q-PLUS-02` / `Q-PLUS-03` to `answered.md`.** They are answered and the INDEX now says
  so, but `questions/open.md` is a shared file with its own update note pending; moving entries there
  while three lanes are live risks a collision for no urgency.

## 🔴 OWNER — the complete list, and it is short

**One item blocks real work:**

1. **Drop the DEV database.** The six schema changes from six accepted ADRs were folded into a
   **regenerated** `Initial` with its **timestamp preserved**, so `20260723182623_Initial` is already in
   `__EFMigrationsHistory` on any environment that has been migrated. `MigrationService/Program.cs:31`
   reads `GetPendingMigrationsAsync()` and `:39` calls `MigrateAsync()` — **pending only** — so the new
   columns are **skipped silently**, and the service prints "up to date" and exits **0**. Both test
   fixtures build **fresh** schemas, so 2807 unit / 132 integration green **proves nothing about a
   deployed database.** ADR-0040's CH-P3 makes this operational rather than academic: on a drifted schema
   the overlap check **fails open and permits a double booking**, and because neither the overlap
   predicate nor the busy-set query materialises an `Order`, **nothing raises an error — it would be
   silent.** One query tells you which world DEV is in:
   `SELECT count(*) FROM "Orders" WHERE "CurrentStatus" IS NULL;`

**Four items need a decision only you can make. None of them blocks anything except what is named:**

2. **`Q-PAYOUT-02` — is a cleaner an employee or a self-employed supplier (OSVČ), and who issues the
   document?** This decides *which document* we generate. Legal. Blocks **T-0522** and nothing else.
3. **`Q-PAYOUT-03` — how does the platform know whether a cleaner is VAT-registered, and what does each
   variant print?** Your specimen states *"Nejsme plátci DPH"*. Legal. Blocks **T-0522** with the above.
4. **`Q-PLUS-01` — does Stripe enforce a once-per-customer trial on the Plus price?** One dashboard
   check. The two candidate defects have **opposite fixes**, so the repo cannot distinguish them. Blocks
   **T-0497**. Narrowed but not closed by your trial ruling.
5. **`Q-PROFILE-01` — `UpdateCurrentUser` requires a client-supplied `Id` the customer web app cannot
   obtain**, so every customer-web profile save 400s. This needs a backend *decision* from you, not a
   frontend workaround. Blocks **T-0447**.

**One item is a run, not a decision, and it is not urgent yet:** the promo counter-repair script
(**T-0545**) must be **run by you**, after the fix is deployed and during low traffic. An agent writes it
first. If the database drop happens before the run, the corrupt DEV state goes with it and the run
becomes unnecessary — **so tell us which comes first rather than letting us assume.**

**Explicitly NOT on your list, though earlier documents said otherwise:** the NSwag regens (done,
`37440bbc`), the `.AreNullsDistinct(false)` migration (folded into `Initial`), and the
`mobile-spec-redump` markers on T-0526/T-0527 — those are *created by* future work, not owed by you today.

---

# ADDENDUM D — reconciliation pass 4 (2026-08-05), and the mechanism that should make it the last one

**Supersedes ADDENDUM C** for the tickets named below; C stands for everything else.

## D0. What this pass was asked to do, and where it disagreed with its brief

The brief listed eleven `ready` tickets as shipped and asked for them to be verified against the tree
and closed. **Nine were. Two were not, and one ticket the brief did not name was.**

Every verdict below was established by reading the **tree at HEAD**. `git log master..HEAD` (72 commits)
was used to *locate* each change; a commit message saying a thing shipped is a lead, not evidence. That
distinction is the whole reason two rows survived this pass.

## D1. Closed — nine, each with the fact that closed it

| Ticket | Closed as | The fact at HEAD |
|---|---|---|
| **T-0457** | `done` | `ContactIdentityFieldRegex` on **all five** hosts (four-of-five would have been the hole). The DTO-walking guard reads the token list out of the **live compiled regex** — `WireSurface.ReadTokens()` reflects the private static member and parses `regex.ToString()` — so a token added to the middleware widens every guard and none can hold a stale copy. `RequestLogPiiSurfaceGuardTests` carries an `InRange(1000, 20000)` anti-vacuity floor |
| **T-0464** | `done` | `Core.Blobs.Abstractions/ServedContentType.cs` — a closed value type, 26 cases pinned. The ticket's trap held: the decoy was **load-bearing** and the naive fix was **stored XSS**, so it went through the SAS `rsct`/`rscc` override instead. Known gap recorded, not hidden: the avatar has no stored content type, so it gets `private` caching but no typed `Content-Type` — that needs a column, i.e. **T-0465** |
| **T-0509** | `done` | The sweep ran and found a hole the ticket had not predicted: `Contains("/gdpr")` never matched `/api/v1/AdminGdpr/...` because no slash precedes "gdpr" there. All five hosts now test `Contains("gdpr/")`. The **derived** guard found it; the hand-written `[InlineData]` list could not have |
| **T-0520** | `done` | Partner web `profile-bank.*` **and** admin `employee-payout-section.*`. The facade spec pins the two properties that matter: *"nothing unmasked until a reveal happens"* and *"goes through the POST reveal command, never a second read"*. The PM-added label AC landed too — `profile_fields_iban` reads **"Bank details"** on all three clients in one change |
| **T-0526** | `done` | One `CancellationAssessor`, called from `CancelOrder.cs:81` and `:91` — the preview and the cancel cannot disagree because they are not two computations. Both customer hosts expose it at `OrderController.cs:174` |
| **T-0538** | `done` | `EnableDefaultContentItems=false` on all five `.csproj`, plus `WebSdkContentGlobTests.cs`, which goes red on a **new** host that reintroduces the glob |
| **T-0539** | `done` | `MaterializeRecurringBookings.cs:87` opens a scope **inside** the loop, so a failed template's entries die with its scope — the `Added → Unchanged is NOT Detached` phantom-row trap is avoided by construction, not by cleanup |
| **T-0545** | **`retired`** | **Obsolete, not done.** The owner drops the DEV database before any repair would run; DEV is the only environment the drift exists in, and the cause is already fixed (`da88b695` + `d78b816b`). ⚠️ One consequence for the architect lane: **ADR-0038 §D6.4 now names a repair with no ticket behind it** |
| **T-0508** | **`superseded`** by T-0522 | The spec's questions were answered by the build shipping first. AC3 → `InvoicePdfData.Supplier` is the cleaner; AC4 → `IsVatPayer` reaches `QuestPdfService.cs:80`, so a registered cleaner stays a data change; AC6 (*"the single largest piece of design"*) → `InvoiceLineItem` now has `Quantity`/`UnitPrice`/`LineTotal`; AC7 → `DueDate` + `ConstantSymbol`. **AC11's boundary survives outside the ticket**: `Q-PAYOUT-01` is still open for **SK**, so nobody may read the shipped document as a CZ/SK one |

## D2. Not closed — and this is why the pass was worth running

**T-0537 stays `ready`.** The brief described it as *"the dashboard lib registered in Nx"*. **That is the
ticket's own out-of-scope half.** The registration did land — `project.json` with tags and a jest target
— but **AC1–AC5 are the guard**, and no guard exists: `grep -rln "src/index.ts"` across `*.mjs`/`*.ts`/`*.yml`
returns nothing, and `agents/tools/` holds only the two `check-*` pairs. The silent state is still
reachable. AC5's sweep was re-run as AC5 requires: **64 lib roots, 0 missing.**

**T-0514 and T-0544 move `ready` → `in_progress`.** The **web leg shipped** (`4984c2eb`); Android and iOS
did not. Android `strings.xml:568` still reads *"Express +20%"* with no waived variant, and `:852-855`
still carries the comment *"No express perk anywhere… Restore this perk only together with the code that
waives the surcharge"* — **that code shipped in T-0493.** iOS `MembershipPerks.swift:6-9` says the same and
its enum still has three cases, so **Plus advertises four perks on mobile and five on web.** They are out
of `ready` so nobody rebuilds the web leg, and both comments must be corrected in the same change that
adds the perk — leaving a stale comment that forbids the thing you just did is how this became needed twice.

**T-0527, which the brief did not name, moved `draft` → `in_review`.** Both halves had shipped
(`ab077504`): `OrderApi.kt:70`, `OrderClient.swift:92`, **`CancellationFeePreview.swift` deleted**, and
the committed iOS suite that pinned the *wrong* ladder **corrected rather than weakened**. Not `done` —
AC10 (parity QA) is open and the customer Android app has no `androidTest` source set.

**Three architect dispositions from 2026-08-04 are accepted and now reflected in the INDEX:** T-0531
rescoped to `XS`/AC1′-only, T-0532 stands with sharper criteria (**now the highest-value unshipped
`ready` ticket**), T-0471 stands unchanged (**now the oldest**).

---

## D3. THE MECHANISM — three candidates, measured, and only one of them is worth building

This is the third pass in a sprint and all three found the same failure: **work ships, the ticket stays
`ready`.** So the question is not "which idea sounds good" but "which one would have caught these
thirteen rows". I measured all three against the ticket corpus **as it stood at HEAD before this pass**.
Numbers below are counts I ran, not estimates.

### Candidate 1 — a frontmatter field checked against `git log`. ❌ **Rejected as stated; ✅ a variant of it is the answer.**

The ticket format carries **no** field naming the commit that shipped it. Adding one (`shipped_in: [sha]`)
fails for the reason the current process fails: **it is a field somebody must remember to write, and the
thing that goes unwritten today is a one-word `status:` line.** Adding a second thing to forget does not
fix forgetting the first.

**But there is a field that is already being written, and it already contradicts `status:`.** On every one
of the nine tickets closed above, the implementing lane had **already written its evidence into `## Review`**
— mutation proofs, suite counts, file:line citations. The only thing nobody did was change one word in the
frontmatter. That contradiction is mechanically detectable with **no new field, no new discipline, and no
git at all**:

> **A ticket whose `status:` is `draft` or `ready` must have an empty `## Review` section and zero
> `- [x]` acceptance boxes.** Anything else is a ticket that reviewed work it claims has not started.

**Measured at HEAD before this pass: 8 hits out of 74 `draft`/`ready` tickets. All 8 were real. Zero false
positives.** Four were tickets from this brief (T-0457, T-0464, T-0526, T-0527); the other four
(**T-0473, T-0479, T-0490** and T-0532) are *also* stale — I spot-checked T-0490 and its fix is in the
tree at `InvoiceDetailScreen.kt:94` while the row says `draft`. **So this check just found the next batch,
which is exactly the population ADDENDUM C declined to reconcile.**

Recall is its weakness: **8 of the ~13** stale rows, because a lane that shipped without writing a review
block leaves no trace here.

### Candidate 2 — a commit trailer naming the ticket. ⚠️ **Real, but it would have caught 3 of 11, and only helps the next sprint.**

Greppability is not the problem — **the commits are not naming tickets at all.** Measured over the 72
commits on this branch: **22 mention any `T-NNNN`; 50 mention none.** Worse, the ones that matter are the
misses: the commits that actually shipped T-0514, T-0520, T-0537, T-0538, T-0539 and T-0544
(`4984c2eb`, `cf24a74c`, `0c76f94a`) name **no ticket whatsoever**. Only `b9753e85` named its three
(T-0457/T-0464/T-0509). **A grep-the-log check would have found 3 of the 11 rows the brief listed.**

A `Ticket: T-NNNN` trailer would fix that going forward — the trailer slot is already in active use
(65 of 72 commits carry `Co-Authored-By`), so this is a one-line addition to the commit convention plus a
line in `agents/process/`. But it is **prospective only** (it says nothing about the 357 tickets already on
disk), it depends on the **same memory that is already failing**, and with no commit-msg hook (there are
none in `.git/hooks/`) nothing enforces it. **Worth adopting as a convention. Not worth relying on as the
mechanism.**

### Candidate 3 — a script in the existing house style. ✅ **This is the one. Highest recall, and it is self-clearing.**

**The rule:** a `ready`/`draft` ticket is **stale on its face** if a source file it names by path has been
committed **on or after** the ticket's own `updated:` date. It doesn't try to decide whether the ticket
shipped — it decides whether the ticket is still describing a repository that exists, which is the
question the PM must answer before dispatch anyway.

It works here because **this backlog is unusually file:line-heavy**: tickets name real paths in Context and
Implementation notes. Paths are extracted as tokens and resolved by **suffix match against `git ls-files`**,
so `Cleansia.Web.Partner/Middleware/RequestLoggingMiddleware.cs`, `libs/…/profile-bank.facade.ts` and
`values/strings.xml` all resolve without the ticket having to write a repo-root path.

**Measured, both directions:**

| | Result |
|---|---|
| Against the corpus **before** this pass | **16 of 23 `ready` rows flagged**, catching **10 of the 12** genuinely-stale tickets (T-0457, T-0464, T-0508, T-0509, T-0514, T-0526, T-0527, T-0537, T-0539, T-0544) |
| Missed | T-0520 and T-0538 — T-0520's targets were files that **did not exist yet**, so no path could be named. **T-0545 was correctly not flagged**: it was obsolete for an unrelated reason, which is the right answer |
| Against the corpus **after** this pass | **5 of 12 `ready` rows flagged** — and all five are **true positives pointing at the next thing to check**: T-0447/0448/0449/0450 are the avatar lane, and `85c453f1` touched `UpdateCurrentUser.cs`, which is the very file `Q-PROFILE-01` blocks T-0447 on |

**That last row is the important one.** The flag rate looks high (70% before) only because the queue really
was that wrong — 13 of 23 `ready` rows. **The signal falls as the queue is reconciled**, because closing a
ticket moves its `updated:` past the commits that touched it. It measures staleness rather than accusing
anyone of it.

**Follow-up, 2026-08-05 — the flag was right and the queue was wronger than this pass recorded.**
Candidate 3 flagged T-0448/T-0450 as "the next thing to check." Checked: **both had already shipped**, in
squash-merge `0e4ede1b` (PR #184), which carried **19 Android files** — `ProfileAvatar.kt`, the
`ProfileViewModel`/`UserRepository` threading, five `strings.xml` and four test files — alongside the
backlog docs its subject line advertises. Both are now `done`. So this pass closed 9; the true count was
**11**, and candidate 3 had already pointed at the other two.

**Candidate 1 would have missed both** — verified directly: T-0448 and T-0450 each have **0 ticked boxes**
and a `## Review` section containing nothing but its HTML-comment template. That is the recall weakness
stated above, now measured on named cases rather than estimated.

**Best combination:** run candidates 1 and 3 in one script. Candidate 1 supplies **precision** (8 hits,
0 false positives), candidate 3 supplies **recall** (10 of 12). Together they would have caught **12 of the
13** rows this pass fixed by hand.

### What it would cost, honestly

- **`agents/tools/check-ticket-staleness.mjs`** — **~200 lines** plus a **~120-line `.test.mjs`**, matching
  the house shape of `check-available-status-parity.mjs`: dependency-free Node, a header comment stating
  *why* it lives outside the Nx workspace, `--warn` / `--root=DIR` flags, and an **anti-false-green anchor**
  — if it resolves **zero** ticket files or **zero** paths, that is a hard failure, never a silent pass
  (ADR-0032 D3). About **half a day** including the self-test. It is not "genuinely small", which is why
  **this pass specified it rather than building it** — and building it would also have written outside this
  pass's `agents/backlog/**` boundary.
- ⚠️ **Path filtering is load-bearing, and the spec above does not say so.** Measured on this corpus
  (62 open `draft`/`ready` tickets), the *same* rule flags **29 tickets** if it counts every path a ticket
  names, and **11** if it counts only product paths under `src/` and `docs/`. The 18-ticket difference is
  almost entirely tickets citing **shared** knowledge docs — `agents/knowledge/patterns-mobile.md`,
  `security-rules.md`, `consistency.md` — which nearly every ticket references and nearly every agent
  edits, so they move constantly and mean nothing about any one ticket. **Exclude `agents/knowledge/**`,
  `agents/process/**`, `agents/architecture/**` and `agents/tools/**` from the path set**, or the check
  fires on half the backlog and gets ignored, which is worse than not having it.
- ⚠️ **Resolve paths by suffix match against `git ls-files`, as specified — not by `os.path.isfile`.**
  A naive exact-path resolver drops the avatar lane's real evidence and catches T-0448 only incidentally,
  through an unrelated `customer-mobile-api.json` commit. Same rule, same corpus, different resolver,
  different answer.

**A fourth candidate, tested and rejected: "the ticket body carries a completion record."** The idea was
that a `## Verdict` / `## Implementation —` heading inside a `ready` ticket is an agent's own admission
that work happened. It flags **1 of 62**. The control kills it: only **4 of 272 `done` tickets** carry the
same marker, so writing one is not the convention — its absence proves nothing, and its presence on T-0448
was luck. Recorded so nobody re-derives it.

- **Where it runs — and the one trap to avoid.** ⚠️ **Do not attach it to the frontend lint step:** that
  step is `continue-on-error: true` (`frontend-ci.yml:73`), so a check placed there can never fail a build.
  And do not repeat `check-consistency.mjs`'s mistake: it appears in **zero** workflow files, so it can
  never set an exit code at all (the defect ADR-0038 CH-P6 found). **Copy `check-available-status-parity.mjs`
  instead** — it took its own repo-root workflow (`.github/workflows/offerability-parity.yml`) precisely so
  it could go red. This one wants the same, plus a run at the **top of every PM dispatch**, which is where
  it pays for itself.
- **It is advisory, not blocking.** A flag means *"re-verify this ticket against the tree before you
  dispatch it"*, not *"this ticket is wrong"*. Making it blocking would fail PRs for touching a file some
  open ticket happens to mention.
- **The zero-cost half is available today:** candidate 1's rule needs no script to be useful as a **PM
  checklist item** — *a `ready` ticket with a written `## Review` is a lie; check it before dispatching* —
  and it caught 8 rows here with no false positives.
- **Adopt candidate 2 as a convention anyway** (`Ticket: T-NNNN` in the commit trailer). It costs one line
  in `agents/process/` and it is what makes candidate 3 unnecessary a year from now.

**One thing this proposal does NOT claim:** none of these catch a ticket that shipped in a commit naming no
ticket, touching only files the ticket never named, with no review block written. **T-0520 was exactly
that**, and it took a human-directed grep to find. The mechanism reduces the manual pass; it does not
delete it.

---

## D4. 🔴 OWNER — the current list, in plain English

Two ADRs landed on 2026-08-04 and both put something new on your desk. **The list got one item shorter and
two items longer.**

**One thing blocks real work, and it is the same one as last time:**

1. **Drop and reseed the DEV database.** Six accepted ADRs' schema changes were folded into a *regenerated*
   `Initial` whose timestamp was preserved, so a migrated environment already has that id in its history and
   the new columns are **skipped silently** — the migration service prints "up to date" and exits 0. It is
   now also holding up the invoice: **T-0522 declares a `konstantní symbol` column the database does not
   have, so the payout-invoice path is down until this runs.** Your reseed is the window for everything
   below. One query tells you which world DEV is in:
   `SELECT count(*) FROM "Orders" WHERE "CurrentStatus" IS NULL;`

**Three decisions only you can make:**

2. **Is Stripe already enforcing one free trial per customer?** One look at the dashboard. We cannot tell
   from the code, and the two possible defects have **opposite** fixes, so we would be guessing. This is the
   only thing holding the Plus trial ticket.

3. **Customer web cannot save a profile — and the fix is a backend decision, not a frontend workaround.**
   The update endpoint demands a user id the customer web app has no way to obtain, so every save fails. We
   can make the server use the caller's own identity, but that changes an endpoint's contract and we are not
   deciding that for you.

4. **The self-billing agreement text — and who reviewed it, you or a lawyer.** This is new
   (ADR-0041). You asked for a checkbox saying we issue invoices on the cleaner's behalf, and the design is
   built so the schema and the screens can ship **inert** until you supply words. Until you do, **nothing is
   shown to anyone** — which is safe and visibly incomplete rather than quietly wrong. Six things the text
   has to say are listed for whoever drafts it. **Four smaller questions ride along with it** and each has a
   safe default we will use if you say nothing: whether we may keep self-billing the cleaners who signed the
   *old* contract while we ask them (default: yes, on the contract's basis, and we stamp every invoice with
   the acceptance behind it so the unasked group stays countable); whether the invoice itself must say it was
   issued on the supplier's behalf (default: it says nothing); whether a cleaner may withdraw the arrangement
   in the app (default: not in v1, and the schema already allows adding it later for free); and whether an
   operator recording your countersigned contract counts as agreement (default: yes, recorded distinctly from
   a self-service tick, forever).

**One thing is a run, not a decision — and it is new (ADR-0042):**

5. **The next time you regenerate the API clients, tell us what happened.** You ruled that the shared
   frontend enums should come out of the generated clients instead of being typed by hand. The design puts a
   small generator **inside the command you already run** (`npm run generate-*-client`) — your command line
   does not change — and **the first regen after it lands is the proof it works.** No agent can test a
   command only you run. Nothing is waiting on this today; the refactor itself is still behind one
   architect review round.

**What came OFF your list since last time:**

- **The promo counter-repair run is gone.** You are dropping the database before it would have run, so the
  corrupt counters go with it. The ticket is retired with that reason on the record, and the bug that caused
  the drift is already fixed.
- **The two invoice questions are answered** (a cleaner is a self-employed supplier we self-bill; cleaners
  are not VAT payers) and the code that acts on both is already written and reviewed.
- **Still not on your list, though older documents said otherwise:** the NSwag regens (done), the
  nulls-distinct migration (folded into `Initial`), and the mobile spec redump (created by work we do, not
  owed by you).

---

## D5. What this pass deliberately did NOT do

- **Did not touch git.** No add, commit, branch, stash or checkout. `git log` / `git show` / `git ls-files`
  were read-only and are what the verification is built on. (The repo-global stash hazard across worktrees
  makes this rule load-bearing, not ceremonial.)
- **Did not write outside `agents/backlog/**`.** Not `agents/knowledge/**`, not `agents/backlog/adr/**`, not
  `agents/tools/**` — **which is why the proposed script in D3 was specified and not built.** No production
  code, no tests, no `CLAUDE.md`.
- **Did not open** `.env`, `.p8`, `Info.plist`, `project.yml` or anything under `src/cleansia_ios/Config/`.
- **Did not close T-0522.** It is `in_review` with a real `manual_steps: ef-migration`. Its AC are all
  checked, but a ticket whose code path is down pending a migration is not `done`.
- **Did not reconcile T-0473 / T-0479 / T-0490** — the three rows candidate 1 surfaced. They are `draft`
  with written review verdicts and at least one (T-0490) is verifiably shipped. They were **out of this
  pass's brief**, and claiming coverage this pass does not have would reproduce the defect it exists to fix.
  **They are the obvious next pass, and they are now named rather than latent.**
- **Did not create T-0546 / T-0547** (the ADR-0042 challenger round and refactor). ADR-0042 is `proposed`,
  and `deliberation.md` says only a finalized decision becomes a ticket. The ADR proposes those ids; the PM
  allocates them when the round is convened.
- **Did not move `Q-PAYOUT-02` / `Q-PAYOUT-03` to `answered.md`.** They are answered (T-0522 AC0 records the
  owner's verbatim words) but `questions/open.md` is a shared file with live lanes.

---

# ADDENDUM E — the filing pass (2026-08-05): eleven rows for work that was never in the backlog

**Adds to ADDENDUM D; supersedes nothing in it except the T-0537 correction below.**

## E1. The headline, and it is not a ticket

**ADR-0033 is `accepted` and NOT IN FORCE, and the reason is a filing failure.** The ADR named its own
enforcement (Block D → **FT-11**) as *its condition of acceptance*. That condition was never filed as a
ticket, so it was never scheduled, so it never landed — and the ADR's §Consequences states in the
present tense that the check *"moves"*. Verified before filing: `.claude/agents/reviewer.md:105-110`
still teaches the **superseded** routing axis verbatim, `agents/knowledge/conventions.md:122-127` teaches
it too (the first adversarial round measured only the reviewer's page), and `INDEX.md` carried **no row**
for FT-11, FT-12 or FT-8.

By ADR-0032's own definition that makes ADR-0033 `(guidance — no gate)`: **three routing tests that bind
nothing, for four days, because nobody wrote down that they had to be turned on.** The living doc
(`architecture/decisions/catalog-governance.md:61-76`) already said so. The backlog did not. That gap —
a decision recorded as accepted while its enabling work exists in no queue — is the finding this pass
records, and it is a **process** failure, not an architecture one.

## E2. What was filed — eleven rows, `T-0549`…`T-0559`

| Group | Rows |
|---|---|
| **ADR-0033 remainder** | **T-0549** FT-11 (`ready`, widened to *both* pages) · **T-0550** FT-12 (`ready`) · **T-0551** FT-8 (⛔ `blocked` **twice**) · **T-0552** F1, the signed erratum on ADR-0032 (`ready`) · **T-0553** the L1/L3/F4 panel (`in_progress`) |
| **Nx guard, pinned but unfixed** | **T-0554** three dangling `tsconfig.base.json` aliases · **T-0555** `libs/cleansia` invisible to Nx |
| **Landed-during-the-pass lanes** | **T-0556** `SaveMyDocuments` unbounded upload (**security**) · **T-0557** host-level request-body limit (**architect**) · **T-0558** two dead upload commands · **T-0559** the object-literal remainder + the lint-regex call (`draft`) |

**Three filings are deliberately not `ready`:**

- **T-0551 is `blocked`, not `ready`** — Block C **as specified in the accepted ADR** appends the new
  routing test and never amends `conventions.md:122-127`, whose first limb the floor *reverses*. Applied
  literally, one page instructs both. A `ready` row would have invited exactly that.
- **T-0553 is `in_progress` and was filed after it spawned** — recorded that way rather than backdated,
  because an in-flight panel that blocks two filed tickets and appears in no row is the same defect as
  FT-11's.
- **T-0559 is `draft`** — its architect ruling does not exist yet, and its counts must be re-derived
  rather than inherited.

## E3. Three backlog-integrity corrections found while filing

| | |
|---|---|
| **T-0546**, **T-0548** | Both existed as ticket **files on disk with no `INDEX.md` row.** Rows added (`draft` / `in_review`). A ticket file with no row is invisible to every mechanism this sprint built |
| **T-0537** | **ADDENDUM D's row is stale.** It says `ready` because *"the guard does not exist"* — the guard shipped in **`e78fb619`** with its own repo-root workflow, and the ticket file now reads `done` with AC1–AC5 ticked. **Corrected to `done` ⚠️ (unreviewed):** its `## Review` is still the empty template, so per `ticket-lifecycle.md:165-179` it owes a reviewer pass or a MANUAL-GATE block |

**The pattern under all three:** ADDENDUM D measured tickets that were stale *against the code*. These
are stale *against each other* — file vs. index — which no candidate in §D3 detects, because all three
candidates read tickets and commits, never the manifest.

## E4. §D3's mechanism, applied — including where it does not reach

Both measured corrections were applied when writing these eleven tickets:

- **Every ticket names the specific product files it touches**, because that is the only signal shown to
  work (candidate 3, resolved by suffix match against `git ls-files`).
- **Five of the eleven are structurally undetectable by it.** T-0549/0550/0551/0552/0553 touch only
  `.claude/agents/**`, `agents/knowledge/**`, `agents/process/**`, `agents/architecture/**` and
  `agents/backlog/adr/**` — and the middle three are *deliberately excluded* from the path rule, because
  counting shared knowledge docs takes it from **11 flags to 29** on this corpus. So each of those five
  carries a **`### Staleness detectability`** section naming the one hand-check that substitutes for the
  script. **This is a real hole in the proposed mechanism: the governance backlog — the part that went
  unfiled in the first place — is exactly the part the script cannot see.**
- **Candidate 1's inverse earned its keep.** Its recall gap is proven (T-0448/T-0450), but reading it
  backwards — *a `done`/`in_review` ticket with an empty `## Review`* — is what surfaced T-0537 and
  T-0548 above. Worth adding to the script as a second rule; it costs nothing extra.

## E5. 🔴 OWNER — what this pass puts on your desk

**Nothing blocking, and one thing worth knowing.**

1. **T-0556 is the one to look at.** `SaveMyDocuments` accepts an unbounded, unchecked upload on **two
   partner hosts**, and the content type is taken from the file **extension**. It is the same defect just
   fixed on the avatar path, in the same feature area, and it is still open.
2. **ADR-0033's routing rule is not running.** Until T-0549 lands, catalog edits route by the
   *superseded* axis on both the author's and the reviewer's page. Nothing regresses meanwhile — the old
   axis is what everyone has been applying all along — but no one should cite ADR-0033 as binding.
3. **T-0557 needs an architect panel before any code**, and it will produce a platform-wide number
   (the request-body ceiling) that touches every intake path on all five hosts.

## E6. What this pass deliberately did NOT do

- **Did not touch git.** No add, commit, branch, stash, checkout or restore. `git log` / `git show` /
  `git status` were read-only.
- **Did not edit `.claude/agents/*.md`.** T-0549 *specifies* the reviewer-charter edit; it does not
  perform it. That separation is part of why the finding sat unfiled — the round that found it could not
  perform the fix either.
- **Did not edit any live lane's ticket file.** T-0535, T-0447, T-0465 and T-0537 were read and left
  alone; T-0559 was filed as T-0535's *remainder* rather than as an edit to it. The single exception is
  **T-0546**, which is committed and clean: additive only — four full paths and a status-log line, **no
  AC and no status changed**.
- **Did not write the ADR-0033 repairs, the erratum, or any ADR.** Those belong to the architect and to
  T-0553's panel; a PM inventing the repair is the exact defect T-0471 exists to correct.
- **Did not reconcile T-0447 / T-0465 / T-0473 / T-0479 / T-0490.** T-0447 says `ready`/`updated: 2026-08-01`
  while `6bd3b0c6` shipped work naming it — a candidate-3 flag, out of this pass's brief, **named here
  rather than left latent.**
- **Did not build the §D3 staleness script.** Still specified, still unbuilt, and E4 now records a hole
  in its coverage that its spec should absorb before anyone writes it.
