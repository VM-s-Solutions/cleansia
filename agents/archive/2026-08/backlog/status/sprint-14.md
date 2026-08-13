# Sprint 14 — owner batch, demo preparation

**Baseline now:** `master` at **`1c8fdd00`** — **all six wave-2 PRs are merged** (moved 2026-08-01;
was `0f3b0d4c`, then `ce2416a0`, planning baseline `bbcf5b24`). Working tree clean apart from a
pre-existing `src/cleansia_ios/Cleansia.xcworkspace/.../Package.resolved`; **all worktrees removed,
all local branches deleted** (`git worktree list` → main checkout only).
**Updated:** 2026-08-01 — **FOURTH PM pass: post-merge reconciliation.** (Third pass, 2026-07-30: the
T-0446 security gate, the ADR-0031 panel, QA's AC4 run, the T-0441 review.)
**Input:** one owner batch of 5 items + 1 approved process change → 12 tickets; **+ 7** from wave-1
findings; **+ 4** from the security gate; **+ 3** from the ADR-0031 panel; **+ 5** from QA and the
T-0440/T-0441 reviews; **+ 2** filed at the wave-2 close-out. **34 total.**

> ## 🟩 READ §9 FIRST — it is the current state. Then §8. Everything before §8 predates the merges.
>
> **§9 (2026-08-01, fifth pass):** `Q-I18N-02` is **ANSWERED** — the last `blocking: yes` question in
> the backlog is closed and **the owner is off the demo chain**. T-0450 is **split** (label → T-0450
> `ready`; Poppins → **T-0472**, blocks nothing). Two new owner items filed: **T-0473** ("Report an
> issue" → red) and the two approved process fixes **T-0474** + **T-0475**. And one thing to say plainly
> to anyone who asks: **the avatar feature is one-third shipped** — only the read path (T-0446) exists.

> ## 🟢 §8 — the post-merge reconciliation. Everything between here and §8 was written BEFORE the merges.
>
> Six PRs shipped — **`acf2f0bc` #175 · `a63b776e` #176 · `d6969fef` #177 · `1d85b35f` #178 ·
> `a10e1f88` #179 · `1c8fdd00` #180.** The four headlines:
>
> 1. **T-0439, T-0446 and T-0451 are `done`.** **T-0440 and T-0441 are MERGED but deliberately NOT
>    `done`** — they stay `qa` on owed AC screenshots. §8.2 gives the lifecycle text that forced that.
> 2. **The owner's regen bundle is DONE** and shipped inside `a63b776e`. §2.12's "two items" and
>    T-0446's "T-0447/0448/0449 are HELD" are both **discharged**. §8.3.
> 3. **The vacuous consistency green is DEAD** (`d6969fef`). §2.14's "⚠️ that fix is NOT on `master`"
>    and §2.14's `OK (0 files scanned)` transcript are **superseded** — see §8.4 for the two
>    measurements and the precise (not blanket) caveat on pre-#177 evidence.
> 4. ~~**Exactly ONE thing now blocks the demo chain, and it is the owner: Q-I18N-02.**~~
>    **⛔ SUPERSEDED BY §9 (2026-08-01) — Q-I18N-02 is ANSWERED. Nothing on the demo chain is waiting
>    on the owner.** T-0450 is split and `ready`; T-0448/T-0449 clear when its write lands.
>
> **Stale statements below that §8 supersedes, listed so nobody re-derives them:** the `0f3b0d4c`
> baseline in this header; "`c23b26e7` and `c9265298` are NOT on `master`" (both are now); §2.14's
> live-`master` consistency transcript; and §2.12/§4's owner regen bundle.

> **From the third pass, still true:** **T-0446's AC4 is CLOSED** (§2.10, and now double-closed by the
> owner's DEV check — §8.3), **§2.12 RETRACTS a false blocker the PM filed against T-0440**, and
> **§2.9 retires the "DEFERRED-TO-CI" caveat** — those suites were never broken.

---

## 0. Read this first — five things that change how you use the backlog

**(a) Wave 1 shipped.** PRs **#170–#174** are merged. **T-0438, T-0442, T-0443, T-0444, T-0445 are
`done`.** Several rows previously said "ready (needs dispatch)" for work that has since shipped; they
are corrected in `INDEX.md` and in each ticket's status log. If you read this document before
2026-07-30's second pass, re-read §2.

**(b) THE OWNER HAS RULED: the avatar feature IS part of the demo.** §3 below was the PM's
recommendation to ship the demo *without* T-0446…T-0449. **It has been overruled.** The argument was
accepted as well-made and the decision went the other way, which is the owner's call to make. §3 is
kept verbatim as the record of what was argued and rejected — it is **not** current guidance.
**T-0446 is now the demo critical path.**

**(c) The INDEX is still stale below the SPRINT-14 block** for anything merged after PR #148.
PRs **#149–#170** shipped outside this process and the owner declined a backfill. Ground yourself in
the **code**, not in those rows.

**(d) `master` frontend CI is GREEN again** — T-0438 landed as `7c82cd2e`. The red-build caveat in the
first pass of this document no longer applies; build evidence produced from `ce2416a0` is honest.

**(e) The T-0446 security gate has returned `APPROVE-WITH-CONDITIONS`** — see the new **§2.5**. The
headline: **no live vulnerability, the demo is not at risk from this feature**, but the gate found one
control in the diff that never executes, one pre-existing PII leak that is probably the largest of its
class in the codebase, and a gap in the security rule set itself. **Two findings folded into T-0446,
four tickets filed out of it (T-0457…T-0460).**

---

## 1. What the PM verified itself in the planning pass (kept — still the grounding for the tickets)

Per the Gate 8 verify-not-trust posture, and because the batch brief is itself a report:

| Claim | Verdict | Evidence |
|---|---|---|
| Two web apps break on the regen | **WRONG — all three do** | `nx build … --configuration=production --skip-nx-cache` on `bbcf5b24`: customer **exit 1** (4 errors), partner **exit 1**, admin **exit 1**. Admin fails via the shared `libs/data-access/partner-stores/src/lib/user/user.effects.ts:96`. |
| `ICreateOrderCommand.accessInstructions` is required | Confirmed | `customer-client.ts:7379` — `accessInstructions: string \| undefined` is a **required key** (not `?:`). |
| `IUpdateCurrentUserCommand.removePhoto` is required | Confirmed | `customer-client.ts:12879`, `partner-client.ts:12860` — `removePhoto: boolean`. |
| The web wizard drops the entry instructions | Confirmed — **live data-loss bug** | Collected `order-wizard.component.html:491-492` → held `order-wizard.models.ts:48` → displayed back `wizard-summary-step.component.ts:240` → **absent** from `new CreateOrderCommand({…})` at `order-wizard.facade.ts:551`. **Now closed** — see §2. |
| Backend cap is 2000 | Confirmed | `Features/Orders/CreateOrder.cs:136-138`, `.MaximumLength(2000)`. |
| "`order_detail_access_instructions` already exists ×5" | Confirmed **and materially incomplete** | Those are **display** labels. The booking-confirm **input hint does not exist** on either mobile platform. A new key ×5 **is** required. |
| Mobile client regen is owner-only | **No — it is not** | Kotlin generates at Gradle build time; Swift via `scripts/generate-api-clients.sh` (`ios-ci.yml:126`). T-0440/T-0441 are **not** owner-blocked. |
| — (found while verifying) | **The local iOS client is STALE** | `CleansiaCustomerApi/Models/CreateOrderCommand.swift` has neither `specialInstructions` nor `accessInstructions` though the spec has both. Trap note in T-0440/T-0449. |
| No client can render an avatar | Confirmed | `Mappers/UserMappers.cs:41` → `Mappers/BlobMappers.cs:12-18` returns `BlobFileDto(FileName: <guid>, Base64Content: null, ContentType: null)`. |
| The web partner avatar path is DEAD CODE | Confirmed — **and still true at `ce2416a0`** | `updateUserCurrent` is **never dispatched** by any component in any app. Re-verified 2026-07-30 post-#171. Not a reference for T-0447. |
| The Android profile header complaint | Confirmed, one structural cause | iOS `HeroGradient` is ONE `HStack` with the chip `.frame(maxHeight: .infinity, alignment: .center)`; Android stacked a second row below a `Spacer(16.dp)`. **Now fixed** — T-0442. |
| The two Android apps don't share a mark | Confirmed | Different hand-drawn vectors, different wrappers. **Now re-cut** — T-0443. |
| Every web app serves a PNG as `.webp` | Confirmed | All three `Logo.webp` shared sha1 `365adf5963`, which `file(1)` called *"PNG image data, 48 x 48"*. **Now fixed** — T-0444. |

---

## 2. Wave 1 — what actually shipped, and what the PM re-verified

Five tickets `done`. Gate evidence (suites, mutation proofs, builds) is **as reported in the PR
bodies**; the PM did not re-run the suites. What the PM *did* re-verify, in the tree at `ce2416a0`:

| Ticket | Merged | PM re-verification (own read of the code, not the report) |
|---|---|---|
| **T-0438** | `7c82cd2e` #171 | `order-wizard.facade.ts:589` now reads `accessInstructions: data.entryInstructions.trim() \|\| undefined`. The data-loss path is closed at the exact call site the ticket named. |
| **T-0442** | `ce2416a0` #174 | `ProfileTab.kt:271-318` is ONE `Row(verticalAlignment = Alignment.Top)`; the chip carries `.align(Alignment.CenterVertically)` at `:315`. Matches `ProfileTab.swift:296-303`. |
| **T-0443** | `10d03f14` #173 | Both `ic_launcher_foreground.xml` re-cut to the same 10-line/108dp structure (were 15 and 24 lines, different shapes); `ic_notification.xml` now **byte-identical** across apps (sha1 `981999053b21`); partner gained the shared `WordmarkSplash` + a `BrandIconCatalogTest` guard. |
| **T-0444** | `3c27cd5a` #172 | customer/admin `Logo.png` byte-identical (sha1 `b303b295b302`) at **616×112**; partner **616×172** and distinct (sha1 `74c42e6dd5e6`). Every `Logo.webp` is now really `RIFF … Web/P`. Guard at `cleansia-brand-name.component.spec.ts:171-221`. |
| **T-0445** | `8241d3cd` #170 | **Gate 0.5 — Verification integrity** is live at `process/quality-gates.md:52-90`, all three legs plus the against-its-neighbours paragraph. Already being cited: every ticket filed today carries a leg-3 AC. |

**T-0444 is the one whose history matters.** It was **reworked twice by owner ruling**: the monogram
was **overruled** (*"NO, I want the web to use usual 'cleansia' logo that is used in ios apps."*) in
favour of the iOS wordmark; then the owner asked for a **distinct stacked "Cleansia Partner" lockup**
on the partner app, matching the partner iOS app. Both rulings are in the ticket's status log, and
the second is why the partner asset is 616×172 while customer and admin are 616×112.

Also merged in this window and **not ticketed**: the Google sign-in **sub-first** resolution fix
(`8241d3cd`, PR #170), routed outside this process. Recorded so the history is honest.

**Two of T-0443's assertions the PM could NOT confirm** (Gate 0.5 leg 3): whether the two apps'
`ic_launcher_foreground.xml` *should* still differ in path data (sha1 `a836259a` vs `61021817`) —
that may be the deliberate partner-lockup distinction the owner later ruled for the web, or a miss;
and no Android build was run by the PM.

---

## 2.5 The T-0446 security gate — **APPROVE-WITH-CONDITIONS**

Full findings: **`agents/archive/2026-08/backlog/security/user-profile-avatar.md`**. Verdict pasted verbatim into
`tickets/T-0446-…md` `## Review`.

**The headline for the owner: there is no live vulnerability here, and the demo is not at risk from
this feature.** The read path is correctly scoped and no exploit could be constructed against it. Four
things were checked rather than assumed and all four cleared: **no IDOR** (the query carries no
user-identifying field at all — the user comes from the JWT email claim, the blob name is
server-generated, and the inbound `BlobUrl` on the *request* DTO is write-ignored); the grant is
**`sr=b sp=r` for one hour**, asserted against a **real** blob client on **the branch that is actually
deployed**; the container is private on **two independent switches** (`storage.bicep:81` account
kill-switch, `:101` per-container); and — the Gate 5 risk the ticket itself pre-empted — **no SAS is
minted for any paged list**, so the admin user list did not silently become N storage calls per page.

### What the PM verified first-hand before recording any of it

Per Gate 8 (verify-not-trust), applied to a security report the same way §1 applied it to the owner's
batch brief. The PM re-derived every load-bearing number from the code on the T-0446 branch:

| The reviewer said | The PM measured | Effect |
|---|---|---|
| `blobUrl` value starts at index 366–460; shortest SAS ~208 chars | **381–419**; a realistic service SAS floor of **193** chars | Reviewer's range **brackets** the PM's. Same conclusion, wider margin. **Confirmed.** |
| The vacuous test's fixture is **187 bytes** | **335 bytes** | **Corrected in the record.** The finding is *sharper*, not weaker — 335 is still only ~43% of a real ~786-byte response, and its closing quote lands at index 332, inside the 500-byte cut. |
| PII closes by index ~330 | **~264–302** across three representative profiles | **Confirmed**, with more headroom. |
| The fix is at `:100` and `:77-78` | Those are the **`Cleansia.Web.Customer`** lines; the other **four hosts are at `:96` / `:74`** | **Precision correction now in the AC**, so the implementer does not edit four wrong lines. |
| The avatar is the only blob reusing a name | **Confirmed** — `SaveOrderPhotos.cs:120-121`, `UploadOrderPhoto.cs:95-97` and dispute evidence all mint unique names | **Confirmed.** |
| Managed-identity SAS is dead code | **Confirmed** — `AccountUrl` is set in **no** config file; only three references exist in the entire tree | **Confirmed.** |

### Folded INTO T-0446 (they are this ticket's own defects)

- **SEC-1 — the redaction control the diff added never executes on this endpoint.** The middleware
  **truncates before it redacts**, and the regex needs a complete quoted string, so on a profile
  response the closing quote is always past the 500-byte cut. **Redaction fires 0% of the time here.**
  No signature escapes today — but that is the truncation doing it, not the new control. Worse, the
  test that "proves" the control uses a hand-trimmed payload the endpoint never emits: **a Gate 0.5
  leg-1 failure, one ticket after Gate 0.5 shipped.** Filed as **AC9**, including a rebuilt test that
  must go red against the current ordering.
- **SEC-4 — mint a fresh blob name on replace.** Filed as **AC10**. The PM's reasoning is in §2.6.

**Credit where it is due, and it is genuine:** adding `blobUrl` to the regex closes a **real, live,
pre-existing credential leak elsewhere.** On `GetOrderPhotos`, `OrderPhotoDto.BlobUrl` sits at index
**49–346** — inside the 500-byte window — so complete signed URLs **including `sig=`** were being
written to Information-level logs before this diff. Dispute evidence has the same shape. That is an
unplanned win the ticket was never asked for, and it lands as soon as AC9's ordering fix goes in.

### Filed OUT of T-0446 — so a pre-existing problem cannot compress a demo-path ticket

| Ticket | Finding | When |
|---|---|---|
| **T-0457** | **SEC-2** — `GET /api/User/GetCurrent` writes the caller's email, name, phone and birth date to the **Information**-level log on all five hosts, inside the 500-byte window, on **every** request. Probably the **largest S6 exposure in the codebase** — it is the most-called authenticated endpoint on the platform. **Pre-existing; does not block T-0446.** | **PRE-DEMO (P1)** |
| **T-0458** | **SEC-3a** — decide the image-sanitization policy (EXIF strip, size cap, resize) and build the seam, piloted on the avatar. Needs an **architect panel**: library choice is partly a **licence** question, and stripping EXIF naively ships **rotated photos**. | post-demo |
| **T-0459** | **SEC-3b** — apply it to order photos and dispute evidence, **the instances that are already cross-user visible**. | post-demo |
| **T-0460** | **SEC-5** — a **gap in the rule set, not a violation of it**: nothing in S1–S11 covers bytes inside a stored artifact served by URL. Add the rule. | post-demo |

### Sequencing against the demo — explicit

**Pre-demo:**
- **T-0446** — unchanged as the demo critical path, now +AC9 +AC10. Still `in_progress`.
- **T-0457** — P1, **serialized behind T-0446** because they write the same five middleware files. It
  is small (one design call, five identical edits, one `[Theory]`) and it does **not** touch a DTO, so
  it adds **nothing** to the owner's regen bundle. It fits in the shadow of the owner's regen wait.

**After the demo:** T-0458 → T-0459 (in that order — T-0459 depends on T-0458's ADR), and T-0460
ideally **before or alongside** T-0458's panel so the ADR can cite the rule rather than invent it.

**The one hard gate to remember later:** **T-0458/T-0459 must land before any cross-user avatar
display** — a cleaner's face on an assigned order, an avatar column in the admin list. Today the
avatar is self-only, so **T-0446 discloses nobody's EXIF to anyone new**. The moment a second person
can fetch that blob, it becomes a live geolocation disclosure. That gate is recorded on T-0458 and in
the findings doc so a future ticket cannot walk past it.

---

## 2.6 SEC-4 — the PM was asked to weigh it, not rubber-stamp it. The call, and the reasoning

The security reviewer explicitly said *"say plainly if you disagree and why."* **The PM agrees: fold
it into T-0446 (AC10).** The reasoning, in order of weight — and note that the *security* consequence
is **not** what carries it:

1. **The stale-avatar defect is demo-visible, and the owner ruled the avatar IS the demo.** The
   contract comment this diff adds tells clients to cache on `fileName` — which never changes on
   replace. So Coil and Kingfisher render the **old** avatar after a successful upload, indefinitely.
   That ships as *"the new photo didn't upload"* on the single screen the demo exists to show. It is
   the same argument §3 made and the owner accepted as well-made — landing a half-working chain is
   worse than an honest placeholder — applied to a defect rather than to scope.
2. **The "don't touch the write path" objection is already dead.** T-0446's **AC4** reopens
   `UpdateCurrentUser.cs:160-164` for the content-type. AC10 is **one line at `:155`** in the same
   method the developer is already editing. The scope amendment widens a file's role; it does not
   open a file the ticket had closed. *(The PM has recorded the amendment explicitly on the ticket
   rather than silently widening it.)*
3. **Three workarounds versus one line.** Deferring makes T-0447, T-0448 **and** T-0449 each carry a
   cache-eviction workaround — three places to get it wrong, in three languages, none testable
   against the others.
4. **It restores consistency rather than inventing a pattern.** Every other blob in this codebase
   mints a unique name per upload. The avatar is the sole outlier, and the comment justifying it
   (*"so URLs already handed out keep resolving"*) was written when **there was no read path at
   all** — it never had a consumer, and T-0446 gives it one that is actively harmful.

**Stated plainly, so a later reader can disagree with the right argument:** the security consequence
on its own would **not** have justified folding this in. An outstanding SAS resolving to the new image
for ≤1h, with no revocation handle (the SAS is ad-hoc, `BlobContainerClient.cs:90-97` sets no
`Identifier`, so only blob deletion or key rotation invalidate it) is real — but today that SAS only
ever reaches the photo's own owner, so it requires the owner's own credential to have leaked first.
**It is the product defect that carries this decision.**

---

## 2.7 The ADR-0031 panel — accepted; three tickets filed, two reports found understated

**ADR-0031 is accepted.** Two code changes were mandated before **T-0439** merges and are **already
dispatched to its developer** — they are not backlog items. The panel lead handed the PM three
findings. The PM re-grounded each one, and **two of the three were understated**:

| Filed | Finding as reported | What the PM found |
|---|---|---|
| **T-0461** | M3 — pin `strictTemplates: true` per app so T-0439's guard cannot be silently de-fanged | **Confirmed as reported.** All three apps carry it today (`apps/*/tsconfig.json:20`), so the rule pins a correct state rather than fixing a break. Added grounding: libs are **59/65**, the two gaps being `data-access/{admin,partner}-stores` — recorded as a **scoping question**, not a bug to fix in passing. |
| **T-0462** | Stale second admin client · app-unreachable lib file · **two** `CLAUDE.md` corrections | **Understated — there are three corrections, and the new one is the worst.** All **six** documented Nx commands (`CLAUDE.md:84-91`) **fail**: the real project names end in **`.app`** (a dot), not `-app`. |
| **T-0463** | `partner-stores` has no `test` target | **Understated — all three data-access libs are `lint`-only.** `admin-stores` and `customer-stores` have no `test` target either. |

### The Nx-command finding deserves its own line, because the right fix is not the reported one

Six commands documented as working, none of which run. **A developer who does not stop to investigate
could reasonably conclude the build is broken rather than the docs** — and then start "fixing" a build
that was never broken. That is a worse failure than a stale repo map.

But the PM checked whether `docs/architecture/frontend.md` repeats it, as asked, and **found the
opposite: that file is the one that is right.** It advertises **npm aliases**, and all seven exist and
already resolve to the correct dotted names — **and `frontend-ci.yml` invokes those same aliases.** So
there are three sources of truth and **`CLAUDE.md` is the only wrong one**. Mechanically inserting the
dots would have "fixed" it into a **fourth** command style matching neither the docs nor CI.
**T-0462's Correction 3 therefore proposes the npm aliases as primary**, with corrected raw Nx names
as a secondary note. That is a deliberate departure from the reported fix, flagged so the owner can
overrule it.

`docs/architecture/frontend.md`'s one genuine staleness — the `::: info` block omitting
`generate-clients` — **is already in flight with the T-0439 developer and was not duplicated.** One
additional hit was found and deliberately **not** folded in: `agents/tools/wave2-2c-final.workflow.js:95`
also uses the wrong name; it is a live tool file in T-0454/T-0461's lane, so it is noted for separate
filing rather than smuggled into T-0462.

### T-0463 is the one to run first, and the panel lead was right to rank it up

The partner app's NgRx effects are **entirely untested** — including
`libs/data-access/partner-stores/src/lib/user/user.effects.ts`, which **broke in T-0438**, is **one of
the three regen call sites**, and is **being edited right now** for T-0446. **Two of the last three
regen breaks landed in a project with no test target.** That is the mechanism, not a coincidence:
T-0439 is building a guard against regen drift, and the place the drift keeps landing has no suite for
a failure to be caught by. Sized **M** with a deliberate scope bound (AC3: real tests for
`user.effects.ts` only; the other two libs get a working target plus a minimal genuine suite) so it
does not become an `L`.

---

## 2.8 Process working as designed — worth naming so it propagates

The frontend developer on the T-0439 lane **baselined its lint numbers empirically** instead of
asserting "no new violations": it created a **throwaway detached `master` worktree**, ran the same
three targets, diffed normalized output, and **removed the worktree afterwards**. It found its three
projects **byte-identical to master** except one `no-non-null-assertion` warning that moved from line
115 to 119 — which it **correctly attributed to the four lines its own factory adds**.

That is exactly the standard of evidence Gate 0.5 and Gate 8 ask for: a measured differential against
a named baseline, with the one delta explained rather than waved past. It is also the **safe** way to
use a worktree, in the same sprint where the unsafe way (`git stash` in someone else's tree) cost ~50
minutes and produced T-0456.

**The same agent also got a judgement call right by not acting.** It added a catalog harvest to
`patterns-frontend.md` — *"Building a generated DTO — construct-then-assign, never an object
literal"* — but **deliberately did not write the corresponding note into T-0446's `## Review`**,
because the backend developer and a reviewer were both live in that file. Correct: a concurrent write
to a file two agents are editing is precisely the T-0456 class. **The PM has folded that note in now
that the lane has cleared**, and pointed T-0447/0448/0449 and T-0463 AC4 at the harvested section.

---

## 2.9 CORRECTION — the "deferred" suites were never broken. Stop treating them as CI-only

**Run locally on `master` for the first time this sprint:**

| Suite | Result |
|---|---|
| `Cleansia.Tests` | **2295 / 2295 passed** |
| `Cleansia.IntegrationTests` | **108 / 108 passed** |
| `Cleansia.HostTests` | **75 / 75 passed** |
| all three | **~5m30s** |

**The `75 failed / 0 passed` a prior agent reported was `DockerUnavailableException` at fixture
creation** — not 75 failing tests. **75 is the true test count and all 75 pass.**

**Every "DEFERRED-TO-CI / UNVERIFIED-LOCALLY" note this sprint was an environment problem, not a suite
problem.** That caveat is now **retired as a default**. A ticket may still declare it — but only after
actually attempting the run and finding Docker unavailable **in that environment**, and it must say so
in those words rather than inheriting the phrase. T-0464 AC7 and T-0466 AC3 both carry this
explicitly, and T-0466 records the expected counts so a differing total is investigated rather than
reported as a pass.

This also removes an excuse: **the integration and host suites are cheap enough to run** (~5m30s for
all three, including the unit suite). There is no longer a cost argument for skipping them.

---

## 2.10 QA closed T-0446 AC4 — the last open unknown on the demo critical path

**AC4 PASSES, and the prediction that it would was right.** Executed against Azurite using the app's
**own** `BlobContainerClientFactory` and the **real** `UpdateCurrentUser.Handler` (only the two
repositories mocked), with real image files from this repo:

- **Chromium 140** rendered the JPEG at **800×600** and the PNG at **48×48** from a bare-GUID URL
  served as `Content-Type: application/octet-stream`.
- **WebKit** (Safari's engine) did the same.
- **`CGImageSource`** — what `UIImage(data:)`, Kingfisher and SDWebImage all sit on — sniffed
  `public.jpeg` and `public.png` from the bytes.
- `X-Content-Type-Options: nosniff` **absent**; SAS fetch **200** with sha256-identical content on
  both files; wire grant exactly `sr=b`, `sp=r`, 1h; container-list with the same token → **403**.

**Consequence: no content-type work is needed in T-0446.** The header defect is real but pre-existing
and codebase-wide — **T-0464**.

### ⏱️ A one-minute check only the owner can do (NOT a ticket, NOT a blocker)

**The one honest threat to the result above: real Azure was not reachable.** QA had no credentials and
**correctly declined to obtain any** — that is the right call and is recorded as a strength, not a gap.
**If real Azure sends `nosniff`, every render above breaks.**

The corroboration is strong but is **inference from a shipped feature**: order photos already travel
this identical `application/octet-stream` path and are already bound to `<img [src]>` on DEV today
(`order-photos.component.html:125`, `:207`).

**You can kill this caveat in one minute: open a partner or admin order detail on DEV and see whether
the before/after photos render.** If they do, the inference is confirmed against real Azure and the
caveat is closed. If they do not, tell us — it changes T-0446, all three client tickets, and makes
**T-0464** demo-blocking rather than post-demo.

### Three tickets were reworded BEFORE anyone starts them

QA's run invalidated guidance that would have been unimplementable:

- **The "403 = expiry, 404 = deleted" rule.** The codes are genuinely correct (QA confirmed 403 on
  expiry, tampered `sig` and missing SAS; 404 on a deleted blob) — **but an `<img>` tag cannot see
  either.** Chromium surfaces a bare `error` event with the 403 body eaten by ORB
  (`net::ERR_BLOCKED_BY_ORB`); WebKit behaves the same. Three clients would have been given an AC they
  **cannot implement**. All three now say: **on any image error, re-fetch the profile once, with a
  single-retry guard** so a deleted blob does not loop.
  > **Record correction:** this rule was reported as having been previously written into
  > T-0447/0448/0449. **It never was** — a grep for `403`/`404` across all three returns nothing. There
  > was nothing to reword; the correct guidance has now been added for the first time. Noted so nobody
  > goes looking for a prior version that does not exist.
- **T-0447 has a hard constraint nobody had scoped: CORS.** `<img src>` loads, but `fetch()` throws
  `TypeError`, `<img crossorigin="anonymous">` errors on the missing `Access-Control-Allow-Origin`,
  `fetch(mode:'no-cors')` returns an opaque response, canvas `getImageData` is blocked, and
  `HttpClient.get(blobUrl, {responseType:'blob'})` fails. `storage.bicep` has **no `cors` block**
  (PM-verified), so **real Azure is in the same state.** **Any crop-or-canvas design that reads the
  stored avatar is dead** and cannot be fixed client-side — it needs a storage-account CORS rule,
  i.e. a deploy change. The ticket now says so, with the working precedent (`[src]` binding) named,
  and preserves the distinction that a **pre-upload preview of the locally picked file is still fine**
  (`staged.preview`, `order-photos.component.html:168`/`:254`).
- **T-0448 now carries a mandatory device run.** Android is the one platform QA could not execute — no
  emulator, and `BitmapFactory` is not exercisable on the JVM. The static trace found **no `image/*`
  MIME literal anywhere in Coil 3.0.4**, so there is no MIME gate to fail — **but that is a trace, not
  a run**, and the other two platforms were executed against real engines. It must not close by
  inference from them.

---

## 2.11 T-0441 APPROVED, and two process items worth keeping

**T-0441 → `qa`.** 321/321 tests, **53/53 Gradle tasks executed** (not up-to-date/cached), no new
consistency violations, both review findings closed **and independently re-proved**. **AC1's
screenshot is the only open item and it is QA's**, correctly deferred — not a reviewer gap. The
`values-*/strings.xml` lane is now **clear for T-0450**.

**(a) A cache can serve the mutation proof itself — and Gate 0.5 does not name it.** The reviewer
caught its own evidence being served from the Gradle build cache **mid-mutation** and re-ran with
`--no-build-cache`, because *"it still compiles"* was the **load-bearing half** of the finding and a
cache-served compile does not establish it.

The sharp part, and the reason leg 2's general *"a cached run is not a run"* did not prevent it:
**a mutation that reproduces a previous mutation byte-for-byte will legitimately hit cache.** The
build system is behaving **correctly** — the inputs really are identical to something already built.
And mutation proofs are **repetitive by design**, so the second flip of the same flag is byte-identical
to the first. Filed as **T-0468**, with the panel asked for the **flag per stack** (Gradle, Nx, dotnet,
Xcode, Jest) rather than a Gradle-only rule — the same class-not-commands argument T-0456 makes.

**(b) An Android ticket wrote a sentence about iOS, and the reviewer handled it exactly right.**
T-0441's `patterns-mobile.md` harvest closes with *"iOS mirrors this — its generated models have the
same all-optional shape."* The reviewer **verified the claim is factually true**
(`CreateOrderCommand.swift:15-32`, every property optional) but noted it is an **Android-layer ticket
writing toward a stack it never executed**, and let it stand as **descriptive, not prescriptive**.
That is the right resolution — neither deleting a true statement nor promoting an unexecuted one.
**Routed to the Architect to confirm or promote once T-0440 lands with its own iOS evidence**, and
recorded on both T-0441 and T-0440 so it survives their close-out.

### The PM lane ruling the reviewer correctly escalated

`agents/knowledge/patterns-mobile.md` is **not** in `process/shared-file-lanes.md`, which enumerates
only `consistency.md`, `INDEX.md`, the 15 i18n bundles, the Policy trio and root `CLAUDE.md`
(PM-verified, `:19-23`). The reviewer confirmed T-0441's lane reasoning was correct **by the letter of
the list** and flagged that the call is not a reviewer's to make.

**Ruling: yes, it joins — and as the whole `patterns-*.md` family, not one file.** Because:
the table's own rationale for `consistency.md` (*"every ticket appends its note; two concurrent
writers destroy each other's hunks"*) applies **verbatim**; **three of these files were written this
sprint by three different tickets** (`patterns-mobile.md`/T-0441, `patterns-backend.md`/T-0446,
`patterns-frontend.md`/T-0439's dev), so the class is already live; **T-0440 is the scheduled next
collision** and is currently averted only by *having been told not to re-harvest*, which is exactly the
fragile mechanism a lane table replaces; and naming one file while leaving its three siblings out
would repeat the enumerate-instances-not-the-class bug that T-0456 exists to fix.

**Serialize per file** (the four are independent, as the i18n bundles serialize per app). Recorded in
`INDEX.md`'s lane list **effective now**; the durable `shared-file-lanes.md` edit is routed through
**T-0456**, already that file's sole writer — **extended, not forked**.

---

## 2.12 ❌ RETRACTION — the `ios-client-regen` blocker was FALSE, and the PM filed it on relay

**§2.11 and the previous version of the regen bundle claimed T-0440 needed an owner regen. That was
wrong. The owner's bundle is back to TWO items.** This section is kept rather than deleted because an
agent reading the old note — or repeating the same read — will re-derive the same false blocker.

**The claim:** iOS's generated models are committed, `accessInstructions` is absent from
`CreateOrderCommand.swift`, therefore the owner must regenerate.

**Refuted on four independent checks, all run by the PM this time:**

| Check | Result |
|---|---|
| `git ls-files src/cleansia_ios/CleansiaCustomerApi` | **0 files — never committed** |
| `src/cleansia_ios/.gitignore` | ignores `CleansiaCustomerApi/` **and** `CleansiaPartnerApi/`, under *"openapi-generator output — machine-owned, never committed, never hand-edited"* |
| local `CreateOrderCommand.swift` | dated **Jul 25 22:35** — generated **before** the spec gained the field |
| committed spec at `HEAD` | `accessInstructions` **present** in `src/cleansia_android/openapi/customer-mobile-api.json` |

Regenerating it is `./scripts/generate-api-clients.sh` — **offline codegen from the committed spec,
authorised for agents**, not the owner-only NSwag step. The developer ran it, got the field at
`CreateOrderCommand.swift:34`, and **694 tests compile and pass**.

### The PM's error, stated plainly, because the lesson is the reusable part

I read the file, correctly observed the field was absent, and stamped it **"PM-verified"**. The read
was accurate. **The artifact was not repo truth.** I checked *existence and content* and never checked
*tracked status* — **one `git ls-files` would have caught it.**

**Reading an untracked, gitignored build artifact and reporting it as a repo fact is not
verification.** Any claim about "what the repo contains" has to be grounded in a `git`-tracked path.
I have been applying verify-not-trust to *other agents' reports* all sprint and this is the one place
I applied it to the wrong object.

**The sharpest part: T-0440 already documented this exact trap, at its own lines 34-39** — *"The
working copy on this machine is STALE… Run `./scripts/generate-api-clients.sh` before starting, or the
field will appear not to exist."* The ticket was right, and I added a block contradicting it. Both the
ticket and `INDEX.md` now carry the retraction inline, and T-0440 points readers at lines 34-39.

**Net: nothing about T-0440 is owner-gated.** It proceeds in full, including the `Localizable.xcstrings`
lane head that T-0450 and T-0449 wait on.

### The owner's bundle, corrected

**Two** items — `nswag-regen` (TS clients) and `mobile-spec-redump` — **plus** the
`markOptionalProperties` scratch experiment (§6 item 8) and the **two exit-code unknowns the architect
folded in**: `npx nswag run`'s failure exit code is unknown (a generator exiting 0 on a failed
generation would let `generate-*-client` typecheck a **stale** tree and report success), and all three
`*-client-formatter.sh` scripts lack `set -e` and end in an `echo`, so each **always exits 0**.
**Do not read a green `generate-*-client` as proof the client regenerated.**

---

## 2.13 Two findings from T-0440, filed as T-0469

Both land on the booking confirm-step instruction fields, so they are **one ticket**, not two.

**A — Swift's `String.count` is grapheme-based, so the iOS cap is MORE PERMISSIVE than the backend.**
The backend's `MaximumLength(2000)` counts **UTF-16 code units**; Swift counts **grapheme clusters**.
One emoji-with-modifier or ZWJ sequence is many UTF-16 units, so a naive iOS cap **passes input the
server rejects** — and the user gets a bare 400 with no field-level feedback. **Kotlin's `.take(2000)`
gets the right property for free.** Genuinely iOS-only, and **not** covered by T-0441's catalog hunk.

**Reported, not written — and that was correct.** The developer was told not to touch
`patterns-mobile.md` under the lane and **complied** (`git diff --stat -- agents/` empty). T-0469 AC2
sequences the write behind T-0440 on that lane. Routing note: an architect is **already ruling on iOS
catalog laws** — this goes to that panel, not a second one, and it should decide whether the rule is
iOS-specific or the general *"client-side length caps must count the same units the server counts"*
(which would also catch a future JS `.length` mistake on web).

**B — the platforms now disagree on capping `specialInstructions`.** T-0440 capped it on iOS;
**T-0441's reviewer explicitly ruled the opposite on Android.** Both are defensible in isolation — the
reasons genuinely differ, since iOS generalized the shared component so a cap there was *new
behaviour* rather than a move — **but two reviewers ruled opposite ways on the same field in the same
flow**, which is exactly the signal that it belongs above either. T-0469 AC3 settles it across all
three clients, and AC5 extends the ruling to `accessInstructions` so the pair cannot diverge again.

**Also from T-0440:** ready-made ru/uk/cs/sk/en parity wording for the Android hint, recorded on
T-0441 as **an offer, not a change request** — with a warning that adopting it **reopens the
`values-*/strings.xml` lane** that was just declared clear for T-0450.

**And a ticket correction:** T-0440's i18n note said to reuse `L10n.OrderDetail.accessInstructions`.
That key exists but is the partner/detail **display** label; the confirm-step hint genuinely did not
exist and is `booking_access_instructions_hint`. Corrected in place so the next reader does not chase
it.

---

## 2.14 T-0440 APPROVED → `qa`, and F-4's vacuous-green — with one correction to the report

**T-0440 is APPROVED.** The verdict is committed as **`c23b26e7`**. **⚠️ PM correction: that commit is
on `fix/tooling-false-green-and-broken-docs`, NOT on `master`** — verified with
`git merge-base --is-ancestor`. Nobody should go looking for it in `master`'s history yet.

**Three items remain open before `done`:**

1. **F-3 — test-first ordering is unverifiable from the artifact** (one squashed commit, no `red→green`
   status-log entry). The reviewer **explicitly declined to assert the tests were written after the
   fact**, and noted its own mutations substantively cover what TDD protects against here. So this is
   a **traceability gap, not a quality finding**. The **developer records the ordering**; if it was
   implementation-first, **that becomes a real Gate 6 question and the reviewer re-reviews.** Recorded
   this way deliberately — the record is only worth having if it is allowed to come back wrong.
2. **AC1 screenshot** and **3. the Gate 8.5 render leg** — both at QA, both reachable only by driving
   the real app on a **16.4 device**. **This is a genuine handoff, not a deferral:** the reviewer
   **proved** neither is capturable in-suite by hosting the field in a **real window** and capturing
   through **two independent mechanisms** — both blank. Same shape as T-0441's screenshot handoff.
   While QA is on the device: does the **ru/uk two-line placeholder** read as intentional beside the
   sibling's one line?

### F-4 — the vacuous consistency gate. Recorded, NOT ticketed — but the fix is not live yet

The reviewer found that `check-consistency.mjs` has **no Swift coverage**, so every `layers: [ios]`
ticket has been recording a **vacuous green** on Gate 8's consistency leg.

**The *silent* half is fixed on a branch** (`c9265298`): an explicit `--paths` matching nothing now
exits **1** with `NOT RUN`, and **absolute paths resolve** — they previously joined onto the repo root
and printed `OK (0 files scanned)`.

**⚠️ PM correction — that fix is NOT on `master`.** It sits on the same unmerged
`fix/tooling-false-green-and-broken-docs` branch. Verified on `master` at `0f3b0d4c` just now:

```
$ node agents/tools/check-consistency.mjs --paths=src/cleansia_ios/CleansiaCustomer
consistency: OK (0 files scanned, stacks: backend, frontend, mobile)   → exit 0
```

**So the vacuous green is still live for anyone working from `master` today.** Treat any
consistency-leg evidence produced from `master` as unproven until that branch merges.

**The retroactive part deserves naming:** the absolute-path bug meant the gate **passed without
reading a file for every agent that followed the absolute-path instruction** — which is what this
backlog's own tickets instruct. That is not iOS-specific; it would have produced a false green on
**any** stack. Consistency-leg evidence recorded across this sprint should be read with that in mind.

**Do NOT file an "add Swift to the walker" ticket.** That is **ADR-0032's** call — it rules that iOS
enforcement belongs in a **SwiftLint `custom_rule` or an XCTest guard, never the walker** — and a
challenger is on it now. Filing one would pre-empt the panel.

### Two smaller items, both recorded as "do not do the wrong thing later"

- **The "hint no longer than its sibling" constraint is REFUTED for iOS.** Android's float label
  ellipsizes; **iOS's hint is plain wrapping text with no line limit in a container with ample
  headroom** — an Android-shaped rule generalized to a platform whose premise fails. **Must not be
  carried into T-0449 or T-0450.** PM-verified neither carries it today, so both received a
  **"do not add"** note — prevention, not removal.
- **The `uk` string ships a typographic apostrophe (U+2019), and that form is CORRECT.** **The PM's own
  parity table in T-0441 had the straight ASCII `'` (U+0027)** — i.e. the artifact most likely to cause
  someone to "fix" it backwards was one I wrote. Corrected in place, annotated with the codepoint, and
  flagged so nobody reverses it.

---

## 3. SUPERSEDED — the PM's demo-scope recommendation (kept as the record; **the owner ruled against it**)

> The orchestrator asked for a reasoned position. **Recommendation: ship the demo without T-0446…T-0449.**
>
> 1. **It is invisible until the last brick.** T-0446 is the read path; until it lands, every client
>    ticket renders the same initials circle the app already shows. Landing T-0448 alone would let a user
>    upload a photo and then not see it — strictly worse than today's honest placeholder.
> 2. **An owner-only handoff sits in the middle of the chain.** T-0446 changes the profile DTO, so the
>    three TS clients **and** both mobile specs must be regenerated by the owner before T-0447/0448/0449
>    can even compile. That is a hard stop of unknown duration inside the critical path — and, per
>    T-0438, the step immediately after a regen has a demonstrated failure history.
> 3. **The security surface is real, not nominal.** All four tickets are `security_touching`. T-0446
>    exposes a blob to a client for the first time on this path; AC4 already surfaces two unverified
>    assumptions. EXIF geolocation on user-uploaded avatars is an open privacy question.
> 4. **The demo does not need it.** Items 3 and 4 make the app *look* finished; item 5 makes it *do* one
>    more thing. For a demo, the first is worth more per unit of effort by a wide margin.

**THE OWNER RULED: the avatar feature IS part of the demo.** The re-sequencing is done and is in
force (§4). Two of the four points above do not go away just because the conclusion did — **point 2**
(the owner-run regen sits inside the critical path) and **point 3** (four `security_touching` tickets
and an open EXIF-privacy question) are now **risks being carried**, not arguments being made. They are
re-surfaced as escalations in §6 rather than dropped.

---

## 4. Sequencing in force (post-ruling)

**Critical path — the demo.**
```
T-0446 (backend read path, in flight)
   └─> [OWNER: nswag-regen + mobile-spec-redump — ONE bundle, do not interleave]
          ├─> T-0447  (web)
          ├─> T-0448  (android)   ← also behind T-0441, T-0450
          └─> T-0449  (ios)       ← also behind T-0440, T-0451, T-0450
```

**Running alongside, not on the path:** T-0439 (regen guard), T-0440, T-0441 — all dispatched.

**New tickets, ordered by what they unblock rather than by size:**

| Order | Ticket | Why here |
|---|---|---|
| now, parallel | **T-0451** (iOS initials contrast) | The only new ticket that is `ready` with **zero** dependencies, and it is a lane head for two demo tickets (`ProfileTab.swift`). Running it first costs nothing and clears the way. It is also a live accessibility failure on a shipped screen. |
| now, parallel | **T-0452** (og:image + apple-touch-icon + manifest) | Demo-adjacent and independent: the owner will share a link. Zero collision with any lane. Needs one short architect panel. |
| after T-0440/T-0441/T-0451 | **T-0450** (ru/uk label + Poppins Cyrillic) | Sits in **four** lanes at once (Android strings, `ProfileTab.kt`, iOS xcstrings, `ProfileTab.swift`) and is a head for T-0448 **and** T-0449. It has to go before them, and it cannot go before T-0440/T-0441 without a collision. ~~Also blocked on a **native-speaker answer** (Q-I18N-02).~~ **SUPERSEDED §9: Q-I18N-02 answered, T-0450 split (Poppins → T-0472) and now `ready`; all four lane heads merged.** |
| after T-0448 | **T-0453** (Android edge-to-edge hero) | Deliberately **behind** the demo path. It shares `ProfileTab.kt` with T-0448, and inserting a non-demo restructure in front of a demo ticket is exactly the wrong trade after the owner's ruling. Doing it after T-0448 also means restructuring against the final hero, not a placeholder. |
| post-demo | **T-0454** (weight-starvation rule) | Prevents the *next* occurrence of a class already fixed. Same reasoning that put T-0439 behind the wave it guards. |
| post-demo | **T-0455** (circular deps) | Zero user-visible change; buys back a checker nobody reads. Sole writer across four libs, so it wants a quiet window, not a busy one. |
| post-demo | **T-0456** (stash/worktree rule) | Cheap, and the incident is fresh — the value of writing an incident down decays fast. |
| **pre-demo, behind T-0446** | **T-0457** (S6 PII in `GetCurrent` logs) | **P1.** Same five middleware files as T-0446 AC9, so it cannot run in parallel — but it is small, touches **no DTO** (so it adds nothing to the owner's regen bundle), and fits in the shadow of the regen wait. Pre-demo because **the demo will be logging real people's data**. |
| post-demo | **T-0458** (sanitizer decision + seam) | Needs an architect panel and a new third-party dependency. **Hard precondition for cross-user avatar display**, which does not exist yet. |
| post-demo, after T-0458 | **T-0459** (apply to order photos + dispute evidence) | Bound by T-0458's ADR. These are the **already cross-user visible** instances — the higher real-world risk of the two, and the reason SEC-3 is not a nit. |
| post-demo | **T-0460** (the missing security rule) | Ideally **before or alongside T-0458's panel** so the ADR cites the rule rather than inventing it. Deliberately given no `depends_on` in either direction so the two cannot deadlock. |
| **post-demo, FIRST of the ADR-0031 three** | **T-0463** (data-access libs have no `test` target) | The highest-value item in the group and the only one that changes what CI can catch. Lane-contested with T-0455 and T-0446's frontend leg on `partner-stores` — **safe order T-0446 → T-0455 → T-0463**. No dependency; it needs a dispatch slot, not a predecessor. |
| post-demo, after T-0439 | **T-0461** (`strictTemplates` consistency rule) | Pins the coverage of a guard that has not merged and still has two ADR-mandated changes outstanding — premature before T-0439. Also lane-blocked behind **T-0454** on **both** files it writes. |
| post-demo, after T-0439 | **T-0462** (code no gate can see + 3 `CLAUDE.md` corrections) | `generate-clients` **does not exist on `master`** (PM-verified) — proposing a doc line for it now would hand the owner an edit that is wrong until T-0439 merges. Carries the `owner-claude-md` manual step, which **must not** be batched into the regen bundle. |

**Lane table:** in `INDEX.md` under "Shared-file lanes — REVALIDATED 2026-07-30". Changes worth
calling out: **T-0442's dependency was removed from T-0448** (it merged), and **T-0450 was added in
its place** — a swap, not a relaxation. **Four new lanes** were added by the security gate, the
important one being the **five copies of `RequestLoggingMiddleware.cs`** (T-0446 AC9 → T-0457): five
files, one logical change, **all five must move together — four-of-five is a hole**, and the line
offsets are not uniform (`Cleansia.Web.Customer` is +4 lines vs the other four hosts).

---

## 5. Process notes for the record

### 5.1 This charter has no `Agent` tool — it cannot dispatch

Verified again in this session. The PM instance has **no `Agent`/`Task` tool** and no `claude` CLI on
`PATH`. It cannot spawn developers, the paired reviewers, or the analyst/architect deliberation
panels. **The orchestrator dispatches.**

Consequences a future PM instance must plan around rather than rediscover:
- Steps 3–7 of the PM workflow ("route", "spawn a reviewer in parallel with every developer",
  "invoke security/optimizer/qa") are **descriptions of what must happen**, not things this instance
  can cause. Write every ticket so it can be picked up cold — file:line grounding, the reviewer
  pairing named, the lane recorded — and hand the sequence to the orchestrator.
- Do **not** write a sprint plan whose next action is "PM dispatches X". The next action is always
  "orchestrator dispatches X".
- The one thing this charter *can* do unaided is **ground-truth verification** — reading code, running
  builds and checkers, parsing binaries. Use it: §1 and §2 of this document, the 33-error lint
  baseline in T-0455 and the font `cmap` measurement in T-0450 are all first-hand.

### 5.2 Worktrees share one repo-global `git stash` stack — this cost ~50 minutes

A reviewer instance ran `git stash -u` inside a **developer's** worktree mid-ticket. `git stash`
pushes **and hard-resets the working tree**, so the developer's uncommitted work vanished under them.

`refs/stash` lives in the **shared `.git` directory**, not in the per-worktree `.git` file — so every
worktree pushes onto **one** stack, and a `pop` in one tree can restore another tree's entries.

`process/shared-file-lanes.md:40-42` already bans `git restore` / `git checkout --` / wholesale-revert
for the same underlying reason, but it enumerates **commands** rather than the **class**, and `stash`
is not among them. **Yes, that file should say so** — and it should say it about the class
(tree-wide state operations that discard or relocate uncommitted work an agent does not own), or the
next agent reaches for `git clean -fdx` or `git reset --hard` and the doc is silent again.

**Filed as T-0456**, not applied by the PM: this charter owns tickets, `INDEX.md` and sprint status —
it does **not** own `agents/process/*.md`. T-0445 set the precedent this sprint (an approved process
change routed as an `architect` + `docs` ticket, landing as Gate 0.5). T-0456 follows it, and its
panel is asked the harder question the incident implies: may a reviewer mutate the tree it is
reviewing **at all**, given that building writes to it?

**Also verified while writing that ticket:** the string `worktree` appears in **zero** process or
charter documents (only in seven ticket files), and `git stash` appears **nowhere in `agents/` or
`.claude/`**. The multi-worktree execution model the team runs on is entirely undocumented.

---

## 6. Escalations to the owner

> ⛔ **This whole section is SUPERSEDED — read §9.7 for the current owner-owed list.** Item 0 (the DEV
> photo check) was discharged in §8.9, and **item 1 (Q-I18N-02) was ANSWERED on 2026-08-01.** Kept as
> the record of what was escalated and when.

1. ~~**Q-I18N-02 — `blocking: yes`, and it may gate the demo.**~~ **ANSWERED 2026-08-01 — see §9.2.**
   The shorter `ru`/`uk` wording for the
   profile "Edit profile" chip. Needs a **native speaker**; the PM deliberately took **no default**.
   T-0450 AC2 will not pass without it. On the pre-prod blocking index.
2. **Q-BRAND-01 — Poppins covers 0/98 Cyrillic code points on every platform.** All three Poppins
   weights, byte-identical binaries on Android and iOS, same family from Google Fonts on web. Every
   heading in `ru`/`uk` currently falls back to a system face beside Nunito body text. T-0450 fixes
   **the profile hero only**; the platform-wide strategy — fallback, per-locale swap, subset-merge, or
   replace Poppins — is a **brand** decision. Non-blocking, `pre-prod`.
3. **Two risks the owner's demo-scope ruling means we are now carrying** (from the overruled §3, which
   the ruling did not make untrue):
   - the **owner-run regen bundle sits inside the demo critical path**, with a demonstrated failure
     history immediately after a regen (T-0438, and PR #166 before it), and T-0439 (the mechanical
     guard) will probably not have landed in time to catch a third occurrence;
   - **four `security_touching` tickets** now run against a demo date, including an open
     **EXIF-geolocation privacy** question on user-uploaded avatars. Compressing a security gate
     against a date is the specific thing this backlog exists to prevent — flagging, not blocking.
4. **A live accessibility failure is shipped today**: iOS dark-mode avatar initials at **2.14:1**
   against a 3:1 floor, on both apps' profile screens. T-0451 is `ready` and cheap; it needs dispatch.
5. **Lint is non-blocking and hiding 33 real errors** (`frontend-ci.yml:40-42`). T-0455 clears the
   module-boundary slice and reports whether the gate can be flipped. Post-demo, but it is the reason
   nobody reads lint output — which is adjacent to how T-0438 shipped.
0. **⏱️ ONE-MINUTE CHECK, and it is the only thing on this list you can act on immediately.** Open a
   partner or admin **order detail on DEV** and see whether the before/after photos render. That
   settles the single caveat on T-0446's AC4: QA proved the avatar renders in Chromium, WebKit and
   `CGImageSource`, but **could not reach real Azure** (no credentials — and it correctly declined to
   obtain any). If real Azure sends `nosniff`, those renders break. Order photos already travel the
   identical `application/octet-stream` path and are already `<img>`-bound on DEV, so **if they
   render, the caveat is closed.** If they do **not**, tell us — it changes T-0446, all three client
   tickets, and promotes **T-0464** from post-demo to demo-blocking. **Not a ticket, not a blocker,
   sixty seconds.**
6. **NEW — the demo will run against a log store that is bearing real people's PII.** Not a decision
   you need to make; an **awareness** item, because it is about live data on a live environment.
   `GET /api/User/GetCurrent` writes the caller's **email, first name, last name, phone number and
   birth date** into Information-level logs on **all five hosts**, on **every** request — and it is
   the most-called authenticated endpoint on the platform. DEV is live and your iPhone is pointed at
   it, so this is already happening. **T-0457 fixes it and is sequenced pre-demo.** Two things worth
   your call rather than ours: (a) whether the existing DEV log retention should be shortened or the
   current logs purged once T-0457 lands, and (b) whether this changes anything about who you are
   comfortable demoing to. Neither blocks any ticket.
7. **NEW — the avatar security gate passed, but it found the Gate 0.5 failure mode inside the very
   sprint that shipped Gate 0.5.** T-0446's redaction test passes for the wrong reason: it uses a
   payload 43% the size of a real response, so it never exercises the truncation that defeats the
   control. Gate 0.5 leg 1 (mutation-prove the test) shipped as **T-0445** five tickets earlier. This
   is not a reason to distrust the gate — the gate is exactly what caught it — but it is evidence
   that **a written rule does not enforce itself**, which is the argument T-0439 (mechanical regen
   guard) and T-0454 (mechanical Compose rule) are both making. Worth remembering when either of
   those looks like overhead. No action needed from you.
8. **NEW — a free experiment to run while you regenerate for T-0446. Costs you one extra command.**
   You are regenerating anyway. Run **one extra pass with `markOptionalProperties: true` into a
   scratch output directory**, diff it, and throw it away — nothing committed, nothing overwritten.
   That empirically settles whether `removePhoto: boolean` would even become **optional** under
   **Option D**, which is **currently the unverified premise the entire Option-D rejection rests on**.
   It is decisive, it is nearly free, and **the opportunity expires the moment the regen is done** —
   after that it costs a whole separate regen cycle to answer. The `architect` records the result in
   the living doc `agents/architecture/decisions/generated-client-contract.md` (**not** in the ADR);
   that doc lands with T-0439. The revisit trigger for D is **call-site-count-shaped**: one regen
   breaking **>10 call sites for a single added optional field**.
9. **NEW — `Q-CI-01` (branch protection) is filed `post-prod` and explicitly non-blocking.** No action
   needed. Recorded here because the reasoning is the useful part and should not be re-litigated: the
   prod deploy is **`workflow_dispatch`-only behind the `prod-weu` Environment**
   (`deploy-pro.yml:19-29`), so **an unbuilt `master` push cannot ship itself**. Branch protection
   would improve the inner loop, not the release safety property — which is already held.
10. **NEW — a third owner-gated manual step now exists, and it is NOT part of the regen bundle.**
   T-0462 proposes **three `CLAUDE.md` corrections** as literal text (owner applies; no agent edits
   that file). One of them matters more than the other two: **all six documented Nx build/serve
   commands currently fail** — the project names end in `.app`, not `-app`. Every agent that reads
   `CLAUDE.md` and tries to build burns a cycle on it. **Do not batch this with the T-0446 regen
   bundle** — T-0462 is post-demo and its text is only final after T-0439 merges.
11. **NEW — a hard gate to remember after the demo.** T-0458/T-0459 (image sanitization) **must land
   before any feature that shows one user's avatar to another user** — a cleaner's face on an
   assigned order, an avatar in the admin user list. The avatar is currently self-only, so nothing is
   disclosed today. That constraint is recorded on the tickets, but it is the kind of thing that gets
   walked past when a small feature looks obvious, so it is here too.
   **Correction applied 2026-07-30 — the gap is narrower than first reported to you.** Both mobile
   platforms **already strip EXIF/GPS client-side**: Android via `ImageCompressor.kt` (PR #154) and
   **iOS via `ImageCompressor.swift`, which came first** (PR #154 mirrored it — the PM verified this;
   the correction as relayed named only Android). What remains is **web uploads**, **blobs stored
   before PR #154 merged**, and the fact that **a client-side strip is unenforceable** — the server
   cannot tell a stripped upload from an unstripped one. Still worth closing, as defence in depth, but
   it is not the wide-open hole the first write-up implied, and T-0458/T-0459 now say so.

---

## 7. Honest statement of what this pass did NOT do

Per Gate 0.5 leg 3, applied to the PM's own work in this session:

- **No specialist agent was dispatched, and no code was written** — see §5.1. Everything below the
  ticket files is unchanged application code.
- **No suite was run.** The gate evidence for the five `done` tickets is **as reported in the PR
  bodies**; the PM re-read the code at `ce2416a0` (§2) but did not re-execute a single test,
  Gradle task or iOS build. Do not read §2's re-verification as a gate re-run — it is a diff read.
- **What the PM did execute, first-hand:** `npx nx lint {partner-stores,partner-services,services,pipes}
  --skip-nx-cache` (33 errors, the T-0455 baseline); a direct `cmap` parse of all six bundled TTFs on
  both platforms (0/98 vs 98/98 Cyrillic, the T-0450 measurement); `shasum` across the font binaries
  and every web/Android brand asset; and the WCAG relative-luminance computation for sky400/sky600 on
  white (2.14 / 4.10, the T-0451 measurement). Those numbers are the PM's own.
- **The 216.8dp / 120.2dp label measurement in T-0450 is NOT the PM's** — it is T-0442's dev's figure,
  carried forward. T-0450 AC1 instructs the implementer to re-measure rather than trust it, because
  the `EditChipMaxWidthFraction` comment says the English headroom is only 5.8dp.
- **No Android, iOS or web build was run in this pass**, so no claim here should be read as build
  evidence.
- **T-0446 AC4 (content-type / extension-less blob) remains an open unknown**, not a known defect. No
  stored blob was fetched.
- **Working-tree state:** `src/cleansia_ios/{CleansiaCustomer,CleansiaPartner}/Info.plist`,
  `CleansiaCustomer/LiveActivity/Info.plist`, `CleansiaCustomer/project.yml`,
  `Cleansia.xcworkspace/…/Package.resolved`, `fastlane/README.md` carry pre-existing uncommitted
  changes. Untouched, unread, unreverted — `Info.plist`/`project.yml` are **off-limits** (owner's live
  Stripe key). This set **moved between sessions**; **re-read `git status` before any iOS work** and do
  not trust this list.
- **Nothing was committed or pushed.** Written this pass: 7 new ticket files, 12 existing ticket files
  updated (status/owner/`depends_on`/status-log), the `INDEX.md` SPRINT-14 block, two entries in
  `questions/open.md` + its pre-prod blocking index, and this document.

### 7.1 Third pass (2026-07-30) — recording the T-0446 security gate

- **The verdict is the security reviewer's, not the PM's.** The PM did not re-run the security
  analysis; it **re-derived the load-bearing numbers** and reconciled them. The one place they
  disagree (the vacuous test's fixture size: reviewer 187 bytes, PM **335**) is recorded in both the
  findings doc and §2.5 rather than quietly harmonised.
- **What the PM executed first-hand this pass:** read the T-0446 worktree's uncommitted diff and all
  four new test files; `grep`-derived the exact `RedactSensitiveFields`/`TruncateBody` line numbers
  across **all five** hosts (finding the +4 offset on `Cleansia.Web.Customer` that the reviewer's
  citation had flattened); a Python serialization of `MyProfileDto` across three representative
  profiles to compute the byte offsets in §2.5; `storage.bicep:81`/`:101`; the blob-name comparison
  across `SaveOrderPhotos` / `UploadOrderPhoto` / `UpdateCurrentUser`; the four `MapToDto()` call
  sites proving no list shape mints a SAS; and a tree-wide search establishing that `AccountUrl` is
  set in **no** configuration file.
- **No test suite was run and no build was executed this pass.** T-0446's own Gate 8 evidence is
  still outstanding — the ticket is `in_progress`, not `in_review`.
- **The PM did not verify AC4.** No stored blob was fetched; the content-type/extension-less-blob
  question remains an **open unknown**, exactly as it was after the second pass.
- **The PM did not modify the T-0446 worktree** — all inspection was read-only `git`/`grep`/`sed`.
  No `git stash`, no `git restore`, nothing staged, nothing committed (see §5.2).
- **The PM did not write the security rule (SEC-5) itself** — it does not own `agents/knowledge/*.md`.
  Routed as **T-0460** (`architect` + `docs`), following the T-0445 / T-0456 precedent.
- **Written this pass:** 4 new ticket files (T-0457…T-0460); 1 new findings doc
  (`security/user-profile-avatar.md`); T-0446 updated (verdict, +AC9, +AC10, scope amendment,
  `blocks`, status log); T-0447/0448/0449 each gained a binding security-conditions block; the
  `INDEX.md` SPRINT-14 block; and this document. **No source file was touched.**

### 7.2 Third pass, continued — the ADR-0031 panel hand-off

- **The PM did not attend the panel and did not re-open its decision.** ADR-0031 is accepted; §2.7
  records its *findings*, not a re-adjudication. The two code changes mandated before T-0439 merges
  are **the T-0439 developer's**, not backlog items, and the PM did not touch them.
- **Two of the three reports were verified as understated, and the corrections are the PM's own
  measurements:** the `CLAUDE.md` Nx names (read from the `name` field of each `apps/*/project.json`);
  the data-access `test`-target gap (read from all three `libs/data-access/*/project.json` — it is
  **three** libs, not one). The `strictTemplates` report was **confirmed exactly as filed**, with lib
  coverage (59/65) added as new grounding.
- **A third correction the PM made to its own earlier work this session:** the EXIF finding (SEC-3) as
  first recorded overstated the exposure. The coordinator corrected it to "Android already strips";
  the PM verified and found **that correction was itself incomplete — iOS strips too, and did it
  first**. The findings doc, T-0458, T-0459 and escalation 11 were all amended. Recorded because a
  security finding that overstates its case is the same verify-not-trust failure the gate exists to
  catch — and it happened in this document.
- **What the PM executed first-hand in this segment:** `git show --stat 2815c4f6`; a search for
  `ImageCompressor` across the iOS tree; `name`/`targets` extraction from every `apps/*/project.json`
  and `libs/data-access/*/project.json`; a `package.json` script-existence check for all seven npm
  aliases `docs/architecture/frontend.md` advertises; a comparison of `frontend-ci.yml`'s
  `continue-on-error` line number between `master` (`:41`) and T-0439's worktree (`:63`); confirmation
  that `generate-clients`, ADR-0031 and `generated-client-contract.md` exist **only** in T-0439's
  uncommitted worktree; and a repo-wide search establishing that nothing imports the stale
  `libs/core/services/src/lib/client/admin-client.ts` and no barrel exports it.
- **The PM did NOT run any Nx command.** T-0462 AC5a requires the implementer to demonstrate one
  failing and one succeeding command; the PM read `project.json` names rather than executing builds,
  and that distinction is deliberate — **the names are verified, the commands are not.**
- **The PM read the other agents' worktrees read-only.** No `git stash`, no `git restore`, nothing
  staged, nothing committed, and no file in any worktree was modified.
- **One note was folded into T-0446's `## Review` on another agent's behalf** — the
  `patterns-frontend.md` harvest that its author correctly declined to write while two agents were
  live in that file. Recorded as a deliberate PM action, not as the author's omission.
- **Written in this segment:** 3 new ticket files (T-0461, T-0462, T-0463); `questions/open.md`
  (Q-CI-01); corrections to `security/user-profile-avatar.md`, T-0458 and T-0459; one addition to
  T-0446's `## Review`; the `INDEX.md` SPRINT-14 block; and this document. **Again, no source file was
  touched** — every finding above is grounded in reads, not edits.

### 7.3 Third pass, final segment — QA's AC4 run and the T-0441 review

- **The QA evidence is QA's, and the PM did not re-run it.** No browser, no Azurite, no
  `CGImageSource` invocation. §2.10 relays an executed result; the PM verified the *code claims*
  around it, not the renders.
- **What the PM verified first-hand here:** `BlobContainerClient.cs:57-68` routing metadata to
  `SetMetadataAsync`; the five `MetadataName` constants at `Metadata.cs:22-26`; the **absence of any
  `cors` block** in `deploy/bicep/modules/storage.bicep`; the four `[src]` bindings in
  `order-photos.component.html` (`:125`/`:207` stored blobs, `:168`/`:254` local `staged.preview`);
  the **absence** of `accessInstructions` from `CleansiaCustomerApi/Models/CreateOrderCommand.swift`;
  the three `reset()` call sites in `BookingBottomSheet.kt` (`:241`, `:301`, `:583`); that **every**
  `SavedStateHandle` hit under `customer-app` is in `build/generated/**` and none in source; and the
  exact contents of `shared-file-lanes.md:19-23` for the lane ruling.
- **One finding is the PM's own and was NOT in the QA report:** `Metadata.CacheMetadata` hardcodes
  `"public, max-age=31536000"` (`Metadata.cs:7-10`) **and the avatar uses it**
  (`UpdateCurrentUser.cs:163`). It is inert today only because of the very decoy T-0464 removes — so
  **the naive version of T-0464's fix would activate `Cache-Control: public` on a private
  SAS-protected avatar**, violating a condition already on record from the security gate. That is now
  T-0464's lead warning and AC5.
- **One reported item did not exist.** A "403 = expiry / 404 = deleted" rule was described as having
  been previously written into T-0447/0448/0449. A grep for `403`/`404` across all three returns
  **nothing** — there was nothing to reword. The corrected guidance was **added**, and the
  non-existence is recorded in §2.10 so nobody hunts for a prior version.
- **The PM made one lane ruling** (`patterns-*.md`) and **routed the durable edit to T-0456** rather
  than editing `process/shared-file-lanes.md` itself, which it does not own. The `INDEX.md` lane list
  carries it effective immediately so the ruling is not waiting on T-0456 to be useful.
- **The PM did not run any suite this pass.** §2.9's counts are as reported by the agent that ran
  them; the PM's contribution is retiring the stale caveat they invalidate.
- **Written in this segment:** 5 new ticket files (T-0464…T-0468); T-0441 → `qa` with its review
  verdict; ~~T-0440 gained `manual_steps: [ios-client-regen]` and its no-free-ride block~~ **— filed
  in error and retracted the same day, see §2.12 and 7.4**; T-0446 AC4 closed with evidence;
  T-0447/0448/0449 each gained a QA-constraints block; T-0456 scope-extended with the lane ruling;
  the `INDEX.md` SPRINT-14 block; and this document. **No source file was touched.**

### 7.4 The PM's own verification failure this pass — recorded, not buried

**The `ios-client-regen` manual step was wrong and the PM filed it on relay** (§2.12). This is the
one place this sprint where a "PM-verified" stamp was applied to something that did not deserve it,
and it is written up here rather than left in the retraction alone, because §7 is where this document
records what it got wrong.

- **The mechanism:** the PM read `CleansiaCustomerApi/Models/CreateOrderCommand.swift`, correctly
  observed `accessInstructions` was absent, and reported it as repo state. The file is **gitignored
  and untracked** — a machine-owned build artifact, stale since Jul 25. **`git ls-files` was never
  run on it.**
- **The generalization:** *existence + content* is not *provenance*. A claim about what the repository
  contains must be grounded in a **`git`-tracked** path. Every other verification this sprint happened
  to be on tracked files, so the gap never showed.
- **The aggravating detail:** T-0440 documented this exact trap at its own lines 34-39 **before** the
  PM walked into it, and the PM's added block **contradicted the ticket's own correct warning**.
- **What was done about it:** retracted in the ticket, in `INDEX.md` and in §2.12 — **all three kept
  visible rather than deleted**, because the failure mode is re-derivation, not absence.
- **What the PM verified this time, first-hand:** `git ls-files` on the directory (0 files);
  `src/cleansia_ios/.gitignore`; the local artifact's timestamp; and `accessInstructions` present in
  `git show HEAD:src/cleansia_android/openapi/customer-mobile-api.json`.
- **Also written in this segment:** T-0469 (the two T-0440 validation-parity findings); the Android
  parity wording recorded on T-0441 as an offer with a lane warning; and T-0440's i18n note corrected
  (`L10n.OrderDetail.accessInstructions` is the **display** label, not the confirm-step hint).
- **Still not run by the PM:** any iOS build, any Swift test, and `./scripts/generate-api-clients.sh`.
  The 694-tests-pass figure is the developer's, relayed — **and this time labelled as relayed.**

---

# 8. FOURTH PM PASS — 2026-08-01, post-merge reconciliation

Everything above §8 was written before wave 2 merged. This section is the reconciliation against
`master` at **`1c8fdd00`**. Every claim below is the PM's own read or run unless labelled *relayed*.

## 8.1 What shipped

| Commit | PR | Ticket | Title |
|---|---|---|---|
| `acf2f0bc` | #175 | **T-0439** | guard the NSwag regen against client/call-site drift |
| `a63b776e` | #176 | **T-0446** | return a resolvable URL for the user's avatar |
| `d6969fef` | #177 | *(untitcketed)* | three gates that reported success without doing anything |
| `1d85b35f` | #178 | **T-0441** | android — capture entry instructions on the booking confirm step |
| `a10e1f88` | #179 | **T-0440** | ios — capture entry instructions on the booking confirm step |
| `1c8fdd00` | #180 | **T-0451** | pin the avatar initials to a colour that survives dark mode |

**`d6969fef` (#177) is not a ticket and should not be back-filled into one.** It is owner-routed work
that (a) fixed three false-green gates in tooling and (b) carried the whole wave-2 backlog — ADR-0032,
ADR-0033, the security findings doc, and T-0450…T-0469 — onto `master`. Recorded here the same way the
Google **sub-first** fix (`8241d3cd`, PR #170) was, so the history stays honest.

## 8.2 Three tickets closed; **two deliberately NOT closed**, and the reasoning is the load-bearing part

**`done`:** **T-0439**, **T-0446**, **T-0451**. Each has a dated status-log line naming its merge
commit and PR, and each was APPROVED by its reviewer.

**Still `qa`, with their code on `master`: T-0440 and T-0441.**

### The question, answered directly

**Does this lifecycle allow `done` with an owed QA item? No.** `ticket-lifecycle.md` §"Done means"
(`:154-163`) lists five conditions, carries **no exceptions clause**, and closes *"Anything short of
this stays out of `done`. We do not mark work complete on hope."* Two fail on both tickets:

- **item 1** — *"AC each have verifiable evidence."* T-0440's **AC1** and T-0441's **AC1** have none;
  T-0440's **AC6** has its launch/navigate legs but not its **render** leg.
- **item 3** — *"QA executed the test plan and recorded the result."* QA has not run.

**The one escape hatch does not reach this case.** §"When the in-workflow gate did not run
(hand-gating)" (`:165-179`) permits `done` when a **reviewer lane died** (a StructuredOutput failure)
while the work landed — discharged by a **MANUAL-GATE block** of hand-inspected evidence plus a
provenance marker in `INDEX.md`. **Both reviewer lanes here ran and approved.** What is missing is not
an inspection but an **artifact that does not exist yet**, and a MANUAL-GATE block cannot be written
for a screenshot nobody has taken. Hand-gating substitutes an inspection for a dead lane; it does not
substitute an assertion for an absent artifact.

### Why the owed item is real, and why this is a handoff rather than a gap

The T-0440 reviewer **proved** the screenshot is not capturable in-suite: it hosted `InstructionsField`
in a **real `UIWindow`** and captured through **two independent mechanisms** —
`drawHierarchy(afterScreenUpdates:)` and `window.layer.render(in:)`, the second of which the developer
had not tried. All four PNGs came back `distinctColors=1`, byte-identical empty vs filled;
`InstructionsField` is never instantiated by any test. It then measured the wrapping **analytically**
instead (295pt available at the narrowest 16.4 device; en/cs/sk wrap to 1 line, ru/uk to 2). That is
evidence of impossibility, not an untried claim.

**Stamping these `done` would retire the question** — which is precisely the failure mode
`T-0445`/Gate 0.5 shipped a gate against, one wave earlier, in this same sprint.

### Recorded so the two facts cannot be confused

Both tickets carry **`merged: <sha>`** in their frontmatter and a 🟡 block in the body; `INDEX.md` has
a dedicated **"MERGED but NOT `done`"** table. `qa` here means *the ticket is open*, not *the code is
unshipped*.

### What is owed, and by whom — the whole list

| Ticket | Owed | Owner |
|---|---|---|
| **T-0440** | AC1 screenshot (confirm step, both fields empty + filled) on a **16.4 device** | **qa** |
| **T-0440** | Gate 8.5 **render** leg — same session, one screen, no new navigation | **qa** |
| **T-0440** | **F-3** — record the actual test-first ordering; there is still no `red→green` line. If it was implementation-first, that becomes a real Gate 6 question and the reviewer re-reviews | **ios** |
| **T-0441** | AC1 screenshot | **qa** |
| *(both, judgement)* | does the **ru/uk two-line placeholder** read as intentional beside the sibling's one line? | **qa**, while on the device |

**Nothing downstream is gated by any of it.** T-0448/T-0449/T-0450 depended on these two for their
**shared-file lane heads plus the field itself** (`values*/strings.xml`, `Localizable.xcstrings`) —
both writes are on `master`. **A screenshot does not gate a code lane.** Recorded on all five tickets.

### One further thing NOT closed, and deliberately not ticketed either

T-0451's status log declares an **unreproduced 519 tests / 1 failure** on the first Core
`clean build test`; the log was not retained, so the test cannot be named, and six subsequent runs
(including an identical `clean build test`) were 519/0. **It is carried as a declared unknown, not
filed.** Filing it would fail **Gate 0** on three legs at once — no named test, no file:line, no
reproducible trigger — i.e. it would be a manufactured finding. **Ask instead:** the next agent that
sees a Core red keeps the full log. The developer was right to declare it rather than absorb it into
a green.

## 8.3 The owner's regen bundle is DONE — and this is what it unblocked

**Both items shipped inside `a63b776e` (#176).** PM-verified first-hand, not relayed:

| Item | Evidence |
|---|---|
| `nswag-regen` | `libs/core/customer-services/.../customer-client.ts` **+4** and `libs/core/partner-services/.../partner-client.ts` **+4** — the `BlobFileDto.blobUrl` member. `admin-client.ts` already carried `blobUrl` on another DTO, so no delta was needed |
| `mobile-spec-redump` | `src/cleansia_android/openapi/customer-mobile-api.json` (4 hits) + `partner-mobile-api.json` (6). **`src/cleansia_ios/openapi/README.md` §"Source of truth" is explicit that iOS and Android read the SAME two committed specs** — so this one redump serves **both** platforms, and iOS needs only `./scripts/generate-api-clients.sh`, which is agent-authorised |

**`master` did not go red after this regen.** That is the first time, and it is T-0439's guard doing
its job on its first live use — one commit after it merged. Worth naming: the two prior regens
(`bbcf5b24`, and `2ce848cb` before it) are, per the ADR-0031 panel, **exactly the two of the last 25
first-parent `master` commits that lack a `(#NNN)`** — i.e. the two that bypassed review.

**T-0446's AC4 is now double-closed.** QA's Azurite run closed it on executed evidence (§2.10); the
**owner** then confirmed on DEV that partner/admin order-detail photos **render**, and those blobs
travel the identical path — stored with no `BlobHttpHeaders`, served `application/octet-stream`,
fetched through a 1-hour SAS. **So real Azure does not send `X-Content-Type-Options: nosniff`.** That
was the single largest threat to the AC4 verdict and the only thing no agent could test. **The
one-minute check in §6 item 0 is discharged — do not ask for it again.** Consequence: **T-0464 is
confirmed post-demo**, exactly as filed, rather than promoted to demo-blocking.

### What became `ready`

| Ticket | Was | Now | Why |
|---|---|---|---|
| **T-0447** (web avatar) | `blocked` | **`ready`** | **Both** halves of its block are gone: T-0446 `done` **and** the regen shipped. **The only one of the three avatar client tickets that is genuinely ready** |
| **T-0457** (S6 PII in `GetCurrent` logs) | `draft` | **`ready`** | The five-copy `RequestLoggingMiddleware.cs` lane is released. **P1** — DEV is live, the owner's iPhone points at it, and this is accruing right now |
| **T-0464** (`ContentType` decoy) | `draft` | **`ready`** | The shared SAS mint is released; sole writer. The architect A-vs-B call is step 1 of the dispatch, not a precondition |
| **T-0471** (ADR-0033 challenger round) | *(new)* | **`ready`** | No dependencies; see §8.6 |

### What did NOT become ready, and on exactly what

| Ticket | Blocked on | Note |
|---|---|---|
| **T-0448**, **T-0449** | **T-0450 only** | Every other dependency is cleared — T-0446 ✅, the spec redump ✅, T-0451 ✅ `done`, and T-0440/T-0441's lane heads + fields **merged**. **Do not "unblock" these by dropping the T-0450 dep**: it writes the same `values-{ru,uk}/strings.xml` and changes what the hero renders in ru/uk |
| **T-0450** | ~~**Q-I18N-02** — the owner~~ **SUPERSEDED BY §9 — answered 2026-08-01; T-0450 is now `ready` and split** | **All four of its lane heads have merged**, so DoR item 4 is satisfied. ~~AC2 needs a **native ru/uk speaker**~~ — the owner supplied the wording; the Poppins half moved to **T-0472** |
| **T-0465** | **T-0464** | Now `ready`, not `done`. Lane T-0446 ✅ → T-0464 → T-0465 |
| **T-0461**, **T-0462** | *nothing* | Both deps satisfied. Both stay `draft` on their **own content** — §8.5 |

## 8.4 The tooling fix is LIVE on `master`, and here is the precise caveat

`d6969fef` (#177) fixed a false green in `agents/tools/check-consistency.mjs`: `dir()` did
`join(REPO, rel)`, so an **absolute** `--paths` became `<repo>/<abs>` — a directory that cannot exist.
It walked to nothing and printed `OK (0 files scanned)` with **exit 0**. **Every agent in this backlog
is instructed to pass absolute paths.**

**PM re-ran both legs on `1c8fdd00` (own runs, exit codes captured, not piped):**

| Command | Result |
|---|---|
| `--paths=<absolute>/src/Cleansia.App/libs` | **32 violation(s)**, exit **1** *(was `OK (0 files scanned)`, exit 0)* |
| `--paths=src/cleansia_ios` | **`consistency: NOT RUN — --paths matched no scannable files`**, exit **1** |
| *(no `--paths`, whole repo)* | **85 violation(s)**, exit **1** |

**So the vacuous green is dead.** §2.14's live-`master` transcript and its "⚠️ that fix is NOT on
`master`" warning are **superseded**.

### The retroactive caveat, stated precisely rather than as a blanket warning

**Any Gate 8 consistency evidence recorded before #177 from an ABSOLUTE path was a non-run, however
green.** But the sprint-14 records the PM can actually see used **relative** paths and reported
non-zero counts, so **those ran**: T-0439's `--paths=src/Cleansia.App/libs` → 32, T-0441's
`--paths=src/cleansia_android/customer-app` → 11. And the two that scanned nothing —
T-0439's `.../tools` (`.mjs` is not walked) and T-0440's `src/cleansia_ios/CleansiaCustomer` (no Swift
coverage at all) — were **correctly recorded as non-runs at the time, by the agents themselves.** That
is the gate working. Record the caveat where it applies; do not use it to discount evidence that was
honest.

**Do not inherit a baseline number.** **47**, **65** and **85** all appear in this sprint's records for
"the consistency baseline". They are scope-specific and some predate the fix. **Measure, and state the
command.** T-0461's AC5 is amended for exactly this reason.

**Two things that did NOT change:** the checker still has **no Swift coverage**, and it is still in
**zero `.github/workflows`** (PM-verified) — so **ADR-0032 D1 prices it `T2-ADVISORY` on every stack**.
iOS enforcement is ADR-0032's call (SwiftLint `custom_rules` or an XCTest guard, **never** the walker).
**Do not file an "add Swift to the walker" ticket.** ADR-0032's **FT-1** — "verify and close the
zero-file-scope `NOT RUN` banner" — is **discharged** by #177; the panel re-scoped it from "build it"
to "verify + close" for exactly this case.

**Two more things #177 fixed, both of which retire open items:**
- **All three `*-client-formatter.sh` now carry `set -euo pipefail` + an input-exists guard.** One of
  the two exit-code unknowns the architect folded into the owner's bundle is closed. **The other
  survives:** `npx nswag run`'s failure exit code is still unknown, so a green `generate-*-client` is
  still not proof the client regenerated.
- **The six broken `CLAUDE.md` Nx commands are fixed** — and the owner used the **npm aliases**, i.e.
  **exactly the departure T-0462 argued for** over the reported "insert the dots" fix. See §8.5.

## 8.5 T-0461 and T-0462 — both unblocked by T-0439, and **both still `draft` on their own content**

This is the part most likely to be got wrong by someone reading only `depends_on`.

### T-0461 — the premise moved twice, and both moves cut against the fix as specified

1. **ADR-0032 (accepted) prices its chosen enforcer `T2-ADVISORY`.** The ticket asks for a
   `check-consistency.mjs` rule *"so T-0439's guard cannot be silently de-fanged"* — but a checker in
   **zero** CI workflows cannot prevent a de-fang; it can only report one to whoever chooses to look.
   **An advisory enforcer for a rule whose whole value is non-bypassability is the defect the ticket
   was filed to prevent.** Meanwhile T-0439 shipped `tools/typecheck-apps.test.mjs`, an 8-case suite
   **already run by `frontend-ci.yml` as a named step** — which is **T1-CI** under ADR-0032 D1, and
   which already discovers each app's compilation unit from that app's `project.json` build target.
   **AC6 added:** choose the enforcer against the tier table and record the reasoning. *"Both"* is a
   legitimate answer; *"the checker rule, described as a gate"* is not.
2. **`check-consistency.mjs` itself changed on `master`** (`d6969fef`, outside the ticket flow), so the
   `T-0454 → T-0461` lane note is stale — a third writer landed ahead of both — and **AC5's "lands
   green" premise is void**: the checker does not exit 0 on this repo.
3. **AC7** (ADR-0032 D2 obliges the `consistency.md` entry to carry
   `**Enforced by:** <enforcer> — <tier>`; this would be the **first** new entry after ADR-0032) and
   **AC8** (the apps/libs scope split is now **decidable**: apps 3/3 → zero baseline → T1-CI legal;
   libs 59/65 → non-zero → `enforcement.md:104-106` **forbids** gating it) added.

**AC1, AC3 and AC4 stand. The defect is still real.** Only the enforcer choice, the tier declaration
and the baseline arithmetic are re-opened.

### T-0462 — the re-verification it demanded is now DONE, and it changed two of three answers

The ticket said its proposed `CLAUDE.md` text was **quoted from an unmerged worktree** and that AC5
required re-verification against the merged `package.json`. **That re-verification has now been run —
as an explicit step, not an assumption.** PM parsed `src/Cleansia.App/package.json` on `1c8fdd00`:

| Claim in the ticket | Verified? |
|---|---|
| `generate-clients` exists | **YES** — real now |
| the three `generate-*-client` names survived T-0439's restructure | **YES** — unchanged, as M1 promised |
| "T-0439 restructures all four onto `nswag:*`" | **NO.** M1 renamed them **`_nswag:*`** — underscore-prefixed, internal. There is **no public `nswag:*`**. Never propose or document those; the point of M1 is that a human never invokes one |

**⛔ And Correction 3 is DISCHARGED — its literal text is now a trap.** The owner already fixed the six
Nx commands in `d6969fef`, **adopting this ticket's own npm-alias recommendation** over the reported
"insert the dots" fix. The block the ticket instructs the owner to *replace* **no longer exists**, so
applying the text as written would **revert the owner's edit**. AC5a's "prove one failing and one
succeeding command" obligation is discharged with it.

**Scope: 3 corrections → 2.** Still genuinely owed: `CLAUDE.md:29` (the repo map calls `core/services/`
"NSwag-generated"; it holds a **stale 280 KB client no regen writes** — PM-verified still present) and
`:97-100` (`generate-clients` undocumented). **AC5b added.** **De-dup ruling: T-0462 owns this edit,
not T-0439** — T-0439's M6 text is stale for the same reason, and the owner gets **one** proposal.

**Both code instances are still live on `master`, PM-verified:** the stale
`libs/core/services/src/lib/client/admin-client.ts` (280 KB, and `nswag-admin.json:39` writes to
`admin-services` instead), and the app-unreachable `email-template-form.facade.ts`.

## 8.6 Two follow-ups filed at close-out

### T-0470 — the credential-shape guard (the class T-0446 closed *around*)

T-0446 closed two classes and **left one explicitly open, in the reviewer's own words**: *a secret
whose field name was never in the redaction token list is caught by nothing.* Nothing in the
middleware, the guard suite or any checklist looks at a value and asks whether it is shaped like a
credential.

**Both live Stripe credentials found this sprint were in that class** — `setupIntentClientSecret`
(found in review round 5) and `ephemeralKey`, which was found **by luck**: it happened to sit behind an
**already-redacted** field, so the *unmasking* guard surfaced it while looking for something else.
A guard that catches a live payment credential as a **side effect** is telling you it had no coverage.

**The sibling guard is cheap because the expensive half already exists and already runs in CI.**
`src/Cleansia.Tests/Logging/RedactionUnmaskedFreeTextGuardTests.cs` already carries the route→wire-DTO
walk, the recursive member flattening, the collection unwrapping, the host-assembly scan, the
anti-vacuity self-check, and `ReadRedactionTokens()` — which parses the **live regex** so a token added
to the middleware widens the guard automatically. The new part is a name/shape predicate (`*Secret*`,
`*Token*`, `*Key*`, `*Password*`; values shaped `sk_`/`ek_`/`seti_`) plus the **same curated-exception
discipline** the existing guard uses (short, per-entry reasoned, and *a newly-added member may never be
silenced by adding it to the list*). **AC4 names the honest mutation:** remove `ephemeralKey` from the
regex and prove the guard names it — i.e. prove it would have caught, deliberately, the credential
that was found by accident.

**Sized S. Sequenced post-demo, behind T-0457 on the middleware lane** — unlike T-0457 there is **no
known live exposure** (both credentials in this class are now listed). This buys the *next* one, which
is the same argument that put T-0439 and T-0454 behind the waves they guard. `security_touching: true`.
**The existing guard found two live credentials within minutes of existing** — that is the expected
value, not a hypothetical.

### T-0471 — ADR-0033's one challenger round on the test-2 floor

**ADR-0033 is `proposed` and cannot bind until one challenger round runs against exactly one item.**
The panel lead **authored the test-2 floor itself**, in answer to challenge C5 which demanded a floor
without proposing one, and then **correctly declined to ratify its own repair** — its status block
says *"A lead may adjudicate between positions the parties argued; inventing the repair and then
ratifying it is not adjudication."*

**One item, one round, three lines of attack the lead already nominated** (is "previously permitted"
decidable, or is the floor an escape hatch? · does the floor contradict test 1, letting the catalog
acquire canonical forms with no Architect at all? · is the retro-validation honest or fitted — the
floor moves **exactly one row** of four). **AC1 forbids a self-challenge**; T-0439 already had to
re-panel an ADR for that reason. **AC2 holds the scope** — test 1 and D2 carried consensus in the
ADR-0032 panel and are not re-opened. `ready`, sized **S**, `depends_on: []`.

**Nothing regresses while it sits** — `conventions.md:125-127` still routes conservatively and
ADR-0032 is accepted. What deferring costs is that **every catalog edit made meanwhile is routed by an
unratified rule**, with nothing to appeal to when a reviewer and a developer disagree.

**Found while writing it, and NOT fixed from inside another ticket:** ADR-0032 and ADR-0033 both carry
a "Number note" saying *0031 exists only in T-0439's worktree and a reader on `master` sees a gap at
0031.* **That is no longer true** — `docs/decisions/0031-….md` is on `master`. ADR-0032 is
`accepted`, and `adr/README.md` rules an unsigned in-body edit to an accepted ADR a process violation,
so each needs a **signed erratum**. T-0471 folds the ADR-0033 one into its verdict and routes the
ADR-0032 one to the PM as a separate finding.

## 8.7 The two process hazards — routed to T-0456, and why there rather than anywhere else

**Both were hit for real this sprint and both generalize:**

- **`cd X && <destructive git>` silently redirects to the MAIN checkout when `X` is missing.** An agent
  **detached HEAD in the owner's repo** this way. The sharp part: **nothing errors.** `cd` complains,
  the git command succeeds, the compound's exit code is the git command's — **0**. It reads as a clean
  run. A rule that lists forbidden git verbs is silent about the **connective that decides where they
  run**; the safe form is `git -C <path>`, or `cd X || exit 1`, never `cd X && <destructive>`.
- **`xcodegen generate` regenerates `Info.plist` from `project.yml`**, wiping the owner's
  **working-tree-only Stripe key** — which is also why a `git pull` costs the same thing. Safe in a
  **scratch worktree** (the committed `project.yml` carries no key), unsafe in the main checkout.

**Routed to T-0456 — extended, not forked — and the reason is T-0456's own thesis.** That ticket
already argues that `shared-file-lanes.md` enumerates **commands** where it should describe the
**class**: *tree-wide state operations that discard or relocate uncommitted work an agent does not
own.* Both hazards are that class. **Filing them as two new tickets would commit, a third time, the
exact error T-0456 exists to fix.** T-0456 is also already the **sole writer** of
`agents/process/shared-file-lanes.md`, so extending it costs one lane and forking would cost two.

**What they add — and this is why they are worth writing down rather than just noting:** they show the
class reaches **further than the current draft**. Hazard 2 reaches past the tree the agent *meant* to
touch (a shell mis-target, not a shared ref). Hazard 3 **is not a git command at all** — it is
regeneration destroying uncommitted local state, and the discriminator the rule must state is *"safe
when the output carries no hand-edited state"* (`generate-api-clients.sh`, `generate-*-client`) versus
*"unsafe when it does"* (`Info.plist`). **AC7 and AC8 added; size still `S`** — three incidents make
the rule better argued, not larger, and one class-shaped sentence replaces N command-shaped ones. *(If
the panel finds itself writing three separate rules, that is the signal to stop and split, not to let
it grow into an `L`.)*

Today this is carried entirely by per-ticket warnings — i.e. by whoever remembers to write one — which
is the fragile mechanism a lane table replaces. **T-0456 has no dependencies and nothing blocks its
dispatch.**

## 8.8 What THIS pass did NOT do (Gate 0.5 leg 3, applied to the PM's own work)

- **No specialist agent was dispatched and no code was written.** No source file was touched; every
  edit is under `agents/`.
- **No test suite, build, Gradle task or iOS build was run.** The gate evidence on the five closed and
  merged tickets is **as recorded by their developers and reviewers**; this pass reconciled state, it
  did not re-gate. Where a number is relayed it is labelled relayed.
- **What the PM DID execute, first-hand, on `1c8fdd00`:** `git log`/`git show --stat` on all six merge
  commits; three `check-consistency.mjs` runs with exit codes captured (32 / NOT RUN / 85); a `node -e`
  parse of `src/Cleansia.App/package.json` for the full script list; a `node -e` parse of every
  `apps/*/project.json` `name` field; `grep` for `blobUrl` across all three TS clients and both mobile
  specs; `grep -rn "check-consistency" .github/workflows/` (**zero hits**); `git show d6969fef --
  CLAUDE.md` and a read of the merged `CLAUDE.md:95-104`; existence checks on
  `libs/core/services/.../admin-client.ts` and `email-template-form.facade.ts`; and reads of
  `RequestLoggingMiddleware.cs:195-249` and `RedactionUnmaskedFreeTextGuardTests.cs`. Those are the
  PM's own.
- **`gh` is not authenticated in this environment** (`gh auth login` prompt), so **PR bodies and review
  states were not read.** The PR numbers come from the merge-commit subjects; the APPROVED verdicts
  come from the ticket files, **except T-0451's, which is relayed by the orchestrator** — see below.
- **T-0451's reviewer verdict text is NOT in its `## Review`.** The only in-artifact trace is a
  status-log line, *"reviewer F1/F2 addressed"*, with both findings closed and reasoned. The ticket now
  says so explicitly under a **PM reconciliation** heading (not a verdict — the PM does not write one),
  with a table pointing at where each AC's evidence actually lives. **Recorded rather than papered
  over**, because a later reader would otherwise take a missing verdict for a missing review.
- **T-0451's declared 519/1 was not investigated** — it does not reproduce and cannot be named.
- **`CLAUDE.md` was read, never written.** No commit, no stage, no push, no `git stash`.
- **Written this pass:** 2 new ticket files (T-0470, T-0471); **18** existing ticket files updated
  (T-0439, T-0440, T-0441, T-0446, T-0447, T-0448, T-0449, T-0450, T-0451, T-0456, T-0457, T-0461,
  T-0462, T-0464, T-0465, T-0467, T-0468, T-0469 — status/owner/`merged`/`adrs`/status-log/scope
  blocks); the `INDEX.md` SPRINT-14 block and its lane list; and this section.

## 8.9 Escalations — the short list of what the owner still owes

**Superseded from §6:** item **0** (the one-minute DEV photo check) is **DONE — you did it**, and it
closed T-0446's AC4. Item **3**'s first risk (the owner-run regen inside the critical path) is
**discharged** — the regen ran and `master` stayed green.

1. ⛔ **SUPERSEDED BY §9 — `Q-I18N-02` IS ANSWERED (2026-08-01). Do not act on this item.** The owner
   chose the verb-only label + truncate-don't-wrap; T-0450 is split and `ready`; the owner is off the
   demo chain. Kept below as the record of what was escalated. ~~**`Q-I18N-02` — `blocking: yes`, unanswered, and it is now THE bottleneck.**~~ The shorter `ru`/`uk`
   wording for the profile "Edit profile" chip. Needs a **native speaker**; the PM deliberately took no
   default. It gates **T-0450 → T-0448 + T-0449**, i.e. **both remaining mobile legs of the avatar
   feature you ruled into the demo**. Nothing else in the sprint is blocked on anything but this.
   *(If it will be slow, say so and we will split T-0450's Poppins-Cyrillic half out so the two mobile
   tickets stop waiting on a chip label — the split is written up on the ticket.)*
2. **Two `CLAUDE.md` lines** (owner-gated; no agent edits that file): `:29` still calls
   `core/services/` "NSwag-generated" — it holds a **stale 280 KB client no regen writes** — and
   `generate-clients` is undocumented at `:97-100`. **The third correction is already done: you applied
   it in `d6969fef`**, using the npm aliases T-0462 recommended. **T-0462 owns the corrected text
   (AC5b); T-0439's M6 text is stale — do not use it.** Not batched with anything.
3. **Awareness, not a decision — `T-0457` is now `ready` and is P1.** `GET /api/User/GetCurrent` is
   still writing every caller's email, name, phone and birth date into Information-level logs on all
   five hosts, on every request, on **live DEV**. Two things are yours rather than ours: (a) whether
   existing DEV log retention should be shortened or current logs purged once it lands, and (b)
   whether it changes who you are comfortable demoing to. Neither blocks a ticket.
4. **Carried, unchanged:** `Q-BRAND-01` (Poppins covers **0/98** Cyrillic on all three platforms — a
   **brand** decision, `blocking: no`) and `Q-CI-01` (branch protection, `post-prod`, explicitly
   non-blocking — the prod deploy is `workflow_dispatch`-only behind the `prod-weu` Environment, so an
   unbuilt `master` push cannot ship itself).
5. **One thing nobody can close and you should know about:** `npx nswag run`'s exit code on a **failed**
   generation is unknown. If it exits 0, `generate-*-client` would typecheck a stale tree and report
   success. **Do not read a green `generate-*-client` as proof the client regenerated.** Your next
   regen is the cheapest place to find out.

---

# 9. FIFTH PM PASS — 2026-08-01. Q-I18N-02 answered · T-0450 split · two new defects filed

**Baseline: `master` at `f649c3bd`** — a docs-only commit on top of `1c8fdd00` (§8's own reconcile).
Working tree carries the owner's uncommitted iOS files; **no agent opened `Info.plist` or `project.yml`.**

## 9.1 The headline, in one line

**`Q-I18N-02` is answered, it was the last `blocking: yes` question in the whole backlog, and the owner
is now off the demo chain entirely.** What stands between the two mobile avatar tickets and `ready` is
one `ready` ticket's write landing on four shared files — not a reply from you.

## 9.2 The answer, and the two things it does NOT settle

**Your words, recorded verbatim in `questions/answered.md` and quoted in T-0450's AC2:**

> *"the ios and android apps have 'Edit profile'. And when translated then it's a long one. I want just
> to keep 'Edit'/'Редактировать' and truncate it if it doesn't fit by the whole length."*

**Two separate rulings, and both matter:**

1. **The label is the verb alone** — `Edit` / `Редактировать`, plus the equivalent verb in `cs`, `sk`,
   `uk`. Those three come from the **leading verb already inside the shipped long string**
   (`Редагувати`, `Upravit`, `Upraviť`) — a derivation from strings a native speaker already approved,
   **not a fresh machine translation.** The ticket says so explicitly, because "check it in a
   translator" is exactly how the right word gets replaced by a plausible wrong one.
2. **Overflow is TRUNCATION** — not wrapping to a second line, not shrinking the type. **This is live,
   not theoretical:** `Редактировать` is 13 characters against `Edit`'s 4, so the truncation path will
   still fire at 320dp.

### ⚠️ What the answer does NOT touch — do not let anyone tell you otherwise

**`Q-BRAND-01` — Poppins — is completely unaffected.** All three bundled Poppins weights cover **0 of
98** Cyrillic code points, on **both** mobile platforms (the binaries are byte-identical, sha1-verified),
while all three Nunito weights cover 98/98. So `Редактировать` — and every `ru`/`uk` user's name in the
profile hero — **still falls back to a system face regardless of how short the string is.** A shorter
Russian word is still a Russian word.

The two defects sat on one screen; they never had one cause. **That is why T-0450 was split** (§9.3).

### And two things I deliberately did not decide for you

Neither is an owner call — both are implementation questions. But an unwritten implementation question
is an *invented* answer, so both are now **explicit AC** on T-0450:

- **AC4 — the truncation mode.** Is it a tail ellipsis? Android already ships one
  (`ProfileTab.kt:339-346`, `TextOverflow.Ellipsis`). **iOS has no `.lineLimit` and no
  `.truncationMode` at all** (`ProfileTab.swift:332-350`), which is why the iOS chip currently *wraps to
  two lines* rather than truncating. The AC requires both modifiers to be set **explicitly** and the
  chosen mode **named with a reason**; the default, if nothing argues otherwise, is `.tail` to match
  Android. **Relying on SwiftUI's unstated default fails the AC even if the pixels come out right.**
- **AC5 — the accessibility label under truncation.** When the label is visually cut, what does
  VoiceOver / TalkBack announce? The AC requires the **complete** string, **verified by executing the
  read**, and — if the platform already guarantees it — the mechanism **named and cited** rather than
  assumed. No "the framework probably handles this".

**One residual I defaulted rather than escalated:** the original question also asked whether the chip
should diverge from the **screen title** (`profile_edit_title`, the same wording today). Your answer
named the app label without separating the surfaces. **T-0450 AC6 defaults to changing the chip only** —
your complaint was truncation, and only the chip truncates; a screen header reading "Edit" is a
different change from the one asked for. **The AC forces it to be recorded**, so if you meant all three
surfaces (there is also a partner Android `edit_profile`) it is a one-line extension, not a rediscovery.

## 9.3 T-0450 is SPLIT, and here is the corrected dependency graph

**Before:**
```
T-0448 ─┐
T-0449 ─┴─> T-0450 { (A) the label  +  (B) Poppins/Cyrillic } ─> Q-I18N-02 ─> THE OWNER
                                        └─> also needed an architect panel + Q-BRAND-01 (unanswered)
```

**After:**
```
T-0448 ─┐
T-0449 ─┴─> T-0450  READY   { (A) the label only }        ← dispatch this; nothing above it

T-0472  draft  { (B) Poppins covers 0/98 Cyrillic }       ← blocks NOTHING
                needs an architect panel; feeds Q-BRAND-01 (still yours, still non-blocking)
                ⚠️ but sequence it LAST on the ProfileTab.kt lane — see 9.3.1
```

**Which half blocks the avatar tickets: the LABEL (T-0450). Which half does not: the FONT (T-0472).**

The label half blocks them because it writes the **same four files** they rebuild —
`values-{ru,uk}/strings.xml`, `ProfileTab.kt`, `Localizable.xcstrings`, `ProfileTab.swift` — and it
changes what the hero chip renders in `ru`/`uk`. These are **shared-file lane** dependencies, not logic:
an avatar dropped into a hero whose label is about to change underneath it, on the same files, is a
three-way conflict on one 40-line view.

The font half blocks nothing. Keeping it inside T-0450 would have made both avatar tickets wait on an
architect panel and an unanswered **brand** decision that neither of them needs.

### 9.3.1 The honest caveat: the split does NOT fully decouple the lanes

If T-0472's architect ruling reaches the **hard-coded** Poppins call sites, it writes
`ProfileTab.kt:437` and `EditProfileScreen.kt:215` — and `EditProfileScreen.kt:230` is T-0448's own
photo-picker TODO. **So T-0472 is appended LAST on that lane**, after T-0448, or its scope is confined
to `Type.kt` with the call sites deferred. Written into both tickets. Claiming a clean split would have
been the easy thing to say and would have produced a conflict later.

### 9.3.2 Two dependency discharges that look like dropped dependencies and are not

`T-0450` lost all three of its `depends_on`; `T-0448` lost `T-0441`; `T-0449` lost `T-0440`. **Every one
was a lane dependency, and a lane dependency is satisfied when the head's write lands** — all four are
on `master` (`1d85b35f`, `ce2416a0`, `a10e1f88`, `1c8fdd00`).

**T-0440 and T-0441 are still `qa`, each owing an AC screenshot.** Leaving them in `depends_on` would
have kept T-0450 — and therefore the entire avatar chain — un-`ready` **on a screenshot**. §8.2 already
ruled *"a screenshot does not gate a code lane"*; this is that ruling applied rather than re-derived.
The reasoning is written into all three ticket status logs, in a table, precisely because "the PM
dropped a dependency" is what it looks like from the outside.

## 9.4 NEW — "Report an issue" goes red (T-0473), and the semantics question I did not absorb

**Your report:** on the order detail screen, both iOS and Android, "Report an issue" uses the secondary
colour; you want it red. **One decision applied twice → one ticket.**

**One correction to the report, so nobody wastes a cycle:** the token in force is **`primary`**, not
`secondary` — `CleansiaColors.primary` on iOS (`OrderDetailView.swift:300-307`) and
`MaterialTheme.colorScheme.primary` on Android (`OrderDetailScreen.kt:510-535`). Your "secondary"
describes the button's **rank** (it is an outlined, second-tier affordance), not its colour role. There
is no `secondary` token to hunt for.

**Three different reds exist and they are not interchangeable** — this is exactly what I was asked not
to let a developer guess:

| | Treatment | Shape |
|---|---|---|
| 1 | iOS `CleansiaDangerButton` (`CleansiaButton.swift:157`) | error-**tinted surface** — `error.opacity(0.12)` fill, `error` glyph/label, `0.4` hairline. Catalog law, `patterns-mobile.md:245`, *"the ONE way"* |
| 2 | Android `CleansiaDestructiveButton` (`CleansiaButton.kt:101`) | **filled, fixed-red container**, deliberately **NOT** `colorScheme.error`, with a written argument at `:80-99`: in dark mode `error` is red-300 and out-luminates the Sky400 primary — *"Danger must not out-rank the primary; it must read as danger."* |
| 3 | outlined + `error` tint | **what Cancel already uses on both platforms** |

**The two Core components are not parity siblings** (tinted surface vs filled container), so "adopt the
danger component on both platforms" would make the platforms **diverge** — an ADR-0018 parity problem —
while closing a colour complaint. That is a strong argument for treatment 3, and it is the **panel's**
call to make and defend, not mine. **AC1 forces the choice with a why-not for the other two.**

**Two things already true in the code that the ticket carries:**

- **Cancel and Report issue are adjacent** — one 8dp spacer between them (`OrderDetailScreen.kt:505-508`;
  the same `VStack` on iOS). Painting Report issue with Cancel's colour leaves **two adjacent buttons of
  the same colour, shape and rank** — one cancels a booking, the other files a complaint. **AC3** forces
  a stated differentiator.
- **A shipped test will stay GREEN while its comment becomes false.**
  `OutlinedButtonColorsTests.swift:61-70` is prefaced *"Cancel destructive, Make recurring + Report issue
  primary"* — and its body asserts the **colour resolver**, not the call site, so it cannot see this
  change at all. **AC4** makes repairing it non-optional. A green suite carrying a lie about the screen
  it names is the exact failure Gate 0.5 exists for.

**The scope question you asked me to answer: the hand-rolled violation is OUT.** Partner
`ProfileHubContent.swift:298-320` (`LogoutRow`) hand-rolls `CleansiaDangerButton` — PM-verified, it
reproduces the fill, the glyph colour and the `0.4` hairline inline. That is the **non-zero baseline**
keeping the catalog entry at `(gate pending: FT-5)` in `catalog-governance.md:111`, because
`enforcement.md:104-106` forbids making a check blocking until its baseline is zero. **Excluded from
T-0473** — different app, different screen, different affordance — so a two-line colour change does not
carry a catalog-tier promotion. It is **named** in the ticket so a reviewer does not read its absence as
an oversight. **Open item: ADR-0032's FT-5 has no `T-*` id.** It is named in the ADR and in
`catalog-governance.md` as the ticket that discharges `(gate pending:)`, but no ticket file exists. **I
did not file it** — you asked for specific tickets and this is not one of them; it should be filed
deliberately, not as a side effect of a colour fix.

### 🔶 Q-DESIGN-01 — recorded rather than absorbed, and it is NOT blocking

"Report an issue" is a **reporting** affordance. Red means **destructive or error** on both design
systems. Nothing is destroyed and nothing has failed.

**You asked for red explicitly, so it is going red — T-0473 ships it.** What is open is what the design
system says afterwards: does the danger role gain a **second sanctioned meaning**, is this a **named
exception**, or does the system need a distinct **warning/attention** role? Two catalog entries state
laws about what red means (`patterns-mobile.md:245` *"the ONE way"*; `CleansiaButton.kt:80-99`'s rank
argument), and painting a non-destructive action red without amending either leaves the next developer
with a catalog that says one thing and a codebase that does another — and the reviewer after that with
no way to tell an approved exception from a defect.

**Filed `blocking: no`, `post-prod`, default = "named exception".** It does not gate T-0473; T-0473
produces its input.

## 9.5 NEW — the two process fixes you approved (T-0474, T-0475)

Both are being implemented by the coordinating agent outside the normal dispatch. **Filed for
traceability and reconciliation** — if the implementation differs from the AC, the implementation wins
and the ticket is corrected to match.

### T-0474 — post-checkout regeneration for iOS

Two gitignored, machine-owned artifacts go stale on every pull touching `src/cleansia_ios`: the Swift
API clients (`src/cleansia_ios/scripts/generate-api-clients.sh` — note the path is under
`src/cleansia_ios/`, not the repo root) and both `.xcodeproj`s (`xcodegen generate`, in both app dirs).

**It has cost twice, in two different ways, and the second one is the argument:**
1. It broke your build — a **Jul-25** client with no `accessInstructions`, and `BookingInstructions.swift`
   with **0** references in `project.pbxproj`, so **code that was on `master` was silently absent from
   the target**. Not a compile error pointing at the cause; a file simply not in the build.
2. It cost a reviewer a false conclusion — it read the stale client, found the field missing, and
   declared **T-0440** owner-regen-blocked, **contradicting that ticket's own warning at its lines
   34-39.** *A warning written on the ticket, in the ticket about to hit the trap, did not prevent it.*
   That is why the fix is not another warning, and it is why **AC2 asks the mechanism directly: "would
   this have stopped that reviewer?"*

*(I verified the **repaired** state — both artifacts now dated 2026-08-01 15:07, 8 `BookingInstructions`
references in `project.pbxproj`, `accessInstructions` present in the generated `CreateOrderCommand.swift`.
The pre-repair figures are yours, recorded as reported; they are no longer re-derivable.)*

**De-dup:** **T-0456 AC8** already owns the *rule* (regeneration destroying uncommitted state, in
`shared-file-lanes.md`, with `xcodegen`→`Info.plist` as its worked example). **T-0474 owns the
mechanism**, pointing the opposite way. Both are wanted; neither writes the other's file.

### T-0475 — Stripe key + `DEVELOPMENT_TEAM` into a gitignored xcconfig

Mirrors the pattern `**/GoogleService-Info.plist` already uses in the same `.gitignore` — owner-local,
gitignored, never committed, referenced by the build.

**One refinement to the brief, and it strengthens the case.** I did **not** open either working-tree
file. Read against the **committed** `project.yml` via `git show`: the value has **two homes and two
destruction paths** —

| Home | Destroyed by |
|---|---|
| `project.yml:22` (`DEVELOPMENT_TEAM`), `:137` (`STRIPE_PUBLISHABLE_KEY`) | **git** operations — `pull`, `checkout`, `reset`. **Not** xcodegen, which only *reads* project.yml |
| the generated `Info.plist` | **`xcodegen generate`**, which rewrites it from `project.yml` |

`git status` shows **both** files modified right now. So the fix has to close **both** paths — hence
**AC1 (git-side)** and **AC2 (xcodegen-side)** as separate criteria rather than one.

**Why it is `S`:** the indirection **already exists** — `project.yml:99` is already
`STRIPE_PUBLISHABLE_KEY: $(STRIPE_PUBLISHABLE_KEY)`, and an xcconfig's entire job is to supply that
build setting. This is plumbing that is already built.

**The one real decision, carried as AC4 rather than defaulted:** `DEVELOPMENT_TEAM` is **not a secret**.
Putting it in the gitignored file means a fresh clone **cannot build at all** rather than merely having
a broken Stripe path. Either answer is defensible; an unstated one is not. **AC5** requires a committed
`*.xcconfig.example` + a README line, so a missing file presents as an instruction rather than a
mysterious signing failure.

### ⚠️ The sequencing that must not be got backwards

```
T-0475 lands  →  you drop your values into the new file  →  T-0474's xcodegen leg is safe
T-0474's generate-api-clients leg is safe TODAY and may ship first
```

**`xcodegen generate` wipes your Stripe key today.** Prescribing "regenerate after every pull" before
T-0475 lands converts an **occasional** loss into one on **every single pull** — strictly worse than the
staleness being fixed. Recorded as `T-0474 depends_on: [T-0475]` with the reason, and as **AC4** on
T-0474 so a partial implementation cannot silently ship the unsafe half.

## 9.6 You asked where the iOS photo uploader is. Here is the answer, for the record.

**Only the read path shipped. The avatar feature is one-third done.**

| Leg | Ticket | State | What exists in the code |
|---|---|---|---|
| **Read path** — the API returns a resolvable 1-hour SAS instead of a bare blob name | **T-0446** | **`done` ✅** `a63b776e` (#176) | shipped |
| **Web** upload + removal | **T-0447** | **`ready`** — never dispatched | `updateUserCurrent` is **dead code**; no component dispatches it |
| **Android** upload + removal | **T-0448** | **`blocked`** on T-0450 | `EditProfileScreen.kt:230` = `.clickable { /* TODO: launch photo picker */ }` — the camera pill is tappable and does nothing |
| **iOS** upload + removal | **T-0449** | **`blocked`** on T-0450 | **no avatar UI at all** — `HeroGradient` is an initials-only circle; there is no picker on `EditProfileView.swift` |

**There is no iOS photo uploader because it has not been built.** T-0446 was the *spine*: without it no
client could render an avatar even after a successful upload, because the API returned
`{fileName:"<guid>", base64Content:null, contentType:null}`. It was necessary and it is invisible on its
own. **"T-0446 done" must not be read as "avatars work"** — that sentence is now in `INDEX.md` as a
standing callout, not just here.

## 9.7 What the owner still owes — the complete list, shortest first

1. ~~**`Q-I18N-02`**~~ ✅ **ANSWERED.** It was the last `blocking: yes` question in the backlog. **You are
   off the demo chain.**
2. **Supply the xcconfig values** once **T-0475** lands — the Stripe publishable key, and
   `DEVELOPMENT_TEAM` if AC4 puts it there, in **both** app directories. **Until you do, your own build
   has no key**, and T-0474's xcodegen leg must not be prescribed. *(`manual_step: xcode-project`.)*
3. **Two `CLAUDE.md` lines** (owner-gated; no agent edits that file) — `:29` still calls
   `core/services/` "NSwag-generated" (it holds a **stale 280 KB client no regen writes**), and
   `generate-clients` is undocumented at `:97-100`. **T-0462 owns the corrected text (AC5b);** T-0439's
   M6 text is stale. Unchanged from §8.9.
4. **Awareness, not a decision — `T-0457` is `ready` and P1.** `GET /api/User/GetCurrent` is still
   writing every caller's email, name, phone and birth date into Information-level logs on all five
   hosts, on every request, on **live DEV**. Unchanged from §8.9.
5. **Non-blocking questions carried:** **`Q-BRAND-01`** (Poppins/Cyrillic — now carried by T-0472; the
   platform-wide brand call is still yours), **`Q-CI-01`** (branch protection, `post-prod`), and
   **new — `Q-DESIGN-01`** (does the danger role gain a second sanctioned meaning, or is "Report an
   issue" a named exception; `post-prod`, default = named exception).
6. **Unchanged from §8.9:** `npx nswag run`'s exit code on a **failed** generation is still unknown.
   **Do not read a green `generate-*-client` as proof the client regenerated.**

## 9.8 What THIS pass did NOT do (Gate 0.5 leg 3, applied to the PM's own work)

- **No specialist agent was dispatched and no code was written.** Every edit is under `agents/`.
  Nothing was committed, staged or pushed; no `git stash`; `CLAUDE.md` untouched.
- **No build, suite, Gradle task or iOS build was run.** This pass reconciled state and filed tickets.
- **`src/cleansia_ios/**/Info.plist` and `**/project.yml` were NOT opened.** T-0475's grounding comes
  entirely from `git show HEAD:…` of the **committed** `project.yml`, which is key-free by design.
- **What I DID execute first-hand, on `f649c3bd`:** `git log`/`git show --stat`; reads of both order-detail
  footers (`OrderDetailView.swift:240-313`, `OrderDetailScreen.kt:470-537`); both Core button files
  (`CleansiaButton.swift:25-160`, `CleansiaButton.kt:75-110`); `OutlinedButtonColorsTests.swift` in full;
  `ProfileTab.kt:325-350` + `:248`/`:269`/`:437` and `EditProfileScreen.kt:100`/`:215`/`:230`;
  `ProfileTab.swift:302-350`; `ProfileHubContent.swift:290-325`; `src/cleansia_ios/.gitignore` and
  `openapi/README.md`; `grep -c BookingInstructions project.pbxproj` → **8**; the presence of
  `accessInstructions` in the regenerated `CleansiaCustomerApi/Models/CreateOrderCommand.swift`;
  and a grep for `report_issue` across all three web apps' i18n bundles and `apps/`+`libs/`.
- **Two claims are RELAYED, not verified, and are labelled as such where they appear:** the owner's
  pre-repair observations (a Jul-25 client, 0 `project.pbxproj` references for `BookingInstructions`) —
  **no longer re-derivable, because the tree is repaired**; and the owner's approval of the two process
  fixes, which reached me through the coordinating agent rather than directly.
- **The `216.8dp` / `120.2dp` label measurements were NOT re-derived.** They remain T-0442's dev's
  report. T-0450 **AC3** requires the width to be re-measured against the `EditChipMaxWidthFraction`
  band rather than inherited — the ticket's own standing trap note.
- **No panel was convened.** T-0472 and T-0473 both go to `draft` **needing** one; that is DoR item 2,
  not a dependency, and both are dispatchable today with the panel as step 1.
- **ADR-0032's FT-5 was not filed as a ticket.** Named in §9.4 as an open item, deliberately not created.
- **Written this pass:** 4 new ticket files (**T-0472, T-0473, T-0474, T-0475**); 3 existing tickets
  updated (**T-0450** rewritten to half (A) + `ready`; **T-0448** and **T-0449** dependency discharges +
  re-sequencing); `questions/open.md` (Q-I18N-02 closed, the blocking index updated, Q-BRAND-01
  annotated, **Q-DESIGN-01** added); `questions/answered.md` (the full Q-I18N-02 record); the `INDEX.md`
  SPRINT-14 block, its ready/blocked tables, the demo-chain diagram, the lane list and the owner-owed
  table; and this section.
