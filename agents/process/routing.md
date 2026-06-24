# Routing — how the PM decides who works a ticket

The PM reads a ticket's `layers` field and the nature of the change, then invokes the right
specialist(s) — and a reviewer alongside each developer. This table is the decision logic.

## By signal

| Signal in the ticket / change | Route to |
|---|---|
| New/changed user-facing behavior with fuzzy AC | `analyst` (write/sharpen the story first) |
| New pattern, new extension point, cross-cutting decision | `architect` (ADR first) |
| New entity, column, index, migration, query filter, seed | `db` |
| Command/query/handler/validator, DTO, mapper, service, integration (Stripe/SendGrid/Firebase), Function | `backend` |
| Angular app/lib: component, facade, NgRx store, route, PrimeNG UI | `frontend` |
| Kotlin: ViewModel, Compose screen, Hilt module, navigation, string resource | `android` |
| Swift/SwiftUI port of an Android surface | `ios` |
| i18n keys / copy across the 5 locales | the relevant dev adds keys; flag wording questions to owner |
| Any diff in `in_review` | `reviewer` (always) |
| `security_touching: true` | `security` (in addition to reviewer) |
| Hot path, list view, paged query, new dependency, heavy UI | `optimizer` |
| PR/diff ready for behavioral verification | `qa` |
| Shipped behavior changed; docs/changelog stale | `docs` |

## Sequencing rules (the PM applies these)

1. **Contract before consumers.** `architect` (if needed) → `db` (if schema) → `backend` locks the
   API DTO shape. Only then do `frontend` / `android` / `ios` start against that contract.
2. **Reviewer in parallel, always.** For every developer instance the PM spawns, it spawns a
   `reviewer` instance reading the same ticket. The PM reconciles both before moving state.
3. **Fan out independent tickets.** Multiple instances of the same charter run concurrently on
   *different* tickets (e.g. two `backend` instances on two unrelated features). Never two instances
   editing the same files at once — the PM serializes those. This applies especially to the **shared-file
   clusters** (`consistency.md`, `INDEX.md`, the per-app i18n bundles, the `Policy.cs`/`PolicyBuilder.cs`
   pair): they get a **single serialized lane**, and parallel agents must **edit only their own hunks and
   never `git restore` a shared file** (a blanket revert wipes a sibling ticket's work — see
   `quality-gates.md` §"Serialize shared-file lanes …" for the 2026-06-23 incident).
4. **Platforms parallel.** `android` and `ios` run together off the same locked contract.
5. **Gates last.** `security` / `optimizer` / `qa` run after implementation + review converge,
   before merge.
6. **Manual steps block.** If a ticket needs an EF migration or NSwag regen, the PM flags it to the
   owner and **holds** the dependent layer until confirmed.

## What the PM does NOT do

- Does not write code, ADRs, stories, or tests — it delegates.
- Does not approve its own merges — the reviewer (and security/QA where applicable) gates.
- Does not run owner-only steps — it flags them.
- Does not ping the owner for routine progress — it batches into the sprint doc.

## Fan-out budget

The PM scales instance count to the work, not to a fixed headcount. Guidance:
- Audit / sweep work (the first job): fan out **wide** — one analyst/reviewer instance per
  subsystem, in parallel, because the subsystems are independent.
- Feature work: usually 1 instance per layer + its reviewer; add a second instance of a layer only
  when there are independent tickets to run.
- Keep the **reviewer-per-developer** invariant regardless of scale.
