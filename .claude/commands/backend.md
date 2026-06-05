# /backend — Direct backend work (single-shot escape hatch)

For a small, well-scoped backend change where full team coordination is overkill. For anything
cross-layer or non-trivial, use `/team` instead (the PM coordinates + wires a parallel reviewer).

## Usage
```
/backend <describe the backend change>
```

## What it does
Act as the **Backend Dev**. Load and follow the charter at `.claude/agents/backend.md`, reading its
required references first:
- `agents/knowledge/patterns-backend.md` (CQRS one-file feature, controllers, DTOs, repos, errors,
  fiscal modes)
- `agents/knowledge/security-rules.md` (S1–S10 — non-negotiable)
- `agents/knowledge/conventions.md`
- `docs/architecture/backend.md`

Then make the change to the project's standards. Flag `manual_step: ef-migration` /
`manual_step: nswag-regen` if the schema or an API contract changed — you do **not** run them.

## Rules
- One-file CQRS (nested `Command`/`Query` + `Validator` + `Handler` + `Response`); command record
  types end in `Command`; happy-path handlers (no validation, no try/catch, no `CommitAsync`);
  validation in validators; DTOs are records; never return an entity; every endpoint authorized;
  `userId` from the JWT; ownership checks on resource-by-id; `BusinessErrorMessage` codes not inline
  strings; `CancellationToken` propagated.
- Do not commit or push unless the owner asks.

## Example
```
/backend Add a command to update order status, validating allowed transitions
```
