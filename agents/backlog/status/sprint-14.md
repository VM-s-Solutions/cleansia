# Sprint 14 — owner batch, demo preparation (2026-07-30)

**Baseline:** `master` at `bbcf5b24`.
**Input:** one owner batch of 5 items + 1 owner-approved process change, handed to the PM by the
orchestrator.

---

## 0. Read this first — two things that change how you use the backlog

**(a) The INDEX is stale after PR #148.** PRs **#149–#170** shipped outside this process. The owner
has declined a backfill. Any row describing post-#148 state may be wrong or missing. **Ground in the
code.** A staleness banner now sits at the top of `INDEX.md`.

**(b) `master` frontend CI is RED right now.** Failing run **30533368357**, workflow `frontend-ci`,
jobs `build` and `e2e-smoke`. Nothing else in this sprint can produce honest build evidence until
T-0438 lands.

---

## 1. What the PM verified itself (not taken on report)

Per the Gate 8 verify-not-trust posture, and because the batch brief is itself a report:

| Claim | Verdict | Evidence |
|---|---|---|
| Two web apps break on the regen | **WRONG — all three do** | `nx build … --configuration=production --skip-nx-cache` on `bbcf5b24`: customer **exit 1** (4 errors), partner **exit 1**, admin **exit 1**. Admin fails via the shared `libs/data-access/partner-stores/src/lib/user/user.effects.ts:96`. |
| `ICreateOrderCommand.accessInstructions` is required | Confirmed | `customer-client.ts:7379` — `accessInstructions: string | undefined` is a **required key** (not `?:`). Build error quoted in T-0438. |
| `IUpdateCurrentUserCommand.removePhoto` is required | Confirmed | `customer-client.ts:12879`, `partner-client.ts:12860` — `removePhoto: boolean`. |
| The web wizard drops the entry instructions | Confirmed — **live data-loss bug** | Collected `order-wizard.component.html:491-492` → held `order-wizard.models.ts:48` → displayed back `wizard-summary-step.component.ts:240` → **absent** from `new CreateOrderCommand({…})` at `order-wizard.facade.ts:551`. |
| Backend cap is 2000 | Confirmed | `Features/Orders/CreateOrder.cs:136-138`, `.MaximumLength(2000)` → `BusinessErrorMessage.MaxLength`. |
| "`order_detail_access_instructions` already exists ×5 — verify before adding" | Confirmed **and materially incomplete** | It exists ×5 in the Android **customer** app (`values/strings.xml:273`) and as `access_instructions` ×5 in the **partner** app (`:295`) — but those are **display** labels. The **booking-confirm input hint does not exist**; only `booking_special_instructions_hint` (`:720`). A new key ×5 **is** required on Android, and the same on iOS (`L10n+BookingConfirm.swift:4`). |
| Mobile specs carry the new fields | Confirmed | `customer-mobile-api.json` → `CreateOrder_Command.accessInstructions`, `UpdateCurrentUser_Command.removePhoto`. |
| Mobile client regen is owner-only | **No — it is not** | Both mobile clients generate from the **committed** specs: Kotlin at Gradle build time; Swift via `scripts/generate-api-clients.sh` (`.github/workflows/ios-ci.yml:126`) into the gitignored `CleansiaCustomerApi/`. So T-0440/T-0441 are **not** owner-blocked. |
| — (found while verifying) | **The local iOS client is STALE** | `CleansiaCustomerApi/Models/CreateOrderCommand.swift` has neither `specialInstructions` nor `accessInstructions`, though the spec has both. An iOS dev that trusts the checkout will conclude the field does not exist. Recorded as a trap note in T-0440 and T-0449. |
| No client can render an avatar | Confirmed | `Mappers/UserMappers.cs:41` → `Mappers/BlobMappers.cs:12-18` returns `BlobFileDto(FileName: <guid>, Base64Content: null, ContentType: null)`. The blob name is a bare `Guid` (`UpdateCurrentUser.cs:155`) with no extension and no content-type set at upload (`:160-164`). |
| Android has a placeholder wired to a TODO | Confirmed | `EditProfileScreen.kt:230` — `.clickable { /* TODO: launch photo picker */ }`. |
| — (found while verifying) | **The web partner avatar path is DEAD CODE** | `updateUserCurrent` is **never dispatched** by any component in any app; only the action/reducer/effect definitions exist (partner + admin stores). The sole live `UpdateCurrentUserCommand` caller is `profile.component.ts:224`. It is **not** a working reference for T-0447. |
| The owner's complaint about the Android profile header | Confirmed, with a single structural root cause | iOS `HeroGradient` is **one** `HStack(alignment: .top)` whose edit chip carries `.frame(maxHeight: .infinity, alignment: .center)` (`ProfileTab.swift:296-303`, with a comment saying exactly that). Android stacks a **second row below** a `Spacer(16.dp)` (`ProfileTab.kt:305-330`). |
| The two Android apps don't share a mark | Confirmed | `ic_launcher_foreground.xml`: customer 15 lines `7e50e895c7fd`, partner 24 lines `cfc7b6256584`. Their `mipmap-anydpi-v26` wrappers also differ (colour vs drawable background); only the partner has `ic_launcher_background.xml`. |
| The Android partner splash is unbranded | Confirmed | No `features/splash/` package in `partner-app` at all (the customer app has one). |
| — (found while verifying) | **The system splash comes free** | Both apps: `values/themes.xml:7` → `windowSplashScreenAnimatedIcon = @drawable/ic_launcher_foreground`. Replacing the foreground updates the system splash without a second asset. |
| — (found while verifying) | **Every web app serves a PNG as `.webp`** | All three `Logo.webp` and `apps/cleansia.app/src/assets/images/logo.png` share sha1 `365adf5963`; `file(1)` reports that byte-stream as *"PNG image data, 48 x 48"*. Also: a 48px source rendered at 28px is soft on 2×. |

