---
id: T-0920
title: "Customer onboarding: concept D, ready to book with your cleaner's view"
size: L
owner: Michael Chaban
created: 2026-11-08
updated: 2026-11-09
depends_on: [T-0914, T-0915, T-0916, T-0917, T-0918, T-0919]
blocks: []
stories: []
adrs: [ADR-0081]
layers: [backend, ios, android, docs]
security_touching: true
---

## Context

Mike, 2026-11-07, on the customer onboarding screen, the one where a new customer enters her phone number and
birth date:

> "can we try to improve the design of onboarding screen for customer? the one where a customer should input phone
> number and birth date. Can you make a few concepts?"

On 2026-11-08, after seeing concepts A, B and C:

> "Can you combine concept A with C. I like the idea of points, steps and mascots, but also I like the example of
> what your cleaner will see."

He then picked the result, concept D, "Ready to book, with your cleaner's view": "Concept D is nice". The same day
he agreed the scope and the four decisions below with "go".

Concept D keeps concept C's mascot beside the greeting and its readiness card, whose header says what a booking still
needs ("2 of 3 done. Add your phone number and you can book.", then "Ready to book"). Inside the open step it shows
concept A's example of the customer card her cleaner sees: her name, a sample address, Call, SMS and Navigate, and
"Calls and texts go to" with her number filling in as she types.

- The mock is `/Users/michael/cleansia-work.noindex/customer-onboarding-concepts-20261107/concepts/d1-checklist-with-example`.
  Read `NOTES.md` first, then `index.html?s=<state>`, `data.js`, `jobs.json` and `shots/*.png`. Its states are
  `empty`, `typing`, `filled`, `cs-dark`, `sheet`, `apple-private` and `long-email`.
- Mike's review page is https://claude.ai/artifact/RZdT9Qh2gDLsmdMRGEHAYH. Its data is `review/data.json` in the same
  run folder, where concept `d` carries what is better, the build, the estimate and the risks.
- The concept brief and the style sheet are `BRIEF-D.md` and `style-sheet.md` in the run folder.

## Scope

**Doing**

- iOS and Android customer onboarding get the mascot, the checklist card, and the cleaner's example inside the open
  step.
- A phone number counts as done only when it is valid for its country.
- An e-mail that Apple hides shows as "Hidden by Apple".
- "Birthday perks" goes. The birth date note becomes "Optional. Never shown to your cleaner." in every locale.

**Decisions.** The review page asks five questions, and this ticket keeps their numbers. Question 1, "Go with D?", is
answered: D as drawn, without concept A's question title. Mike took the other four as recommended. The scope message
he answered with "go" numbered the same four 1 to 4, in the same order.

2. **The birth date.** Keep asking for it, with the honest note. No perk is built, and the field does not move to Edit
   profile only.
3. **An Apple account with a hidden name.** Onboarding asks for the name. When Apple sends no name, the server stores
   a made-up one ("Cleansia" / "Customer"), so today the name step never shows and the greeting reads "Welcome aboard,
   Cleansia!". The server will tell the app when the stored name is one it made up, which is about half a day of
   server work.
4. **The Android birth date.** Android onboarding opens a month calendar today. It moves to wheels, matching the iOS
   rule of 1 October (owner decision D15: never a month calendar). This adds 1 to 1.5 days.
5. **The disabled button.** It becomes a readable grey with dark text, 6.2:1 instead of 2:1, in both apps. It changes
   every screen, so it goes in its own commit and can be dropped alone.

**Done** means both apps match the mock's states, shown side by side in screenshots; the new logic is tested; CI is
green; and Mike gets a customer TestFlight build.

**Estimate.** iOS about 2.25 days, Android about 3.25 days, the server about half a day.

**Delivery**

- PR A carries the server signal for decision 3 and the iOS half. The server signal is built first, on
  `feature/customer-onboarding-d`, and a DTO change ships in the same commit as its regenerated API clients. The iOS
  half starts after Mike merges #366 and #368, because #368 moves shared iOS Core code that this work edits.
- PR B carries the Android half. It starts after PR 3 (`feature/android-long-term-20261108`, T-0918) merges, because
  PR 3 rewrites the Android `:core` module.
- Part 1 of 3, now: file this ticket; decide, build and check the server signal; open PR A as a draft; and write the
  iOS build brief. Part 1 changes no iOS or Android source except regenerated API client code. Part 2 builds iOS, and
  part 3 builds Android.

## Acceptance criteria

- [ ] **AC1 — the readiness card.** Given a signed-in customer on onboarding, When the screen appears, Then the
  greeting has the mascot beside it (waving while a step is missing, thumbs-up once the card reads "Ready to book"),
  and one card lists Name, Email, Phone number and Birth date under a header that counts the three required steps
  ("2 of 3 done" with one line naming what is missing, then "Ready to book"). Each row is in one of three states:
  done (a check, one line, the label left and the value right), open (a ring, opened in place with its field and the
  example), or waiting (a grey ring, one line, and a full-width tap target that opens it). The open step is chosen
  once, from the saved profile, when the screen appears. On iOS it is the name if the account has none, otherwise the
  phone; typing never moves it, and Next on Last name opens the phone row with its field focused. Android builds no
  name step (open point 2): no account that signs in on Android can carry a placeholder or a blank name, so its open
  step is the phone.
