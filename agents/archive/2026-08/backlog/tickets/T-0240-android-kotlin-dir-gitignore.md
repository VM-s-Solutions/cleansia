---
id: T-0240
title: Android — add .kotlin build-artifact dir to .gitignore
status: done
size: S
owner: pm
created: 2026-06-12
updated: 2026-06-15
depends_on: []
blocks: []
stories: []
adrs: []
layers: [android]
security_touching: false
manual_steps: []
sprint: 6
source: T-0195 reviewer nit (Wave-3 close)
---

## Context
The T-0195 reviewer flagged that the Kotlin 2.x `.kotlin/` build-artifact directory (project-local
Kotlin compiler session/cache output under `src/cleansia_android/`) is not ignored — verified
2026-06-12: no `.kotlin` entry in the root or android `.gitignore`. Risk is accidental commits of
machine-local build state. **No-decision note:** pure mechanical hygiene fix — skips the panel.

## Acceptance criteria
- [ ] **AC1** — `.kotlin/` is ignored for the Android tree (entry in `src/cleansia_android/.gitignore`,
  matching the existing `build/`-style entries); `git status` after a local Android build shows no
  `.kotlin` artifacts.
- [ ] **AC2** — If any `.kotlin` artifacts are already tracked, they are `git rm --cached`-removed in
  the same change (verify: `git ls-files | grep .kotlin` is empty).

## Out of scope
- Any other gitignore hygiene (gradle caches etc. are already covered).

## Status log
- 2026-06-12 — draft (created by pm at Wave-3 close; T-0195 nit)
- 2026-06-14 — **ready** (PM, Wave-6 intake / Batch **6A**). Pure mechanical hygiene, no-decision, skips the
  panel. Sole editor of `src/cleansia_android/.gitignore` — fully disjoint, parallel with all of 6A/6B/6C/6D.
  Plan: `status/sprint-8.md` §3 Batch 6A.
- 2026-06-14 — **review** (android). AC1 done: added `.kotlin/` to `src/cleansia_android/.gitignore`
  in the Gradle/build block (next to `build/`). Verified with `git check-ignore -v` that `.kotlin/`
  is matched at the tree root and in each module (`core`, `partner-app`, `customer-app`). AC2: no
  `.kotlin` artifacts are tracked (`git ls-files | grep .kotlin` empty), so the `git rm --cached`
  step is a confirmed no-op — nothing to untrack. No source comments, no other files touched.

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
