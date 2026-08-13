# Rendering a server-redacted field on a client (living decision notes)

> Companion to **ADR-0047**
> (`docs/decisions/adr-0047.md`).
> An accepted ADR is immutable; this page is the *evolving* design notes, trade-off space and current
> shape. Update this when the design evolves; supersede the ADR for a real decision change.
>
> 🟢 **ADR-0047 is `accepted`** (that file, `:3`), ruled by a lead 2026-08-11 **with amendments A1–A4**.
> **Retires when:** its status line stops reading `accepted`. The catalog entry
> (`agents/knowledge/patterns-mobile.md` §*"The redaction narrowing of rule (1)"*) and the deviation
> entry (`agents/knowledge/consistency.md` §*"Rendering a server-redacted field off an entitlement
> flag"*) carry the rule; the deviation entry's roster is **closed** by T-0590.
>
> ### The two amendments that change what a lane does — read these before the ADR body
>
> **A1 — "named" was never the obligation; WHOLE is.** The gate is not compliant because it has a name.
> It is compliant when **every conjunct lives on the presentation model and the view's expression is a
> single reference with no `&&`**. Two forms satisfy "named" and leave the defect live: a `val` inside
> the composable, and a *partial* gate the view conjoins `&& isMine` onto. The Android lane shipped the
> second by accident and **the mutation reinstating the entitlement flag passed green**; only moving the
> whole gate onto the model made it red. **The acceptance test for this rule is a mutation, not a
> reading.**
>
> **A2 — the ADR's original premise about the redaction's shape was false.** It said the server blanks
> to `string.Empty` and `[]` and never `null`. `OrderPiiRedaction.cs:40-53` nulls `AccessInstructions`
> and every free-text field; `:30-31` nulls the coordinates; only the string scalars go to `""` and only
> the two collections go to `[]`. **The redaction is mixed, so the roster spans both forms and no
> single-form arrival test covers it** — which makes `isNullOrBlank`/`isEmpty` mandatory for a stronger
> reason than the one originally given. Three shipped doc comments still assert the false premise
> (`OrderDetail.swift:133`, `OrderDisclosurePresentation.kt:21`,
> `OrderDetailRedactionGateTests.swift:17`) and are ticketed for correction; the *code* in all three is
> correct.

## Scope

Any field the **server** blanks or coarsens per caller class, on any client. Today that is one seam —
`OrderPiiRedaction` on the order list and order detail — but the rule is written about the *shape*,
because the next such seam will not be an order.

**Out of scope:** authorization itself (S1 server-truth, `security-rules.md`), and anything about what
the server chooses to redact. This page is only about how a client renders what arrived.

## The shape of the problem

Two booleans travel on one DTO and they are not the same boolean.

```
GetOrderDetails.Handle
        │
        ├── isEntitledToCustomerData = CanAccessOrderAsync(order)        (:58)
        │        └──▶ redact / do not redact                            (:137-139)
        │
        └── isAssignedToCurrentUser  = AssignedEmployees.Any(id == me)   (:81-82)
                 └──▶ ships on the DTO, read by the clients
```

They diverge for exactly one caller: the **employee who books a cleaning for their own home**. They
arrive at the handler as the order's *customer*, so nothing is redacted — and the flag is false.

That is not an edge case somebody imagined. It is the reason `b2a8cf62` chose `CanAccessOrderAsync`
over "is assigned" for the server-side predicate in the first place. A client that then gates the
render on the flag re-introduces the rejected predicate on the other side of the wire.

## Current shape

| Question | Answer | Reads |
|---|---|---|
| What location may this caller see? | `OrderLocation` — `precise` / `approximate` / `none` | the **arrival** of `address`, then of `customerAddressApproximate` |
| May this caller call/SMS the customer? | *(deviation)* `isAssignedToCurrentUser && phone != null` | should be: `phone` arrived |
| Show access instructions? | *(deviation)* `isMine && populated && (OnTheWay ∨ InProgress)` | should be: populated ∧ the status term |
| Show notes & issues? | *(deviation)* `isMine` | should be: the lists are non-empty |
| Show work tools / fetch photos? | `isAssignedToCurrentUser && live status` | **correct — an action/request gate, stays** |
| Which primary action? | `status × isAssignedToCurrentUser` | **correct — an action gate, stays** |

`OrderLocation` is the shipped reference on both platforms:
`src/cleansia_android/partner-app/.../features/orders/OrderLocationPresentation.kt:17-32` and
`src/cleansia_ios/CleansiaPartner/Sources/Features/Orders/OrderLocationPresentation.swift:13-45`.

## Trade-off space

**Axis 1 — what discriminates the disclosure level?**

| Option | Disposition |
|---|---|
| The **arrival of the precise field** | ✅ **Chosen.** It is the server's own answer, transported as data rather than re-derived. |
| An entitlement flag on the DTO | Rejected — it is a *different* server answer to a *different* question (above). |
| A dedicated `canSeeX` flag per redacted group | Rejected, and the closest call: it works, but it is a wire field per group whose content is derivable from the group itself, and it re-creates the drift the moment the redaction list moves and the flag's derivation does not. **Revisit only if a field gains a third disclosure level with no observable difference on the wire.** |
| Client-side policy (re-implement the rule) | Rejected on S1 — a second authorization implementation. |

**Axis 2 — how far does the rule reach?**

| Option | Disposition |
|---|---|
| Rendered fields only | ✅ **Chosen (ADR-0047 D1).** |
| Rendered fields **and** action gates | Rejected — it deletes gates that must stay, and its canonicalization ticket would have shipped that deletion. |
| Rendered fields **and** request gates | Rejected — a request gate fails closed; the worst case is a call not made. |

## Invariants (what a reviewer enforces)

1. **The gate on a redacted field's render is that field's own arrival**, never an entitlement flag.
2. **Where a coarse substitute exists, the pair is one sealed value** and no surface reads either half
   directly.
3. **The gate is a named property on the presentation model**, not an inline `if` in a view body —
   otherwise it cannot be pinned without a UI harness, which is how this survived on two platforms.
4. **Blank counts as absent.** The server redacts to `string.Empty`/`[]`, not `null`.
5. **A lifecycle conjunct survives; only the entitlement conjunct is withdrawn.**
6. **The pin drives the divergent shape** — field populated ∧ flag false. A test with the flag true
   proves nothing; a test with the field blank proves the *server's* behaviour.

## Known gaps (accepted, named)

| # | Gap | Bound |
|---|---|---|
| 1 | The enforcer is per-surface, so a **fourth** redacted field rendered off the flag tomorrow is caught by nothing until its own test is added | inherent — the property is relational (which fields does the server blank?) and needs the C# redaction list, which no single-stack linter can read. The membership test in `consistency.md` is what a reviewer applies. |
| 2 | The **customer** apps have their own redaction surfaces and were **not** swept | ADR-0047's roster is the partner surfaces only. Stated so the roster is not read as platform-wide. |
| 3 | Whether the entitled non-assignee should also see **photos** on the partner app is unanswered | a product question about a fetch gate, not a rendering rule; the server already answers it (photos serve only the strict gate). |

## Open questions / future evolution

- **If a redacted field ever gains a third disclosure level** — precise / coarse / *category* — axis 1's
  rejected `canSeeX` option comes back into play, because arrival can only discriminate two states per
  field. The sealed value already has the room; the wire does not.
- **If the server's redaction predicate widens** (a new caller class admitted to customer data), every
  client following this rule needs **no change at all**. That is the whole point, and it is the thing
  to check the next time the predicate moves: a client that needed a change was re-deriving.