- [ ] **AC2 — a phone valid for its country.** Given the phone step is open, When the number she has typed is not
  yet valid for its country (for example +420 777 12), Then the phone row is not done, the header still asks for the
  phone number, Call and SMS stay dashed, and Save and continue stays disabled. When the number becomes valid (for
  example +420 777 123 456), Then the row's check, the header, the Call and SMS chips and the button switch on
  together. One value decides all of them, and a unit test covers it on each platform.
- [ ] **AC3 — the example fills with her number.** Given the phone step is open, When she types, Then the example
  under the field ("What your cleaner sees", marked Example) shows "Calls and texts go to" with the number as typed,
  with its country code (777 12 shows +420 777 12, and an empty field shows a dash), beside her name, the sample
  address and the Call, SMS and Navigate chips. When the name step is open instead, Then the example shows the name
  as she types it, and a dash while it is empty.
- [ ] **AC4 — Hidden by Apple, and a long e-mail.** Given an account whose e-mail is an Apple relay address (for
  example x7k2p9q8r4@privaterelay.appleid.com), When onboarding shows the Email row, Then the row is done and reads
  "Hidden by Apple", and the relay address appears nowhere on the screen. Given a long e-mail
  (jana.dvorakova.vinohrady.praha@gmail.com at 375 × 812), When the row cannot fit it, Then the local part gives way
  with an ellipsis while the domain stays whole (jana.dvorakova.vin…@gmail.com), and a screen reader reads the whole
  address once.
- [ ] **AC5 — the birth date note.** Given onboarding in any of the five locales (en, cs, sk, uk, ru), When the Birth
  date row shows, Then its note reads "Optional. Never shown to your cleaner." (cs "Nepovinné. Uklízeči se nikdy
  nezobrazí."), and no customer-app string in any locale promises birthday perks.
- [ ] **AC6 — the sheet stores nothing until a wheel moves.** Given the birth date is not set, When she opens its
  sheet and closes it with Done or a swipe down without moving a wheel, Then no birth date is stored and the row still
  reads "Not set". When she moves a wheel, Then the day on the wheels is stored and the row shows it. On Android a
  source test on the sheet and the minified build's emulator walk cover this; the view model never sees the sheet.
  Mike answered open point 1 with A on 2026-11-08, so iOS behaves the same way.
- [ ] **AC7 — the name step for a hidden-name Apple account.** Given an Apple account whose stored first name is
  Cleansia or whose stored last name is Customer, the words the server writes for a part that neither Apple nor the
  e-mail gave, When she reaches the iOS onboarding, Then the server tells the app that the name is made up, and the
  Name step is open with empty First name and Last name fields. The stored name shows nowhere: not in the fields, the
  row, the example, or the greeting, which reads "Welcome aboard!". Save and continue stays disabled until her name
  and a valid phone number are in. When she saves her name, Then the server stores it and no longer reports the name
  as made up. Given an account whose name the person typed or Apple sent, When onboarding appears on either platform,
  Then the Name row is done and shows that name. A hidden-name Apple account whose e-mail gives two name-shaped pieces
  keeps that guess and is not asked: `jana.novakova@seznam.cz` is stored as Jana Novakova, and its Name row is done.
  That is the accepted limit of ADR-0081 D4, and lifting it changes only what the server computes. Android builds no
  name step and does not read the server's signal (open point 2): no Apple account can sign in on Android.
- [ ] **AC8 — screen-reader row states.** Given VoiceOver on iOS or TalkBack on Android, When focus reaches a row of
  the card, Then it reads the label and the value and ends with the row's state: "done", "current step" or "not done"
  (cs "hotovo", "aktuální krok", "nevyplněno"), with the three state words in all five locales. The drawn marks are
  not read, a waiting row does not also read "Not set", and on the open row only the head (mark and label) is one
  element, while the field and the example stay their own. This is checked with the real screen reader on both
  platforms, because the mock's reads were taken from its text only.
- [ ] **AC9 — Android wheels.** Given the Android customer app, When she opens the birth date on onboarding, Then a
  half-height bottom sheet shows day, month and year wheels instead of today's month calendar, with future dates
  blocked and an empty date opening thirty years before today, as on iOS.
- [ ] **AC10 — a readable disabled button.** Given the shared primary button on any screen of the customer and
  partner apps, on iOS and Android, where the screen sets no disabled colours of its own, When the button is
  disabled, Then it has a grey fill and a dark label, as the mock draws it: #475569 on #E2E8F0 in light (6.2:1,
  against about 2:1 on iOS today) and #94A3B8 on #283548 in dark (4.8:1). The change is one commit on each platform,
  so it can be dropped alone.
- [ ] **AC11 — screenshots against the mock.** Given the iOS build from PR A and the Android build from PR B, When
  onboarding is captured in each of the mock's states (empty, typing, filled, cs-dark, sheet, apple-private, and
  long-email at 375 × 812; on Android every state but apple-private, which no Android account can reach), Then each
  capture is shown side by side with the mock's shot of the same state, every difference is named, and the fit holds
  as the mock measures it: nothing scrolls at 390 × 844 in empty, filled, cs-dark and sheet, nor at 375 × 812 in empty
  and long-email.

## Open points for Mike, before the app builds

