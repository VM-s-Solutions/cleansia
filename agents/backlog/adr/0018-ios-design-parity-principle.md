# ADR-0018 — iOS design-parity principle: same layout/flow/branding as the Android apps, built with NATIVE SwiftUI components, and iOS convention WINS on a genuine Android-vs-iOS conflict

- **Status:** accepted   <!-- proposed | accepted | superseded | rejected -->
- **Date:** 2026-06-23
- **Supersedes:** —
- **Superseded by:** —
- **Applies to:** ios | cross-cutting (process: a standing **Gate-DP** the reviewer + ios charters run on every iOS **screen** ticket)
- **Refines:** **ADR-0013** (iOS architecture & port strategy — the "parity port" definition). This ADR does **not** re-open any ADR-0013/0014 *architecture* decision; it makes the **visual/UX meaning** of "parity port" explicit, concrete, and reviewable. It sits beside **ADR-0016** (the Apple App Review / quality bar) as the second standing iOS reviewer gate. Consumes ADR-0014 (iOS-16 floor: SwiftUI, `ObservableObject`, the iOS-16 MapKit variant).
- **Ticket:** IOS-DESIGN-PARITY-ADR (this ADR) · **Consumers:** a one-line design-parity note on every iOS **screen/feature** ticket in `status/sprint-12.md` (the first vertical + each feature wave); the standing **Gate-DP** added to `agents/backlog/ios-app-review-checklist.md` (§G) + the sprint-12 reviewer-check #22; the living companion `architecture/decisions/ios-app-architecture.md` (design-parity section).

> This ADR freezes **what "parity port" means visually**. ADR-0013 said the iOS apps are a *parity port* of the
> Kotlin/Compose apps onto the same Mobile API contract, but did not pin what *visual* parity means — leaving
> room for two wrong readings: (a) pixel-clone Android (re-implement Material in SwiftUI), or (b) "native iOS"
> as a license to redesign screens/flows. The owner resolved both. It ships **no Swift code** — it is the
> principle + the reviewable gate + the ticket notes. Once `accepted` it is immutable — supersede, never edit.

> **Owner decision this ADR records (2026-06-23):** *"Both iOS apps look the same as Android, with
> iOS-component improvements."* Resolved via two follow-ups:
> 1. **BALANCE** — *"Same layout/flow/branding as Android, built with NATIVE SwiftUI components."* Identical
>    screens, navigation structure, user flows, branding, and visual layout as the Kotlin/Compose apps — but
>    implemented with **native iOS components** (native pickers, `.sheet`, nav bars, SF Symbols where they map
>    to the Android icons, haptics, swipe-back) so it feels right on iPhone. Same app, polished for the platform.
> 2. **CONFLICT RULE** — *"iOS convention WINS on genuine conflicts."* Where an Android pattern and an
>    iOS-native pattern genuinely conflict, use the **iOS-native** pattern — that **is** the "iOS component
>    improvements" the owner asked for. Keep layout/flow/branding identical; **upgrade the components.**

---

## Context

ADR-0013 fixed iOS as a **parity port** (same Mobile API contract, no invented product behavior, mirror the
Android *code* not the doc — e.g. the `OnTheWay` lifecycle). ADR-0014 set the SwiftUI/`ObservableObject`/iOS-16
substrate. Neither pinned the **visual/UX** meaning of "parity," and that ambiguity is load-bearing because it
shapes **every screen ticket** (T-0303 onward) and the iOS reviewer gate. The owner's clarification removes the
ambiguity: parity is of **layout, flow, and branding** (the *what* and the *arrangement*), and the
**components** are native iOS (the *how* of each control) — with the **iOS convention winning** wherever the two
platforms' idioms genuinely diverge.

This is **one decision** — "the iOS design-parity principle" — because the BALANCE and the CONFLICT RULE are
inseparable: the BALANCE ("same layout/flow, native components") is under-defined without a tie-breaker for the
cases where "same layout" and "native component" pull apart, and the CONFLICT RULE ("iOS wins") is meaningless
without the BALANCE's scope (it wins **on the component**, never on the layout/flow/branding). They are the two
halves of one rule and ship as one ADR. The *implementation* is per-screen, in the feature tickets.

