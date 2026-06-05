# Cleansia Frontend — Claude Code Guide

This is the Angular 19 / Nx monorepo for the 3 web apps (customer SSR, partner SPA, admin SPA).

**The canonical project guide is the root [`../../CLAUDE.md`](../../CLAUDE.md)** — read it for the
full architecture, conventions, the agent operating system, and the i18n/NSwag/owner-only rules.

## Frontend specifics

- Patterns & conventions for this codebase: [`../../agents/knowledge/patterns-frontend.md`](../../agents/knowledge/patterns-frontend.md)
- Canonical architecture: [`../../docs/architecture/frontend.md`](../../docs/architecture/frontend.md)
- The Frontend Dev charter: [`../../.claude/agents/frontend.md`](../../.claude/agents/frontend.md)

Key rules (full list in the catalog): OnPush + signals, logic in facades not components,
`<cleansia-*>`/PrimeNG (never raw form controls), `TranslatePipe` on every string with keys in all 5
locales, no `any`, three explicit data states. Never run `npm run generate-*-client` or hand-edit the
NSwag-generated clients — that's owner-only; flag `manual_step: nswag-regen`.

## graphify

This project has a graphify knowledge graph at graphify-out/.

Rules:
- Before answering architecture or codebase questions, read graphify-out/GRAPH_REPORT.md for god nodes and community structure
- If graphify-out/wiki/index.md exists, navigate it instead of reading raw files
- After modifying code files in this session, run `python3 -c "from graphify.watch import _rebuild_code; from pathlib import Path; _rebuild_code(Path('.'))"` to keep the graph current