1. **What Done does in the birth date sheet.** The mock's sheet has a Done that only closes, and its notes say iOS
   behaves this way today. It does not. Since the fix plan of 2026-10-25, the iOS sheet has Cancel and Done, and Done
   stores the day on the wheels: on an empty field, the day shown, untouched (`BirthDateField.swift:92-101`,
   `CalendarDay.swift:47-50`, `docs/mobile-app/patterns.md:1375-1385`). That change was made so that the day shown
   can be taken without moving a wheel. AC6 as agreed reverses it on iOS, and the sheet is shared with the customer's
   Edit profile and the partner's personal details. The choice is AC6 as agreed, or today's iOS Done kept and matched
   on Android. **Answered 2026-11-08:** Mike, *"Q1: Go with A"*, so AC6 as agreed.
2. **The name step on Android.** Android has no Sign in with Apple
   (`docs/flows/auth-and-identity.md#native-account-only`), and only Apple sign-in makes up a name, so no Android
   account reaches the `apple-private` state today. About half a day of the Android estimate builds that name path.
   The choice is whether PR B builds it. **Answered 2026-11-08:** Mike, *"Open Point 2: no"*, so PR B builds no name
   step.

## Out of scope

- The other fixes in `/Users/michael/cleansia-work.noindex/apple-email-check-20261108/REPORT.md`: relay delivery,
  the delete-screen retype, the name dropped at sign-in, the Apple token revoke and the rest stay Mike's separate
  call. This ticket takes only the onboarding side of its item 2 (the made-up name) and item 12 (birthday perks). The
  made-up name on the cleaner's job card, the receipts and the PDFs, the raw relay address on the profile tab, and
  the server's relay detection (item 7) stay there.
- A real birthday perk.
- Web onboarding.
- The partner app's phone field. The shared phone field's new options are off by default, so the partner app does
  not change.
- The brand button's white label, 4.1:1 on the brand fill.
- Concept A's question title.
- Android's other birth date pickers, which still open a month calendar: the customer's Edit profile
  (`EditProfileScreen.kt:510`) and the partner's personal details (`PersonalSectionScreen.kt:440`). They are reported
  for Mike's call.

## Implementation notes

- iOS: `CleansiaCustomer/Sources/Features/Profile/ProfileOnboardingView.swift` and `ProfileViewModel.swift`. Today
  the screen opens with a centred greeting and subtitle and no mascot (`ProfileOnboardingView.swift:123-140`), and any
  non-blank phone counts (`ProfileViewModel.swift:154-156`). #368 edits `ProfileViewModel.swift`.
- Android: `customer-app/.../features/profile/ProfileOnboardingScreen.kt`. Today it has no name fields and no
  mascot, any non-blank phone counts (`:81`), and the birth date opens a month calendar (`:319-341`).
- Both mascot images already ship: `Mascot.waving` and `Mascot.thumbsUp` in iOS Core, and `mascot_waving` and
  `mascot_thumbs_up` in the Android customer app's `drawable-nodpi`.
- Both platforms already have a country-aware phone check, which the partner profile uses: `isPossiblePhoneNumber`
  (Android `:core`, `CleansiaPhoneInput.kt:155`) and `PhoneNumberKitDisplayFormatter.isPossibleNumber` (iOS Core,
  `PhoneNumberKitDisplayFormatter.swift:32`). It tests a possible length for the country rather than full validity, on
  purpose: the server's `PhoneNumberAttribute` is the judge, so a newly allocated range is never refused on the phone.
  The iOS build brief says which check "valid for its country" uses.
- The shared primary button already takes per-screen disabled colours on both platforms (`CleansiaButton.swift`,
  `CleansiaButton.kt`). Decision 5 changes its default.
- The made-up name is `AppleAuth.cs:99-100` ("Cleansia" / "Customer"). The comment above it (`:94-98`) says these
  values are the only evidence `User.ReplaceSystemGeneratedName` has that a name was made up. When Apple sends no name
  and the e-mail is not a relay address, the server builds a name from the e-mail instead (`DeriveNameFromEmail`), and
  the server signal has to say on which side such a name falls.
- The concept recognises a relay address by the `@privaterelay.appleid.com` suffix, the one suffix the server checks
  too (`AppleAuth.cs:92`).
- Strings: `NOTES.md` lists the new keys in en and cs. The sk, uk and ru texts are not written yet, and every new key
  ships in all five locales.

## Work record

- 2026-11-08 — Filed. Checked on `feature/customer-onboarding-d` at `3e720a7e7` (origin/master): every gap the scope
  names is still in the tree. Both apps count any non-blank phone as done. "Optional — for birthday perks." is
  `onboarding_birthdate_helper` on both platforms, in all five locales. No mobile code knows the relay domain (`git
  grep privaterelay` finds nothing in the two mobile trees). The server's made-up name is "Cleansia" / "Customer".
  Android onboarding has no name fields and opens a month calendar, and neither app shows a mascot on onboarding. The
  commands and their output are in `/Users/michael/cleansia-work.noindex/onboarding-d-20261108/ticket-ground-truth.txt`.