---

## Decision

> **Design-parity principle.** An iOS screen is correct when **(1)** its **layout, flow, and branding match the
> corresponding Android Compose screen** (cite it), **(2)** every control is a **native SwiftUI component** — no
> Material re-implementation — using the **standard iOS pattern** for that control, and **(3)** wherever the
> Android idiom and the iOS-native idiom **genuinely conflict, the iOS-native pattern is chosen** and the
> divergence is noted in the ticket. Parity is on the **layout/flow/branding**; the **iOS convention wins on
> the component.**

### D1 — What is held identical to Android (parity is NON-negotiable here)

- **Screen inventory + per-screen content.** The set of screens, and *what is on each screen*, matches the
  Android app one-to-one (the Android parity map is the source of truth). iOS does not add, drop, merge, or
  split screens vs Android. (Behavioral parity — same lifecycle, same flows — is already ADR-0013.)
- **Navigation structure + user flow.** The same screen graph and the same step order: the partner
  Take→OnTheWay→Start→Complete order-loop, the customer 3-step booking flow (Services → WhenWhere → Confirm),
  the same tab structure, the same back-stack semantics. iOS does not re-route a flow.
- **Branding.** Colors, logo, typography scale, spacing rhythm, iconography *meaning*, the mascot/empty-state
  identity — the design tokens (T-0297) reproduce the Android brand. An iOS screen must be recognisably *the
  same app*.
- **Information architecture + layout arrangement.** The same regions in the same places (header/content/
  primary-action), the same grouping and ordering of fields/sections. A field that is above the fold on
  Android is above the fold on iOS.

### D2 — What is upgraded to native iOS (the "iOS component improvements")

- **Every control is a native SwiftUI component.** **No Material re-implementation in SwiftUI** (no faux
  Material text field, no faux Material bottom sheet, no custom ripple). Use the platform control: `TextField`/
  `SecureField`, `DatePicker`, `Picker`/`Menu`, `Toggle`, `.sheet`/`.confirmationDialog`/`.alert`,
  `NavigationStack` + nav bars, `TabView`, `List`/`Form`, `swipeActions`, `.refreshable`.
- **Platform affordances are expected, not optional.** SF Symbols **where they map to the Android icon's
  meaning** (the same semantic, the native glyph); the **swipe-back** edge gesture + nav-bar back button;
  **haptics** on the right moments (success/error/selection); pull-to-refresh; context menus where Android used
  a long-press menu. These are the "polished for the platform" the owner asked for.
- **The `Cleansia*` shared components (T-0297) are native-SwiftUI wrappers** that carry the **brand** (D1) over
  the **native control** (D2) — they are the brand-skin on the platform control, never a Material clone. (This
  is the SwiftUI analogue of how `:core`'s `CleansiaButton`/`CleansiaTextField` skin Compose's Material
  controls — here they skin *native iOS* controls instead.)

### D3 — The conflict rule: iOS-native wins on a genuine component conflict (and the divergence is noted)

When an Android UI pattern and the iOS-native pattern for the **same job** genuinely conflict — i.e. faithfully
copying the Android *component* would produce something un-iOS — choose the **iOS-native** component, keep the
layout/flow/branding (D1) identical, and **note the divergence in the ticket** (one line: "Android X → iOS-native
Y, because iOS convention"). Named example mappings (the canonical set a reviewer checks against — not
exhaustive):

