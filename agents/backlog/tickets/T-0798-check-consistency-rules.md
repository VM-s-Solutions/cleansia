---
id: T-0798
title: "`check-consistency.mjs` — the F1–F15 rules that hold the line, filed at `--warn` with today's counts, promoted to failing where the sweeps reach zero"
status: done
size: S
owner: —
created: 2026-09-20
updated: 2026-09-22
depends_on: [T-0785, T-0786, T-0787, T-0788, T-0789, T-0790, T-0793, T-0794, T-0795, T-0796, T-0797]
blocks: [T-0799]
stories: []
adrs: []
layers: [frontend]
security_touching: false
manual_steps: []
sprint: —
---

## Context

Every earlier consistency pass drifted back because nothing checked the rule after the sweep. The
consistency scout wrote fifteen rules (CS §10) whose baseline counts are the defects this batch
removes; `agents/tools/check-consistency.mjs` already has the self-test shape and the `--warn`
baseline mechanism (`agents/process/enforcement.md`). Ground truth at `069b72ae`:
`DEFAULTS.frontend` (`check-consistency.mjs:700`) does not include `src/Cleansia.App/apps`, `walk()`
does not visit `.html` / `.scss` / `.json`, and **no CI workflow runs the checker** — the jest specs
named in T-0785 / T-0786 / T-0789 / T-0792 / T-0795 are what CI executes; this checker is the
Reviewer's on-demand gate.

## Doing

- Widen `DEFAULTS.frontend` to include `src/Cleansia.App/apps`; add `.html` / `.scss` / `.json` to
  `walk()` for the new rules.
- Add F1–F15 as listed in CS §10 with the current counts as the `--warn` baseline: F1 raw `<button>`
  (admin 46 / partner 38); F2 legacy `cleansia-button` bindings (20); F3 `<p-confirmDialog` outside
  the shell (18); F4 `providers: [ConfirmationService]` (18); F5 direct `.confirm(` outside
  `dialog.service.ts` (18 files); F6 inline `style=` (34 / 56 / 27 / 18); F7
  `severity="success|warn|info"` (10 / 0 / 4); F8 `!important` (39); F9 undefined tokens (18 names /
  45 sites); F10 off-scale radius / `.p-button` overrides (≈200 / 3); F11 i18n namespaces + no
  `common.*` (admin 11 / partner 5); F12 orphan stylesheets (7); F13 hand-check i18n sections (30+);
  F14 literal `aria-label` (3); F15 `showSuccess(translate.instant(` (33 files). One self-test case
  per rule in `check-consistency.test.mjs`.
- After T-0785–T-0797 land: every rule whose count reached 0 is promoted from `--warn` to failing
  (enforcement.md §*"when a stack's baseline hits zero"*); the ones that did not (F10 radius sweep,
  F13, the hand-check i18n sections) stay advisory with the count recorded in the rule's comment
  and in this ticket.

## NOT

- Wiring the checker into a CI workflow — that is enforcement.md's own per-stack ratchet decision
  and needs zero baselines first. Rules for the customer app.

## Done looks like

`node agents/tools/check-consistency.mjs frontend` reports the F-rules with counts;
`node agents/tools/check-consistency.test.mjs` green with one case per new rule; the rules at 0
exit 1 on a reintroduced violation (proved by the self-test).

## Acceptance criteria

- [ ] **AC1** — Given the rule definitions landed at `--warn` (any time), When
      `node agents/tools/check-consistency.mjs frontend` runs at `069b72ae`'s counts, Then each F-rule
      reports its baseline and the run exits 0.
- [ ] **AC2** — Given `check-consistency.test.mjs`, When it runs, Then one case per F-rule proves
      the rule finds a planted violation and ignores a clean fixture.
- [ ] **AC3** — Given the sweeps T-0785–T-0797 shipped, When a rule's count is 0, Then it is
      promoted to failing and re-adding one violation makes `check-consistency.mjs frontend` exit 1.
- [ ] **AC4** — Given a rule whose count did not reach 0 (F10, F13), When the checker runs, Then it
      stays `--warn` with the residual count recorded in the rule's comment and on this ticket.

## Implementation notes

The rule definitions can land any time at `--warn`; the promotion step is what depends on the
sweeps. `docs-ci`-style promotion is not done here. **Regen:** none.

## Status log

- 2026-09-20 — filed 2026-09-20 from the UI-polish discovery; branch chore/ui-polish-and-dead-code.
  Phase 4, tools lane; the definitions may land early, the promotion lands after phase 3.
- 2026-09-22 — **done**; shipped as `279e3180e` (the guards that hold the admin and partner line run
  where CI runs them — the jest theme specs — and the static rules join the checker) + the fixes
  `bc731a640` (the partner card-width guard keeps its reach; the checker prints the baseline it is
  ratcheted on) and `fa30d0ba4` (F12 resolves a page's component from the whole web tree under
  `--paths=`; the header stops citing a `consistency.md` §F that did not exist) on
  chore/ui-polish-and-dead-code (PR #260). **The F-numbering that shipped is the tool's, not this
  ticket's filing** — F1 raw form control, F2 PrimeNG widget outside the wrapper, F3 `<p-confirmDialog>`
  outside the shell, F4 component-scoped `ConfirmationService`, F5 direct `.confirm(`, F6 legacy
  `cleansia-button` bindings, F7 `*ngIf` / `*ngFor` / `[(ngModel)]`, F8 a component's own teardown,
  F9 undeclared `--cleansia-*` token, F10 off-scale radius / tinted shadow, F11 locale-bundle parity
  and namespaces, F12 orphan page stylesheet, F13 the page's `h1`, F14 literal `aria-label`, F15
  per-feature error-key map (the filed F6 inline `style=` and F7 `severity=` are held by the jest
  `detail-pages.spec.ts` and `dialogs.spec.ts` instead; the filed F8 `!important` count has no
  guard — plate). Hard gates (`add`): F2, F3, F4, F5,
  F7, F9, F13, F14, F15 and F11's parity half; advisory (`warn`) with the count printed per run:
  F1 (4), F6 (3), F8 (27), F10 (77 radii + 6 shadows), F11's stray namespaces (9), F12 (1) — all
  measured 2026-09-22. `DEFAULTS.frontend` now walks `src/Cleansia.App/apps`; `.html` / `.scss` /
  `.json` are read; the customer trees are skipped. `check-consistency.test.mjs` carries a red case
  per F-rule. **Not done (NOT list):** no workflow runs the checker; the jest specs are what CI
  executes. `consistency.md` §F carries the rows (T-0799). Findings on the plate: the F2 tag list is
  the camelCase spelling only; the F-ids collide with the older sprint-12 `F1` label in
  `consistency.md` E1 / E8 (named in §F's intro); `agents/cleanup/consistency-baseline.md` does not
  list the six advisory baselines; the admin `page-shell.spec.ts` card-width test does not strip
  media-query headers as the partner twin does.
