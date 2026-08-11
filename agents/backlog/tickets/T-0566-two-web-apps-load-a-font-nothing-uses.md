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

## Review

**AC1 — leftover, not an unwired design. Proceeding was correct.** Three independent lines of
evidence:

1. Kanit has never appeared in a `font-family` declaration in *any* Angular stylesheet, in any commit.
   `git log -S Kanit --all --name-only` lists no `.scss`/`.css` under `src/Cleansia.App` — only the
   three `index.html` files.
2. The only `font-family` declarations naming Kanit anywhere in the repo are the six **email
   templates** (`email-templates/*.html:17`), where it is a *fallback*: `font-family: 'Nunito',
   'Kanit', sans-serif`. Those templates carry the byte-identical 18-weight `<link>` the apps had. The
   app request is a copy-paste of the email-template `<head>`: the loader came across, the declaration
   did not.
3. The same request was already deleted from the **customer** app in `61f7f58a` (2026-04-04) with no
   replacement stylesheet, and nothing regressed in the four months since. Admin and partner were
   simply missed by that sweep.

So nothing is missing downstream of the link — there is no design waiting on a stylesheet. Kanit's
only role in this codebase is as an email-template fallback, which is untouched.

**Catalog-edit routing:** one entry added to `patterns-frontend.md` ("Request only the web font
families you actually name"), routed **inline**.
- *Test 1 (code sweep)* — does not fire, zero baseline by construction. Sweep: all three apps
  compiled and diffed request-set against reference-set; unreferenced requests are `[]` for admin,
  partner and customer after this ticket, which is the new assertion passing in all three projects
  (24/19/28 tests green).
- *Test 2 (narrowing)* — floor claimed. Searched `agents/knowledge/*.md` for `index.html`,
  `google fonts`, `googleapis`, `web font`, `font-family`, `preconnect`, `typeface`. Only hits:
  `patterns-frontend.md:147` (`index.html` as the `app-root` shell), `patterns-backend.md:1447`
  (`libfontconfig1`, a container package), `patterns-mobile.md:635` (iOS `CleansiaTypography`). None
  reaches web font *requests* at any level of generality — first statement, not a narrowing.
- *Test 3 (unbuilt stack)* — does not fire; all three web apps were built and their suites run here.
- Inline is not free: the gate ships with the entry (`apps/*/src/app/theme/font-stack.spec.ts`,
  **T1-CI** — `frontend-ci.yml:85` `Unit tests (affected)` is not `continue-on-error`).

**Not fixed here, reported instead** (both are the *inverse* direction the ticket forbids guarding, and
both are pre-existing): the customer app declares Poppins `800` in 8 rules and Nunito `500` in 53 rules
while requesting neither weight, so those render at the nearest requested weight. Admin and partner
declare Poppins in their compiled CSS but request it in no app — known and accepted in `55ad850e`.