| Android (Compose / Material) pattern | iOS-native component it BECOMES | What stays identical (D1) |
|---|---|---|
| Compose **bottom navigation bar** (`NavigationBar`) | SwiftUI **`TabView`** (bottom tab bar) | the **same tabs in the same order** (Dashboard·Orders·Invoices·Profile / Home·Orders·Rewards·Profile + Book) |
| Compose **`ModalBottomSheet`** / the AnchoredDraggable booking sheet | **`.sheet` + `.presentationDetents`** (e.g. `.medium`/`.large` for the 3-snap booking sheet; native drag-to-dismiss) | the **same 3 steps, same content, same snap intent** (Services/WhenWhere/Confirm) |
| Compose **`DatePicker`/`TimePicker`** (Material wheels/calendar) | native **`DatePicker`** (`.graphical`/`.wheel`/`.compact` as fits) | the **same field, same label, same when/where step placement** |
| **Material `TextField`** (floating label, underline/box) | native **`TextField`/`SecureField`** with the **same label text + the same validation/error copy** | the **same form fields, same order, same labels, same error strings (×5 locales)** |
| Android **system back** (hardware/gesture) + Material top-app-bar back | iOS **swipe-back edge gesture + `NavigationStack` nav-bar back** | the **same back-stack / the same destination on back** |
| **Coil** `AsyncImage` (Compose image loading) | **SwiftUI `AsyncImage`** (or Kingfisher if caching parity is needed) with the **same frame/aspect/placeholder layout** | the **same image placement, size, and placeholder/empty layout** |
| Material **`Snackbar`** (the `SnackbarController` bus) | a native **bottom toast/overlay** surfaced by the same `SnackbarController` bus (ADR-0011 D2: VM surfaces it) | the **same message, the same one-per-failure semantics** |
| Material **`AlertDialog`** | native **`.alert` / `.confirmationDialog`** | the **same title/body/actions, same destructive-action semantics** |

The rule's boundary: iOS wins on the **component**, **never** on the **layout/flow/branding** (D1). "iOS
convention" is not a license to move a flow, drop a field, re-order steps, or re-brand — only to render each
control the native way.

### D4 — Reviewable form (so the principle is a gate, not a vibe)

Every iOS **screen/feature** ticket is checked against three concrete, verifiable assertions — **Gate-DP**
(recorded in §G of `ios-app-review-checklist.md` + sprint-12 reviewer-check #22):

1. **Layout/flow/branding parity (cite Android).** "This screen's layout, flow, and branding match the
   corresponding Android Compose screen `<path/Screen.kt>`." The ticket cites the specific Compose screen; the
   reviewer compares region arrangement, flow position, field set/order, and brand.
2. **Native components, standard iOS pattern.** "Every control is a native SwiftUI component (no Material
   re-implementation); the standard iOS pattern is used for [nav / pickers / sheets / lists / back / images]."
   The reviewer confirms no faux-Material control and that the platform affordances (swipe-back, SF Symbols,
   haptics, detents) are present where applicable.
3. **Conflicts resolved iOS-native + noted.** "Where an Android and an iOS convention conflicted, the
   iOS-native pattern was chosen and the divergence is noted." The reviewer confirms each divergence is the
   table's mapping (or a justified new one) and that **no** divergence touches layout/flow/branding.

### D5 — Scope guard

