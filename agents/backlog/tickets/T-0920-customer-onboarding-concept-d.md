---
id: T-0920
title: "Customer onboarding: concept D, ready to book with your cleaner's view"
size: L
owner: Michael Chaban
created: 2026-11-08
updated: 2026-11-08
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
  once, from the saved profile, when the screen appears: the name if the account has none, otherwise the phone.
  Typing never moves it, and Next on Last name opens the phone row with its field focused.
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
  reads "Not set". When she moves a wheel, Then the day on the wheels is stored and the row shows it. On Android a JVM
  test covers this through the view model. See open point 1: iOS stores the day shown on Done today.
- [ ] **AC7 — the name step for a hidden-name Apple account.** Given an Apple account whose stored name the server
  made up because Apple sent none, When she reaches onboarding, Then the server tells the app that the name is made
  up, and the Name step is open with empty First name and Last name fields. The made-up name shows nowhere: not in the
  fields, the row, the example, or the greeting, which reads "Welcome aboard!". Save and continue stays disabled until
  her name and a valid phone number are in. When she saves her name, Then the server stores it and no longer reports
  the name as made up. Given an account whose name the person typed or Apple sent, When onboarding appears, Then the
  Name row is done and shows that name.
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
  long-email at 375 × 812), Then each capture is shown side by side with the mock's shot of the same state, every
  difference is named, and the fit holds as the mock measures it: nothing scrolls at 390 × 844 in empty, filled,
  cs-dark and sheet, nor at 375 × 812 in empty and long-email.

## Open points for Mike, before the app builds

1. **What Done does in the birth date sheet.** The mock's sheet has a Done that only closes, and its notes say iOS
   behaves this way today. It does not. Since the fix plan of 2026-10-25, the iOS sheet has Cancel and Done, and Done
   stores the day on the wheels: on an empty field, the day shown, untouched (`BirthDateField.swift:92-101`,
   `CalendarDay.swift:47-50`, `docs/mobile-app/patterns.md:1375-1385`). That change was made so that the day shown
   can be taken without moving a wheel. AC6 as agreed reverses it on iOS, and the sheet is shared with the customer's
   Edit profile and the partner's personal details. The choice is AC6 as agreed, or today's iOS Done kept and matched
   on Android.
2. **The name step on Android.** Android has no Sign in with Apple
   (`docs/flows/auth-and-identity.md#native-account-only`), and only Apple sign-in makes up a name, so no Android
   account reaches the `apple-private` state today. About half a day of the Android estimate builds that name path.
   The choice is whether PR B builds it.

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
