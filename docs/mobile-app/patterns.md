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

The customer Book FAB sits centred on the top edge of the bottom bar, so half of it rises above the
bar. Content scrolled to the end must clear the **FAB's top**, not the bar's. Only the four tab roots
(Home, Orders, Rewards, Profile) need this, because a pushed screen or a sheet covers the whole shell,
FAB included.

- **iOS.** The native `TabView` insets each tab's scroll content by its own bar (49pt) and nothing
  else, so the FAB's 33pt overhang covered the last card on every tab. The shell gives each of the
  four roots a `safeAreaInset(edge: .bottom)` of `BookFabMetrics.scrollClearance`: the overhang
  (`chromeEnvelope − systemTabBarHeight`) plus 12pt, which is 45pt. The clearance is derived from the
  same constants that place the FAB, so the two cannot drift apart. `contentMargins` would do the same
  job but needs iOS 17, which is above the floor.
- **Android.** The bar box is 12dp + a 64dp pill + 12dp = 88dp above the navigation-bar inset, and the
  FAB's top sits on the box's top edge. Each tab root ends with
  `Spacer(Modifier.navigationBarsPadding().height(MainShellBottomClearance))`, where the clearance is
  `88.dp + 16.dp`. The navigation-bar inset is therefore added at runtime, and a 3-button bar is about
  twice as tall as the gesture handle. The fixed `108.dp` it replaced ignored the inset and left the
  last item under the FAB in either mode.

**A state that fills an empty tab centres between the title and the FAB's top**, the area that is
actually visible. Orders is the only tab that does this today. On iOS, a `ScrollView` proposes no
height, so a centring stack inside it collapses. The empty and error states therefore take their
minimum height from a `GeometryReader` around the scroll view (`containerRelativeFrame` is iOS 17+).
With the clearance above in place, that height runs from the title to the FAB's top. Before this, a
fixed `minHeight` band started under the title and the block sat high. On Android, the
`ScrollableStateContainer` box keeps the viewport height but pads off the navigation-bar inset and
`MainShellBottomClearance`. Before this, it centred over the area under the floating bar too, and the
block sat low. Both keep pull-to-refresh.

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