- 2026-11-08 — Built the server signal for decision 3, as
  `/Users/michael/cleansia-work.noindex/onboarding-d-20261108/design.md` decides. `GET api/User/GetCurrent` on the
  customer and partner hosts, web and mobile, now carries `isNamePlaceholder`, computed on every read by
  `User.IsNamePlaceholder()`, with no column and no migration (`has-pending-model-changes` reports none). The
  placeholder words moved from `AppleAuth` to `User.PlaceholderFirstName` and `User.PlaceholderLastName`, so the
  implementation note's `AppleAuth.cs:99-100` and `:94-98` now live in `User.cs`. Before the change, both mobile
  specs re-dumped and both NSwag clients regenerated byte-identical to the committed files; after it, each differs
  by the one field, and the regenerated iOS clients differ by the one optional member. The new theory and the two
  handler assertions failed against a stub first, then passed. On the final tree the Release build had 0 errors;
  `Cleansia.Tests` passed 9195 of 9195, `Cleansia.IntegrationTests` 1042 of 1042 and `Cleansia.HostTests` 518 of
  518; the web unit tests of the 71 projects the two clients affect passed 5325 of 5325, and the three web
  production builds succeeded; the Android `ProfileWireTest` passed 10 of 10 and `UserProfileWireTest` 7 of 7; and
  both client typechecks, `typecheck:test`, `typecheck:specs`, `check-module-boundaries`, `check-docs-refs`,
  `check-catalog-claims` and `check-backlog-consistency` passed. ADR-0081 and the docs section are not written yet.
- 2026-11-08 — Wrote the docs for the server signal. ADR-0081 (`proposed`) records the member, its rule, why it
  is computed on every read rather than stored, and its one named exception to ADR-0048 D1 rule 2.
  `docs/flows/auth-and-identity.md` gains the section *An Apple account's name* (`#apple-account-name`) and a
  clause in its edge-cases row for Google or Apple sign-in. The decisions index gains the row and one sentence of
  supersession prose, the sidebar gains the entry, `agents/architecture/decisions/mobile-wire-contract.md` gains
  the exception under *Current shape*, and `User.IsNamePlaceholder()` points at the new section. The index's
  record count read 75 while 80 records existed, so it now reads 81 rather than 76. Before the commit,
  `check-docs-refs` (445 references, 0 unresolved), `check-catalog-claims`, `check-backlog-consistency` and the
  VitePress site build passed.
- 2026-11-08 — Brought the ticket, ADR-0081 and the auth flow page in line with the decided rule, and fixed one
  test row. AC7 now names the rule, an Apple account whose stored first name is Cleansia or last name is Customer,
  and its accepted limit from ADR-0081 D4: a hidden-name account whose e-mail gives two name-shaped pieces keeps
  that guess and is not asked. ADR-0081 and the flow page now say that `true` means at least one stored part is a
  word the server wrote, that the onboarding, not Edit profile, leaves the whole stored name out, and that only the
  iOS onboarding greets a hidden-name Apple customer as Cleansia, because Android has no Sign in with Apple. The
  theory on `User.IsNamePlaceholder()` builds its two-word row on `jane.doe@example.com`, whose derived name is Jane
  Doe, so the row pins an e-mail-built name as given. The citations this branch moved now read `User.cs:409` on the
  security law, the flow page and the owner plate, and `UserMappers.cs:23,67` in the user-uploaded-artifacts note;
  ADR-0043 keeps its own as an accepted record. Part 1 also edited two Android test sources by hand, besides the
  regenerated client code: `ProfileWireTest` and `UserProfileWireTest` each gained the member in their pinned
  property set and captured payload, because both pin the re-dumped spec. On the final tree the Release build had 0
  errors; `Cleansia.Tests` passed 9195 of 9195, `Cleansia.IntegrationTests` 1042 of 1042 and `Cleansia.HostTests`
  518 of 518; `has-pending-model-changes` reported none; the three web typechecks passed; the affected web tests
  (5325 of 5325 over 71 projects) and the three web production builds were replayed from the Nx cache, and the two
  Android wire tests (10 of 10 and 7 of 7) were up to date, because none of their inputs changed; and
  `check-docs-refs`, `check-catalog-claims`, `check-backlog-consistency`, `check-module-boundaries`, their
  self-tests and the VitePress site build passed.
- 2026-11-08 — Mike answered both open points. Open point 1: *"Q1: Go with A"*, so AC6 stands as agreed: the wheels
  store the day as they move, Done only closes, there is no Cancel, an untouched sheet stores nothing, and the
  customer's Edit profile and the partner's personal details change with it. Open point 2: *"Open Point 2: no"*, so
  PR B builds no name step. AC1's name clause, AC7 and AC11's Android capture now say what each platform builds, and
  ADR-0081's Applies-to line and D5 no longer name an Android mapper. The Android build brief's Q9 was settled yes
  the same day: on Android, AC6 is proven by a source test on the sheet and by the minified build's emulator walk,
  not through the view model, which never sees the sheet. PR B's records reword AC6's last sentence with that proof.
  - ADR-0018 D1: the Android onboarding has no name step, because no account that signs in on Android can carry a
    placeholder or a blank name (open point 2, answered "no" by Mike on 2026-11-08); every state an Android account
    can reach matches iOS.