This ADR refines the **visual/UX** meaning of ADR-0013's "parity port" and adds **Gate-DP**. It does **not**:
write Swift code; re-open any ADR-0013/0014 architecture decision (package shape, auth, codegen, the
MapKit-default, Stripe, push, lead app, i18n); change the screen *inventory* or *behavior* (that is ADR-0013
parity); or renumber/restructure the sprint-12 tickets. A *new* Android↔iOS mapping a feature surfaces (beyond
D3's table) is decided in that feature ticket under this principle and folded into the table by a living-doc
note (a genuinely new *rule* about the principle itself would be a superseding ADR).

---

## Alternatives considered

- **Pixel-clone Android (re-implement Material in SwiftUI).** Rejected — this is reading (a) the owner explicitly
  ruled out. A faux-Material iOS app feels wrong on iPhone (no swipe-back, wrong pickers, wrong sheets), fights
  the platform, and is *more* work (re-building controls iOS already provides). The owner wants native
  components — that is the "improvements" half.
- **"Native iOS" as a redesign license (re-think screens/flows for iOS).** Rejected — this is reading (b) the
  owner ruled out. It would fork the two platforms' UX, double the design surface, and break the parity that
  makes one product. Layout/flow/branding are held identical (D1); only the components change.
- **Android pattern always wins (faithful even on conflicts).** Rejected by the CONFLICT RULE — it produces the
  un-iOS result (Material sheets, Android date wheels, no swipe-back) the owner is correcting. iOS wins on the
  component (D3).
- **iOS pattern wins on everything (including layout/flow).** Rejected — over-reads the conflict rule. iOS wins
  on the **component only**; layout/flow/branding parity is non-negotiable (D1, the rule's boundary). Moving a
  flow "because iOS" is a Gate-DP failure.
- **Leave it as a companion-doc note (no ADR).** Rejected — it is a **load-bearing principle that shapes every
  screen ticket and adds a standing reviewer gate**; ADR-0016 set the precedent that a standing iOS gate is an
  ADR + a checklist artifact, not folklore. A companion-only note would leave the gate unenforceable and the
  conflict rule (a real trade-off with rejected alternatives) without a defended record. It is recorded as a
  small ADR refining ADR-0013, plus the gate in the standing checklist, plus the living-doc companion.

---

## Consequences

**Cheaper / safer:**
- **"Parity port" is now unambiguous** — no screen ticket can drift into pixel-cloning Material or into an
  iOS redesign. Every dev and reviewer reads the same concrete rule + the same Android→iOS mapping table.
- **Native components are less code and pass the Apple bar more easily** — using `DatePicker`/`.sheet`/`TabView`
  instead of re-implementing Material reduces surface, and native affordances (swipe-back, Dynamic Type,
  VoiceOver on standard controls) feed straight into ADR-0016's HIG/accessibility item (AR-QUAL-1).
- **The brand-skin-over-native-control split** keeps the shared `Cleansia*` components doing exactly one job
  (carry the brand) over the platform control — the same clean seam Android's `:core` components have.

**More expensive (new obligations):**
- **Every iOS screen ticket cites its Android Compose counterpart** and is checked against Gate-DP's three
  assertions — a small per-ticket cost that the reviewer enforces.
- **Each genuine Android↔iOS divergence is noted in-ticket** (the one-line mapping). A divergence that touches
  layout/flow/branding is a **Gate-DP failure** (the reviewer rejects it).
- **The mapping table is a living artifact** — a new control mapping a feature surfaces is folded back into the
  table (living-doc note) so the set converges instead of being re-decided per screen.

**Plan impact (parallel, this change — no renumber/restructure):**
- A one-line design-parity note on the first vertical (T-0303) + each feature-wave screen ticket
  (T-0304/0305/0306/0307/0308/0309/0310/0312/0313/0314): "satisfies Gate-DP — cite the Android Compose screen;
  native SwiftUI components; iOS-wins-on-conflict, divergences noted."
- **Gate-DP** added as §G of `ios-app-review-checklist.md` and as sprint-12 reviewer-check **#22**; it runs on
  every iOS **screen** ticket alongside ADR-0016's **Gate-AR** and the SwiftLint/SwiftFormat gate (ADR-0016 D1).

---

## How a reviewer verifies compliance (Gate-DP — the three assertions of D4)

Recorded as §G of `agents/backlog/ios-app-review-checklist.md` (the standing iOS reviewer gate) + sprint-12
reviewer-check **#22**. On every iOS **screen/feature** ticket:
1. **Layout/flow/branding parity (Android cited).** The ticket names the corresponding Compose screen; the
   reviewer confirms region arrangement, flow position, field set + order, and branding **match**. A moved
   flow / dropped field / re-brand is a blocking finding.
2. **Native components only.** No Material re-implementation; every control is a native SwiftUI component using
   the standard iOS pattern; platform affordances (swipe-back, SF Symbols mapping the Android icon meaning,
   haptics, `.presentationDetents`, pull-to-refresh) present where applicable. A faux-Material control is a
   finding.
3. **Conflicts iOS-native + noted.** Each Android↔iOS divergence is the canonical mapping (D3) or a justified
   new one, is **noted in the ticket**, and touches **only the component** (never layout/flow/branding). An
   undocumented divergence, or one that moves layout/flow, is a finding.

(Gate-DP is a **screen-ticket** gate; pure-infra tickets — codegen, the auth layer, the DI root — are N/A.)

---

## Roles affected

No new code roles. The **reviewer + ios** charters own Gate-DP (as they own Gate-AR). Catalog edit (same
change): `agents/knowledge/patterns-mobile.md` iOS section gains the design-parity principle (same
layout/flow/branding, native SwiftUI components, iOS-wins-on-conflict) + the Android→iOS mapping table, so a
faux-Material control or a moved flow is a catalog deviation. The living companion
`architecture/decisions/ios-app-architecture.md` gains a design-parity section. ADR-0013's header gains a
"Refined by ADR-0018" pointer (a status-block pointer, not a body edit).

---

## Challenge / Defense / Verdict trail (condensed)

Author drafted the principle + the conflict rule + the mapping table + Gate-DP; challengers (parity-fidelity,
scope-creep, enforceability) attacked; the Lead adjudicated. **Verdict: all challenges RESOLVED; zero blocking;
consensus reached.**

| # | Challenge (severity) | Disposition | Where |
|---|---|---|---|
| CH-1 (fidelity) | "iOS convention wins" is a back-door to redesign screens — doesn't it gut the parity ADR-0013 promised? (MAJOR) | REBUT | D3 boundary + D1: iOS wins on the **component only**, **never** layout/flow/branding. The rule's explicit boundary + Gate-DP assertion #3 (a divergence that touches layout/flow is a **failure**) close the back door. Parity is preserved on the *what*; only the *how* of each control is native. |
| CH-2 (the other extreme) | If layout/flow are sacred, doesn't that force pixel-cloning Material (the thing the owner rejected)? (MAJOR) | REBUT | D2 + Alternatives: layout/flow/branding identical does **not** mean *component* identical — every control is the **native** SwiftUI one (no Material re-impl). The brand-skin-over-native-control split (D2) is exactly how you keep the brand while using the platform control. Both wrong readings (pixel-clone / redesign) are explicitly rejected. |
| CH-3 (enforceability) | "Looks the same, feels native" is a vibe, not a gate — how does a reviewer *check* it? (MAJOR) | CONCEDE + REVISE | D4 + the Gate-DP three assertions: cite-the-Android-Compose-screen, native-components-only, conflicts-iOS-native-and-noted. The named mapping table (D3) makes "native pattern" concrete per control. It is now three checkable assertions on every screen ticket, recorded in the standing checklist (§G) + reviewer-check #22. |
| CH-4 (form) | Is a whole ADR warranted, or is a companion note enough? (MODERATE) | DEFEND | Alternatives: it shapes **every** screen ticket + adds a **standing gate** — ADR-0016's precedent is that a standing iOS gate is an ADR + a checklist artifact. The conflict rule is a real trade-off with rejected alternatives needing a defended, immutable record. A tight ADR refining ADR-0013 (not re-opening it) + the gate in the existing checklist is the clean house-discipline form. |
| CH-5 (drift) | New screens will surface control mappings the table doesn't list — does the principle fragment? (MODERATE) | DEFEND | D5 + Consequences: a new mapping is decided in that feature ticket under this principle and **folded back into the table** (living-doc note) so the set converges. Only a new *rule about the principle* would be a superseding ADR; a new *control mapping* is a living-doc fold-in. |

**Affirmed unchallenged:** the owner's BALANCE + CONFLICT RULE wording is captured verbatim; parity remains on
layout/flow/branding (extends ADR-0013, does not re-open it); native SwiftUI components (no Material re-impl);
the `Cleansia*` components are brand-skins over native controls; Gate-DP is a screen-ticket gate (infra tickets
N/A); it sits beside Gate-AR (ADR-0016) and the lint gate.

**Escalations to the owner:** none — this ADR *records* the owner's stated design principle and makes it
reviewable. No new product/business window is invented.
