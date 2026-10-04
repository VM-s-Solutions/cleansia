# Shared patterns

Conventions both native apps follow. They repeat across dozens of files, so they are stated once here
and the code carries the warning plus a pointer.

## A cached repository must be wiped on sign-out {#session-wipe}

Repositories that cache per-user data live for the app process. On a shared handset that makes them a
leak: **the next account inherits the previous one's data unless the cache is explicitly cleared.**

Every such cache is wired into the sign-out, forced-401 and account-delete paths. A new one that is not
in that set is a defect, and it is the kind nobody notices until two people use one phone.

## The error model {#error-model}

Operations return a success or an error carrying the parsed message; the consuming ViewModel raises the
snackbar.

**A network failure stays silent at the call site** — the network interceptor owns that toast, and
showing both produces two messages for one failure.

## What counts as a real network failure {#cancellation-noise}

When a screen unmounts mid-fetch — a fast tab switch, a pop on forced sign-out, the app backgrounding —
the coroutine cancels and OkHttp surfaces it as one of several `IOException` subtypes.

The cancel flag is set **asynchronously**, so checking it alone misses cases where the exception is
thrown before the flag propagates. Additional signals are needed: a message containing *canceled* or
*closed*, an `InterruptedIOException`, and a socket closed underneath a cancelled call.

Get this wrong and every fast tab switch shows the user an infrastructure error.

## Only a successful answer is cached {#negative-caching}

Reference data fetched lazily — serviced countries and cities, catalogues — caches **only on success**.

A failed fetch returns null and is not cached, so the next access retries. Caching the failure pinned an
empty list until force-stop, which made the address picker tell every user *"we don't serve this city"*
after a single startup-time network blip.

## Snackbar insets are a stack, not a value {#snackbar-inset}

Screens with persistent bottom chrome raise the snackbar above it. The host lives at the root of the
composition — outside the nav graph — so a composition local provided further down does not flow *up*
to it; a shared flow does.

The state is a **stack of owned entries** because several scopes can be alive at once (a bottom-nav
shell under a modal sheet is the everyday case). A scope going away must restore whatever is still
active underneath it rather than resetting to the default.

## Spacing is a plain object, not a composition local {#spacing}

Layout values never differ between themes, so a theme lookup costs runtime for no benefit.

New code uses the scale. Existing screens keep their literals until touched for another reason — a
blanket find-and-replace produces visual regressions that are hard to QA.

## Image upload happens off the main thread {#image-upload}

The picker callback is dispatched on the main thread, so reading and encoding a multi-megabyte photo
inline froze the UI. Compression hops to a background dispatcher and encodes in the same hop.

The uploading flag is set **before compression rather than at the network call** — set later, the user
gets a second of dead tap, which is the same freeze without the frame drops.

Uploads are single-flight guarded, matching iOS.

## Auth screens carry no mascot {#auth-screens}

No auth screen in either app draws the mascot (owner ruling 2026-10-01). In the customer apps that
means sign-in, sign-up, forgot password, e-mail confirmation and the profile completion after a first
sign-in. In the partner apps it means sign-in, registration, e-mail confirmation and forgot password.
The partner app's pre-sign-in intro carousel keeps its two mascots, because it introduces the app
rather than asking for anything. Nothing replaced the mascot: its height went to the form. Both
platforms changed together, so the branding parity of [ADR-0018](/decisions/adr-0018) still holds,
and the mascot is unchanged everywhere else.

**The customer sign-in fits one screen.** Without the mascot and the guest link the form fits every
supported iPhone, the SE included, and ordinary Android phones; only the legacy 360×640dp size still
scrolls. The scroll view stays on both platforms, because the keyboard, field errors and large text
still need it. On iOS, `CenteredAuthScroll` (the one container the centred auth forms of both apps
share, in CleansiaCore since 2026-10-03) sets
`scrollBounceBehavior(.basedOnSize)` from iOS 16.4, so a form that fits neither rubber-bands nor shows
a scroll indicator. Do not reach for `ViewThatFits` here: when the keyboard changes the available
space the view switches branch, the focused field is recreated, the keyboard drops, and the cycle
repeats. Android keeps the form top-anchored and pads it by the real system bars and keyboard
(`systemBarsPadding().imePadding()` ahead of `verticalScroll`) instead of a fixed 64dp. The activity is
edge-to-edge, so once the form fits there is no scroll range left to bring a field out from under the
navigation bar or the keyboard.

**Every customer auth screen sits the same way** (since 2026-10-02). On iOS, e-mail confirmation sits in
`CenteredAuthScroll` below its back row, like forgot password; it was a plain top-anchored scroll view
that rubber-banded. On Android, sign-up, forgot password and e-mail confirmation take the same
`systemBarsPadding().imePadding()` as sign-in: none of these routes sits in a `Scaffold`, so the latter
two drew their back arrow under the status bar, and sign-up's fixed 64dp top and 32dp bottom (now 24dp
each, as on sign-in) let the end of the form sit under a 3-button navigation bar or the keyboard.

