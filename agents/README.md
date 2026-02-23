# Cleansia Agent System

This directory contains the configuration for the multi-agent automation system for the Cleansia project.

## Overview

The agent system provides specialized AI agents for different aspects of the development workflow:

| Agent | Purpose |
|-------|---------|
| **Orchestrator** | Routes tasks to appropriate specialists |
| **Backend Specialist** | .NET, C#, CQRS, PostgreSQL |
| **Frontend Specialist** | Angular, TypeScript, NgRx, PrimeNG |
| **Mobile Specialist** | Android (Kotlin/Compose), iOS (Swift) |
| **Code Review** | Validates code against CODING_STANDARDS.md |
| **Code Sync** | Synchronizes models between platforms |
| **Docs** | Maintains documentation |

## Directory Structure

```
agents/
├── config/                 # Agent YAML configurations
│   ├── orchestrator.yaml
│   ├── backend-specialist.yaml
│   ├── frontend-specialist.yaml
│   ├── mobile-specialist.yaml
│   ├── code-review.yaml
│   ├── code-sync.yaml
│   └── docs.yaml
├── prompts/
│   └── system/            # System prompts for each agent
│       ├── orchestrator.md
│       ├── backend-specialist.md
│       ├── frontend-specialist.md
│       ├── mobile-specialist.md
│       ├── code-review.md
│       ├── code-sync.md
│       └── docs.md
├── tools/                 # Custom tool definitions (future)
└── README.md
```

## Claude Code Custom Commands

The following slash commands are available in Claude Code:

| Command | Description |
|---------|-------------|
| `/review` | Review code against coding standards |
| `/sync` | Sync models between platforms |
| `/backend` | Work on .NET backend tasks |
| `/frontend` | Work on Angular frontend tasks |
| `/mobile` | Work on Android/iOS mobile tasks |
| `/feature` | Implement a full-stack feature |
| `/docs` | Update documentation |

### Usage Examples

```bash
# Review a specific file
/review src/Cleansia.App/Features/Orders/Commands/CreateOrder.cs

# Sync models to all platforms
/sync all

# Create a backend command
/backend Create a command to update order status

# Create a frontend component
/frontend Create a status badge component

# Implement a full feature
/feature Add employee time tracking
```

## Key Coding Standards

All agents enforce these critical rules:

### Backend (.NET)
- Nested classes for Command/Query structure
- Handlers = happy path only (no validation, no try-catch)
- No CommitAsync in handlers (UoW handles this)
- DTOs are records, not classes
- Extension methods for mapping (ToDto, ToEntity)

### Frontend (Angular)
- Never use enum values in templates
- All text uses translation keys
- Facade pattern for NgRx state access
- OnPush change detection
- Standalone components

### Mobile (Android)
- HiltViewModel for all ViewModels
- StateFlow for UI state
- Navigation via events
- String resources for all text

## Integration Options

### 1. Claude Code CLI
Use the slash commands directly in Claude Code:
```
/review
/sync all
/backend Create a new endpoint
```

### 2. Git Hooks (Future)
```bash
# .git/hooks/pre-commit
claude code "/review staged"
```

### 3. GitHub Actions (Future)
```yaml
- name: Code Review
  run: claude code "/review ${{ github.event.pull_request.number }}"
```

## Configuration

Agent configurations are in YAML format in `config/`. Each config specifies:

- **name** - Agent identifier
- **description** - What the agent does
- **model** - Claude model to use
- **system_prompt_file** - Path to system prompt
- **file_patterns** - Files the agent works with
- **capabilities** - What the agent can do
- **tools** - Available tools/commands

## System Prompts

System prompts in `prompts/system/` contain:

- Role definition
- Technology expertise
- Coding standards to follow
- Example code patterns
- Common tasks
- What NOT to do

## Extending the System

### Adding a New Agent

1. Create `config/new-agent.yaml`
2. Create `prompts/system/new-agent.md`
3. Optionally add `.claude/commands/new-agent.md`

### Adding Custom Tools

Create tool definitions in `tools/` directory for reusable operations.

## Maintenance

Keep agents in sync with:
- `CODING_STANDARDS.md` - Update prompts when standards change
- `CLEANSIA_PROJECT_DOCUMENTATION.md` - Update when architecture changes
- Platform-specific patterns - Update when adopting new patterns
