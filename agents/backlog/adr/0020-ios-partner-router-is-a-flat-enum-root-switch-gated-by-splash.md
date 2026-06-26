# ADR-0020 — the iOS partner router is a flat-enum root-switch (`PartnerRootView`), gated by a `.splash` decision state that re-resolves login → shell-vs-lock — NOT a path-based `NavigationStack` audience router

- **Status:** accepted   <!-- proposed | accepted | superseded | rejected -->
- **Date:** 2026-06-26
- **Supersedes:** —
- **Superseded by:** —
- **Applies to:** ios | mobile (the partner-app navigation contract) | cross-cutting
- **Refines / consumes:** **ADR-0013** D2 (`ObservableObject`/sealed-state VMs — via ADR-0014 D2′) +
  D9 (partner-first; the shell sits behind the gate the lead vertical proves) and ADR-0013's
  "mirror the Android **code**, not the doc" parity rule. **ADR-0014** (iOS-16 floor — `NavigationStack`
  is iOS-16+, available; this decision is floor-compatible). **ADR-0019** (the generated-client auth seam
  the gate's status call rides — `employeeCheckCurrentEmployee` goes out through the Core-spine factory).
  Consumes the T-0303 living-doc note "the fuller session/splash gating is **T-0304's SplashGate** concern;
  do not grow `hasValidSession` into it" — this ADR is that gating, made canonical. **Not a UX-parity call —
  ADR-0018 (Gate-DP) governs the *screens* (login/lock/dashboard); this ADR governs the *router shape between*
  them.**
- **Ticket:** surfaced by the T-0304 Understand pass (the Phase-2 partner shell + SplashGate + RegistrationLock)
  · **Consumers:** T-0304 (builds it) and **every later partner navigation wave** (T-0305 auth-completeness /
  onboarding chain, T-0307 order-loop, T-0309 pay, T-0310 profile) — they extend this router, not reinvent it.
  The customer wave (T-0312+) copies the *pattern* (its own root view + audience states).

> **The decision in one line.** The partner app routes its **top-level audience** (logged-out vs
> resolving vs locked vs in-shell) with the **flat-enum `PartnerRootView` root-switch** T-0303 already
> shipped — extended with `.splash` (the decision state) and `.registrationLock` cases, seeded
> `hasValidSession ? .splash : .login`, where a verified login routes to `.splash` (the Android
> "bounce through Splash so registration re-checks" idiom, `PartnerNavHost.kt:118-124`) and `.splash`
> re-resolves to `.dashboard`-shell vs `.registrationLock` vs `.login`. It is **not** re-architected
> into a path-based `NavigationStack` audience router. It ships **no feature code** — it is the canonical
> partner router shape every later wave consumes; the concrete cases are built in T-0304.

---

## Context

**What T-0303 already shipped (the on-disk shape this decision canonicalizes).**
`src/cleansia_ios/CleansiaPartner/Sources/PartnerRootView.swift` is a **flat-enum root-switch**:

- a `@State private var route: Route` over `enum Route { case login; case dashboard; case verifyEmail }`,
  seeded in `init` as `container.hasValidSession ? .dashboard : .login`;
- a `switch route` in the body renders the matching root screen (`LoginView` / `DashboardView` /
  `PlaceholderVerifyEmailView`);
- a `NavigationStack` **wraps** the switch (the per-screen push container), and a `.task` drains
  `sessionManager.forcedSignOutStream` to reset `route = .login` on a forced sign-out;
- `Route.afterLogin(_ success:)` already branches the verified-vs-unverified login
  (`requiresEmailConfirmation ? .verifyEmail : .dashboard`) — the §7.2 router-gate the T-0303 panel made
  a **required** security test (`PartnerRootRouteTests.swift`).

So the partner app already has a working **root-switch** audience router. The `NavigationStack` present is
the **intra-audience push stack**, not the audience selector — the audience is the enum.

**What the Android partner does (the parity source, verified).** Android's `PartnerNavHost.kt` is a
path-based `NavHost` whose **`startDestination = NavRoute.Splash`** (`:72`). The `SplashGate`
(`:448-509`) resolves a `SplashViewModel` to one of `{Authenticated, Unauthenticated, NeedsOnboarding,
NeedsRegistrationLock}` and the host navigates accordingly (`:74-97`), `popUpTo(Splash){inclusive=true}`
each time so Splash is consumed. The load-bearing idiom: a verified **login navigates to `Splash`**, not
straight to Main (`:118-124`, "Bounce through Splash so SplashGate re-checks registration status and routes
to Main vs Lock"); `ConfirmEmail` success does the same (`:149-155`); and the `RegistrationLock`'s
`onCompleted` navigates to `Main` popping the lock (`:197-201`). The **shell (`MainScaffold`, Orders/etc.)
is only ever reached from `Authenticated` or the lock's completion** — it is unreachable until the gate
passes.

**The tension this ADR resolves.** Two valid iOS readings of "mirror that navigation":

1. **Flat-enum root-switch (what T-0303 shipped):** the top-level audience is a Swift `enum`; a root view
   `switch`es over it; `NavigationStack` is the per-audience push container. Android's `Splash → {Main |
   Lock | Login | Onboarding}` becomes `enum Route { splash; dashboard(shell); registrationLock; login; …}`.
2. **Path-based `NavigationStack` audience router:** model `Splash`/`Login`/`Lock`/`Main` as
   `NavigationPath` destinations and drive the audience by pushing/replacing the path — a near-literal
   transliteration of Android's `NavHost` + `NavRoute` + `popUpTo`.

The catalog currently has a row that points the *generic* "Android typed routes" at "`NavigationStack` +
typed route enum" (`patterns-mobile.md` Android↔iOS table, the `navigation.Routes` row). That row is right
for **intra-audience** navigation (pushing OrderDetail, ProfileSection, etc. onto a stack) but is **silent
on the top-level audience switch** — and T-0303 resolved the audience switch as a root-`enum`, not a path.
Left unstated, T-0305/0307/0309/0310 would each re-decide whether a new top-level state is an `enum` case or
a `NavigationStack` path — exactly the per-ticket reinvention the pattern-evolution loop exists to prevent.
This ADR pins the **one** partner-router shape and reconciles the catalog.

This is **one decision** — "the partner router shape + how the gate sits in it" — because the shape choice
and the gate placement are inseparable: choosing the root-`enum` is what makes the gate a **state**
(`.splash`) the root switches into rather than a screen pushed on a stack, and the "login → `.splash` →
re-resolve" idiom only reads cleanly when the audience is a switch the splash state re-drives (a pushed-path
splash would have to pop-and-replace, the awkwardness Android pays with `popUpTo(...){inclusive}` on every
hop). The *implementation* (the concrete `.splash`/`.registrationLock` cases + the `SplashGateViewModel`) is
the T-0304 ticket.

---

## Decision

> **Contract principle.** The partner app's **top-level audience routing** is the **flat-enum
> `PartnerRootView` root-switch** (the T-0303 shape), extended — **not** replaced by a path-based
> `NavigationStack` audience router. `NavigationStack` remains the **intra-audience push container**
> within a root state; it is **not** the audience selector.

### D1 — The router is `PartnerRootView` switching over a flat audience `enum`

The `enum Route` T-0303 shipped grows the two states T-0304 needs:

```swift
// PartnerRootView.Route — extended from T-0303's { login, dashboard, verifyEmail }.
enum Route: Equatable {
    case splash            // NEW (T-0304): the decision state — resolves to one of the others
    case login
    case verifyEmail       // existing T-0303 placeholder (real flow = T-0305)
    case registrationLock  // NEW (T-0304): the fail-closed gate (ADR / sprint-12 §7.4 Decision 1)
    case dashboard         // = the authed SHELL (the TabView: Dashboard·Orders·Invoices·Profile)
}
```

- `PartnerRootView` `switch`es over `route` and renders the matching root surface; `NavigationStack` wraps
  the switch (the per-audience push container, unchanged from T-0303).
- **`.dashboard` becomes the shell.** Where T-0303's `.dashboard` rendered the bare `DashboardView`, T-0304's
  `.dashboard` renders the **`PartnerShellView`** (the SwiftUI `TabView` — Android `MainScaffold` parity,
  `MainScaffold.kt:44-49` the `Dashboard·Orders·Invoices·Profile` tabs). The existing `DashboardView` becomes
  the Dashboard **tab's** content. (Gate-DP governs the shell screen; this ADR governs that it is the
  `.dashboard`/shell *state* the router lands in only past the gate.)

### D2 — The seed changes from `.dashboard` to `.splash` (closing the T-0303 fail-open)

The T-0303 seed `hasValidSession ? .dashboard : .login` was correct for the *proving vertical* (no gate
existed yet) but is a **fail-open hole** for T-0304: it lands an authenticated-but-not-registration-complete
partner **straight on the shell**, ungated. T-0304 changes the seed to:

```swift
_route = State(initialValue: container.hasValidSession ? .splash : .login)
```

- A returning session no longer short-circuits to the shell; it enters `.splash`, which re-resolves the
  registration gate (D3). This is the iOS analogue of Android's `startDestination = Splash` (`:72`) — an
  authed user always passes through the gate, never around it.
- A logged-out user still goes straight to `.login` (Android's `Unauthenticated`/`NeedsOnboarding` branch;
  onboarding-vs-login is **T-0305's** concern — the partner `.splash` for T-0304 resolves only the
  authed branch, see D5).

### D3 — `.splash` is the SplashGate decision state (login bounces through it; it re-resolves)

`.splash` renders a `SplashGateView` backed by a `SplashGateViewModel` (the `SplashViewModel` parity,
`PartnerNavHost.kt:478-509`) that, on appear, **resolves the registration gate once** and drives `route`:

- **No/empty session** → `.login` (defensive; the seed already routes logged-out users to `.login`, but the
  splash re-checks so a forced sign-out mid-resolve is handled).
- **Session + registration complete** (the AND predicate, sprint-12 §7.4 Decision 1) → `.dashboard` (shell).
- **Session + NOT complete** → `.registrationLock`.
- **Session + the status API `.failure`** → `.registrationLock` (**fail CLOSED** — the
  `SplashViewModel` `ApiResult.Error → NeedsRegistrationLock` parity, `PartnerNavHost.kt:506`; never the
  shell). This is the SplashGate half of the fail-closed contract recorded in sprint-12 §7.4.

**The "bounce through Splash" idiom (the load-bearing piece).** A verified, token-bearing login does **not**
route straight to `.dashboard`. `Route.afterLogin` changes so a **verified** login routes to **`.splash`**
(which re-resolves shell-vs-lock), keeping `.verifyEmail` for the unverified branch:

```swift
static func afterLogin(_ success: LoginSuccess) -> Route {
    success.requiresEmailConfirmation ? .verifyEmail : .splash   // was: .dashboard
}
```

This is the exact Android idiom (`PartnerNavHost.kt:118-124`): login success → `Splash` → re-check → Main
vs Lock. **It preserves the T-0303 §7.2 security gate** (`requiresEmailConfirmation == true → .verifyEmail`,
a token-bearing *unverified* partner never lands authed) **and** adds the registration gate on top of it (a
token-bearing *verified-but-incomplete* partner never lands on the shell — they land on `.registrationLock`).
The required T-0303 router-gate test (`PartnerRootRouteTests`) is **extended**, not broken: `verifyEmail`
still asserts on `requiresEmailConfirmation == true`; a new assertion covers `false → .splash`.

The lock's **completion** (the watermark/unlock fires) routes `.registrationLock → .dashboard` (Android's
`onCompleted → Main` popping the lock, `:197-201`); the lock's **sign-out** routes `→ .login`
(Android's `onSignedOut → Login`, `:202-205`). The forced-sign-out stream still resets any state to
`.login` (the T-0303 `.task` over `forcedSignOutStream`, unchanged).

### D4 — `NavigationStack` is the intra-audience push stack, not the audience router

Within a root state, pushing a detail screen (OrderDetail from the Orders tab, a ProfileSection from the
Profile tab, the onboarding-chain sections from the lock) uses the **`NavigationStack` + typed route enum**
the catalog already prescribes (`patterns-mobile.md` `navigation.Routes` row). The split is:

- **Top-level audience** (login / splash / lock / shell) = the `PartnerRootView` **`enum` switch** (this ADR).
- **Intra-audience navigation** (push/pop within the shell or the lock) = **`NavigationStack` paths** (the
  existing catalog row).

This is the same split Android draws — the **router shape is one `NavHost`**, but the audience hops use
`popUpTo(...){inclusive=true}` (a replace, not a push: the audience is *switched*, not *stacked*) while
detail navigation is a normal push. The iOS root-`enum` makes the "switch, don't stack" semantics explicit
(setting `route` replaces the whole root; there is no audience back-stack to accidentally pop into a
logged-out state), which is *safer* than a path the user could swipe-back out of.

### D5 — Scope guard

This ADR decides the **partner top-level router shape + how the gate sits in it** (the `enum`-switch, the
`.splash` decision state, the seed, the login-bounce idiom, the `NavigationStack`-is-intra-audience split).
It does **not**: write the T-0304 wiring (the ticket); decide the **fail-closed predicate / lock-screen
content / SplashGate fail-closed semantics** — those are sprint-12 §7.4 Decision 1 (a confirmation of the
Android gate, no new trade-off); decide the **onboarding `NeedsOnboarding` branch** (Android's 4th
SplashOutcome) — that is **T-0305** (the partner auth-completeness/onboarding chain), homed in the §7.4
deferral map; or decide any **screen UX** (Gate-DP governs login/lock/shell screens). The **customer** app
(T-0312+) gets its **own** root view with its own audience states (Home shell + Book FAB, Google/SIWA sign-in)
— it copies this *pattern*, not the partner enum. A future top-level audience state (e.g. a maintenance/kill
screen) is a new `enum` case under this ADR (a living-doc fold-in), not a new router.

---

## Alternatives considered

- **Path-based `NavigationStack` audience router (transliterate Android's `NavHost` + `NavRoute` +
  `popUpTo`).** Rejected (D1/D4). It is the most *literal* Android mirror, but: (a) it **discards the working
  T-0303 `PartnerRootView` root-switch** and the §7.2 router-gate test built on it, for no behavioral gain;
  (b) the audience hops are all **replace** semantics (`popUpTo(...){inclusive=true}` on every Android hop) —
  modeling a *replace* as a `NavigationPath` push-then-clear is more code and more foot-guns (a stray
  swipe-back or a path left un-cleared can strand the user in a stale audience, e.g. swiping back from the
  shell into a consumed `.splash` or a logged-out `.login`); (c) the audience is a **small closed set**
  (login/splash/lock/shell) that an `enum` models exactly and exhaustively (the compiler checks every case is
  handled) — a `NavigationPath` is the right tool for an **open, growing** push stack (detail screens), the
  wrong tool for a **closed audience switch**. The literal-transliteration instinct (ADR-0013's "mirror the
  code") is satisfied by mirroring the **decision tree** (Splash → {shell | lock | login}), not the
  **mechanism** (`NavHost` paths) — the same "parity is of behavior, not vendor/mechanism" logic ADR-0013 D6
  used for MapKit-vs-Mapbox.
- **Keep T-0303's seed `hasValidSession ? .dashboard : .login` and gate *inside* the shell.** Rejected (D2).
  It lands an authed-but-incomplete partner **on the shell first** and then tries to lock them — a
  fail-**open** window (the shell, Orders included, is momentarily reachable) on the exact security gate this
  whole feature exists to enforce. The gate must sit **between** login and the shell (sprint-12 §7.4
  Decision 1), which means the router resolves it **before** rendering the shell — `.splash` first, shell only
  on pass. Android puts `Splash` *before* `Main` for precisely this reason.
- **A single `RootRouterViewModel` (an `ObservableObject`) owning `route` instead of `@State` on the view.**
  Not rejected as wrong — it is a refinement T-0304 *may* take (lifting `route` into a VM so the splash/lock
  callbacks and the forced-sign-out stream mutate one observable source rather than the view's `@State`). The
  ADR fixes the **shape** (flat-enum audience switch, `.splash` decision state, the seed + bounce idiom); whether
  `route` lives in `@State` (T-0303's form) or a small root VM is an implementation choice the ticket makes
  under this ADR, not a separate decision. (Recorded so T-0304 isn't blocked on re-litigating it.)
- **Two separate roots — a `LoggedOutRootView` and a `LoggedInRootView` swapped by `hasValidSession`.**
  Rejected — it splits the audience switch across two views and a boolean, which is *less* exhaustive than one
  `enum` (the `.splash`/`.verifyEmail`/`.registrationLock` "in-between" states belong to neither cleanly) and
  duplicates the forced-sign-out reset. One root view over one audience `enum` is the simpler, exhaustively-checked
  shape.

---

## Consequences

**Cheaper / safer:**
- **One canonical partner router** — T-0305/0307/0309/0310 add a top-level state as an `enum` case (or a tab
  inside `.dashboard`'s shell) under this ADR; none re-decides the router shape. The seam does not
  metastasise into per-ticket navigation reinvention.
- **The audience switch is exhaustive + replace-only by construction** — a Swift `enum` switch is
  compiler-checked (no unhandled audience), and setting `route` replaces the whole root (no audience
  back-stack to swipe-back into a logged-out or consumed-splash state). This is *safer* than a path-based
  audience router for a security-gated app.
- **It builds on proven code** — the T-0303 `PartnerRootView` + its `forcedSignOutStream` reset + the §7.2
  router-gate test are extended, not thrown away. The login-bounce idiom is a two-line change
  (`afterLogin` returns `.splash` not `.dashboard`) plus the new `.splash`/`.registrationLock` cases.
- **The gate sits where it must** — `.splash` resolves before the shell renders, so the fail-closed gate
  (§7.4 Decision 1) is structurally enforced: there is no router path from login to the shell that bypasses
  `.splash`.

**More expensive (new obligations):**
- **The catalog `navigation.Routes` row is now split-scoped** — it must say `NavigationStack`+typed-route-enum
  is for **intra-audience** navigation, while the **top-level audience** is the root-`enum` switch (the
  catalog edit below). A reviewer check (#23) is added so a later wave doesn't model a top-level audience state
  as a pushed path.
- **The `SplashGateViewModel` is new hand-written code that must be tested** — its resolve-once decision tree
  (complete → shell, incomplete → lock, `.failure` → lock, no-session → login) is the iOS sibling of the
  Android `SplashViewModel` and carries the TC-IOS-REGLOCK SplashGate cases (sprint-12 §7.4).
- **The customer wave pays the pattern, not the code** — T-0312 builds its **own** root view + audience enum
  (different states: onboarding/sign-in/shell-with-Book-FAB). Recorded so it copies the *shape* and doesn't try
  to reuse `PartnerRootView`.

---

## How a reviewer verifies compliance

**The new check (composes with ADR-0013 #1–#10, ADR-0014 #11–#13, ADR-0019 #13-gen, ADR-0016 #14–#21,
ADR-0018 #22):**

**#23 — partner top-level audience routing is the flat-enum `PartnerRootView` root-switch, gated by `.splash`.**
1. **The audience is an `enum` switch, not a pushed path.** `PartnerRootView` owns a `route: Route` over a
   closed `enum` (`splash`/`login`/`verifyEmail`/`registrationLock`/`dashboard`) and `switch`es over it; a
   top-level audience state modeled as a `NavigationPath`/`navigationDestination` push is a finding. The
   `NavigationStack` present is the **intra-audience** push container (OrderDetail, ProfileSection, the
   onboarding-chain sections) — not the audience selector.
2. **The seed and the bounce.** The root is seeded `hasValidSession ? .splash : .login` (NOT `.dashboard` —
   the T-0303 seed is a fail-open hole once the gate exists); `Route.afterLogin` returns `.splash` for a
   verified login (NOT `.dashboard`) and `.verifyEmail` for `requiresEmailConfirmation == true`. A verified
   login that routes straight to `.dashboard` (bypassing `.splash`/the gate) is a **blocking** finding.
3. **`.splash` resolves before the shell.** There is **no** router path from `.login` to `.dashboard` (the
   shell) that does not pass through `.splash` (the gate). The `.dashboard` state renders the shell only;
   `.registrationLock` and `.dashboard` are both reached **only** from `.splash`'s resolution (or the lock's
   completion → `.dashboard`).
4. **Forced sign-out + lock callbacks replace, not stack.** Setting `route` (forced-sign-out → `.login`,
   lock-complete → `.dashboard`, lock-sign-out → `.login`) replaces the whole root; there is no audience
   back-stack the user can swipe-back into a consumed/stale audience.

**Test contract (T-0304 + the partner navigation waves):**
- **TC-IOS-ROUTER-SEED:** the root seeds `.splash` when `hasValidSession`, `.login` otherwise (NOT
  `.dashboard`).
- **TC-IOS-ROUTER-BOUNCE:** a verified login (`requiresEmailConfirmation == false`) routes to `.splash`
  (NOT `.dashboard`); an unverified login routes to `.verifyEmail` (the extended T-0303 §7.2 router-gate test).
- **TC-IOS-SPLASH-RESOLVE:** the `SplashGateViewModel` resolves complete → `.dashboard`, incomplete →
  `.registrationLock`, `.failure` → `.registrationLock` (fail closed), no-session → `.login`. (The fail-closed
  cases are shared with TC-IOS-REGLOCK, sprint-12 §7.4.)

(Gate-DP, ADR-0018, separately governs the login/lock/shell **screens**. This check governs the **router
between** them — it is infra-shaped, but it *is* checked on T-0304 because T-0304 is the screen ticket that
builds the router.)

---

## Roles affected

CRC (added with the T-0304 wiring, joining the planned `agents/knowledge/roles/ios-*` cards):

- **`ios-partner-root-router`** (new, thin) — `PartnerRootView` + its `Route` enum (and, if T-0304 lifts it, a
  small `RootRouterViewModel`): *responsibility:* select the partner app's **top-level audience**
  (login / splash / verifyEmail / registrationLock / shell) and replace the root on an audience change.
  *Collaborators:* `SessionManager` (the `forcedSignOutStream` reset + `hasValidSession` seed via the
  `AppContainer`), the `SplashGateViewModel` (resolves the gate), the login/lock callbacks. *Does NOT know:*
  the registration predicate (that is the `RegistrationLockViewModel`/`SplashGateViewModel`'s — the router
  just lands the *state* they decide), how any screen renders internally, or any business payload.
  **If the router ever evaluates `hasCompletedProfile`/`areDocumentsUploaded`/`contractStatus` itself, the
  responsibility is wrong — it must land the state the gate VM decides.**
- **`ios-splash-gate-vm`** (new) — `SplashGateViewModel`: *responsibility:* resolve the registration gate
  **once** on appear (status complete → shell, incomplete/`.failure` → lock, no-session → login) — the
  `SplashViewModel` parity, **fail closed on `.failure`**. *Collaborators:* the profile/registration repo
  (reads `employeeCheckCurrentEmployee` via the ADR-0019 generated-client seam), the `TokenStore` presence
  (via `hasValidSession`), the router. *Does NOT know:* how the lock screen renders, the shell, or any token
  value (presence only).

The existing `ios-header-adapter`/`ios-session-refresher`/`ios-generated-client-auth-bridge` CRCs are
unchanged — the gate's status call is just another caller of the generated-client auth seam.

**Catalog edits (same change, per the pattern-evolution loop):** `agents/knowledge/patterns-mobile.md` iOS
section — the `navigation.Routes` row is **split-scoped**: the top-level **audience** is the flat-enum
`PartnerRootView` root-switch (gated by `.splash`, seeded `hasValidSession ? .splash : .login`, verified
login bounces through `.splash`); `NavigationStack` + typed route enum is for **intra-audience** push
navigation. A top-level audience state modeled as a pushed path is a deviation (reviewer #23). The living
companion `agents/architecture/decisions/ios-app-architecture.md` gains a *Partner router shape* note.
Sprint-12 records the T-0304 acceptance note + reviewer-check #23.

---

## Challenge / Defense / Verdict trail (condensed)

Author drafted (grounded in the on-disk `PartnerRootView.swift` + the verified Android `PartnerNavHost.kt`
/ `MainScaffold.kt` + the T-0303 §7.2 router-gate + the living-doc "SplashGate is T-0304's concern" note);
challengers (literal-parity, fail-open-seam, future-cost) attacked; the Lead re-verified every citation
against the real iOS + Android code and adjudicated. **Verdict: all challenges RESOLVED; zero blocking;
consensus reached.**

| # | Challenge (severity) | Disposition | Where |
|---|---|---|---|
| CH-1 (literal parity) | ADR-0013 says "mirror the Android **code**" — Android is a path-based `NavHost`, so the faithful port is a `NavigationStack` audience router, not a root-`enum`. (MAJOR — the parity rule is load-bearing) | REBUT | D1/D4 + Alternatives: parity is of the **decision tree** (Splash → {shell | lock | login}), not the **mechanism** (`NavHost` paths) — the same "behavior not vendor/mechanism" logic ADR-0013 D6 used for MapKit. Android's audience hops are all **replace** (`popUpTo{inclusive}`), which a closed Swift `enum` switch models more safely + exhaustively than a `NavigationPath`. And T-0303 **already shipped** the root-switch with a passing §7.2 gate test — switching to paths discards working, defended code for zero behavioral gain. |
| CH-2 (fail-open seam) | If the router seed stays `hasValidSession ? .dashboard : .login` and the gate lives inside the shell, isn't that simpler? (MAJOR — it touches the security gate) | REBUT | D2 + the §7.4 Decision-1 placement rule: gating *inside* the shell lands an authed-but-incomplete partner **on Orders first** — a fail-OPEN window on the exact gate this feature enforces. The seed must be `.splash` so the gate resolves **before** the shell renders; reviewer #23.3 checks there is **no** login→shell path bypassing `.splash`. Android puts `Splash` before `Main` for this reason. |
| CH-3 (the bounce) | Why route a verified login to `.splash` and re-fetch, instead of straight to `.dashboard`? It's an extra network round-trip. (MODERATE) | DEFEND | D3 + `PartnerNavHost.kt:118-124`: the bounce is the **only** thing that applies the *registration* gate to a fresh login (the login response says nothing about profile/docs/contract). Straight-to-`.dashboard` would put a verified-but-unapproved cleaner on the shell. The re-fetch is the gate; it is the same `employeeCheckCurrentEmployee` call the lock + splash share, through the ADR-0019 seam. The §7.2 `verifyEmail` gate is preserved (unverified still → `.verifyEmail`). |
| CH-4 (future cost) | A flat enum will balloon as more audiences appear (onboarding, maintenance, kill-screen) — won't it become an unwieldy switch? (MINOR) | DEFEND | D5 + Consequences: the top-level audience is a **small closed set** (login/splash/lock/shell + T-0305's onboarding) — exactly what an `enum` models well; growth is *exhaustively-checked* (the compiler flags an unhandled new case). The thing that grows open-endedly — **detail screens** — stays on `NavigationStack` paths (D4). A new audience state is one `enum` case + a living-doc fold-in, not a new router. |
| CH-5 (state home) | Should `route` live in a `RootRouterViewModel`, not the view's `@State`? Pinning `@State` over-constrains T-0304. (MINOR) | CONCEDE (narrow) | Alternatives: the ADR fixes the **shape** (flat-enum audience switch, `.splash` decision state, seed + bounce), **not** where `route` is stored. T-0304 may lift it into a small root VM; recorded as an allowed implementation refinement so the ticket isn't blocked re-litigating it. |

**Affirmed unchallenged:** the on-disk `PartnerRootView` shape (flat `enum` switch, `hasValidSession` seed,
`afterLogin` branch, `forcedSignOutStream` reset); the Android `startDestination = Splash` + the
login/confirm "bounce through Splash" idiom + `SplashOutcome` set + the shell-only-from-Authenticated
reachability; `NavigationStack` as the intra-audience push container; ADR-0014 floor compatibility
(`NavigationStack` is iOS-16+); ADR-0019 as the seam the gate's status call rides; ADR-0018 (Gate-DP)
governs the screens, this ADR the router between them.

**Lead verification (against the on-disk iOS + Android code, 2026-06-26):**
`PartnerRootView.swift:4-51` (the flat-enum root-switch, seed `:11`, `afterLogin` `:47-49`,
`forcedSignOutStream` reset `:18-22`); `PartnerRootRouteTests.swift` (the §7.2 router-gate test extended,
not broken); `PartnerNavHost.kt:72` (`startDestination = Splash`), `:74-97` (SplashGate → audience hops with
`popUpTo{inclusive}`), `:118-124` (login bounces through Splash), `:149-155` (confirm bounces through
Splash), `:197-205` (lock onCompleted → Main, onSignedOut → Login), `:448-509` (`SplashGate` +
`SplashViewModel` decision tree, `.Error → NeedsRegistrationLock` `:506`); `MainScaffold.kt:44-49` (the
`Dashboard·Orders·Invoices·Profile` tabs the `.dashboard` shell mirrors); the living-doc §"Session-presence"
note ("the fuller session/splash gating is T-0304's SplashGate concern; do not grow `hasValidSession` into
it"). Confirmed: the decision extends the shipped root-switch and mirrors the Android decision tree, not its
path mechanism.

**Escalations to the owner:** none — this is a within-mandate design call composing accepted ADRs (ADR-0013
D2/D9, ADR-0014, ADR-0019) and canonicalizing the shape T-0303 already shipped. No new product/business
window; no owner input required.