- 2026-11-08 — Built the iOS half of PR A to `/Users/michael/cleansia-work.noindex/onboarding-d-20261108/BRIEF-build-ios.md`,
  with Mike's answers of the day: Q1 "Go with A"; Q2, Q5, Q6 and Q7 ship the brief's defaults; Q3, no DEV deploy;
  Q4, no live AXe captures, so the hosted typing and sheet frames stand.
  - **Commits.** `65a699506` the profile carries `isNamePlaceholder` (absent reads `false`) and
    `isEmailHiddenByApple`; `c3b7773be` the shared text and phone fields gain label-above, Next, the `+420` prefix and
    autoFocus, all off by default; `52f87a4ea` the birth date wheels store a day only once one moves and Done only
    closes, so customer Edit profile and the partner's personal details lose Cancel with it; `8b3337d44` the
    onboarding, concept D; `62b5b9d45` the grey disabled button, alone; and this record.
  - **The screen.** The mascot waves beside the greeting while a step is missing and gives a thumbs-up at "Ready to
    book". One card counts name, e-mail and phone, names what is missing, and draws a row per step: done, open (with
    its field and the cleaner's view, the example) or waiting. The open step is picked once from the saved profile:
    the name when the server reports its stand-in for a hidden Apple name or a stored part is blank, otherwise the
    phone. A phone counts only when it is a possible number for its country, and one `OnboardingChecklist` feeds the
    marks, the count, the prompt, Call and SMS, the mascot and Save. An Apple relay address reads "Hidden by Apple".
    The birth date is a fourth, optional row with the honest note in five locales, opening the shared wheels.
  - **Where the build departs from the brief, each for a reason.** (1) The example well is padded 4 pt top and bottom,
    the brief's first fit fallback: with 8 pt the 375 × 812 `empty` and `long-email` states scrolled by 6 pt (content
    602.3 against 596.5). (2) A done row carries a row-sized content shape: without it, its combined VoiceOver
    element was the 24 pt of its words, not the 44 pt row. (3) A row's value takes the width the label leaves,
    instead of a `Spacer`, which cost 32 pt between label and value and cut a long address early. (4) The example's
    dial and the done phone row format with a named region: on the iOS 16.4 simulator `Locale.current.region` reads
    the override `en_US@rg=czzzzz` as `czzzzz`, and Core's handset-region engine then formats nothing
    (`part2/logs/probe-region-16.log`). (5) Both shared fields re-run their focus once they land in a window, the
    brief's named fallback for risk 3: on iOS 16.4, Next on Last name left the phone unfocused
    (`part2/logs/red-ProfileOnboardingLayoutTests-16.log`); the text field takes the same fix because the waiting Name
    row focuses First name. (6) The Ukrainian copy uses the catalog's apostrophe, `’`, as its three existing
    "Необов’язково" do.
  - **Named differences from the mock** are listed on the side-by-side page,
    `/Users/michael/cleansia-work.noindex/onboarding-d-20261108/part2/side-by-side-ios.html` (one file, every image
    inlined): the 56 pt button, the 24 pt card radius, the resting field border, the medium date style, the wheels
    opening thirty years before the capture day, the stand-in keypad, the chips at their own width and as filled
    tints, the footer rule only while the keyboard is up, the dark waiting ring in slate-400, Skip and the labels one
    weight up, the values one size up, the greeting's line break, the 4 pt well and the composed sheet without a
    grabber.
  - **Tests first.** Red runs, in `/Users/michael/cleansia-work.noindex/onboarding-d-20261108/part2/logs/`: from
    commits 1 to 3, `red-UserProfileClientMappingTests.log`,
    `red-CleansiaPhoneInputPrefixTests-CleansiaTextFieldInputTraitsTests-PhoneNumberKitDisplayFormatterTests.log` and
    `red-BirthDateFieldSheetTests.log`, with `sheet-16.log` and `sheet-26.log`; from commit 4,
    `red-ProfileViewModelTests.log` (Save took `+42077712` and `+420111`), `red-OnboardingChecklistTests.log` (the
    build), `red-ProfileOnboardingLayoutTests.log` (five of six layout tests and the birth date note red on the old
    screen) and `red-ProfileOnboardingLayoutTests-16.log` (the focus chain and the dial on iOS 16.4); from commit 5,
    `red-disabled-button.log` (the build). Green on their first run, so proving less: in part A,
    `testABlankFirstNameOrAMissingEmailConfirmationIsRefused` and the untouched half of the hosted sheet test; here,
    the fit test (the old screen fits too) and Core's `+42077712` impossible pin (PhoneNumberKit already calls it
    impossible, so AC2's example holds).
  - **Evidence.** 44 frames on each simulator, PNG and a VoiceOver `.semantics` file each, in
    `/Users/michael/cleansia-work.noindex/onboarding-d-20261108/part2/captures/onboarding-16/` (iOS 16.4) and
    `onboarding-26/` (iOS 26.3). The layout tests pass on both simulators. The gates:
    Core Debug 985 of 985; Customer Debug 1968 run, 21 skipped, 0 failures; Partner Debug 1422 run, 36 skipped, 0
    failures; the same three at `-O`, whole-module, with `MallocScribble`, with the same totals; all on iOS 26.3,
    and the three catalogs and both `Package.resolved` files unchanged after every run (`part2/logs/gates-b.status`).
  - **Decision 5** is `62b5b9d45` alone, with its docs (`docs/mobile-app/patterns.md#disabled-button`) and its tests,
    so `git revert 62b5b9d45` drops it. Its commit body lists the 36 call sites it changes (Core 1, customer 21,
    partner 14); the other 28 pass neither `enabled` nor `loading` and never change.
  - ADR-0018 D1: until PR B merges, the iOS customer onboarding differs from Android's (mascot, checklist card,
    example, grey disabled button).
  - Reported, not rewritten: the implementation note's phone sentence, *"the server's `PhoneNumberAttribute` is the
    judge"*, is wrong for the customer path. `UpdateCurrentUser` checks the phone only for uniqueness
    (`UpdateCurrentUser.cs:62-66`), and `[PhoneNumber]` on `User.PhoneNumber` is not enforced on save; T-0921 F1
    takes it up.
  - ADR-0081 moves to `accepted` on instruction of 2026-11-08, with its *What shipped* note; the decisions
    index follows.
  - Left for Mike before PR A leaves draft: the routed `patterns-mobile.md:1016` row (Review, below); the copy flags
    of the brief's section 6.2 (the Slovak written to match the mock's Czech, the Ukrainian and Russian texts, and
    Q7's verb); and AC8 on a phone with VoiceOver, against the reads `ProfileOnboardingLayoutTests` pins.
