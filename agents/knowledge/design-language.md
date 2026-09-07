# Cleansia Design Language

> **The tool for every UI decision in this repo.** Read it before designing, and run §7 before showing
> anything to the owner. `patterns-frontend.md` / `patterns-mobile.md` say how to build a component;
> this says what it should look like, how the page should be structured, and what it may claim.

**Contents**
1. [Prime directive — parity with the apps](#1-prime-directive)
2. [The system — exact values](#2-the-system)
3. [What AI-generated looks like](#3-what-ai-generated-looks-like)
4. [What human-designed looks like](#4-what-human-designed-looks-like)
5. [Structure](#5-structure)
6. [Content](#6-content)
7. [The pre-submission review](#7-the-pre-submission-review)
8. [Workflow](#8-workflow)

---

## 1. Prime directive

**Cleansia is one product on four surfaces.** The Android and iOS apps are the reference — they share
byte-identical font binaries, one colour ramp, one shape scale and one component set. The web matches
them. A concept that changes the typeface, the brand ramp or the button shape is a **rebrand**, not a
design; say so out loud rather than shipping it as a concept.

Everything in §2 is lifted from the shipped apps. Do not invent an alternative.

---

## 2. The system

### 2.1 Colour — the full ramp

From `customer-app/ui/theme/Color.kt` and `CleansiaColors.swift`.

| Role | Token | Hex |
|---|---|---|
| **Headings, top-bar titles** | `Sky700` | **`#0369A1`** — the file's own comment reads *"brand secondary, top-bar title"* |
| Deep heading / footer ground | `Sky900` | `#0C4A6E` |
| Darkest brand | `Sky950` | `#082F49` |
| **Primary action** | `Sky600` | `#0284C7` |
| Dark-mode primary, gradient end | `Sky400` | `#38BDF8` |
| Container / chip fill | `Sky100` | `#E0F2FE` |
| Tinted section ground | `Sky50` | `#F0F9FF` |
| Body text | `Slate700` | `#334155` |
| Muted / captions | `Slate500` | `#64748B` |
| Hairline | `Slate200` | `#E2E8F0` |
| Page ground | `Slate50` | `#F8FAFC` |
| Card surface | white | `#FFFFFF` |
| Success | text `#15803D` on `#DCFCE7` | green-700 / green-100 |
| Error | text `#B91C1C` on `#FEE2E2` | red-700 / red-100 |
| Rating star | `#F59E0B` | amber-500 |

> **Headings are `Sky700`, not black.** This was flagged twice by the owner and is the single most
> repeated mistake. Near-black `Slate900` headings are what a generic template does; the app has a
> brand heading colour and the web must use it.

### 2.2 Gradients — categorical, never decorative

`BrandGradients.kt` defines four pairs. They identify a **category of card**; they are never a hero
wash or a button fill.

| Pair | Light | Used for |
|---|---|---|
| Blue | `#0284C7 → #38BDF8` | the default booking card |
| Purple | `#7C3AED → #A78BFA` | recurring / setup prompts |
| Cyan | `#0891B2 → #67E8F9` | referral |
| Plus | `#082F49 → #0F172A` | Cleansia Plus |

**A gradient behind a whole hero is the #1 AI tell (§3).** A gradient as one card's identity inside a
system of four is a design decision. Keep it the second thing.

### 2.3 Type

**Poppins** headings, **Nunito** body — bundled, byte-identical across Android and iOS. Poppins has
no Cyrillic; the Nunito fallback must stay (`uk` and `ru` are live locales).

| Slot | Face | Size / line-height |
|---|---|---|
| displayLarge / Medium | Poppins Bold | 32 / 28 · lh 40 / 36 · ls −0.5 / −0.4 |
| headlineLarge / Medium / Small | Poppins SemiBold | 24 / 22 / 18 |
| titleLarge / Medium | Nunito Bold | 16 / 15 |
| bodyLarge / Medium | Nunito Regular | 16 / 14 · lh 1.6–1.8 |
| labelLarge / Medium / Small | Nunito Bold / SemiBold / Bold | 14 / 12 / 12 |

Web may exceed `displayLarge` for a marketing hero — the one sanctioned divergence. Heading
line-height **1.1–1.3**, body **1.6–1.8**; those must differ (§3.2). Large headings carry deliberate
negative tracking, never the browser default.

### 2.4 Shape and elevation

`Shapes`: `6 · 12 · 16 · 24 · 32`. Default card **16**, sheets and hero cards **32**.
Primary buttons are **pills** (`CircleShape`) at heights **40 / 48 / 56** with horizontal padding
**16 / 24 / 32**.

Elevation is Material-neutral. **Never a coloured glow** — `rgba($primary, …)` in a `box-shadow` is a
defect. Ceiling for a resting button: `0 2px 6px rgba(15,23,42,.12)`.

### 2.5 Spacing

8-pt grid with 4-pt extensions: `2 · 4 · 8 · 12 · 16 · 20 · 24 · 32 · 40`.
**Vary it.** Identical padding on every block is a tell (§3.3).

### 2.6 Shared components

`CleansiaSectionHeader` (badge + title + subtitle) · `CleansiaPrimaryButton` (pill) · `CleansiaChip` ·
`CleansiaTextField` · `CleansiaDialog` · `MascotEmptyState` · `OrderTrackerBar` · `TrustStrip` ·
`PopularPackageCard` · `SnapSheet` · `SudsRefreshIndicator` · `WordmarkSplash`.

The customer mobile home reads: address bar → upsell carousel → trust strip → order-again → recurring
schedules → popular packages → recent bookings → milestone progress → seasonal card.

### 2.7 The mascot

18 stills, 13 clips in `apps/cleansia.app/src/assets/images/mascot/`. Drawn with a **heavy navy
outline, flat fills, one warm accent**. `MascotEmptyState` already makes him a component.

Rules: never on a coloured blob, never with a drop shadow, and never only once on a page.

---

## 3. What AI-generated looks like

Research-backed. Sources at the end of this section. These are the tells — the things that make
thousands of sites look like each other.

### 3.1 Colour
- **Purple-to-blue gradient hero** — named repeatedly as the single biggest giveaway. Models have seen
  thousands of SaaS landing pages and this is their default "looks professional" choice.
- Gradient buttons and gradient text.
- More than three colour families (brand + neutrals + one accent is the ceiling).
- `box-shadow` on more than two element types.
- Alternating `#f5f5f5` / `#fafafa` section bands as the only rhythm device.

### 3.2 Typography
- **Inter, Roboto, Open Sans, Poppins-as-everything** with no second choice made.
- Heading and body from the same family at the same weight.
- Uniform size increments between heading levels.
- **Same line-height for headings and body** — headings need 1.1–1.3, body 1.6–1.8.
- Browser-default letter-spacing on large display type.

### 3.3 Layout
- **Three or more consecutive sections that are all 3-column card grids.**
- Every section the same shape: `heading → description → cards`.
- **Identical padding, identical radius, identical card height everywhere.** Real systems create
  hierarchy through *intentional variation*; uniformity reads flat.
- Centred body text (centre only heroes and short headings).
- The stock hero: full-screen, huge centred text, two side-by-side CTAs.

### 3.4 Copy
- Buzzwords: *unlock, empower, seamless, leverage, streamline, robust, elevate, transform.*
- Abstract headlines that could belong to any company — "Build the future of work", "Your all-in-one
  platform". Compare Stripe's *"Financial infrastructure for the internet"* or Linear's *"Plan and
  build products"*.
- Generic CTAs — "Learn more", "Get started" — instead of "Spočítat cenu".
- Multi-sentence subtitles.

### 3.5 Imagery and icons
- Emoji as section or card icons.
- Stock photos of diverse people around a laptop; abstract 3D blobs.
- The same image reused across sections.
- AI illustration — too smooth, too symmetrical, slightly plastic.
- Gradient placeholder boxes standing in for real images.

### 3.6 Motion and interaction
- **Hover states that do nothing**; buttons that snap instead of easing.
- The same fade-up-on-scroll on every section — and note that fade-up-with-spring, cursor-reactive
  gradients and tilt-on-hover cards are *themselves* now defaults, not differentiators.
- More than two CTAs in one section.
- Touch targets under 44px.

**Sources:** [925 Studios — AI Slop Web Design](https://www.925studios.co/blog/ai-slop-web-design-guide) ·
[AIToolPick — 30-point checklist](https://aitoolpick.org/blog/ai-generated-website-checklist/) ·
[This Is Also — Why every website looks the same](https://thisisalso.com/blog/every-website-looks-the-same) ·
[Webflow — 2026 trends](https://webflow.com/blog/web-design-trends-2026) ·
[grzzly — Handmade design as a trust signal](https://grzz.ly/blog/handmade-design-trust-signal-ux-conversion/)

---

## 4. What human-designed looks like

The inverse list — the craft signals. These are what the owner means by *"professionalism and skills
to develop websites"*.

**One authored decision.** *"Pick the one decision your competitors would never make and commit to
it."* One committed choice breaks sameness better than ten polished details. For Cleansia the
candidates are the mascot cast, the suds motif, and publishing the real operational rules.

**Content first, layout second.** Write the real headline, the real microcopy, the real numbers —
then shape the layout around them. Generic cards-and-grids first is the template path.

**Intentional variation.** Different sections have different *shapes*, not just different content.
Spacing changes where the subject changes. Card sizes differ where importance differs.

**Considered states.** Every interactive element has rest / hover / active / focus / disabled, and the
transitions ease rather than snap. Hover that does nothing is the loudest tell in §3.

**Restraint.** Human-centred pages aren't packed because features exist. Fewer, larger, better.

**Evidence of a person.** Real photography with visible art direction, illustration with an
imperfection, copy in a voice. Cleansia has all three available: the mascot, real before/after job
photos, and a Czech voice that can be plain rather than corporate.

**Detail that survives inspection.** Optical alignment, tabular numerals in price columns, consistent
icon stroke weight, no orphaned words in headings, correct Czech typography (`„quotes"`, non-breaking
spaces after single-letter prepositions `v `, `s `, `k `, `z `).

---

## 5. Structure

**Every section must differ in shape from its neighbours.** Before drawing, list the sections and
name each one's *form*. If two adjacent forms match, change one.

A usable form vocabulary for this product:

| Form | Good for |
|---|---|
| Split hero (copy ⟷ mascot or tool) | the offer |
| Inline tool (calculator, picker) | making "fixed price" concrete |
| Horizontal strip (trust, parameters) | facts that need no explanation |
| Numbered sequence | process, with who-acts marked |
| Media pair (before/after) | proof |
| Ledger / table | rules with numbers |
| Feature card row | services — **use once, not three times** |
| Editorial band (inverted ground) | one strong claim |
| Q&A columns | objections |

**Rhythm.** Vertical padding changes with subject: related sections `clamp(56px, 8vh, 80px)`, subject
changes `clamp(88px, 14vh, 128px)`. Even rhythm reads as a template.

**One primary action per page**, repeated down the page. Never two competing filled buttons in one
section — a secondary is a text link or a low-emphasis chip.

**Above the fold** must answer: what is this, what does it cost, what do I do next. For a booking
marketplace the highest-converting pattern is a **booking tool in the hero itself** — service, size,
date — so the visitor sees a real price without leaving the page.

---

## 6. Content

### 6.1 Voice
Plain Czech, second person, concrete. Say the number. No buzzwords (§3.4). Subtitles are one sentence.
CTAs name the outcome — *"Spočítat cenu"*, not *"Zjistit více"*.

### 6.2 Only claims the system can prove

No invented ratings, customer counts or testimonials; no star rows rendered from a literal. The real
material is stronger and unrepeatable by a competitor's generated page:

| True claim | Source |
|---|---|
| Fixed price before you book | `QuoteOrder` / `OrderPricingCalculator` |
| Book online end to end — no phone call | Product |
| Free cancellation to 24 h; 25 % at 4–24 h; 50 % under 4 h | `business-rules.md` |
| 15 minutes to change your mind — 60 on a first booking | `business-rules.md` |
| Cleaner cancels: full refund **and** 500 Kč credit | `business-rules.md` |
| Windows 08:00–20:00, one-hour slots | `business-rules.md` |
| From 4 h notice; express from 2 h (+20 %) | `business-rules.md` |
| Pick your cleaner again — first refusal up to 12 h | ADR-0036 / ADR-0045 |
| Plus 199 Kč/mo or 2 030 Kč/yr, 14-day trial | seed `MembershipPlans` |
| Prague + ~30 km · own equipment · eco products | FAQ |
| Every cleaner admin-approved before taking work | `ContractStatus.Approved` |

Prices per service are **not** established — mark them clearly as samples until a real ceník exists.
Ratings become legitimate when `OrderReview` has rows and the page renders the aggregate.

---

## 7. The pre-submission review

**Run this before showing any concept.** Every line is a yes/no. A no is a fix, not a note.

**Parity**
- [ ] Headings are `Sky700 #0369A1` — not black, not slate
- [ ] Primary action is `Sky600`, buttons are pills at 40/48/56
- [ ] Poppins headings + Nunito body, Cyrillic fallback intact
- [ ] Radius from `6/12/16/24/32`; spacing from the 8-pt scale
- [ ] Gradients used categorically, never as a hero wash

**Anti-generic**
- [ ] No purple→blue hero gradient, no gradient text, no gradient buttons
- [ ] No three consecutive 3-column card grids
- [ ] Adjacent sections differ in *form*, not just content
- [ ] Padding and radius vary intentionally across the page
- [ ] Heading line-height (1.1–1.3) differs from body (1.6–1.8)
- [ ] Large headings carry deliberate letter-spacing
- [ ] No emoji as icons; icon stroke weight consistent
- [ ] Max two CTAs per section; one primary per page
- [ ] Body text left-aligned; centring reserved for hero

**Craft**
- [ ] Every interactive element has a defined hover/active/focus state
- [ ] Prices and numbers use tabular numerals and align
- [ ] No orphaned single words at the end of a heading
- [ ] Czech typography: `„…"`, non-breaking space after `v s k z o u i a`
- [ ] Mascot appears more than once, never on a blob, never shadowed
- [ ] Touch targets ≥ 44px

**Content**
- [ ] Zero invented ratings, counts or testimonials
- [ ] No buzzwords from §3.4
- [ ] Every CTA names its outcome
- [ ] Sample prices are visibly marked as samples
- [ ] Missing facts bracketed `[E-MAIL]`, not fabricated

---

## 8. Workflow

1. **Analyse** — the surface, its job, and what is specifically wrong, with `file:line`.
2. **Check parity** — what do Android and iOS do here? Reuse the shape.
3. **Ground in truth** (§6) — establish what the page may claim. Content precedes layout.
4. **Name the section forms** (§5) and confirm no two neighbours match.
5. **Produce 2 concepts** with the `design` skill. Two good ones beat three adequate ones.
6. **Run §7 in full and fix what fails — before showing anything.**
7. **Owner selects.**
8. **Implement** against `patterns-frontend.md`; new shared tokens land on all platforms together.

Never skip 6.
