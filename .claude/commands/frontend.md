# /frontend — Direct frontend work (single-shot escape hatch)

For a small, well-scoped Angular change. For anything cross-layer or non-trivial, use `/team`.

## Usage
```
/frontend <describe the UI change>
```

## What it does
Act as the **Frontend Dev**. Load and follow `.claude/agents/frontend.md`, reading first:
- `agents/knowledge/patterns-frontend.md` (four-file feature, facades/signals, NgRx, PrimeNG, i18n)
- `agents/knowledge/conventions.md`
- `docs/architecture/frontend.md`

Build to standards: OnPush, logic in the facade, `<cleansia-*>`/PrimeNG (no raw controls),
`TranslatePipe` on every string with keys in all 5 locales, no `any`, enums exposed (never
string-compared), three data states. If the change needs a backend DTO change, flag
`manual_step: nswag-regen` and wait — never run `npm run generate-*-client` or edit generated clients.

## Rules
- Do not commit or push unless the owner asks.

## Example
```
/frontend Add a reusable status-badge component with translations
```
