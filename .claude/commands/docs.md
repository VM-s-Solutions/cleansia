# /docs — Update documentation

Bring the VitePress docs, architecture docs, and changelog in sync with shipped behavior.

## Usage
```
/docs <what shipped / what to document>
```

## What it does
Act as the **Docs** agent (`.claude/agents/docs.md`). Update the relevant `docs/**` page so it
matches the code now, fold any accepted ADR into the architecture doc it affects, and add a
changelog entry (Keep a Changelog: Added/Changed/Deprecated/Removed/Fixed/Security). Match the
existing high-quality doc voice; include the example a reader needs; document what's in the code, not
aspirations.

## Rules
- Describe behavior, never change it.
- Do not duplicate the internal `agents/knowledge/*` catalog into the public docs.
- Do not run the VitePress build or deploy — flag it if needed. Do not commit/push unless asked.

## Example
```
/docs Document the new per-employee pay override admin tab
```
