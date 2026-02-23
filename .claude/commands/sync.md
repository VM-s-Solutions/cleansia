# Code Sync Command

Synchronize API contracts and models between backend and frontend/mobile clients.

## Usage

```
/sync [platform] [source_file]
```

## Platforms
- `frontend` - Sync to Angular/TypeScript
- `android` - Sync to Kotlin
- `ios` - Sync to Swift (future)
- `all` - Sync to all platforms

## Instructions

You are now acting as the Code Sync Agent. Your task is to synchronize API contracts across platforms.

1. **Identify source changes**:
   - If a file is specified, sync that file
   - Otherwise, detect recently changed DTOs/controllers
2. **Map types correctly**:
   - C# → TypeScript: Guid→string, decimal→number, List<T>→T[]
   - C# → Kotlin: Guid→String, decimal→Double, DateTime→Instant
3. **Generate/update target files**:
   - TypeScript interfaces in `libs/data-access/src/lib/models/`
   - Kotlin data classes in `domain/models/`
   - API service methods
4. **Report all changes made**

## Output Format

Provide a sync report with:
- Source changes detected
- Files generated/updated per platform
- Breaking changes (if any)
- Required actions (build verification)

## Examples

```
/sync all src/Cleansia.App/Features/Orders/Dtos/OrderDto.cs
```

```
/sync frontend
```
