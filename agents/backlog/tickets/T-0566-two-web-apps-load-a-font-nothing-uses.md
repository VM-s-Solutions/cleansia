---
id: T-0566
title: Admin and partner download all 18 weights of a font no stylesheet references, and the customer app requests a Poppins weight nothing uses
status: ready
size: XS
owner: frontend
created: 2026-08-07
updated: 2026-08-07
depends_on: []
blocks: []
stories: []
adrs: []
layers: [frontend]
security_touching: false
manual_steps: []
sprint: 15
---

## Context

Found while fixing T-0472's web half (`55ad850e`). Not part of that ticket — it is a pure waste
finding, and it was reported rather than swept in.

**Verified 2026-08-07, independently of the lane that raised it:**

- `apps/cleansia-admin.app/src/index.html` and `apps/cleansia-partner.app/src/index.html` both request
  **Kanit**, roman + italic, **all 18 weights**.
- `grep -rn "Kanit"` across every `.scss`, `.css` and `.ts` in `src/Cleansia.App`, excluding
  `node_modules` and the two `index.html` files themselves → **0 hits**. No `font-family` declaration
  anywhere names it.
- The customer app requests Poppins `wght@500`, and no Poppins rule uses weight 500. Measured weights
  in the compiled CSS: 600 ×55, 700 ×33, 800 ×8, inherited ×2.

So two apps block first paint on a render-blocking stylesheet request for a family they never use, and
a third downloads one weight it never sets.

## Why it is worth an XS rather than nothing

It is on the **critical path**: a Google Fonts `<link>` in `<head>` is render-blocking, and Angular's
production critical-CSS inliner fetches it at build time too. The cost is paid on every cold visit to
the admin and partner apps, for nothing.

It is also a **correctness smell, not only a performance one**: a font request that no rule references
is either a deleted design that left its loader behind, or a design that was never wired up. Whoever
removes it should say which, because the second case means a stylesheet is missing rather than a link
being surplus.

## Acceptance criteria

- [ ] **AC1** — Establish which case Kanit is: leftover from a removed design, or a design never
      wired. Say so in `## Review`; `git log -S Kanit -- apps/` answers it. **If it is the second
      case, stop and escalate** — the fix is then a missing stylesheet, not a deleted link.
- [ ] **AC2** — Assuming AC1 says "leftover": remove the Kanit request from both apps, and drop
      `wght@500` from the customer app's Poppins request.
- [ ] **AC3** — A guard that fails when a requested family is referenced by no declaration. The
      T-0472 guards (`apps/*/src/app/theme/font-stack.spec.ts`) already compile the build stylesheets
      and parse each app's `index.html` font request — this is the same two inputs, one more
      assertion, in the file that already has both.
- [ ] **AC4** — Mutation-proved: re-adding a request for an unreferenced family must turn AC3's
      assertion red, and it must **not** fire on a family that IS referenced. Both directions, applied
      one at a time, restored byte-exact.
- [ ] **AC5** — All three production builds green, and the bundle/request delta reported.

## Notes

AC3 is what makes this worth doing. Deleting two lines takes a minute; the guard is what stops the
next dead loader, and it costs one assertion because T-0472 already put both inputs in front of it.

Do **not** widen the guard to "every declared family must be requested" — that is the opposite
direction and it is legitimately violated by system stacks (`sans-serif`, `monospace`, `Menlo`,
`primeicons`, `Consolas`). The signal is **requested but never referenced**, not the reverse.