**Every partner auth form is centred** (since 2026-10-03, owner remark). Sign-in, registration, forgot
password and e-mail confirmation centre their form in the space they are given when it fits, and
scroll from the top when it does not (a short phone, the keyboard, a large text size), with 24 at the
sides and 32 above and below. Forgot password and e-mail confirmation keep their back row at the top
and centre the form under it. On iOS the four use `CenteredAuthScroll`, which moved from the customer
app to CleansiaCore when the partner app became its second caller. On Android they share
`CenteredAuthColumn` (partner `features/auth/`), a scrolling column at least as tall as its viewport
that centres its content. Its keyboard handling is unchanged: the partner activity has no
`adjustResize`, so the system pans the window for the keyboard, and padding by the keyboard here would
change it on every partner screen. Until then each partner form sat at the top, under a fixed 64pt
(64dp) pad on sign-in and registration and right under the back row on the other two, over an empty
bottom on a tall phone. The customer forms are unchanged: centred on iOS, top-anchored on Android.

**The customer sign-up still scrolls, by decision.** Removing its mascot was the whole change (owner
ruling 2026-10-01). Even without it, the form is about 220pt taller than a 6.1" iPhone, and it is
taller again on an SE. The one layout that fits every phone is a two-step sign-up (social buttons,
e-mail and the terms tick first; name, password and referral second). It was offered and not taken.

## Tab roots clear the Book FAB, not just the bar {#bottom-chrome-clearance}

The customer Book FAB is docked on the top edge of the bottom bar and rises above it. Content
scrolled to the end must clear the **FAB's top**, not the bar's. Only the four tab roots
(Home, Orders, Rewards, Profile) need this, because a pushed screen or a sheet covers the whole shell,
FAB included.

- **iOS.** The native `TabView` insets each tab's scroll content by its own bar (49pt) and nothing
  else, so the FAB's 33pt overhang covered the last card on every tab. The shell gives each of the
  four roots a `safeAreaInset(edge: .bottom)` of `BookFabMetrics.scrollClearance`: the overhang
  (`chromeEnvelope − systemTabBarHeight`) plus 12pt, which is 45pt. The clearance is derived from the
  same constants that place the FAB, so the two cannot drift apart. `contentMargins` would do the same
  job but needs iOS 17, which is above the floor. A tab root adds no bottom padding of its own on top:
  Profile kept a 40pt one under its last buttons until 2026-10-02, which left them 52pt above the FAB
  instead of 12pt.
- **Android.** The bar box is 12dp + 74dp + 12dp = 98dp above the navigation-bar inset. The 74dp FAB
  sets its height, not the 64dp pill: a `Box` is as tall as its tallest child, and the FAB's
  `offset(y = (-12).dp)` moves where it is drawn without changing its measured size. The pill sits at
  the top of the box's content, and the offset lifts the FAB 12dp above the pill, onto the box's top
  edge. Each tab root ends with
  `Spacer(Modifier.navigationBarsPadding().height(MainShellBottomClearance))`, where the clearance is
  the box plus 16dp: `12.dp + 74.dp + 12.dp + 16.dp`, which is 114dp. The navigation-bar inset is
  therefore added at runtime, and a 3-button bar is about twice as tall as the gesture handle. The
  fixed `108.dp` it replaced ignored the inset and left the last item under the FAB in either mode.
  **Count the FAB, not the pill.** A box sized from the pill comes to 88dp, and a clearance built on
  it leaves a 6dp gap instead of 16dp.

**A state that fills an empty tab centres between the title and the FAB's top**, the area that is
actually visible. Orders does this on both platforms. On iOS so do the Rewards error state and the
Disputes screen's empty and error states (since 2026-10-02); Disputes is a pushed screen with no FAB, so
its area runs from the navigation bar to the bottom edge. On iOS, a `ScrollView` proposes no
height, so a centring stack inside it collapses. The empty and error states therefore take their
minimum height from a `GeometryReader` around the scroll view (`containerRelativeFrame` is iOS 17+).
With the clearance above in place, that height runs from the title to the FAB's top. Before this, a
fixed `minHeight` band started under the title and the block sat high. On Android, the
`ScrollableStateContainer` box keeps the viewport height but pads off the navigation-bar inset and
`MainShellBottomClearance`. Before this, it centred over the area under the floating bar too, and the
block sat low. Both keep pull-to-refresh.

**A sticky bottom bar reserves its measured height, never a literal.** The Plus offer's button bar
(the button and its billing disclosure) changes height with the disclosure's length, the text size
and, on Android, the navigation mode. A fixed 140 reservation left a blank band in some cases and let
the bar cover the last perk in others. On iOS the bar is mounted with `safeAreaInset(edge: .bottom)`
on the offer's scroll view, not overlaid in a `ZStack`, so the content reserves exactly its height. On
Android the bar reports its height through `onSizeChanged`, and the scroll column pads by that height.
On both platforms the bar keeps 12 under the disclosure, above the home indicator or navigation bar.

