# Design Reconciliation — Figma file vs. Stitch brief

> **Source of truth:** Figma file `GhrskekOsdrySUnmmc5RGL` (the design file the user linked).
> The `stitch_cleansia_customer_app/` folder is ignored per user instruction.
> Dark-mode palette is pulled from the existing customer web app `src/Cleansia.App/apps/cleansia.app/src/styles.scss` since the Figma file contains light-mode only.

---

## Screen inventory

### In Figma (15 screens, all Light)

| Node | Screen | Size |
|---|---|---|
| `0:535` | Splash Screen (Light) | 390×884 |
| `0:499` | Welcome Carousel 1 (Light) | 390×884 |
| `0:444` | Welcome Carousel 2 (Light) | 390×884 |
| `0:659` | Welcome Carousel 3 (Light) | 390×884 |
| `0:93`  | Sign In (Light) | 390×1181 |
| `0:915` | Home Tab (Light) | 390×1892 |
| `0:699` | Book Step 1: Services (Light) | 390×1609 |
| `0:805` | Book Step 2: Property (Light) | 390×1075 |
| `0:1281`| Book Step 3: Schedule (Light) | 390×2210 |
| `0:3`   | Book Step 4: Address (Light) | 390×1201 |
| `0:1136`| Book Step 5: Extras (Light) | 390×1727 |
| `0:559` | Payment Sheet (Light) | 390×884 |
| `0:1064`| Booking Success (Light) | 390×969 |
| `0:152` | Orders List (Light) | 390×884 |
| `0:251` | Order Detail (Light) | 390×2247 |

### In brief but NOT in Figma (missing — 15 screens)

- Sign up
- Email verification (6-digit code)
- Forgot password
- Reset password
- Track order (public / magic-link)
- Rate & review
- Disputes list
- Dispute thread
- Profile home
- Edit profile
- Addresses list
- Add/edit address
- Payment methods
- Preferences (language, theme, notifications)
- Legal pages (Terms / Privacy / GDPR)
- Language switcher bottom sheet
- Loader / skeleton states
- Empty states
- Error state
- 404
- Push notification templates

### In Figma but NOT in brief (novel — 0 screens)

None — Figma is a strict subset of the briefed inventory.

### Dark mode

- **Figma file has zero Dark variants.** Brief mandated both.
- Dark palette will be derived from the web app's slate palette (see "Dark mode tokens" below).

---

## Design tokens

### Figma has NO defined variables

`get_variable_defs` returned `{}`. All colors and type are baked as raw values per layer. Tokens below are extracted from the Sign In screen (`0:93`) via `get_design_context`.

### Color palette — what Figma actually uses

| Role | Brief spec | Figma actual | Match? |
|---|---|---|---|
| Primary base | `#0284c7` (sky-600) | `#006194` (deeper teal-blue) | ✗ |
| Apple/dark chip | n/a | `#191c1d` | new |
| Text primary | `#111827` | `#3f4850` | ✗ |
| Text secondary / placeholder | `#6b7280` | `#707881` | ✗ (close) |
| Input surface | `#ffffff` | `#f3f4f5` | ✗ (filled input style, not outlined) |
| Divider | `#e5e7eb` | `#e1e3e4` | ✗ (close) |
| Card surface | `#ffffff` | `#ffffff` | ✓ |
| Page background | `#f9fafb` | (inferred) white/page | tbd — needs check on other screens |

The brief's `#0284c7` sky blue was **replaced by a darker teal-blue `#006194`** throughout. This is the whole-file primary, not a one-off.

### Semantic colors (brief)

Not visible on the Sign In screen. Will need to extract from Orders List / Order Detail (`0:152`, `0:251`) for status badges in Phase 4.

### Typography — MAJOR DEVIATION

| Role | Brief spec | Figma actual |
|---|---|---|
| Headings | Poppins (500/600/700) | **Plus Jakarta Sans** |
| Body | Nunito (400/600/700) | **Liberation Serif** (!) |

- Heading H1 in Figma: 36px / line-height 40px / tracking -0.9px / color `#006194`
- Body: 16px / line-height 24px / `#3f4850`
- Labels: 14px bold / `#3f4850`
- Small link text: 12px bold
- Uppercase divider label: 12px bold / tracking 1.2px

**Plus Jakarta Sans** is a solid replacement and trivial to use on Android (Google Fonts). **Liberation Serif** is unusual — it's a metric-compatible Times New Roman clone, NOT a typical mobile body font. This is almost certainly a Stitch-AI artifact (Stitch may have substituted Nunito with the closest locally-available font when Nunito wasn't registered). **Recommend overriding to Nunito (brief) or Inter (neutral default) for body** unless the user insists on keeping Liberation Serif.

