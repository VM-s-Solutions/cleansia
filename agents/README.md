# Agent System

A token-efficient agent workflow for managing multi-platform projects with Claude Code.

## The Problem

Without structure, every Claude Code session burns 50-80% of tokens on codebase exploration. A typical 4-task session uses ~200k tokens when it should use ~50k.

## The Solution: Plan then Execute

```
You (natural language)  →  /plan  →  Task Specs  →  /execute  →  Code changes
                            ↑                         ↑
                     Reads CLAUDE.md            Reads specialist
                     Greps codebase             prompts only
                     ~20k tokens               ~15k tokens/task
```

## Quick Start

### 1. Create CLAUDE.md in your project root

Every project needs a `CLAUDE.md` that describes:
- Tech stack and versions
- Directory structure
- Build/run commands
- Architecture patterns and conventions
- Key entities and relationships

This is the single source of truth that agents read instead of exploring the entire codebase.

### 2. Plan your work

```
/plan

Customer app:
- Optimize Lighthouse performance score (currently 46%)
- Order confirmation email missing extras details
- "View Order Status" button redirects to login

Backend:
- No order status update email being sent
```

The planner investigates the codebase and outputs precise task specs with exact file paths, line numbers, and change descriptions.

### 3. Execute the plan

```
/execute TASK-001
```

Or execute an entire phase:
```
/execute phase 1
```

Or execute everything:
```
/execute all
```

The executor reads the task spec, applies the specialist's coding standards, makes the exact changes, and verifies the build.

## Slash Commands

| Command | Purpose | Token Budget |
|---------|---------|-------------|
| `/plan` | Decompose tasks into precise specs | ~20k |
| `/execute` | Execute one or more task specs | ~15k per small task |
| `/backend` | Direct backend work (skip planning) | ~30k |
| `/frontend` | Direct frontend work (skip planning) | ~30k |
| `/mobile` | Direct mobile work (skip planning) | ~30k |
| `/review` | Code review against standards | ~15k |
| `/sync` | NSwag client regeneration | ~5k |
| `/docs` | Documentation updates | ~10k |
| `/feature` | Full-stack feature (legacy - prefer /plan) | ~100k+ |

## When to Use /plan vs Direct Commands

**Use `/plan` when:**
- You have 2+ tasks to do
- Tasks span multiple apps or layers (backend + frontend)
- You want to minimize token usage
- Tasks have dependencies on each other

**Use direct commands (`/backend`, `/frontend`) when:**
- Single, well-defined task
- You already know exactly what file to change
- Quick fix that doesn't need investigation

## Directory Structure

```
agents/
├── README.md                          # This file
├── config/                            # Agent configuration (YAML)
│   ├── task-planner.yaml
│   ├── orchestrator.yaml
│   ├── backend-specialist.yaml
│   ├── frontend-specialist.yaml
│   ├── mobile-specialist.yaml
│   ├── code-review.yaml
│   ├── code-sync.yaml
│   └── docs.yaml
├── prompts/system/                    # Agent system prompts
│   ├── task-planner.md               # Core - decomposes tasks into specs
│   ├── orchestrator.md               # Routes tasks to specialists
│   ├── backend-specialist.md         # .NET/C#/CQRS conventions
│   ├── frontend-specialist.md        # Angular/Nx/NgRx conventions
│   ├── mobile-specialist.md          # Kotlin/Compose conventions
│   ├── code-review.md                # Quality standards
│   ├── code-sync.md                  # NSwag/model sync
│   └── docs.md                       # Documentation standards

.claude/commands/                      # Slash commands (Claude Code)
├── plan.md                            # /plan - task decomposition
├── execute.md                         # /execute - run task specs
├── backend.md                         # /backend - direct backend work
├── frontend.md                        # /frontend - direct frontend work
├── mobile.md                          # /mobile - direct mobile work
├── review.md                          # /review - code review
├── sync.md                            # /sync - client regeneration
├── docs.md                            # /docs - documentation
└── feature.md                         # /feature - full-stack (legacy)
```

## Multi-Project Usage

This system works with any project. To add a new project:

1. **Create `CLAUDE.md`** in the project root with the project context
2. **Copy `.claude/commands/`** to the new project's `.claude/commands/`
3. **Copy `agents/prompts/system/`** for the relevant specialists
4. Done. The task planner reads `CLAUDE.md` to understand the project.

The specialist prompts (`backend-specialist.md`, `frontend-specialist.md`) contain coding conventions. If the new project uses different conventions, create project-specific variants.

## Token Budget Guide

| Session Type | Expected Tokens | Example |
|-------------|----------------|---------|
| Plan 4 tasks | ~20k | `/plan` with 4 bullet points |
| Execute 1 small task | ~15k | Single file change + build |
| Execute 1 medium task | ~25k | Multi-file change + i18n + build |
| Execute 1 large task | ~40k | New feature with multiple components |
| Full session (plan + execute 4 tasks) | ~80-100k | Typical workday session |
| Direct `/backend` small fix | ~20k | Single handler change |

## Model Selection

The `/plan` command includes a **Model Recommendations** section telling you which model to use for each phase. Switch before executing:

```
/model sonnet          # before /execute phase 1
/model haiku           # before /execute phase 2 (i18n-only)
/model opus            # before /execute TASK-005 (complex architecture)
```

| Model | Cost | Use For |
|-------|------|---------|
| **Haiku** | Cheapest | i18n keys, config edits, single-line fixes, copy-paste tasks |
| **Sonnet** | Mid | 80% of execution tasks — components, handlers, bug fixes, refactors |
| **Opus** | Expensive | **Planning** (`/plan`), complex multi-file architecture, novel design |

**Rule of thumb:**
- **Always plan on Opus** — the planner stays under 20k tokens but its quality determines whether execution wastes tokens or not. A precise Opus plan saves 2-3x its cost in downstream execution.
- If the task spec is so precise it's basically a diff, **execute on Haiku**.
- If you need the agent to make judgment calls, **execute on Sonnet**.
- If the task says "design X from scratch", **execute on Opus**.

## Tips for Minimum Token Usage

1. **Always /plan first** - investigation is cheaper than repeated exploration
2. **Be specific in your task descriptions** - "fix login redirect after password reset on customer app" beats "fix login"
3. **Group related tasks** - "Customer app: A, B, C" not three separate sessions
4. **Use /execute per phase** - don't execute everything at once if phases are independent
5. **Don't re-plan** - if the plan is good, just execute it
6. **Keep CLAUDE.md updated** - stale context = wasted tokens re-discovering structure
7. **Switch models per phase** - use Haiku for i18n, Sonnet for code, Opus only when needed
