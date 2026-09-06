---
id: T-0680
title: The generated clients answer a non-array 200 with null, and 20 call sites assume an array
status: todo
size: M
owner: —
created: 2026-09-06
updated: 2026-09-06
depends_on: []
blocks: []
stories: []
adrs: []
layers: [frontend]
security_touching: false
manual_steps: []
sprint: 16
---

## Context

**This is a record of a class, not a sweep.** Owner ruling 2026-09-06: file it with the site list and
fix a site when a lane is already in that file for another reason. Do not schedule a pass over all of
them, and do not build machinery for it.

### The mechanism

Every array-returning method on the three generated clients carries this branch:

```ts
if (Array.isArray(resultData200)) {
    result200 = [] as any;
    for (let item of resultData200) result200!.push(Thing.fromJS(item));
}
else {
    result200 = null as any;      // <-- the declared type says this cannot happen
}
```

So for a method declared `Observable<Thing[]>` the client emits **`null`** for a 200 whose body is
not a JSON array — an empty body, a `{}`, a `null` — and for a **204**, which falls past both
branches to a bare `return ObservableOf(null as any)`.

Nothing catches it. `catchError` never fires because null is not an error. TypeScript never complains
because the declared return type is non-nullable. **36 methods carry the branch** (12 admin, 14
customer, 10 partner).

### What was already fixed

`getPlans`, both readers — see the commit for T-0679's sibling work. That is the only one addressed;
everything below is open.

Two admin templates were also fixed at the same time, and they are worth separating out because they
were a **different and live** defect: `service-form.component.html` and `package-form.component.html`
read `facade.languages()[0].code`, which throws on an **empty** array — and both facades' own
`catchError` sets the signal to `[]` on any failed read. A transient network failure took the form
down. Those are done.

### The 20 remaining unguarded sites

Traced across `libs/` outside `libs/core/`: **41 call sites, 22 unguarded**, of which two are now
fixed. They assign or dereference the raw client value with no `?? []`, no `|| []` and no truthiness
check.

**Dereference immediately — these throw rather than merely storing a lie:**

| # | Site | What it does with the null |
|---|---|---|
| 1 | `cleansia-customer-features/order-wizard/…/order-wizard.facade.ts:325` | `countries.length` and `countries[0].id` in the same callback; **no `catchError` at all** |
| 2 | `cleansia-customer-features/order-wizard/…/order-wizard.facade.ts:339` | `[...extras]` — spreading null throws `not iterable` |
| 3 | `cleansia-customer-features/order-wizard/…/order-wizard.facade.ts:400` | `consents.some(…)` |
| 4 | `cleansia-customer-features/gdpr/…/gdpr.facade.ts:39` | `set()` with **no `catchError`**; `:83` then `.find(…)` |
| 5 | `cleansia-admin-features/service-management/…/service-form.facade.ts:81, :106` | `languages.filter(…)`, `categories.filter(…)` |
| 6 | `cleansia-admin-features/package-management/…/package-form.facade.ts:147` | `languages.filter(…)` |
| 7 | `cleansia-admin-features/country-management/…/service-area-management.facade.ts:57, :115` | `countries.map(…)` |
| 8 | `cleansia-partner-features/profile/…/profile-bank.facade.ts:87` | `countries.map(…)` |
| 9 | `cleansia-partner-features/profile/…/profile.facade.ts:118` | `countries.map(…)` |
| 10 | `cleansia-admin-features/loyalty-promo-codes/…/promo-code-form.facade.ts:92` | `items.filter(…)` |

**Store it and move on — a null in a signal, deref deferred to a reader:**
`company-info.facade.ts:52`, `company-info-form.facade.ts:55`, `country-management.facade.ts:31`,
`currency-management.facade.ts:32`, `language-management.facade.ts:31`, and four NgRx effects piping
the raw value into an action payload (`customer-stores/catalog.effects.ts:16` and `:28`,
`partner-stores/code.effects.ts:17`, `admin-stores/admin-code.effects.ts:17`).

### The 19 that already get it right

`admin-user-form.facade.ts`, `employee-detail.facade.ts`, `employee-payout.facade.ts`,
`employee-management.facade.ts`, `profile.facade.ts:302`, `saved-address.store.ts`,
`recurring-bookings.facade.ts:365`, `email-template-list.facade.ts`, plus explicit `if (rows)` /
`if (!roster)` / `if (cities === null)` guards in `data-protection`, `deletion-requests`,
`document-requirements` (×2), `fiscal-failures-list`, `pay-config-form`, partner `gdpr`,
`order-preferred-cleaner`, `order-preferred-offer`, `order-service-area`.

**The team half-knows.** Some of the unguarded sites carry a comment saying "falls back to empty list
on failure" while the code does not.

### Why no test caught any of it

A hand-written facade stub always returns a plausible array. `package-form.component.spec.ts` seeds
`signal([{ code: 'en', name: 'English' }])`, so its `[0].code` binding never met an empty list;
`service-form` has a facade spec and **no component spec at all**, so its identical binding was never
rendered by anything. Only a catch-all stub — or a real proxy — produces the shape the client actually
admits, which is exactly how this was found.

## Acceptance criteria

- [ ] **AC1** — Given a lane is editing one of the 20 sites for any reason, When it touches the
      client read, Then it coalesces with `?? []` and the site is struck off this list.
- [ ] **AC2** — Given a site is fixed, Then a test asserts the null case, seeded by mocking the
      client to return `null` — not by a plausible array.

## Out of scope

- **A sweep of all 20.** Owner ruling: opportunistic only.
- **A shared `orEmpty()` helper or an RxJS operator.** An import at every call site plus a file plus a
  barrel export, to save four characters over `?? []`.
- **A repo checker.** It would have to trace a generated method's value through an RxJS pipe, a
  subscribe callback and a signal write across files — the repo's first dataflow analysis, to catch a
  class with no confirmed production incident.
- **A wrapper service around the three clients.** A new abstraction paid by every future reader of
  every feature lib, to insert a `??`.
- **An NSwag template override.** The configs set `template: Angular` with no `templateDirectory`, so
  this means vendoring NSwag's whole Angular template set, version-pinned, to change one branch.
- **Hand-editing the generated clients.** Regeneration is owner-run and erases it.
- **Any backend change.** The endpoints are correct: `HandleSuccess<T>`'s first arm matches and
  returns a real list. This is a client-side robustness gap, not a server bug.

## Implementation notes

`?? []` at the point the value enters the facade, not at each reader — the house rule is that
components delegate to facades, and a signal that can hold null is the actual defect.

Where a callback also receives the value (`onLoaded?.(plans)`), coalesce ONCE and pass the same list
to both; its callers index it exactly as the signal's readers do.

## Status log

- 2026-09-06 — filed. Found while stubbing the customer booking e2e smoke, where a catch-all `{}`
  stub produced the null the real client admits.

## Review

<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
