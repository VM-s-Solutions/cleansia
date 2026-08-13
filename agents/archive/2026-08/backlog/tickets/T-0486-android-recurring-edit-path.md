---
id: T-0486
title: Android — wire the recurring-booking edit path (the repository function exists and has no caller)
status: draft
size: M
owner: android
created: 2026-08-02
updated: 2026-08-06
depends_on: [T-0485]
blocks: []
stories: []
adrs: []
layers: [android]
security_touching: false
manual_steps: []
sprint: 15
---

## Context

**Owner remark #6 (2026-08-02):** *"Cannot edit a recurring cleaning setup on either mobile app."*
Android half. **iOS is T-0487.** Both are rewritten from **T-0485**'s story before dispatch — the
scope below is what the PM verified exists, **not** the scope of the fix.

### Ground truth — PM-verified on `master` at `0e4ede1b`

The Android plumbing is **built and dead**:

| Layer | State |
|---|---|
| `core/recurring/RecurringBookingApi.kt:49` | `suspend fun update(body: UpdateRecurringBookingRequest): Response<RecurringBookingTemplateDto>` — **written**, maps to the generated `UpdateRecurringBookingCommand` at `:51` |
| `core/recurring/RecurringBookingRepository.kt:70` | `suspend fun update(request: UpdateRecurringBookingRequest): ApiResult<RecurringBookingTemplateDto>` — **written** |
| `core/recurring/RecurringBookingDtos.kt:69` | `data class UpdateRecurringBookingRequest` — **written** |
| callers in `features/recurring/` | **ZERO** — `grep '\.update('` across the feature package returns nothing |
| navigation | only `Routes.CreateRecurringBooking(orderId: String? = null)` (`navigation/Routes.kt:102`), navigated from four sites — all **create**, none **edit** |
| `RecurringBookingsScreen.kt:66` | comment: *"Create + edit ship via `CreateRecurringScreen` — entry points are the …"* — **the comment describes an edit path that does not exist** |

**That last row is the one to flag to a reviewer:** a comment asserting a shipped capability is worse
than silence, because the next developer trusts it. Repairing or deleting it is part of this ticket
regardless of what else lands.

## Acceptance criteria

> **⚠️ These AC are PROVISIONAL. T-0485's story replaces AC1–AC4 before this ticket goes `ready`.**
> They are written so the ticket is not empty and so the story author can see what the platform
> constrains. AC5–AC8 are stable.

- [ ] **AC1 (provisional) — an edit entry point exists** at the location T-0485 AC4 specifies, and a
      customer can change the fields T-0485 AC1 marks editable and see the change persisted after a
      cold restart. Evidence: a screen recording or before/after screenshots plus the reload.
- [ ] **AC2 (provisional) — the already-generated-orders behaviour matches T-0485 AC2** and the UI
      **tells the customer** which of their existing bookings this does and does not touch, in the
      same vocabulary the delete dialog already uses (`recurring_bookings_delete_dialog_what_stops` /
      `_what_stays`). Evidence: the copy plus the screenshot.
- [ ] **AC3 (provisional) — the dead plumbing is either USED or DELETED.** If T-0485's shape does not
      fit `RecurringBookingRepository.kt:70`'s signature, the unused function, its API sibling and
      its request DTO are **removed**, not left beside a second one. Evidence: `git diff --stat`.
- [ ] **AC4 (provisional) — the ViewModel carries an edit mode, not a copy of the create wizard.**
      `CreateRecurringViewModel.kt` is a create-only state machine (PM-read: no `templateId`, no
      `isEdit`, no load-existing). Whether it grows a mode or an edit VM is written beside it is
      stated with a reason. Evidence: the stated choice.
- [ ] **AC5 — the misleading comment at `RecurringBookingsScreen.kt:66` is repaired.** Whatever ships,
      that line either becomes true or goes. Evidence: the diff.
- [ ] **AC6 — the failure path is real.** A failed update surfaces the backend error through the
      existing snackbar/error contract, not a silent no-op. **This is the exact class of defect the
      partner-onboarding investigation found** (a validated value discarded behind a success toast —
      T-0507). Evidence: an error-path test plus the screenshot.
- [ ] **AC7 — a test that goes red against the current code (Gate 0.5 leg 1).** A ViewModel test
      driving the edit path, proved to fail before the wiring exists. Evidence: the red run, then
      green.
- [ ] **AC8 (Gate 0.5)** — `:customer-app` compile + `testDebugUnitTest` **un-cached**
      (`--rerun-tasks --no-build-cache`), task outcomes recorded.