---

## 2. Sequencing and rationale

**Wave 0 — unblock (must be first; everything else's build evidence is worthless until it lands).**
- **T-0438** `ready`. Also closes the wizard data-loss bug in the same edit, because the correct value
  to pass for `accessInstructions` **is** the `entryInstructions` the user already typed. Fixing the
  build with `undefined` would have been a wasted edit.

**Wave 1 — demo-visible, cheap, parallel (all `ready`; no shared-file collisions between them).**
- **T-0442** Android profile hero → iOS. Highest visible-impact-per-effort in the batch: one owner
  complaint, one structural cause, `S`.
- **T-0443** Android brand assets. Cheap-ish (`M`) and the most visible thing in a demo — it is the
  first frame of the app. The system splash rides along for free.
- **T-0444** Web logo/favicon. `S`, gated behind T-0438 only so its build evidence is honest.
- **T-0440 / T-0441** entry-instructions capture on iOS/Android. Both `S`, both unblocked, and they
  close a cross-platform inconsistency the demo could surface (the partner sees an Access card that is
  always empty).
- **T-0445** the process gate. `S`, doc-only, no collision with anything except T-0439 (serialized).

**Wave 2 — after T-0445.**
- **T-0439** the regen-drift guard. Deliberately *not* first: it prevents the *next* occurrence, not
  this one, and it needs a panel. It is serialized behind T-0445 on `quality-gates.md`.

**Wave 3 — the avatar feature (see §3).**
- **T-0446** spine → owner regen bundle → **T-0447 / T-0448 / T-0449** in parallel.

---

## 3. PM recommendation: the avatar feature should NOT gate the demo

The orchestrator asked for a reasoned position. **Recommendation: ship the demo without T-0446…T-0449.**

1. **It is invisible until the last brick.** T-0446 is the read path; until it lands, every client
   ticket renders the same initials circle the app already shows. Landing T-0448 alone would let a user
   upload a photo and then not see it — strictly worse than today's honest placeholder.
2. **An owner-only handoff sits in the middle of the chain.** T-0446 changes the profile DTO, so the
   three TS clients **and** both mobile specs must be regenerated by the owner before T-0447/0448/0449
   can even compile. That is a hard stop of unknown duration inside the critical path — and, per
   T-0438, the step immediately after a regen has a demonstrated failure history.
3. **The security surface is real, not nominal.** All four tickets are `security_touching`. T-0446
   exposes a blob to a client for the first time on this path; AC4 already surfaces two unverified
   assumptions (no content-type is set at upload; the blob name is an extension-less GUID). EXIF
   geolocation on user-uploaded avatars is an open privacy question I have flagged to the security
   panel. None of that should be compressed against a demo date.
4. **The demo does not need it.** Items 3 and 4 make the app *look* finished; item 5 makes it *do* one
   more thing. For a demo, the first is worth more per unit of effort by a wide margin.

**What I would do instead:** land Waves 0-1, then start **T-0446 only** so the regen bundle reaches
the owner early and the client tickets are ready to run the moment it is confirmed. Re-estimate: the
brief's "~6.5 days before the read path" looks broadly right in shape, but T-0446 itself is a solid
`M` on its own (two panels, a security gate, three test projects, plus the content-type unknown) and
the three client tickets are `M` each — so the honest number is **larger** than 6.5 days, not smaller,
once panels and the owner round-trip are counted. **This is the owner's call, not mine** — if the demo
must show an avatar, say so and I will re-sequence with T-0446 first and Wave 1 behind it.

---

## 4. Blocked, and on whom

| Ticket | Blocked on | Who clears it |
|---|---|---|
| T-0447, T-0448, T-0449 | T-0446 **+** the `nswag-regen` / `mobile-spec-redump` bundle | the **owner** (after T-0446 is implemented) |
| T-0439, T-0440, T-0441, T-0446 | their deliberation panels | the orchestrator (dispatch the panels) |
| T-0439 | `quality-gates.md` lane behind T-0445 | T-0445 landing |
| T-0448 | `ProfileTab.kt` + `strings.xml` lanes behind T-0442 / T-0441 | those landing |
| T-0449 | `Localizable.xcstrings` lane behind T-0440 | T-0440 landing |
| **Every ticket in this sprint** | **no specialist could be dispatched — see §6** | the **orchestrator** |

---

## 5. Escalations to the owner

1. **`master` is red.** T-0438 is written and `ready`; it needs a frontend dev + reviewer dispatched.
2. **A demo-scope decision:** §3 above — does the demo need the avatar? Default answer if silent: no.
3. **No new `questions/open.md` entries were needed.** Everything else in the batch was derivable from
   code or a defensible default, and each default is written into the ticket that carries it (the 2000
   cap from the validator; iOS as brand source per the owner's own words; the adaptive-icon safe zone
   and the monochrome-notification constraint as platform requirements).

---

## 6. Honest statement of what this sprint did NOT do

Per the gate this batch is adding (T-0445, leg 3 — *declare the unverifiable*), applied to my own work:

- **No specialist agent was dispatched, and no code was written.** The PM instance running this batch
  has **no `Agent`/`Task` tool** and no `claude` CLI on `PATH` — verified. I could not spawn the
  `frontend`/`android`/`ios`/`backend` developers, the reviewers alongside them, or the analyst/
  architect deliberation panels. Per my charter I do not write code, ADRs, stories or tests myself, and
  I do not approve merges. **The orchestrator must dispatch the waves in §2.** Every ticket is written
  to be picked up cold, with file:line grounding and the reviewer-per-developer pairing named.
- **Consequently: zero quality gates were executed on any change**, because there are no changes.
  The only thing that ran was my own **ground-truth build** of the three web apps, which is *evidence
  of the defect*, not evidence of a fix.
- **`Cleansia.Tests` / `IntegrationTests` / `HostTests` were not run** — nothing touched the backend.
- **No Android or iOS build was run** — nothing touched those trees. In particular the iOS 16.4 floor
  smoke (Gate 8.5) has not run for T-0440/T-0449 and must not be recorded as passing.
- **The 11-row iOS↔Android hero delta table in T-0442 is my own read of the two files, not a reviewed
  spec.** The Android dev is instructed to re-derive it; AC2 makes each row match-or-explicitly-deviate.
- **T-0446 AC4 (content-type / extension-less blob) is an open unknown**, not a known defect. I traced
  that the upload sets no content-type and the name is a bare GUID; I did **not** fetch a stored blob
  to see whether that actually breaks rendering. The AC says "verify this — do not assume" for that
  reason.
- **The working tree carries pre-existing uncommitted iOS changes.** As of the end of this session:
  `CleansiaCustomer/Info.plist`, `CleansiaPartner/Info.plist`, `CleansiaCustomer/LiveActivity/Info.plist`,
  `CleansiaCustomer/project.yml`, `Cleansia.xcworkspace/…/Package.resolved`, `fastlane/README.md`.
  I did not touch, read or revert any of them; `Info.plist`/`project.yml` are **off-limits** (owner's
  live Stripe key). Note the set **moved during this session** — the conversation-start snapshot also
  listed two `Localizable.xcstrings` as staged and had `master` at `ac2162aa`, while HEAD was
  `bbcf5b24` by the time I read it. So **re-read `git status` before starting any iOS work**; do not
  trust this list or the one in T-0440/T-0449.
- **Nothing was committed or pushed.** Only backlog artifacts were written: 12 ticket files, the
  `INDEX.md` SPRINT-14 block + staleness banner, and this document.
