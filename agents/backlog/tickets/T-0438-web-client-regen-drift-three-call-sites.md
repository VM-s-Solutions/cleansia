---
id: T-0438
title: Unbreak master — three web call sites missing the newly-required regen fields (and wire the wizard's entry instructions through)
status: ready
size: S
owner: frontend
created: 2026-07-30
updated: 2026-07-30
depends_on: []
blocks: [T-0439, T-0440, T-0446]
stories: []
adrs: []
layers: [frontend]
security_touching: false
manual_steps: []
sprint: 14
---

## Context

**`master` frontend CI is RED.** Failing run **30533368357**, workflow `frontend-ci`, jobs `build`
and `e2e-smoke`. The owner regenerated all NSwag clients after commit `bbcf5b24`
(`feat(order): add accessInstructions and removePhoto fields to order and user commands`). NSwag
emits non-optional interface members for these two fields, so every existing call site that builds
the command object with an object literal now fails `tsc`:

- `libs/core/customer-services/src/lib/client/customer-client.ts:7379` —
  `ICreateOrderCommand.accessInstructions: string | undefined` (**required key**, not `?:`)
- `libs/core/customer-services/src/lib/client/customer-client.ts:12879` and
  `libs/core/partner-services/src/lib/client/partner-client.ts:12860` —
  `IUpdateCurrentUserCommand.removePhoto: boolean` (**required**, non-nullable)

The three call sites:

| # | File | Missing |
|---|---|---|
| 1 | `libs/cleansia-customer-features/order-wizard/src/lib/order-wizard/order-wizard.facade.ts:551` (`new CreateOrderCommand({…})`) | `accessInstructions` |
| 2 | `libs/cleansia-customer-features/profile/src/lib/profile/profile.component.ts:224` (`new UpdateCurrentUserCommand({…})`) | `removePhoto` |
| 3 | `libs/data-access/partner-stores/src/lib/user/user.effects.ts:96` (`new UpdateCurrentUserCommand({…})`) | `removePhoto` |

**This ticket also closes a live data-loss bug at call site #1.** The wizard has *always* collected
an entry-instructions value and rendered it back to the user on the summary step, then silently
dropped it at submit:

- collected: `order-wizard.component.html:491-492` → `facade.updateFormData({ entryInstructions: $event })`
- held: `order-wizard.models.ts:48` (`entryInstructions: string`), seeded `''` at `:75`
- displayed back: `components/wizard-summary-step.component.ts:240` — `if (d.entryInstructions) rows.push(...)`
- **never sent**: `order-wizard.facade.ts:551` `new CreateOrderCommand({…})` has no `accessInstructions` key

So the correct repair for call site #1 is **not** `accessInstructions: undefined` — it is to send
the value the user already typed. The backend has accepted it since `bbcf5b24`
(`Features/Orders/CreateOrder.cs:224`, persisted via `OrderFactory.cs:121`) and both the partner and
admin web order-detail screens already render it
(`cleansia-partner-features/orders/.../order-details.component.html:245`,
`cleansia-admin-features/order-management/.../order-detail.component.html:357`) — today they render
an always-empty field for every web-placed order.

Call sites #2 and #3 get `removePhoto: false` only. Actual removal **UI** is out of scope here — it
is T-0446 (avatar feature).

**Second occurrence of this exact failure shape.** The first was `specialInstructions` (fixed in
`ccca1496`, PR #166, "fix(web): unbreak the customer build, and wire both regenerated clients").
`quality-gates.md` §"After an NSwag regen, build **all three** apps before pushing" already states
the rule; it is unenforced, which is why it recurred. The **guard** is a separate ticket (T-0439) so
this one can land immediately — do not build the guard here.

## Acceptance criteria

- [ ] **AC1** — Given the regenerated clients on `master`, When `npm run build:cleansia-customer`,
      `build:cleansia-partner` and `build:cleansia-admin` are each run at production configuration,
      Then all three exit 0. Evidence: the three commands + exit codes recorded in `## Review`.
- [ ] **AC2** — Given a customer who typed text into the wizard's entry-instructions field, When the
      order is submitted, Then the `CreateOrderCommand` carries that text in `accessInstructions`,
      trimmed, and `undefined` when the field is blank/whitespace-only. Evidence: a facade unit test
      asserting both arms.
- [ ] **AC3** — Given the entry-instructions field, When the user types, Then the input is capped at
      **2000** characters, matching the backend validator
      (`Features/Orders/CreateOrder.cs:136-138`, `.MaximumLength(2000)` →
      `BusinessErrorMessage.MaxLength`). Evidence: the `maxlength` binding at file:line + the
      existing `specialInstructions` cap it mirrors.
- [ ] **AC4** — Given the two `UpdateCurrentUserCommand` call sites, When a profile is saved with no
      photo action, Then `removePhoto: false` is sent and the existing avatar is **not** deleted.
      Evidence: `UpdateCurrentUser.cs:135` (`if (!hasNewPhoto && !command.RemovePhoto) return;`)
      cited in review + the two call sites at file:line.
- [ ] **AC5** — Given the change, When `nx affected -t test` runs, Then it is green, and
      `node agents/tools/check-consistency.mjs --paths=<changed dirs>` reports no new violation.

## Out of scope

- Any regen-drift **guard** — T-0439.
- Any avatar upload/removal **UI** — T-0446 (this ticket only sends `removePhoto: false`).
- iOS / Android entry-instructions capture — T-0440 / T-0441.
- Touching any generated `*-client.ts` file (never hand-edit; owner-only regen).

## Implementation notes

- `entryInstructions` is the wizard's own field name; keep it. Map it at the command boundary only
  (`accessInstructions: <trimmed> || undefined`), mirroring how the facade already treats
  `promoCodeToSend`. Do not rename the model field — the summary step and template both bind it.
- Mirror the existing `specialInstructions` handling in the same facade for trim/blank semantics so
  the two free-text fields behave identically.
- If the entry-instructions textarea has no `maxlength`, add it; if `specialInstructions` also lacks
  one, add it there too (same file, same commit — it is the same defect class).
- `removePhoto: false` is a literal, not a form value, at both call sites in this ticket.
- No new i18n keys expected. The display key `pages.order_detail.access_instructions` is
  **pre-seeded** on web — verify before adding anything.

**No-decision note (skips the deliberation panel):** no new behavior or architectural choice. The
field was already collected, already displayed back to the user, and already accepted by the
backend; this restores the wiring and repairs a red build. Sizing/AC/deps/layers set → DoR met.

## Status log
- 2026-07-30 — draft (created by pm; owner batch item 1, URGENT)
- 2026-07-30 — ready (no deps; DoR met; no-decision note recorded)
- 2026-07-30 — **NOT dispatched.** The PM instance running this batch had no `Agent`/`Task` tool
  available, so no `frontend` developer and no paired `reviewer` could be spawned. The ticket stays
  `ready` and awaits dispatch by the orchestrator. The RED build below is the PM's own ground-truth
  run — evidence of the defect, **not** evidence of a fix.

## Pre-work evidence (PM ground-truth run, 2026-07-30, on `bbcf5b24`)

Production builds, `--skip-nx-cache`, from `src/Cleansia.App`:

| Command | Exit |
|---|---|
| `npx nx build cleansia.app --configuration=production` | **1** (4 errors) |
| `npx nx build cleansia-partner.app --configuration=production` | **1** |
| `npx nx build cleansia-admin.app --configuration=production` | **1** |

The **admin** app was not in the brief's blast radius but fails identically, via the shared
`libs/data-access/partner-stores/src/lib/user/user.effects.ts:96`. Representative error:

```
✘ [ERROR] TS2345: Argument of type '{ … specialInstructions: string | undefined; }' is not
assignable to parameter of type 'ICreateOrderCommand'.
  Property 'accessInstructions' is missing … but required in type 'ICreateOrderCommand'.
    libs/cleansia-customer-features/order-wizard/src/lib/order-wizard/order-wizard.facade.ts:551:47
    'accessInstructions' is declared here.
    libs/core/customer-services/src/lib/client/customer-client.ts:7379:4
```

These three commands are the AC1 gate; re-run them **after** the fix, un-cached, and record the exit
codes. A cached/`nx cloud`-replayed green is not evidence.

## Review
<!-- reviewer writes verdict here; PM reconciles before advancing state -->