## Out of scope

- **iOS** — T-0487.
- **Any backend change.** If T-0485 AC3 finds the command does not carry a needed field, that is a
  **backend ticket the story names**, and this ticket **holds** — it does not invent a contract.
- **The 3-step-wizard vs single-page-form divergence** — T-0481.
- **Catalog-name localization in the wizard** — **T-0477**, which edits
  `CreateRecurringScreen.kt:977/980/998`. **⚠️ Same file. Serialize: T-0477 first** (it is `S` and
  mechanical), then this. Recorded on both tickets.
- **The Plus gate's enforcement** — **T-0494**.

## Implementation notes

**No panel of its own — T-0485 is the panel.** This ticket implements a finalized story.

**Shared-file lane:** `features/recurring/CreateRecurringScreen.kt` is claimed by **T-0477**. One
writer at a time; **T-0477 goes first.**

## Status log
- 2026-08-02 — **draft (created by pm from the owner's remark #6).** The dead `update()` plumbing at
  three layers, the zero feature-layer callers, the create-only navigation route and the false comment
  at `RecurringBookingsScreen.kt:66` are all PM-verified at `0e4ede1b`. **`depends_on: [T-0485]`** —
  AC1–AC4 are explicitly provisional and get rewritten from the story; this ticket must not be
  dispatched against them.
- 2026-08-06 — **android.** Gate 0: **the ticket's premise is stale.** The edit path — VM edit mode,
  screen, route, nav wiring, 5-locale strings, 7 tests — shipped in `2012b014` (PR #189, 2026-08-02),
  one day after the PM's `0e4ede1b` verification. AC1/AC3/AC4/AC5 were already met at HEAD. What was
  **not** built is what this ticket delivers: the backend refusal never reached the user (AC6), the
  update silently erased `endsOn`, and AC2's "what this touches" copy did not exist. Detail in
  `## Review` → ANDROID.

## Review

### ANDROID — 2026-08-06. Verdict: **premise stale; three real defects found and fixed on the path the ticket names.**

#### 0. Gate 0 — most of this ticket was already delivered

The ground-truth table was verified at `0e4ede1b` (2026-08-01). `2012b014` (2026-08-02) landed the
edit path. At HEAD:

| Ticket's claim (at `0e4ede1b`) | State at HEAD | AC |
|---|---|---|
| `grep '\.update('` in `features/recurring/` returns nothing | `CreateRecurringViewModel.kt:194` calls `recurringRepo.update(...)` | **AC3 met** — plumbing is USED |
| only `Routes.CreateRecurringBooking(orderId)` | `Routes.kt:103` is `CreateRecurringBooking(orderId: String? = null, templateId: String? = null)`; `CleansiaNavHost.kt:613-615` navigates it from the list card's Edit action | **AC1 met** |
| VM is a create-only state machine, no `templateId`/`isEdit`/load-existing | `editingTemplateId` / `isEditing` / `prefillFromTemplate` all present (`:71-73`, `:218`) — **one VM with a mode**, not a second wizard | **AC4 met**, with the stated reason: the three paths differ only in where the initial form state comes from |
| `RecurringBookingsScreen.kt:66` asserts an edit path that does not exist | the same comment now reads true — the card Edit action it names exists (`:341-348`) | **AC5 met** at HEAD, no edit needed |
| — | 7 edit-path VM tests already green | **AC7** needed a *new* red-first test; see §3 |

**T-0485 never produced a story** (`status: draft`, no doc in `agents/archive/2026-08/backlog/stories/`), so AC1–AC4
were still formally provisional. They are moot: the code that would have been written to them exists.
The shared-file lane is clear — **T-0477 is `done`** and its `localizedName(...)` calls are at
`CreateRecurringScreen.kt:990/994/1009`.

#### 1. What was actually broken — three defects, all on the edit path

**(a) The backend refusal never reached the user — this is AC6, and it is the T-0507 defect class.**
`CreateRecurringViewModel.submit()`'s error arm called `snackbar.showErrorKey(recurring_edit_failed)`
— *"Couldn't update the schedule. Try again."* — and put the parsed server message only into
`ActionState.Error(...)`. `CreateRecurringScreen` reads `submitState` **solely** to compute
`submitting = submitState is ActionState.Submitting` (`:114`): the `Error` arm is never rendered. So
the localized, parsed message was computed by `RecurringBookingRepository.httpError` → `ApiErrorParser`
→ stored → **discarded**.

This is exactly the surface `bd604a2b` [T-0494] made load-bearing. That commit gated Update on active
membership as the **fourth link** of the ordered `Cascade.Stop` chain (`UpdateRecurringBooking.cs:47-56`,
after ownership so a stranger's template still resolves as *not found*), and `6a54c10f` shipped
`error_recurring_booking_membership_required` in all five Android locales precisely because
*"on Android the server refusal is the entire UX"* (T-0494 `## Review` §B4.2). It was, and Android
threw it away. Fixed by mirroring the house idiom — 27 existing call sites, e.g. `HomeTabViewModel.kt:48`,
`DisputeDetailViewModel.kt:91`, and `CreateRecurringViewModel`'s **own** `init` block at `:89-91`:

```kotlin
if (result.error !is ApiError.Network) {
    snackbar.showError(result.error.getUserMessage())
}
_submitState.value = ActionState.Error(result.error.getUserMessage())
```

`ApiError.Network` stays silent — `NetworkErrorInterceptor` owns that toast, per the repository's own
contract doc. `recurring_create_failed` / `recurring_edit_failed` are now unreferenced and were deleted
from all five locales (same principle as AC3: used or deleted).

**(b) The update silently erased `EndsOn`.** `UpdateRecurringBooking.Handler` calls
`RecurringBookingTemplate.UpdateSchedule` (`RecurringBookingTemplate.cs:123-149`), which assigns
**every** schedule column from the command — including `EndsOn = endsOn`. The Android form had no
`endsOn` field at all: `prefillFromTemplate` did not read `template.endsOn` and `toUpdateRequest`
omitted it, so the `= null` default went on the wire and a bounded schedule became perpetual. The DTO
already carried `endsOn` **down** (`RecurringBookingDtos.kt:47`); only the echo back was missing.
Fixed: `CreateRecurringFormState.endsOnIso` → prefilled → sent.

**Live exposure today is zero and that is the only reason this is not a P1:** no client writes
`EndsOn`. Web hard-codes `command.endsOn = undefined` (`recurring-bookings.facade.ts:250`), Android
create omits it, and there is no editor on any surface. It is a loaded gun, not a fired one — and the
field is on the command, so the first surface that sets it inherits the bug.

**Completeness audit of the 12 command fields** (`UpdateRecurringBooking.cs:14-26`), since a
full-replace handler makes this the whole safety argument:

| Sent by Android before | Sent now |
|---|---|
| TemplateId, Frequency, DayOfWeek, TimeOfDay, Rooms, Bathrooms, SavedAddressId, SelectedServiceIds, SelectedPackageIds, PaymentType, StartsOn — **11 of 12** | all **12** |

Template columns the command cannot touch, so the update cannot lose them: `IsActive` (owned by
`SetActive`), `PreferredEmployeeId` (ADR-0036 — `UpdateSchedule` does not assign it),
`LastMaterializedFor` (deliberately cleared, see §2). **The API shape is safe for a full send** —
every editable field is both readable from `RecurringBookingTemplateDto` and writable on the command,
so the screen can send a complete current picture. Currency: the form is seeded from
`RecurringBookingRepository.templates`, which `RecurringBookingsViewModel.init` refreshes on every
composition of the list the Edit action lives on (`:33-35`), and `prefillFromTemplate` re-fetches on a
cache miss.

**(c) A 2xx with an unusable body was a silent no-op.** `create`/`update` mapped
`resp.body() == null` to `networkError()` — the **silent** channel, whose whole contract is "the
interceptor already toasted." `RecurringBookingApi.toAppDto()` returns null when any required wire
field is null, so a thin server response would leave the user on the form with no snackbar, no
navigation, and a write that did not happen. Now returns `ApiError.Unknown(error_generic_unknown)`.

#### 2. Reported, NOT fixed

1. **BACKEND, duplicate-occurrence risk — `UpdateSchedule` clears `LastMaterializedFor`
   (`RecurringBookingTemplate.cs:147`) and nothing else de-duplicates occurrences.** That watermark is
   the *only* idempotency guard: `ComputeOccurrences` starts at `max(StartsOn, LastMaterializedFor +
   step, now)` (`MaterializeRecurringBookingTemplate.cs:228-231`), and `MaterializeRecurringBookings`
   creates orders through `OrderFactory` with no "does an order already exist for this template at this
   time" check (grep for `RecurringTemplateId` across `Core.AppServices` finds readers in
   `AutoCancelStaleRecurringOrders` / `SendRecurringOrderReminders` / `CleanupStalePendingOrders`, and
   exactly one **writer** at `MaterializeRecurringBookingTemplate.cs:192`). So: weekly Thursday
   template, sweep materialises Thursday on Monday and stamps the watermark; customer edits *anything*
   on Tuesday; watermark → null; next sweep searches from now and re-emits **the same Thursday slot** —
   a second order, priced and (if card) chargeable. The 7-day default horizon
   (`MaterializeRecurringBookings.cs:38`) is exactly the window in which this bites. Out of scope here
   ("Any backend change"), and it is the strongest challenge to any AC2 copy: the notice can only
   honestly promise that existing bookings are not *rewritten*.
2. **ANDROID, lapsed subscriber cannot reach pause/delete.** Both entry points to the recurring list
   are hidden for a non-member — `PlusRecurringEntryRow.kt:58` (`if (state?.hasMembership != true)
   return`) and `HomeTab.kt:175` (`showRecurringSection = isPlus && ...`). The backend leaves
   `SetActive` and `Delete` **deliberately ungated** so a lapsed subscriber can always stop a schedule
   that is still generating (`UpdateRecurringBooking.cs:107-108`; T-0494 `## Review` §"LEAVE OPEN" ×2).
   Android's hide defeats that: the customer whose membership lapsed keeps getting cleanings booked and
   has no screen from which to stop them. This is **not** the gap T-0494 §B4.2 recorded (that one is
   the missing upsell); it is the opposite failure — hiding too much — and it wants its own ticket.
3. **`consistency.md` E6's deviation note is stale.** It marks `RecurringBookingsScreen` as using
   `collectAsState()`; at HEAD it uses `collectAsStateWithLifecycle()` (`:81-84`). Not edited — E6 is
   an existing rule's deviation list, not mine to rewrite.
4. **Two sibling repositories carry defect (c).** `UserRepository.kt:97` and `OrderRepository.kt:194`
   both map a 2xx-with-null-body to `networkError()`. Not fixed (out of lane, and see §5).

#### 3. Client-side Plus gate — decision: **do not add one here**

T-0494 already routed the missing `recurring_plus_gate_*` upsell to its own Android ticket, and the
server check is authoritative either way. Adding a gate on *edit specifically* would help nobody who
can reach the screen (the entry point is already membership-hidden) and would push in the wrong
direction given §2.2 — Android's recurring problem is that it hides too much, not too little. What the
lapsed member needs is the refusal **legible**, which is §1(a). Recorded here so the iOS lane does not
read this omission as an oversight.

#### 4. Parity note for iOS (T-0487)

Surface to reproduce 1:1: one VM with three modes keyed on optional `orderId` / `templateId` nav args
(no second edit VM); prefill from the cached template list with a refresh-on-miss; **all 12** command
fields sent on update including the echoed `endsOn`; error arm shows the parsed server message and
stays silent only on the transport channel; edit-mode notice on the last step in the delete dialog's
"what stops / what stays" vocabulary; success → snackbar + one-shot effect → navigate to the list.

#### 5. Catalog-edit routing — **routed to the Architect, not written inline**

Candidate entry: *"a 2xx whose body is unusable must not take the silent `ApiError.Network` channel."*

- **Test 1 fires → Architect.** Sweep run:
  `grep -rn --include='*.kt' "body() ?: return networkError()" customer-app/src/main partner-app/src/main core/src/main`
  → **2 hits besides the two this ticket fixed**: `UserRepository.kt:97`, `OrderRepository.kt:194`.
  Stating the rule puts both shipped call sites in violation, so it needs a `consistency.md` deviation
  entry + a canonicalization ticket — neither of which I may file for myself.
- **Test 2 (recorded even though test 1 already routes it):** searched `agents/knowledge/consistency.md`
  for `ApiResult` / `networkCall` / `ApiError.Network` and `agents/knowledge/patterns-mobile.md` for
  `ApiError.Network` / `silent`. E4 and E5 govern *"wrap calls in `networkCall { }`, parse errors with
  `ApiErrorParser`, return `ApiResult<T>`"* — they name the wrapper and the contract but no sentence
  reaches *which* `ApiError` variant a successful-but-empty response takes. So the floor would have been
  available; test 1 fires first and takes precedence.
- **Nothing was written to either catalog by this ticket.** The error-surfacing shape in §1(a) is the
  existing 27-site idiom, applied, not invented.

#### 6. Gate 0.5 — mutation table

Every rule mutated one at a time, RED confirmed from the JUnit XML, restored byte-exact by SHA-256
(`CreateRecurringViewModel.kt` `13c7789c…`, `RecurringBookingRepository.kt` `e3161b17…`,
`values-ru/strings.xml` `827caa94…` — verified identical after each restore and at the end).

| # | Rule | Mutation | Result |
|---|---|---|---|
| M1 | update echoes `endsOn` | drop `endsOn = endsOnIso` from `toUpdateRequest` | **RED** ×1 — *edit mode echoes the stored end date back…* |
| M2 | prefill reads `endsOn` | drop `endsOnIso = template.endsOn` from `prefillFromTemplate` | **RED** ×1 — same test |
| M3 | failure shows the server message | swap `showError(getUserMessage())` → `showErrorKey(...)` | **RED** ×2 — edit **and** create arms |
| M4 | transport failure stays silent | remove the `!is ApiError.Network` guard | **RED** ×1 — *…stays silent — the interceptor owns that toast* |
| M5 | `update` empty body is surfaceable | restore `?: return networkError()` on the update site | **RED** ×1 |
| M6 | `create` empty body is surfaceable | restore `?: return networkError()` on the create site | **RED** ×1 |
| M7 | notice present per locale | delete `recurring_edit_notice_what_stays` from `values-ru` | **RED** ×2 |
| M8 | Cyrillic locales are translated | copy the English title into `values-ru` | **RED** ×1 |

Trap checks: no test asserts on source text (all assert on **behaviour** — captured request fields,
mock call shape, or parsed XML values); every test above is killed by at least one single mutation;
the locale test asserts **per-locale presence**, not locale-vs-locale, so a key dropped from all five
reports five misses rather than passing. (A drop from `values/` additionally fails compilation, since
`R.string` resolves against the default locale — noted, not mutation-tested.)

#### 7. Verification — `--rerun-tasks --no-build-cache`, exit codes captured before any pipe

| Run | Command | Exit | Result |
|---|---|---|---|
| Baseline | `:customer-app:testDebugUnitTest --rerun-tasks` | 0 | **519 / 57 classes**, 0 failures — matches the recorded baseline |
| After the fixes | `:customer-app:compileDebugKotlin :customer-app:testDebugUnitTest --rerun-tasks --no-build-cache` | 0 | **527 / 58 classes**, 0 failures — `40/53 actionable tasks executed`, no "up-to-date" |
| Final, all 3 modules | `:core: :partner-app: :customer-app:` compile + test, `--rerun-tasks --no-build-cache` | 0 | `96 actionable tasks: 96 executed`. **core 151 / 21**, **partner-app 224 / 46**, **customer-app 527 / 58** — 0 failures, 0 errors. The only `UP-TO-DATE` lines are the nine no-op `pre*Build` lifecycle tasks; every `compileDebugKotlin` and `testDebugUnitTest` executed |

New tests: `CreateRecurringViewModelTest` 11 → **15**, `RecurringBookingRepositoryTest` 14 → **16**,
`RecurringEditNoticeStringsTest` **2** (new class). +8 tests, +1 class.

#### 8. AC verdicts

| AC | Verdict |
|---|---|
| **AC1** entry point + persisted edit | **met at HEAD** (`2012b014`), not by this ticket. Card Edit action → `Routes.CreateRecurringBooking(templateId)` → prefilled wizard → `update`. Screen-recording evidence is QA's. |
| **AC2** already-generated-orders behaviour is told to the customer | **met** — `recurring_edit_notice_{title,what_changes,what_stays}` ×5 locales on step 3 in edit mode, written in the delete dialog's `_what_stops`/`_what_stays` vocabulary. **Caveat in §2.1:** the copy promises existing bookings are not *rewritten*, which is true; it does not promise they cannot be *duplicated*, which is a live backend risk |
| **AC3** dead plumbing used or deleted | **met at HEAD** — used, and now used correctly (12/12 fields) |
| **AC4** edit mode, not a copied wizard | **met at HEAD** — one VM, mode on a nav arg |
| **AC5** misleading comment repaired | **met at HEAD** — the comment became true. The *other* stale comment (`CreateRecurringScreen`'s header describing only Paths A/B) was repaired here |
| **AC6** the failure path is real | **met by this ticket** — §1(a) and §1(c); 4 new tests, mutations M3–M6 |
| **AC7** a test red against the current code | **met** — 8 mutations, all RED, restored byte-exact (§6) |
| **AC8** un-cached compile + test | **met** (§7) |
