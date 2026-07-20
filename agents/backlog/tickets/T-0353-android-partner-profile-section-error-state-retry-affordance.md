---
id: T-0353
title: "Android partner profile section-form Error state has no retry affordance (renders an empty form)"
status: done
size: S
owner: android
created: 2026-06-30
updated: 2026-07-19
depends_on: [T-0337]
blocks: []
stories: []
adrs: []
layers: [android]
security_touching: false
priority: low
manual_steps: []
sprint: 12
source: HARDENING-1 T-0337 android review — UX gap (NON-blocking enhancement)
---

> **NON-blocking UX enhancement surfaced by the T-0337 android review (HARDENING-1).** Behavior-preserved from
> before T-0337 (the sealed-state migration intentionally kept the screens' consumption shape), so this is an
> enhancement, not a regression.

## The gap
After T-0337, the partner profile section screens (Personal / Address / Identification / Emergency / Bank)
consume the new sealed `*UiState` via `is Loading` + `as? Loaded` — **not** an exhaustive `when` over
`Loading`/`Error`/`Loaded`. As a result, when the load lands in the **`Error`** state, the screen falls through
neither the `Loading` branch nor the `Loaded` branch and renders an **empty editable form with no retry
affordance** — the user sees a blank form, can't tell the load failed, and has no "Retry" action. The
`*UiState.Error(canRetry)` data is already produced by the VMs (T-0337); the screens just don't render it.

## Acceptance criteria
- [ ] **AC1 (Error renders)** — Given a partner profile section whose load fails, When the screen composes,
  Then it shows an explicit error state (message + a retry affordance when `Error.canRetry` is true) — not an
  empty editable form.
- [ ] **AC2 (retry works)** — Tapping retry re-triggers the section load (returns to `Loading` → `Loaded` on
  success, or back to `Error` on failure); no parallel load path is introduced.
- [ ] **AC3 (exhaustive consumption)** — The section screens consume the sealed state exhaustively
  (`Loading`/`Error`/`Loaded`) rather than `is Loading` + `as? Loaded`; the `Loaded`/`Loading` rendering is
  unchanged; strings are `R.string.*` ×5; the app builds and tests stay green.

## Out of scope
- The **save** (`ActionState`) error path — that already surfaces via the snackbar/effect bus (T-0337); this
  ticket is the **load** Error state only.
- Any VM/state-shape change — the `*UiState.Error(canRetry)` already exists (T-0337); this is a screen-layer
  rendering fix only.
- The iOS side and any backend change.

## Implementation notes
- The fix is in the section **screens** (`{Personal,Address,Identification,Emergency,Bank}SectionScreen.kt`),
  not the VMs: replace the `is Loading` + `as? Loaded` consumption with an exhaustive `when` that also renders
  the `Error` branch (mirror the canonical sealed-state screen pattern used elsewhere — an error panel + a
  retry button gated on `canRetry`).
- Reuse the existing shared error/retry composable if one exists in `:core` rather than introducing a new one.
- depends_on T-0337 (the sealed state this renders shipped there).

## Status log
- 2026-06-30 — filed from the HARDENING-1 T-0337 android review. The section screens consume the sealed state
  with `is Loading` + `as? Loaded` (not exhaustive), so the `Error` state renders an empty form with no retry.
  Behavior-preserved from before T-0337, so an enhancement, not a regression. NON-blocking, low priority.
  `proposed`, not dispatched.

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
- 2026-07-19 — **frontmatter reconciled to reality (proposed → done)** — shipped `91fbe1eb` (hardening-cluster-2, merged): SectionScaffold isError/onRetry.
