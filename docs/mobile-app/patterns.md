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

**Profile's hero starts at sky-700 in light mode** (since 2026-10-05). The system's white clock and
icons read 4.1:1 on the brand blue's top, sky-600, under the 4.5:1 their text needs. Profile alone
now starts its gradient at sky-700 (`#0369A1`) and keeps the brand blue's sky-400 bottom. The hero's
top, the block that bleeds above it and the fade's colour (below) all take it, and the shared brand
blue that Home and the package cards use is unchanged. Dark mode keeps the brand blue on both
platforms, white on sky-800 at 7.6:1.

- **iOS** (`ProfileTab.heroTop`) does it on every version, because Profile asks for the white clock
  while its hero is behind it (`StatusBarStyleBridge`, since 2026-10-05, [below](#status-bar-fade)).
  Until then it did it from iOS 17 only: before iOS 17 the system draws the clock black in light mode
  whatever is under it, 5.1:1 on sky-600 and 3.5:1 on sky-700, so there the hero kept the brand blue.
  From the colour values, white reads 5.93:1 on the hero's top at rest, and at least 4.86:1 scrolled
  while the 90 % fade lies over the hero, the white avatar or the white stats card. While the hero's
  own bottom passes up through the fade, the fade steps over the blues the clock cannot be read on
  ([below](#status-bar-fade)). The dip the system chose is gone: until 2026-10-05, at 130–150pt of
  scroll, with the hero still under the whole fade, it sometimes drew the clock black on the 90 %
  sky-700 fade when the avatar or the white stats card was under or just below it, 4.0–4.3:1 from the
  colour values (5.45 and 4.92:1 in the screenshots), and the owner ruled that the clock is forced
  white while the hero is behind it (2026-10-05). `ContentSafeAreaBindingTests` pins the light hero's
  top with no version check, and the wiring.
- **Android** (`profileHeroColors`, `ProfileTab.kt`) needs no gate, because it sets the icons itself
  (below). Measured on the emulator, the clock reads 5.40:1 at rest, where the theme's dark icons read
  3.26:1 before, and 4.87–5.69:1 scrolled with the hero under the status bar; dark mode reads
  6.0–7.5:1. While the hero's own bottom passes through the fade, the fade now steps over the colours
  neither the white nor the dark icons can be read on (since 2026-10-05, [below](#status-bar-fade)),
  and the clock reads 4.54:1 at worst; until then the cross-fade and the icons' flip took it under
  4.5:1 for about 17dp of scroll, to 3.10:1. `ProfileHeroClockTest` pins the light top, the untouched
  brand blue, white at 4.5:1 or better in both themes, and the wiring.
- **Simulator screenshots run lighter than the colours.** In an iOS simulator screenshot sky-600
  `#0284C7` is captured as `#0097D2` and sky-700 `#0369A1` as `#007DB1`, so a contrast read off one
  comes out about a fifth low. The 3.3:1 recorded for this clock on 2026-10-04 is 4.1:1 from the colour
  values, and the new top reads 4.60:1 at rest and 4.27–4.54:1 scrolled in the screenshots. The iOS
  figures above are from the colour values.

Content in these scroll views passes under the status bar once it scrolls, so these screens also use
the fade below.

## Content fades under the status bar {#status-bar-fade}

Home, Profile and the Plus offer draw to the top edge with no navigation bar, so once their content
scrolls up it passes under the clock and the camera cut-out. On those three screens, once the content
has left its top, it fades out under the status bar. On iOS the fade is one solid colour, the colour
actually behind the status bar, held at 90 % over the clock, signal and battery and eased to clear in
its last 5pt. It ends at the bottom line of the status bar's content: the Dynamic Island's or the
notch's bottom edge, or the bottom of a home-button phone's 20pt status bar, with nothing below it,
the same on every iOS version. On Home that colour is the page colour; on Profile and the Plus offer
it is the hero's own colour while the hero is under the status bar, and the page colour once it has
scrolled past. Android draws the same rule (since 2026-10-05), ending at the bottom of the camera
hole, the line its clock is centred on (below). Until then it drew an opaque band of the page
colour over the status bar and 24dp below it, a white band over the Plus and Profile heroes; the
owner ruled that Android follows iOS, and that on both the fade ends at the clock and island line
(2026-10-04). iOS changed four times (owner remarks). On 2026-10-03 a band of the page colour, like
Android's then, read as a solid white strip, and the system soft edge and blur that replaced it were too
tall, too sharp and too opaque. On 2026-10-04 the light blur under a 40 % wash of the page colour
that followed read as a white band over the Plus offer's navy hero, and was so see-through that the
content under the clock clashed with it (below). The solid colour that replaced it the same day
covered the whole top safe area and a 10pt tail, 72pt on an iPhone 17 Pro, about 21pt below the
island; the owner asked for it to end at the clock and island line (remark 2026-10-04). Orders and
Rewards keep a fixed title above their scroll view, so nothing passes under the status bar there.

**It is scroll-driven, never static.** At rest nothing is drawn, so the full-bleed Profile and Plus
heroes (above) paint the strip themselves. A pull-to-refresh never raises it either. It is decoration
only: it takes no touches and is hidden from VoiceOver and TalkBack. Both platforms show it from the
first point of scroll.

- **iOS** wraps the scroll view in `StatusBarFadeScrollView` (customer `Components/`). A
  `GeometryReader` behind the content reads the content's top in a named coordinate space, and state
  is written only when it crosses the threshold, not on every scrolled frame. A `PreferenceKey` reader
  does not work here, because the scroll view does not pass its content's preference changes to an
  `onPreferenceChange` outside it.
- **One solid colour, the same on every iOS version.** Over the scroll view lies a strip from the
  screen's top edge down to the bottom of the status bar's content (`StatusBarFade.height`, below),
  faded in and out with the scroll. Its fill is a colour and no material: the blur under the colour
  that preceded it read as a different colour. A gradient masks the strip: the colour at 90 %
  (`StatusBarFade.opacity`) down to 5pt above its end, then a smoothstep falloff over those last 5pt
  (`StatusBarFade.falloff`), sampled at nine points, to clear at the end, so no line marks it. Nothing
  reaches below it. At 90 % the content under the clock stays out of its way, and the system's clock,
  signal and battery stay legible on it. Under Reduce Transparency the colour is drawn at 100 %. The
  strip's reader keeps the safe area on purpose: one that ignores it reports a top inset of 0, which
  collapses the fade to nothing. The strip takes no touches, so Home's address row and the Plus
  offer's back arrow under it still answer.
- **It ends at the island's line, by a measured rule.** The system reports neither the Dynamic Island
  nor the notch, and the status-bar frame it does report (`statusBarManager.statusBarFrame`)
  overshoots both: 54pt over islands that end at 48pt or 50.7pt, and 47pt over the iPhone 16e's
  notch, which ends at 33.7pt. The top of the safe area, though, sits a near-constant 11–11.3pt below
  an island, so on an island phone the fade ends 12pt above the safe area's top
  (`StatusBarFade.housingClearance`). Below a notch the gap is wider and varies with the phone: 14pt
  under the X's 44pt safe area, 13.3–15pt under 47 and 48pt, and 12.5–16pt under the minis' 50pt. So
  on a notch phone the clearance is read from the safe area's top
  (`StatusBarFade.clearance(safeTop:)`, since 2026-10-05): 12pt over 44pt, 13.5pt over 47 and 48pt,
  and 14.25pt over the minis' 50pt, midway between the 12 mini's notch and the 13 mini's, which share
  that safe area. An island's safe area starts at 59pt or deeper and keeps 12pt. A phone whose safe
  area starts 20pt down or less is a home-button phone, and the fade covers its whole 20pt status bar
  (`StatusBarFade.classicStatusBar`). Measured in the simulators with a red page scrolled under the
  fade and the screenshot masked to show the housing, in points (the minis' screenshots are 2.88px to
  the point):

  | Phone (iOS) | Safe area's top | Housing's bottom | Fade's end | Until 2026-10-05 |
  |---|---|---|---|---|
  | iPhone 17 Pro (26.3) | 62 | 50.7 | 50.0 | the same |
  | iPhone 16 (18.6), iPhone 14 Pro (16.4) | 59 | 48.0 | 47.0 | the same |
  | iPhone X (16.4), XS (18.6), 11 Pro (18.6) | 44 | 30.0 | 32.0 | the same |
  | iPhone 12 (18.6) | 47 | 32.0 | 33.67 | 35.0 |
  | iPhone 13 (26.3), iPhone 16e (18.6) | 47 | 33.67 | 33.67 | 35.0 |
  | iPhone XR (16.4), 11 (18.6) | 48 | 33.0 | 34.5 | 36.0 |
  | iPhone 12 mini (18.6 and 26.3) | 50 | 34.03 | 35.76 | 38.19 |
  | iPhone 13 mini (26.3) | 50 | 37.5 | 35.76 | 38.19 |
  | iPhone SE, 3rd generation (16.4) | 20 | 20 | 20 | the same |

  Every end now lies within 2pt of its line. The X, XS and 11 Pro end exactly 2pt below the notch,
  the most the rule allows, so their 12pt stands; the 12, XR, 11 and 12 mini ended 3–4.2pt below it
  before. The fade's 5pt ease begins below the clock on an island phone, whose digits end 11pt above
  the island's bottom, and on the SE, whose digits end 4.5pt above its status bar's: the mask at the
  digits' baseline is 0.85 or more there. A notch's digits end only 1–4.9pt above the notch, so on a
  notch phone the ease begins above their lowest rows, and the mask at their baseline is 0.52–0.80
  (0.74 on the X and XS, unchanged). Between the island's or the notch's bottom and the safe area's
  top, a strip of about 11–14pt beside the housing, scrolled content shows unfaded; that is what the
  owner asked for.
- **The colour behind the status bar.** Home passes nothing, and its fade is the page colour
  (`CleansiaColors.background`). A screen with a hero at its top passes `heroTint` and marks the hero
  with `statusBarFadeHero()`: the Plus offer passes `MembershipPalette.sky950`, the top of its navy
  hero, and Profile passes its hero's top, `ProfileTab.heroTop` ([above](#full-bleed-hero)). Profile
  takes its hero's colour for the same reason as Plus: the page colour over its blue hero was a pale
  band, on which the clock measured 1.7:1 in light mode. Both pass their hero's colour on every iOS
  version, because the screen asks for the white clock while the hero is behind it (below). From
  2026-10-04 until that change the Plus offer passed the page colour before iOS 17 in light mode
  (`fadeHeroTint`, gone): the system then drew the clock, signal and battery black whatever was under
  them, which read 1.7:1 on the 90 % navy in the iOS 16.4 simulator. The fade wears the hero's colour
  while the hero reaches below the fade, and cross-fades in proportion into the page colour as the
  hero's bottom passes up through it, from the fade's end to the top of the screen
  (`StatusBarFade.heroShare`), stepping once over the shades the clock cannot be read on (below). The
  hero's reader writes state only while the hero's bottom is within −80 to +20pt of the status bar's
  edge, in whole points (`heroBottomRange`), and only the fade reads it, so the content is not redrawn
  as it scrolls. The fade wears the hero's
  **top** colour, not the colour of the part under it: once Profile has scrolled far enough that its
  lighter lower gradient is behind the status bar, the band reads a shade darker than the hero
  beneath, like a status-bar backing. On Plus, whose hero runs from sky-950 to slate-900, the
  difference is slight. Measured from iOS 26.3 screenshots, the clock reads 12.1:1 over the Plus hero
  and 14–20:1 over the page colour; over Profile's hero, see [above](#full-bleed-hero).
- **The cross-fade steps over the shades the clock cannot be read on** (since 2026-10-05,
  `StatusBarFade.legibleShare`). From iOS 17 the system takes the clock's colour from the content under
  it, and it kept the clock white over a dark hero's cross-fade until the fade was nearly as light as
  0.5 luminance. In proportion all the way, the cross-fade from a dark hero into the light page took
  the clock to 2.21:1 on Profile and 2.22:1 on the Plus offer (screenshots, at 190pt and 365pt of
  scroll; finding 2026-10-05). The share keeps its proportion outside those shades and jumps across
  them at their middle: from the darkest share on which the white clock still reads 4.5:1, the fade at
  90 % over white content (`whiteClockLimit`), to the lightest on which the fade over the hero itself
  is light enough, luminance 0.6, that the system draws the clock black at 13:1 or more
  (`blackClockFloor`). Profile's sky-700 over the page steps between shares 0.956 and 0.210, the Plus
  offer's navy between 0.689 and 0.130. A dark page (dark mode), or a hero too light for the white
  clock, has no such shades and keeps the plain proportion. Measured again in light mode on the
  iPhone 17 (iOS 26.3), every 5–10pt: Profile's clock reads 3.60:1 at worst in the screenshots (4.5:1
  from the colour values), then black at 16:1; the Plus offer's 3.53:1 at worst (4.5:1), then 17:1.
  Dark mode is unchanged, 4.63:1 or more on Profile in the screenshots.
- **The clock is asked for white while the hero is behind it** (owner decision 2026-10-05). Until then
  the app set no status-bar style, so the clock, signal and battery were the system's: before iOS 17
  black in light mode whatever was under them, and from iOS 17 taken from the content, which at
  130–150pt of Profile drew them black on the 90 % sky-700 fade, about 4.1:1
  ([above](#full-bleed-hero)). `StatusBarStyleBridge` (`StatusBarFadeScrollView.swift`) is a
  `UIViewControllerRepresentable` whose controller answers `preferredStatusBarStyle`: the SwiftUI
  hosting controllers hand `childForStatusBarStyle` down to it, logged and measured on iOS 16.4, 18.6
  and 26.3. `.toolbarColorScheme(.dark, for: .navigationBar)` was tried first, and does nothing while
  the navigation bar is hidden, as it is on these screens. On a screen with a hero the band mounts the
  bridge and asks for `.lightContent` while `StatusBarFade.asksForWhiteClock` holds, that is while the
  fade at the drawn share reads 4.5:1 for the white clock over white content, and for the system's
  default once the page colour has taken over, where the step above has made the fade light enough
  for the black clock. Dark mode asks for white throughout. The Plus offer's states with no plan to
  price (loading, an error, none in the market), whose navy hero does not scroll, ask for white
  throughout. Home has no hero and no bridge, so its clock stays the system's. Measured every 5–10pt
  across Profile (0–300pt) and the Plus offer (0–520pt), light and dark, on the iPhone 17 (iOS 26.3),
  iPhone 16 (18.6) and iPhone 14 Pro (16.4) simulators. From the colour values, white on the hero at
  rest reads 5.93:1 on Profile and 13.88:1 on the Plus offer, white at worst 4.50:1 at the share where
  the fade steps across, and black 13.0:1 or more after it; in dark mode white throughout, at least
  5.97:1 on Profile and 10.31:1 on the Plus offer. In the screenshots, which run lighter
  ([above](#full-bleed-hero)): white 3.60:1 at worst on Profile and 3.48:1 on the Plus offer (16.4),
  black 16.25:1 or more, and 4.58:1 or more in dark mode. On iOS 16.4 the Plus hero at rest went from a
  black clock at 1.74:1 to white at 12.07:1, and its reduced hero from black to white at 13.28:1. On no
  runtime is the clock black on a hero-coloured fade.
- **What pins it.** `ContentSafeAreaBindingTests` pins the end within 2pt of each measured island,
  notch and status bar, the ten notch phones above among them, the mask at each measured clock's
  baseline (0.85 or more, 0.5 or more under a notch), the 5pt ease, the full-strength
  fallback, the cross-fade over the fade's span (it never rises back, and outside the stepped shades
  no step is larger than one point's share), the step over the illegible shades (for Profile and the Plus offer, every share from
  1 to 0 reads 4.5:1 for the white clock or is light enough for the black one, with one jump and never
  back; shares outside the shades and the cases with none are left alone, and the band draws the
  stepped share), the reporting band, both hero screens' wiring, the clock each drawn share asks for
  (when white is asked it reads 4.5:1 or more over white content, otherwise the fade is light enough
  for black; dark mode always asks for white; a light hero is left to the system), the bridge's
  controller answering `.lightContent` or `.default`, the band handing the bridge the share it paints,
  the reduced Plus hero mounting it, Profile's light hero top with no version check and the Plus offer
  with no `fadeHeroTint`, and that the fade has no material, no per-version branch and no system edge.
  Checked on the iOS 26.3, 18.6 and 16.4 simulators, light and dark: Plus with its hero under the
  status bar and with content scrolled past it, Profile's hero, and Home with a card under the clock;
  the end line also on the iPhone 16e and the iPhone SE.
- **Why not iOS 26's own soft edge.** From the first to the second remark of 2026-10-03, iOS 26 drew
  the system's soft scroll edge, the one a navigation bar draws: a `safeAreaBar` stand-in 24pt tall
  with a near-clear fill, then `scrollEdgeEffectStyle(.soft, for: .top)`, because the system draws its
  edge only under a bar and these screens hide theirs. iOS 16–25 laid an `.ultraThinMaterial` band
  over the status bar, masked to clear 24pt below it. The system draws its edge well below the status
  bar, a milky wash down to about 77pt on the iPhone 17 Pro, at a height an app cannot set, so it could
  not cover the status bar alone, and the 16–25 band ended in a visible line. Both are gone, with
  `StatusBarFade.depth`. When checking a scroll-driven effect, prefer real drags. In the 2026-10-03
  checks `UIScrollView.setContentOffset` did not update SwiftUI geometry; in the 2026-10-04 checks an
  animated `setContentOffset` did drive both the threshold and the hero cross-fade, on iOS 26.3, 18.6
  and 16.4.
- **Android** applies `Modifier.statusBarFade(scrollState, heroTint, heroHeight)` (customer
  `ui/components/StatusBarFade.kt`) directly before `verticalScroll(scrollState)`, so it draws over
  the viewport and not over the scrolled content. It is visible while `scrollState.value > 0`. Since
  2026-10-05 it draws the iOS rule:
  - **One solid colour, the one behind the status bar.** Home passes no tint, and its fade is the
    page background. The Plus offer passes `Sky950`, its hero's top, and Profile its hero's top
    colour (`profileHeroColors`, [above](#full-bleed-hero)), each with the hero's height as measured
    by `onSizeChanged`. The fade wears the hero's colour while the hero's bottom reaches the fade's
    end, and cross-fades in proportion into the page colour as that bottom passes up through it to the
    top of the screen (`statusBarFadeHeroShare`), the hero's colour laid over the page in its share as
    iOS lays it (`statusBarFadeColor`).
  - **It steps over the shades neither icon colour reads on** (since 2026-10-05). Between a dark hero
    and the light page lie colours on which neither the white icons nor the system's 60 % black ones
    read 4.5:1: on Profile the clock fell under it from 465 to 510px of scroll on the emulator, about
    17dp, and to 3.10:1 at 485px (finding 2026-10-05). `statusBarFadeIllegibleShares` finds those hero
    shares once per hero and page, judging the light icons over the fade at 90 % over white and the
    dark ones over the fade at 90 % over the hero itself, the worst of what can show through its last
    10 % (`statusBarClockReads`). `statusBarFadeLegibleShare` moves a share inside them to the legible
    step past the nearer edge, on the eight-bit steps the colour can hold, so the colour keeps its
    proportion everywhere else and jumps across the band in one pixel of scroll. The icons flip at that
    jump, because the 0.25 crossover always falls inside the band. A dark page has no such shades, so
    dark mode is unchanged, and the Plus offer's navy hero over the light page takes the same step.
    Measured again on Profile every 5px from 400 to 620px of scroll: 4.54:1 at worst, on the white
    icons over the fade's darker edge (465–490px), then 4.82:1 on the dark icons from 495px and 5.7:1
    on the page; in dark mode 7.44:1 at worst. The Plus offer could not be scrolled that far on the
    emulator, whose harness has no session and so no plans; the unit test checks its every share.
  - **Held at 90 %, eased out over its last 5dp, with no tail** (`STATUS_BAR_FADE_OPACITY`,
    `FadeEase`, a smoothstep sampled at nine points). 5dp is iOS's `StatusBarFade.falloff`, chosen so
    the ease starts below the clock; until 2026-10-05 Android eased over 6dp. Every stop is the one
    colour at some alpha: Android interpolates gradient colours unpremultiplied, so a stop of
    `Color.Transparent`, which is transparent black, passed through greys and tinted the light theme
    (2026-10-02).
  - **It ends at the clock's line**, the bottom of the display cutout's path (`cutoutPath`, API 31 and
    later), which is the camera hole itself and the line the system centres the clock and icons on
    (`statusBarFadeHeight`). The status-bar inset is not that line: on the Pixel 8 emulator it is
    132px, while the clock and icons span 50–81px and the hole ends at 102px. `cutoutExtentFor` picks
    the reader by API level. From API 31 it is the cutout's path. On API 28–30, which have no path, it
    is the span of the cutout's bounding rectangles (`DisplayCutout.getBoundingRects()`, since
    2026-10-05), so a phone there with a camera in its top edge also ends the fade on the cutout's line
    rather than below the clock. Below API 28 there is no cutout API, and the fade ends at the status
    bar's bottom, as it does with no cutout inside the status bar. `statusBarFadeHeight` decides, the
    same for both readers, whether the span is the clock's line (inside the status bar) or not (a side
    cutout in landscape). The rectangle is not the hole itself: on a Pixel 5 emulator at API 30 with a
    top cutout it runs 0–136px against a 145px status bar, so the fade ends at 136px, where it ended at
    145px before; that is the closest line the system offers below API 31. Checked on emulated cutouts
    only, not on a real API 28–30 phone. Until 2026-10-05 the band covered the status bar and a 24dp
    tail, 195px on the emulator, which hid Home's address line.
  - **The icons follow the colour.** On a screen with a hero, the status bar's icons are set light
    while the fade's colour has a relative luminance under 0.25 (`statusBarIconsLight`). A light theme
    draws them in 60 % black (`#636465` measured on the page), which reads better than white only on a
    lighter colour. A screen holds them while it is resumed, and the newest screen to set them owns
    them and hands them back to the theme's on pause or dispose, because Profile leaves composition
    only once Plus has entered. `CleansiaTheme` sets the bars in a `DisposableEffect(darkTheme)`, not a
    `SideEffect`: side effects run after every other effect in a frame, so it undid the screen's
    setting. Android sets the icons itself, as iOS asks for them since 2026-10-05, so on both the Plus
    fade is navy in light mode on every version.
  - **Home pads inside the scroll.** Home moved its status-bar padding inside the scroll, so the
    address bar starts below the status bar at rest and then scrolls under the fade. That leaves
    Home's `PullToRefreshBox` filling the whole screen, so its indicator pads `WindowInsets.statusBars`
    before its 8dp and rests below the status bar, not under it.
  - **Measured on the emulator** (API 35, 1080 × 2400), the clock over the Plus hero in light mode
    went from 1.35:1 to 14.2:1 at rest, where white icons replace the theme's dark ones, and reads
    14.1–14.2:1 scrolled, where it read 5.7:1 on the old white band. Plus in dark mode reads
    14.1–14.8:1, and Home 5.7:1 in light mode and 17.9:1 in dark as before, with the fade ending 30px
    higher. `StatusBarFadeTest` pins the hold, the ease and its 5dp, the single colour, the end line
    and the cutout rule (one test per API branch: the path from 31, the rectangles' span on 28–30,
    neither below 28), the cross-fade, the step (the shades exist for a dark hero over the light
    page and not over a dark one; at every share from 0 to 1 the clock reads 4.5:1 in the icons it is
    given, for Profile and Plus in both themes; one jump, never back) and the icon rule;
    `StatusBarFadeBindingTest` pins each screen's wiring, that the fade is coloured with the stepped
    share, Home's padding and refresh indicator, and both heroes' tint and height.

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
- **Just before submit**, in `submit()`, before anything is sent: no profile read, no quote, no order.
  (Until 2026-10-04 it also ran in the submit that followed a cash booking's card capture, which is
  gone.)

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

## A service a chosen package covers reads as covered {#package-covered}

On the booking's services step and on the schedule form, a service that a chosen package already
includes is drawn so the customer sees at a glance that it is booked already (owner remark
2026-10-04). The rule and its wording are in
[Charging a package and a service together](/product/business-rules#charging-a-package-and-a-service-together).
iOS is the reference, and Android draws the same values:

| | Value |
|---|---|
| Row fill | the brand primary at 8 % in light mode and 16 % in dark, over the row's card |
| Row border | 1.5 (pt or dp) of the primary at 60 %, in place of the neutral hairline; corners unchanged |
| Badge | under the name: a check in a circle, then the copy in semibold, at most two lines, padding 8 × 4, corners 12 (a capsule on one line, a rounded box on two), on the primary at 14 % in light mode and 24 % in dark |
| Badge ink, text and icon | `onPrimaryContainer`, sky-900 `#0C4A6E` in light mode and sky-100 `#E0F2FE` in dark |

**The ink is not the primary.** sky-600 reads 4.1:1 even on white and about 3.1:1 on the badge, under
the 4.5:1 its text needs. On the theme's own values the ink reads 7.2:1 in light mode and 6.1:1 in
dark on a covered row, and at least 5.5:1 on a picked one. The badge is part of the row's tap target and
of what VoiceOver and TalkBack read for it, its icon is hidden from both, and the row's add or select
control is unchanged.

- **iOS** (`Booking/Steps/ServicesStepComponents.swift`): `InPackageStyle` holds the values, and
  `InPackageNote` is the badge (`checkmark.circle.fill` at 13pt, Nunito semibold 14, the size of the
  row's secondary text). The booking's `ServiceRow` swaps the tint for its picked look when picked: the
  primary container at 50 % and a 2pt primary border. The schedule form's row has no picked fill, so a
  picked covered row keeps the tint under the primary border. `InPackageMarkerLookTests` pins the tints,
  the border, the contrast in both schemes and both lists drawing the covered row. Checked on the iOS
  26.3 and 16.4 simulators, light and dark: the badge text measures 5.8:1 and 4.7:1 from the
  screenshots, whose colours run darker than the tokens.
- **Android** (`features/booking/DoubleBooking.kt`): `inPackageRowFill()`, `inPackageRowBorder()` and
  the badge `InPackageMarker` (`Icons.Filled.CheckCircle` at 14dp, `labelMedium` semibold, 13sp, above
  the rows' 12sp secondary text). On both lists a picked row keeps its picked look, the 2dp primary
  border over its existing fill, and still carries the badge. `DoubleBookingTest` measures the ink over
  a covered and a picked row in both themes, from `LightColors` and `DarkColors` themselves, and pins
  the tints, the border and both lists. Unlike iOS, it has not yet been checked on a screen.

**The row's price and secondary text clear 4.5:1 on every row** (finding 2026-10-04, since
2026-10-05). Two texts on a service row read under it. The *from* price was the brand primary, sky-600
in light mode, which reads 4.1:1 on the plain card and 3.6–3.7:1 on a covered or picked row. In dark
mode the secondary text, a description or the per-room price, was the theme's slate-400, 4.2:1 on a
covered or picked row. Both apps now draw them in two inks of their own, and the primary is unchanged,
so the rows' fills, borders, ticks and badge stay the brand colour:

| Text | Light mode | Dark mode |
|---|---|---|
| The *from* price | sky-700 `#0369A1`: 5.9:1 plain, 5.4:1 covered, 5.2–5.4:1 picked | the primary, sky-400, as before: 6.8:1 plain, 5.0:1 covered, 5.1:1 picked |
| Secondary text, on a covered or picked row | the theme's slate-700, as before: 9:1 or more | slate-300 `#CBD5E1`: 7.2:1 or more |

A plain row keeps the theme's secondary colour in both modes, 5.7:1 in dark.

- **iOS**: `ServiceRow.fromPriceInk` and `ServiceRow.secondaryInk`, on the booking's services step.
  The schedule form's rows show neither text. `InPackageMarkerLookTests` checks both inks at 4.5:1 or
  more on the plain, covered and picked rows in both schemes, with the row tints composited as drawn,
  and that the row draws its price, description and per-room price in them.
- **Android**: `fromPriceInk` (`ServicesStep.kt`) and `rowSecondaryText` (`DoubleBooking.kt`), on the
  booking's `ServiceRow` and on the description of the schedule form's `ServiceCard`. Measured on the
  emulator, the light *from* price went from 4.10 to 5.93:1 on a plain row, 3.70 to 5.36:1 on a covered
  one and 3.57 to 5.17:1 on a picked one. The dark secondary text went from 4.16 to 7.19:1 on a covered
  row, and on a picked one from 4.94 to 8.54:1 (booking) and 6.32 to 10.91:1 (schedule form).
  `DoubleBookingTest` measures both inks over every row a service list draws, in both themes, and pins
  the three call sites.
- **The web** already met it. Its row prices are sky-700 in light mode and sky-300 in dark, 5.36:1 or
  more, and a covered row's description takes `--cl-muted-on-tint`
  ([the services step](/customer-app/ordering-flow#step-0-services-packages)).

Since 2026-10-05 the light *from* price is one case of the general rule below: iOS's
`ServiceRow.fromPriceInk` reads `CleansiaColors.primaryText` in light mode, and Android's `fromPriceInk`
draws the same sky-700.

## Blue text is sky-700, not the brand primary {#brand-text-ink}

The brand primary in light mode, sky-600 `#0284C7`, reads 4.10:1 on white and 3.91:1 on the page, and
less on the tints blue labels sit on, under the 4.5:1 that text needs (finding 2026-10-05). Since
2026-10-05 both apps, like the websites, draw blue **text** in an ink of its own, links and text
buttons included, and keep the primary for fills, borders, standalone icons and filled buttons:

| Client | Text ink | Light mode | Dark mode |
|---|---|---|---|
| iOS | `CleansiaColors.primaryText` (Core) | sky-700 `#0369A1`, 5.93:1 on white | sky-400, the primary, unchanged |
| Android | `ColorScheme.primaryText`, or `primaryText()` for the theme in force (`:core`, `cz.cleansia.core.ui.theme`, `BrandColors.kt`) | sky-700 `#0369A1` | the theme's primary, sky-400, unchanged |
| Web | `--cl-accent-text` on the customer site; the shared PrimeNG preset's text, outlined and link buttons on all three sites | sky-700; a link or a text button goes to sky-800 under the pointer | sky-300 on the customer site → [Blue text on the customer site](/architecture/frontend#accent-text), [links and text buttons](/architecture/frontend#link-ink) |

The two texts the finding named moved on both apps: the package details sheet's price, 4.10 to 5.93:1,
and the schedule form's default-address badge, 3.88 to 5.62:1 on iOS (on 40 % sky-100) and 3.38 to
4.89:1 on Android (on the primary at 12 %). A sweep of each customer app moved every other text drawn
in the primary: prices and totals, the confirmation code, *Order again*, *See all*, *Manage*, *View
all*, *Retry*, the add-address rows' label, the picked day part and arrival time, the default, current
tier and Plus badges, the referral code, the dispute pill for *in review* and *waiting*, a cleaner's
initial, *This device* and the Plus offer's social-proof headline among them. The lowest after reads
4.58:1, a cleaner's initial on the primary at 20 %. Each app's commit lists every site with its ratio
before and after (iOS `81ef1bafb` and `89b5361f9`, Android `302a0fa6a`). The dispute pill and, on
Android, the *Current* tier pill draw their 14 % wash from the ink, so the wash is now sky-700 at
14 %, a shade darker.

**Links and text buttons take it too, in both apps** (owner decision 2026-10-05: *"go to the darker,
but so that it still feels natural"*). After the sweep above the shared Core and `:core` components,
every text button and the whole partner app still drew the primary. Since then, in the customer and
the partner app alike:

- **The shared components**: a text link (`CleansiaTextLink`), a picked chip's label (its 12 % wash and
  border keep the primary), a dropdown's picked row and its check, the section header's badge, the
  consent text's links (the booking's contract notice among them) and an HTML legal text's links
  (iOS `HtmlDocument.linkLightHex`; the quote rule keeps the primary). On iOS also the reveal panel's
  *Show* and *Hide* with its chevron and lock, and the Live Activity's clock, countdown and step labels,
  whose wordmark, bar and dots keep the primary; on Android also the error state's back link.
- **Text and outlined buttons.** Android's text button is `:core`'s `CleansiaTextButton`, Material's
  `TextButton` with its content in the text ink. The 54 that drew Material's default ink (dialog
  actions, card actions, inline *Edit*s) use it, and `CleansiaTextLink` is built on it; a destructive
  one keeps Material's `TextButton` in its own red. iOS's shell tint is the text ink, so the back
  buttons and the toolbars' text buttons take it, and so do the schedule card's *Edit*, *Pause* and
  *Resume*. An outlined button's label is `onSurface` by default on both platforms, and since this
  change so is the leading icon of Android's `CleansiaOutlinedButton`, as on iOS. An outlined button
  with a blue label, *Make this recurring* among them, draws its label and icon in the text ink inside
  a primary outline.
- **The selected tab**, its icon and label in one ink, in both apps on both platforms. The pill under an
  Android tab is a fill and keeps the primary.
- **The partner app's links**, each with its icon: the earnings card's *View details*, *View period
  pay*, *Add photo* and the add-photo tile, the order's call and navigate chips, *Copy instruction*,
  the location prompt's action, the job-radius card's buttons, the available-jobs sort menu, the
  language chooser and its picked row, the pending offers card's call to action and the rejected
  registration step's *Contact support*.
- **The avatar's initials** on iOS are sky-700 on the white disc in both apps
  (`CleansiaColors.primaryTextOnFixedWhite`, 5.93:1 in both modes); `onFixedWhite`, the sky-600 the
  partner app still drew, is gone. Android's customer avatar already drew sky-700. Android's partner
  avatar is a different design, initials in the primary on a 40 % container disc, and is unchanged.

**One ink per control.** An icon inside a link or a button takes its label's ink, so no control shows
two blues. **What keeps the primary**: filled buttons (a white label on sky-600), fills and washes,
borders and outlines, toggles, sliders and progress bars, standalone icons, the consent checkbox's
tick box and the wordmark. Dark mode is unchanged on both platforms, the text ink there being the
primary. On iOS a circular spinner with no tint of its own follows the shell's tint, so it is sky-700
in light mode now, and so are the accents of iOS 16–18's compact date picker. Blue text in the partner
app that is neither a link nor a button, section labels, pay amounts and the selected segment among
them, still draws the primary (reported 2026-10-05).

Measured in light mode, before and after, from the token values: on white (cards, sheets, the
dropdown, the avatar disc, a legal page) 4.10 to 5.93:1, on the page 3.91 to 5.67:1, on a picked
chip's 12 % wash 3.52 to 5.10:1, on the contact chips' 10 % wash 3.61 to 5.23:1, on the add-photo
tile's 8 % 3.70 to 5.37:1, on Material's dialog surface 3.34 to 4.84:1, on iOS 26's tab bar 3.58 to
5.19:1 and on iOS 16–18's 3.89 to 5.64:1. Read off iOS 26.3 screenshots, which run lighter (below): a
text link 3.18 to 4.43:1, a picked chip 2.83 to 3.94:1 and the selected tab 3.21 to 4.51:1. Measured
on the Android emulator: a dialog's text button 3.34 to 4.84:1, a picked chip on the page 3.37 to
4.89:1, the call chip 3.45 to 5.00:1 and the selected tab's icon 4.10 to 5.93:1. Each app's commit
lists every site (iOS `688cf6160`, Android `48f0749c8`).

**Blue text on the light-blue container has an ink of its own in dark mode** (finding 2026-10-05).
`primaryContainer` is sky-100 in light mode and sky-700 in dark, and the text ink on it reads 5.17:1 in
light mode but 2.77:1 in dark, sky-400 on sky-700. Text drawn straight on that container takes
`CleansiaColors.primaryTextOnContainer` (iOS Core) or `ColorScheme.primaryTextOnContainer` (Android
`:core`): sky-700 in light mode, as the text ink, and sky-100 `#E0F2FE` in dark, 5.17:1 on sky-700. It
takes:

- in both apps, *This device* on the devices list, and the shared section header's badge, which has no
  caller today;
- in the customer app, the initial of an order's cleaner, and on iOS the default-address badge in the
  address manager and in the booking's saved-address chooser;
- in the partner app on iOS, the *Pending* invoice badge.

The customer's read 2.77 to 5.17:1 in dark mode and are unchanged in light. The partner's drew the
primary, 3.57:1 in light mode and 2.77:1 in dark, and read 5.17:1 in both. Left as they are, because
they already clear 4.5:1: Android's default-address badges, which sit on a 12 % primary wash and not on
the container (5.43:1 in dark), the fills washed from the container at 35–60 % (4.89–6.03:1), the
schedule form's badge on iOS's 40 % container (4.89:1), and the web's badges (5.17:1 or more in both
themes, [Blue text on the customer site](/architecture/frontend#accent-text)). Icons on the container,
which need 3:1, are not part of this, and in dark mode the primary's glyphs on its discs read 2.77:1
(reported 2026-10-05). iOS's `ComponentTextInkTests` checks the token at 4.5:1 or more on the container
in both modes, and `BrandTextInkTests` and the partner's `TextInkTests` pin the sites; Android's
`PrimaryTextTest` pins the token's two values and reads all three modules, so that no `Text` drawn
straight on a `primaryContainer` fill takes the text ink or the primary (iOS `235136257`, Android
`59c1d05f9`).

**A dispute's status pill reads 4.5:1 for every status, in both modes** (finding 2026-10-05). The pill
writes its label in the status's ink on a 14 % wash of that ink over the card, on the list row and the
detail header alike. *Pending* took the rating star's amber-500, 1.93:1 on its own wash in light mode;
*Resolved*, green-700 in both modes, read 4.15:1 in light and 2.57:1 in dark; *Closed* read 2.66:1 in
dark on Android and 4.47:1 on iOS; a status the app does not know, about 1.2:1. Both apps now pick a
light and a dark ink per status, the same pairs (iOS `DisputeStatusPresentation`, Android
`disputeStatusInk` in `DisputeFormatters.kt`), and the wash still comes from the ink:

| Status | Light mode | Dark mode |
|---|---|---|
| *Pending* | amber-800 `#92400E`, 5.70:1 | amber-500, as before, 5.33:1 |
| *Under review*, *Waiting for response* | the text ink, as before, 4.83:1 | the text ink, 5.21:1 |
| *Resolved* | green-800 `#166534`, 5.75:1 | green-400 `#4ADE80`, 6.19:1 |
| *Closed*, and a status the app does not know | slate-600 `#475569`, 6.13:1 | slate-300 `#CBD5E1`, 6.94:1 |
| *Escalated* | the error colour, as before, 5.09:1 | 5.79:1 |

Read off the iOS 26.3 screenshots, *Pending* went from 1.76 to 4.60:1 in light mode and *Resolved*
from 2.51 to 5.04:1 in dark; measured on the Android emulator, *Pending* went from 1.93 to 5.69:1 and
*Closed* in dark from 2.67 to 6.92:1. iOS's `DisputesListCardTests` and Android's
`DisputeStatusInkTest` check every status, an unknown one and none at 4.5:1 or more on their wash over
the card in both modes (iOS `96a1ea10e`, Android `a6b1b9942`). The web's pills already read 4.5:1 or
more: the customer's 4.79:1 or more in both themes, the admin's shared status badge 4.51:1 at its
lowest, the warning tone.

- **iOS**: `BrandTextInkTests` checks the token at 4.5:1 or more on every ground those texts sit on,
  pins the texts whose colour comes from a helper, and scans the customer sources so that no `Text`
  is drawn in the primary. `ComponentTextInkTests` (Core) finds no shared `Text` drawn in the primary
  but the wordmark's fallback, and pins the icons beside a text-ink label and the chrome that keeps
  the primary; the partner app's `TextInkTests` pins its sites. Checked on the iPhone 17 Pro (iOS 26.3)
  simulator, whose screenshots run lighter than the colours (sky-600 is captured as `#0097D2`, sky-700
  as `#007DB1`): the price read 3.30 to 4.60:1 there, the badge 3.17 to 4.41:1.
- **Android**: `PrimaryTextTest` (`:core`) pins the token's two values and 4.5:1 on every ground a link
  or a label sits on, the legal text's link and quote colours, and reads all three modules' sources:
  no `TextButton` on Material's default ink, no outlined button whose label or icon rides it, no
  button content and no `:core` text in the bare primary, both selected tabs in one ink, and the
  partner links with their icons. `PrimaryTextContrastTest` (customer) pins sky-700 at 4.5:1 or more
  on every light ground the customer's texts sit on, reads the customer sources so that no `Text`
  takes the bare primary or sky-600 again (39 did before), and no longer exempts labels inside
  buttons. Measured on the emulator: the package sheet's price 4.10 to 5.93:1, the schedule form's
  *DEFAULT* badge 3.37 to 4.89:1, the *MOST POPULAR* badge 3.51 to 5.08:1 and *Add new address* 3.91
  to 5.67:1.

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