- 2026-11-09 — From the review of PR A. `6f07824d2`: a hosted test types national digits into the empty `+420` phone
  field and expects them stored behind the code. AC2 and AC3 rest on that setter, and no test reached it: with it
  stubbed to store the digits as typed, the test failed on the iOS 26.3 simulator; with the real one it passes. Routed
  for ratification before PR A leaves draft, with the `:1016` row: T-0921 F13's address-picker row (`:1046`) and
  devices bullet (`:2041`), and whether `:2063` still holds (Review, below). The `:1016` draft now names the simulator
  CI hosts its tests on; the iOS 16.4 run was local.
- 2026-11-09 — Architect ruling, 2026-11-09: `patterns-mobile.md:1016`, the `BirthDateField` row, is ratified and
  applied from the `## Review` draft, with three fixes to match the tree: the `in: ...Date()` future-block belongs to
  the sheet (`BirthDateField.swift:113`), the onboarding is the sheet's one caller besides the field outside the
  tests, and the unguarded environment pin sits in `BirthDateWheelSheet`. Test 2 fires and is answered: Mike's Q1
  "Go with A" set the sheet, the one shared sheet keeps the binding, the Greenwich pin, the seed and the future-block
  in one place, and `birth_date_picker_is_the_shared_one` (T1-CI, `swiftlint lint --strict`) still enforces the row,
  with its three residuals named. Test 1 finds no deviation (the `## Review` sweep, rerun at `8f0a53252`).
- 2026-11-09 — Architect ruling, 2026-11-09: the *No auth header image* bullet (`patterns-mobile.md:2063` as routed)
  still holds and stays as written. It defers its list to `docs/mobile-app/patterns.md#auth-screens`, which since
  commit 4 records Mike's pick of concept D (2026-11-08) and no longer counts the profile completion as an auth
  screen; at `8f0a53252` no file under either app's `Features/Auth` draws a mascot, and `AuthHeaderImage` is gone.
  No catalog edit, so nothing routes.