The snackbar has its own lift above the same chrome (→ [Snackbar insets](#snackbar-inset)). On a tab
root it clears the FAB's top by the same gap the content leaves: 12pt on iOS
(`BookFabMetrics.chromeEnvelope + 12`), and on Android `MainShellBottomClearance`, the 98dp box plus
16dp, above the navigation-bar inset the host already pads. Until 2026-10-02 Android lifted it by a fixed
88dp, which put a tab-root snackbar 10dp over the FAB.

## A hero inside a scroll view paints its own status-bar strip {#full-bleed-hero}

On both platforms, the customer Profile hero and the Plus offer hero reach the top edge of the
screen, behind the clock. The two platforms get there differently, and the iOS route has a trap.

- **Android** paints the hero's gradient first and insets its content by the status bar afterwards:
  the Plus hero applies `background(…)` before `windowInsetsPadding(WindowInsets.statusBars)`, and the
  Profile hero pads its content by `statusBarTop + 48dp`. The window is edge-to-edge, so the gradient
  starts at the top of the screen.
- **iOS** keeps its scroll views inside the safe area, with no `.ignoresSafeArea` on the scroll view
  and no measured top inset (T-0766, 2026-09-16). **An `.ignoresSafeArea` on a background inside a
  `ScrollView` does nothing.** The scroll view turns the top safe area into a content inset, so its
  content has no safe area left to ignore. Until 2026-10-01 that form stopped both gradients at the
  status-bar line and left a band of page background behind the clock. Now the hero's background
  paints upward past its own frame. A 600pt block of the gradient's first colour sits directly above
  the unchanged gradient (`.background(alignment: .bottom) { VStack(spacing: 0) { … } .padding(.top,
  -bleed) }`), and it fills both the status-bar strip and the rubber-band overscroll. A negative top
  padding on the gradient itself would stretch its colours, which is why the solid block goes above
  it. Screens that do not scroll, such as the reduced Plus states, keep `.ignoresSafeArea`, which
  works there. The partner app's profile hub hero (`ProfileHubContent`) had the same strip and has
  used the same form since 2026-10-02; `ContentSafeAreaBindingTests` pins the bleed.

Content in these scroll views passes under the status bar once it scrolls, so these screens also use
the fade below.

## Content fades under the status bar {#status-bar-fade}

Home, Profile and the Plus offer draw to the top edge with no navigation bar, so once their content
scrolls up it passes under the clock and the camera cut-out. On those three screens, once the content
has left its top, it fades out under the status bar. On iOS the fade covers the status bar alone (the
clock, signal and battery) and ends in a short tail 8pt below it: a light blur under a see-through wash
of the page colour, the same on every iOS version, so the content stays visible under the clock. On
Android it is a band of the page background that reaches 24dp below the status bar. iOS changed twice
on 2026-10-03 (owner remarks): a band of the page colour, like Android's, read as a solid white strip,
and the system soft edge and blur that replaced it were too tall, too sharp and too opaque (below).
Orders and Rewards keep a fixed title above their scroll view, so nothing passes under the status bar
there.

**It is scroll-driven, never static.** At rest nothing is drawn, so the full-bleed Profile and Plus
heroes (above) paint the strip themselves. A pull-to-refresh never raises it either. It is decoration
only: it takes no touches and is hidden from VoiceOver and TalkBack. Both platforms show it from the
first point of scroll.

- **iOS** wraps the scroll view in `StatusBarFadeScrollView` (customer `Components/`). A
  `GeometryReader` behind the content reads the content's top in a named coordinate space, and state
  is written only when it crosses the threshold, not on every scrolled frame. A `PreferenceKey` reader
  does not work here, because the scroll view does not pass its content's preference changes to an
  `onPreferenceChange` outside it.
- **One overlay, the same on every iOS version.** Over the scroll view lies a strip as tall as the
  status bar plus `StatusBarFade.tail` (8pt), faded in and out with the scroll. Its fill is
  `.ultraThinMaterial` at 40 % (`blur`) under the page colour at 40 % (`wash`). The wash moves the
  content the way the page does, lighter in light mode and darker in dark, which keeps the clock
  legible over a busy card; the material alone lifted dark content towards grey and left a glow at the
  screen's edges. A gradient masks the strip: full across the top 35 % of the status bar
  (`holdShare`), then a smoothstep falloff, sampled at nine points, to clear at the tail's end, so no
  line marks the status bar's edge. Under Reduce Transparency the material goes and the wash alone, at
  85 % (`solidWash`), stands in for it. The strip's reader keeps the safe area on purpose: one that
  ignores it reports a top inset of 0, which collapses the fade to its tail. The strip takes no
  touches, so Home's address row and the Plus offer's back arrow under it still answer.
  `ContentSafeAreaBindingTests` pins the mask (full at the top, never rising, no step above 0.2, clear
  at the tail's end, a tail of 6–10pt), and that the fade has no per-version branch and no system
  edge. Checked on the iOS 26.3, 18.6 and 16.4 simulators, light and dark, with a card under the
  clock.
- **Why not iOS 26's own soft edge.** From the first to the second remark of 2026-10-03, iOS 26 drew
  the system's soft scroll edge, the one a navigation bar draws: a `safeAreaBar` stand-in 24pt tall
  with a near-clear fill, then `scrollEdgeEffectStyle(.soft, for: .top)`, because the system draws its
  edge only under a bar and these screens hide theirs. iOS 16–25 laid an `.ultraThinMaterial` band
  over the status bar, masked to clear 24pt below it. The system draws its edge well below the status
  bar, a milky wash down to about 77pt on the iPhone 17 Pro, at a height an app cannot set, so it could
  not cover the status bar alone, and the 16–25 band ended in a visible line. Both are gone, with
  `StatusBarFade.depth`. When checking a scroll-driven effect, use real drags:
  `UIScrollView.setContentOffset` does not update SwiftUI geometry.
- **Android** applies `Modifier.statusBarFade(scrollState)` (customer `ui/components/StatusBarFade.kt`)
  directly before `verticalScroll(scrollState)`, so it draws over the viewport and not over the
  scrolled content. It is visible while `scrollState.value > 0`. Home moved its status-bar padding
  inside the scroll, so the address bar starts below the status bar at rest and then scrolls under the
  band. That leaves Home's `PullToRefreshBox` filling the whole screen, so its indicator pads
  `WindowInsets.statusBars` before its 8dp and rests below the status bar, not under it. The band's
  gradient ends on `background.copy(alpha = 0f)`, the page colour at zero alpha: Android interpolates
  gradient colours unpremultiplied, so a tail of `Color.Transparent`, which is transparent black,
  passed through greys and tinted the light theme. `StatusBarFadeBindingTest` pins both
  (2026-10-02).

## A booking swiped away keeps its draft {#booking-draft}

On both apps the booking sheet closes with a swipe down on any step, and closing it throws nothing
away (owner remark 2026-10-03). The draft, meaning the step and every choice on it, lives in the
`BookingViewModel` that the signed-in shell keeps for the session. What happens to it depends on how
the sheet is opened:

- **A plain open resumes it.** The Book button and the other plain *Book now* entries reopen the sheet
  on the step it was left on, with every choice it held, once its time has been re-checked (below).
- **An open that seeds a booking starts a fresh one in its place.** *Order again*, a popular package
  and the quick-size card's *See my price* reset the draft, fill it with that order, package or size,
  and open on the first step.
- **A booking placed resets it**, as before. On iOS the sheet holds the swipe only while a booking is
  being placed, so the outcome has a screen to land on.
- **It lasts as long as the signed-in shell.** After a sign-out, or once the app has been quit, the
  next booking starts from scratch.

Until then iOS held the swipe from step 2 on, so a half-built booking could be closed only by stepping
back to the first step. Android let the swipe through on any step, but every plain open reset the
draft, while *Order again* skipped the reset, so an abandoned draft's dirtiness level, date, time and
payment carried into the repeated order.

**A resumed draft follows Home's address** (both apps since 2026-10-03). On a plain open the draft's
address is refilled from the saved address Home's top bar has chosen when the sheet filled the address
in from it and Home's choice has since changed; a blank address is always filled in. An address the
customer picked in the sheet is never replaced (`hydratedWithPreferred`: `BookingPrefill` on iOS,
`BookingBottomSheet.kt` on Android). Until then Android kept the address the draft had, so a booking
reopened after Home switched address was quoted and created for the old one while Home showed and
priced the new one.

**A booking's time is re-checked against the When step's own rules** (since 2026-10-03). Both apps
ask `draftTimeStillHolds` (`WhenWhereStep.kt` on Android, `BookingTimeSlots` on iOS), which reads the
same slot states the When step draws, at three moments:

- **A plain open**, which resumes the draft.
- **A return to the foreground with the sheet open.** Android re-checks on `ON_START` after an
  `ON_STOP` in the sheet, a real return from the background. iOS re-checks whenever the shell's
  `scenePhase` turns `.active` while the booking is presented, so also after a spell that was only
  inactive, such as Control Center. It is the shell's `scenePhase` because inside a sheet it stops
  updating on iOS 16. The check changes nothing while the time holds, so the two triggers come to the
  same.
- **Just before submit**, in `submit()` and in the submit that follows a card guarantee, before
  anything is sent: no profile read, no quote, no card capture, no order.

The time holds while the When step still offers it, which means its day is not past and it is not
inside the 2 h lead time, and while it is in the band it was in when its price was quoted. That moment
is when the quote for the chosen time landed, read only while the last quote that landed names that
time; with no such quote it is when the sheet closed. Every quote that lands resets it, and a seeded
open forgets both moments. A standard time that has since slid into the 2–4 h express band does not
hold, because it was quoted as a standard time, without the express surcharge. A time that was
already express when it was quoted does, and so does one re-quoted after it went express, because that
quote carries the surcharge. A time that does not hold is cleared, and its day with it once the day is
past. Nothing is sent, the booking goes back to the When step if it was past it, and a notice says the
time picked is no longer available or its price has changed and asks for a new one
(`booking_draft_time_changed`, worded the same on both apps). It first opened *While you were away*,
which did not fit a customer refused at submit who had never left. A time that holds is kept. On both
apps a kept day, whether its time held or was cleared, is named again against the moment of the
re-check, as the When step's strip names it. A day that has since become today therefore reads *Today*
on Confirm and is the day the When step selects. iOS stores the day as its label and re-derives the
label. Android stores the date and re-labels the day it shows (`selectedDate`, display only, so nothing
is re-quoted). A seeded open is not re-checked: it resets the draft anyway.

On a return to the foreground the When step also rebuilds its day strip and its slots from the
current clock, so a step left on screen no longer offers a slot that has since come inside the lead
time, or calls yesterday *Today*. Android rebuilds both on `ON_START`; iOS redraws the sheet from the
shell's foreground re-check.

Until the first of these, a draft resumed on the Confirm step could keep a time that had come inside
the lead time, and only the server refused it. Until the foreground and submit re-checks, a sheet left
open in the background, or a Confirm step left on screen, kept such a time too. The band was judged
from when the sheet closed, so a time quoted standard and left after it had gone express read as
unchanged and kept a price without the surcharge. Android re-labelled a kept day only once the When
step redrew its strip, so a booking picked on Thursday for Friday and resumed on Friday read *Fr* on
Confirm for a clean that was today.

## The booking's steps slide the way they go {#booking-steps}

Going on, the next step comes in from the trailing edge and the current one leaves to the leading
edge. Going back, the previous step comes in from the leading edge and the current one leaves to the
trailing edge (owner remark 2026-10-03). Leading and trailing follow the layout direction, so a
right-to-left language mirrors the slide. On iOS the steps crossfade instead under Reduce Motion, and
on Android the slide runs at zero duration when animations are removed in the system settings.

- **iOS** (`BookingSheetView`) used one transition for every change until then, so going back looked
  like going on. The direction is set one run-loop turn before the step changes: a page that leaves
  animates with the transition it last rendered with, so setting both in one update sent it the old
  way. **The page changes identity inside a `ZStack` of its own**,
  `ZStack { stepPage.transition(stepTransition).id(step) }`, with the animation on the `ZStack`, so
  the container that removes the leaving page is the one that keeps it on screen through the slide.
  Until 2026-10-04 the `.id(step)` sat at the top of the step area, the container was the sheet's
  outer `VStack`, and the leaving page vanished in one frame on every step change while only the next
  one slid in (recorded frame by frame on iOS 26.3 and 16.4). `BookingStepGateTests` pins the
  `ZStack` and a single `.id(step)`.
- **Android** (`BookingBottomSheet.kt`) was already directional, but with fixed left and right, which
  a right-to-left layout would have turned around. `stepSlideDirection(forward, rtl)` now mirrors the
  sign under `LayoutDirection.Rtl`. No locale the app ships is right-to-left, so nothing changed on
  screen.

## Every map is quiet, with one Cleansia pin {#maps}

All four map surfaces in both apps show a muted base map with **no points of interest**, and their only
marker is **the Cleansia pin** (owner ruling 2026-10-01). The four are the customer address picker
(booking, the saved-address chooser and the address manager), the customer order detail, the partner
profile's address picker and the partner order detail. Street and place names stay, so a customer can
still find their street. Neither platform can recolour the other's map tiles, so parity means three
things: no POIs, a muted base and the same pin.

- **iOS** (`CleansiaCore/Location/MapKitMapProvider.swift`). Every map is an `MKMapView` with
  `MKStandardMapConfiguration(elevationStyle: .flat, emphasisStyle: .muted)`,
  `pointOfInterestFilter = .excludingAll` and no traffic (`CleansiaMapStyle`). The address picker moved
  off SwiftUI's `Map(coordinateRegion:)`, which takes no configuration on iOS 16, to an `MKMapView`
  representable behind `MapProvider.pickerMap(region:showsUserLocation:bottomInset:)`. The iOS 17
  `.mapStyle` modifier would have left iOS 16 users with every POI. The picker writes the settled region
  back to its binding, and applies a region the binding moves to only when it differs from the one it
  last reported, so a drag never fights the finger. Apple's logo and *Legal* link must stay visible, and
  they sit inside the map's bottom layout margin. So each picker (the customer
  `BookingAddressPickerView` and the partner `AddressPickerView`) measures what it lays over the map's
  bottom edge, the location-button row and the confirm card, and passes that height as `bottomInset`.
  The button row counts because on iOS 16 *Legal* sits at the bottom right, under the button. The card
  grows with the text size, so the inset is measured rather than fixed, and the logo and *Legal* stay
  above both up to AX5. MapKit centres its region inside the layout margins, so the centre pin is
  pinned to the margins' centre, not the view's. When the inset changes, for example on a live
  text-size change, the picker sets the picked point back under the pin, because MapKit holds the map
  still while the margins move. **So the card must not change height on a lookup** (since 2026-10-03,
  owner remark). Every drag starts a reverse lookup, and the card's address block used to be one line
  while it looked the address up (*Looking up…*) and two lines once it had it (the street over its
  city line). Each lookup therefore moved the inset, and with it the map, the pin and the card. Both
  pickers now hold two lines in every state: the customer `BookingAddressPickerView` (booking and the
  address manager share it) and the partner `AddressPickerView`, which had the same one-line lookup
  state. While there is no second line, the title holds its place in the second line's font, unseen
  and hidden from VoiceOver, so the height still follows the text size. `AddressPickerConfirmCardTests`
  measures the partner card in all five languages, at the default and an accessibility text size, in
  every lookup state. Since 2026-10-02 the two full-bleed order maps (the customer
  `OrderDetailMap` and the partner `OrderDetailView`) keep the logo and *Legal* above their `SnapSheet`
  the same way: `fullBleedMap(coordinate:)` measures the map and reads the sheet's top, which `SnapSheet`
  publishes to its backdrop (`snapSheetTop`), and the bottom margin is the part of the map below it, at
  every anchor and through a drag. The region is set once per coordinate, centred on the whole map with
  zero margins, and only then does the margin follow the sheet, so the logo and *Legal* move and the pin
  does not. A trailing margin of the ornament's width keeps *Legal* clear of the mascot puck on the
  sheet's edge, where iOS 16 puts it. Because the region is no longer re-set on every update, a pan
  survives a sheet drag or a poll; a new coordinate still re-centres.
- **Android** (`:core` `location/CleansiaMap.kt`). `CleansiaMapStyle(darkTheme)` is Mapbox Standard
  through the pinned maps-compose 11.8.0: POI and transit labels off, the faded theme, no 3D objects,
  and the day or night light preset from the app theme. It replaced `MapStyles`, whose classic
  `light-v11` / `dark-v11` styles drew Mapbox's own POI and transit layers. The two order maps show the
  Mapbox wordmark and attribution again, which Mapbox's terms require; they had been switched off.
  Both are lifted above the resting sheet, which covers the map's bottom edge. Since 2026-10-02 the two
  address pickers (the customer `AddressManagerScreen` and the partner `AddressPickerScreen`) lift them
  above their bottom card too, by the card's measured height, keeping Mapbox's own horizontal places;
  only the ornaments move, not the camera centre the pin marks. Since 2026-10-03 both pickers' address
  blocks keep the height of their two lines while they look the address up (a minimum height of the
  `titleSmall` and `bodySmall` line heights, converted through the density so it follows the font
  scale). Each line keeps to one, so a long locale or a large font cannot outgrow the reservation.
  The partner card's first line carries the looking-up and drag-the-map hints as well as the street,
  and ends in an ellipsis. The customer card gives each state its own text, and only its two hints end
  in an ellipsis. Its street and postcode-and-city lines, and the partner card's place line, are cut
  off at the card's edge (Compose's default), where iOS ends every line of both cards in an ellipsis.
  Before, each lookup bobbed the card and the ornaments above it, by about 12dp on the customer
  picker and 16dp on the partner one. `AddressPickerCardTest` (partner) pins the reservation, the
  one-line rule and that the card's height reaches only the ornaments.
  `CleansiaMapUsageTest` fails any of the four maps that leaves either ornament on its defaults.

**The pin** is a brand-sky teardrop with a white house, 40 × 50 (pt or dp): sky-600 `#0284C7` on a
light map and sky-400 `#38BDF8` on a dark one. On iOS it is `CleansiaMapMarker` and on Android
`CleansiaMapPin`. Its tip is the bottom centre of its bounds: an order map anchors its annotation
there, and a picker lifts its centre pin by the pin's full height so the tip points at the coordinate
being picked. It is decorative, because the address it marks is always written beside the map, and
VoiceOver and TalkBack skip it. It replaced the pins each surface drew for itself, which disagreed
across the platforms: plain discs, a disc on a stick and MapKit's balloon. `CleansiaMapUsageTest` (Android `:core`) fails a map that stops using the shared
style or pin; `MapMarkerTests` and `MapKitMapProviderFullBleedTests` (`CleansiaCoreTests`) pin the iOS
configuration, the single centre pin and the tip.

## Three moments are felt {#haptics}

Both apps play a haptic at three moments and nowhere else (owner decision D15, 2026-10-01).
[ADR-0018](/decisions/adr-0018) D2 expects haptics on the right moments, and one played everywhere
stops meaning anything.

| Moment | iOS | Android |
|---|---|---|
| A slide-to-confirm commits: the customer's booking, and the cleaner's contract, order-detail and orders-list slides | a medium impact, in Core `SlideToConfirm` just before its action | `LongPress`, in customer `SwipeToConfirmButton` and partner `SlideToCommit` |
| The outcome of an action is shown | `SnackbarController.show` plays the success, error or warning notification haptic | `GlobalSnackbarHost` plays `CONFIRM` for a success and `REJECT` for an error or a warning; below API 30 all three play `LONG_PRESS` |
| A size stepper takes a step | a selection tick in the customer `PropertyStepper`, on the booking and schedule size rows | `CLOCK_TICK`, Android's selection tick, from `rememberStepperTick` in the customer booking `CompactCounter` and the schedule `Stepper` (since 2026-10-02) |

- **Each haptic is played at the one place its callers share**, so there is no wrapper type. Every
  outcome in both apps is shown by the shared snackbar host, and every slide goes through one
  component per app.
- **What plays nothing.** An info message reports no outcome. A cancelled request never reaches the
  snackbar host. A stepper disables its button at a bound, so a refused step never ticks.
- **An offline failure is an outcome like any other** and plays the error haptic. On iOS and the
  partner Android app the screen shows it. On customer Android `NetworkErrorInterceptor` shows it and
  the screen stays silent, so it is shown, and felt, once ([the error model](#error-model)).
- **Android has no warning haptic.** Every warning the apps raise is a refusal, so a warning plays
  `REJECT`.
- **The phone's setting wins.** System Haptics on iOS and touch feedback on Android turn all three off.
- **No dependency moved.** iOS uses the UIKit feedback generators, which exist on the iOS 16 floor.
  SwiftUI's `sensoryFeedback` would need iOS 17 and a second code path. Android stays on the pinned
  Compose BOM, because `CONFIRM` and `REJECT` are `View` haptic constants from API 30.

No test can feel a haptic. `SnackbarHapticTest` (Android `:core`) pins the constant each severity plays,
and the fallback below API 30.

## iOS draws its own controls {#native-ios}

The iOS apps keep Android's screens, flows and branding, and draw each control the way iOS does. That
is [ADR-0018](/decisions/adr-0018): D1 holds the layout, flow and branding identical, and D2 and D3 let
iOS win on the component. The adoptions below (owner decision D15, 2026-10-01) all sit inside D2 and
D3 as written, so no ADR changed. Each one is also a row in the living mapping table that a reviewer
checks an iOS screen against (`agents/architecture/decisions/ios-app-architecture.md`).

- **Android is unchanged** unless an item says otherwise. These are component and motion changes, and
  Android keeps its own idiom for each.
- **The iOS floor stays 16** ([ADR-0014](/decisions/adr-0014)). An API from a later version sits behind
  `#available`, and below that version the screen looks and works as it did.

What changed on iOS:

- **A short list opens as a menu on its field.** A `CleansiaDropdown` over a closed list (the dispute
  reason, the partner sign-up market, the partner document type) is the label of a native `Menu` that
  holds an inline `Picker`. The list drops down from the field, with the system checkmark on the
  chosen option, instead of a sheet sliding up over the screen; on iOS 26 the menu is Liquid Glass. A
  searchable one keeps the sheet and its search field, because the country lists (bank country,
  nationality, business country) are about 250 entries long. Both draw the same field, and the whole
  field opens either one. Android keeps its bottom sheet; its customer dispute form already used an
  anchored menu for the reason.
- **Photos zoom into their viewer, from iOS 18.** An order's photo and a dispute's evidence image grow
  out of their thumbnail into the full-screen viewer and shrink back into it, and a swipe down closes
  the viewer. The photo viewer keys the way back on the page on screen, so after paging it shrinks
  into that photo's thumbnail. On iOS 26 its close button is clear Liquid Glass, the variant for a
  control over media, and a translucent dark circle below; since 2026-10-02 the dispute-evidence viewer
  closes through the same control (`Components/MediaCloseButton.swift`), where it had a bare white X
  that vanished over a light photo. iOS 16 and 17 keep the plain full-screen cover, closed with its X. The one
  `#available(iOS 18, *)` gate sits in the customer app's `zoomSource` / `zoomDestination`
  (`Components/ZoomTransition.swift`). Android has no twin: this is motion, not layout.
- **Numbers roll instead of snapping.** A number that changes in place rolls digit by digit, like an
  odometer (`.contentTransition(.numericText())`, iOS 16, so one code path): the booking and schedule
  size steppers, the booking sheet's *Step n of 4*, the Rewards points figure and the partner
  dashboard figures. The animation is keyed on the number itself. The roll only moves digits, so it is
  kept to texts that are mostly a number.
- **The size steppers have 44pt targets and are one VoiceOver control each.** The customer room and
  bathroom steppers keep their branded pill, because two native `Stepper`s do not fit the size row
  Android draws. Each button's glyph stays 28pt inside a 44pt hit region, HIG's minimum, without the
  pill growing; the two steppers on the booking row split the gap between them. VoiceOver reads each
  pill as one adjustable control, named by its row (*Your home*, or *Rooms* and *Bathrooms* on a
  schedule) with the count as its value, and a swipe up or down steps it within the buttons' bounds.
  Android has matched it since 2026-10-02. The booking counter keeps its 28dp glyphs, each inside a
  48dp target laid out as on iOS (10dp above and below, 16dp toward the label, 4dp outward); the
  schedule stepper already had 48dp buttons. Both are one TalkBack node, named and valued as on iOS and
  adjusted like a slider with a swipe up or down (`adjustableStepper()` in the customer app's
  `StepperAccessibility.kt`).
- **On iOS 26 the booking sheet grows out of the Book button.** Opened from the Book FAB, the booking
  sheet zooms out of the button and shrinks back into it when it closes. Every other way into booking
  (Home's book buttons, the carousel slides, *Order again*) slides the sheet up as before. The FAB looks
  the same at rest: no glass was added, since the glass FAB was retired for rendering corrupted on an
  iOS 26 iPhone ([ADR-0022](/decisions/adr-0022)). The gate is iOS 26, not iOS 18 where the API starts.
  On iOS 18.6 the zoom presents, swipes away and keeps [the draft](#booking-draft), but iOS 18 shows
  the zoomed sheet as a full-screen page with no grabber and no card edge, so nothing tells the
  customer it can be swiped away. iOS 18 to 25 keep the plain sheet.
- **Every confirmation is a system dialog** (owner remark 2026-10-03). A confirmation that only asks
  (a title, a message, a confirm and a cancel) is a native `.alert`, with a red confirm where it
  destroys. That covers every sign-out, deleting the account (both apps), cancelling or switching
  Plus, deleting a schedule, removing a saved card, revoking a device (both apps), and on the partner
  side the cash-collected confirm, deleting a note or an issue, declining or refusing an offer and
  the removal-reason notice. Deleting a saved address and choosing a photo's source stay the
  `.confirmationDialog` action sheet. The partner's document dialogs, which take input, are system
  dialogs too. An upload asks for the type with a `.confirmationDialog` anchored to the Upload
  button, then for the description with an `.alert` holding a text field. A replacement and a
  deletion request are each one `.alert` with a text field.
  - **No dialog stays up while its request runs.** A system dialog closes on the tap. The row or the
    button it came from shows a spinner while the request runs, the other rows' buttons wait, and a
    refusal goes to the snackbar. The retry hint the card showed after a failure had nowhere left to go and was
    deleted.
  - **iOS 16 hides a disabled alert button** and never brings it back, so an alert cannot hold its
    button off until its field is filled. Two dialogs need a filled field, and both differ between the
    platforms for that reason. For the deletion request, the view model refuses a blank reason itself,
    with *This field is required.* in the snackbar, and sends nothing. The customer's address rename
    keeps *Save* enabled, and a blank label closes the alert and saves nothing, with no message.
    Android can bring a button back, so both its dialogs hold the confirm disabled until the reason
    or the label is typed. Those two are the only differences in what the two platforms' dialogs do.
  - The alerts use the same title, message and button strings, and lose the card's icon circle and
    spring. `CleansiaDialog`, the branded card, had no caller left and is deleted from CleansiaCore.
    Until 2026-10-01 it was every confirmation, and until 2026-10-03 it was still the six that held a
    field or stayed up while submitting (card removal, both device revokes and the three document
    dialogs).
  - **Android has matched since 2026-10-03** (owner remark). Every confirmation, notice and short
    choice in both Android apps is a Material 3 `AlertDialog` with the same strings: a title, the
    text, a `TextButton` confirm (in the error colour where it destroys) and a `TextButton` cancel,
    with no icon circle, read by TalkBack as the system reads any dialog. The rules above hold there
    too. The dialog closes on the tap. Removing a card or revoking a device shows a spinner on that
    row while the other rows' buttons wait, a refusal goes to the snackbar, and the two retry hints
    are deleted in all five locales. A field sits in the dialog's text. A document upload asks for the
    type in a dialog that lists them, then for the description in a second dialog that holds the field
    and names the type and the file. A replacement and a deletion request are one dialog each. The
    cleaner's unreachable-server splash, which held its sign-out dialog open over the wipe, now closes
    it on the tap and does not offer sign-out again while the wipe runs. The `:core` `CleansiaDialog`,
    which drew a window of its own with an icon halo and a spring, is deleted too.
    `SystemDialogUsageTest` fails any screen that imports Compose's `Dialog` window again.
  - **Some forms differ by platform idiom, not by content.** Deleting a saved address is a dialog on
    Android and an action sheet on iOS. The preferred-cleaner list is a dialog on Android and a sheet
    on iOS. Choosing a photo's source is a bottom sheet on Android and an action sheet on iOS.
  - **No ADR changed.** [ADR-0018](/decisions/adr-0018) D3 maps Material's `AlertDialog` to `.alert`
    and `.confirmationDialog`. That row now holds on both sides with no exception left, and D1 holds
    the content identical, which did not change.
- **The birth date is picked on wheels.** The date-of-birth field (customer profile edit and
  completion, partner personal details) opens day, month and year wheels in a half-height sheet,
  instead of a month-by-month calendar that started at today. An empty field's wheels open thirty
  years back, so an adult's year is a short spin. That date is only shown: closing the sheet untouched
  leaves the field empty, as on Android, whose picker opens with no selection. Future dates stay
  blocked, and the stored day keeps its time-zone handling.
- **A few symbols bounce, from iOS 17.** The notification bell (customer Home, partner dashboard)
  bounces once when the unread count rises, and stays still when it falls. The booking-success check,
  the Plus welcome screen's check and the *code applied* check of the promo and referral sheets bounce
  once as they appear. Nothing
  else moves: before iOS 17 these stay still, and Reduce Motion tones the bounce down by itself. The
  one gate is Core's `cleansiaBounce(onIncreaseOf:)` / `cleansiaBounceOnAppear()`
  (`Components/SymbolBounce.swift`).
- **A long press on a saved address offers its actions.** In the customer's address manager, a long
  press lifts an address card and offers *Set as default*, *Rename* and *Delete*, the same three actions
  as its ellipsis menu. Both menus are built from one list, so they cannot drift apart. A tap still
  selects the address, and *Delete* still asks first. Swipe actions on these cards need the iOS 27 SDK,
  which the toolchain does not have yet. Android keeps its row menu.
- **The preference pickers are system lists.** The customer's Language, Market and Appearance pickers
  and the partner's Language and Theme pickers are a native inset-grouped list with a checkmark on the
  chosen row, on the brand surface, instead of a hand-built card of rows. They gain the system row
  highlight, Dynamic Type row heights and the iOS 26 list look. The rows, their order and their labels
  are unchanged, and choosing one still applies it and goes back.
- **On iOS 26 the cleaner's tab bar shrinks while a tab scrolls down.** The partner tab bar minimizes to
  the selected tab's button as a tab root scrolls down, and comes back on a scroll up, which gives the
  content the room. The customer tab bar does not: the Book FAB sits over it at a fixed offset, and a
  shrunk bar would leave the button floating on its own. Below iOS 26 both bars are as they were.
