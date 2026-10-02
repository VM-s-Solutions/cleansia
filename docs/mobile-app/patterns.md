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
still need it. On iOS, `CenteredAuthScroll` (the one container the centred auth forms share) sets
`scrollBounceBehavior(.basedOnSize)` from iOS 16.4, so a form that fits neither rubber-bands nor shows
a scroll indicator. Do not reach for `ViewThatFits` here: when the keyboard changes the available
space the view switches branch, the focused field is recreated, the keyboard drops, and the cycle
repeats. Android keeps the form top-anchored and pads it by the real system bars and keyboard
(`systemBarsPadding().imePadding()` ahead of `verticalScroll`) instead of a fixed 64dp. The activity is
edge-to-edge, so once the form fits there is no scroll range left to bring a field out from under the
navigation bar or the keyboard.

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
  job but needs iOS 17, which is above the floor.
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
actually visible. Orders is the only tab that does this today. On iOS, a `ScrollView` proposes no
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

The snackbar has its own lift above the same chrome (→ [Snackbar insets](#snackbar-inset)), and this
does not change it.

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
  works there.

Content in these scroll views passes under the status bar once it scrolls, so these screens also use
the fade below.

## Content fades under the status bar {#status-bar-fade}

Home, Profile and the Plus offer draw to the top edge with no navigation bar, so once their content
scrolls up it passes under the clock and the camera cut-out. On those three screens, once the content
has left its top, a band of the page background covers the status bar and fades to clear 24pt (24dp)
below it. Orders and Rewards keep a fixed title above their scroll view, so nothing passes under the
status bar there.

**It is scroll-driven, never static.** At rest the band is hidden, so the full-bleed Profile and Plus
heroes (above) paint the strip themselves. A pull-to-refresh never raises it either. It is decoration
only: it takes no touches and is hidden from VoiceOver and TalkBack. Both platforms show it from the
first point of scroll and fade it in and out.

- **iOS** wraps the scroll view in `StatusBarFadeScrollView` (customer `Components/`). A
  `GeometryReader` behind the content reads the content's top in a named coordinate space, and state
  is written only when it crosses the threshold, not on every scrolled frame. A `PreferenceKey` reader
  does not work here, because the scroll view does not pass its content's preference changes to an
  `onPreferenceChange` outside it. The band's own reader keeps the safe area on purpose: one that
  ignores it reports a top inset of 0, which collapses the band to its fade.
- **iOS 26's native soft scroll edge is not used.** The owner's first choice was
  `scrollEdgeEffectStyle(.soft, for: .top)`, alone or with a zero-height `safeAreaBar`, but it draws
  nothing when the navigation bar is hidden. On the iOS 26.3 simulator with real drags, scrolled frames
  were byte-identical to frames with no treatment. So the band runs on every iOS version. It has not
  been checked on a physical iPhone 17. When checking a scroll-driven effect, use real drags:
  `UIScrollView.setContentOffset` does not update SwiftUI geometry.
- **Android** applies `Modifier.statusBarFade(scrollState)` (customer `ui/components/StatusBarFade.kt`)
  directly before `verticalScroll(scrollState)`, so it draws over the viewport and not over the
  scrolled content. It is visible while `scrollState.value > 0`. Home moved its status-bar padding
  inside the scroll, so the address bar starts below the status bar at rest and then scrolls under the
  band.

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
  still while the margins move.
- **Android** (`:core` `location/CleansiaMap.kt`). `CleansiaMapStyle(darkTheme)` is Mapbox Standard
  through the pinned maps-compose 11.8.0: POI and transit labels off, the faded theme, no 3D objects,
  and the day or night light preset from the app theme. It replaced `MapStyles`, whose classic
  `light-v11` / `dark-v11` styles drew Mapbox's own POI and transit layers. The two order maps show the
  Mapbox wordmark and attribution again, which Mapbox's terms require; they had been switched off.
  Both are lifted above the resting sheet, which covers the map's bottom edge.

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
| A size stepper takes a step | a selection tick in the customer `PropertyStepper`, on the booking and schedule size rows | none yet |

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
  control over media. iOS 16 and 17 keep the plain full-screen cover, closed with its X. The one
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
  Android is unchanged: the booking counter's buttons are 28dp, the schedule stepper's are Material's
  48dp, and on both the buttons are unlabelled and TalkBack has no adjust action.
- **On iOS 26 the booking sheet grows out of the Book button.** Opened from the Book FAB, the booking
  sheet zooms out of the button and shrinks back into it when it closes. Every other way into booking
  (Home's book buttons, the carousel slides, *Order again*) slides the sheet up as before. The FAB looks
  the same at rest: no glass was added, since the glass FAB was retired for rendering corrupted on an
  iOS 26 iPhone ([ADR-0022](/decisions/adr-0022)). The gate is iOS 26, not iOS 18 where the API starts.
  From step 2 on the booking sheet refuses a swipe down, so a half-built booking cannot be swiped away,
  and only on iOS 26 has a zoom-presented sheet been checked to keep refusing it. iOS 18 to 25 keep the
  plain sheet until someone checks them on a device.
- **A plain confirmation is the system alert; a rich one keeps the branded card.** A confirmation that
  only asks (a title, a message, a confirm and a cancel), closes on the tap and then starts its work is
  a native `.alert`, with a red confirm where it destroys. That covers every sign-out, deleting the
  account (both apps), cancelling or switching Plus, deleting a schedule, and on the partner side the
  cash-collected confirm, deleting a note or an issue, declining or refusing an offer and the
  removal-reason notice. A confirmation that holds a field, or stays up while it submits, is still the
  `CleansiaDialog` card: removing a saved card, revoking a device (both apps), and the partner's
  document replace, upload and deletion request. A system alert closes on the tap, so it cannot keep a
  disabled button up until the request lands, or show the error inside itself. The alerts use the same
  title, message and button strings; they lose the card's icon circle and spring. Android keeps its
  branded dialog for every confirmation, since the content is what ADR-0018 D1 holds identical.
- **The birth date is picked on wheels.** The date-of-birth field (customer profile edit and
  completion, partner personal details) opens day, month and year wheels in a half-height sheet,
  instead of a month-by-month calendar that started at today. An empty field's wheels open thirty
  years back, so an adult's year is a short spin. That date is only shown: closing the sheet untouched
  leaves the field empty, as on Android, whose picker opens with no selection. Future dates stay
  blocked, and the stored day keeps its time-zone handling.
- **A few symbols bounce, from iOS 17.** The notification bell (customer Home, partner dashboard)
  bounces once when the unread count rises, and stays still when it falls. The booking-success check
  and the *code applied* check of the promo and referral sheets bounce once as they appear. Nothing
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