- 2026-11-09 — Shipped, the server signal and the iOS half: #369 merged to master as 45146bd5d.
- 2026-11-08 — Built T-0920's Android half, PR B (#371), on `feature/customer-onboarding-d-android`, from master at
  5b57d65b2 after #370. The customer onboarding shows the mascot (waving, a thumbs-up at "Ready to book"), the
  readiness card with done, open and waiting rows, and the cleaner's example in the phone step. A phone counts as done
  only when libphonenumber holds its length possible for its country (`isPossiblePhoneNumber`, Q6's default). The
  shared phone field can draw `+420` before the national digits, off by default, so the partner's two phone fields did
  not change; tapping the prefix switches to the free form and keeps the digits (Q2's default). An Apple relay e-mail
  reads "Hidden by Apple", and a long e-mail keeps its domain. The birth date opens the system's day, month and year
  wheels in a half-height sheet: a wheel stores its day as it moves, Done only closes, there is no Cancel, and an
  untouched sheet stores nothing (Q1, answered A by Mike on 2026-11-08). "Birthday perks" is gone in all five locales.
  TalkBack reads each row once, ending with its state, and the `+420` prefix once. Commits 68a654c67, 1e8878664,
  58cac494a, 2bd7b977f, d3450ec05 and 97a7ce140. Tests: core 419, partner 1270 and customer 1853 (3542) before;
  425, 1270 and 1865 (3560) at d3450ec05, 0 failures; at 97a7ce140, with T-0922's fixes in, 426, 1271 and 1866
  (3563), 0 failures. Red runs: `part3/logs/red-CleansiaPhoneInputPrefixTest.log`,
  `red-ProfileOnboardingScreenTest-compile.log`, `red-ProfileOnboardingScreenTest-ac6.log`,
  `red-OnboardingChecklistTest-compile.log`, `red-ProfileOnboardingScreenTest-c3-pins.log`,
  `red-PrimaryTextTest-compile.log`, `red-CleansiaButtonTest.log`, `red-ProfileOnboardingScreenTest-ac8-cleared.log`
  and `fix-red-rev3.log`.
- 2026-11-08 — The grey disabled button, commit 2bd7b977f, alone: slate-600 on slate-200 (6.15:1) and slate-400 on
  `#283548` (4.84:1), the loading spinner in the label's ink; 31 call sites in the two apps change only while disabled
  or loading (listed in the commit); the five customer screens that draw Material's `Button` keep their own colours.
  Shot before and after in one emulator session: every changed app pixel sits inside a button's bounds.
- 2026-11-08 — TalkBack, read from its own speech log on the API 35 emulator, said each row's texts again after its
  state ("Name, Jana Dvořáková, done. Name. Jana Dvořáková"). The rows now clear their texts and keep the one
  description (d3450ec05), as the brief's section 7.4 prescribes; it now says "Name, Jana Dvořáková, done", and the
  birth date row "Birth date, Optional. Never shown to your cleaner., not done", then its role, Button. The `+420`
  prefix still read its code twice ("Country code +420. +420. Button"); 97a7ce140 clears its text the same way, and
  TalkBack now says "Country code +420. Button" (`talkback/97a7ce140-FxOnboardingEmpty`).
- The ADR-0018 D1 line for the Android name step is already in this ticket's work record (PR A, 2026-11-08). Component
  differences: Android's wheels are the system spinner; Android's Edit profile and the partner's personal details keep
  Material's calendar (Out of scope).
- AC6 on Android is proven by a source test on the sheet (`ProfileOnboardingScreenTest`) and the minified build's
  emulator walk, not through the view model, which never sees the sheet; AC6's last sentence now says so, as the PM
  agreed on 2026-11-08 (Q9).
- Evidence (out of tree, `/Users/michael/cleansia-work.noindex/onboarding-d-20261108/android/part3/`):
  `side-by-side-android.html`, `captures/` (34 frames on API 35 and 2 on API 30, the decision-5 before and after),
  `talkback/`, `walk/` (the staging start and stand-in walk screenshots), `logs/`. At 97a7ce140 (PR B's code head,
  F9 to F11 included) both staging builds start with an empty crash buffer (`walk/start-gate-97a7ce140.json`), and
  `check-r8-monitor.py` over their dex rejects 0: partner 828 methods with a monitor of 41006 parsed, 99 of them with
  switch targets unchecked; customer 907 of 63103, 107. The APK for a phone: `apk/customer-staging-97a7ce140.apk`,
  SHA-256 3abcbae0e5e27cb5ce1490933b5c98ceabf6462b34bceca9178b9a8b3796700e.
- Gates: compile and unit tests of the three modules (3563 at 97a7ce140, 0 failures); `check-consistency mobile` (the one E1 at
  `OrdersListViewModel.kt:92`, unchanged); `check-docs-refs` (0 unresolved); `check-catalog-claims` (C1 0, C2 0, C3 0);
  `check-backlog-consistency` (C1 0, C2 0, reach 0); every PR check (Android CI, iOS CI, Docs CI, Frontend CI, Booking policy parity, Secret scan) passed on its first run at b5c050eed, #371.
- 2026-11-09 — Architect ruling, 2026-11-09: `patterns-mobile.md:1030`, the birth-date row's Android column, is
  ratified and applied from PR B's routed draft, with one fix to match the tree: the customer Edit profile's dialog has
  no future-block (`EditProfileScreen.kt:509`), so the column now says so. Test 1 finds no deviation:
  `git grep -n -e DatePickerDialog -e android.widget.DatePicker -- src/cleansia_android` finds the partner's personal
  details, Edit profile, the onboarding's sheet and the recurring schedule's *Starts on* (not a birth date), each as
  the column names it. Test 2 fires and is answered by Mike's Q1 "Go with A" and AC9; the column obliges no Android
  call site, and `ProfileOnboardingScreenTest` pins the seed, the listener, Done and the missing Cancel.
- 2026-11-09 — Architect ruling, 2026-11-09: `ios-app-architecture.md:82`, the date-picker row, takes PR B's routed
  Android clause. Its iOS clauses, which #369 did not route, still described the sheet before Q1 (a draft that Done
  wrote, the day shown on an untouched empty field, and Cancel); they now match `52f87a4ea`, because the divergence
  sentence cannot be true otherwise: a wheel writes as it moves, Done only closes, there is no Cancel, and on an
  untouched empty field iOS and the Android customer store nothing, against the Android partner's Confirm. The stale
  `ProfileOnboardingScreen.kt:319-330` citation is gone; `check-catalog-claims` does not scan this file, so nothing
  gates its citations.

## Review

Catalog-edit routing for the docs and catalog edits of the iOS half, by the test of `conventions.md`
(*Who ratifies a catalog edit*), with the sweeps and searches it asks for.

- **`docs/mobile-app/patterns.md#auth-screens`, the profile completion takes the mascot (commit 4).** Searched
  `mascot` and `auth screen` (case-insensitive) in `agents/knowledge/patterns-mobile.md` and
  `agents/knowledge/consistency.md`. `consistency.md` has neither. In `patterns-mobile.md`, `mascot` finds the asset
  and puck rule (553-580), the empty states (824), the asset row (1060), the decode note (1095), the point-size note
  (1396), the order pieces row (1017) and the component lists (21, 385), none about which screens draw one; `auth
  screen` finds one sentence, 2062-2063: *"No auth header image (owner ruling 2026-10-01): `AuthHeaderImage` is
  deleted, and no auth screen in either app draws a header image or a mascot; do not add one back →
  `docs/mobile-app/patterns.md#auth-screens`."* Two readings, as `conventions.md` asks for a hard case: (1) the page
  that sentence defers to listed the profile completion among the auth screens, so the sentence governed it, and
  the carve-out is Mike's ruling of 2026-11-08 (concept D, mascot included), written in the page the sentence defers
  to; (2) "auth screen" in the catalog sentence never meant this form, which asks for what a booking needs, not for
  a sign-in. Under either reading the catalog line stays as it is and the owner's ruling is what changed the list;
  no catalog sentence is edited. Since the review of PR A (2026-11-09), the ratification of the rows below also
  confirms that this sentence still matches Mike's ruling.
- **`docs/mobile-app/patterns.md#disabled-button` (commit 5).** A first statement. `git grep -n -i disabled` over
  `patterns-mobile.md`, `consistency.md` and `docs/mobile-app` finds rules on fields (`patterns.md:917`), alert
  buttons (`patterns-mobile.md:772-775`, `patterns.md:1349-1354`), confirm guards (640, 769), slides (1910, 1929),
  a placeholder photo section (1577), an image cache (970), the invoice viewer (1054) and an input modifier (1138);
  none governs a filled button's disabled colours. Inline; it obliges no call site beyond the shared button itself.
- **`agents/knowledge/patterns-mobile.md`, the address-picker row and the devices bullet (T-0921 F13).** Test 1:
  `git grep -n "import MapKit" -- src/cleansia_ios/CleansiaCustomer/Sources src/cleansia_ios/CleansiaPartner/Sources`
  finds nothing; the one `pickerMap` caller is Core's `AddressPickerScreen`; `CleansiaCore/Sources/CleansiaCore/Devices`
  holds the client protocol, view and view model, and the customer's `DevicesView.swift` holds only an initializer.
  No call site becomes a deviation. Test 2, two readings: (1) the replaced sentences, *"the Views stay app-local and
  carry the only sanctioned feature-layer `import MapKit`"* and *"a customer-local
  `CustomerDevicesViewModel`/`CustomerDevicesView`"*, governed where these views live, and the test counts replacing
  a governing sentence as narrowing, so the edit routes; (2) #368, merged by Mike on 2026-11-08, already moved both
  into Core and its commits rewrote the docs pages to say so (`6a1ccf05d`, `dd5719d42`), so the edit withdraws two
  sentences the tree contradicts and adds no constraint the tree does not already hold. Edited inline under reading
  2, as the instruction "correct both to the tree" reads it; under reading 1 it routes per ADR-0033. Test 3:
  iOS only, built and run here. It adds no constraint, so it names no enforcer. The review of PR A (2026-11-09) reads
  them under reading 1, so both rows are routed with the `:1016` row below and stay as edited until ratified.
- **`agents/knowledge/patterns-mobile.md:1016`, the `BirthDateField` row (T-0921 F13): routed per ADR-0033, not
  edited.** It describes the sheet commit 3 replaced (a draft, Cancel, a Done that stores `CalendarDay.confirmed`,
  the `NavigationStack` and the tint), and commit 4 has the onboarding present the now public
  `BirthDateWheelSheet` from a row of its own. Rewriting it replaces the sentence that governs the sheet and carves
  an exception out of *"the ONE tappable birth-date control for both apps"*, so test 2 fires. Sweep for test 1:
  `git grep -n "BirthDateField(\|BirthDateWheelSheet(" -- src/cleansia_ios` finds Edit profile, the partner's
  personal details, the field's own sheet and the onboarding. Draft of the row's right-hand cell, for ratification:

  > the Core **`BirthDateField`** (`Components/BirthDateField.swift`) — the ONE tappable birth-date **field** for
  > both apps (`label:`/`placeholder:`/optional `errorText:` params, medium date formatted via the environment
  > `\.locale` so the in-app language switch re-renders it, `in: ...Date()` future-block), and its sheet, the public
  > **`BirthDateWheelSheet(birthDate:title:note:)`** in the same file: a `.wheel` `DatePicker` with its label hidden,
  > at the `.medium` detent with the drag indicator, under the title and a **Done** that only closes (its 44 pt frame
  > inside the `.plain` button). A wheel that moves stores its day at once; a sheet closed untouched stores nothing;
  > an empty field's wheels show a day thirty years back through `CalendarDay.pickerBinding`'s getter
  > (`CalendarDay.unsetSeed`), taken only by moving a wheel. There is no Cancel (Mike, 2026-11-08, Q1 "Go with A";
  > from the fix plan of 2026-10-25 until then a draft, Cancel and a Done that stored the day shown; a `.graphical`
  > calendar seeded at today until owner decision D15, 2026-10-01). **The customer onboarding presents
  > `BirthDateWheelSheet` from its own birth date row**, the one caller that is not `BirthDateField`; a screen that
  > wants the wheels presents this sheet, never a `DatePicker` of its own. `BirthDateFieldSheetTests` and
  > `CalendarDayTests` (`CleansiaCoreTests`) pin the binding, Done and the untouched sheet, hosted on the iOS 26
  > simulator CI runs. A per-app private date field or wheel sheet is a defect. **Enforced by:** the swiftlint custom
  > rule `birth_date_picker_is_the_shared_one` (`src/cleansia_ios/.swiftlint.yml`) — **T1-CI**, with its three
  > residuals as today.
- 2026-11-09 — Shipped, the Android half: #371 merged to master as 08edb60f6. Customer TestFlight 1.0.0 (20), built from f4dca6526 and tagged `ios/customer/1.0.0-20`, is processed, with its dSYMs on Crashlytics. DEV still runs a server without `isNamePlaceholder` until a DEV deploy, so that build shows the hidden-name step only after one.