### Shape

| Role | Brief | Figma actual |
|---|---|---|
| Card radius | 16/24 | **32** (Login Card) |
| Input radius | 12 | 0 (filled chip, no radius on Sign In inputs) |
| Button | pill 9999 | ✓ pill 9999 |
| Hero card radius | 24 | 48 (Clean Interior image panel) |

Corner-radius scale expanded. Recommend adopting **16 / 24 / 32 / 48 / pill** as the mobile scale.

### Depth

- Card shadow: `0 20px 40px rgba(0,97,148,0.08)` — tinted with primary, not neutral
- Button shadow: `0 10px 15px -3px rgba(0,0,0,0.1), 0 4px 6px -4px rgba(0,0,0,0.1)`
- Image panel: `0 25px 50px -12px rgba(0,0,0,0.25)`
- Glass morphism: primary button uses `backdrop-blur(10px)` + `rgba(0,97,148,0.85)` — confirms the brief's "glass morphism is a signature style" guidance

---

## Dark mode tokens (derived from web app, not Figma)

Source: `src/Cleansia.App/apps/cleansia.app/src/styles.scss`

| Role | Value |
|---|---|
| Page bg | `#0f172a` (slate-900) |
| Card surface | `#1e293b` (slate-800) |
| Elevated surface | `#283548` |
| Border | `#334155` (slate-700) |
| Text primary | `#e2e8f0` (slate-200) |
| Text secondary | `#94a3b8` (slate-400) |
| Accent / primary (dark) | `#38bdf8` (sky-400) — **diverges from Figma's `#006194`** |

**Open question:** In dark mode, do we keep the Figma primary `#006194` (which is dark and low-contrast on a slate-900 bg) or swap to the web's `#38bdf8` sky-400 for dark mode readability? Recommend **swap to `#38bdf8` for dark** — this is what the web does and meets WCAG AA on slate-900. Needs user sign-off.

---

## Components observed on Sign In

- **Hero heading + subheading section** (left-aligned, 36px + 16px)
- **Card** (white, 32px radius, 32px padding, tinted shadow)
- **Filled text input** (no border, `#f3f4f5` bg, 16px body, 19px vertical padding)
- **Label + link row** (label left, link right — "Password" / "Forgot password?")
- **Primary button** (pill, glass morphism with primary tint, 57px tall, icon on right)
- **Divider with centered label** ("OR CONTINUE WITH")
- **Social login grid** (2 columns — Google filled-light, Apple filled-dark `#191c1d`)
- **Footer link** ("New to Cleansia? Create Account")
- **Ambient decorative image panel** (opacity 0.2, saturation mixed, rotated 2deg — decorative only)

Components from brief inventory **not yet verified** in the Figma file (require crawls of the other 14 screens): bottom tab bar, order card, step indicator, status badge, time slot picker, code input, chip/tag, rating stars, order status timeline, price summary card, bottom sheet, snackbar. Those will surface as I query each screen in Phase 6.

---

## Open questions for the user

1. **Primary color shift:** Figma uses `#006194`, brief specified `#0284c7`. Keep the Figma value as source of truth? (Recommend: **yes — Figma is now canonical**.)
2. **Body font:** Figma says "Liberation Serif" — almost certainly a Stitch substitution artifact. Swap to **Nunito** (brief) or **Inter** (neutral) for the mobile app? (Recommend: **Nunito** for consistency with the brief and the web app.)
3. **Dark mode primary:** Use web's `#38bdf8` (sky-400) in dark mode, since `#006194` lacks contrast? (Recommend: **yes**.)
4. **Missing screens:** 15 screens from the brief aren't in Figma (sign up, verify, forgot password, profile area, addresses, payment methods, preferences, rate & review, disputes, legal, etc.). Options:
   - (a) I design them in code using the extracted Figma tokens + brief spec
   - (b) You add them to the Figma file first, then I implement
   - (c) Cut them from v1 scope
   Which?
5. **Dark-mode Figma frames:** Confirmed none exist in the file. Should I derive dark variants from tokens (web app palette), or wait for you to add Dark frames in Figma?
6. **Web fonts hosting:** For Android, Plus Jakarta Sans + Nunito will be bundled as app assets. OK with bundling (≈300–600 KB) vs. downloadable fonts (lighter APK, async load)?

---

## Phase 1 status

- [x] Figma file crawled — 15 Light-only screens catalogued
- [x] Tokens extracted from Sign In (no Figma variables defined)
- [x] Dark palette pulled from web app
- [x] Deviations from brief documented
- [ ] **User sign-off needed on the 6 open questions above before Phase 2**
