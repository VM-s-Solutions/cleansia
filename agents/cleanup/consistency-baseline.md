# `check-consistency` — the stated baseline

**CL-022 / CL-023 / CL-024.** P4's ticket said *"convention debt to a stated baseline"*. Working it
site by site, most of the debt turned out not to be debt: **20 of the 66 violations were the checker
being wrong**, and of the 46 that survive, none has a user-visible cost and several would make the
code worse to "fix".

So this file is the deliverable. The number is not zero, and each remaining entry says why.

```
66  →  46      and the 46 are declared below, not merely outstanding
```

## What was actually wrong — the checker (CL-024)

Twenty phantom violations, in four classes. Each is now guarded by its own case in
`check-consistency.test.mjs` (26 tests, up from 19), including a matching **"STILL flags"** case so a
narrowing cannot quietly become blindness.

| Rule | Phantoms | What the rule got wrong |
|---|---|---|
| `conv` (`: any`) | **11** | Fired on `ControlValueAccessor` members. Angular *declares* `writeValue(obj: any)`, `registerOnChange(fn: any)`, `registerOnTouched(fn: any)` — a narrower type does not implement the interface. Same for `TrackByFunction<T>`, whose return type Angular declares as `any`. The rule was asking for code that will not compile. |
| `C3` | **4** | Two shapes of correct teardown were invisible: a `takeUntil` sitting further up the pipe than the 25-line lookahead (a real admin pipe measured **33**), and teardown held on the stream's *definition* or inside the helper the call site delegates to. |
| `B10` | **2** | Matched `.Close/.Escalate/.Resolve(` on any receiver, so `TimeZoneResolution.Resolve(...)` read as a Dispute state-write. Now requires the file to name the type at all. |
| `B1` | **2** | A flat 4-line lookahead bled into the *next* record, so an HTTP body DTO declared above the real `Command` read as a mis-named command. |
| `conv` (hardcoded `Text`) | **1** | Matched `Text(` inside `ClipData.newPlainText("referral_code", …)` — a clipboard label, not a rendered string. |

## What was actually fixed in the code

**`GetMyServingCleaners` was declared `ICommand<IReadOnlyList<Response>>` while being a pure read.**
`UnitOfWorkPipelineBehavior.IsNotCommand` decides whether to commit by testing whether the runtime type
name ends in `Command` — and this one is named `Query`, so the pipeline never committed it. Harmless
today because the handler writes nothing, but a landmine: add one write later and it is silently
dropped, with no test able to see it. Converted to `IQuery`/`IQueryHandler`, which is what it is. It
also violated *"Commands never return collections"*.

---

## The declared 46 — now **15**, and the drop is mostly the checker

`B3` (21) was the rule being wrong; `D2` (8) was a stated risk that did not exist; `conv` (2) went with
`CL-024`. What remains is `E1` ×9, `B1` ×5 and `E5` ×1, all declared below with reasons and none with a
user-visible cost.

### `B3` ×21 — **RESOLVED 2026-08-14: the rule was wrong, not the code**

The rule was narrowed and the sites stand. Checking what each base actually does split them three ways:

- **`BaseAuthValidator` ×4, `BaseUserValidator` ×1** — declare **no rules in a constructor**, only
  `protected void AddEmailRules(...)` helpers the derived class calls explicitly. The rules land exactly
  as if written inline. The flag was about the `: Base…` token and nothing observable.
- **`LoginValidator` ×5** — its rule **order** is the point. Its own comment: *"Cascade.Stop so a locked
  account never evaluates the password."* Composing it away removes a deliberate gate.
- **`UserEmailValidator` ×11** — its constructor declares a rule that re-checks the caller against the
  database on every request, and **that is load-bearing.** The three web hosts install no revocation
  directory — `UserRevocationWiringPinTests` pins that they must not — a Partner access token lives
  **1440 minutes**, and GDPR erasure rewrites `User.Email` to `deleted_{id}@anonymized.local`. So this
  lookup is the only thing stopping an erased or unconfirmed principal from acting on a token that is
  still signature-valid for up to a day. The owner confirmed that intent on 2026-08-14.

> **What P4 got wrong, recorded because the shape recurs.** This entry originally called the rule
> *"deliberate reuse, not sloppiness"* and left it at that — right conclusion, no evidence. It then
> raised a *hazard*: FluentValidation runs the base's `RuleFor` as a separate chain, so it executes even
> when the derived validator's own rules have failed. That is true, and it is the **correct** design
> here: a security precondition that only ran when the payload happened to be valid would be a weaker
> gate. It costs one indexed lookup on an already-invalid request, which is the right trade.

`check-consistency.mjs` now exempts the four bases by name, with a paired *"STILL flags anything else"*
self-test so the narrowing cannot quietly become a deletion.



Every one inherits `LoginValidator`, `BaseAuthValidator`, `UserEmailValidator` or `BaseUserValidator`.
That is deliberate reuse, not sloppiness. Satisfying the rule means either duplicating the base rules
into 21 validators or inventing a composition abstraction — in **auth validation**, for zero
user-visible gain.

> **There is a real hazard behind this rule and it should be your call, not mine.** FluentValidation
> runs an inherited base class's rules as a **separate chain**, so a derived validator's
> `Cascade.Stop` does not span the inheritance boundary. That is the same mechanism `CLAUDE.md`
> already warns about for `TakeOrder` — *"a second chain runs regardless of this one's verdict"*.
> Nothing observed has gone wrong, and I found no defect. But if the base validators carry rules whose
> order matters, that is where it would hide.

### `E1` ×9 — Android `UiState` as a flag-bag data class — **not doing**

Nine ViewModels across both apps. Converting to sealed interfaces means rewriting each ViewModel and
every `when` that reads it. Real churn, no user impact, no observed defect.

### `D2` ×8 — **DONE 2026-08-14. The stated risk did not exist.**

The baseline claimed *"two of the five affected forms call `reset()`, so this is a behaviour change to
shipped admin forms."* Checking each site:

- **2 of the 8 group calls are no-ops** — every control inside them already declares `nonNullable: true`
  individually, so converting the group changes nothing at all.
- **Of the remaining 6, five forms never call `reset()`.**
- **The one that does** resets a `recipientEmail` control: today to `null`, now to `''`. Both render as
  an empty box and both fail `Validators.required`. Neither the user nor the validator can tell them
  apart.

So there was no behaviour change to protect. All eight converted; the admin app builds and its 34 tests
pass. **The lesson is the one this track keeps relearning: "it changes behaviour" is a claim about the
tree, and it needs checking like any other.**

### `B1` ×5 — commands returning a raw scalar — **not doing**

`Logout`, `Register`, `RegisterEmployee`, `ResendConfirmationEmail`, `HandlePaymentNotification` return
`bool`/`string` instead of a `Response` record. Wrapping them **changes the API contract**, which
forces an NSwag regeneration and a coordinated change across three web clients and two mobile apps —
for no behaviour difference at all.

### `conv` ×2 — `: any` — **not doing**

`cleansia-select.models.ts` (`value: any` on a select option) and `error.codes.ts`
(`(translate, value?: any) => string`). Both are deliberate generic escape hatches in the design
system; `unknown` would push narrowing onto every consumer.

### `E5` ×1 — repository returning a nullable body — **not doing**

Already flagged by the rule itself as a *tracked migration* to `ApiResult<T>`.

---

## How to move this number

Each block above is a decision, not an oversight. If you want one of them done, it is a ticket with a
stated cost — `B3` and `D2` are the two where I would want your ruling before touching anything,
because both change behaviour in code that currently works.
